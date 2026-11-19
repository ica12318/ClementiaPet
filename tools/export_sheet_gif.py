from pathlib import Path
import sys

from PIL import Image


def main() -> int:
    if len(sys.argv) < 3:
        print("usage: export_sheet_gif.py SHEET.png OUTPUT.gif [CELL] [COLUMNS] [FRAMES] [DURATION_MS]")
        return 2
    sheet_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])
    cell = int(sys.argv[3]) if len(sys.argv) > 3 else 288
    columns = int(sys.argv[4]) if len(sys.argv) > 4 else 8
    frames = int(sys.argv[5]) if len(sys.argv) > 5 else 48
    duration = int(sys.argv[6]) if len(sys.argv) > 6 else 50

    sheet = Image.open(sheet_path).convert("RGBA")
    images = []
    for index in range(frames):
        x = (index % columns) * cell
        y = (index // columns) * cell
        tile = sheet.crop((x, y, x + cell, y + cell))
        palette = tile.convert("P", palette=Image.Palette.ADAPTIVE, colors=255)
        palette.info["transparency"] = 0
        images.append(palette)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    images[0].save(
        output_path,
        save_all=True,
        append_images=images[1:],
        duration=duration,
        loop=0,
        disposal=2,
        transparency=0,
        optimize=False,
    )
    print(output_path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
