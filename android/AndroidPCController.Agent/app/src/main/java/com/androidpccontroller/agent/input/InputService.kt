package com.androidpccontroller.agent.input

import android.accessibilityservice.AccessibilityService
import android.accessibilityservice.GestureDescription
import android.content.Intent
import android.graphics.Path
import android.graphics.Rect
import android.os.Build
import android.os.Bundle
import android.util.Log
import android.view.KeyEvent
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo
import com.androidpccontroller.agent.connection.Protocol
import kotlinx.coroutines.*

class InputService : AccessibilityService() {

    companion object {
        private const val TAG = "InputService"
        private var instance: InputService? = null
        fun getInstance(): InputService? = instance

        fun isRunning(): Boolean = instance != null
    }

    private val scope = CoroutineScope(Dispatchers.Default + SupervisorJob())
    private val commandQueue = kotlinx.coroutines.channels.Channel<Protocol.InputEventMessage>(Channel.BUFFERED)

    override fun onServiceConnected() {
        super.onServiceConnected()
        instance = this
        Log.d(TAG, "Input service connected")

        scope.launch {
            processCommands()
        }
    }

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        // We don't need to process accessibility events for remote input
    }

    override fun onInterrupt() {
        Log.d(TAG, "Input service interrupted")
    }

    override fun onDestroy() {
        super.onDestroy()
        instance = null
        scope.cancel()
    }

    fun executeInputEvent(event: Protocol.InputEventMessage) {
        commandQueue.trySend(event)
    }

    private suspend fun processCommands() {
        for (event in commandQueue) {
            try {
                when (event.eventType) {
                    Protocol.InputEventType.TAP -> handleTap(event)
                    Protocol.InputEventType.DOUBLE_TAP -> handleDoubleTap(event)
                    Protocol.InputEventType.LONG_PRESS -> handleLongPress(event)
                    Protocol.InputEventType.SWIPE -> handleSwipe(event)
                    Protocol.InputEventType.KEY_EVENT -> handleKeyEvent(event)
                    Protocol.InputEventType.TEXT_INPUT -> handleTextInput(event)
                }
            } catch (e: Exception) {
                Log.e(TAG, "Error executing input event: ${e.message}")
            }
        }
    }

    private fun handleTap(event: Protocol.InputEventMessage) {
        val path = Path()
        path.moveTo(event.x, event.y)

        val gesture = GestureDescription.Builder()
            .addStroke(GestureDescription.StrokeDescription(path, 0, 100))
            .build()

        dispatchGesture(gesture, null, null)
    }

    private fun handleDoubleTap(event: Protocol.InputEventMessage) {
        val path1 = Path()
        path1.moveTo(event.x, event.y)
        val gesture1 = GestureDescription.StrokeDescription(path1, 0, 50)

        val path2 = Path()
        path2.moveTo(event.x, event.y)
        val gesture2 = GestureDescription.StrokeDescription(path2, 100, 50)

        val gesture = GestureDescription.Builder()
            .addStroke(gesture1)
            .addStroke(gesture2)
            .build()

        dispatchGesture(gesture, null, null)
    }

    private fun handleLongPress(event: Protocol.InputEventMessage) {
        val path = Path()
        path.moveTo(event.x, event.y)

        val gesture = GestureDescription.Builder()
            .addStroke(GestureDescription.StrokeDescription(path, 0, event.duration.coerceAtLeast(500)))
            .build()

        dispatchGesture(gesture, null, null)
    }

    private fun handleSwipe(event: Protocol.InputEventMessage) {
        val path = Path()
        path.moveTo(event.x, event.y)
        path.lineTo(event.endX, event.endY)

        val gesture = GestureDescription.Builder()
            .addStroke(GestureDescription.StrokeDescription(path, 0, event.duration.coerceAtLeast(300)))
            .build()

        dispatchGesture(gesture, null, null)
    }

    private fun handleKeyEvent(event: Protocol.InputEventMessage) {
        val keyCode = event.keyCode
        val action = if (event.keyEventAction == 0) {
            KeyEvent.ACTION_DOWN
        } else {
            KeyEvent.ACTION_UP
        }

        val keyEvent = KeyEvent(
            System.currentTimeMillis(),
            System.currentTimeMillis(),
            action,
            keyCode,
            0
        )
        dispatchGesture(
            GestureDescription.Builder().build(),
            object : GestureResultCallback() {
                override fun onCompleted(gestureDescription: GestureDescription) {
                    // Key event dispatched
                }
                override fun onCancelled(gestureDescription: GestureDescription) {
                    Log.w(TAG, "Gesture cancelled for key event")
                }
            },
            null
        )

        sendKeyEventViaInput(keyCode, action)
    }

    private fun sendKeyEventViaInput(keyCode: Int, action: Int) {
        try {
            val command = "input keyevent $keyCode"
            Runtime.getRuntime().exec(arrayOf("sh", "-c", command))
        } catch (e: Exception) {
            Log.e(TAG, "Failed to send key event: ${e.message}")
        }
    }

    private fun handleTextInput(event: Protocol.InputEventMessage) {
        val text = event.text
        if (text.isEmpty()) return

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            val arguments = Bundle().apply {
                putCharSequence(
                    AccessibilityNodeInfo.ACTION_ARGUMENT_SET_TEXT_CHARSEQUENCE,
                    text
                )
            }

            val focusedNode = findFocus(AccessibilityNodeInfo.FOCUS_INPUT)
            if (focusedNode != null) {
                focusedNode.performAction(AccessibilityNodeInfo.ACTION_SET_TEXT, arguments)
            } else {
                insertTextViaClipboard(text)
            }
        } else {
            insertTextViaClipboard(text)
        }
    }

    private fun insertTextViaClipboard(text: String) {
        try {
            val clipboard = android.content.ClipboardManager::class.java
            val method = clipboard.getMethod("setPrimaryClip", android.content.ClipData::class.java)
            val clipData = android.content.ClipData.newPlainText("input", text)
            val clipboardManager = getSystemService(CLIPBOARD_SERVICE) as android.content.ClipboardManager
            method.invoke(clipboardManager, clipData)

            Runtime.getRuntime().exec(arrayOf("sh", "-c", "input keyevent 279"))
        } catch (e: Exception) {
            Log.e(TAG, "Failed to insert text: ${e.message}")
        }
    }

    fun getScreenBounds(): Rect {
        val rootNode = rootInActiveWindow
        return if (rootNode != null) {
            val rect = Rect()
            rootNode.getBoundsInScreen(rect)
            rect
        } else {
            val metrics = resources.displayMetrics
            Rect(0, 0, metrics.widthPixels, metrics.heightPixels)
        }
    }

    fun performGlobalActionCompat(action: Int): Boolean {
        return super.performGlobalAction(action)
    }

    fun findNodeByText(text: String): AccessibilityNodeInfo? {
        val rootNode = rootInActiveWindow ?: return null
        val nodes = rootNode.findAccessibilityNodeInfosByText(text)
        return nodes?.firstOrNull()
    }

    fun findNodeById(viewId: String): AccessibilityNodeInfo? {
        val rootNode = rootInActiveWindow ?: return null
        val nodes = rootNode.findAccessibilityNodeInfosByViewId(viewId)
        return nodes?.firstOrNull()
    }
}
