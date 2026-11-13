"""Builds the app icons from docs/brand/app-icon.svg: app-icon.png (1024 px), app-icon.ico and app-icon.icns.

Each size is rendered from the SVG, not scaled down from a bigger bitmap, so small icons stay sharp.
Rerun this whenever app-icon.svg changes.

Usage:
  uv run --no-project --with resvg-py --with pillow python -I scripts/make_icons.py docs/brand
"""

import io
import sys
from pathlib import Path

import resvg_py
from PIL import Image

ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]
ICNS_SIZES = [16, 32, 64, 128, 256, 512, 1024]


def render(svg: str, size: int) -> Image.Image:
    png = bytes(resvg_py.svg_to_bytes(svg_string=svg, width=size, height=size))
    return Image.open(io.BytesIO(png)).convert("RGBA")


def main() -> None:
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    brand = Path(sys.argv[1])
    svg = (brand / "app-icon.svg").read_text(encoding="utf-8")

    render(svg, 1024).save(brand / "app-icon.png", optimize=True)

    ico = [render(svg, size) for size in ICO_SIZES]
    ico[-1].save(brand / "app-icon.ico", format="ICO", sizes=[(s, s) for s in ICO_SIZES], append_images=ico[:-1])

    icns = [render(svg, size) for size in ICNS_SIZES]
    icns[-1].save(brand / "app-icon.icns", format="ICNS", append_images=icns[:-1])

    for name in ("app-icon.png", "app-icon.ico", "app-icon.icns"):
        print(f"wrote {brand / name} ({(brand / name).stat().st_size:,} bytes)")


if __name__ == "__main__":
    main()
