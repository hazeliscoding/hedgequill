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

## M0: Placeholder (as soon as possible)

- [x] Add `LICENSE` (Apache-2.0), `.gitignore` and `.gitattributes`.
- [x] Add the logo, lockups and design tokens in `docs/brand/`.
- [ ] Scaffold the solution: `Hedgequill.Core`, `Hedgequill.Desktop` and `Hedgequill.Tests` on .NET 10 and Avalonia 12, with nullable on, warnings as errors, and versions in `Directory.Packages.props`.
- [ ] Build a static Welcome screen from the prototype: Hedgequill, "A quiet place to write.", New project…, Open folder…, an empty Recent list and the line about plain files, in light and dark. Cardo and Gloock ship as static TTF files.
- [ ] Generate the app icons (`.ico`, `.icns`, `.png`) from `mark.svg` on a paper tile.
- [ ] Run the NativeAOT probe: publish for win-x64 and osx-arm64 with AvaloniaEdit showing invented sample text. Record trim or AOT warnings, whether typing, undo and selection work, the exe size and the working set. Record the result as a decision.
- [ ] Turn on the guardrails from the first code commit:
  - [ ] CI builds and tests on Windows and macOS;
  - [ ] gitleaks, run from its release binary checked against its published checksum, with `--log-opts="--full-history HEAD"`;
  - [ ] a network guard that fails on networking APIs anywhere but the updater's file, with a positive control;
  - [ ] a test that fails if `Hedgequill.Core` references Avalonia;
  - [ ] Avalonia's build telemetry off, with a test that fails if the override stops working.
- [ ] Add a screenshot of the Welcome screen to the README.

**Done when:** CI is green on Windows and macOS, the Welcome screen renders in both themes, the NativeAOT probe's result is a dated decision here, and a test PR that adds `HttpClient` to `Hedgequill.Core` fails the network guard.

## M1: Write

- [ ] New project… creates a folder of Markdown files plus one small file for their order and details (its name and format are settled here). Open folder… opens one, and Recent lists the last few.
- [ ] The navigator: acts › chapters › scenes, plus Notes and Research pages. Add, rename, reorder by drag or keyboard, and delete to the system trash.
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
