# Extrude with Modify features rejects; Profile constructs join the schema

## What changed

- A single `Extrude` lowers only its Profile. A `Modify` on it holding a Hole, Perforation or
  Pattern was admitted and then never cut: the build succeeded with the feature missing.
  It now rejects with `firmament-v2-extrude-modify-feature-unsupported`, naming the dropped
  feature and the `Compose { Base ... }` form that does cut it. EdgeFinish on an Extrude keeps
  its existing profile-boundary route.
- The semantic schema declares the everyday part path: `Profile`, `Extrude`, `Compose`, `Base`,
  `Rect2`, `RoundedRect2`, `Circle2` and `Slot2`, with field kinds and buildable entries.
  Completion offers them as construct entries. The Hole `On` help now says Box and Compose stock.

Found while an agent used Helios through MCP: the hole vanished without a diagnostic, and the
schema gave no route to a Profile-based body (see HeliosCAD
`docs/development/reports/2026-10/agent-dogfooding-mcp.md`).

## Evidence

- `FirmamentSemanticSchemaTests.ProfileWorkflowEntries_BuildAsWritten` compiles every new entry
  in its documented host, including Compose with a Modify hole.
- `ModifyFeaturesOnAPlainExtrude_RejectInsteadOfVanishing`.
- No fixture used Extrude with Modify holes; the one Extrude + Modify fixture is an EdgeFinish.
- Targeted subset (schema, completion, profile, outline, sketch, IntelliDraft, hole, grammar): 510 pass.

## Verdict

Success. The location of this rejection in CLI/web output is the document anchor (line 1); the
parser's feature span is not carried through the string-code admission path.
