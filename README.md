# Android PC Controller

A professional Windows desktop application for connecting and controlling Android devices from your PC.

## Features

- **Screen Mirroring** — Real-time Android screen display on your PC
- **Remote Control** — Mouse clicks map to taps, keyboard input sent to device
- **File Transfer** — Upload, download, drag-and-drop files between PC and Android
- **Application Manager** — View, launch, uninstall, clear data for installed apps
- **APK Installer** — Drag APK files to install on connected devices
- **Screenshots** — Capture device screenshots with one click
- **Screen Recording** — Record device screen to MP4 files
- **Clipboard Sync** — Copy/paste between PC and Android
- **ADB Terminal** — Integrated shell for advanced users
- **Multi-Device** — Connect and manage multiple Android devices simultaneously
- **Wireless ADB** — Connect over Wi-Fi without USB cable
- **Game Controls** — Keyboard-to-touch mapping for gaming
- **Diagnostics** — Connection troubleshooting and device health monitoring
- **Dark Theme** — Modern premium dark UI

## Requirements

- Windows 10/11 (x64)
- .NET 9 Runtime
- Android device with USB Debugging enabled
- [Android Platform Tools](https://developer.android.com/tools/releases/platform-tools) (ADB)

## Quick Start

1. Enable **Developer Options** on your Android device
2. Enable **USB Debugging** in Developer Options
3. Connect your device via USB
4. Accept the USB debugging authorization prompt on your phone
5. Launch Android PC Controller

## Building from Source

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Android Studio](https://developer.android.com/studio) (for the Android agent)
- Visual Studio 2022+ (recommended)

### Build the Windows Application

```bash
dotnet restore
dotnet build
dotnet test
```

### Build the Android Agent

The Gradle wrapper is included, so no global Gradle install is required:

```bash
cd android/AndroidPCController.Agent
./gradlew.bat assembleDebug   # Windows
./gradlew assembleDebug       # Linux/macOS
```

Or open `android/AndroidPCController.Agent` in Android Studio and build.

## Project Structure

```
AndroidPCController/
├── src/
│   ├── AndroidPCController.App/          # WPF application (UI)
│   ├── AndroidPCController.Core/         # Interfaces and models
│   ├── AndroidPCController.Adb/          # ADB client implementation
│   ├── AndroidPCController.Devices/      # Device management
│   ├── AndroidPCController.Streaming/    # Screen streaming
│   ├── AndroidPCController.Input/        # Input control
│   ├── AndroidPCController.Files/        # File transfer
│   ├── AndroidPCController.Security/     # Encryption and validation
│   ├── AndroidPCController.Infrastructure/ # Logging, settings
│   └── AndroidPCController.Tests/        # Unit tests (239 tests)
├── android/
│   └── AndroidPCController.Agent/        # Kotlin Android companion app
├── tools/                                # ADB platform-tools
└── docs/                                 # Documentation
```

## Architecture

The application follows a modular clean architecture:

- **Core** — Pure interfaces and models, no dependencies
- **Infrastructure** — Logging, settings, shared utilities
- **Adb** — ADB process management and command execution
- **Devices** — Device sessions and management
- **Streaming** — Screen capture and streaming
- **Input** — Remote input injection
- **Files** — File transfer operations
- **Security** — Encryption, validation, sanitization

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for detailed architecture documentation.

> **Note:** `tools/platform-tools/adb.exe`, `AdbWinApi.dll` and `AdbWinUsbApi.dll` are intentionally
> gitignored (large binaries). If they are missing, download
> [Android platform-tools](https://developer.android.com/tools/releases/platform-tools) and extract
> them into `tools/platform-tools/` (or install ADB on your PATH) before first run.

## Configuration

Settings are stored at `%AppData%/AndroidPCController/config/settings.json`.

Key settings:
- **Streaming** — FPS, resolution, bitrate, codec
- **Connection** — Auto-reconnect, timeout
- **Privacy** — Clipboard sync, notification sync
- **Files** — Default download directory
- **Advanced** — ADB path, debug logging

## Testing

```bash
dotnet test
```

239 unit tests covering:
- Security validation (encryption, path traversal, command injection)
- Settings persistence
- Logging functionality
- Model creation and defaults
- Input controller key mapping

## License

MIT License

## Acknowledgments

Built with:
- [.NET 9](https://dotnet.microsoft.com/) — Windows application framework
- [WPF](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/) — UI framework
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/MVVM) — MVVM toolkit
- [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXAML) — UI theming
- [ADB](https://developer.android.com/tools/adb) — Android Debug Bridge
- [Kotlin](https://kotlinlang.org/) — Android agent language
