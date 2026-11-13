"""Builds docs/brand/lockup.svg and lockup-dark.svg: the mark beside "Hedgequill" in outlined Gloock.

GitHub can't load web fonts inside an SVG, so the wordmark is converted to paths.
Rerun this whenever mark.svg or mark-dark.svg changes.

Usage:
  uv run --no-project --with fonttools --with uharfbuzz python -I scripts/make_lockup.py <Gloock-Regular.ttf> docs/brand

Gloock is under the SIL Open Font License 1.1:
  https://github.com/google/fonts/tree/main/ofl/gloock
"""

import re
import sys
from pathlib import Path

import uharfbuzz as hb
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.ttLib import TTFont

WORD = "Hedgequill"
INK = {"": "#231f1c", "-dark": "#ece6da"}  # --text-primary, light and dark, from the prototype
MARK = 64  # the mark's viewBox is 64 x 64
BASELINE = 46  # centres the word's capitals on the quill
CAP_HEIGHT = 26
GAP = 0  # the quill leaves 11 px clear on its right, which is gap enough


def num(v: float) -> str:
    return f"{v:.2f}".rstrip("0").rstrip(".")


def wordmark(font_path: str) -> tuple[str, float]:
    font = TTFont(font_path)
    scale = CAP_HEIGHT / font["OS/2"].sCapHeight
    glyphs = font.getGlyphSet()
    order = font.getGlyphOrder()

    buf = hb.Buffer()
    buf.add_str(WORD)
    buf.guess_segment_properties()
    hb.shape(hb.Font(hb.Face(hb.Blob.from_file_path(font_path))), buf)  # applies the font's kerning

    pen = SVGPathPen(glyphs, ntos=num)
    x = 0
    for info, pos in zip(buf.glyph_infos, buf.glyph_positions):
        # Font units point up; SVG points down. Origin is the baseline at the word's left edge.
        offset = ((x + pos.x_offset) * scale, -pos.y_offset * scale)
        glyphs[order[info.codepoint]].draw(TransformPen(pen, (scale, 0, 0, -scale, *offset)))
        x += pos.x_advance
    return pen.getCommands(), x * scale


def mark_body(path: Path) -> str:
    svg = path.read_text(encoding="utf-8")
    body = re.search(r"<svg[^>]*>(.*)</svg>", svg, re.S).group(1)
    return re.sub(r"<title>.*?</title>", "", body).strip()


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    font_path, out = sys.argv[1], Path(sys.argv[2])
    word, word_width = wordmark(font_path)
    width = MARK + GAP + word_width + 2
    for suffix, ink in INK.items():
        svg = (
            f'<svg xmlns="http://www.w3.org/2000/svg" width="{num(width)}" height="{MARK}" '
            f'viewBox="0 0 {num(width)} {MARK}" fill="none" role="img" aria-label="Hedgequill">'
            f"<title>Hedgequill</title>"
            f"{mark_body(out / f'mark{suffix}.svg')}"
            f'<path transform="translate({MARK + GAP} {BASELINE})" d="{word}" fill="{ink}"/>'
            f"</svg>\n"
        )
        (out / f"lockup{suffix}.svg").write_text(svg, encoding="utf-8", newline="\n")
        print(f"wrote {out / f'lockup{suffix}.svg'} ({num(width)} x {MARK})")


if __name__ == "__main__":
    main()
