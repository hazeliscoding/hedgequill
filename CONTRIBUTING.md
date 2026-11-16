# Contributing to Hedgequill

Thanks for helping. Hedgequill is a small, offline app for writing fanfiction, and it stays that way on purpose, so this page is short.

## Before you start

- [README.md](README.md) says what Hedgequill is and what it promises.
- [ROADMAP.md](ROADMAP.md) holds the decisions already made, what's being built and what's not planned. Check it before proposing a feature. Decisions stand unless the owner reopens them.
- For anything bigger than a fix, open an issue first, so nobody builds something that's already decided against.

## Build, run and test

You need the .NET 10 SDK, 10.0.404 or later; `global.json` says why.

```bash
dotnet build
dotnet test --project tests/Hedgequill.Tests
dotnet format --verify-no-changes
```

Run the app with its Recent list and settings in a scratch folder, so development never touches your own:

```bash
HEDGEQUILL_SETTINGS=/tmp/hedgequill-dev dotnet run --project src/Hedgequill.Desktop
```

In PowerShell, set `$env:HEDGEQUILL_SETTINGS = "$env:TEMP\hedgequill-dev"` first.

After a UI change, `dotnet run --project tools/Hedgequill.Screenshots -- docs/screenshots` renders the Welcome screen and a workspace in light and dark. Attach before and after screenshots to the pull request.

## Project layout

| Project | What it holds |
|---|---|
| `src/Hedgequill.Core` | The project model, file I/O, saving and AO3 HTML. No Avalonia, no networking. |
| `src/Hedgequill.Desktop` | The Avalonia app, the spell checkers and the update check. |
| `tests/Hedgequill.Tests` | Unit tests, headless UI tests and the privacy guards. |
| `tests/Hedgequill.UiTests` | Drives the published app's real window on Windows. It moves the mouse, so leave the PC alone while it runs. |
| `tools/` | Probes for NativeAOT, typing speed and screenshots. |

## Hard rules

These come from [AGENTS.md](AGENTS.md), and reviews hold every change to them.

- **Never lose words.** Files are written whole or not at all, and unsaved text never goes away without the writer choosing it.
- **The update check is the only network code,** in `src/Hedgequill.Desktop/Updates/Updater.cs`. No telemetry, analytics or crash reports. A test fails on network APIs anywhere else.
- **Files stay plain Markdown** that any editor can open, and Delete moves to the project's `Trash/` folder rather than erasing.
- **Tests use invented text,** never a real fic.
- **NativeAOT stays free of warnings.** No reflection-based serialization; use source generators.

## Pull requests

- Use [Conventional Commits](https://www.conventionalcommits.org/), one change per commit.
- CI builds and tests on Windows and macOS, publishes with NativeAOT, and scans for secrets. It must pass.
- New work goes in `ROADMAP.md`. GitHub issues are for bug reports and contributors' tasks.
