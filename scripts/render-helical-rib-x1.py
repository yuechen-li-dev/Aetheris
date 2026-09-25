from pathlib import Path
import sys
import numpy as np
from PIL import Image, ImageDraw

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("artifacts/local/helical-rib-x1")
root.mkdir(parents=True, exist_ok=True)
vertices = []
faces = []
with (root / "helical-rib-24.obj").open() as stream:
    for line in stream:
        if line.startswith("v "):
            vertices.append([float(x) for x in line.split()[1:]])
        elif line.startswith("f "):
            faces.append([int(x) - 1 for x in line.split()[1:]])
points = np.asarray(vertices)
triangles = np.asarray(faces)
center = np.array([0.0, 0.0, 15.55])
view = np.array([1.0, -1.5, 0.65])
view /= np.linalg.norm(view)
right = np.cross([0.0, 0.0, 1.0], view)
right /= np.linalg.norm(right)
up = np.cross(view, right)
projected = np.column_stack(((points-center) @ right, (points-center) @ up, (points-center) @ view))
width, height = 1400, 1600
scale = min((width-180)/(np.ptp(projected[:,0])), (height-180)/(np.ptp(projected[:,1])))
xy = np.column_stack((width/2 + projected[:,0]*scale, height/2-projected[:,1]*scale))
verts = points[triangles]
normals = np.cross(verts[:,1]-verts[:,0], verts[:,2]-verts[:,0])
normlen = np.linalg.norm(normals, axis=1)
valid = normlen > 1e-13
normals[valid] /= normlen[valid,None]
light = np.array([0.5,-0.7,0.6])
light /= np.linalg.norm(light)
brightness = np.clip(0.4 + 0.6*(normals @ light), 0.18, 1.0)
depth = projected[triangles,2].mean(axis=1)
visible = np.where(valid & ((normals @ view) > 0))[0]
order = visible[np.argsort(depth[visible])]
image = Image.new("RGB", (width,height), (245,247,251))
draw = ImageDraw.Draw(image)
for index in order:
    shade = float(brightness[index])
    color = (int(58+118*shade), int(83+130*shade), int(121+124*shade))
    draw.polygon([tuple(xy[v]) for v in triangles[index]], fill=color)
image.save(root / "helical-rib-24.png")
print(len(order), "visible triangles")
