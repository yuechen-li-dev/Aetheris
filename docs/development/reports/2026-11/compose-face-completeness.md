# Compose face completeness

Every face of a composed body now has a stable selector in inspection, picking (Helios Label/Inspector) and PMI.

## What changed

- `NameComposeFaces` (FirmamentBuildAndExport.ComposeFaces.cs) runs on the correspondence of the section-stack,
  composed-chamfer and profile-rim routes before PMI and runtime use. It names:
  - profile sides split by section levels (coplanar or co-cylindrical pieces share one `Side` name), single curved sides;
  - caps of Base/Boss/Pocket and now plain Add/Remove and Add/RemoveOffset operations, in the composition frame
    (XZ layouts were missed before: caps assumed +Z);
  - hole walls `H.Wall`, `H.Counterbore`, `H.Shoulder`, `H.Countersink`, blind-drill `H.Tip`;
  - feature walls by promoting the alias side selector to the owner;
  - chamfer faces `Finish.Side("…")`;
  - hollow cavity floors/ceilings `op.CapInside`.
- The composed-chamfer route now returns `RuntimeBody`/`RuntimeCorrespondence` (11 fixtures had none).
- Construction-plane blind drills carried no descendant for the host face they replace; they now carry it.
- Add operations name `Bottom` as well as `Top` (lugs hung below the stock).
- Shoulder and drill-point faces are excluded from hole diameter targets.
- Junction fillets (`where: A meets B`) transfer face correspondence across the rebuild by surface identity and
  overlap (`FaceCorrespondenceTransfer`; a merged face is owned by its largest source, the others stay as aliases), name rounding faces `Finish.Fillet`, and rebuild PMI on the filleted body.
  Before, any `Pmi` on such a part failed with `pmi-export-evidence-mismatch`.
- Parser: blind-drill names are hole owners; finishes and cap owners are face owners (`face-owner:` alias).

## Evidence

Survey of 66 non-invalid Compose fixtures (52 build):

| | before | after |
|---|---|---|
| every face named | 10 of 52 | 51 of 52 |
| no correspondence | 14 | 1 (`coaxial-primitive-compose`) |

`ComposeFaceNamingTests` (18 cases, including ctc-01 with two junction fillets). The 14 non-building fixtures fail identically
before and after (libraries, assemblies, expected rejections).

## Remaining

- Coaxial Compose (`Cylinder`/`Frustum` spans) has no correspondence and its parser refuses `Pmi`; left as is.
- `face(Finish.Fillet)` faces are named but not PMI targets (the junction route parses the source without its finishes).
- A split side shares one selector across pieces; Helios resolves the first piece for geometry, which is the same plane.

Verdict: success for the stack, chamfer, rim and junction routes; coaxial is an honest stop (separate consumer).
