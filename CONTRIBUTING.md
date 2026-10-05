# Contributing to Knock Knock

Thanks for helping! Bug reports, ideas and pull requests are all welcome.

## Getting set up

You need Windows 10/11 and Node.js 20+.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1             # builds dist\KnockKnock.exe
powershell -ExecutionPolicy Bypass -File .\tests\run-tests.ps1     # detection tests
powershell -ExecutionPolicy Bypass -File .\run-from-source.ps1     # run without the .exe
```

- **UI work:** `cd ui && npm run dev` opens the settings UI in a browser with a mock app behind it
  (see `ui/src/bridge.ts`), so you can work on it without rebuilding the .exe.
- **Debugging:** `KnockKnock.exe --debug` enables the browser dev tools (F12) in the settings window.
- **C# code** must compile with the .NET Framework 4.8 compiler built into Windows, which supports
  C# 5. That means no string interpolation (`$"..."`), `?.`, `nameof` or expression-bodied members.

## Pull requests

1. Branch from `main`. `main` is protected: changes land through pull requests, and CI must pass.
2. Keep changes focused, and add a test in `tests/` when you change detection behaviour.
3. Add a line under **Unreleased** in `CHANGELOG.md` describing what changed for users.

## Versioning and releases

Knock Knock uses [Semantic Versioning](https://semver.org/):

- **PATCH** (1.0.**1**): bug fixes
- **MINOR** (1.**1**.0): new features that keep existing settings working
- **MAJOR** (**2**.0.0): changes that break settings or behaviour people rely on

To release (maintainers):

1. Update `VERSION` (for example `1.1.0`) and move the **Unreleased** notes in `CHANGELOG.md`
   under a new `## [1.1.0] - YYYY-MM-DD` heading. Merge this through a pull request.
2. Tag the merged commit and push the tag:
   ```powershell
   git tag v1.1.0
   git push origin v1.1.0
   ```
3. The **Release** workflow builds and tests the code, checks the tag matches `VERSION`, and
   publishes a GitHub Release with `KnockKnock.exe`, its SHA-256 checksum and the changelog notes.
