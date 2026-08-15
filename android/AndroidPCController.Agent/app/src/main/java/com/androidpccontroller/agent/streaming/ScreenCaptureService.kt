package com.androidpccontroller.agent.streaming

import android.app.Activity
import android.app.Notification
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.PixelFormat
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.Image
import android.media.ImageReader
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Handler
import android.os.IBinder
import android.os.Looper
import android.util.DisplayMetrics
import android.util.Log
import android.view.WindowManager
import androidx.core.app.NotificationCompat
import com.androidpccontroller.agent.AgentApplication
import com.androidpccontroller.agent.AgentService
import com.androidpccontroller.agent.MainActivity
import com.androidpccontroller.agent.R
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.io.ByteArrayOutputStream
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

class ScreenCaptureService : Service() {

    companion object {
        private const val TAG = "ScreenCaptureService"
        private const val NOTIFICATION_ID = 2001
        private const val VIRTUAL_DISPLAY_NAME = "PCControllerCapture"

        private var instance: ScreenCaptureService? = null
        fun getInstance(): ScreenCaptureService? = instance

        fun start(context: Context, resultCode: Int, data: Intent) {
            val intent = Intent(context, ScreenCaptureService::class.java).apply {
                putExtra("resultCode", resultCode)
                putExtra("data", data)
            }
            context.startForegroundService(intent)
        }

        fun stop(context: Context) {
            val intent = Intent(context, ScreenCaptureService::class.java)
            context.stopService(intent)
        }
    }

    private var mediaProjection: MediaProjection? = null
    private var virtualDisplay: VirtualDisplay? = null
    private var imageReader: ImageReader? = null
    private val handler = Handler(Looper.getMainLooper())
    private val scope = CoroutineScope(Dispatchers.Default + SupervisorJob())
    private val isCapturing = AtomicBoolean(false)
    private val frameCounter = AtomicLong(0)

    private var captureWidth = 1280
    private var captureHeight = 720
    private var captureDpi = 320
    private var captureFrameRate = 30
    private var captureQuality = 80

    private val _isStreaming = MutableStateFlow(false)
    val isStreaming: StateFlow<Boolean> = _isStreaming.asStateFlow()

    var frameCallback: ((ByteArray, Int, Int) -> Unit)? = null

    private val captureRunnable = object : Runnable {
        override fun run() {
            if (isCapturing.get()) {
                captureFrame()
                handler.postDelayed(this, 1000L / captureFrameRate)
            }
        }
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        instance = this
        startForeground(NOTIFICATION_ID, createNotification("Screen capture initializing"))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val resultCode = intent?.getIntExtra("resultCode", Activity.RESULT_CANCELED)
            ?: Activity.RESULT_CANCELED
        val data = intent?.getParcelableExtra<Intent>("data")

        if (resultCode != Activity.RESULT_OK || data == null) {
            Log.e(TAG, "Invalid screen capture permission result")
            stopSelf()
            return START_NOT_STICKY
        }

        getScreenDimensions()
        startCapture(resultCode, data)

        return START_STICKY
    }

    private fun getScreenDimensions() {
        val windowManager = getSystemService(Context.WINDOW_SERVICE) as WindowManager
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
            val windowMetrics = windowManager.currentWindowMetrics
            val bounds = windowMetrics.bounds
            captureWidth = bounds.width()
            captureHeight = bounds.height()
        } else {
            @Suppress("DEPRECATION")
            val display = windowManager.defaultDisplay
            val metrics = DisplayMetrics()
            @Suppress("DEPRECATION")
            display.getRealMetrics(metrics)
            captureWidth = metrics.widthPixels
            captureHeight = metrics.heightPixels
        }
        captureDpi = resources.configuration.densityDpi

