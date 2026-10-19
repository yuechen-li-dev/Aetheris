"""Inspect real Helios downloads after qualify-helios-exports.mts and CLI parity exports.
Requires pypdf; pypdfium2 additionally renders the downloaded PDF for visual review.
"""
from hashlib import sha256
from pathlib import Path
import json
import math
import struct
import xml.etree.ElementTree as ET
import zipfile
from pypdf import PdfReader

root = Path(__file__).resolve().parent.parent / "artifacts/local/helios-exports"
families = {
    "inspection-plate": {"step", "glb", "usda", "stl", "obj", "pdf", "svg", "pptx"},
    "shared-block-assembly": {"step", "glb", "usda"},
    "scene": {"glb", "usda"},
}
for name, extensions in families.items():
    with zipfile.ZipFile(root / (name + "-exports.zip")) as archive:
        assert archive.testzip() is None
        assert set(archive.namelist()) == {name + "." + ext for ext in extensions}
        destination = root / name
        destination.mkdir(exist_ok=True)
        for entry in archive.infolist():
            assert "/" not in entry.filename and "\\" not in entry.filename
            (destination / entry.filename).write_bytes(archive.read(entry))
    glb = (destination / (name + ".glb")).read_bytes()
    magic, version, length, json_length, json_type = struct.unpack_from("<5I", glb)
    assert magic == 0x46546C67 and version == 2 and length == len(glb) and json_type == 0x4E4F534A
    document = json.loads(glb[20:20 + json_length])
    assert document["asset"]["version"] == "2.0" and document["meshes"] and document["nodes"]
    assert any(node.get("name") == "Aetheris mm Z-up to glTF m Y-up" for node in document["nodes"])
    if name == "scene":
        assert len(document["cameras"]) == 3
    usd = (destination / (name + ".usda")).read_text(encoding="utf-8")
    assert usd.startswith("#usda 1.0") and "metersPerUnit = 0.001" in usd and 'upAxis = "Z"' in usd

part = root / "inspection-plate"
step = (part / "inspection-plate.step").read_bytes()
assert step == (root / "inspection-plate.step").read_bytes() == (root / "native-plate.step").read_bytes()
stl = (part / "inspection-plate.stl").read_bytes()
assert stl == (root / "native-plate.stl").read_bytes()
triangles, = struct.unpack_from("<I", stl, 80)
assert triangles > 0 and len(stl) == 84 + 50 * triangles
vertices = []
for offset in range(84, len(stl), 50):
    values = struct.unpack_from("<12fH", stl, offset)
    assert all(math.isfinite(v) for v in values)
    assert abs(sum(v*v for v in values[:3]) - 1) < 1e-5
    vertices.extend((values[3:6], values[6:9], values[9:12]))
assert [min(v[i] for v in vertices) for i in range(3)] == [-50, -35, 0]
assert [max(v[i] for v in vertices) for i in range(3)] == [50, 35, 8]
assert "f " in (part / "inspection-plate.obj").read_text(encoding="utf-8")
assert ET.parse(part / "inspection-plate.svg").getroot().tag.endswith("svg")
reader = PdfReader(part / "inspection-plate.pdf")
text = "\n".join(page.extract_text() for page in reader.pages)
for required in ("THRU", "X0", "Y0", "100", "70", "NOT FOR MANUFACTURE", "INCOMPLETE REQUIREMENTS"):
    assert required in text, required
(root / "pdf-text.txt").write_text(text, encoding="utf-8")
with zipfile.ZipFile(part / "inspection-plate.pptx") as deck:
    assert deck.testzip() is None and "ppt/slides/slide1.xml" in deck.namelist()
try:
    import pypdfium2 as pdfium
    document = pdfium.PdfDocument(str(part / "inspection-plate.pdf"))
    for i in range(len(document)):
        document[i].render(scale=1.8).to_pil().save(root / f"drawing-page-{i+1}.png")
except ImportError:
    print("Install pypdfium2 or render with pdftoppm for visual review.")
evidence = {"success": True, "partFormats": len(families["inspection-plate"]), "pdfPages": len(reader.pages),
            "stlTriangles": triangles, "stepByteIdenticalToNativeCli": True, "stlByteIdenticalToNativeCli": True,
            "stepSha256": sha256(step).hexdigest(), "stlSha256": sha256(stl).hexdigest()}
(root / "files.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
print(json.dumps(evidence, indent=2))
