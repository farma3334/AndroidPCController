# Security

## Overview

Android PC Controller implements security at multiple layers to protect user data and device integrity.

## Encryption

- **AES-256-CBC** encryption for sensitive configuration data
- PBKDF2 key derivation with 100,000 iterations
- Machine-specific encryption keys

## Input Validation

### File Paths
- All file paths are normalized using `Path.GetFullPath`
- Traversal sequences (`../`) are rejected
- Invalid path characters are detected

### Package Names
- Validated against Android package naming rules
- Only alphanumeric characters, underscores, and dots allowed
- Must start with a letter

### Shell Commands
- Dangerous commands are blocked (e.g., `rm -rf /`, `su`, `reboot`)
- User input is sanitized before command construction
- Null bytes and control characters are stripped

## Device Security

- ADB authorization is never bypassed
- Device identity is verified using SHA-256 hashes
- Secure tokens are generated using `RandomNumberGenerator`
- Wireless pairing codes are not stored insecurely

## Privacy Controls

Users can configure:
- Clipboard synchronization (on/off)
- Notification synchronization (on/off)
- Usage analytics (on/off)
- Crash reports (on/off)
- Device history (on/off)

## Data Storage

- Settings stored at `%AppData%/AndroidPCController/config/`
- Sensitive data encrypted at rest
- Logs do not contain passwords or tokens
- Clipboard history is not stored by default

## Network Security

- ADB ports are not exposed to the network
- Wireless connections use ADB's built-in authentication
- No arbitrary network listeners are opened

## Best Practices

- Never store credentials in plain text
- Validate all external input
- Use least privilege principle
- Log security events without exposing sensitive data
- Provide clear error messages without revealing system internals
