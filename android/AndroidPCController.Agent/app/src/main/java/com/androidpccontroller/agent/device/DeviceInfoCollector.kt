package com.androidpccontroller.agent.device

import android.annotation.SuppressLint
import android.app.ActivityManager
import android.content.ClipboardManager
import android.content.Context
import android.content.pm.PackageManager
import android.os.BatteryManager
import android.os.Build
import android.os.Environment
import android.os.StatFs
import android.provider.Settings
import android.util.DisplayMetrics
import android.view.WindowManager
import com.androidpccontroller.agent.connection.Protocol

class DeviceInfoCollector(private val context: Context) {

    fun collectDeviceInfo(): Protocol.DeviceInfoMessage {
        val deviceName = getDeviceName()
        val screenResolution = getScreenResolution()
        val batteryInfo = getBatteryInfo()
        val storageInfo = getStorageInfo()
        val installedApps = getInstalledApps()

        return Protocol.DeviceInfoMessage(
            deviceId = com.androidpccontroller.agent.AgentApplication.getDeviceId(),
            deviceName = deviceName,
            manufacturer = Build.MANUFACTURER,
            model = Build.MODEL,
            androidVersion = Build.VERSION.RELEASE,
            apiLevel = Build.VERSION.SDK_INT,
            screenWidth = screenResolution.first,
            screenHeight = screenResolution.second,
            screenDensity = getScreenDensity(),
            batteryLevel = batteryInfo.first,
            isCharging = batteryInfo.second,
            totalStorage = storageInfo.first,
            availableStorage = storageInfo.second,
            installedApps = installedApps
        )
    }

    private fun getDeviceName(): String {
        val deviceName = try {
            Settings.Global.getString(context.contentResolver, Settings.Global.DEVICE_NAME)
        } catch (e: Exception) {
            null
        }
        return deviceName ?: "${Build.MANUFACTURER} ${Build.MODEL}"
    }

    private fun getScreenResolution(): Pair<Int, Int> {
        val windowManager = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            val windowMetrics = windowManager.currentWindowMetrics
            val bounds = windowMetrics.bounds
            Pair(bounds.width(), bounds.height())
        } else {
            @Suppress("DEPRECATION")
            val display = windowManager.defaultDisplay
            val metrics = DisplayMetrics()
            @Suppress("DEPRECATION")
            display.getRealMetrics(metrics)
            Pair(metrics.widthPixels, metrics.heightPixels)
        }
    }

    private fun getScreenDensity(): Float {
        val windowManager = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            val windowMetrics = windowManager.currentWindowMetrics
            val bounds = windowMetrics.bounds
            val densityDpi = context.resources.configuration.densityDpi
            densityDpi.toFloat() / 160f
        } else {
            @Suppress("DEPRECATION")
            val display = windowManager.defaultDisplay
            val metrics = DisplayMetrics()
            @Suppress("DEPRECATION")
            display.getRealMetrics(metrics)
            metrics.density
        }
    }

    private fun getBatteryInfo(): Pair<Int, Boolean> {
        val batteryManager = context.getSystemService(Context.BATTERY_SERVICE) as BatteryManager
        val batteryLevel = batteryManager.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY)
        val isCharging = batteryManager.isCharging
        return Pair(batteryLevel, isCharging)
    }

    private fun getStorageInfo(): Pair<Long, Long> {
        val stat = StatFs(Environment.getDataDirectory().path)
        val totalBytes = stat.totalBytes
        val availableBytes = stat.availableBytes
        return Pair(totalBytes, availableBytes)
    }

    @SuppressLint("QueryPermissionNeeded")
    private fun getInstalledApps(): List<Protocol.AppInfo> {
        val packageManager = context.packageManager
        val packages = packageManager.getInstalledPackages(0)
        return packages.map { packageInfo ->
            Protocol.AppInfo(
                packageName = packageInfo.packageName,
                versionName = packageInfo.versionName ?: "unknown",
                versionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                    packageInfo.longVersionCode
                } else {
                    @Suppress("DEPRECATION")
                    packageInfo.versionCode.toLong()
                }
            )
        }.sortedBy { it.packageName }
    }

    fun getClipboardContent(): String? {
        val clipboardManager = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        val clipData = clipboardManager.primaryClip
        return if (clipData != null && clipData.itemCount > 0) {
            clipData.getItemAt(0).text?.toString()
        } else {
            null
        }
    }

    fun setClipboardContent(text: String) {
        val clipboardManager = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
        val clipData = android.content.ClipData.newPlainText("PC Controller", text)
        clipboardManager.setPrimaryClip(clipData)
    }

    fun getRunningApps(): List<String> {
        val activityManager = context.getSystemService(Context.ACTIVITY_SERVICE) as ActivityManager
        val runningTasks = activityManager.getRunningTasks(20)
        return runningTasks.map { it.topActivity?.packageName ?: "" }.filter { it.isNotEmpty() }
    }

    fun getScreenBrightness(): Int {
        return try {
            Settings.System.getInt(context.contentResolver, Settings.System.SCREEN_BRIGHTNESS)
        } catch (e: Settings.SettingNotFoundException) {
            128
        }
    }

    fun setScreenBrightness(brightness: Int) {
        try {
            Settings.System.putInt(
                context.contentResolver,
                Settings.System.SCREEN_BRIGHTNESS,
                brightness.coerceIn(0, 255)
            )
        } catch (e: SecurityException) {
            // Write permission not granted
        }
    }

    fun isAccessibilityServiceEnabled(): Boolean {
        val accessibilityService = "${context.packageName}/com.androidpccontroller.agent.input.InputService"
        val enabledServices = Settings.Secure.getString(
            context.contentResolver,
            Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES
        ) ?: return false
        return enabledServices.contains(accessibilityService)
    }
}
