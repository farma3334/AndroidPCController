package com.androidpccontroller.agent

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Bundle
import android.text.method.ScrollingMovementMethod
import android.widget.ScrollView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AppCompatActivity
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.lifecycleScope
import com.androidpccontroller.agent.connection.ConnectionManager
import com.androidpccontroller.agent.permissions.PermissionManager
import com.androidpccontroller.agent.streaming.ScreenCaptureService
import com.google.android.material.button.MaterialButton
import com.google.android.material.textfield.TextInputEditText
import android.widget.TextView
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.*

class MainActivity : AppCompatActivity() {

    companion object {
        private const val TAG = "MainActivity"
        private const val PREFS_NAME = "agent_prefs"
        private const val KEY_HOST = "pc_host"
        private const val KEY_PORT = "pc_port"
    }

    private lateinit var permissionManager: PermissionManager

    private lateinit var etPcAddress: TextInputEditText
    private lateinit var etPcPort: TextInputEditText
    private lateinit var tvConnectionStatus: TextView
    private lateinit var tvDeviceId: TextView
    private lateinit var tvNotificationPermission: TextView
    private lateinit var tvAccessibilityStatus: TextView
    private lateinit var tvMediaProjectionStatus: TextView
    private lateinit var tvStorageStatus: TextView
    private lateinit var tvStreamingStatus: TextView
    private lateinit var tvLog: TextView
    private lateinit var btnToggleService: MaterialButton
    private lateinit var btnGrantNotification: MaterialButton
    private lateinit var btnEnableAccessibility: MaterialButton
    private lateinit var btnRequestScreenCapture: MaterialButton

    private val projectionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            AgentService.setProjectionResult(result.resultCode, result.data!!)
            updatePermissionStatus()
            appendLog("Screen capture permission granted")

