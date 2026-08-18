# 01 — Geometry Inventory vs. Modeling Strategy

Core lesson: do not confuse what faces and features are visible with how the model should be authored.

A geometry inventory is useful evidence. It is not a feature tree.

## Geometry inventory

A BRep face inventory may report:

- Planes.
- Cylinders.
- Cones.
- Loops.
- Holes.
- Slots.
- Adjacent faces and edge types.

This tells the LLM what exists in the final shape. It does not prove which operation created each face.

## Modeling strategy

A modeling strategy describes a robust construction path, such as:

- Start with a base mass.
- Add or remove simple volumes.
- Use semantic hole and slot features where available.
- Pattern or mirror repeated features instead of duplicating them manually.
- Finish edges last with fillets, chamfers, or rounds.

The strategy should preserve design intent and stable references, not merely match the final shell.

## Motivating example: CTC-01

A naive interpretation of a prismatic part like CTC-01 might be:

- “Extruded outline with holes.”

That may match some visible geometry, but it can hide design intent and produce fragile source if the outline is doing too much.

A more resilient interpretation to consider is:

- “Block out the prismatic mass with boxes and cuts, then apply holes and slots, then finish edges.”

This note does not claim the exact CTC-01 feature tree. The point is conceptual: final faces should be translated into a plausible authoring strategy before generating Firmament candidates.

## LLM rule of thumb

Before writing source, ask two questions:

1. What geometry is visible?
2. What construction order would make that geometry stable, editable, and CAD-native?

If the answers differ, report both. Do not collapse strategy into inventory.
