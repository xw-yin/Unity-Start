"""Build a labeled comparison board from explicit local frames; never score motion."""
import argparse
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps


def build(manifest_path, output, columns=3, width=480):
    manifest_path = Path(manifest_path).resolve()
    data = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    kind = data.get("evidence_kind")
    if kind not in ("sparse_keyframes", "continuous_frames"):
        raise ValueError("Explicit evidence_kind is required")
    fps = float(data["fps"])
    if not math.isfinite(fps) or fps <= 0 or columns < 1 or width < 160:
        raise ValueError("Invalid fps, columns or width")
    frames = data["frames"]
    if not frames:
        raise ValueError("No frames")
    groups = {}
    loaded = []
    for item in frames:
        frame = item["frame"]
        if not isinstance(frame, int) or frame < 0:
            raise ValueError("frame must be a non-negative integer")
        groups.setdefault((item["label"], item["view"]), []).append(frame)
        path = Path(item["path"])
        if not path.is_absolute():
            path = manifest_path.parent / path
        with Image.open(path) as source:
            loaded.append(ImageOps.contain(source.convert("RGB"), (width, 360)))
    if kind == "continuous_frames":
        for group in groups.values():
            if len(group) < 2 or any(b != a + 1 for a, b in zip(group, group[1:])):
                raise ValueError("Continuous mode requires sequential frames per character/view")
    height = max(im.height for im in loaded) + 52
    canvas = Image.new("RGB", (columns * width, 84 + math.ceil(len(frames)/columns)*height), "#20242b")
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.load_default(size=16)
    draw.text((12, 10), str(data.get("title", "Frame review")), font=font, fill="white")
    draw.text((12, 37), f"{kind} | source fps {fps:g} | No motion-quality claim", font=font, fill="#edc76e")
    for i, (item, im) in enumerate(zip(frames, loaded)):
        x, y = (i % columns)*width, 84+(i//columns)*height
        canvas.paste(im, (x+(width-im.width)//2, y))
        caption = f'{item["label"]} | f{item["frame"]} | {item["frame"]/fps:.3f}s | {item["view"]}'
        draw.text((x+10, y+height-38), caption, font=font, fill="white")
    output = Path(output).resolve()
    if output == manifest_path or any(output == (Path(f["path"]) if Path(f["path"]).is_absolute() else manifest_path.parent/Path(f["path"])).resolve() for f in frames):
        raise ValueError("Output must not overwrite inputs")
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)
    print(json.dumps({"output": str(output), "count": len(frames), "kind": kind}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest")
    parser.add_argument("output")
    parser.add_argument("--columns", type=int, default=3)
    parser.add_argument("--width", type=int, default=480)
    args = parser.parse_args()
    build(args.manifest, args.output, args.columns, args.width)
