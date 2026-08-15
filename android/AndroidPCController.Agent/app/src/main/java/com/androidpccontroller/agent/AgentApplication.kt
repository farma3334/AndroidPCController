package com.androidpccontroller.agent

import android.app.Application
import android.app.NotificationChannel
import android.app.NotificationManager
import android.os.Build
import java.util.UUID

class AgentApplication : Application() {

    companion object {
        lateinit var instance: AgentApplication
            private set

        const val AGENT_SERVICE_CHANNEL = "agent_service_channel"
        const val SCREEN_CAPTURE_CHANNEL = "screen_capture_channel"

        private const val PREFS_NAME = "agent_prefs"
        private const val KEY_DEVICE_ID = "device_id"

        fun getDeviceId(): String {
            val prefs = instance.getSharedPreferences(PREFS_NAME, MODE_PRIVATE)
            var deviceId = prefs.getString(KEY_DEVICE_ID, null)
            if (deviceId == null) {
                deviceId = UUID.randomUUID().toString().replace("-", "").take(16)
                prefs.edit().putString(KEY_DEVICE_ID, deviceId).apply()
            }
            return deviceId
        }
    }

    override fun onCreate() {
        super.onCreate()
        instance = this
        createNotificationChannels()
    }

    private fun createNotificationChannels() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val notificationManager = getSystemService(NotificationManager::class.java)

            val serviceChannel = NotificationChannel(
                AGENT_SERVICE_CHANNEL,
                "Agent Service",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "PC Controller Agent background service"
                setShowBadge(false)
            }

            val screenCaptureChannel = NotificationChannel(
                SCREEN_CAPTURE_CHANNEL,
                "Screen Capture",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Screen capture for PC streaming"
                setShowBadge(false)
            }

            notificationManager.createNotificationChannels(
                listOf(serviceChannel, screenCaptureChannel)
            )
        }
    }
}
