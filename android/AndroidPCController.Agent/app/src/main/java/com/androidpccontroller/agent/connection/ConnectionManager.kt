package com.androidpccontroller.agent.connection

import android.util.Log
import kotlinx.coroutines.*
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.*
import okhttp3.*
import okio.ByteString
import okio.ByteString.Companion.toByteString
import java.io.IOException
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

class ConnectionManager(private val scope: CoroutineScope) {

    companion object {
        private const val TAG = "ConnectionManager"
        private const val INITIAL_BACKOFF_MS = 1000L
        private const val MAX_BACKOFF_MS = 30000L
        private const val BACKOFF_MULTIPLIER = 2.0
        private const val PING_INTERVAL_MS = 15000L
        private const val CONNECT_TIMEOUT_MS = 10000L
        private const val MAX_FRAME_SIZE = 1024 * 1024 * 2
    }

    enum class ConnectionState {
        DISCONNECTED,
        CONNECTING,
        CONNECTED,
        RECONNECTING,
        ERROR
    }

    private data class ConnectionConfig(
        val host: String,
        val port: Int
    )

    private val _connectionState = MutableStateFlow(ConnectionState.DISCONNECTED)
    val connectionState: StateFlow<ConnectionState> = _connectionState.asStateFlow()

    private val _incomingMessages = Channel<Protocol.BaseMessage>(Channel.BUFFERED)
    val incomingMessages: Channel<Protocol.BaseMessage> = _incomingMessages

    private val _outgoingMessages = Channel<Protocol.BaseMessage>(Channel.BUFFERED)

    private val isRunning = AtomicBoolean(false)
    private val currentBackoff = AtomicLong(INITIAL_BACKOFF_MS)
    private val messageCounter = AtomicLong(0)

    private var webSocket: WebSocket? = null
    private var okHttpClient: OkHttpClient? = null
    private var connectionJob: Job? = null
    private var pingJob: Job? = null
    private var receiveJob: Job? = null
    private var writeJob: Job? = null
    private var currentConfig: ConnectionConfig? = null

    private val messageBuffer = mutableListOf<Byte>()
    private val bufferLock = Any()

    fun connect(host: String, port: Int) {
        if (isRunning.getAndSet(true)) {
            Log.w(TAG, "Already connected or connecting")
            return
        }

        currentConfig = ConnectionConfig(host, port)
        currentBackoff.set(INITIAL_BACKOFF_MS)

        okHttpClient = OkHttpClient.Builder()
            .connectTimeout(CONNECT_TIMEOUT_MS, TimeUnit.MILLISECONDS)
            .readTimeout(0, TimeUnit.MILLISECONDS)
            .writeTimeout(30, TimeUnit.MILLISECONDS)
            .pingInterval(PING_INTERVAL_MS, TimeUnit.MILLISECONDS)
            .build()

        connectionJob = scope.launch(Dispatchers.IO) {
            connectInternal(host, port)
        }

        writeJob = scope.launch(Dispatchers.IO) {
            writeLoop()
        }
    }

    fun disconnect() {
        isRunning.set(false)
        connectionJob?.cancel()
        pingJob?.cancel()
        receiveJob?.cancel()
        writeJob?.cancel()

        webSocket?.close(1000, "Client disconnect")
        webSocket = null

        _connectionState.value = ConnectionState.DISCONNECTED

        synchronized(bufferLock) {
            messageBuffer.clear()
        }
    }

    fun sendMessage(message: Protocol.BaseMessage) {
        if (!isRunning.get()) {
            Log.w(TAG, "Cannot send message: not connected")
            return
        }

        scope.launch {
            _outgoingMessages.trySend(message)
        }
    }

