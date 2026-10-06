# Changelog

All notable changes to Knock Knock are listed here. The project follows
[Semantic Versioning](https://semver.org/): `MAJOR.MINOR.PATCH`.

## [Unreleased]

### Added
- Project website at https://ahd-aljadeed.github.io/Knock-Knock/.
- Install with Scoop; winget manifest ready for submission.
- Issue templates for bug reports and feature requests.

## [1.0.0] - 2026-10-04

First public release.

### Added
- Knock detection from the laptop microphone, using the raw (unprocessed) signal so the
  driver's noise suppression doesn't erase knocks.
- **Teach your knock**: knock 6 times and Knock Knock learns how your knock sounds on your
  desk. Other thumps are ignored.
- **Rhythm check**: knocks must be evenly spaced and similarly strong.
- **Ignore while typing** and **ignore when you've been away** filters.
- **Actions per knock count** (2-8 knocks): screenshot (any screen or all), open an app, file
  or website, media keys, keyboard shortcut, lock the PC, run a command.
- Settings window (React in WebView2) with a live mic visualiser, activity log showing why
  knocks were accepted or ignored, and general settings.
- Tray icon: open settings, toggle listening, delete last screenshot, open the screenshot folder.
- The microphone is fully closed while listening is off or the PC is locked.
- Single-file `KnockKnock.exe`; `run-from-source.ps1` for PCs where Smart App Control blocks it.

[1.0.0]: https://github.com/Ahd-Aljadeed/knock-knock/releases/tag/v1.0.0
