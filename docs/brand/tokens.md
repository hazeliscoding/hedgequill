# Brand and design tokens

Hedgequill's look comes from the UI prototype: the prototype's base design tokens, re-tuned for a writing tool, with a burgundy accent on warm neutrals. When the app is built, these become its Avalonia resources. Change them there and here together.

## Logo

- `mark.svg` and `mark-dark.svg`: the spiny quill, a quill pen whose vane has hedgehog spines. The shaft is a gap cut through the vane, not a painted line, so it shows whatever is behind it.
- `lockup.svg` and `lockup-dark.svg`: the mark beside the wordmark in Gloock, outlined to paths because GitHub can't load web fonts inside an SVG. `scripts/make_lockup.py` rebuilds them from the marks; rerun it whenever a mark changes.
- On a dark taskbar or dock, the app icon puts the light mark on a paper tile (`#f0ede6`), because the ink nib disappears on dark backgrounds.

| | Light | Dark |
|---|---|---|
| Vane | `#6e2633` | `#c4677a` |
| Nib and wordmark | `#231f1c` | `#ece6da` |

## Type

| Role | Family | Notes |
|---|---|---|
| Manuscript | Cardo | The page you write on. Fallbacks: Iowan Old Style, Palatino Linotype, Georgia. The text column is 620 px wide (640 for a Notes or Research page). |
| Titles | Gloock | Welcome title (44 px), chapter and document titles (24–34 px). |
| Interface | The system UI font | Segoe UI on Windows, the system font on macOS. |

Cardo and Gloock are under the SIL Open Font License 1.1 and ship inside the app as static TTF files, because Avalonia reads neither WOFF2 nor variable fonts.

Interface type scale: caption 11/14, callout 12/16, body 13/18, heading 15/20, display 28/34 (size/line height in px).

## Color

| Token | Light | Dark |
|---|---|---|
| Accent | `#6e2633` | `#a14a5c` |
| Accent hover | `#5d1f2b` | `#ad5668` |
| Accent pressed | `#4d1923` | `#8f3f50` |
| Accent soft | `#f1e3e5` | `rgba(161,74,92,.18)` |
| Accent text and links | `#6e2633` | `#dc98a4` |
| Focus ring | `#6e2633` | `#dc98a4` (the dark accent is only 2.9:1 on the page) |
| Window and sidebar | `#f0ede6` | `#1a1917` |
| Page (editor) | `#fbf8f1` | `#201f1d` |
| Raised (menus, dialogs) | `#fffdf8` | `#2b2927` |
| Sunken | `#f5f2eb` | `#1a1917` |
| Toolbar | `#f3f0e9` | `#1d1c1a` |
| Text | `#231f1c` | `#ece6da` |
| Text, secondary | `#5f5852` | `#aaa399` |
| Text, tertiary | `#6c655f` | `#968f86` |
| Markdown markers (faded) | `#9a918a` | `#847c73` |
| Selection in the manuscript | `rgba(110,38,51,.16)` | `rgba(161,74,92,.3)` |
| Danger | `#d9453b`, text `#b1362d` | |
| Warning | `#c9830f`, text `#83560a` | |
| Success | `#2f9a5a`, text `#1f6f41` | |

Every text color keeps 4.5:1 on the backgrounds it sits on. The prototype's tertiary text (`#7d756d` light, `#8a837a` dark) fell to 3.9–4.4:1, so it's darkened and lightened here. Faded Markdown markers are the one exception: they're meant to recede (2.9:1 light, 4.0:1 dark), and the text they wrap stays full contrast.

## Spacing, radius and shadow

- **Spacing (px):** 0, 2, 4, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64.
- **Radius (px):** 3, 5, 7, 12, 16, plus 999 for pills.
- **Shadows**, light (dark uses the same shapes at much higher opacity, with a white hairline):
  1. `0 1px 2px rgba(0,0,0,.06), 0 0 0 .5px rgba(0,0,0,.08)` for controls;
  2. `0 6px 16px -4px rgba(0,0,0,.12), 0 2px 4px rgba(0,0,0,.05)` for popovers;
  3. `0 16px 40px -8px rgba(0,0,0,.2), 0 4px 10px rgba(0,0,0,.06)` for dialogs;
  4. `0 40px 90px -10px rgba(0,0,0,.35), 0 12px 28px rgba(0,0,0,.14)` for windows.
- **Motion (ms):** 60, 120, 180, 260, 360. All motion is off when the system asks for reduced motion.
