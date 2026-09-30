# Changelog

All notable changes to `TypeIt4Me` will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Shared WPF visual system with light, dark, and Windows high-contrast palettes, original vector icons, visible focus, and themed controls.
- Floating snippet library with whole-row insertion, richer search, clear empty/locked/error states, keyboard actions, and purposeful Mini Mode.
- Dedicated Settings, redesigned editor/PIN/help/confirmation windows, inline validation, and unsaved-change handling.
- Per-monitor DPI manifest, optional native dark chrome/rounded corners, saved full/Mini dimensions, and work-area clamping.
- Windows screenshot/binding review executable and isolated production smoke harness, with CI UI artifacts.

### Fixed
- Locked command execution, cancelled tray unlock, overlapping insertion, and focus tracking of secondary app windows.
- PIN-buffer lifetime during async import and encryption-key clearing during active data operations.
- Search refresh after edits, bulk collection Count notifications, and custom auto-lock's unnecessary PIN prompt.
- Swallowed add/delete persistence errors and row loss after failed deletion.
- Missing PIN metadata no longer resets stored data.

### Documentation
- Corrected abbreviation, modifier-command, IV derivation, and memory-handling claims; added actual Windows screenshots and verification limits.
