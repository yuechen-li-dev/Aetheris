# Humanoid research tooling

The humanoid module is experimental. **No usable canonical adult is currently
promoted.** `CanonicalAdultTemplate.Create()` retains the NONCANONICAL X0
synthetic body for regression tests. Its legacy `adult.standard.v1` identity
must not be mistaken for a qualified Antonia body.

The X1 tool admits the original Antonia 1.2.0 base figure with its CC-BY 3.0
notice, retains source polygon identity, generates candidate skin weights and
evaluates A-pose preparation through Aetheris's normal skin evaluator. Its
output is an `AntoniaAdoptionCandidate`, **not** a `CanonicalHumanoid`.
Candidate loading as a canonical runtime artifact is rejected.

From the repository root:

```powershell
pwsh -File scripts/qualify-humanoid-x1.ps1
# Cached inputs, focused tests, and evidence rendering:
pwsh -File scripts/qualify-humanoid-x1.ps1 -SkipFetch -SkipFullTests
# Direct bounded adoption, without Blender or network:
dotnet run --project tools/Aetheris.Humanoid.X0 -- adopt-antonia --source artifacts/local/humanoid-x1/source/Antonia-1.2.obj --notice artifacts/local/humanoid-x1/source/README --out-dir artifacts/local/humanoid-x1
```

The script verifies source/license hashes, runs adoption twice, checks exact
JSON/OBJ replay, renders evidence, and runs tests. `-SkipRender` omits Blender;
Blender only renders the exported Aetheris geometry. Source rig, CharMorph and
Rigify are never runtime dependencies. A successful reproduction exit means
the recorded experiment ran, **not** that deformation passed.

Inspect `adoption-evidence.json` for counts, identities, bind error, symmetry,
weight coverage and each failed pose. The expanded
`antonia-adoption-candidate.json` contains source/prepared positions, fixed
binding triangles, original quads, source index maps, regions, components,
paired vertices, 55-joint proxies, weights, preparation rotations and
provenance. All generated output stays in ignored `artifacts/local/humanoid-x1/`.

The first generated weights cause shoulder and hip deformation defects. X1
therefore supplies **no canonical measurement, landmark, attachment or morph
API for this candidate**. The existing `inspect`, `morph`, `pose`, and `sweep`
commands still operate on X0-format runtime artifacts; they cannot qualify an
X1 candidate by relabeling it.

For a future separately licensed reference, retain it as `ImportedHumanoid`,
verify its frame, pose and component admission, then fit positions against the
eventually approved canonical topology with reviewed landmarks and bounded
correspondence. Keep canonical vertex/face IDs, connectivity and binding
triangulation unchanged. Source vertex indices, rig names and weights cannot
be copied by index onto a different topology. Any unresolved admission or
deformation gate prevents canonical promotion.

Implementing that adapter is blocked until a qualified target and its explicit
versioned identity are selected. The legacy X0 `adult.standard.v1` must not be
reused for Antonia's different connectivity. Fitting algorithms and numerical
budgets will need qualification against the approved target.

See [the X1 release record](../release/HUMANOID-X1.md) for evidence and
[third-party notices](../../THIRD_PARTY_NOTICES.md) for attribution.
