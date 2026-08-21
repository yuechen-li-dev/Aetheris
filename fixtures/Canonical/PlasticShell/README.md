# PlasticShell canonical fixtures

`plastic-shell-basic.firmament` is the minimal bounded analytic shell. `plastic-shell-enclosure.firmament` is the MOLD-X0/X0a flagship with an explicit gate, four physically materialized PCB standoffs, four ejector contacts, and two AutoRib candidates.

The focused X0a fixtures isolate a single standoff, a two-support rib, a three-support rib/standoff junction, the four-support AutoRib workflow, and the complete materialized enclosure. All lower through the same exact one-body B-rep materializer; none are manual Boss or Rib authoring examples.

Build the flagship through the real domain path:

```powershell
dotnet run --project Aetheris.CLI -- build fixtures/Canonical/PlasticShell/plastic-shell-enclosure.firmament --output artifacts/local/mold-x0a/mold-x0a-materialized-enclosure.step --json
```

X0a physically realizes the exact drafted shell, analytic annular standoffs, and constant-shell-thickness flat-top rib walls as one manifold body. Standoff and rib identities retain geometric face associations in AP242 and the evidence sidecar; ejectors and gates remain tooling/manufacturing semantics rather than extra product bodies. The vertical ribs truthfully report zero release draft.

The former polar height field is not a canonical product fixture. It survives only behind `aetheris experimental heightfield-art` as explicitly non-manufacturing mathematical art.
