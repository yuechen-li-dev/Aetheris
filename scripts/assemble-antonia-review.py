"""Assemble existing local renders into a labelled visual review sheet."""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "artifacts/local/humanoid-production"


def main():
    column_width = 360
    row_height = 440
    header_height = 90
    sheet = Image.new("RGB", (column_width * 3, header_height + row_height * 4), "#24282d")
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 19)
    small_font = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 15)
    titles = ("Previous A-pose weights / LBS", "Improved Antonia / DQS", "Local Genesis 9 reference")
    for column, title in enumerate(titles):
        draw.text((column * column_width + 12, 16), title, fill="white", font=font)
    draw.text((12, 51), "Identical Antonia requests; Genesis is a separate visual benchmark.",
              fill="#c8cdd2", font=small_font)
    poses = ("hip90", "knee90", "elbow120", "shoulder120")
    for row, pose in enumerate(poses):
        paths = (
            BUNDLE / "comparison" / f"before--{pose}.png",
            BUNDLE / "renders" / f"candidate--{pose}.png",
            BUNDLE / "comparison" / f"genesis--{pose}.png",
        )
        for column, path in enumerate(paths):
            with Image.open(path) as source:
                rendered = source.convert("RGB")
                rendered.thumbnail((column_width, row_height - 35))
            x = column * column_width + (column_width - rendered.width) // 2
            y = header_height + row * row_height + 35
            sheet.paste(rendered, (x, y))
            label = pose
            if column == 2 and pose == "hip90":
                label += " (actual 87.519 degrees)"
            draw.text((column * column_width + 12, y - 27), label, fill="white", font=small_font)
    output = BUNDLE / "comparison" / "review-sheet.png"
    sheet.save(output)
    print(output)


if __name__ == "__main__":
    main()
