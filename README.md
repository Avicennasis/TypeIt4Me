# TypeIt4Me

A lightweight, secure text expansion tool for Windows built with .NET 6 and WPF.

## Features

*   **Text Expansion**: Create shortcuts (e.g., `eml`) that expand into full text (e.g., `my.email@example.com`).
*   **Global Hotkeys**: Trigger snippets anywhere using global hotkeys.
*   **Search**: Built-in search bar with debouncing for fast, responsive filtering.
*   **Themes**: Switch between Dark and Light modes to suit your preference.
*   **Mini Mode**: Collapse the window to a compact view for screen efficiency.

## Security Architecture
TypeIt4Me prioritizes the security of your snippets.

*   **Encryption**: Snippets are encrypted using **AES-256** (Advanced Encryption Standard).
    *   **Version 2 (Current)**: Uses a unique, randomly generated 32-byte salt and 16-byte initialization vector (IV) for *every* file save. This ensures that even if you save the same data twice, the encrypted file will look completely different, preventing pattern analysis.
    *   **Key Derivation**: Your PIN is strengthened using **PBKDF2** (SHA-256) with 600,000 iterations to derive the encryption key (OWASP recommended).
*   **PIN Storage**: Your PIN is **never** stored in plain text. It is hashed using a unique salt and PBKDF2-SHA256 before being saved to `settings.json`.
*   **Memory Safety**: 
    *   The PIN is kept in memory only while the application is running.
    *   Cryptographic keys and sensitive buffers are explicitly zeroed out (scrubbed) from memory immediately after use to mitigate RAM scraping attacks.
*   **Auto-Lock**: Configure an idle timer to automatically lock the application and clear sensitive data from memory when you step away.
*   **Input Injection**: The application uses the Windows `SendInput` API for text expansion, avoiding the system clipboard entirely. This prevents your snippets from appearing in clipboard history managers.

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

## License

This project is licensed under the **MIT License**.

The Application Icon is from [Material Symbols](https://fonts.google.com/icons) by Google, licensed under the **Apache License Version 2.0**.

See [LICENSE](LICENSE) for details.

