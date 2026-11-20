"""Sample external STEP inputs through the real Aetheris CLI and save replay evidence."""

import argparse
from collections import Counter, defaultdict, deque
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
from http.client import HTTPException
from itertools import islice
import json
from pathlib import Path
import re
import shutil
import subprocess
from tempfile import NamedTemporaryFile
import time
from urllib.parse import quote
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parents[1]
API = "https://api.step.parts/v1"
MIB = 1024 * 1024
REPEAT = (
    "din913_set_screw_m3x3", "adafruit_5128_macropad_rp2040_keyplate", "bearing_608zz",
    "plain_project_box_50x50x25", "profile_2020_b_slot6_a", "gt2_pulley_16t_bore3_w6",
    "shaft_coupler_4mm_to_3mm_m3_setscrew", "square_fan_grille_25mm",
)
STRESS = ("robstride_06", "arduino_nicla_sense_env", "raspberry_pi_4_model_b", "arduino_nicla_vision")
ASSET_URL = re.compile(r"https://media\.githubusercontent\.com/media/earthtojake/step\.parts/"
                       r"([a-f0-9]{40})/catalog/step/[a-zA-Z0-9_.%+-]+\.step")
PRODUCT_OCCURRENCE = re.compile(rb"#\d+\s*=\s*NEXT_ASSEMBLY_USAGE_OCCURRENCE\s*\(", re.I)


def remaining(deadline):
    seconds = deadline - time.monotonic()
    if seconds <= 0:
        raise TimeoutError("time budget exhausted")
    return seconds


def download(url, limit, deadline):
    chunks = bytearray()
    with urlopen(url, timeout=min(10, remaining(deadline))) as response:
        while len(chunks) <= limit:
            remaining(deadline)
            block = response.read1(min(256 * 1024, limit + 1 - len(chunks)))
            if not block:
                return chunks
            chunks.extend(block)
    raise ValueError(f"response exceeds {limit} bytes")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")


def rotating(index, seed):
    def rank(value):
        return sha256(f"{seed}\0{value}".encode())

    families = defaultdict(list)
    for part in index:
        if part["id"] not in REPEAT + STRESS:
            families[part["category"], part["family"]].append(part["id"])
    categories = defaultdict(deque)
    for (category, family), ids in sorted(families.items(), key=lambda item: rank(repr(item[0]))):
        categories[category].append(deque(sorted(ids, key=rank)))

    # Take one pick per category per pass and one per family before revisiting it.
    order = sorted(categories, key=rank)
    while order:
        for category in order:
            queue = categories[category]
            family = queue.popleft()
            yield family.popleft()
            if family:
                queue.append(family)
        order = [category for category in order if categories[category]]


def part_metadata(part_id, lane, deadline):
    try:
        payload = json.loads(download(f"{API}/parts/{quote(part_id, safe='')}", MIB, deadline))
        part = payload.get("part", payload)
        sample = {field: part[field] for field in ("id", "category", "family", "byteSize", "sha256", "stepUrl", "pageUrl")}
        if sample["id"] != part_id:
            raise ValueError("catalog returned a different part ID")
        validate_sample(sample)
        return {**sample, "lane": lane, "upstreamSource": part.get("stepSource")}
    except (OSError, HTTPException, ValueError, KeyError) as error:
        return {"id": part_id, "lane": lane, "error": str(error)}


def validate_sample(sample):
    asset = ASSET_URL.fullmatch(sample["stepUrl"])
    if not asset or not re.fullmatch(r"[a-f0-9]{64}", sample["sha256"]) or sample["byteSize"] < 1:
        raise ValueError(f"{sample['id']} has invalid STEP asset metadata")
    return asset[1]


