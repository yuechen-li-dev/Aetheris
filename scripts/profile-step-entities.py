"""Count STEP entities and serialized bytes; audit exact immutable definitions.

This diagnostic reads canonical one-entity-per-line exporter output. It never
rewrites STEP or proposes merging topology. Output defaults to artifacts/local.
"""
import argparse
import collections
import hashlib
import json
import pathlib
import re

parser = argparse.ArgumentParser()
parser.add_argument("input", type=pathlib.Path)
parser.add_argument("--output", type=pathlib.Path, default=pathlib.Path("artifacts/local/step-entity-profile.json"))
args = parser.parse_args()
raw = args.input.read_bytes()
entities = {}
histogram = collections.defaultdict(lambda: {"count": 0, "bytes": 0})
for line in raw.splitlines(keepends=True):
    text = line.decode("utf-8").strip()
    match = re.fullmatch(r"#(\d+)=(.*);", text)
    if not match:
        continue
    body = match[2]
    names = re.findall(r"\b([A-Z][A-Z0-9_]*)\(", re.sub(r"'(?:(?:'')|[^'])*'", "", body))
    kind = names[0] if not body.startswith("(") else "+".join(names)
    entities[int(match[1])] = (kind, body, len(line))
    histogram[kind]["count"] += 1
    histogram[kind]["bytes"] += len(line)

# STEP value geometry can share a definition; topological identity is separate.
# Names and every argument participate. No tolerance-based equivalence is used.
immutable = {"CARTESIAN_POINT", "DIRECTION", "VECTOR", "AXIS2_PLACEMENT_3D", "AXIS2_PLACEMENT_2D",
             "LINE", "CIRCLE", "ELLIPSE", "PLANE", "CYLINDRICAL_SURFACE", "CONICAL_SURFACE",
             "SPHERICAL_SURFACE", "TOROIDAL_SURFACE", "B_SPLINE_CURVE_WITH_KNOTS", "B_SPLINE_SURFACE_WITH_KNOTS"}
token = re.compile(r"'(?:(?:'')|[^'])*'|#(\d+)")
signatures = {}
active = set()
def signature(identity):
    if identity in signatures:
        return signatures[identity]
    if identity in active:
        return "cycle:" + str(identity)
    active.add(identity)
    body = entities[identity][1]
    normalized = token.sub(lambda m: "@" + signature(int(m[1])) if m[1] and int(m[1]) in entities else m[0], body)
    value = hashlib.sha256(normalized.encode("utf-8")).hexdigest()
    active.remove(identity)
    signatures[identity] = value
    return value

groups = collections.defaultdict(list)
for identity, (kind, body, size) in entities.items():
    if kind in immutable:
        groups[kind, signature(identity)].append(identity)
duplicates = collections.defaultdict(lambda: {"duplicateDefinitions": 0, "duplicateLineBytes": 0})
for (kind, key), ids in groups.items():
    duplicates[kind]["duplicateDefinitions"] += len(ids) - 1
    duplicates[kind]["duplicateLineBytes"] += sum(entities[i][2] for i in ids[1:])

required = ["ADVANCED_FACE", "EDGE_CURVE", "ORIENTED_EDGE", "EDGE_LOOP", "FACE_BOUND", "FACE_OUTER_BOUND",
            "PCURVE", "SURFACE_CURVE", "CARTESIAN_POINT", "DIRECTION", "VECTOR", "AXIS2_PLACEMENT_3D",
            "LINE", "CIRCLE", "B_SPLINE_CURVE_WITH_KNOTS", "B_SPLINE_SURFACE_WITH_KNOTS", "PRODUCT",
            "SHAPE_REPRESENTATION", "REPRESENTATION_CONTEXT"]
for kind in required:
    histogram[kind]
number = re.compile(r"(?<![#\w.])[-+]?(?:\d+\.\d*|\d*\.\d+|\d+)(?:[Ee][-+]?\d+)?")
lengths = collections.Counter()
numeric_bytes = 0
for kind, body, size in entities.values():
    without_strings = re.sub(r"'(?:(?:'')|[^'])*'", "", body)
    for match in number.finditer(without_strings):
        lengths[len(match[0])] += 1
        numeric_bytes += len(match[0])
report = {
    "input": str(args.input.resolve()), "sha256": hashlib.sha256(raw).hexdigest().upper(),
    "bytes": len(raw), "lines": len(raw.splitlines()), "entities": len(entities),
    "entityBytes": sum(size for kind, body, size in entities.values()),
    "histogram": dict(sorted(histogram.items(), key=lambda item: (-item[1]["bytes"], item[0]))),
    "exactImmutableDuplication": dict(sorted(duplicates.items(), key=lambda item: (-item[1]["duplicateLineBytes"], item[0]))),
    "numericLiteralBytes": numeric_bytes, "numericLiteralLengthHistogram": dict(sorted(lengths.items())),
    "duplicateBytesAreEstimate": "Duplicate line bytes exclude reference renumbering effects; topology and pcurve associations are not counted as internable.",
}
# Identity-independent graph digest: immutable value geometry counts once, while every
# topological/association/metadata occurrence retains its multiplicity. Includes names,
# numeric strings and recursively resolved references, not just topology totals.
semantic = collections.Counter((kind, signature(identity)) for identity, (kind, _, _) in entities.items())
normalized = [(kind, key, 1 if kind in immutable else count) for (kind, key), count in sorted(semantic.items())]
report["normalizedArtifactSha256"] = hashlib.sha256(json.dumps(normalized, separators=(",", ":")).encode()).hexdigest().upper()
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({key: report[key] for key in ("bytes", "lines", "entities", "histogram", "exactImmutableDuplication")}, indent=2))
