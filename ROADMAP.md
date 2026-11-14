# Roadmap

Hedgequill is a desktop app (.NET 10, Avalonia 12) for writing fanfiction offline and posting it to AO3. This file holds the decisions already made, the milestones to build, and what's out of scope.

## Architecture characteristics

What the design must protect, in this order when two conflict. Everything else gets ordinary good design.

1. **Simple.** One screen and nothing to learn.
2. **Never lose words.** Plain files and autosave. A failed save keeps the text on screen.
3. **Private.** Offline, no account, no telemetry. The update check is the one network request.

## Decisions (2026-11-10)

- **A desktop app for writing fanfic and posting it to AO3:** one window with a list of acts › chapters › scenes plus Notes and Research pages, the page, and an AO3 preview that can be hidden. Scrivener is paid and felt too hard to use, and novelWriter, the closest free one, felt overwhelming. Nothing found combined styled Markdown, fiction structure and AO3-ready output in one offline app. This gives up everything those apps do that a fic doesn't need; see Not planned.
- **Simple, never lose words and private** are the characteristics above.
- **.NET with Avalonia, AvaloniaEdit as the editor, and Velopack for updates from GitHub Releases,** the stack of the owner's other desktop apps. This gives up a web editor such as TipTap in Tauri, which stays the fallback if AvaloniaEdit can't make styled Markdown feel smooth (M2).
- **Tests use invented sample text, never a real fic.** The owner's writing is private, and test fixtures end up in a public repo.

## Decisions (2026-11-12)

- **Start the project.** The owner tried Obsidian with the Longform plugin first, and it wasn't enough. This gives up building on Obsidian, which already has Live Preview, plugins and mobile apps.
- **The name is Hedgequill.** A hedgehog's quills are pens, and a hedgehog curls up to keep itself safe, like an app that stays private and offline. The working name, Margin, was dropped: at least five writing and notes apps already use it. This gives up a name that says "writing" on its own.
- **The UI prototype is the reference** for layout, states and copy: the claude.ai artifact "Hedgequill Prototype", which isn't public. It covers the Welcome screen, the workspace, Copy for AO3, every save state, the menus, light and dark, and Windows and macOS frames. This gives up designing screens in code. One fix to carry over: its Windows title bar still shows an "M" tile from the Margin name.
- **Public, under Apache-2.0,** like the owner's other Avalonia apps. Velopack's GitHub Releases updater needs public releases to work without a token, and a privacy promise is easier to trust when people can read the code. This gives up a closed-source paid app.
- **Hedgequill updates itself.** It checks GitHub Releases at launch and every few hours, downloads in the background, shows "Update ready, restart to finish", and installs when you close it. It never restarts on its own, so an update can't interrupt a sentence. Help › Check for updates… checks on demand. The request sends nothing about the writer or their words. It is the only network code, the owner's call, so that users are always on the latest version. This gives up a strict "no network" promise, and an off switch: a setting would be the app's first, and copies with updates off fall behind.
- **Windows and macOS at 1.0.** These are what the prototype draws. The owner writes on Windows, and many AO3 writers use Macs. NativeAOT can't build for another OS, so each platform publishes on its own CI runner, as in Prompuff. This gives up Linux until someone asks (Later).
- **NativeAOT by default,** as in the owner's other Avalonia apps, for start-up time and memory. AvaloniaEdit hasn't been tried under it, and a library can fail there (Ical.Net did, in another app), so M0 publishes a probe first. If AvaloniaEdit fails, Hedgequill ships self-contained without NativeAOT, as Prompuff does. This gives up a few MB and some start-up time in that case.
- **Laid out like the owner's other apps:** `Hedgequill.Core` (the project model, files, saving and AO3 HTML, no UI), `Hedgequill.Desktop` (the app) and `Hedgequill.Tests`. A test fails if Core references Avalonia, so the parts that guard your words can be tested without a window. This gives up a single project.
- **Velopack's package ID is `hazeliscoding.Hedgequill`,** not `Hedgequill`. Setup empties `%LOCALAPPDATA%\<packId>` on every install and the uninstaller deletes it, so the ID must never equal the folder that holds settings and the recent-projects list. This gives up an install folder named plainly Hedgequill.
- **Setup only, no portable zip.** A portable copy can't update itself, and the owner wants every copy up to date. This gives up running without installing.
- **Signing as in the owner's other apps:** Windows Setup is signed with the owner's Azure Artifact Signing profile, through OIDC in a release job of its own, so no package the build loads can reach the signing token. macOS builds are ad-hoc signed and not notarized, as in Prompuff, and the README explains Open Anyway. This gives up a Mac first launch without Gatekeeper's warning.
- **The logo is the spiny quill,** the owner's pick from six options: a quill pen whose vane has hedgehog spines. The lockup sets the wordmark in Gloock, the prototype's title face, outlined to paths. This gives up a hedgehog figure in the mark; a mascot can come separately (Later).
- **Design tokens come from the prototype,** in `docs/brand/tokens.md`. Its tertiary text fell below 4.5:1, so it's darkened in light mode and lightened in dark. This gives up an exact match with the prototype's grey.
- **Avalonia's build telemetry is off,** as in Carmine. Avalonia 12 sends usage data from every build, and a private app shouldn't send anything its contributors didn't choose. This gives up Avalonia's usage data from this repo.

