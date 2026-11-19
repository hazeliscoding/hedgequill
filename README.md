<h1 align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/brand/lockup-dark.svg">
    <img alt="Hedgequill" src="docs/brand/lockup.svg" height="56">
  </picture>
</h1>

**A quiet place to write.** Hedgequill is a free desktop app for writing fanfiction on your own computer and posting it to AO3.

Writing apps for long fiction tend to cost money, or come crowded with features a fic doesn't need. A plain Markdown editor has no idea what a chapter or a scene is. And getting a chapter into AO3 with its italics and scene breaks intact means pasting it in and checking it by hand. Hedgequill does the whole job in one calm window, and your words stay in plain files on your computer.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/screenshots/demo-dark.gif">
  <img alt="Writing a scene in Hedgequill. A new paragraph is typed; Ctrl+I starts italics, whose markers stay faded, and the AO3 preview on the right catches up as the header goes from Saving… to Saved. Ctrl+Shift+C copies the chapter as HTML for AO3. Ctrl+Shift+F slides the list and the preview away for focus mode, and Escape slides them back. Ctrl+/ opens the keyboard shortcuts." src="docs/screenshots/demo-light.gif">
</picture>

> **Status: 1.0 is out.** [Download it](#download) for Windows or macOS. [ROADMAP.md](ROADMAP.md) says what's planned next.

## Before and after

| You're… | In one long document | In Hedgequill |
|---|---|---|
| moving a scene | cut and paste, then find where it was | drag it in the list |
| writing in italics | toolbar buttons, or markup you have to remember | Ctrl+I; the markers stay faded, so italics look like italics |
| posting a chapter | copy it out, then check what survived the paste | Copy for AO3, then paste into AO3's HTML editor; the preview shows what AO3 will keep |
| hitting a save error | hope the last save was recent | your text stays on screen, with Retry and Save a copy… |

## What it does

- One window holds the list of acts, chapters and scenes, with Notes and Research pages, the page you're writing, and an AO3 preview you can hide.
- There's no markup to learn. The files are Markdown, but Ctrl+B, Ctrl+I and the scene-break button type the markers for you.
- Notes and Research pages take lists, checklists, code and tables, and Ctrl+E shows a page without its markers, as Obsidian's reading view does.
- Pasting from Google Docs or Word keeps italics and bold. Copy for a beta reader sends a chapter to Google Docs or an email with them, and `<!-- comments -->` to yourself stay out of what you post.
- Copy for AO3 puts a chapter on the clipboard as HTML for AO3's editor. It keeps only what AO3 keeps: paragraphs, emphasis, strikethrough, headings, quotes, lists, links and scene breaks. Word counts match AO3's.
- A project is a folder of Markdown files plus one small file for their order. Any text editor can open them, with or without Hedgequill, and the folder can live in OneDrive or another synced folder.
- There's also spell check, find and replace across the project, earlier versions of each scene, focus mode and a text size you can change.

## The promise

- **Never lose words.** Hedgequill saves as you type. If a save fails, your text stays on screen until it's somewhere safe.
- **Private.** There's no account and no telemetry, and your writing never leaves your computer unless you copy it out. The only network request is the update check, which asks GitHub Releases for a newer version and sends nothing about you or your writing.
- **Up to date.** Updates download quietly and install when you close the app. It never restarts on its own.

Hedgequill runs on Windows and macOS, and it's open source, so you can check all of this.

## Download

From the [latest release](https://github.com/hazeliscoding/hedgequill/releases/latest):

| Computer | Download |
|---|---|
| Windows x64 (10 and 11) | `Hedgequill-Setup.exe`, signed |
| Mac with Apple Silicon | `Hedgequill-macOS-arm64.pkg` |
| Mac with Intel | `Hedgequill-macOS-x64.pkg` |

None of them needs anything else installed. Every release also has a `SHA256SUMS` file: check a download with `shasum -a 256 --check --ignore-missing SHA256SUMS` on a Mac, or `Get-FileHash Hedgequill-Setup.exe` in PowerShell.

Until enough people have downloaded it, Windows may say it "protected your PC". Choose **More info**, check that the publisher is Hazel Granados, then **Run anyway**.

The Mac builds aren't notarized by Apple, so macOS blocks the installer the first time:

1. Open the `.pkg` and close the warning.
2. Go to **System Settings › Privacy & Security**, scroll down to Security, and choose **Open Anyway**.
3. Confirm once more.

## Contributing

Issues are welcome. [CONTRIBUTING.md](CONTRIBUTING.md) explains how to build and test a change, and [ROADMAP.md](ROADMAP.md) has the open work and the decisions behind it.

## License

[Apache-2.0](LICENSE). The wordmark uses [Gloock](https://github.com/google/fonts/tree/main/ofl/gloock), under the SIL Open Font License 1.1.
