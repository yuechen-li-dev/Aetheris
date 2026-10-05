"""Read-only external OpenUSD and embedded-PowerPoint evidence, no house authoring.
Run with the qualified OpenUSD Python environment after CLI exports/deck compilation.
"""
import hashlib
import json
import math
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from pxr import Usd, UsdGeom, UsdShade

output = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/local/archviz-house-x0').resolve()
stage = Usd.Stage.Open(str(output / 'house.usda'))
assert stage and not stage.GetCompositionErrors()
assert UsdGeom.GetStageUpAxis(stage) == 'Z'
unit = UsdGeom.GetStageMetersPerUnit(stage)
assert abs(unit - .001) < 1e-12
prims = list(Usd.PrimRange.Stage(stage, Usd.TraverseInstanceProxies()))
meshes = [p for p in prims if p.IsA(UsdGeom.Mesh)]
cameras = [p for p in prims if p.IsA(UsdGeom.Camera)]
assert len(cameras) == 3
looks = set()
translucent = []
for prim in meshes:
    mesh = UsdGeom.Mesh(prim)
    points = mesh.GetPointsAttr().Get()
    indices = mesh.GetFaceVertexIndicesAttr().Get()
    assert points and indices and all(0 <= i < len(points) for i in indices)
    assert all(math.isfinite(v) for point in points for v in point)
    material, _ = UsdShade.MaterialBindingAPI(prim).ComputeBoundMaterial()
    assert material, str(prim.GetPath())
    looks.add(str(material.GetPath()))
    surface = material.ComputeSurfaceSource()[0]
    opacity = surface.GetInput('opacity').Get()
    if opacity is not None and opacity < 1:
        translucent.append(str(prim.GetPath()))
        assert abs(opacity - .18) < 1e-6
assert len(translucent) == 3
inspection = json.loads((output / 'usd-export.json').read_text(encoding='utf-8-sig'))
assert len(meshes) == sum(n['kind'] in ('Part', 'EnvironmentPanel', 'WindowFinish') for n in inspection['nodes'])
box = UsdGeom.BBoxCache(Usd.TimeCode.Default(), ['default', 'render']).ComputeWorldBound(stage.GetDefaultPrim()).ComputeAlignedRange()
lo = list(box.GetMin()); hi = list(box.GetMax())
for actual, expected in zip(lo + hi, inspection['bounds']['minimumMm'] + inspection['bounds']['maximumMm']):
    assert abs(actual - expected) < .01, (actual, expected)

def sha(data):
    return hashlib.sha256(data).hexdigest()

ns = {'m': 'http://schemas.microsoft.com/office/drawing/2017/model3d',
      'p': 'http://schemas.openxmlformats.org/presentationml/2006/main',
      'mc': 'http://schemas.openxmlformats.org/markup-compatibility/2006'}
models = 0
fallbacks = 0
with zipfile.ZipFile(output / 'house.pptx') as archive:
    names = archive.namelist()
    for name in names:
        if name.endswith('.rels'):
            assert not any(r.attrib.get('TargetMode') == 'External' for r in ET.fromstring(archive.read(name)))
        if name.startswith('ppt/slides/slide') and name.endswith('.xml'):
            xml = ET.fromstring(archive.read(name))
            models += len(xml.findall('.//m:model3d', ns))
            fallbacks += len(xml.findall('.//mc:Fallback/p:pic', ns))
    for slide, preview in [(1, 'hero.png'), (2, 'overview.png')]:
        assert archive.read(f'ppt/media/model3d-{slide}-1.glb') == (output / 'house.glb').read_bytes()
        assert archive.read(f'ppt/media/model3d-{slide}-1.png') == (output / preview).read_bytes()
assert models == fallbacks == 2
roundtrip = None
if (output / 'house-office-roundtrip.pptx').exists():
    with zipfile.ZipFile(output / 'house-office-roundtrip.pptx') as archive:
        assets = [name for name in archive.namelist() if name.endswith('.glb')]
        assert assets and all(archive.read(name) == (output / 'house.glb').read_bytes() for name in assets)
        model_count = sum(len(ET.fromstring(archive.read(name)).findall('.//m:model3d', ns))
                          for name in archive.namelist() if name.startswith('ppt/slides/slide') and name.endswith('.xml'))
        assert model_count == 2
        roundtrip = dict(model3d=model_count, embeddedGlbParts=len(assets), exactModelBytes=True)
validators = {}
for name in ('house', 'house-enclosed'):
    issues = json.loads((output / (name + '.validator.json')).read_text())['issues']
    assert issues['numErrors'] == 0
    validators[name] = {k: issues[k] for k in ('numErrors', 'numWarnings', 'numInfos')}
report = dict(openUsd=dict(version=Usd.GetVersion(), compositionErrors=0, meshOccurrences=len(meshes),
                          sharedPrototypes=len(stage.GetPrototypes()), cameras=[str(p.GetPath()) for p in cameras],
                          boundLooks=len(looks), translucentPanes=len(translucent), boundsMm=dict(min=lo,max=hi)),
              glb=validators,
              powerpointPackage=dict(slides=2, model3d=models, fallbackPictures=fallbacks,
                                     embeddedModelSha256=sha((output / 'house.glb').read_bytes()), externalRelationships=0,
                                     officeRoundtrip=roundtrip))
(output / 'external-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