    private suspend fun connectInternal(host: String, port: Int) {
        while (isRunning.get()) {
            try {
                _connectionState.value = if (_connectionState.value == ConnectionState.DISCONNECTED) {
                    ConnectionState.CONNECTING
                } else {
                    ConnectionState.RECONNECTING
                }

                Log.d(TAG, "Connecting to $host:$port")

                val url = "ws://$host:$port"

                val request = Request.Builder()
                    .url(url)
                    .build()

                val callback = object : WebSocketListener() {
                    override fun onOpen(webSocket: WebSocket, response: Response) {
                        Log.d(TAG, "Connected to $host:$port")
                        this@ConnectionManager.webSocket = webSocket
                        _connectionState.value = ConnectionState.CONNECTED
                        currentBackoff.set(INITIAL_BACKOFF_MS)

                        startReceiveLoop(webSocket)
                    }

                    override fun onMessage(webSocket: WebSocket, text: String) {
                        handleRawMessage(text.toByteArray(Charsets.UTF_8))
                    }

                    override fun onMessage(webSocket: WebSocket, bytes: ByteString) {
                        handleRawMessage(bytes.toByteArray())
                    }

                    override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                        Log.d(TAG, "Closing: $code $reason")
                        webSocket.close(code, reason)
                    }

                    override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                        Log.d(TAG, "Closed: $code $reason")
                        handleDisconnect()
                    }

                    override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                        Log.e(TAG, "Connection failure: ${t.message}")
                        handleDisconnect()
                    }
                }

                webSocket = okHttpClient?.newWebSocket(request, callback)

                awaitCancellation()
            } catch (e: CancellationException) {
                throw e
            } catch (e: Exception) {
                Log.e(TAG, "Connection error: ${e.message}")
                handleDisconnect()
            }
        }
    }

    private fun handleDisconnect() {
        _connectionState.value = ConnectionState.DISCONNECTED
        webSocket = null

        if (isRunning.get()) {
            scope.launch {
                val backoff = currentBackoff.get()
                Log.d(TAG, "Reconnecting in ${backoff}ms...")
                delay(backoff)
                currentBackoff.set(
                    minOf(
                        (backoff * BACKOFF_MULTIPLIER).toLong(),
                        MAX_BACKOFF_MS
                    )
                )
                val config = currentConfig ?: return@launch
                connectInternal(config.host, config.port)
            }
        }
    }

    private fun startReceiveLoop(webSocket: WebSocket) {
        receiveJob?.cancel()
        receiveJob = scope.launch(Dispatchers.IO) {
            // WebSocket handles receiving via callbacks
            // This coroutine manages the lifecycle
            try {
                awaitCancellation()
            } catch (e: CancellationException) {
                // Normal cancellation
            }
        }
    }

    private fun handleRawMessage(data: ByteArray) {
        try {
            synchronized(bufferLock) {
                messageBuffer.addAll(data.toList())

                while (messageBuffer.size >= 4) {
                    val lengthBytes = messageBuffer.take(4).toByteArray()
                    val expectedLength = lengthBytes[0].toInt() and 0xFF or
                            (lengthBytes[1].toInt() and 0xFF shl 8) or
                            (lengthBytes[2].toInt() and 0xFF shl 16) or
                            (lengthBytes[3].toInt() and 0xFF shl 24)

                    if (expectedLength > MAX_FRAME_SIZE) {
                        Log.e(TAG, "Message too large: $expectedLength")
                        messageBuffer.clear()
                        return
                    }

                    if (messageBuffer.size >= 4 + expectedLength) {
                        val messageBytes = messageBuffer.drop(4).take(expectedLength).toByteArray()
                        messageBuffer.subList(0, 4 + expectedLength).clear()

                        val json = String(messageBytes, Charsets.UTF_8)
                        val message = Protocol.fromJson(json)
                        _incomingMessages.trySend(message)
                    } else {
                        break
                    }
                }
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error parsing message: ${e.message}")
        }
    }

    private suspend fun writeLoop() {
        try {
            for (message in _outgoingMessages) {
                val ws = webSocket ?: continue
                val data = Protocol.createLengthPrefixedMessage(message)
                ws.send(data.toByteString(0, data.size))
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            Log.e(TAG, "Write loop error: ${e.message}")
        }
    }

    fun isConnected(): Boolean = _connectionState.value == ConnectionState.CONNECTED

    fun destroy() {
        disconnect()
        okHttpClient?.dispatcher?.executorService?.shutdown()
        okHttpClient?.connectionPool?.evictAll()
    }
}
