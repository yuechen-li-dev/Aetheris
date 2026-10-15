"""Summarize measured corpus outcomes, never grant compiler admission.

Production JSON stays in ignored evidence. The compact release ledger retains
every source hash, syntax/owner outcome, diagnostic code and reviewed disposition.
Dependency contexts come from actual successful Assembly source receipts, not
guessed include paths. A receipt proves that consumer, not all specializations.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path


def walk(value):
    yield value
    children = value.values() if isinstance(value, dict) else value if isinstance(value, list) else []
    for child in children:
        yield from walk(child)


def payload(entry):
    try:
        return json.loads(entry["productionOutput"])
    except ValueError:
        return {}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--fixtures", type=Path, default=Path("fixtures"))
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--downstream-evidence", type=Path)
    args = parser.parse_args()
    measured = json.loads(args.report.read_text(encoding="utf-8-sig"))
    entries = measured["entries"]
    files = sorted(p for p in args.fixtures.rglob("*") if p.is_file() and p.suffix.lower() in (".firmament", ".firmfixture", ".firmasm"))
    current = {p.relative_to(args.fixtures).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest().upper() for p in files}
    if {e["source"]: e["sha256"] for e in entries} != current:
        raise SystemExit("Corpus inventory/content differs from the measured report; rerun collection.")
    contexts = defaultdict(list)
    for entry in entries:
        if entry["productionExit"] != 0:
            continue
        for node in walk(payload(entry)):
            if not isinstance(node, dict) or "sourceDependencies" not in node:
                continue
            for dependency in node["sourceDependencies"] or []:
                if dependency.get("isRoot"):
                    continue
                key = (str(Path(dependency["path"]).resolve()).casefold(), dependency["sha256"].upper())
                contexts[key].append({"consumer": entry["source"], "owner": entry["owner"], "level": entry["productionLevel"]})
    downstream = json.loads(args.downstream_evidence.read_text(encoding="utf-8-sig")) if args.downstream_evidence else {}
    rows = []
    for entry in entries:
        source = entry["source"]
        diagnostics = [n for n in walk(payload(entry)) if isinstance(n, dict) and "code" in n and "message" in n]
        codes = sorted({n["code"] for n in diagnostics if isinstance(n["code"], str)})
        bare_messages = [d for n in walk(payload(entry)) if isinstance(n, dict) and isinstance(n.get("diagnostics"), list) for d in n["diagnostics"] if isinstance(d, str)]
        primary = next((n["message"] for n in diagnostics if isinstance(n["message"], str)), bare_messages[0] if bare_messages else entry["productionErrors"].strip())
        dependency_contexts = contexts.get((str((args.fixtures / source).resolve()).casefold(), entry["sha256"]), [])
        dependency_contexts = list({(c["consumer"], c["owner"], c["level"]): c for c in dependency_contexts}.values())
        classification, reason = entry["classification"], entry["reason"]
        negative = any(p.casefold() == "invalid" for p in Path(source).parts) or Path(source).stem.casefold().endswith(".invalid")
        review = downstream.get(source)
        if negative and entry["productionExit"] == 0:
            if not review or review.get("sourceSha256") != entry["sha256"] or review.get("witnessPassed") is not True:
                classification, reason = "UnresolvedNegativeAcceptance", "A negative fixture was accepted without matching reviewed downstream evidence."
            else:
                classification, reason = review["classification"], review["reason"]
        elif classification == "LegacyGrammarBoundary":
            classification = "LegacySerializationAdmitted" if entry["productionExit"] == 0 else "LegacySerializationRejected"
            reason = "Explicit serialization-shaped compatibility input uses its production owner; V2 structural failure is recorded, not counted as a V2 regression."
        elif classification == "NegativeFixtureRejected":
            classification = "ExpectedNegativeRejection"
        elif classification == "NegativeFixtureAccepted":
            classification = "UnresolvedNegativeAcceptance"
        elif entry["productionExit"] != 0 and entry["structuralSuccess"] and dependency_contexts:
            classification = "AdmittedInConsumerContext"
            reason = "Successful production dependency receipts match this exact source hash. The standalone outcome is retained; no claim about every possible Template specialization."
        elif classification == "DeclarationModule":
            classification = "DeclarationModuleRequiresConsumer"
        elif classification == "BothRejected":
            classification = "MalformedOrUnsupportedSourceRejected"
            reason = "Both structural and production admission reject this source; first syntax diagnostic: " + entry["syntaxDiagnostics"][0]["message"]
        elif classification == "SemanticOrCapabilityRejection":
            classification = "SourceAdmissionRejected"
            reason = "Structurally represented source is rejected by its production owner: " + primary
        elif classification == "ExperimentalSource":
            classification = "ExperimentalAdmitted" if entry["productionExit"] == 0 else "ExperimentalRejected"
        if classification in ("ExpectedNegativeRejection", "LegacySerializationRejected", "DeclarationModuleRequiresConsumer", "ExperimentalRejected") and primary:
            reason += " Owner diagnostic: " + primary
        rows.append({"source": source, "sha256": entry["sha256"], "structuralSuccess": entry["structuralSuccess"],
                     "syntaxFingerprint": entry["syntaxFingerprint"], "syntaxDiagnosticCodes": sorted({d["code"] for d in entry["syntaxDiagnostics"]}),
                     "owner": entry["owner"], "productionLevel": entry["productionLevel"], "standaloneProductionExit": entry["productionExit"],
                     "productionDiagnosticCodes": codes, "productionDiagnosticMessages": bare_messages,
                     "sourceIntent": "Negative" if negative else "Experimental" if source.startswith(("Speculative/", "Experiments/")) else "Unspecified",
                     "classification": classification, "reason": reason,
                     "consumerContexts": dependency_contexts, "downstreamEvidence": downstream.get(source)})
    canonical = [r for r in rows if r["source"].startswith("Canonical/")]
    unresolved = [r for r in rows if r["classification"] in ("StructuralGap", "OwnerException", "UnresolvedNegativeAcceptance")]
    manifest = json.dumps(sorted(current.items()), separators=(",", ":")).encode()
    report = {"schemaVersion": 1, "sourceCount": len(rows), "canonicalSourceCount": len(canonical),
              "canonicalStructurallyParsed": sum(r["structuralSuccess"] for r in canonical),
              "structurallyParsed": sum(r["structuralSuccess"] for r in rows), "standaloneProductionAccepted": sum(r["standaloneProductionExit"] == 0 for r in rows),
              "inventorySha256": hashlib.sha256(manifest).hexdigest().upper(), "unresolvedMismatchCount": len(unresolved),
              "universalSemanticParityQualified": False,
              "classifications": dict(sorted(Counter(r["classification"] for r in rows).items())),
              "canonicalClassifications": dict(sorted(Counter(r["classification"] for r in canonical).items())), "entries": rows}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "entries"}, indent=2))
    if unresolved:
        raise SystemExit("Unresolved mismatches remain; see ledger.")


if __name__ == "__main__":
    main()
