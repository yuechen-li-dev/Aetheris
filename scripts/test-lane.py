#!/usr/bin/env python3
"""Aetheris test lanes.

  affected  the test projects (and Firmament suites) a change can reach, without Slow tests (the inner loop)
  fast      every test project without Slow tests
  full      every test, the gate before a commit (same as `dotnet test Aetheris.slnx -c Release --no-build`)

The change is the working tree (staged, unstaged and untracked) plus commits since --base (default: the merge
base with origin/master). A changed file selects its own project and every test project that references it,
transitively. Inside Aetheris.Kernel.Firmament.Tests, a change under a Firmament source folder listed in
scripts/test-suites.json selects only those suites; any other Firmament change selects all of them.

Examples:
  python3 scripts/test-lane.py affected --dry-run
  python3 scripts/test-lane.py affected
  python3 scripts/test-lane.py fast --project Aetheris.Kernel.Firmament.Tests --suite Drawing
  python3 scripts/test-lane.py full
"""
import argparse
import concurrent.futures
import json
import os
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FAST_FILTER = "Category!=Slow&Category!=SlowCorpus"


def projects():
    """name -> (csproj path relative to ROOT, referenced project names), for every project in Aetheris.slnx."""
    text = (ROOT / "Aetheris.slnx").read_text(encoding="utf-8")
    found = {}
    for path in re.findall(r'Path="([^"]+\.csproj)"', text):
        path = path.replace("\\", "/")
        csproj = ROOT / path
        if not csproj.exists():
            continue
        refs = []
        for node in ET.parse(csproj).getroot().iter():
            if node.tag.endswith("ProjectReference") and node.get("Include"):
                refs.append(Path(node.get("Include").replace("\\", "/")).stem)
        found[csproj.stem] = (path, refs)
    return found


def is_test_project(name):
    return name.endswith(".Tests")


def dependents(graph):
    reverse = {name: set() for name in graph}
    for name, (_, refs) in graph.items():
        for ref in refs:
            reverse.setdefault(ref, set()).add(name)
    return reverse


def changed_files(base):
    def git(*args):
        return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    if base is None:
        base = git("merge-base", "HEAD", "origin/master").strip()
    files = set(git("diff", "--name-only", base).split())
    files |= set(git("diff", "--name-only", "--cached").split())
    files |= set(git("ls-files", "--others", "--exclude-standard").split())
    return sorted(files), base


def owner(path, graph_dirs):
    best = None
    for name, directory in graph_dirs.items():
        if path.startswith(directory + "/") and (best is None or len(directory) > len(graph_dirs[best])):
            best = name
    return best


def select(files, graph, config):
    """(test projects, Firmament suites or None for all, reasons)."""
    graph_dirs = {name: str(Path(path).parent).replace("\\", "/") for name, (path, _) in graph.items()}
    reverse = dependents(graph)
    tests = {name for name in graph if is_test_project(name)}
    selected, suites, reasons = set(), set(), []
    all_suites = False
    firmament_tests = config["firmamentTestProject"]
    for path in files:
        if any(path.startswith(prefix) for prefix in config["ignoredPrefixes"]):
            continue
        listed = next((projects_ for prefix, projects_ in config["pathProjects"].items() if path.startswith(prefix)), None)
        if listed is not None:
            selected |= set(listed) & tests
            if firmament_tests in listed:
                suites.add("Language")
            if listed:
                reasons.append(f"{path} -> {', '.join(listed)}")
            continue
        project = owner(path, graph_dirs)
        if project is None:
            reasons.append(f"{path} -> every test project (no owning project)")
            return tests, None, reasons
        reached, frontier = {project}, [project]
        while frontier:
            for parent in reverse.get(frontier.pop(), ()):
                if parent not in reached:
                    reached.add(parent)
                    frontier.append(parent)
        hit = reached & tests
        selected |= hit
        if firmament_tests in hit:
            if project == firmament_tests:
                suites |= suites_of_test_file(path)
            elif project == "Aetheris.Kernel.Firmament":
                mapped = next((s for prefix, s in config["firmamentSourceSuites"].items() if path.startswith(prefix)), None)
                if mapped is None:
                    all_suites = True
                else:
                    suites |= set(mapped)
            else:
                all_suites = True
        reasons.append(f"{path} ({project}) -> {len(hit)} test project(s)")
    return selected, (None if all_suites or not suites else sorted(suites)), reasons