            val host = etPcAddress.text.toString()
            val port = etPcPort.text.toString().toIntOrNull() ?: 9100
            startAgentService(host, port)
        } else {
            appendLog("Screen capture permission denied")
            Toast.makeText(this, "Screen capture permission denied", Toast.LENGTH_SHORT).show()
        }
    }

    private val notificationPermissionLauncher = registerForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        updatePermissionStatus()
        if (granted) {
            appendLog("Notification permission granted")
        } else {
            appendLog("Notification permission denied")
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        permissionManager = PermissionManager(this)

        initViews()
        loadSavedPreferences()
        setupClickListeners()
        updatePermissionStatus()
        updateDeviceId()

        observeServiceState()
        observeConnectionState()
    }

    override fun onResume() {
        super.onResume()
        updatePermissionStatus()
        updateStreamingStatus()
    }

    private fun initViews() {
        etPcAddress = findViewById(R.id.etPcAddress)
        etPcPort = findViewById(R.id.etPcPort)
        tvConnectionStatus = findViewById(R.id.tvConnectionStatus)
        tvDeviceId = findViewById(R.id.tvDeviceId)
        tvNotificationPermission = findViewById(R.id.tvNotificationPermission)
        tvAccessibilityStatus = findViewById(R.id.tvAccessibilityStatus)
        tvMediaProjectionStatus = findViewById(R.id.tvMediaProjectionStatus)
        tvStorageStatus = findViewById(R.id.tvStorageStatus)
        tvStreamingStatus = findViewById(R.id.tvStreamingStatus)
        tvLog = findViewById(R.id.tvLog)
        btnToggleService = findViewById(R.id.btnToggleService)
        btnGrantNotification = findViewById(R.id.btnGrantNotification)
        btnEnableAccessibility = findViewById(R.id.btnEnableAccessibility)
        btnRequestScreenCapture = findViewById(R.id.btnRequestScreenCapture)

        tvLog.movementMethod = ScrollingMovementMethod()
    }

    private fun loadSavedPreferences() {
        val prefs = getSharedPreferences(PREFS_NAME, MODE_PRIVATE)
        val savedHost = prefs.getString(KEY_HOST, "")
        val savedPort = prefs.getInt(KEY_PORT, 9100)

        etPcAddress.setText(savedHost)
        etPcPort.setText(savedPort.toString())
    }

    private fun savePreferences() {
        val prefs = getSharedPreferences(PREFS_NAME, MODE_PRIVATE)
        prefs.edit().apply {
            putString(KEY_HOST, etPcAddress.text.toString())
            putInt(KEY_PORT, etPcPort.text.toString().toIntOrNull() ?: 9100)
            apply()
        }
    }

    private fun setupClickListeners() {
        btnToggleService.setOnClickListener {
            savePreferences()
            val host = etPcAddress.text.toString()
            val port = etPcPort.text.toString().toIntOrNull()

            if (host.isBlank()) {
                Toast.makeText(this, "Please enter PC address", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            if (port == null || port < 1 || port > 65535) {
                Toast.makeText(this, "Please enter valid port", Toast.LENGTH_SHORT).show()
                return@setOnClickListener
            }

            if (AgentService.getInstance() != null) {
                stopAgentService()
            } else {
                requestPermissionsAndStart(host, port)
            }
        }

        btnGrantNotification.setOnClickListener {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                notificationPermissionLauncher.launch(Manifest.permission.POST_NOTIFICATIONS)
            } else {
                Toast.makeText(this, "Notification permission not required", Toast.LENGTH_SHORT).show()
            }
        }

        btnEnableAccessibility.setOnClickListener {
            permissionManager.openAccessibilitySettings()
        }

        btnRequestScreenCapture.setOnClickListener {
            requestScreenCapturePermission()
        }
    }

    private fun requestPermissionsAndStart(host: String, port: Int) {
        val permissionsToRequest = mutableListOf<String>()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (!permissionManager.hasNotificationPermission()) {
                permissionsToRequest.add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }

        if (permissionsToRequest.isNotEmpty()) {
            ActivityCompat.requestPermissions(
                this,
                permissionsToRequest.toTypedArray(),
                PermissionManager.REQUEST_NOTIFICATION_PERMISSION
            )
        } else {
            requestScreenCapturePermission()
        }
    }

    private fun requestScreenCapturePermission() {
        val mediaProjectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        try {
            projectionLauncher.launch(mediaProjectionManager.createScreenCaptureIntent())
        } catch (e: Exception) {
            appendLog("Error requesting screen capture: ${e.message}")
            Toast.makeText(this, "Screen capture not supported", Toast.LENGTH_SHORT).show()
        }
    }

    override fun onRequestPermissionsResult(
        requestCode: Int,
        permissions: Array<out String>,
        grantResults: IntArray
    ) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        when (requestCode) {
            PermissionManager.REQUEST_NOTIFICATION_PERMISSION -> {
                updatePermissionStatus()
                if (grantResults.all { it == PackageManager.PERMISSION_GRANTED }) {
                    requestScreenCapturePermission()
                }
            }
            PermissionManager.REQUEST_STORAGE_PERMISSION -> {
                updatePermissionStatus()
            }
        }
    }

    private fun startAgentService(host: String, port: Int) {
        val intent = Intent(this, AgentService::class.java).apply {
            action = AgentService.ACTION_START
            putExtra(AgentService.EXTRA_HOST, host)
            putExtra(AgentService.EXTRA_PORT, port)
        }
        startForegroundService(intent)
        btnToggleService.text = getString(R.string.btn_stop_service)
        appendLog("Service starting...")
    }

    private fun stopAgentService() {
        val intent = Intent(this, AgentService::class.java).apply {
            action = AgentService.ACTION_STOP
        }
        startService(intent)
        btnToggleService.text = getString(R.string.btn_start_service)
        appendLog("Service stopping...")
    }

    private fun observeServiceState() {
        lifecycleScope.launch {
            val service = AgentService.getInstance()
            service?.serviceState?.collectLatest { state ->
                runOnUiThread {
                    when (state) {
                        AgentService.ServiceState.IDLE -> {
                            btnToggleService.text = getString(R.string.btn_start_service)
                            tvConnectionStatus.text = getString(R.string.status_disconnected)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.disconnected_red)
                            )
                        }
                        AgentService.ServiceState.CONNECTING -> {
                            btnToggleService.text = getString(R.string.btn_stop_service)
                            tvConnectionStatus.text = getString(R.string.status_connecting)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.accent)
                            )
                        }
                        AgentService.ServiceState.CONNECTED -> {
                            btnToggleService.text = getString(R.string.btn_stop_service)
                            tvConnectionStatus.text = getString(R.string.status_connected)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.connected_green)
                            )
                        }
                        AgentService.ServiceState.STREAMING -> {
                            btnToggleService.text = getString(R.string.btn_stop_service)
                            tvConnectionStatus.text = getString(R.string.status_streaming)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.connected_green)
                            )
                        }
                        AgentService.ServiceState.ERROR -> {
                            btnToggleService.text = getString(R.string.btn_start_service)
                            tvConnectionStatus.text = "Error"
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.disconnected_red)
                            )
                        }
                    }
                }
            }
        }
    }

    private fun observeConnectionState() {
        lifecycleScope.launch {
            val service = AgentService.getInstance()
            service?.getConnectionManager()?.connectionState?.collectLatest { state ->
                runOnUiThread {
                    when (state) {
                        ConnectionManager.ConnectionState.CONNECTED -> {
                            tvConnectionStatus.text = getString(R.string.status_connected)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.connected_green)
                            )
                        }
                        ConnectionManager.ConnectionState.CONNECTING,
                        ConnectionManager.ConnectionState.RECONNECTING -> {
                            tvConnectionStatus.text = getString(R.string.status_connecting)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.accent)
                            )
                        }
                        ConnectionManager.ConnectionState.DISCONNECTED -> {
                            tvConnectionStatus.text = getString(R.string.status_disconnected)
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.disconnected_red)
                            )
                        }
                        ConnectionManager.ConnectionState.ERROR -> {
                            tvConnectionStatus.text = "Connection Error"
                            tvConnectionStatus.setTextColor(
                                ContextCompat.getColor(this@MainActivity, R.color.disconnected_red)
                            )
                        }
                    }
                }
            }
        }
    }

    private fun updatePermissionStatus() {
        val hasNotification = permissionManager.hasNotificationPermission()
        val hasAccessibility = permissionManager.isAccessibilityServiceEnabled()
        val hasStorage = permissionManager.hasStoragePermission()

        tvNotificationPermission.text = if (hasNotification) {
            getString(R.string.granted)
        } else {
            getString(R.string.not_granted)
        }
        tvNotificationPermission.setTextColor(
            ContextCompat.getColor(
                this,
                if (hasNotification) R.color.connected_green else R.color.disconnected_red
            )
        )

        tvAccessibilityStatus.text = if (hasAccessibility) {
            getString(R.string.enabled)
        } else {
            getString(R.string.disabled)
        }
        tvAccessibilityStatus.setTextColor(
            ContextCompat.getColor(
                this,
                if (hasAccessibility) R.color.connected_green else R.color.disconnected_red
            )
        )

        tvStorageStatus.text = if (hasStorage) {
            getString(R.string.granted)
        } else {
            getString(R.string.not_granted)
        }
        tvStorageStatus.setTextColor(
            ContextCompat.getColor(
                this,
                if (hasStorage) R.color.connected_green else R.color.disconnected_red
            )
        )

        tvMediaProjectionStatus.text = "Check at runtime"
        tvMediaProjectionStatus.setTextColor(
            ContextCompat.getColor(this, R.color.accent)
        )
    }

    private fun updateStreamingStatus() {
        val isStreaming = ScreenCaptureService.getInstance()?.isStreaming?.value ?: false
        tvStreamingStatus.text = if (isStreaming) {
            getString(R.string.status_streaming)
        } else {
            getString(R.string.status_not_streaming)
        }
        tvStreamingStatus.setTextColor(
            ContextCompat.getColor(
                this,
                if (isStreaming) R.color.connected_green else R.color.disconnected_red
            )
        )
    }

    private fun updateDeviceId() {
        tvDeviceId.text = AgentApplication.getDeviceId()
    }

    private fun appendLog(message: String) {
        val timestamp = SimpleDateFormat("HH:mm:ss", Locale.getDefault()).format(Date())
        val logMessage = "[$timestamp] $message\n"

        runOnUiThread {
            tvLog.append(logMessage)

            val scrollView = tvLog.parent as? ScrollView
            scrollView?.post {
                scrollView.fullScroll(ScrollView.FOCUS_DOWN)
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        savePreferences()
    }
}
