"""Validate an Aetheris USDA/evidence pair using the external OpenUSD SDK.

Run with NVIDIA's bundled Python and its lib/python + pip-packages on PYTHONPATH.
Evidence is produced by `aetheris asm export-usd --evidence`.
"""
import argparse
import json
import time
from collections import Counter
from pathlib import Path
from pxr import Gf, Usd, UsdGeom, UsdPhysics, UsdShade


def matrix(values):
    return Gf.Matrix4d(*values)


def error(a, b):
    return max(abs(a[r][c] - b[r][c]) for r in range(4) for c in range(4))


def bounds(points):
    return [[min(p[c] for p in points) for c in range(3)],
            [max(p[c] for p in points) for c in range(3)]]


def validate(usd_file, evidence_file):
    evidence = json.loads(Path(evidence_file).read_text())
    start = time.perf_counter()
    stage = Usd.Stage.Open(str(usd_file))
    load_ms = (time.perf_counter() - start) * 1000
    assert stage, "Stage did not open"
    assert not stage.GetCompositionErrors(), stage.GetCompositionErrors()
    assert UsdGeom.GetStageMetersPerUnit(stage) == .001
    assert UsdGeom.GetStageUpAxis(stage) == "Z"
    assert stage.GetDefaultPrim().GetPath() == "/Assembly"
    occurrences = {p.GetAttribute("aetheris:occurrenceIdentity").Get(): p
                   for p in stage.Traverse() if p.HasAttribute("aetheris:occurrenceIdentity")}
    mesh = evidence["mesh"]
    assert set(occurrences) == {o["id"] for o in mesh["occurrences"]}
    definitions = {d["id"]: d for d in mesh["definitions"]}
    checks = [(Usd.TimeCode.Default(), mesh["occurrences"])]
    for sample in evidence["samplePoses"]:
        checks.append((Usd.TimeCode(sample["time"]), [dict(id=i["stableId"], transform=i["resolvedTransform"]["matrix"])
                                                    for i in sample["pose"]["instances"]]))
    transform_errors = []
    for frame, expected in checks:
        cache = UsdGeom.XformCache(frame)
        for o in expected:
            delta = error(cache.GetLocalToWorldTransform(occurrences[o["id"]]), matrix(o["transform"]))
            assert delta < 1e-9, (str(frame), o["id"], delta)
            transform_errors.append(delta)
    # Proper quaternion interpolation preserves rigid frames between authored poses.
    if len(checks) > 2:
        mid = (checks[1][0].GetValue() + checks[2][0].GetValue()) / 2
        cache = UsdGeom.XformCache(mid)
        for p in occurrences.values():
            m = cache.GetLocalToWorldTransform(p)
            assert abs(m.GetDeterminant() - 1) < 1e-9
    expected_points, usd_points, conservative_points = [], [], []
    cache = UsdGeom.XformCache(Usd.TimeCode.Default())
    prototypes = {}
    for o in mesh["occurrences"]:
        prim = occurrences[o["id"]]
        if o["parentId"] is not None:
            assert prim.GetParent() == occurrences[o["parentId"]]
        if o["definitionId"] is None:
            continue
        geometry = stage.GetPrimAtPath(str(prim.GetPath()) + "/Geometry")
        assert geometry.IsInstance()
        prototype = geometry.GetPrototype().GetPath()
        assert prototypes.setdefault(o["definitionId"], prototype) == prototype
        d = definitions[o["definitionId"]]
        usd_mesh = UsdGeom.Mesh(stage.GetPrimAtPath(str(geometry.GetPath()) + "/Mesh"))
        assert usd_mesh.GetSubdivisionSchemeAttr().Get() == "none"
        assert usd_mesh.GetOrientationAttr().Get() == "rightHanded"
        assert usd_mesh.GetNormalsInterpolation() == "vertex"
        assert list(usd_mesh.GetFaceVertexIndicesAttr().Get()) == d["indices"]
        assert all(n == 3 for n in usd_mesh.GetFaceVertexCountsAttr().Get())
        assert len(usd_mesh.GetNormalsAttr().Get()) == len(d["normals"]) // 3
        assert UsdShade.MaterialBindingAPI(usd_mesh).ComputeBoundMaterial()[0]
        world = matrix(o["transform"])
        points = [Gf.Vec3d(*d["positions"][i:i+3]) for i in range(0, len(d["positions"]), 3)]
        expected_points.extend(world.Transform(p) for p in points)
        local_bounds = bounds(points)
        conservative_points.extend(world.Transform(Gf.Vec3d(x,y,z))
                                   for x in [local_bounds[0][0],local_bounds[1][0]]
                                   for y in [local_bounds[0][1],local_bounds[1][1]]
                                   for z in [local_bounds[0][2],local_bounds[1][2]])
        usd_world = cache.GetLocalToWorldTransform(usd_mesh.GetPrim())
        usd_points.extend(usd_world.Transform(Gf.Vec3d(p)) for p in usd_mesh.GetPointsAttr().Get())
    aetheris_bounds, usd_bounds = bounds(expected_points), bounds(usd_points)
    bbox_error = max(abs(a-b) for aa, bb in zip(aetheris_bounds, usd_bounds) for a, b in zip(aa, bb))
    assert bbox_error < .0001, bbox_error
    # Independent USD bounds service, rather than only recomputing positions.
    bound = UsdGeom.BBoxCache(Usd.TimeCode.Default(), [UsdGeom.Tokens.default_]).ComputeWorldBound(stage.GetDefaultPrim()).ComputeAlignedRange()
    conservative_bounds = [list(bound.GetMin()), list(bound.GetMax())]
    aetheris_conservative_bounds = bounds(conservative_points)
    conservative_error = max(abs(a-b) for aa,bb in zip(aetheris_conservative_bounds,conservative_bounds) for a,b in zip(aa,bb))
    assert conservative_error < .0001, conservative_error
    joints = {p.GetAttribute("aetheris:interfaceName").Get(): p
              for p in stage.Traverse() if p.IsA(UsdPhysics.Joint)}
    assert len(joints) == len(evidence["joints"] or [])
    families = []
    for j in evidence["joints"] or []:
        p = joints[j["name"]]
        family = p.GetAttribute("aetheris:interfaceFamily").Get()
        assert j["family"] == family
        schema = {"Fixed": UsdPhysics.FixedJoint, "Revolute": UsdPhysics.RevoluteJoint, "Prismatic": UsdPhysics.PrismaticJoint}[family]
        assert p.IsA(schema)
        joint = UsdPhysics.Joint(p)
        assert joint.GetBody0Rel().GetTargets() == [occurrences[j["parentOccurrenceId"]].GetPath()]
        assert joint.GetBody1Rel().GetTargets() == [occurrences[j["childOccurrenceId"]].GetPath()]
        for n, field in enumerate(["parentLocalFrame", "childLocalFrame"]):
            expected = matrix(j[field]["matrix"])
            quat = p.GetAttribute(f"physics:localRot{n}").Get()
            rot = Gf.Matrix4d().SetRotate(Gf.Quatd(quat))
            rot.SetTranslateOnly(Gf.Vec3d(p.GetAttribute(f"physics:localPos{n}").Get()))
            assert error(rot, expected) < 1e-6
        if family != "Fixed":
            assert p.GetAttribute("physics:axis").Get() == "Z"
        families.append(family)
    topology = []
    for d in mesh["definitions"]:
        # Weld equal face-boundary vertices solely for mesh closure verification.
        points = [tuple(round(v, 7) for v in d["positions"][i:i+3]) for i in range(0, len(d["positions"]), 3)]
        edges = Counter()
        for i in range(0, len(d["indices"]), 3):
            a,b,c = [points[n] for n in d["indices"][i:i+3]]
            for u,v in [(a,b),(b,c),(c,a)]:
                edges[tuple(sorted((u,v)))] += 1
        bad = sum(count != 2 for count in edges.values())
        assert bad == 0, (d["identity"], "nonmanifold or open display edges", bad)
        topology.append(dict(definition=d["identity"], triangles=len(d["indices"])//3, unmatchedEdges=bad))
    return dict(tool="NVIDIA-distributed OpenUSD", version=".".join(map(str, Usd.GetVersion())), openedSuccessfully=True,
                compositionErrors=[], externalLoadMilliseconds=load_ms, occurrences=len(occurrences),
                sharedPrototypes=len(prototypes), jointFamilies=families, maxWorldTransformError= max(transform_errors),
                authoredPosesChecked=len(checks), bboxErrorMm=bbox_error, aetherisDisplayBoundsMm=aetheris_bounds,
                usdDisplayBoundsMm=usd_bounds, usdConservativeSceneBoundsMm=conservative_bounds,
                conservativeSceneBoundsErrorMm=conservative_error,
                metersPerUnit=.001, upAxis="Z", meshClosure=topology)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("usd")
    parser.add_argument("evidence")
    parser.add_argument("--out", default="artifacts/local/usd-x0/external-validation.json")
    args = parser.parse_args()
    report = validate(args.usd, args.evidence)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
