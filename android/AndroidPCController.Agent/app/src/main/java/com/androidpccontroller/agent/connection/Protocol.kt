package com.androidpccontroller.agent.connection

import com.google.gson.Gson
import com.google.gson.GsonBuilder
import com.google.gson.JsonDeserializer
import com.google.gson.JsonSerializer
import com.google.gson.JsonObject
import com.google.gson.JsonParser
import java.lang.reflect.Type

object Protocol {
    const val PROTOCOL_VERSION = 1

    val gson: Gson = GsonBuilder()
        .registerTypeHierarchyAdapter(BaseMessage::class.java, MessageAdapter())
        .create()

    enum class MessageType {
        HELLO,
        HELLO_ACK,
        DEVICE_INFO,
        DEVICE_INFO_RESPONSE,
        CAPABILITIES,
        CAPABILITIES_RESPONSE,
        INPUT_EVENT,
        INPUT_EVENT_ACK,
        SCREEN_STREAM_START,
        SCREEN_STREAM_STOP,
        SCREEN_FRAME,
        SCREEN_FRAME_ACK,
        CLIPBOARD_SYNC,
        CLIPBOARD_SYNC_ACK,
        PING,
        PONG,
        DISCONNECT,
        ERROR,
        COMMAND,
        COMMAND_RESPONSE
    }

    enum class InputEventType {
        TAP,
        DOUBLE_TAP,
        LONG_PRESS,
        SWIPE,
        KEY_EVENT,
        TEXT_INPUT
    }

    enum class Capability {
        SCREEN_CAPTURE,
        INPUT_INJECTION,
        CLIPBOARD_ACCESS,
        FILE_TRANSFER,
        NOTIFICATION_ACCESS,
        DEVICE_INFO
    }

    abstract class BaseMessage {
        abstract val type: MessageType
        val protocolVersion: Int = PROTOCOL_VERSION
        val timestamp: Long = System.currentTimeMillis()
        val messageId: String = java.util.UUID.randomUUID().toString()
    }

    data class HelloMessage(
        val deviceId: String,
        val deviceName: String,
        val agentVersion: String
    ) : BaseMessage() {
        override val type: MessageType = MessageType.HELLO
    }

    data class HelloAckMessage(
        val success: Boolean,
        val sessionId: String,
        val serverVersion: String,
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.HELLO_ACK
    }

    data class DeviceInfoMessage(
        val deviceId: String,
        val deviceName: String,
        val manufacturer: String,
        val model: String,
        val androidVersion: String,
        val apiLevel: Int,
        val screenWidth: Int,
        val screenHeight: Int,
        val screenDensity: Float,
        val batteryLevel: Int,
        val isCharging: Boolean,
        val totalStorage: Long,
        val availableStorage: Long,
        val installedApps: List<AppInfo>
    ) : BaseMessage() {
        override val type: MessageType = MessageType.DEVICE_INFO
    }

    data class AppInfo(
        val packageName: String,
        val versionName: String,
        val versionCode: Long
    )

    data class DeviceInfoResponseMessage(
        val success: Boolean,
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.DEVICE_INFO_RESPONSE
    }

    data class CapabilitiesMessage(
        val capabilities: List<Capability>,
        val supportedInputEvents: List<InputEventType>,
        val maxFrameRate: Int = 30,
        val maxFrameWidth: Int = 1920,
        val maxFrameHeight: Int = 1080
    ) : BaseMessage() {
        override val type: MessageType = MessageType.CAPABILITIES
    }

    data class CapabilitiesResponseMessage(
        val success: Boolean,
        val enabledCapabilities: List<Capability>,
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.CAPABILITIES_RESPONSE
    }

    data class InputEventMessage(
        val eventType: InputEventType,
        val x: Float = 0f,
        val y: Float = 0f,
        val endX: Float = 0f,
        val endY: Float = 0f,
        val keyCode: Int = 0,
        val keyEventAction: Int = 0,
        val text: String = "",
        val duration: Long = 0
    ) : BaseMessage() {
        override val type: MessageType = MessageType.INPUT_EVENT
    }

    data class InputEventAckMessage(
        val success: Boolean,
        val originalMessageId: String,
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.INPUT_EVENT_ACK
    }

    data class ScreenStreamStartMessage(
        val frameRate: Int = 30,
        val quality: Int = 80,
        val maxWidth: Int = 1920,
        val maxHeight: Int = 1080
    ) : BaseMessage() {
        override val type: MessageType = MessageType.SCREEN_STREAM_START
    }

    data class ScreenStreamStopMessage(
        val reason: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.SCREEN_STREAM_STOP
    }

    data class ScreenFrameMessage(
        val frameNumber: Long,
        val width: Int,
        val height: Int,
        val format: String,
        val data: ByteArray,
        val timestamp: Long
    ) : BaseMessage() {
        override val type: MessageType = MessageType.SCREEN_FRAME

        override fun equals(other: Any?): Boolean {
            if (this === other) return true
            if (other !is ScreenFrameMessage) return false
            return frameNumber == other.frameNumber && messageId == other.messageId
        }

        override fun hashCode(): Int {
            return frameNumber.hashCode() * 31 + messageId.hashCode()
        }
    }

    data class ScreenFrameAckMessage(
        val frameNumber: Long,
        val success: Boolean
    ) : BaseMessage() {
        override val type: MessageType = MessageType.SCREEN_FRAME_ACK
    }

