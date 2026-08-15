# Architecture

## Overview

Android PC Controller is built with a modular clean architecture that separates concerns into distinct layers.

## Dependency Graph

```
AndroidPCController.App (WPF)
├── AndroidPCController.Core (Interfaces + Models)
├── AndroidPCController.Infrastructure (Logging, Settings)
├── AndroidPCController.Adb (ADB client)
├── AndroidPCController.Devices (Device management)
├── AndroidPCController.Streaming (Screen streaming)
├── AndroidPCController.Input (Input injection)
├── AndroidPCController.Files (File transfer)
└── AndroidPCController.Security (Encryption, validation)
```

## Project Responsibilities

### Core (No dependencies)

Pure interfaces and data models. No implementation logic.

```
Core/
├── Interfaces/
│   ├── IAdbClient.cs              # ADB process interaction
│   ├── IDeviceManager.cs          # Device lifecycle management
│   ├── IDeviceSession.cs          # Per-device session
│   ├── IScreenStreamer.cs         # Screen capture/streaming
│   ├── IInputController.cs        # Remote input injection
│   ├── IFileTransferService.cs    # File upload/download
│   ├── IApplicationManager.cs     # App management
│   ├── IClipboardService.cs       # Clipboard sync
│   ├── IScreenshotService.cs      # Screenshot capture
│   ├── IScreenRecorder.cs         # Screen recording
│   ├── IDiagnosticsService.cs     # Connection diagnostics
│   ├── ILogService.cs             # Application logging
│   ├── ISettingsService.cs        # Configuration
│   └── ISecurityService.cs        # Encryption/validation
├── Models/
│   ├── DeviceInfo.cs              # Device properties
│   ├── DeviceCapabilities.cs      # Device feature flags
│   ├── InputEvent.cs              # Input events
│   ├── FileInfo.cs                # File information
│   ├── AppInfo.cs                 # Application information
│   ├── GameProfile.cs             # Gaming profiles
│   ├── AutomationStep.cs          # Automation scripts
│   ├── ProtocolMessage.cs         # Communication protocol
│   └── AppSettings.cs             # Settings model
```

### ADB (References: Core)

Real ADB implementation using `System.Diagnostics.Process` to execute `adb.exe`.

- Device enumeration via `adb devices -l`
- Property retrieval via `getprop`
- Shell command execution
- File push/pull
- Screen capture via `screencap`
- Input injection via `input` command
- App management via `pm` command
- Wireless debugging via `adb connect/pair`
- Device polling for connection state changes

### Devices (References: Core, Adb)

Device lifecycle management and session handling.

- `DeviceManager` — Manages multiple device sessions
- `DeviceSession` — Per-device state with all service implementations
- Creates concrete instances of InputController, FileTransferService, etc.
- Handles connection/disconnection events

### Streaming (References: Core)

Screen capture and frame delivery.

- `ScreenStreamer` — Frame-by-frame capture via ADB screencap
- Adjustable FPS and resolution
- Frame metadata (dimensions, timestamp, key frame flag)
- Error recovery for failed captures

### Input (References: Core)

Remote input injection.

- `InputController` — ADB shell `input` commands
- Tap, swipe, long press, pinch gestures
- Key events (Home, Back, Recent)
- Text input
- Mouse movement and scroll
- Screen rotation

### Files (References: Core)

File transfer operations.

- `FileTransferService` — ADB push/pull
- Directory listing with metadata
- Progress tracking
- Upload/download with cancellation
- Rename, delete, create directory

### Security (References: Core)

- AES-256-CBC encryption for sensitive data
- Path traversal prevention
- Package name validation
- Command injection prevention
- Input sanitization
- Secure token generation
- Device identity verification

### Infrastructure (References: Core)

- `LogService` — Thread-safe circular log buffer (10K entries)
- `SettingsService` — JSON settings with read/write lock

## Communication Protocol

Version 1 protocol between PC and Android agent:

```
Message Format:
{
  "version": 1,
  "messageType": "HELLO",
  "requestId": "uuid",
  "timestamp": "2026-01-01T00:00:00Z",
  "payload": { ... }
}
```

### Message Types

| Type | Direction | Purpose |
|------|-----------|---------|
| HELLO | Both | Initial handshake |
| DEVICE_INFO | Android → PC | Device properties |
| CAPABILITIES | Android → PC | Feature flags |
| START_STREAM | PC → Android | Begin screen capture |
| STOP_STREAM | PC → Android | Stop screen capture |
| SCREEN_FRAME | Android → PC | Encoded screen frame |
| INPUT_EVENT | PC → Android | Touch/key event |
| CLIPBOARD_UPDATE | Both | Clipboard content |
| FILE_REQUEST | PC → Android | File operation request |
| FILE_RESPONSE | Android → PC | File operation result |
| PING | PC → Android | Keep-alive |
| PONG | Android → PC | Keep-alive response |
| COMMAND | PC → Android | Shell command |
| RESPONSE | Android → PC | Command result |

## Data Flow

### Screen Streaming
```
Android MediaProjection
  → JPEG/PNG encoding
  → TCP/WebSocket transport
  → PC FrameReceived event
  → WPF Image control
```

### Input Injection
```
PC Mouse/Keyboard event
  → Coordinate transformation
  → InputController.SendTapAsync
  → ADB shell "input tap X Y"
  → Android input system
```

### File Transfer
```
PC Drag-and-drop file
  → FileTransferService.UploadFileAsync
  → ADB push to temp location
  → Move to destination
  → Progress events
```

## Design Patterns

- **MVVM** — ViewModels with CommunityToolkit.Mvvm
- **Dependency Injection** — Microsoft.Extensions.DependencyInjection
- **Observer** — Events for state changes
- **Strategy** — Interchangeable service implementations
- **Factory** — Device session creation

## Error Handling

Every operation provides meaningful error messages with:
- What went wrong
- Possible causes
- Suggested solutions
- Retry/diagnostics actions

## Thread Safety

- `ConcurrentDictionary` for device sessions
- `SemaphoreSlim` for ADB command serialization
- `ReaderWriterLockSlim` for settings access
- `Interlocked` for atomic counters
- All UI updates via dispatcher

## Resource Management

All disposable resources are properly managed:
- `IAsyncDisposable` on all services
- `CancellationToken` support throughout
- Graceful shutdown on application exit
- Cleanup of temp files and processes