def choose_samples(args, deadline):
    index = json.loads(download(f"{API}/catalog/parts.index.json", 32 * MIB, deadline))
    candidates = [(key, "repeat") for key in REPEAT] + [(key, "stress") for key in STRESS]
    candidates.extend((key, "rotate") for key in islice(rotating(index["items"], args.seed), args.rotate * 2))
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        metadata = list(pool.map(lambda candidate: part_metadata(*candidate, deadline), candidates))
    available = [sample for sample in metadata if "error" not in sample]
    if not available:
        raise ValueError("No sample metadata could be fetched.")
    revision = validate_sample(available[0])
    excluded = [sample for sample in metadata if "error" in sample]
    fixed = [sample for sample in available if sample["lane"] != "rotate"]
    size_groups = (deque(), deque(), deque())
    for sample in available:
        if sample["lane"] == "rotate":
            length = sample["byteSize"]
            size_groups[0 if length < 100_000 else 1 if length < MIB else 2].append(sample)
    ordered = list(fixed)
    while any(size_groups):
        ordered.extend(group.popleft() for group in size_groups if group)
    samples, seen = [], set()
    size, rotated = 0, 0
    for sample in ordered:
        if sample["lane"] == "rotate" and rotated >= args.rotate:
            continue
        reason = None
        if validate_sample(sample) != revision:
            reason = "catalog revision changed during selection"
        elif sample["sha256"] in seen:
            reason = "duplicate STEP bytes"
        elif sample["byteSize"] > args.file_mib * MIB or size + sample["byteSize"] > args.total_mib * MIB:
            reason = "byte budget"
        if reason:
            excluded.append({"id": sample["id"], "lane": sample["lane"], "reason": reason})
            continue
        samples.append(sample)
        size += sample["byteSize"]
        seen.add(sample["sha256"])
        rotated += sample["lane"] == "rotate"
    return {"seed": args.seed, "revision": revision, "catalog": index["catalog"],
            "samples": samples, "excluded": excluded}


def get_asset(sample, cache, deadline):
    digest = sample["sha256"]
    path = cache / (digest + ".step")
    if path.is_file() and path.stat().st_size == sample["byteSize"]:
        data = path.read_bytes()
        if sha256(data) == digest:
            return path, data, True
    data = download(sample["stepUrl"], sample["byteSize"], deadline)
    if len(data) != sample["byteSize"] or sha256(data) != digest:
        raise ValueError("STEP download did not match the pinned length and SHA-256")
    # Each run selects distinct hashes. Use an atomic publish for concurrent runs.
    with NamedTemporaryFile(dir=cache, delete=False) as temporary:
        temporary.write(data)
    try:
        Path(temporary.name).replace(path)
    finally:
        Path(temporary.name).unlink(missing_ok=True)
    return path, data, False


def invoke(cli, words, directory, label, deadline):
    start = time.monotonic()
    try:
        result = subprocess.run(["dotnet", str(cli), *map(str, words)], capture_output=True,
                                timeout=remaining(deadline))
        output, error, exit_code = result.stdout, result.stderr, result.returncode
    except subprocess.TimeoutExpired as expired:
        output, error, exit_code = expired.stdout or b"", expired.stderr or b"", None
    except TimeoutError:
        return {"exit": None, "seconds": 0}, {}, ""
    if output:
        (directory / (label + ".json")).write_bytes(output)
    if error:
        (directory / (label + ".stderr.txt")).write_bytes(error)
    try:
        payload = json.loads(output)
    except ValueError:
        payload = {}
    return {"exit": exit_code, "seconds": round(time.monotonic() - start, 3)}, payload, error.decode("utf-8", "replace")


def classification(attempt, payload, stderr, mode):
    if attempt["exit"] is None:
        return "timeout", [], None
    if attempt["exit"] != 0:
        if attempt["exit"] < 0 or attempt["exit"] >= 128 or "Unhandled exception." in stderr:
            return "crash", [stderr.strip().splitlines()[0] if stderr.strip() else f"exit {attempt['exit']}"], None
        diagnostics = [f"{d['code']} {d['source']}: {d['message']}"
                       for d in payload.get("diagnostics", []) if d.get("severity") == "Error"]
        if payload.get("errorKind") in ("import-failure", "assembly-like-step") or (
                mode == "assembly" and stderr.strip().startswith(("Importer.", "Entity:"))):
            return "refused", diagnostics or stderr.strip().splitlines(), None
        return "error", diagnostics or [payload.get("error") or stderr.strip() or "nonzero exit"], None
    if mode == "assembly":
        qualifications = [item.get("qualification") for item in payload.get("importQualifications", [])]
        facts = {key: payload.get(key) for key in ("definitionCount", "occurrenceCount", "bodyCount", "assemblyBoundingBox", "performance")}
    elif mode == "compound":
        qualifications = [item.get("importQualification") for item in payload.get("solids", [])]
        facts = {key: payload.get(key) for key in ("rootCount", "solidCount", "aggregateVolume")}
    else:
        summary = payload.get("summary", {})
        qualifications = [summary.get("importQualification")]
        facts = {key: summary.get(key) for key in ("bodyCount", "shellCount", "faceCount", "edgeCount",
                 "vertexCount", "boundingBox", "lengthUnit", "curveFamilies", "surfaceFamilies")}
    statuses = sorted({q.get("geometryStatus") or q.get("status", "unknown") for q in qualifications if q})
    repairs = Counter(r.get("code", "unspecified") for q in qualifications if q for r in q.get("repairs", []))
    facts.update(qualification=statuses, repairs=dict(repairs))
    if "Failed" in statuses:
        return "unqualified", [], facts
    if statuses == ["Qualified"] and all(qualifications):
        return ("qualified-with-repairs" if repairs else "qualified"), [], facts
    return "inspectable" if payload else "error", [], facts