    data class ClipboardSyncMessage(
        val content: String,
        val mimeType: String = "text/plain",
        val source: String = "android"
    ) : BaseMessage() {
        override val type: MessageType = MessageType.CLIPBOARD_SYNC
    }

    data class ClipboardSyncAckMessage(
        val success: Boolean,
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.CLIPBOARD_SYNC_ACK
    }

    data class PingMessage(
        val pingId: Long = System.currentTimeMillis()
    ) : BaseMessage() {
        override val type: MessageType = MessageType.PING
    }

    data class PongMessage(
        val pingId: Long,
        val roundTripTime: Long = 0
    ) : BaseMessage() {
        override val type: MessageType = MessageType.PONG
    }

    data class DisconnectMessage(
        val reason: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.DISCONNECT
    }

    data class ErrorMessage(
        val errorCode: Int,
        val errorMessage: String,
        val originalMessageId: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.ERROR
    }

    data class CommandMessage(
        val command: String,
        val arguments: Map<String, String> = emptyMap()
    ) : BaseMessage() {
        override val type: MessageType = MessageType.COMMAND
    }

    data class CommandResponseMessage(
        val success: Boolean,
        val command: String,
        val result: Map<String, Any> = emptyMap(),
        val message: String = ""
    ) : BaseMessage() {
        override val type: MessageType = MessageType.COMMAND_RESPONSE
    }

    private class MessageAdapter : com.google.gson.TypeAdapter<BaseMessage>() {
        private val delegateGson = Gson()

        override fun write(out: com.google.gson.stream.JsonWriter, value: BaseMessage) {
            val jsonTree = delegateGson.toJsonTree(value).asJsonObject
            jsonTree.addProperty("type", value.type.name)
            delegateGson.getAdapter(com.google.gson.JsonObject::class.java).write(out, jsonTree)
        }

        override fun read(`in`: com.google.gson.stream.JsonReader): BaseMessage {
            val jsonElement = JsonParser.parseReader(`in`)
            val jsonObject = jsonElement.asJsonObject
            val typeStr = jsonObject.get("type")?.asString
                ?: throw IllegalArgumentException("Missing 'type' field in message")

            val messageType = try {
                MessageType.valueOf(typeStr)
            } catch (e: IllegalArgumentException) {
                throw IllegalArgumentException("Unknown message type: $typeStr")
            }

            val messageClass = when (messageType) {
                MessageType.HELLO -> HelloMessage::class.java
                MessageType.HELLO_ACK -> HelloAckMessage::class.java
                MessageType.DEVICE_INFO -> DeviceInfoMessage::class.java
                MessageType.DEVICE_INFO_RESPONSE -> DeviceInfoResponseMessage::class.java
                MessageType.CAPABILITIES -> CapabilitiesMessage::class.java
                MessageType.CAPABILITIES_RESPONSE -> CapabilitiesResponseMessage::class.java
                MessageType.INPUT_EVENT -> InputEventMessage::class.java
                MessageType.INPUT_EVENT_ACK -> InputEventAckMessage::class.java
                MessageType.SCREEN_STREAM_START -> ScreenStreamStartMessage::class.java
                MessageType.SCREEN_STREAM_STOP -> ScreenStreamStopMessage::class.java
                MessageType.SCREEN_FRAME -> ScreenFrameMessage::class.java
                MessageType.SCREEN_FRAME_ACK -> ScreenFrameAckMessage::class.java
                MessageType.CLIPBOARD_SYNC -> ClipboardSyncMessage::class.java
                MessageType.CLIPBOARD_SYNC_ACK -> ClipboardSyncAckMessage::class.java
                MessageType.PING -> PingMessage::class.java
                MessageType.PONG -> PongMessage::class.java
                MessageType.DISCONNECT -> DisconnectMessage::class.java
                MessageType.ERROR -> ErrorMessage::class.java
                MessageType.COMMAND -> CommandMessage::class.java
                MessageType.COMMAND_RESPONSE -> CommandResponseMessage::class.java
            }

            return delegateGson.fromJson(jsonObject, messageClass)
        }
    }

    fun toJson(message: BaseMessage): String = gson.toJson(message)

    fun fromJson(json: String): BaseMessage = gson.fromJson(json, BaseMessage::class.java)

    fun createLengthPrefixedMessage(message: BaseMessage): ByteArray {
        val jsonBytes = toJson(message).toByteArray(Charsets.UTF_8)
        val lengthBytes = jsonBytes.size.toByteArrayLittleEndian()
        return lengthBytes + jsonBytes
    }

    fun parseLengthPrefixedMessage(data: ByteArray): BaseMessage? {
        if (data.size < 4) return null
        val length = data.take(4).toByteArray().toIntLittleEndian()
        if (data.size < 4 + length) return null
        val jsonBytes = data.sliceArray(4 until 4 + length)
        return try {
            fromJson(String(jsonBytes, Charsets.UTF_8))
        } catch (e: Exception) {
            null
        }
    }

    private fun Int.toByteArrayLittleEndian(): ByteArray {
        return byteArrayOf(
            (this and 0xFF).toByte(),
            (this shr 8 and 0xFF).toByte(),
            (this shr 16 and 0xFF).toByte(),
            (this shr 24 and 0xFF).toByte()
        )
    }

    private fun ByteArray.toIntLittleEndian(): Int {
        return (this[0].toInt() and 0xFF) or
                (this[1].toInt() and 0xFF shl 8) or
                (this[2].toInt() and 0xFF shl 16) or
                (this[3].toInt() and 0xFF shl 24)
    }
}
