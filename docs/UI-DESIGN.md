# Interface design and implementation

The 2026-09-30 redesign makes insertion the primary action. A quiet sage and teal identity, a compact floating library, and readable local-data explanations support daily use over another application. It uses existing WPF and MVVM rather than a new UI framework.

## Shared visual system

| Concern | Implementation |
| --- | --- |
| Resource entry point | `Views/Resources/DesignSystem.xaml`, shared by the app and UI review |
| Typography | `Tokens.xaml`: Segoe UI Variable Text / Segoe UI; Cascadia Mono / Consolas for content and hotkeys |
| Type scale | 12 caption, 14 body, 16 section, 24 heading; 13 for supporting copy |
| Spacing | 8 / 16 / 24 DIP scale, with smaller optical adjustments inside rows |
| Corners | 6 DIP controls, 12 DIP cards; optional native rounded window corners |
| Colors | Matching semantic keys in LightTheme, DarkTheme, HighContrastTheme |
| Components | `Controls.xaml`: buttons, icon toggles, inputs, radio buttons, checkboxes, focus rings, cards, menus, expanders, scrollbars |
| Icon family | Original 16-unit stroke geometries in `Icons.xaml`, rendered by `SymbolIcon` |
| Elevation | Native DWM window shadow; surfaces separated by borders, without simulated glass or glow |
| Interaction states | Hover/pressed surfaces; selection tint and border; keyboard focus ring; disabled controls; text plus error color; destructive confirmations default to Cancel |
| Motion | No decorative animation or custom transition; the UI does not require animations to be enabled |

Body, supporting, accent-button, and error palette combinations are checked for at least 4.5:1 contrast by the Windows review tool. Decoration and disabled controls are not represented as a comprehensive WCAG certification.

`ThemeService` changes palette brushes while preserving shared styles and icon resources. Mutable root brushes update existing and hidden windows. High contrast follows `SystemParameters.HighContrast`; system-color resources take precedence over the saved light/dark preference. Normal themes are explicitly chosen in Settings rather than automatically following the Windows dark-mode preference.

## Windows and workflows

- **Main:** 420 × 620 DIP by default, minimum 320 × 400. Native `WindowChrome` provides dragging, resizing, double-click behavior, and Alt+Space. Search precedes a virtualized, recycling ListBox; the row's main button inserts, with quieter edit and more actions.
- **Mini Mode:** 300 × 300 DIP by default, minimum 268 × 200. Keeps the title/drag region, pin state, expand, minimize, close, search, compact names/hotkeys, insertion arrows, and result status. F2, Delete, Shift+F10, Ctrl+N, Ctrl+, and F1 preserve access to other workflows.
- **Editor:** name, optional category and captured global hotkey, multiline content, count/limit, expandable syntax reference, validation, dirty state, Save/Cancel. Enter stays multiline in content, Tab leaves fields, Ctrl+S saves, and cancellation preserves the original object. Unsaved cancellation uses an inline discard panel, with Keep editing as the default, rather than stacking another modal window.
- **Settings:** modeless singleton with appearance, window/tray, privacy/locking, auto-lock, and data sections. Controls operate real commands. Long content scrolls while the footer stays accessible.
- **PIN:** creation with confirmation and minimum-length validation; entry with an inline retry error and disabled controls while checking. Short work areas can scroll the form and reach its actions. Changing and removing PIN protection authenticate the current PIN.
- **Confirmation/input:** shared themed windows, native owned file pickers, explicit destructive language, conventional default/cancel actions.
- **Help:** keyboard access, accurate command semantics, Mini Mode/tray usage, local-data and memory limits, version, source link, and attribution.

Full and Mini Mode dimensions are stored separately in optional settings fields. Placement is clamped to the nearest monitor work area, including negative coordinates and removed monitors. The manifest requests PerMonitorV2 DPI awareness, with a PerMonitor fallback. Layout rounding and device-pixel snapping are enabled.

`WindowAppearance` requests DWM dark chrome and rounded corners. Unsupported attributes simply leave the opaque WPF surface usable. Native integrations follow Microsoft's [rounded-window guidance](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/apply-rounded-corners) and [DWM attribute definitions](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute). No backdrop dependency is required.

## Architecture and scoped fixes

The app remains .NET 8 WPF with CommunityToolkit.Mvvm and Hardcodet.NotifyIcon.Wpf. No runtime package was added. Commands and view-model state drive behavior; view code handles Windows lifecycle, focus, hotkey capture, and dialog ownership.

The redesign also corrects these interaction defects:

- Locking redacts results, blocks direct command invocation, and unregisters snippet hotkeys; a cancelled tray unlock stays locked.
- Data-operation leases defer clearing the encryption PIN until an active save/import/export finishes, while immediately hiding snippets. Unlock waits for that operation to finish.
- Modal windows and overlapping insertions cannot trigger another insertion. Focus tracking excludes every window from this process.
- Encrypted import owns its PIN buffer until asynchronous decryption finishes, then clears it.
- Editing refreshes an active search without requiring a collection change; searching includes content/hotkeys and trims the query. Enter waits for current results.
- Bulk collection updates notify Count and the indexer before Reset, keeping empty/result states accurate.
- Custom auto-lock no longer requests a PIN; invalid drafts cannot alter the saved timeout.
- Missing PIN salt reports a metadata problem without resetting files.
- Add/delete save errors propagate to the UI. Failed deletion restores its row and reports an error.

Encryption algorithms and file formats, the input-injection implementation, and the hotkey registration implementation were retained. Display-only snippet properties are `[JsonIgnore]`; existing settings use defaults for new placement fields.

## Asset provenance

`Views/Resources/Icons.xaml` contains original TypeIt4Me geometry under the repository's MIT license. `TypeIt4Me.ico` is the existing Google Material Symbols application icon, under Apache 2.0; its attribution is retained in `LICENSE`, README, and Help. Windows supplies the fonts; no font files or decorative raster assets are shipped. Documentation PNGs are genuine WPF captures with synthetic data, not product assets or mockups.
