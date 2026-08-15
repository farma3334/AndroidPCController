package com.androidpccontroller.agent

import android.app.Notification
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import androidx.core.app.NotificationCompat
import com.androidpccontroller.agent.connection.ConnectionManager
import com.androidpccontroller.agent.connection.Protocol
import com.androidpccontroller.agent.device.DeviceInfoCollector
import com.androidpccontroller.agent.input.InputService
import com.androidpccontroller.agent.streaming.ScreenCaptureService
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import java.util.concurrent.atomic.AtomicBoolean

class AgentService : Service() {

    companion object {
        private const val TAG = "AgentService"
        private const val NOTIFICATION_ID = 1001

        private var instance: AgentService? = null
        fun getInstance(): AgentService? = instance

        private var pendingProjectionResult: Int = 0
        private var pendingProjectionData: Intent? = null

        fun setProjectionResult(resultCode: Int, data: Intent) {
            pendingProjectionResult = resultCode
            pendingProjectionData = data
        }

        const val ACTION_START = "com.androidpccontroller.agent.START"
        const val ACTION_STOP = "com.androidpccontroller.agent.STOP"
        const val ACTION_SEND_CLIPBOARD = "com.androidpccontroller.agent.SEND_CLIPBOARD"
        const val EXTRA_HOST = "host"
        const val EXTRA_PORT = "port"
        const val EXTRA_CLIPBOARD_TEXT = "clipboard_text"
    }

    private lateinit var connectionManager: ConnectionManager
    private lateinit var deviceInfoCollector: DeviceInfoCollector
    private val scope = CoroutineScope(Dispatchers.Default + SupervisorJob())
    private val isRunning = AtomicBoolean(false)
    private var wakeLock: PowerManager.WakeLock? = null
    private var currentHost = ""
    private var currentPort = 9100

    private val _serviceState = MutableStateFlow(ServiceState.IDLE)
    val serviceState: StateFlow<ServiceState> = _serviceState.asStateFlow()

    enum class ServiceState {
        IDLE,
        CONNECTING,
        CONNECTED,
        STREAMING,
        ERROR
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        instance = this

        deviceInfoCollector = DeviceInfoCollector(this)
        connectionManager = ConnectionManager(scope)

        startForeground(NOTIFICATION_ID, createNotification("Agent service started"))

        observeConnection()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_START -> {
                val host = intent.getStringExtra(EXTRA_HOST) ?: return START_NOT_STICKY
                val port = intent.getIntExtra(EXTRA_PORT, 9100)
                startService(host, port)
            }
            ACTION_STOP -> {
                stopService()
            }
            ACTION_SEND_CLIPBOARD -> {
                val text = intent.getStringExtra(EXTRA_CLIPBOARD_TEXT)
                if (text != null) {
                    sendClipboardContent(text)
                }
            }
        }

