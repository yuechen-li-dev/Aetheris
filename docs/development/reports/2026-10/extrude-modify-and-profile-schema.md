# Extrude takes Modify features; Profile constructs join the schema

## What changed

- A single `Extrude` with holes, slots or patterns in a `Modify` used to build without them: the
  plain profile route lowered only the Profile. It now builds as `Compose name { Base name { ... } }`
  (field text carried verbatim), so the features are cut exactly as on Compose. Edge finishes alone
  keep the plain profile route.
- `Perforation` in a Modify on a Compose (or Extrude) body was silently ignored. Every Modify feature
  the section stack cannot lower now rejects (`compose-modify-feature-unsupported`) at its declaration.
- The semantic schema declares `Profile`, `Extrude`, `Compose`, `Base`, `Rect2`, `RoundedRect2`,
  `Circle2` and `Slot2` with buildable entries; EdgeFinish help points at Profile corner rounding.
- Diagnostics: codes naming a declaration (`code:Kind:Name`, `host=Name`) are located at it; the
  message is the producer's explanation, not the raw code; a bare code beside its explained form is
  dropped. The EdgeFinish-on-Profile-boundary rejection explains what is supported.
- Web runtime: Profile-built bodies are named and located by their declaration; hole tree nodes
  carry support face, local center and world point.

Found while an agent used Helios through MCP (HeliosCAD
`docs/development/reports/2026-10/agent-dogfooding-mcp.md`).

## Evidence

- `ModifyFeaturesOnAPlainExtrude_AreCutLikeCompose`: same holes and centres as the Compose spelling.
- `UnsupportedModifyFeaturesOnProfileBodies_RejectAtTheirDeclaration`: line of the Perforation.
- `ProfileWorkflowEntries_BuildAsWritten`; the reference example in solids-and-features builds.
- Full `Aetheris.Kernel.Firmament.Tests`: 3,213 pass.

## Verdict

Success. Not done: an EdgeFinish target for the vertical edges of a Box. It would rebuild the Box as
a Compose and lose Box-only features such as side holes; the Profile spelling is exact and documented.
