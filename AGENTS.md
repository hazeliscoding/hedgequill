# AGENTS.md

These are the working rules for agents in this repo. Hedgequill is a desktop app (.NET 10, Avalonia 12, Apache-2.0) for writing fanfiction offline and posting it to AO3.

## Sources of truth

- `README.md`: the pitch and the promise.
- `ROADMAP.md`: decisions already made, the milestones to build, and what's out of scope. Check it before proposing features, and respect its decisions unless the owner reopens them. Work from the next unchecked item, tick it off when it's done, and don't build past the current milestone without asking. Record new decisions there, dated, each with its reason and what it gives up, in the same change that needs them. Mark a changed decision superseded; don't delete it.
- New work goes in `ROADMAP.md` too: a box in a milestone, or a Later idea until it has one. GitHub issues are for bug reports and contributors' tasks, never roadmap work. Don't add plan, spec or backlog documents.
- **The UI prototype** is the reference for layout, states and copy: the claude.ai artifact "Hedgequill Prototype", which isn't public. The owner shares what a task needs from it. Build what it shows; don't redesign in code. `docs/brand/tokens.md` holds its colors, type and spacing.

## Hard rules

- **Never lose words.** Autosave. Write a file atomically (temporary file, then replace), so a crash never leaves half of it. When a save fails, the text stays in the editor and the user gets Retry and Save a copy…, as the prototype shows. Never discard unsaved text without the user choosing to: not on close, not on reload, not on update. A writer who loses a chapter once won't come back.
- **The update check is the only network code.** Velopack makes it, from one file in `Hedgequill.Desktop`. No telemetry, no analytics, no crash reports the app sends itself, no fonts or images fetched at runtime, and no other network use. `Hedgequill.Core` never references Velopack or networking. Tests fail on both. The privacy promise in the README depends on this.
- **Never restart into an update on your own.** A downloaded update installs when the user closes Hedgequill, or when they choose to restart. An unprompted restart can interrupt a sentence.
- **The user's files stay plain Markdown** that any text editor can open. Hedgequill's own data in a project is one small file for order and details. Never write a format only Hedgequill can read.
- **Tests and fixtures use invented sample text, never a real fic.** The owner's writing is private, and this repo is public.
- **AO3 HTML stays within what AO3 keeps:** paragraphs, emphasis, headings, quotes and scene breaks. Anything else becomes plain text, so the preview matches what gets posted.
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

- **Project:** a folder of Markdown files plus Hedgequill's small file for their order and details.
- **Act, chapter, scene:** the three levels of the story, in that order. A scene is the smallest part the navigator moves. A chapter is what Copy for AO3 copies. How they map to files is settled in M1.
- **Page:** a Markdown file in Notes or Research, outside the story.
- **Navigator:** the list on the left that shows the project's acts, chapters, scenes and pages.
- **Scene break:** the marker between scenes inside a chapter. It's a plain Markdown break in the file.
- **Markers:** the Markdown characters that make emphasis and headings. The editor shows them faded.
- **AO3 HTML:** the HTML Copy for AO3 produces, limited to what AO3 keeps.
- **Save state:** Saving…, Saved, Couldn't save, Changed on disk or Read-only, as the prototype shows them.

## Commands

The solution arrives with M0. Until then:

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
