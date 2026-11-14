# AGENTS.md

These are the working rules for agents in this repo. Hedgequill is a desktop app (.NET 10, Avalonia 12, Apache-2.0) for writing fanfiction offline and posting it to AO3.

## Sources of truth

- `README.md`: the pitch and the promise.
- `ROADMAP.md`: decisions already made, the milestones to build, and what's out of scope. Check it before proposing features, and respect its decisions unless the owner reopens them. Work from the next unchecked item, tick it off when it's done, and don't build past the current milestone without asking. Record new decisions there, dated, each with its reason and what it gives up, in the same change that needs them. Mark a changed decision superseded; don't delete it.
- New work goes in `ROADMAP.md` too: a box in a milestone, or a Later idea until it has one. GitHub issues are for bug reports and contributors' tasks, never roadmap work. Don't add plan, spec or backlog documents.
- **The UI prototype** is the reference for layout, states and copy: the claude.ai artifact "Hedgequill Prototype", which isn't public. The owner shares what a task needs from it. Build what it shows; don't redesign in code. `docs/brand/tokens.md` holds its colors, type and spacing.

## Hard rules

- **Never lose words.** Autosave. Write a file atomically (temporary file, then replace), so a crash never leaves half of it. When a save fails, the text stays in the editor and the user gets Retry and Save a copy…, as the prototype shows. Never discard unsaved text without the user choosing to: not on close, not on reload, not on update. A writer who loses a chapter once won't come back.
- **The update check is the only network code.** Velopack makes it, from one file in `Hedgequill.Desktop`. No telemetry, no analytics, no crash reports the app sends itself, no fonts or images fetched at runtime, and no other network use. `Hedgequill.Core` never references Velopack or networking. Tests fail on both. The privacy promise in the README depends on this. The one launcher call allowed is `Launcher.LaunchDirectoryInfoAsync` for File › Show project folder, which opens a local folder; never launch a URL or a file.
- **Never restart into an update on your own.** A downloaded update installs when the user closes Hedgequill, or when they choose to restart. An unprompted restart can interrupt a sentence.
- **The user's files stay plain Markdown** that any text editor can open. Hedgequill's own data in a project is `hedgequill.json`, for order and details. Never write a format only Hedgequill can read.
- **Delete never erases.** It moves the file or folder into the project's `Trash/` folder, and Undo moves it back. A Markdown file Hedgequill can't place in the outline stays on disk untouched.
- **Tests and fixtures use invented sample text, never a real fic.** The owner's writing is private, and this repo is public.
- **AO3 HTML stays within what AO3 keeps:** paragraphs, emphasis, strikethrough, headings, quotes, lists, links and scene breaks. Anything else becomes plain text, so the preview matches what gets posted.
- **Velopack's package ID stays `hazeliscoding.Hedgequill`,** never `Hedgequill`. Setup empties `%LOCALAPPDATA%\<packId>` on every install, and `%LOCALAPPDATA%\Hedgequill` holds settings and the recent-projects list.

## Code

- .NET 10 with nullable on and warnings as errors. Package versions live in `Directory.Packages.props`.
- `Hedgequill.Core` holds the project model, file I/O, saving and AO3 HTML, and never references Avalonia; a test enforces it. `Hedgequill.Desktop` is the app. `Hedgequill.Tests` holds the tests.
- NativeAOT must stay free of warnings: no reflection-based serialization. Use source generators. (M0 confirms AvaloniaEdit works under NativeAOT; see `ROADMAP.md`.)
- Cardo and Gloock ship as static TTF files. Avalonia reads neither WOFF2 nor variable fonts.
- Shortcuts use the platform's command key: Ctrl on Windows, Cmd on macOS.

## Brand

- The logo is the spiny quill in `docs/brand/`. Use the lockup SVGs; don't re-typeset the wordmark with a web font. `scripts/make_lockup.py` rebuilds the lockups from the marks, so rerun it when a mark changes.

## Dependencies

- **A special-purpose library** (a Markdown parser, an installer framework): add it yourself, with its reason in the commit message.
- **A general-purpose helper:** only after checking that nothing in the project already does the job.
- **A framework:** never without asking the owner.
- **Anything that brings in network code:** never, beyond Velopack for the update check.

