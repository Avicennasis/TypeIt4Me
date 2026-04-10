# TypeIt4Me

[![CI](https://github.com/Avicennasis/TypeIt4Me/actions/workflows/ci.yml/badge.svg)](https://github.com/Avicennasis/TypeIt4Me/actions/workflows/ci.yml)

A lightweight, secure text expansion tool for Windows built with .NET 8 and WPF.

## Features

*   **Text Expansion**: Create shortcuts (e.g., `eml`) that expand into full text (e.g., `my.email@example.com`).
*   **Global Hotkeys**: Trigger snippets anywhere using global hotkeys.
*   **Search**: Built-in search bar with debouncing for fast, responsive filtering.
*   **Themes**: Switch between Dark and Light modes to suit your preference.
*   **Mini Mode**: Collapse the window to a compact view for screen efficiency.

## Security Architecture
TypeIt4Me prioritizes the security of your snippets with enterprise-grade cryptography.

*   **Encryption**: Snippets are encrypted using **AES-256-CBC** with **HMAC-SHA256 authentication** (Authenticated Encryption).
    *   **Version 3 (Current)**:
        - Uses **AES-256** encryption with randomly generated 32-byte salt and 16-byte IV per save
        - **HMAC-SHA256** authentication prevents tampering and detects corrupted data
        - Separate derived keys for encryption and authentication (defense-in-depth)
        - Each save generates unique encrypted output, preventing pattern analysis attacks
    *   **Key Derivation**: Your PIN is strengthened using **PBKDF2-SHA256** with **600,000 iterations** (OWASP 2024 recommendation) to derive encryption and authentication keys
*   **PIN Storage**:
    *   Your PIN is **never** stored in plain text
    *   Hashed using PBKDF2-SHA256 with a unique, randomly generated 32-byte salt
    *   Minimum 4-character PIN enforced, 6+ characters recommended
    *   Stored securely in `settings.json` alongside its unique salt
*   **Memory Safety**:
    *   Cryptographic keys and sensitive buffers are explicitly zeroed from memory immediately after use
    *   Helps mitigate RAM scraping and memory dump attacks
    *   **Note**: PIN remains in memory as a .NET string during session (immutable string limitation)
*   **Input Validation**:
    *   Maximum snippet size enforced (100 KB) to prevent denial-of-service attacks
    *   Auto-lock timeout validated (0-1440 minutes)
    *   Cryptographic buffer length validation prevents buffer overflow attacks
*   **Auto-Lock**: Configure an idle timer (1-1440 minutes) to automatically lock the application when you step away
*   **Input Injection**: Uses Windows `SendInput` API for text expansion, completely avoiding the system clipboard. This prevents snippets from appearing in clipboard history managers or being logged by clipboard monitoring tools.

## File Locations
*   **Snippets**: `%AppData%\TypeIt4Me\snippets.json` (Encrypted if PIN is set)
*   **Settings**: `%AppData%\TypeIt4Me\settings.json`
*   **Portable**: Can be run as a standalone executable.

## Installation

1.  Download the latest release.
2.  Extract the `Dist` folder.
3.  Run `TypeIt4Me.exe`.

## Usage

1.  **Add**: Click `+ New Snippet`, enter content, save.
2.  **Use**: Type your hotkey or use the Play (▶) button.
3.  **Config**: Click the Menu (☰) for Settings (Themes, Auto-Lock), PIN management, and Import/Export.
4.  **Tray**: The app minimizes to the system tray. Double-click the tray icon or use the hotkey to restore.

## Special Keys & Commands

TypeIt4Me supports special key presses and commands using curly bracket syntax. These can be combined with regular text in your snippets.

### Navigation Keys
| Command | Key | Aliases |
|---------|-----|---------|
| `{TAB}` | Tab | |
| `{ENTER}` | Enter | `{RETURN}` |
| `{ESC}` | Escape | `{ESCAPE}` |
| `{BACKSPACE}` | Backspace | |
| `{DELETE}` | Delete | `{DEL}` |
| `{INSERT}` | Insert | `{INS}` |
| `{HOME}` | Home | |
| `{END}` | End | |
| `{PAGEUP}` | Page Up | `{PGUP}` |
| `{PAGEDOWN}` | Page Down | `{PGDN}` |
| `{SPACE}` | Space | |

### Arrow Keys
| Command | Key | Aliases |
|---------|-----|---------|
| `{UP}` | Arrow Up | `{ARROWUP}` |
| `{DOWN}` | Arrow Down | `{ARROWDOWN}` |
| `{LEFT}` | Arrow Left | `{ARROWLEFT}` |
| `{RIGHT}` | Arrow Right | `{ARROWRIGHT}` |

### Modifier Keys
| Command | Key | Aliases |
|---------|-----|---------|
| `{SHIFT}` | Shift | |
| `{CTRL}` | Control | `{CONTROL}` |
| `{ALT}` | Alt | |
| `{WINKEY}` | Windows Key | `{WIN}`, `{LWIN}`, `{RWIN}` |

### Toggle & Special Keys
| Command | Key | Aliases |
|---------|-----|---------|
| `{CAPSLOCK}` | Caps Lock | `{CAPS}` |
| `{NUMLOCK}` | Num Lock | |
| `{SCROLLLOCK}` | Scroll Lock | |
| `{PRINTSCREEN}` | Print Screen | `{PRTSC}` |

### Function Keys
`{F1}` through `{F12}` - All function keys are supported.

### Sleep Command
Use `{SLEEP <milliseconds>}` to pause between actions (1ms to 60,000ms):
- `{SLEEP 500}` - Pause for half a second
- `{SLEEP 2000}` - Pause for 2 seconds

### Example Snippets
```
Hello{TAB}World{ENTER}
```
Types "Hello", presses Tab, types "World", then presses Enter.

```
{CTRL}a{CTRL}c{TAB}{CTRL}v
```
Select all, copy, tab to next field, paste.

```
Starting...{SLEEP 1000}Done!
```
Types "Starting...", waits 1 second, then types "Done!".

## License
This project is licensed under the MIT License.

The Application Icon is from [Material Symbols](https://fonts.google.com/icons) by Google, licensed under the **Apache License Version 2.0**.

See [LICENSE](LICENSE) for details.

## Credits
**Author:** Léon "Avic" Simmons ([@Avicennasis](https://github.com/Avicennasis))

