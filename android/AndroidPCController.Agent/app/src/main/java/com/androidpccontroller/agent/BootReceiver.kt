package com.androidpccontroller.agent

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build
import android.util.Log

class BootReceiver : BroadcastReceiver() {

    companion object {
        private const val TAG = "BootReceiver"
    }

    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED) {
            Log.d(TAG, "Boot completed, checking if auto-start is enabled")

            val prefs = context.getSharedPreferences("agent_prefs", Context.MODE_PRIVATE)
            val autoStart = prefs.getBoolean("auto_start", false)

            if (autoStart) {
                Log.d(TAG, "Auto-start enabled, starting AgentService")
                val serviceIntent = Intent(context, AgentService::class.java)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    context.startForegroundService(serviceIntent)
                } else {
                    context.startService(serviceIntent)
                }
            }
        }
    }
}
