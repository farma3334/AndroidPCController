package com.androidpccontroller.agent.permissions

import android.Manifest
import android.app.Activity
import android.app.ActivityManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat

class PermissionManager(private val context: Context) {

    companion object {
        const val REQUEST_NOTIFICATION_PERMISSION = 1001
        const val REQUEST_SCREEN_CAPTURE = 1002
        const val REQUEST_STORAGE_PERMISSION = 1003
    }

    fun hasNotificationPermission(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.POST_NOTIFICATIONS
            ) == PackageManager.PERMISSION_GRANTED
        } else {
            true
        }
    }

    fun isAccessibilityServiceEnabled(): Boolean {
        val serviceName = ComponentName(context, "com.androidpccontroller.agent.input.InputService")
        val enabledServices = Settings.Secure.getString(
            context.contentResolver,
            Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES
        ) ?: return false

        val colonSplitter = StringSplitter(':')
        for (service in colonSplitter.split(enabledServices)) {
            if (ComponentName.unflattenFromString(service) == serviceName) {
                return true
            }
        }
        return false
    }

    fun openAccessibilitySettings() {
        val intent = Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        context.startActivity(intent)
    }

    fun requestNotificationPermission(activity: Activity) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            ActivityCompat.requestPermissions(
                activity,
                arrayOf(Manifest.permission.POST_NOTIFICATIONS),
                REQUEST_NOTIFICATION_PERMISSION
            )
        }
    }

    fun hasStoragePermission(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            StorageEnvironmentHelper.isExternalStorageManager()
        } else {
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.READ_EXTERNAL_STORAGE
            ) == PackageManager.PERMISSION_GRANTED
        }
    }

    fun requestStoragePermission(activity: Activity) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            val intent = Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION).apply {
                data = Uri.parse("package:${context.packageName}")
                addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            }
            context.startActivity(intent)
        } else {
            ActivityCompat.requestPermissions(
                activity,
                arrayOf(Manifest.permission.READ_EXTERNAL_STORAGE),
                REQUEST_STORAGE_PERMISSION
            )
        }
    }

    fun hasMediaProjectionPermission(): Boolean {
        val mediaProjectionManager = context.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        return false
    }

    fun createScreenCaptureIntent(): Intent {
        val mediaProjectionManager = context.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        return mediaProjectionManager.createScreenCaptureIntent()
    }

    fun isServiceRunning(serviceClass: Class<*>): Boolean {
        val activityManager = context.getSystemService(Context.ACTIVITY_SERVICE) as ActivityManager
        val runningServices = activityManager.getRunningServices(Integer.MAX_VALUE)
        return runningServices.any {
            it.service.className == serviceClass.name
        }
    }

    fun getAllPermissionsStatus(): Map<String, Boolean> {
        return mapOf(
            "notification" to hasNotificationPermission(),
            "accessibility" to isAccessibilityServiceEnabled(),
            "storage" to hasStoragePermission(),
            "media_projection" to false
        )
    }

    fun getAccessibilityServiceCommand(): String {
        return "am start -a android.settings.ACCESSIBILITY_SETTINGS"
    }

    private class StringSplitter(private val delimiter: Char) {
        fun split(input: String): List<String> {
            return input.split(delimiter).filter { it.isNotEmpty() }
        }
    }
}

private object StorageEnvironmentHelper {
    fun isExternalStorageManager(): Boolean {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            android.os.Environment.isExternalStorageManager()
        } else {
            true
        }
    }
}
