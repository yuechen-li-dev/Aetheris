"""Deterministic two-sided diagnostic captures of Aetheris display meshes.

These captures do not substitute for Cadmata orbit/selection qualification.
No surfaces are added, healed, culled, or reconstructed by this renderer.
"""
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

root = Path(sys.argv[1])
tiles = []
for directory in sorted(p for p in root.iterdir() if p.is_dir()):
    report_path = directory / "diagnostics.json"
    report = json.loads(report_path.read_text(encoding="utf-8-sig")) if report_path.exists() else {}
    mesh_path = directory / "mesh.json"
    patches = json.loads(mesh_path.read_text()) if mesh_path.exists() else []
    vertices, triangles = [], []
    for patch in patches:
        offset = len(vertices)
        vertices.extend(patch["Positions"])
        indices = patch["TriangleIndices"]
        triangles.extend([[offset + n for n in indices[i:i+3]] for i in range(0, len(indices), 3)])
    for name, direction in [("iso", [1., -1.3, .9]), ("opposite-iso", [-1., 1.3, -.9])]:
        canvas = Image.new("RGB", (900, 800), (240, 243, 248))
        draw = ImageDraw.Draw(canvas)
        draw.text((20, 15), directory.name + " / " + name, fill=(25, 30, 40))
        draw.text((20, 35), report.get("ImportStatus", "Failed") + " / " + report.get("Filename", "worker failed"), fill=(40, 45, 55))
        if triangles:
            points = np.asarray(vertices)
            tri = np.asarray(triangles)
            view = np.asarray(direction)
            view /= np.linalg.norm(view)
            right = np.cross(view, [0., 0., 1.])
            right /= np.linalg.norm(right)
            up = np.cross(view, right)
            centered = points - (points.min(axis=0) + points.max(axis=0)) / 2
            projected = np.column_stack((centered @ right, centered @ up, centered @ view))
            scale = min(780 / max(np.ptp(projected[:, 0]), 1e-9), 650 / max(np.ptp(projected[:, 1]), 1e-9))
            xy = np.column_stack((450 + projected[:, 0] * scale, 425 - projected[:, 1] * scale))
            verts = points[tri]
            normal = np.cross(verts[:, 1] - verts[:, 0], verts[:, 2] - verts[:, 0])
            length = np.linalg.norm(normal, axis=1)
            valid = length > 1e-13
            normal[valid] /= length[valid, None]
            brightness = .3 + .7 * np.abs(normal @ view)
            order = np.where(valid)[0]
            order = order[np.argsort(projected[tri[order], 2].mean(axis=1), kind="stable")]
            for index in order:
                shade = brightness[index]
                draw.polygon([tuple(xy[n]) for n in tri[index]], fill=tuple(int(c * shade) for c in (140, 168, 198)))
        else:
            draw.text((80, 350), "NO IMPORTED DISPLAY GEOMETRY - see diagnostics", fill=(180, 30, 30))
        canvas.save(directory / (name + ".png"))
        if name == "iso":
            canvas.thumbnail((360, 320))
            tiles.append(canvas)
    print(directory.name, len(triangles), "triangles")
contact = Image.new("RGB", (360 * 4, 320 * ((len(tiles) + 3) // 4)), "white")
for index, tile in enumerate(tiles):
    contact.paste(tile, ((index % 4) * 360, (index // 4) * 320))
contact.save(root / "contact-sheet.png")
