# Assembly architecture

AETHERIS-ASSEMBLY-M0 defines two independent structures. The **assembly tree** is the authoritative product/BOM hierarchy: assemblies and part instances have a parent, ordered children, a definition identity, and a deterministic instance path. The **Mate graph** connects compatible `SemanticValue` participants reachable through that hierarchy. Tree ancestry never implies a Mate, and a Mate may connect siblings, cousins, or exposed subassembly participants.

A part instance and a subassembly instance are distinct occurrences of a definition. `BearingModule.Rotor.Shaft` is a structured compiler path; its stable ID is `assembly-instance:BearingModule.Rotor.Shaft`. Exposed definition semantics are cloned into the instance scope, so `LeftBolt.Shank` and `RightBolt.Shank` retain common definition provenance but never share final semantic IDs.

An `Interface` is a relational Concept. A normal Concept describes one semantic object; an Interface declares named Roles, structural capability requirements, relational requirements, and optional explicitly admitted rigid freedoms. A `Mate` instantiates one Interface by assigning actual `SemanticValue` participants to every Role. No origin-specific branch exists: native, Template/Table-derived, Forge-produced, and recognized values are admissible when their capabilities and exact bindings prove the contract.

The compiler lowers source syntax before downstream work into `AssemblyIr` (`aetheris/assembly-ir/m0`, or `aetheris/assembly-ir/m1` after geometry execution): explicit tree nodes, Interface definitions, a Mate graph, typed placement constraints, placement results and residual/status data, dimensional relations, stackup results, provenance, diagnostics, and phase timings. Nominal geometry remains nominal; tolerance is symbolic engineering intent. M1 specializes ordinary Firmament Templates, materializes each distinct definition once, applies Mate-derived transforms per instance, and records an `aetheris/assembly-geometry/m1` instance artifact without Boolean-flattening the product.

## Placement convention

`Anchor: path;` fixes the containing part occurrence at identity. Axis coincidence is solved analytically with double-precision rigid rotation and translation. Axis-plus-plane seating removes axial translation. Remaining translations and rotations are reported; an Interface may explicitly admit a symmetry freedom such as `rotation:about-axis`. Multiple Mate-derived transforms for one instance are compared and conflicting translations produce `assembly-placement-overconstrained`; there is no last-mate-wins behavior.

M0 intentionally does not implement general nonlinear solving, kinematics, contact, assembly FEA, auto-fastener selection, or random/statistical tolerancing.
