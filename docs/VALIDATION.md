# Windows UI verification

Implementation review date: **2026-09-30**. Application version remains **1.8.0**; this change does not publish a release.

## Build and test gate

Run on Windows with .NET 8 and Windows Desktop support:

```powershell
dotnet restore TypeIt4Me.sln
dotnet build TypeIt4Me.sln -c Release --no-restore
dotnet test TypeIt4Me.sln -c Release --no-build --logger "trx;LogFileName=results.trx" --logger "console;verbosity=normal"
```

The implementation was built and tested on a Windows 11 VM with SDK 8.0.425, reached over SSH from the Linux development host. The Linux host cannot launch WPF or execute its WindowsDesktop testhost. Windows CI remains on GitHub-hosted `windows-latest`; no public PR code is sent to a fleet runner.

The verification results and any remaining limits are recorded below. CI additionally publishes a self-contained win-x64 executable and uploads the TRX and UI review artifacts.

## Deterministic resource and layout review

```powershell
dotnet run --project tools/UiReview/UiReview.csproj -c Release -- ui-review
```

This separate executable loads the same `DesignSystem.xaml` as the application, with existing test doubles and synthetic snippets. It never invokes production startup or writes to the user's normal AppData profile. It fails on recorded WPF binding warnings and checks resource retention, live theme changes, insertion/working/locked states, palette contrast, editor validation/cancellation, size restoration, and virtualization with 1,000 snippets.

Images cover both themes, full/minimum windows, Mini Mode/minimum Mini Mode, populated/empty/search/no-results, unpinned/busy/input-error states, editor/minimum/reference/validation, Settings/minimum/data section, PIN/creation error, Help/minimum/about, input, delete/PIN-removal confirmation, inline editor discard, lock, focus, large collections, and high-contrast resource treatment. Main-window renders at 96/120/144 DPI exercise 100%/125%/150% text and vector rendering.

These are **client-area captures from real WPF windows**. They intentionally omit native title bars, OS shadows, desktop surroundings, and native file-picker windows. The high-contrast capture uses Windows system colors; it does not change the OS accessibility setting. Rendering at three DPI values is not equivalent to moving a live HWND between physical monitors with different scaling.

Images were reviewed in successive passes. Corrections included deferred resource lookup, a read-only hotkey binding, live colors on hidden windows, input padding, minimum-size Mini Mode, accent-button text contrast, and the command-reference affordance. The review host now supplies a DispatcherSynchronizationContext so asynchronous filtering updates its WPF collection view on the correct thread; it checks rendered item counts and service errors as well as binding warnings. Root-panel margins are included in the capture bounds.

## Isolated production smoke

```powershell
dotnet run --project tools/UiReview/UiReview.csproj -c Release -- --startup-smoke startup-smoke
```

Use a **fresh output directory** on an interactive Windows desktop. This mode runs the real App startup, services, native tray icon, DWM integration, and global hotkeys. An internal test-only data-directory hook selects an isolated synthetic profile before constructing services. It does not reset or import a personal profile. A second owned process provides a synthetic text field, and clipboard text is compared without being printed or changed.

The smoke verifies startup, minimize/close-to-tray, tray-command restoration, Settings/Help event wiring, live dark chrome, Mini Mode size restoration, editor saving, encrypted V3 saving, lock redaction, inline incorrect-PIN retry, successful unlock/decryption, foreground restoration, SendInput, Ctrl+Alt+E registration, PIN removal with preserved plaintext snippets, and real import/export round trips. It writes a pass/fail report and binding log, then exits its own processes.

SSH's noninteractive session passed the application/storage checks but Windows denied foreground activation. Running the same smoke on the active console desktop through a temporary scheduled task passed the native input checks. That temporary task was removed afterward. Native input smoke is deliberately excluded from hosted CI, where foreground ownership is unreliable.

## Results

| Check | Windows 11 result |
| --- | --- |
| Release restore / full rebuild | Passed; 0 errors, 13 existing warnings (baseline: 43) |
| Exact no-build TRX test gate | 154 passed, 0 failed |
| WPF resource/layout review | 24 checks passed; 67 client-area PNGs; no binding warnings or fake-service errors |
| Isolated production desktop smoke | 21 checks passed; no binding warnings; 3 additional real-startup PNGs |
| Static review | XAML/project/manifest XML and context JSON parse; palette keys match; diff whitespace check passes |

All 70 current captures and two baseline comparisons are retained in the [screenshot gallery](screenshots/README.md). Screenshots were reviewed in individual windows and contact sheets; scaling images were inspected at their native pixel dimensions. Hosted CI status is reported in the PR after its checks finish.

## Remaining limits and follow-up

- Physical mixed-DPI/multi-monitor dragging, actual OS high-contrast switching, Windows 10/older DWM fallback, and Narrator were not exercised on the single-display Windows 11 VM. Placement and theme precedence have automated coverage; fallback calls are guarded.
- Native file-picker keyboard interaction and actual tray-icon double-click were not manually clicked. Owned pickers use standard Windows dialogs; real import/export and the same restoration command invoked by the tray were exercised.
- Existing persistence follow-up: SettingsManager logs save failures without propagating them, and updating the encrypted snippet file plus PIN metadata is not a two-file transaction. Failed import/save recovery and corrupt collection reporting need a separate storage-focused change. The redesign retains formats and does not claim to solve every disk-failure path.
- Existing build warnings in unchanged hotkey/storage, benchmark, and test code remain; they are not suppressed.