def suites_of_test_file(path):
    source = ROOT / path
    if not source.exists() or source.suffix != ".cs":
        return {"Language"}
    found = set(re.findall(r'\[Trait\("Suite", "(\w+)"\)\]', source.read_text(encoding="utf-8")))
    return found or {"Language"}


def run(graph, names, test_filter, config, suites, jobs):
    started = time.time()

    def one(name):
        path = graph[name][0]
        filter_ = test_filter
        if name == config["firmamentTestProject"] and suites:
            suite_filter = "|".join(f"Suite={s}" for s in suites)
            filter_ = f"({filter_})&({suite_filter})" if filter_ else suite_filter
        args = ["dotnet", "test", path, "-c", "Release", "--no-build", "--nologo", "--verbosity", "quiet"]
        if filter_:
            args += ["--filter", filter_]
        t = time.time()
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True)
        summary = [line.strip() for line in result.stdout.splitlines() if re.search(r"(Passed|Failed)!|No test matches", line)]
        return name, result.returncode, time.time() - t, summary, result.stdout + result.stderr

    failures = []
    # The largest projects first, so the critical path starts at once.
    order = sorted(names, key=lambda n: (n != config["firmamentTestProject"], n != "Aetheris.CLI.Tests", n != "Aetheris.Kernel.Core.Tests", n))
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        for name, code, seconds, summary, output in pool.map(one, order):
            status = "ok" if code == 0 else "FAILED"
            print(f"{status:6} {seconds:6.0f}s  {name}  {' '.join(summary)}", flush=True)
            if code != 0:
                failures.append((name, output))
    for name, output in failures:
        print(f"\n===== {name} =====\n{output[-6000:]}")
    print(f"\n{len(names)} project(s) in {time.time() - started:.0f}s; {len(failures)} failed")
    return 1 if failures else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("lane", choices=["affected", "fast", "full"])
    parser.add_argument("--base", help="git ref the change is measured from (affected; default: merge base with origin/master)")
    parser.add_argument("--project", action="append", help="limit to these test projects (fast)")
    parser.add_argument("--suite", action="append", help="limit Aetheris.Kernel.Firmament.Tests to these suites (fast)")
    parser.add_argument("--include-slow", action="store_true", help="keep Slow tests (affected, fast)")
    parser.add_argument("--no-build", action="store_true", help="skip `dotnet build Aetheris.slnx -c Release`")
    parser.add_argument("--dry-run", action="store_true", help="print the selection and stop")
    parser.add_argument("--jobs", type=int, default=max(2, os.cpu_count() or 2), help="test projects run at once")
    args = parser.parse_args()

    config = json.loads((ROOT / "scripts" / "test-suites.json").read_text(encoding="utf-8"))
    graph = projects()
    tests = sorted(name for name in graph if is_test_project(name))
    suites = None
    test_filter = None if args.include_slow else FAST_FILTER

    if args.lane == "full":
        names, test_filter = tests, None
    elif args.lane == "fast":
        names = sorted(set(args.project)) if args.project else tests
        suites = sorted(set(args.suite)) if args.suite else None
        unknown = [s for s in suites or [] if s not in config["firmamentSuites"]]
        if unknown:
            parser.error(f"unknown suite(s) {unknown}; known: {config['firmamentSuites']}")
    else:
        files, base = changed_files(args.base)
        selected, suites, reasons = select(files, graph, config)
        names = sorted(selected)
        print(f"change since {base[:10]}: {len(files)} file(s)")
        for reason in reasons[:40]:
            print(f"  {reason}")
        if len(reasons) > 40:
            print(f"  ... {len(reasons) - 40} more")

    print(f"lane {args.lane}: {len(names)} test project(s); filter {test_filter or '(none)'}; "
          f"Firmament suites {', '.join(suites) if suites else 'all'}")
    for name in names:
        print(f"  {name}")
    if args.dry_run or not names:
        return 0
    if not args.no_build:
        build = subprocess.run(["dotnet", "build", "Aetheris.slnx", "-c", "Release", "-v", "q", "-nologo"], cwd=ROOT)
        if build.returncode != 0:
            return build.returncode
    if args.lane == "full":
        # The solution-level run schedules every project itself and is faster than one process per project.
        return subprocess.run(["dotnet", "test", "Aetheris.slnx", "-c", "Release", "--no-build"], cwd=ROOT).returncode
    return run(graph, names, test_filter, config, suites, args.jobs)


if __name__ == "__main__":
    sys.exit(main())