## Decisions (2026-11-13)

- **NativeAOT stays: AvaloniaEdit works under it.** M0's probe (`tools/Hedgequill.AotProbe`) published AvaloniaEdit 12.0.0 on Avalonia 12.1.6 for win-x64 and osx-arm64 with no trim or AOT warnings. In a real window it typed, undid, redid, selected, replaced a selection, and faded and italicized Markdown markers through a line transformer, on both. Its working set after the checks was 57 MB on CI's win-x64 runner and 107 MB on osx-arm64 (72 MB on the owner's PC, against 131 MB without NativeAOT). The app's own win-x64 build is a 17.3 MB exe plus 18 MB of native libraries (Skia 11.1, ANGLE 5.1, HarfBuzz 1.7), and with the Welcome screen open it holds a 78 MB working set and 103 MB private. This gives up any library that needs reflection: the Markdown and AO3 code in M1–M3 must stay NativeAOT-safe, and the fallback to a build without NativeAOT goes unused.
- **.NET SDK 10.0.404 or later.** The SDK on the owner's PC, 10.0.302, brings a NativeAOT compiler (Microsoft.DotNet.ILCompiler 10.0.10) with a high-severity advisory, GHSA-p3jp-9v83-76w9, fixed in 10.0.14, and warnings as errors turns the advisory into a failed restore. `global.json` pins 10.0.404 and looks in a git-ignored, repo-local `.dotnet/` first, then the system install, so the system SDK stays untouched. This gives up building with an older SDK.
- **The dark focus ring is the accent text color** (`#dc98a4`), because the dark accent (`#a14a5c`) is only 2.86:1 on the page. This gives up one focus color for both themes.
- **The app icon is the light mark on a paper tile**, edge to edge on every platform, because the ink nib disappears on a dark taskbar. This gives up macOS's inset icon grid until M4's packaging.
- **gitleaks runs on the Windows leg only,** as in Staypaw. It scans the history, and one scan per run is enough.
- **A project's folders mirror its outline,** as the prototype draws them. Acts and chapters are folders, a scene is a Markdown file named after its title, and pages sit in `Notes/` and `Research/`. Acts are optional, so a chapter can sit at the top. A new project is a folder named after its title, holding `Chapter 1/Scene 1.md`, `Notes/` and `Research/`. Another editor sees the outline in the folder tree. This gives up file names that stay put: renaming a scene renames its file.
- **`hedgequill.json` holds the order and details,** at the project root: the order of each folder's contents, which folders are acts, and any title a file name can't hold, such as "What now?" on Windows. Files it doesn't list still show, sorted by name, and deleting it loses only the order. JSON, because .NET reads and writes it NativeAOT-safe without another library. Acts are listed so an empty act doesn't reopen as an empty chapter. This gives up a project folder that looks untouched.
- **Delete moves to the project's `Trash/` folder,** with Undo, as the prototype shows, not to the system trash the M1 item first named. A move works the same on Windows, macOS and drives without a trash, needs no native code, and keeps every word inside the project. This gives up a tidy project: the writer empties `Trash/` themselves.
- **Alt+Up and Alt+Down reorder the navigator,** which the prototype doesn't draw but M1 asks for. A scene or page at the edge of its chapter or group moves into the next one. This gives up nothing the prototype shows.
- **Find is AvaloniaEdit's own search panel,** opened with Ctrl+F (⌘F). The prototype lists Find… without drawing it, and the panel already searches, steps through matches and replaces. This gives up a Find bar styled from the prototype until one is drawn.
- **Files are written with LF line endings.** A scene saved on Windows reads the same on a Mac and in Git, and a file with CRLF endings changes to LF the first time Hedgequill saves it. This gives up keeping each file's original line endings.
- **Changed on disk is noticed when Hedgequill next saves that file:** before writing, it compares the file with what it last read or wrote. It never overwrites the other app's change without Keep mine. This gives up a warning the moment another app saves; a file watcher can add one later.
- **Choose folder… copies the whole project,** with the text in memory, into the folder the writer picks, and carries on there. A read-only folder can't take a single scene, and splitting a project across folders would break it. This gives up writing just the one scene elsewhere; Save a copy… still does that when a save fails.
- **Closing with unsaved text asks first:** "Close without saving?", with Close anyway. The prototype doesn't draw it, but AGENTS.md's first rule is never to lose words without the writer choosing to. This gives up a window that always closes at once.

## M0: Placeholder (as soon as possible)

- [x] Add `LICENSE` (Apache-2.0), `.gitignore` and `.gitattributes`.
- [x] Add the logo, lockups and design tokens in `docs/brand/`.
- [x] Scaffold the solution: `Hedgequill.Core`, `Hedgequill.Desktop` and `Hedgequill.Tests` on .NET 10 and Avalonia 12, with nullable on, warnings as errors, and versions in `Directory.Packages.props`.
- [x] Build a static Welcome screen from the prototype: Hedgequill, "A quiet place to write.", New project…, Open folder…, an empty Recent list and the line about plain files, in light and dark. Cardo and Gloock ship as static TTF files.
- [x] Generate the app icons (`.ico`, `.icns`, `.png`) from `mark.svg` on a paper tile.
- [x] Run the NativeAOT probe: publish for win-x64 and osx-arm64 with AvaloniaEdit showing invented sample text. Record trim or AOT warnings, whether typing, undo and selection work, the exe size and the working set. Record the result as a decision.
- [x] Turn on the guardrails from the first code commit:
  - [x] CI builds and tests on Windows and macOS;
  - [x] gitleaks, run from its release binary checked against its published checksum, with `--log-opts="--full-history HEAD"`;
  - [x] a network guard that fails on networking APIs anywhere but the updater's file, with a positive control;
  - [x] a test that fails if `Hedgequill.Core` references Avalonia;
  - [x] Avalonia's build telemetry off, with a test that fails if the override stops working.
- [x] Add a screenshot of the Welcome screen to the README.

**Done when:** CI is green on Windows and macOS, the Welcome screen renders in both themes, the NativeAOT probe's result is a dated decision here, and a test PR that adds `HttpClient` to `Hedgequill.Core` fails the network guard.

**Done** (2026-11-13), in [#1](https://github.com/hazeliscoding/hedgequill/pull/1):
- CI is green on win-x64 and osx-arm64 ([run](https://github.com/hazeliscoding/hedgequill/actions/runs/43379974849)): build, 80 tests, format, NativeAOT publish with warnings as errors, the probe, a 15-second smoke test, and gitleaks.
- `WelcomeTests` renders the Welcome screen headlessly in both themes and checks the window and card colors against the tokens. `docs/screenshots/` holds both renders.
- The probe's result is the first decision of 2026-11-13, above.
- [#2](https://github.com/hazeliscoding/hedgequill/pull/2) added an `HttpClient` to `Hedgequill.Core`. Its [run](https://github.com/hazeliscoding/hedgequill/actions/runs/43381318104) failed on both runners with `src/Hedgequill.Core/Leak.cs:6  HTTP client`, and it was closed without merging.
- Locally, the same leak plus the word "Avalonia" in Core and an emptied telemetry override failed the core purity test, the network guard and the telemetry test.

## M1: Write

- [x] New project… creates a folder of Markdown files plus `hedgequill.json` for their order and details. Open folder… opens one, and Recent lists the last few.
- [x] The navigator: acts › chapters › scenes, plus Notes and Research pages. Add, rename, reorder by drag or keyboard, and delete to the project's `Trash/` folder, with Undo.
- [ ] The editor: AvaloniaEdit in Cardo, with Bold, Italic and Insert scene break adding plain Markdown markers, Find…, Focus mode and the chapter overview.
- [ ] Autosave, and every save state from the prototype: Saving…, Saved, Couldn't save (the text stays, with Retry and Save a copy…), Changed on disk (reload, or keep and overwrite) and Read-only (edits stay in memory until you choose a writable folder).
- [ ] Saves are atomic: write a temporary file, then replace, so a crash mid-save never leaves half a file.

**Done when:** tests on invented text pass, including one where the disk write fails and the text survives in memory and in Save a copy…; a project made in Hedgequill opens as plain Markdown in another editor; and the owner has written a scene in it.

## M2: Styled Markdown

- [ ] Emphasis, strong and headings are styled in place, with their markers faded. Ctrl+B and Ctrl+I toggle them.
- [ ] A scene break shows as a centred ornament; the file keeps a plain Markdown break.
- [ ] Typing stays smooth in a long chapter.

**Done when:** on a 10,000-word invented chapter, a keystroke shows within one frame at 60 Hz on the owner's PC, measured, and the styling matches the prototype. If AvaloniaEdit can't get there, reopen the stack decision before M3.

## M3: AO3

- [ ] Markdown to AO3 HTML in `Hedgequill.Core`: paragraphs, emphasis, headings, quotes and scene breaks. Anything else becomes plain text.
- [ ] The AO3 preview: it can be hidden and resized, and starts hidden when the window is narrower than 1,200 px.
- [ ] Copy chapter HTML, Copy chapter title and Show HTML, with the note "Paste into AO3's HTML editor, not Rich Text."
- [ ] Fixture tests: invented chapters in, expected HTML out, byte for byte.

**Done when:** the fixtures pass, and a chapter copied from Hedgequill and pasted into AO3's HTML editor looks the same in AO3's preview as in Hedgequill's.

## M4: 1.0

- [ ] Windows x64 Setup, signed, and macOS packages for Apple Silicon and Intel, ad-hoc signed.
- [ ] Self-update as decided above, plus Help › Check for updates….
- [ ] Help: Writing in Hedgequill, Keyboard shortcuts, Pasting into AO3 and About.
- [ ] Accessibility: every control has a name Narrator and VoiceOver read, the whole app works from the keyboard, and contrast meets 4.5:1.
- [ ] `CONTRIBUTING.md` and `SECURITY.md`.

**Done when:** 1.0.0 installs on clean Windows and macOS runners, an installed 1.0.0 updates itself to a test 1.0.1 when closed, and the owner has written a chapter in Hedgequill and posted it to AO3.

## Later

- **Linux.** Avalonia and Velopack support it. Settle first who tests it.
- **A mascot,** the owner's call.
- **A winget package.** Settle first who updates the install, since Velopack updates Hedgequill in place and a winget upgrade must not fight it.

## Not planned

- Tag syntax, build settings, compile formats (ePub, PDF, Word), templates, corkboards and labels. They are what made the alternatives feel heavy. Scrivener and novelWriter do them well.
- Posting to AO3 from the app. Copy for AO3 and AO3's own editor keep your AO3 login out of Hedgequill.
- Accounts, sync, telemetry, crash reports Hedgequill sends itself, or anything hosted.
- A portable zip. It can't update itself.

## How we'll know it works

Evidence comes from dogfooding: the owner writing and posting their own fic with Hedgequill (n=1, in a log). After release it also comes from public signals: issues, pull requests and downloads.