## Glossary

One meaning per term. Use these words in code, docs and commits.

- **Project:** a folder of Markdown files plus `hedgequill.json`, which holds their order and details.
- **Act, chapter, scene:** the three levels of the story, in that order. Acts and chapters are folders, a scene is a Markdown file, and acts are optional. A scene is the smallest part the navigator moves. A chapter is what Copy for AO3 copies.
- **Index:** `hedgequill.json` at the project root: each folder's order, which folders are acts, and titles a file name can't hold.
- **Page:** a Markdown file in Notes or Research, outside the story.
- **Navigator:** the list on the left that shows the project's acts, chapters, scenes and pages.
- **Scene break:** the marker between scenes inside a chapter. It's a plain Markdown break in the file.
- **Markers:** the Markdown characters that make emphasis and headings. The editor shows them faded.
- **AO3 HTML:** the HTML Copy for AO3 produces, limited to what AO3 keeps.
- **Save state:** Saving…, Saved, Couldn't save, Changed on disk or Read-only, as the prototype shows them.

## Commands

- `dotnet build` builds everything. `dotnet test --project tests/Hedgequill.Tests` runs the tests, the privacy guards among them. `dotnet format --verify-no-changes` checks style, as CI does.
- `tests/Hedgequill.UiTests` drives the published app's real window on Windows with FlaUI. Set `HEDGEQUILL_APP` to the NativeAOT `Hedgequill.exe` and `HEDGEQUILL_UI_SHOTS` to a folder for its screenshots, then `dotnet test --project tests/Hedgequill.UiTests`. It moves the real mouse and types, so leave the PC alone while it runs; without `HEDGEQUILL_APP` it skips. CI runs it on Windows and uploads the screenshots, with the macOS runner's.
- `HEDGEQUILL_SETTINGS` points the app at another folder for its Recent list and `settings.json`, so tests never touch the writer's.
- `global.json` pins .NET SDK 10.0.404 or later, because older 10.0 SDKs bring a NativeAOT compiler with a known vulnerability. It looks in a repo-local `.dotnet/` first (git-ignored), then the system install.
- `dotnet publish src/Hedgequill.Desktop -c Release -r win-x64 -warnaserror` builds the NativeAOT app (`osx-arm64` on a Mac; NativeAOT can't build for another OS). On Windows with Visual Studio 18, put `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on `PATH` first, or the link step fails because `vswhere.exe` isn't found.
- `tools/Hedgequill.AotProbe`, published the same way and run, opens AvaloniaEdit under NativeAOT, types, undoes, selects and styles Markdown markers, and exits 0 only if every check passed. CI runs it on Windows and macOS. It opens a window for about a second.
- `dotnet run --project tools/Hedgequill.Screenshots -- docs/screenshots` renders the Welcome screen and an invented project's workspace headlessly, in light and dark, with an empty Recent list rather than yours. Rerun it when either changes.
- `uv run --no-project --with resvg-py --with pillow python -I scripts/make_icons.py docs/brand` rebuilds the app icons from `app-icon.svg`.
- `uv run --no-project --with fonttools --with uharfbuzz python -I scripts/make_lockup.py <Gloock-Regular.ttf> docs/brand` rebuilds the lockups. Its header says where to get the font.

## Working style

- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `chore:`, `test:`, `ci:`, `build:`, `refactor:`). Keep each commit atomic, and use a scope when it adds clarity (`feat(editor): …`).
- **No AI attribution** in commits or pull requests: no `Co-Authored-By` trailers, no "Generated with" lines and no session links.
- **Checks:** automate acceptance checks instead of handing manual steps to the owner. Give every check that tests for an absence a positive control. For example, the network guard must fail on a deliberate `HttpClient`.
- **Structural rules get tests.** When a decision is a rule about the code's shape, add a test that fails when it's broken.
- **Validation:** evidence comes from dogfooding and public signals (issues, downloads). Don't plan interviews, recruiting or outreach.
- **Docs:** short and concise.
- **Code comments:** explain why, not what. Don't restate names or types, and don't leave commented-out code.
- Local Playwright output goes to `.playwright-mcp/`, which git ignores.