def analyze(sample, args, output, cache, deadline):
    directory = output / sample["sha256"]
    directory.mkdir()
    row = {**sample, "attempts": []}
    try:
        path, data, cached = get_asset(sample, cache, deadline)
    except TimeoutError as error:
        return {**row, "status": "fetch-timeout", "diagnostics": [str(error)]}
    except (OSError, HTTPException, ValueError) as error:
        return {**row, "status": "fetch-error", "diagnostics": [str(error)]}
    row["cached"] = cached
    schema = re.search(rb"FILE_SCHEMA\s*\(\(\s*'([^']+)", data[:16384], re.I)
    row["schema"] = schema[1].decode("ascii", "replace") if schema else None
    # Product occurrence entities are a routing hint. The CLI decides actual admission.
    mode = "assembly" if PRODUCT_OCCURRENCE.search(data) else "part"
    case_deadline = min(deadline, time.monotonic() + args.seconds)
    while True:
        route = [] if mode == "part" else [mode]
        attempt, payload, stderr = invoke(args.cli, ["analyze", *route, path, "--json"], directory, mode, case_deadline)
        status, diagnostics, facts = classification(attempt, payload, stderr, mode)
        row["attempts"].append({"mode": mode, "status": status, "diagnostics": diagnostics, **attempt})
        if mode == "assembly" and status == "refused":
            # Preserve the hierarchy refusal and check if the exact geometry is accessible.
            mode = "part"
            continue
        if mode == "part" and payload.get("errorKind") == "assembly-like-step":
            mode = "compound"
            continue
        return {**row, "mode": mode, "status": status, "diagnostics": diagnostics, "facts": facts}


def round_trip(row, args, output, cache, deadline):
    directory = output / row["sha256"]
    source, original, exported = (directory / name for name in ("input.firmament", "original.step", "exported.step"))
    shutil.copyfile(cache / (row["sha256"] + ".step"), original)
    source.write_text('Model Imported { units: mm InlineStep part { path: "original.step" geometryOnly: true'
                      f' sha256: "{row["sha256"]}" }} }}\n', encoding="utf-8")
    case_deadline = min(deadline, time.monotonic() + args.seconds)
    build, _, _ = invoke(args.cli, ["build", source, "--output", exported, "--json"], directory, "export", case_deadline)
    if build["exit"] != 0:
        return {"export": build}
    compare, payload, _ = invoke(args.cli, ["analyze", "compare", original, exported, "--json"], directory, "compare", case_deadline)
    return {"export": build, "compare": compare, "comparison": {key: payload.get(key) for key in (
        "bboxComparison", "topologyComparison", "surfaceFamilyComparison", "volumeComparison")}}


def group_issues(rows):
    groups = defaultdict(list)
    for row in rows:
        evidence = row["diagnostics"][0] if row.get("diagnostics") else row.get("mode", row["lane"])
        if not row["status"].startswith("qualified"):
            groups[f"{row['status']}: {diagnostic_signature(evidence)}"].append(row["id"])
        for attempt in row["attempts"][:-1]:
            if attempt["mode"] == "assembly" and attempt["status"] == "refused":
                evidence = attempt["diagnostics"][0] if attempt["diagnostics"] else "no diagnostic"
                groups[f"assembly refused: {diagnostic_signature(evidence)}"].append(row["id"])
    return dict(groups)


