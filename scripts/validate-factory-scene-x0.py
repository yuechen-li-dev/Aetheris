"""Independent OpenUSD topology, appearance, bounds and PPTX media validation.
Run with the repository's OpenUSD Python environment after qualification exports.
"""
import hashlib
import json
import math
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from pxr import Usd, UsdGeom, UsdShade

output = Path(sys.argv[1] if len(sys.argv) > 1 else 'artifacts/local/factory-scene-x0').resolve()
stage = Usd.Stage.Open(str(output / 'factory.usda'))
assert stage and not stage.GetCompositionErrors()
assert UsdGeom.GetStageUpAxis(stage) == 'Z'
assert abs(UsdGeom.GetStageMetersPerUnit(stage) - .001) < 1e-12
prims = list(Usd.PrimRange.Stage(stage, Usd.TraverseInstanceProxies()))
meshes = [p for p in prims if p.IsA(UsdGeom.Mesh)]
cameras = [p for p in prims if p.IsA(UsdGeom.Camera)]
assert len(cameras) == 5 and len(meshes) > 1000
looks, luminous = set(), []
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
    emission = surface.GetInput('emissiveColor').Get()
    if emission is not None and any(v > 0 for v in emission):
        luminous.append(str(prim.GetPath()))
assert len(luminous) >= 18
inspection = json.loads((output / 'usd-export.json').read_text(encoding='utf-8-sig'))
assert len(meshes) == sum(n['kind'] in ('Part', 'EnvironmentPanel', 'WindowFinish') for n in inspection['nodes'])
box = UsdGeom.BBoxCache(Usd.TimeCode.Default(), ['default', 'render']).ComputeWorldBound(stage.GetDefaultPrim()).ComputeAlignedRange()
lo, hi = list(box.GetMin()), list(box.GetMax())
for actual, expected in zip(lo + hi, inspection['bounds']['minimumMm'] + inspection['bounds']['maximumMm']):
    assert abs(actual - expected) < .01, (actual, expected)
ns = {'m': 'http://schemas.microsoft.com/office/drawing/2017/model3d',
      'p': 'http://schemas.openxmlformats.org/presentationml/2006/main',
      'mc': 'http://schemas.openxmlformats.org/markup-compatibility/2006'}
models = fallbacks = 0
with zipfile.ZipFile(output / 'factory.pptx') as archive:
    for name in archive.namelist():
        if name.endswith('.rels'):
            assert not any(r.attrib.get('TargetMode') == 'External' for r in ET.fromstring(archive.read(name)))
        if name.startswith('ppt/slides/slide') and name.endswith('.xml'):
            xml = ET.fromstring(archive.read(name))
            models += len(xml.findall('.//m:model3d', ns))
            fallbacks += len(xml.findall('.//mc:Fallback/p:pic', ns))
    for slide, preview in [(1, 'hero.png'), (2, 'overview.png')]:
        assert archive.read(f'ppt/media/model3d-{slide}-1.glb') == (output / 'factory.glb').read_bytes()
        assert archive.read(f'ppt/media/model3d-{slide}-1.png') == (output / preview).read_bytes()
assert models == fallbacks == 2
office_roundtrip = None
if (output / 'factory-office-roundtrip.pptx').exists():
    with zipfile.ZipFile(output / 'factory-office-roundtrip.pptx') as archive:
        model_parts = [n for n in archive.namelist() if n.endswith('.glb')]
        assert model_parts and all(archive.read(n) == (output / 'factory.glb').read_bytes() for n in model_parts)
        model_count = sum(len(ET.fromstring(archive.read(n)).findall('.//m:model3d', ns))
                          for n in archive.namelist() if n.startswith('ppt/slides/slide') and n.endswith('.xml'))
        assert model_count == 2
        office_roundtrip = dict(model3d=model_count, embeddedModelParts=len(model_parts), exactGlbBytes=True)
validators = {}
for name in ('factory', 'factory-enclosed'):
    issues = json.loads((output / (name + '.validator.json')).read_text())['issues']
    assert issues['numErrors'] == issues['numWarnings'] == 0
    validators[name] = {k: issues[k] for k in ('numErrors', 'numWarnings', 'numInfos')}
report = dict(openUsd=dict(version=Usd.GetVersion(), compositionErrors=0, meshOccurrences=len(meshes),
                          sharedPrototypes=len(stage.GetPrototypes()), cameras=len(cameras), boundLooks=len(looks),
                          emissiveOccurrences=len(luminous), boundsMm=dict(min=lo,max=hi)),
              glb=validators, powerpointPackage=dict(slides=2, model3d=models, fallbackPictures=fallbacks,
              embeddedModelSha256=hashlib.sha256((output / 'factory.glb').read_bytes()).hexdigest(), externalRelationships=0,
              officeRoundtrip=office_roundtrip, desktopInteraction='see powerpoint-native-validation.json'))
(output / 'external-validation.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report))
