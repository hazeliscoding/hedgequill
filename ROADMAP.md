# Roadmap

Hedgequill is a desktop app (.NET 10, Avalonia 12) for writing fanfiction offline and posting it to AO3. This file lists what has shipped, the milestones to build next, what's out of scope, and every decision so far with its reason.

## Architecture characteristics

What the design must protect, in this order when two conflict. Everything else gets ordinary good design.

1. **Simple.** One screen and nothing to learn.
2. **Never lose words.** Plain files and autosave. A failed save keeps the text on screen.
3. **Private.** Offline, no account, no telemetry. The update check is the one network request.

## Shipped

| Version | Date | What it brought |
|---|---|---|
| 1.0.0 | 2026-11-16 | M0 to M5, in [#1](https://github.com/hazeliscoding/hedgequill/pull/1) and [#3](https://github.com/hazeliscoding/hedgequill/pull/3) to [#9](https://github.com/hazeliscoding/hedgequill/pull/9): the workspace with acts, chapters, scenes, Notes and Research; styled Markdown and spell check; Copy for AO3 with its preview, summary and notes, and AO3's word count; find and replace across the project, earlier versions, synced folders, text size and high contrast; motion; and a signed Windows Setup and Mac packages that update themselves, with Help. |
| 1.0.1 | 2026-11-16 | A long scene's first lines stay in view as it scrolls under the title ([#11](https://github.com/hazeliscoding/hedgequill/pull/11)). |
| 1.0.2 | 2026-11-18 | Right-click menus and the navigator's More menu show their items again, and Escape leaves focus mode from a chapter's overview too ([#16](https://github.com/hazeliscoding/hedgequill/pull/16)). |

The milestones' checklists, "Done when" lines and evidence are in `git show v1.0.1:ROADMAP.md`. 1.0.0 installed on clean Windows and macOS runners and updated itself to a test 1.0.1 when closed ([run](https://github.com/hazeliscoding/hedgequill/actions/runs/43965594186)). M5's last check, a chapter written in Hedgequill and posted to AO3, moved to M7.

Each feature in M6 to M10 is drawn in the prototype and approved before it's built.

## M6: Markdown in your notes

Notes and Research pages already style headings, emphasis, quotes, strikethrough and links. These add what notes use most. One editor draws pages and scenes, so scenes get them too.

- [ ] Lists: bullets and numbers with a hanging indent, so a wrapped line lines up under the text, not the marker. Enter continues a list, Enter on an empty item ends it, and Tab and Shift+Tab nest an item.
- [ ] Checklists: `- [ ]` and `- [x]` draw a box. Clicking it writes or clears the x in the file, and Undo takes it back.
- [ ] Code: `inline code` and fenced blocks in a monospace face on a light background, with their markers faded. AO3 HTML doesn't keep code, so in a scene it copies as plain text.
- [ ] Tables: pipe tables in monospace, with the pipes and the header's dashes faded, and in a scene they copy as plain text too. Settle first how the columns line up: by rewriting the table's spacing in the file as you type, or by spacing drawn on screen that leaves the file as typed.

**Done when:** tests on invented text cover a list's Enter, Tab and Shift+Tab, a box ticked by a click and then undone, a fenced block styled from its first line to its last, a table's columns lining up, and a scene with code and a table copying for AO3 as plain text; `ContrastTests` passes with code's background in all four themes; and the owner has kept notes with them.

## M7: Bring work in, send it out

- [ ] Write a chapter in Hedgequill and post it to AO3, the check left over from M5.
- [ ] Paste from Google Docs or Word keeps italics and bold: Hedgequill reads the HTML they put on the clipboard and writes Markdown markers. Everything else comes in as plain text.
- [ ] Copy for a beta reader: the chapter's HTML goes on the clipboard as rich text, so it pastes into Google Docs or an email with its italics. Settle first that Avalonia can put HTML on the clipboard on both systems under NativeAOT.
- [ ] Comments to yourself: Markdown's `<!-- -->` comments, faded in the editor and left out of AO3 HTML and the beta reader's copy. The word count comes from the same HTML, so it leaves them out too.

**Done when:** fixture tests pass on invented text copied from Google Docs and from Word, as each puts it on the clipboard; a chapter with a comment copies for AO3 and for a beta reader without it; and the owner has posted a chapter written in Hedgequill to AO3 and brought a work in progress over from Google Docs or Word.

## M8: Writing days

- [ ] Targets: a word goal and a deadline per project, such as an exchange's minimum or a Big Bang's, and the words written today, all counted as AO3 counts them. Settle first where today's count lives so it survives a restart; `hedgequill.json` is the likely home for the goal and deadline.
- [ ] Notes beside the page: a Notes or Research page in the right pane, in place of the AO3 preview, saved like any other page.
- [ ] Smart punctuation: curly quotes as you type, and `--` turns into an em dash. Undo brings back what was typed. AO3 already counts `--` as an em dash, so word counts don't change. Settle first whether it's always on or a menu item, since Hedgequill has no settings window.

**Done when:** tests on invented text cover a target's progress and deadline, today's words across a restart, curly quotes and dashes with their Undo, and a page saved from the right pane; and the owner has written toward a target.

## M9: Proofread and print

- [ ] Read aloud for proofreading, in the voices installed on the computer, never an online one. It needs native calls, as spell check does, so it gets its own file and its own names in the network guard, and a NativeAOT probe on Windows and macOS runs first.
- [ ] Export as a book: a print-ready PDF at 5 × 8 in, laid out like an AO3 download.
  - A title page with the byline and summary, and a Notes page with a dedication and ruled lines for a handwritten note.
  - Each chapter on a new page under "Chapter N: Title", scene breaks as short rules, and curly quotes.
  - A running title and page numbers, with margins that mirror for binding.
  - Settle first how to write the PDF without a browser. The network guard forbids starting another program, and SkiaSharp, already in the app, can write PDFs.

**Done when:** an invented work exports to a PDF that a test reads back, with 5 × 8 in pages, mirrored margins and each chapter starting a new page; the read-aloud probe passes under NativeAOT on Windows and macOS; and the owner has printed a copy.

## M10: More places to run it

- [ ] Linux. Avalonia and Velopack support it. Settle first who tests it, and which spell checker it uses, since Hedgequill ships no dictionaries.
- [ ] A winget package. Settle first who updates the install: Velopack updates Hedgequill in place, and a winget upgrade must not fight it.
- [ ] A mascot, if the owner wants one: a hedgehog figure to go with the spiny quill, added to `docs/brand/`.

**Done when:** a Linux build installs, opens and updates itself on a clean Linux runner, as the Windows build does in the release workflow; winget and Hedgequill agree on the installed version after an update; and the owner has decided on the mascot.

## Later

Nothing yet. A new idea waits here until it has a milestone.

## Not planned

- Tag syntax, build settings, compile formats (ePub, Word, or any PDF except M9's book), templates, corkboards and labels. They're what made Scrivener and novelWriter feel heavy, and both apps do them well.
- Posting to AO3 from the app. Copy for AO3 and AO3's own editor keep your AO3 login out of Hedgequill.
- Accounts, sync, telemetry, crash reports Hedgequill sends itself, or anything hosted.
- A portable zip, which can't update itself.
- AO3's tags: fandoms, rating, warnings, relationships, characters and the rest. AO3's form suggests its own tags as you type, which needs AO3. Hedgequill keeps the summary and notes.
- Images in chapters, and work skins. AO3 shows only images hosted online, which goes against Private's offline promise, and a work skin is CSS the writer writes, which goes against Simple.

## How we'll know it works

The owner writes and posts their own fic with Hedgequill and keeps a log of it (n=1). Since 1.0, public signals count too: issues, pull requests and downloads.

## Decisions

Each decision says why it was made and what it gave up. A changed one is marked superseded, never deleted. M0 to M5 are the milestones folded into Shipped.

### 2026-11-10

- **A desktop app for writing fanfic and posting it to AO3:** one window with a list of acts › chapters › scenes plus Notes and Research pages, the page, and an AO3 preview that can be hidden. Scrivener is paid and felt too hard to use, and novelWriter, the closest free one, felt overwhelming. Nothing found combined styled Markdown, fiction structure and AO3-ready output in one offline app. This gives up everything those apps do that a fic doesn't need; see Not planned.
- **Simple, never lose words and private** are the characteristics above.
- **.NET with Avalonia, AvaloniaEdit as the editor, and Velopack for updates from GitHub Releases,** the stack of the owner's other desktop apps. This gives up a web editor such as TipTap in Tauri, which stays the fallback if AvaloniaEdit can't make styled Markdown feel smooth (M2).
- **Tests use invented sample text, never a real fic.** The owner's writing is private, and test fixtures end up in a public repo.

### 2026-11-12

- **Start the project.** The owner tried Obsidian with the Longform plugin first, and it wasn't enough. This gives up building on Obsidian, which already has Live Preview, plugins and mobile apps.
- **The name is Hedgequill.** A hedgehog's quills are pens, and a hedgehog curls up to keep itself safe, like an app that stays private and offline. The working name, Margin, was dropped: at least five writing and notes apps already use it. This gives up a name that says "writing" on its own.
- **The UI prototype is the reference** for layout, states and copy: the claude.ai artifact "Hedgequill Prototype", which isn't public. It covers the Welcome screen, the workspace, Copy for AO3, every save state, the menus, light and dark, and Windows and macOS frames. This gives up designing screens in code. One fix to carry over: its Windows title bar still shows an "M" tile from the Margin name.
- **Public, under Apache-2.0,** like the owner's other Avalonia apps. Velopack's GitHub Releases updater needs public releases to work without a token, and a privacy promise is easier to trust when people can read the code. This gives up a closed-source paid app.
- **Hedgequill updates itself.** It checks GitHub Releases at launch and every few hours, downloads in the background, shows "Update ready, restart to finish", and installs when you close it. It never restarts on its own, so an update can't interrupt a sentence. Help › Check for updates… checks on demand. The request sends nothing about the writer or their words. It's the only network code, and the owner chose it so that every copy stays on the latest version. This gives up a strict "no network" promise, and an off switch: a setting would be the app's first, and copies with updates off fall behind.
- **Windows and macOS at 1.0.** These are what the prototype draws. The owner writes on Windows, and many AO3 writers use Macs. NativeAOT can't build for another OS, so each platform publishes on its own CI runner, as in Prompuff. This gives up Linux until someone asks (now M10).
- **NativeAOT by default,** as in the owner's other Avalonia apps, for start-up time and memory. AvaloniaEdit hasn't been tried under it, and a library can fail there (Ical.Net did, in another app), so M0 publishes a probe first. If AvaloniaEdit fails, Hedgequill ships self-contained without NativeAOT, as Prompuff does. This gives up a few MB and some start-up time in that case.
- **Laid out like the owner's other apps:** `Hedgequill.Core` (the project model, files, saving and AO3 HTML, no UI), `Hedgequill.Desktop` (the app) and `Hedgequill.Tests`. A test fails if Core references Avalonia, so the parts that guard your words can be tested without a window. This gives up a single project.
- **Velopack's package ID is `hazeliscoding.Hedgequill`,** not `Hedgequill`. Setup empties `%LOCALAPPDATA%\<packId>` on every install and the uninstaller deletes it, so the ID must never equal the folder that holds settings and the recent-projects list. This gives up an install folder named plainly Hedgequill.
- **Setup only, no portable zip.** A portable copy can't update itself, and the owner wants every copy up to date. This gives up running without installing.
- **Signing as in the owner's other apps:** Windows Setup is signed with the owner's Azure Artifact Signing profile, through OIDC in a release job of its own, so no package the build loads can reach the signing token. macOS builds are ad-hoc signed and not notarized, as in Prompuff, and the README explains Open Anyway. This gives up a Mac first launch without Gatekeeper's warning.
- **The logo is the spiny quill,** the owner's pick from six options: a quill pen whose vane has hedgehog spines. The lockup sets the wordmark in Gloock, the prototype's title face, outlined to paths. This gives up a hedgehog figure in the mark; a mascot can come separately (now M10).
- **Design tokens come from the prototype,** in `docs/brand/tokens.md`. Its tertiary text fell below 4.5:1, so it's darkened in light mode and lightened in dark. This gives up an exact match with the prototype's grey.
- **Avalonia's build telemetry is off,** as in Carmine. Avalonia 12 sends usage data from every build, and a private app shouldn't send anything its contributors didn't choose. This gives up Avalonia's usage data from this repo.

### 2026-11-13

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
- **A printed book moves from Not planned to Later,** at the owner's request: a PDF to print and bind, like the gift edition the owner already makes with a script (now in M9). Not planned keeps ePub, Word and other compile formats. This gives up part of the line that kept Hedgequill out of compiling.
- **The menu bar is one NativeMenu:** File, Edit, View and Chapter, as the prototype draws them.
  - macOS shows it at the top of the screen. Windows shows it in the prototype's caption row (app icon, menus, title), and Avalonia draws the window buttons there.
  - Help and the AO3 items stay out until M3 and M4, so no item does nothing.
  - This gives up the system-drawn Windows title bar; whether Windows 11's snap layouts still show over the maximize button is unchecked.
- **View › Appearance offers Match system, Light and Dark,** remembered in `settings.json` beside the Recent list. The prototype shows Light and Dark only, but the app follows the system until the writer picks. This gives up nothing the prototype shows.
- **Duplicate is built now,** as the prototype draws it in the row menus, the Chapter menu and on Ctrl+D (⌘D): "Title copy" right after the original, with the text in memory. This gives up nothing; it was missing from the M1 items.
- **File › Show project folder is the one thing Hedgequill launches:** `Launcher.LaunchDirectoryInfoAsync` on the project folder, which opens Explorer or Finder. The network guard allows that one call and still fails on every other launcher, URL or file opener. Opening a local folder sends nothing anywhere. This gives up a guard that bans the launcher outright.
- **End-to-end tests drive the real window on Windows CI,** with FlaUI through UI Automation, against the NativeAOT build. They cover the caption row's menus, typing and saving, Find, focus mode, dragging by the caption row, and closing straight after a keystroke. The macOS runner stands in for a Mac the owner doesn't have: it starts the app, reads its menu bar through System Events and takes screenshots, uploaded with each run. This gives up nothing in the app; FlaUI is a test-only dependency, and Windows 11's snap layouts stay a manual check.

### 2026-11-14

- **Hedgequill catches up with the alternatives before 1.0,** at the owner's request, after comparing it with them.
  - M2 gains spell check. M3 gains AO3's summary and notes, a mark on chapters changed since they were copied, and word counts that match AO3's.
  - A new M4, Everyday writing, adds find and replace across the project, earlier versions of a scene, synced folders, text size and high contrast. 1.0 moves to M5, so "M4" in the decisions above means 1.0.
  - None of these is in the prototype yet. Each is drawn there before it's built.
  - This gives up an earlier 1.0.
- **Word counts follow AO3's,** as otwarchive's `lib/word_counter.rb` counts them (checked 2026-11-14): `--` becomes an em dash, hyphens and apostrophes join a word, any other punctuation splits one, and Chinese and Japanese count by character. Exchanges and Big Bangs set minimums by AO3's count, and M1 counts runs between spaces, so "wait—no" is one word in Hedgequill and two on AO3. This gives up M1's simpler count; counts in existing projects change.
- **AO3 HTML adds strikethrough, lists and links** to paragraphs, emphasis, headings, quotes and scene breaks. AO3's sanitizer (otwarchive's `sanitizer_config.rb`, checked 2026-11-14) keeps all three, and writers use links in author's notes and lists in letters and text messages. The old list called itself "what AO3 keeps" but was narrower. Everything else AO3 keeps, such as tables, alignment and images, still becomes plain text. The preview shows a link but never opens it, as AGENTS.md's launcher rule requires. AGENTS.md's AO3 rule and the README change with this. This gives up the smallest converter.
- **Spell check uses the system's checker:** Windows' Spell Checking API and macOS's `NSSpellChecker`. It works offline, ships no dictionaries, and follows the writer's own languages and word list. Add to project dictionary keeps the word in `hedgequill.json`, so a fandom's names stop being flagged in that project only. Every alternative the owner compared it with underlines misspellings, and AvaloniaEdit doesn't. It needs COM on Windows and Objective-C on macOS, so a NativeAOT probe runs first, as in M0. This gives up identical suggestions on both platforms, and Linux will need another checker.
- **Earlier versions stay in the project:** the first save of a scene in a session keeps its previous text in the project, as `Trash/` keeps deleted files, and Earlier versions… in the row menu brings one back. Never lose words covered crashes and failed saves, but not a rewrite the writer regrets, since Undo ends when the app closes. Scrivener has snapshots and Obsidian has File Recovery. This gives up disk space and a tidy project: the writer deletes old versions themselves.
- **Synced folders get tested, not built.** Windows 11 often backs up Documents to OneDrive, and a writer can edit the same Markdown on a phone through OneDrive, iCloud Drive or Dropbox. M4 tests a save while another process holds the file, as a sync client does mid-upload, and adds the file watcher the 2026-11-13 decision left for later. This gives up nothing; sync stays Not planned.

### 2026-11-15

- **The editor reads Markdown by CommonMark's rules, one line at a time,** in `Hedgequill.Core`, so M3's AO3 HTML can read it the same way and the preview matches.
  - `*` and `_` both make emphasis, `2 * 3 * 4` stays plain, and `snake_case` isn't italic. Headings take one to six `#`, and quotes one or more `>`.
  - Emphasis can't span a hard line break. Each paragraph is one line in Hedgequill, and AvaloniaEdit styles a line at a time, so typing restyles only the line that changed.
  - This gives up emphasis across a hard line break, which some pasted text has.
- **A scene break is any Markdown thematic break:** a line of three or more `-`, `*` or `_`, with spaces allowed between. Writers coming from other apps type `* * *` or `***`, and every Markdown reader breaks there. A `---` straight under a line of text stays a scene break, where CommonMark would make that line a heading. This gives up underlined (setext) headings; `#` headings still work.
- **The line holding the caret shows a scene break as plain text,** as Obsidian's Live Preview does, so the writer can edit it; on every other line it's the ornament. Bold on an empty line types `****`, which is a thematic break until the first letter goes in, and an ornament there would hide the caret. This gives up an ornament that stays put while the caret passes.
- **Bold and Italic unwrap the text they style:** selecting a bold word without its markers and pressing Ctrl+B removes them, since the markers fade and a writer selects the word they see. This gives up nothing.
- **Spell check is the one native code, and the network guard lists its names.** Windows' checker is COM and macOS's is Objective-C, and the guard banned native calls because they can reach the network unseen. Only `Spelling/WindowsSpellChecker.cs` and `Spelling/MacSpellChecker.cs` may make them, every string literal in those files must be on the guard's list (libraries, the checker's class and interface IDs, and Objective-C classes and selectors), and a class or selector must be written out, never built. Positive controls show it still fails on `NSURLSession`, `winhttp.dll` or another COM class. This gives up a guard that bans native calls outright.
- **The spell check probe passed under NativeAOT on Windows and macOS:** the app's own spell checker files, linked into `tools/Hedgequill.AotProbe`, opened the system's checker (source-generated COM on Windows, the Objective-C runtime on macOS), flagged "teh" and only "teh" in an invented line, suggested "the", and passed a correct line, with no trim or AOT warnings ([run](https://github.com/hazeliscoding/hedgequill/actions/runs/43783731612)).
- **Spell check is drawn in the prototype now** (version 3), as the 2026-11-14 decision asked before building it: a thin wavy underline in the danger red, and right-clicking the word puts up to five suggestions and Add to project dictionary at the top of the editor's menu. This gives up nothing.
- **The editor gets the prototype's right-click menu with it,** since the suggestions live there: Cut, Copy, Paste, Bold, Italic, Insert scene break and Focus mode, which M1 hadn't built. Like the navigator's menus it shows no icons, and it leaves out Look up…, which does nothing yet. This gives up the prototype's menu icons for now.
- **Spell check waits for a pause in typing:** 400 ms after the last keystroke it checks the changed sentences on screen, a sentence at a time and a few milliseconds per turn, and asks the checker about each sentence once per session. A 100-word paragraph took Windows' checker up to 19 ms in one call, longer than a frame. The word being typed loses its underline until the pause. This gives up an underline that appears while the word is still being typed.
- **On a Mac, spell check uses the writer's first chosen language** under System Settings › Keyboard › Spelling. Left to guess, macOS read the language off each line, and some lines came back unchecked. Windows already checks in the display language. This gives up a second language in the same project on a Mac.
- **M2's one-frame bar counts as met with one garbage collection over it,** the owner's call. On a 10,082-word chapter with spell check on, a keystroke reached the screen in a median 2.2–2.4 ms (95th percentile about 5 ms), but one or two keystrokes in 380 took 15–23 ms: a single blocking .NET collection about a second after typing starts in a chapter just opened. Plain AvaloniaEdit does exactly the same, and a web editor, the fallback, has garbage collection pauses too. This gives up a strict "every keystroke" and leaves the stack as it is.
- **Quotes are italic and secondary as the prototype draws them, but not indented.** AvaloniaEdit can't indent a line without replacing its `>` with something the caret can't enter. This gives up the prototype's 1.6 em indent.

### 2026-11-16

- **One reader for the preview, the HTML and the word count.** `Ao3Text.Read` in `Hedgequill.Core` turns Markdown into blocks of styled runs. The AO3 HTML, the preview and every word count are drawn from it, so they can't disagree. It reads lines by the editor's rules (decided 2026-11-15). This gives up a ready-made Markdown library.
- **How Markdown becomes AO3 HTML:**
  - `#` is `<h2>`, as the prototype has it, because AO3 sets the chapter title above it, down to `<h6>`.
  - Each block is one line of HTML, and lines inside a paragraph join with `<br />`, since AO3 turns a line break inside a paragraph into another break.
  - Strikethrough is two tildes and becomes `<s>`.
  - Links keep http, https, ftp, mailto and relative targets, as AO3's sanitizer does. A link with any other target keeps only its text.
  - Lists are one level deep. A line that isn't an item ends the list.
  - Images, tables and raw HTML stay as text, escaped.
  - Fixtures in `tests/Hedgequill.Tests/Fixtures/Ao3/` fix this byte for byte.
  - This gives up nested lists and lazy continuation lines, which fic rarely uses.
- **Word counts follow AO3's code,** otwarchive's `lib/word_counter.rb` at `def2683` (checked 2026-11-16). It counts each text node of the HTML, so `*wo*rd` is two words on AO3 and in Hedgequill. Thai joins Chinese and Japanese in counting a word per character. The fixtures' counts come from an independent copy of AO3's counter, run on the expected HTML. This gives up nothing; it supersedes M1's count, as decided on 2026-11-14.
- **The preview catches up a moment after typing stops,** 300 ms after the last keystroke, rather than on every key. It draws the whole chapter, and redrawing it per key would slow typing in a long one. This gives up a preview that changes letter by letter.
- **The preview's footnote lists what AO3 HTML now carries:** "Only paragraphs, emphasis, strikethrough, headings, quotes, lists, links and scene breaks are carried over." The prototype's line predates the 2026-11-14 decision. This gives up nothing.
- **Copy's sheet warns about AO3's limit instead of unconverted items.** The prototype's sheet lists items that couldn't be converted, but anything AO3 doesn't keep stays as text, so nothing is left out. Its warning box is used for a chapter over 500,000 characters, which still copies. This gives up nothing the prototype needs.
- **Summary and notes are drawn in the prototype** (version 4), as the 2026-11-14 decision asked. A chapter's overview gets a "For AO3" section with Summary, Beginning notes and End notes, each with its count against AO3's limit and Copy. The project menu's Summary and notes opens the same fields for the whole work, and a chapter edited since it was copied gets a dot in the navigator. This gives up nothing.
- **Summary and notes live in `hedgequill.json`,** the owner's call, under `details`, keyed by the chapter's path (`""` for the work), with each node's other details. They follow renames and moves, save atomically like the rest, and autosave a moment after typing stops and with every flush. This gives up a plain Markdown file another editor would show them in.
- **`hedgequill.json` moves to format 2.** A Hedgequill from before summaries would drop them the next time it saved the index, so it refuses a format 2 project, as it refuses any newer format. This gives up opening M3's projects in M1 and M2 builds.
- **A field's count is the length of the HTML its Copy gives,** since AO3 counts what's pasted into its form. This gives up a count of the Markdown typed.
- **Changed since copied compares fingerprints:** Copy chapter HTML keeps a short SHA-256 of the HTML in `hedgequill.json`, and a chapter is marked when its HTML differs. An edit that changes nothing on AO3, such as a space at the end of a paragraph, doesn't mark it, and undoing an edit clears the mark. This gives up keeping the text that was copied.
- **The editor shows strikethrough and links as it shows emphasis:** `~~struck~~` is struck through and a link's brackets and target fade. The prototype doesn't draw either, but they follow its rule for markers. This gives up a link that looks like a link in the editor; the preview shows it as one.
- **A save that meets a file another app holds waits it out.** While a sync client uploads a file, Windows refuses to replace it: access denied if the client lets others read, a sharing violation if not. Hedgequill showed the first as Read-only. Both now mean the file is held: the save tries again at the autosave delay for about ten seconds, then shows Couldn't save with Retry. The file is still replaced whole or not at all, and a file marked read-only still shows Read-only. This gives up an immediate Couldn't save while a file is held.
- **A file watcher picks up changes made in other apps,** as the 2026-11-13 decision left for later. A moment after another app changes a scene or page, Hedgequill reloads it if nothing in it is unsaved, or shows Changed on disk at once if something is. Its own saves don't count. This gives up nothing; a change the watcher misses still shows Changed on disk at the next save.
- **High contrast follows the system, with no setting of its own.** Windows' contrast themes and macOS's Increase Contrast switch Hedgequill to the prototype's high contrast, stronger text and solid borders, over whichever of light or dark View › Appearance picks, and back without a restart. Windows' contrast themes also pick their own colors; Hedgequill keeps its palette. This gives up the contrast theme's colors.
- **Find in project, earlier versions and text size are drawn in the prototype** (version 5), as the 2026-11-14 decision asked. Find in project takes the navigator's place, Earlier versions… is in a scene's row menu and opens a sheet, and Larger text, Smaller text and Actual size are in the View menu. This gives up nothing.
- **Earlier versions live in `Versions/`,** beside `Trash/`, under the same folders as their scene: `Chapter 1/Fog.md` keeps them as `Versions/Chapter 1/Fog/2026-11-16 09.12.05.md`, plain Markdown named for when it was kept. They follow the scene when it, its chapter or its act is renamed or moved. Scenes keep versions; empty text and Notes and Research pages don't. The sheet gives every version a time, since a day can keep several. This gives up a chapter named Versions at the top of the project, which Hedgequill now keeps for itself as it does Trash.
- **Find in project looks for the words as typed,** never a pattern, in every scene and page, and lists up to 50 matches a scene. Replace all puts in the replacement as typed, and its notice has Undo, which leaves alone any scene typed in since. Replace all saves through autosave, so each scene's first save that session keeps an earlier version. This gives up regular expressions.
- **Text size scales only the page's text,** from 80% to 200% in nine steps, as the prototype draws it. Titles, the navigator and the preview keep their size, and the text column keeps its width. On Windows the menu labels the shortcut Ctrl++, where the prototype writes Ctrl+=, because Avalonia names the = key by its shifted symbol, as Firefox does. This gives up the prototype's label.
- **Motion follows the prototype, and the system's reduced motion turns all of it off.** Up to M4 nothing moved. Now the navigator and preview slide over 260 ms on the soft spring, focus mode slides the page down and fades the header and toolbar, chevrons turn, hovers fade in, the save details pop in, and Saving… turns. Hedgequill reads Windows' Animation effects switch or macOS's Reduce motion once at launch, so a change made while it's open applies at the next launch. Avalonia exposes neither setting, so `Themes/ReducedMotion.cs` reads it with one native call per platform, the only native calls besides spell check's, and the guard lists every name it may pass. A setting in the app instead would have shown the motion before the people who need it found the switch. This gives up keeping native calls to spell check alone.
- **A sliding pane keeps its width and is cut off, as in the prototype,** so the navigator and the preview don't reflow every frame. The chapter beside them does reflow when its column narrows. With the typing probe's 10,000-word chapter, a slide's frames take 1.5 ms at the median and 3.2 ms at the 95th percentile on the owner's PC, under one 60 Hz frame. Opening the preview takes about 50 ms on its first frame, the preview laying out the whole chapter as it did before it slid, so that slide starts a little late. This gives up nothing the prototype shows.
- **A `v*` tag builds the installers and a draft release,** in `release.yml`, modeled on Prompuff's: Velopack and vpk 1.2.161, Windows Setup on the `win` channel, and a `.pkg` for each Mac on its own channel, `osx-arm64` or `osx-x64`, so an update never brings the other architecture. Both Macs build on the Apple Silicon runner, where NativeAOT cross-compiles the Intel one, which is smoke-tested under Rosetta. The bundle ID is `io.github.hazeliscoding.hedgequill`. Windows Setup is installed on a clean runner and the installed app opened before anything is published. A run by hand builds the same installers unsigned and publishes nothing. This gives up Prompuff's beta channels and delta updates; deltas can come once there's a release to update from.
- **The signing job signs in as `hedgequill-release-signing`,** an app registration with no secret, trusted only for this repo's `release` environment, which only `v*` tags can deploy to, and allowed only to sign with the one certificate profile. The environment holds the Azure IDs and the profile's names as variables, so they stay out of the public workflow. `signtool verify /pa` must pass on Setup and on the `Hedgequill.exe` it installs, or the job fails. A test tag, `v0.9.0`, proved it on 2026-11-16 and was deleted after: both files signed by Hazel Granados and timestamped by Microsoft ([run](https://github.com/hazeliscoding/hedgequill/actions/runs/43945220543)). This gives up nothing.
- **Help, About and the update notices are drawn in the prototype** (version 6). Help's three pages and About open as sheets with one Done; Check for updates… shows Checking, then Downloading, then "Update ready" with Restart, and once an update is ready the item reads Restart to finish updating. This gives up nothing.
- **How self-update works,** as decided on 2026-11-12: the check runs at launch and every four hours, quietly unless it finds an update, which downloads in the background and then shows "Update ready". The update installs after Hedgequill closes, through Velopack's updater, which waits for it to exit. Restart closes the way the window's close button does, so unsaved text is written, or the writer chooses to lose it, before anything installs, and it reopens the project that was open. A downloaded update that never got installed, after a crash, installs at the next launch. `Updates/Updater.cs` is the only file that names Velopack's update manager. This gives up an update that installs while Hedgequill stays open.
- **The release workflow tests the update itself:** it installs the build with Setup on a clean Windows runner, packs the same build as the next patch version into a local folder, points the installed app's check at that folder through `HEDGEQUILL_UPDATE_FEED`, closes the window, and checks that the next version is installed; it first passed on 2026-11-16 ([run](https://github.com/hazeliscoding/hedgequill/actions/runs/43959115041)). Each Mac `.pkg` is installed on the runner and the installed app opened. A network share in `HEDGEQUILL_UPDATE_FEED` is ignored, so it can't be a second way onto the network. This gives up testing the Mac's update the same way.
- **Help lives in the app, not on a website,** since Hedgequill opens no links. About shows the source address as text you can select. Writing in Hedgequill leaves out the prototype's Move to…, which the app doesn't have; it gives dragging and Alt+Up and Alt+Down instead. This gives up help that can change without a release.
- **Keyboard shortcuts shows each command's keys as its menu item does,** read from the menu, so they can't disagree. On Windows that's Ctrl+Return and Ctrl++, the names Avalonia gives those keys, where the prototype writes Ctrl+Enter and Ctrl+=. This gives up the prototype's labels.
- **Accessibility is three tests:** in every screen and sheet, each control Narrator and VoiceOver can reach has a name that isn't its type's; every button a mouse can press takes keyboard focus, except Bold, Italic, Scene break and the navigator's chevrons, whose keys are listed beside them; and `ContrastTests` holds text to 4.5:1 in all four themes. The name and focus checks each have a positive control. This gives up nothing.
- **Security reports go through GitHub's private vulnerability reporting,** which `SECURITY.md` points to, so the repo carries no email address. This gives up nothing.
- **The README drops its Alternatives table,** at the owner's request. Checked on 2026-11-12, it compared Hedgequill with Scrivener, novelWriter, Obsidian with Longform, Ellipsus, Typora and iA Writer, and said when to pick each one. `git show v1.0.1:README.md` still has it. This gives up telling a reader when another app would suit them better.
- **After 1.0 the shipped milestones fold into a Shipped table,** as the owner's other projects do, and Later's ideas become M6 to M9. M6 comes first: pasting from Google Docs or Word is the easiest way to bring a work in progress over, and M6 carries 1.0's last check. Linux and winget wait until M9, since each has a question to settle first, about who tests it and who updates it. This gives up M0 to M5's checklists in this file; `git show v1.0.1:ROADMAP.md` has them. Its M6 to M9 are now M7 to M10, since Markdown in notes went first (2026-11-18).

### 2026-11-18

- **Markdown in notes comes next, as M6,** at the owner's request. Pages are where a writer keeps character sheets, to-dos and research, and their lists, checklists, code and tables showed as plain text. The editor styles them in place, as it does emphasis, and in scenes too, since one editor draws both. Bring work in, send it out moves to M7 and the rest follow. This gives up starting with the milestone that brings a work in progress over from Google Docs or Word.