def diagnostic_signature(message):
    return re.sub(r"#\d+|Entity:\d+|(?<==)\d+(?=[:,])", "<entity>", message.split(": faceId=", 1)[0])


def run(args):
    started = time.monotonic()
    deadline = started + args.budget
    manifest = json.loads(args.replay.read_text(encoding="utf-8")) if args.replay else choose_samples(args, deadline)
    hashes, size = set(), 0
    for sample in manifest["samples"]:
        revision = validate_sample(sample)
        if revision != manifest["revision"] or sample["sha256"] in hashes or sample["byteSize"] > args.file_mib * MIB:
            raise ValueError("Manifest has a mismatched revision, repeated asset, or over-budget file.")
        hashes.add(sample["sha256"])
        size += sample["byteSize"]
    if not hashes or size > args.total_mib * MIB:
        raise ValueError("Manifest is empty or exceeds the total byte budget.")
    args.output.mkdir(parents=True, exist_ok=False)
    args.cache.mkdir(parents=True, exist_ok=True)
    write_json(args.output / "manifest.json", manifest)
    rows = []
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        for row in pool.map(lambda sample: analyze(sample, args, args.output, args.cache, deadline), manifest["samples"]):
            rows.append(row)
            print(f"{len(rows)}/{len(hashes)} {row['id']}: {row['status']}", flush=True)
    selected = [row for row in rows if row.get("mode") == "part"
                and row["status"].startswith("qualified") and row["byteSize"] <= MIB]
    # Spread the default comparisons across the repeat and rotating lanes.
    first_by_lane = [row for lane in ("repeat", "rotate", "stress")
                     if (row := next((item for item in selected if item["lane"] == lane), None))]
    comparisons = first_by_lane + [r for r in selected if r not in first_by_lane]
    for row in comparisons[:args.round_trips]:
        if time.monotonic() < deadline:
            row["roundTrip"] = round_trip(row, args, args.output, args.cache, deadline)
    counts = dict(Counter(row["status"] for row in rows))
    kernel = sorted(args.cli.parent.glob("Aetheris.Kernel*.dll"))
    write_json(args.output / "report.json", {"cli": str(args.cli),
               "binarySha256": {path.name: sha256(path.read_bytes()) for path in [args.cli, *kernel]},
               "revision": manifest["revision"], "bytes": size, "seconds": round(time.monotonic() - started, 3),
               "counts": counts, "issues": group_issues(rows), "samples": rows})
    print(f"{len(rows)} STEP files, {size / MIB:.1f} MiB: {counts}")
    print(args.output / "report.json")
    if any(counts.get(kind) for kind in ("fetch-error", "fetch-timeout")):
        return 2
    return 1 if counts.get("crash") or counts.get("error") else 0


def main():
    now = datetime.now(timezone.utc)
    week = now.isocalendar()
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", type=Path, default=ROOT / "Aetheris.CLI/bin/Release/net10.0/aetheris.dll")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/local/step-parts" / now.strftime("%Y%m%dT%H%M%S%fZ"))
    parser.add_argument("--cache", type=Path, default=ROOT / "artifacts/local/step-parts/cache")
    parser.add_argument("--replay", type=Path, help="Use a saved manifest instead of selecting from the current catalog.")
    parser.add_argument("--seed", default=f"{week.year}-W{week.week:02d}")
    parser.add_argument("--rotate", type=int, default=20)
    parser.add_argument("--round-trips", type=int, default=2)
    parser.add_argument("--workers", type=int, default=2)
    parser.add_argument("--seconds", type=float, default=25)
    parser.add_argument("--budget", type=float, default=300)
    parser.add_argument("--file-mib", type=int, default=64)
    parser.add_argument("--total-mib", type=int, default=96)
    args = parser.parse_args()
    if min(args.workers, args.seconds, args.budget, args.file_mib, args.total_mib) <= 0 or min(args.rotate, args.round_trips) < 0:
        parser.error("Budgets and workers must be positive, and counts must be nonnegative.")
    args.cli = args.cli.resolve()
    if not args.cli.is_file():
        parser.error("Build Aetheris.CLI in Release or pass --cli with a built aetheris.dll.")
    try:
        return run(args)
    except (OSError, HTTPException, ValueError, KeyError) as error:
        parser.exit(2, f"STEP corpus smoke: {error}\n")


if __name__ == "__main__":
    raise SystemExit(main())
