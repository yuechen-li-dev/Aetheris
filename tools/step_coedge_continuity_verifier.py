#!/usr/bin/env python3
"""Independent STEP coedge continuity verifier.

Purpose-built forensic script for checking raw STEP topology loop continuity:
ADVANCED_FACE -> (FACE_OUTER_BOUND|FACE_BOUND) -> EDGE_LOOP -> ORIENTED_EDGE
-> EDGE_CURVE -> VERTEX_POINT -> CARTESIAN_POINT.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

ENTITY_RE = re.compile(r"^#(\d+)\s*=\s*([A-Z0-9_]+)\s*\((.*)\)\s*$", re.DOTALL)
REF_RE = re.compile(r"#(\d+)")


@dataclass
class Entity:
    eid: int
    name: str
    args_raw: str


def split_top_level_args(s: str) -> List[str]:
    parts: List[str] = []
    buf: List[str] = []
    depth = 0
    in_string = False
    i = 0
    while i < len(s):
        ch = s[i]
        if ch == "'":
            # STEP escapes apostrophes as doubled quotes.
            if in_string and i + 1 < len(s) and s[i + 1] == "'":
                buf.append("''")
                i += 2
                continue
            in_string = not in_string
            buf.append(ch)
        elif not in_string and ch == '(':
            depth += 1
            buf.append(ch)
        elif not in_string and ch == ')':
            depth -= 1
            buf.append(ch)
        elif not in_string and depth == 0 and ch == ',':
            parts.append(''.join(buf).strip())
            buf = []
        else:
            buf.append(ch)
        i += 1
    if buf:
        parts.append(''.join(buf).strip())
    return parts


def split_part21_entities(text: str) -> Iterable[str]:
    data_start = text.find("DATA;")
    data_end = text.rfind("ENDSEC;")
    if data_start == -1 or data_end == -1 or data_end <= data_start:
        raise ValueError("Could not locate DATA/ENDSEC section")

    body = text[data_start + len("DATA;"):data_end]
    stmt: List[str] = []
    in_string = False
    for ch in body:
        if ch == "'":
            if in_string and stmt and ''.join(stmt).endswith("'"):
                # keep state; escaped quote handled naturally by no toggle.
                pass
            in_string = not in_string
            stmt.append(ch)
            continue
        if ch == ';' and not in_string:
            payload = ''.join(stmt).strip()
            if payload:
                yield payload
            stmt = []
        else:
            stmt.append(ch)


def parse_entities(step_path: Path) -> Dict[int, Entity]:
    text = step_path.read_text(encoding="utf-8", errors="ignore")
    entities: Dict[int, Entity] = {}
    for raw in split_part21_entities(text):
        m = ENTITY_RE.match(raw)
        if not m:
            continue
        eid = int(m.group(1))
        name = m.group(2)
        args_raw = m.group(3).strip()
        entities[eid] = Entity(eid=eid, name=name, args_raw=args_raw)
    return entities


def parse_point_xyz(args_raw: str) -> Optional[Tuple[float, float, float]]:
    args = split_top_level_args(args_raw)
    if len(args) < 2:
        return None
    coords = args[1].strip()
    if not (coords.startswith('(') and coords.endswith(')')):
        return None
    xyz = split_top_level_args(coords[1:-1])
    if len(xyz) != 3:
        return None
    try:
        return (float(xyz[0]), float(xyz[1]), float(xyz[2]))
    except ValueError:
        return None


def parse_single_ref(token: str) -> Optional[int]:
    m = REF_RE.fullmatch(token.strip())
    return int(m.group(1)) if m else None


def parse_bool(token: str) -> Optional[bool]:
    t = token.strip()
    if t == ".T.":
        return True
    if t == ".F.":
        return False
    return None


def parse_ref_list(token: str) -> List[int]:
    token = token.strip()
    if not (token.startswith('(') and token.endswith(')')):
        return []
    out: List[int] = []
    for part in split_top_level_args(token[1:-1]):
        ref = parse_single_ref(part)
        if ref is not None:
            out.append(ref)
    return out


def vdist(a: Tuple[float, float, float], b: Tuple[float, float, float]) -> float:
    return math.dist(a, b)


def classify_gap(g: float) -> str:
    if g <= 1e-12:
        return "exact/negligible"
    if g <= 1e-7:
        return "tiny-gap"
    if g <= 1e-3:
        return "suspicious"
    return "disconnected"


def verify_file(
    step_path: Path,
    face_filter: Optional[set[int]] = None,
    top_n: Optional[int] = None,
) -> Dict:
    ents = parse_entities(step_path)

    # Pass 2: build topology tables that do not depend on ORIENTED_EDGE direction.
    points: Dict[int, Tuple[float, float, float]] = {}
    vertex_to_point: Dict[int, int] = {}
    edge_curve_vertices: Dict[int, Tuple[int, int]] = {}
    edge_loops: Dict[int, List[int]] = {}
    bounds: Dict[int, Tuple[str, int]] = {}
    advanced_faces: Dict[int, List[int]] = {}

    for eid, e in ents.items():
        if e.name == "CARTESIAN_POINT":
            xyz = parse_point_xyz(e.args_raw)
            if xyz is not None:
                points[eid] = xyz

    for eid, e in ents.items():
        args = split_top_level_args(e.args_raw)
        if e.name == "VERTEX_POINT" and len(args) >= 2:
            p = parse_single_ref(args[1])
            if p is not None:
                vertex_to_point[eid] = p
        elif e.name == "EDGE_CURVE" and len(args) >= 4:
            v1 = parse_single_ref(args[1])
            v2 = parse_single_ref(args[2])
            if v1 is not None and v2 is not None:
                edge_curve_vertices[eid] = (v1, v2)
        elif e.name == "EDGE_LOOP" and len(args) >= 2:
            edge_loops[eid] = parse_ref_list(args[1])
        elif e.name in ("FACE_OUTER_BOUND", "FACE_BOUND") and len(args) >= 2:
            loop_id = parse_single_ref(args[1])
            if loop_id is not None:
                bounds[eid] = (e.name, loop_id)
        elif e.name == "ADVANCED_FACE" and len(args) >= 2:
            advanced_faces[eid] = parse_ref_list(args[1])

    # Pass 3: resolve ORIENTED_EDGE from fully-built EDGE_CURVE table (order-independent).
    oriented_edge_effective: Dict[int, Tuple[int, int]] = {}
    for eid, e in ents.items():
        if e.name != "ORIENTED_EDGE":
            continue
        args = split_top_level_args(e.args_raw)
        if len(args) < 5:
            continue
        edge_elem = parse_single_ref(args[3])
        orient = parse_bool(args[4])
        if edge_elem is None or orient is None:
            continue
        ec = edge_curve_vertices.get(edge_elem)
        if ec is None:
            continue
        oriented_edge_effective[eid] = ec if orient else (ec[1], ec[0])

    # Build face -> bounds via ADVANCED_FACE args[1]
    loop_reports = []

    for face_id, bound_ids in advanced_faces.items():
        if face_filter and face_id not in face_filter:
            continue

        for bound_id in bound_ids:
            bound = bounds.get(bound_id)
            if bound is None:
                continue
            bound_type, loop_id = bound
            oe_ids = edge_loops.get(loop_id, [])
            joins = []
            max_gap = 0.0
            verdict = "exact/negligible"

            if oe_ids:
                for i in range(len(oe_ids)):
                    cur_oe = oe_ids[i]
                    nxt_oe = oe_ids[(i + 1) % len(oe_ids)]
                    cur = oriented_edge_effective.get(cur_oe)
                    nxt = oriented_edge_effective.get(nxt_oe)
                    gap = None
                    classification = "missing-topology"
                    v_end = None
                    v_next = None
                    if cur and nxt:
                        v_end = cur[1]
                        v_next = nxt[0]
                        p_end = points.get(vertex_to_point.get(v_end, -1))
                        p_next = points.get(vertex_to_point.get(v_next, -1))
                        if p_end is not None and p_next is not None:
                            gap = vdist(p_end, p_next)
                            classification = classify_gap(gap)
                            max_gap = max(max_gap, gap)
                    if classification in ("tiny-gap", "suspicious", "disconnected", "missing-topology"):
                        verdict = classification if classification != "tiny-gap" else verdict
                        if classification == "suspicious" and verdict != "disconnected":
                            verdict = "suspicious"
                        if classification == "disconnected":
                            verdict = "disconnected"
                        if classification == "missing-topology" and verdict == "exact/negligible":
                            verdict = "missing-topology"

                    joins.append(
                        {
                            "from_oriented_edge": cur_oe,
                            "to_oriented_edge": nxt_oe,
                            "from_effective_end_vertex": v_end,
                            "to_effective_start_vertex": v_next,
                            "from_effective_end_point": p_end if cur and nxt else None,
                            "to_effective_start_point": p_next if cur and nxt else None,
                            "gap": gap,
                            "classification": classification,
                        }
                    )

            loop_reports.append(
                {
                    "file": str(step_path),
                    "face_id": face_id,
                    "bound_id": bound_id,
                    "bound_type": bound_type,
                    "loop_id": loop_id,
                    "oriented_edges": oe_ids,
                    "max_gap": max_gap,
                    "verdict": verdict,
                    "joins": joins,
                }
            )

    loop_reports.sort(key=lambda x: x["max_gap"], reverse=True)
    if top_n is not None:
        loop_reports = loop_reports[:top_n]

    return {"file": str(step_path), "loop_reports": loop_reports}


def fmt_gap(g: Optional[float]) -> str:
    return "NA" if g is None else f"{g:.9f}"


def fmt_point(p: Optional[Tuple[float, float, float]]) -> str:
    if p is None:
        return "NA"
    return f"({p[0]:.9f}, {p[1]:.9f}, {p[2]:.9f})"


def print_report(result: Dict) -> None:
    print(f"FILE: {result['file']}")
    if not result["loop_reports"]:
        print("  No loops found for requested selection.")
        return
    for lr in result["loop_reports"]:
        print(f"FACE: #{lr['face_id']}")
        print(f"BOUND: {lr['bound_type']} #{lr['bound_id']}")
        print(f"LOOP: #{lr['loop_id']}")
        print("ORIENTED_EDGES:", " ".join(f"#{x}" for x in lr["oriented_edges"]))
        for j in lr["joins"]:
            print(
                "JOIN: "
                f"OE#{j['from_oriented_edge']}.end(v#{j['from_effective_end_vertex']}) -> "
                f"OE#{j['to_oriented_edge']}.start(v#{j['to_effective_start_vertex']}) "
                f"end_pt = {fmt_point(j['from_effective_end_point'])} "
                f"next_start_pt = {fmt_point(j['to_effective_start_point'])} "
                f"gap = {fmt_gap(j['gap'])} "
                f"[{j['classification']}]"
            )
        print(f"MAX_GAP: {lr['max_gap']:.9f}")
        print(f"VERDICT: {lr['verdict']}")
        print()


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("files", nargs="+", type=Path, help="STEP files to inspect")
    ap.add_argument("--face", action="append", type=int, default=[], help="ADVANCED_FACE id filter (repeatable)")
    ap.add_argument("--top", type=int, default=None, help="Only show top N loops by max gap")
    ap.add_argument("--json", type=Path, default=None, help="Optional JSON output path")
    args = ap.parse_args()

    face_filter = set(args.face) if args.face else None
    all_results = []
    for f in args.files:
        result = verify_file(f, face_filter=face_filter, top_n=args.top)
        print_report(result)
        all_results.append(result)

    if args.json:
        args.json.write_text(json.dumps(all_results, indent=2), encoding="utf-8")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
