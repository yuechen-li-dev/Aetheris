# 99 — Glossary

## BRep

Boundary representation: a model of solid geometry using faces, edges, vertices, loops, surfaces, and topology.

## STEP/AP242

A STEP data exchange format profile used to carry CAD product geometry and related model data between systems.

## Firmament

Aetheris source language for describing CAD models and related design intent at a higher level than raw kernel geometry.

## AIR

Aetheris Intermediate Representation: an internal representation used between source-level authoring and lower-level geometry or kernel operations.

## CIR

Constructive Intermediate Representation: an intermediate layer for constructive geometry intent and operations.

## DisplayIR

A representation used for display-oriented inspection and visualization data.

## Feature tree

An ordered sequence of modeling operations that constructs a part from stable references and earlier features.

## Modeling strategy

The CAD-native construction plan for a part: what to build first, what to cut later, what references to preserve, and what details to defer.

## Semantic decompilation

Recovering a plausible modeling strategy, feature tree, and design intent from final geometry and inspection evidence.

## Prismatic object

A mostly block-like, machined, extruded, or planar-faced object where boxes, cuts, pockets, holes, and slots explain much of the shape.

## Blockout

The early gross mass of a model before fine details and edge finishing are applied.

## Boss

An added protruding mass, often used as a pad, raised feature, or mounting region.

## Cut

A subtractive operation that removes material from an existing body.

## Pocket

A recessed cut that removes material without necessarily passing through the entire part.

## Slot

An elongated cut or opening, often with straight sides and rounded or rectangular ends.

## Through-hole

A hole that passes completely through the body.

## Clearance hole

A hole sized to allow a fastener or shaft to pass through without threading into that part.

## Fillet

A rounded transition applied to an edge or corner.

## Chamfer

A beveled transition applied to an edge or corner.

## Round

A general rounded edge treatment; often used interchangeably with fillet in casual CAD discussion.

## Edge finish

Late-stage edge treatments such as fillets, chamfers, and rounds.

## Sketch/profile

A 2D curve, loop, or region used as input to an operation such as extrude, cut, revolve, or sweep.

## Resilient modeling

Modeling that remains understandable and editable because it uses stable references, clear feature order, and intent-preserving operations.

## Geometric equivalence

Two models are geometrically equivalent when their final shapes match, even if they were authored through different feature trees.

## Design intent

The functional and editable meaning behind the geometry: why features exist, which dimensions matter, and how the model should change safely.