        val scale = 1280f / maxOf(captureWidth, captureHeight)
        if (scale < 1f) {
            captureWidth = (captureWidth * scale).toInt()
            captureHeight = (captureHeight * scale).toInt()
        }
    }

    private fun startCapture(resultCode: Int, data: Intent) {
        val mediaProjectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager

        try {
            mediaProjection = mediaProjectionManager.getMediaProjection(resultCode, data)

            mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                override fun onStop() {
                    Log.d(TAG, "MediaProjection stopped")
                    stopCapture()
                }
            }, handler)

            imageReader = ImageReader.newInstance(
                captureWidth,
                captureHeight,
                PixelFormat.RGBA_8888,
                2
            )

            imageReader?.setOnImageAvailableListener({ reader ->
                if (isCapturing.get()) {
                    val image = reader.acquireLatestImage()
                    if (image != null) {
                        processImage(image)
                        image.close()
                    }
                }
            }, handler)

            virtualDisplay = mediaProjection?.createVirtualDisplay(
                VIRTUAL_DISPLAY_NAME,
                captureWidth,
                captureHeight,
                captureDpi,
                DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                imageReader?.surface,
                null,
                handler
            )

            isCapturing.set(true)
            _isStreaming.value = true
            startForeground(NOTIFICATION_ID, createNotification("Streaming screen to PC"))

            handler.post(captureRunnable)

            Log.d(TAG, "Screen capture started: ${captureWidth}x${captureHeight} @ ${captureFrameRate}fps")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start screen capture: ${e.message}")
            stopSelf()
        }
    }

    private fun processImage(image: Image) {
        try {
            val planes = image.planes
            val buffer = planes[0].buffer
            val pixelStride = planes[0].pixelStride
            val rowStride = planes[0].rowStride
            val rowPadding = rowStride - pixelStride * captureWidth

            val bitmap = Bitmap.createBitmap(
                captureWidth + rowPadding / pixelStride,
                captureHeight,
                Bitmap.Config.ARGB_8888
            )
            bitmap.copyPixelsFromBuffer(buffer)

            val croppedBitmap = if (bitmap.width != captureWidth || bitmap.height != captureHeight) {
                Bitmap.createBitmap(bitmap, 0, 0, captureWidth, captureHeight).also {
                    bitmap.recycle()
                }
            } else {
                bitmap
            }

            val outputStream = ByteArrayOutputStream()
            croppedBitmap.compress(Bitmap.CompressFormat.JPEG, captureQuality, outputStream)
            val jpegData = outputStream.toByteArray()

            croppedBitmap.recycle()

            val frameNumber = frameCounter.incrementAndGet()
            frameCallback?.invoke(jpegData, captureWidth, captureHeight)

        } catch (e: Exception) {
            Log.e(TAG, "Error processing image: ${e.message}")
        }
    }

    fun captureFrame(): ByteArray? {
        if (!isCapturing.get()) return null

        val image = imageReader?.acquireLatestImage() ?: return null

        return try {
            val planes = image.planes
            val buffer = planes[0].buffer
            val pixelStride = planes[0].pixelStride
            val rowStride = planes[0].rowStride
            val rowPadding = rowStride - pixelStride * captureWidth

            val bitmap = Bitmap.createBitmap(
                captureWidth + rowPadding / pixelStride,
                captureHeight,
                Bitmap.Config.ARGB_8888
            )
            bitmap.copyPixelsFromBuffer(buffer)

            val croppedBitmap = if (bitmap.width != captureWidth || bitmap.height != captureHeight) {
                Bitmap.createBitmap(bitmap, 0, 0, captureWidth, captureHeight).also {
                    bitmap.recycle()
                }
            } else {
                bitmap
            }

            val outputStream = ByteArrayOutputStream()
            croppedBitmap.compress(Bitmap.CompressFormat.JPEG, captureQuality, outputStream)
            val jpegData = outputStream.toByteArray()

            croppedBitmap.recycle()
            jpegData
        } catch (e: Exception) {
            Log.e(TAG, "Error capturing frame: ${e.message}")
            null
        } finally {
            image.close()
        }
    }

    fun updateFrameRate(fps: Int) {
        captureFrameRate = fps.coerceIn(1, 60)
    }

    fun updateQuality(quality: Int) {
        captureQuality = quality.coerceIn(10, 100)
    }

    private fun stopCapture() {
        isCapturing.set(false)
        _isStreaming.value = false
        handler.removeCallbacks(captureRunnable)

        virtualDisplay?.release()
        virtualDisplay = null

        imageReader?.close()
        imageReader = null

        mediaProjection?.stop()
        mediaProjection = null
    }

    private fun createNotification(contentText: String): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, AgentApplication.SCREEN_CAPTURE_CHANNEL)
            .setContentTitle("PC Controller - Screen Capture")
            .setContentText(contentText)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    override fun onDestroy() {
        super.onDestroy()
        stopCapture()
        scope.cancel()
        instance = null
    }
}