        return START_STICKY
    }

    private fun startService(host: String, port: Int) {
        if (isRunning.getAndSet(true)) {
            Log.w(TAG, "Service already running")
            return
        }

        currentHost = host
        currentPort = port

        acquireWakeLock()
        connectionManager.connect(host, port)

        scope.launch {
            processIncomingMessages()
        }

        _serviceState.value = ServiceState.CONNECTING
        updateNotification("Connecting to PC...")
    }

    private fun stopService() {
        isRunning.set(false)

        if (ScreenCaptureService.getInstance() != null) {
            ScreenCaptureService.stop(this)
        }

        connectionManager.disconnect()
        releaseWakeLock()
        _serviceState.value = ServiceState.IDLE

        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    private fun observeConnection() {
        scope.launch {
            connectionManager.connectionState.collect { state ->
                when (state) {
                    ConnectionManager.ConnectionState.CONNECTED -> {
                        _serviceState.value = ServiceState.CONNECTED
                        updateNotification("Connected to PC")
                        sendHello()
                    }
                    ConnectionManager.ConnectionState.DISCONNECTED,
                    ConnectionManager.ConnectionState.RECONNECTING -> {
                        _serviceState.value = ServiceState.CONNECTING
                        updateNotification("Reconnecting...")
                    }
                    ConnectionManager.ConnectionState.CONNECTING -> {
                        _serviceState.value = ServiceState.CONNECTING
                        updateNotification("Connecting...")
                    }
                    ConnectionManager.ConnectionState.ERROR -> {
                        _serviceState.value = ServiceState.ERROR
                        updateNotification("Connection error")
                    }
                }
            }
        }
    }

    private suspend fun processIncomingMessages() {
        for (message in connectionManager.incomingMessages) {
            try {
                handleMessage(message)
            } catch (e: Exception) {
                Log.e(TAG, "Error handling message: ${e.message}")
            }
        }
    }

    private fun handleMessage(message: Protocol.BaseMessage) {
        when (message) {
            is Protocol.HelloAckMessage -> {
                Log.d(TAG, "Hello acknowledged: ${message.message}")
                if (message.success) {
                    sendDeviceInfo()
                    sendCapabilities()
                }
            }

            is Protocol.InputEventMessage -> {
                val inputService = InputService.getInstance()
                inputService?.executeInputEvent(message)

                connectionManager.sendMessage(
                    Protocol.InputEventAckMessage(
                        success = true,
                        originalMessageId = message.messageId
                    )
                )
            }

            is Protocol.ScreenStreamStartMessage -> {
                startScreenStreaming(message)
            }

            is Protocol.ScreenStreamStopMessage -> {
                stopScreenStreaming()
            }

            is Protocol.ClipboardSyncMessage -> {
                deviceInfoCollector.setClipboardContent(message.content)
                connectionManager.sendMessage(
                    Protocol.ClipboardSyncAckMessage(
                        success = true,
                        message = "Clipboard updated"
                    )
                )
            }

            is Protocol.PingMessage -> {
                connectionManager.sendMessage(
                    Protocol.PongMessage(
                        pingId = message.pingId
                    )
                )
            }

            is Protocol.DisconnectMessage -> {
                Log.d(TAG, "Server requested disconnect: ${message.reason}")
                stopService()
            }

            is Protocol.CommandMessage -> {
                handleCommand(message)
            }

            else -> {
                Log.w(TAG, "Unhandled message type: ${message.type}")
            }
        }
    }

    private fun sendHello() {
        val hello = Protocol.HelloMessage(
            deviceId = AgentApplication.getDeviceId(),
            deviceName = deviceInfoCollector.collectDeviceInfo().deviceName,
            agentVersion = "1.0.0"
        )
        connectionManager.sendMessage(hello)
    }

    private fun sendDeviceInfo() {
        val deviceInfo = deviceInfoCollector.collectDeviceInfo()
        connectionManager.sendMessage(deviceInfo)
    }

    private fun sendCapabilities() {
        val capabilities = Protocol.CapabilitiesMessage(
            capabilities = listOf(
                Protocol.Capability.SCREEN_CAPTURE,
                Protocol.Capability.INPUT_INJECTION,
                Protocol.Capability.CLIPBOARD_ACCESS,
                Protocol.Capability.DEVICE_INFO
            ),
            supportedInputEvents = listOf(
                Protocol.InputEventType.TAP,
                Protocol.InputEventType.DOUBLE_TAP,
                Protocol.InputEventType.LONG_PRESS,
                Protocol.InputEventType.SWIPE,
                Protocol.InputEventType.KEY_EVENT,
                Protocol.InputEventType.TEXT_INPUT
            ),
            maxFrameRate = 30,
            maxFrameWidth = 1920,
            maxFrameHeight = 1080
        )
        connectionManager.sendMessage(capabilities)
    }

    private fun startScreenStreaming(message: Protocol.ScreenStreamStartMessage) {
        val pendingData = pendingProjectionData
        val pendingResult = pendingProjectionResult

        if (pendingData != null) {
            ScreenCaptureService.start(this, pendingResult, pendingData)
            _serviceState.value = ServiceState.STREAMING

            val screenCapture = ScreenCaptureService.getInstance()
            screenCapture?.updateFrameRate(message.frameRate)
            screenCapture?.updateQuality(message.quality)

            screenCapture?.frameCallback = { frameData, width, height ->
                val frameNumber = System.currentTimeMillis()
                val screenFrame = Protocol.ScreenFrameMessage(
                    frameNumber = frameNumber,
                    width = width,
                    height = height,
                    format = "jpeg",
                    data = frameData,
                    timestamp = System.currentTimeMillis()
                )
                connectionManager.sendMessage(screenFrame)
            }

            updateNotification("Streaming screen")
        } else {
            Log.w(TAG, "No projection data available")
            connectionManager.sendMessage(
                Protocol.ErrorMessage(
                    errorCode = 1001,
                    errorMessage = "Screen capture permission not granted"
                )
            )
        }
    }

    private fun stopScreenStreaming() {
        ScreenCaptureService.stop(this)
        _serviceState.value = ServiceState.CONNECTED
        updateNotification("Connected to PC (not streaming)")
    }

    private fun handleCommand(message: Protocol.CommandMessage) {
        val result = when (message.command) {
            "get_device_info" -> {
                val info = deviceInfoCollector.collectDeviceInfo()
                mapOf(
                    "deviceName" to info.deviceName,
                    "manufacturer" to info.manufacturer,
                    "model" to info.model,
                    "androidVersion" to info.androidVersion,
                    "apiLevel" to info.apiLevel,
                    "screenWidth" to info.screenWidth,
                    "screenHeight" to info.screenHeight,
                    "batteryLevel" to info.batteryLevel
                )
            }
            "get_clipboard" -> {
                val clipboard = deviceInfoCollector.getClipboardContent() ?: ""
                mapOf("clipboard" to clipboard)
            }
            "set_clipboard" -> {
                val text = message.arguments["text"] ?: ""
                deviceInfoCollector.setClipboardContent(text)
                mapOf("success" to true)
            }
            "get_running_apps" -> {
                mapOf("apps" to deviceInfoCollector.getRunningApps())
            }
            "get_brightness" -> {
                mapOf("brightness" to deviceInfoCollector.getScreenBrightness())
            }
            "set_brightness" -> {
                val brightness = message.arguments["value"]?.toIntOrNull() ?: 128
                deviceInfoCollector.setScreenBrightness(brightness)
                mapOf("success" to true)
            }
            "take_screenshot" -> {
                val frame = ScreenCaptureService.getInstance()?.captureFrame()
                if (frame != null) {
                    mapOf(
                        "success" to true,
                        "data_size" to frame.size
                    )
                } else {
                    mapOf("success" to false, "error" to "Screen capture not available")
                }
            }
            "lock_screen" -> {
                val inputService = InputService.getInstance()
                if (inputService != null) {
                    inputService.performGlobalActionCompat(android.accessibilityservice.AccessibilityService.GLOBAL_ACTION_LOCK_SCREEN)
                    mapOf("success" to true)
                } else {
                    mapOf("success" to false, "error" to "Accessibility service not enabled")
                }
            }
            "home" -> {
                val inputService = InputService.getInstance()
                if (inputService != null) {
                    inputService.performGlobalActionCompat(android.accessibilityservice.AccessibilityService.GLOBAL_ACTION_HOME)
                    mapOf("success" to true)
                } else {
                    mapOf("success" to false, "error" to "Accessibility service not enabled")
                }
            }
            "back" -> {
                val inputService = InputService.getInstance()
                if (inputService != null) {
                    inputService.performGlobalActionCompat(android.accessibilityservice.AccessibilityService.GLOBAL_ACTION_BACK)
                    mapOf("success" to true)
                } else {
                    mapOf("success" to false, "error" to "Accessibility service not enabled")
                }
            }
            "recent_apps" -> {
                val inputService = InputService.getInstance()
                if (inputService != null) {
                    inputService.performGlobalActionCompat(android.accessibilityservice.AccessibilityService.GLOBAL_ACTION_RECENTS)
                    mapOf("success" to true)
                } else {
                    mapOf("success" to false, "error" to "Accessibility service not enabled")
                }
            }
            else -> {
                mapOf("success" to false, "error" to "Unknown command: ${message.command}")
            }
        }

        connectionManager.sendMessage(
            Protocol.CommandResponseMessage(
                success = true,
                command = message.command,
                result = result,
                message = "Command executed"
            )
        )
    }

    fun sendClipboardContent(text: String) {
        connectionManager.sendMessage(
            Protocol.ClipboardSyncMessage(
                content = text,
                mimeType = "text/plain",
                source = "android"
            )
        )
    }

    fun getConnectionManager(): ConnectionManager = connectionManager

    private fun acquireWakeLock() {
        val powerManager = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = powerManager.newWakeLock(
            PowerManager.PARTIAL_WAKE_LOCK,
            "PCController:AgentServiceWakeLock"
        ).apply {
            acquire(60 * 60 * 1000L) // 1 hour max
        }
    }

    private fun releaseWakeLock() {
        wakeLock?.let {
            if (it.isHeld) {
                it.release()
            }
        }
        wakeLock = null
    }

    private fun updateNotification(text: String) {
        val notification = createNotification(text)
        val notificationManager = getSystemService(Context.NOTIFICATION_SERVICE) as android.app.NotificationManager
        notificationManager.notify(NOTIFICATION_ID, notification)
    }

    private fun createNotification(text: String): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )

        val stopIntent = PendingIntent.getService(
            this,
            1,
            Intent(this, AgentService::class.java).apply { action = ACTION_STOP },
            PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, AgentApplication.AGENT_SERVICE_CHANNEL)
            .setContentTitle("PC Controller Agent")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentIntent(pendingIntent)
            .addAction(android.R.drawable.ic_media_pause, "Stop", stopIntent)
            .setOngoing(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    override fun onDestroy() {
        super.onDestroy()
        stopService()
        scope.cancel()
        instance = null
    }
}
