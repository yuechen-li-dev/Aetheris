# Typed interfaces and reusable subassemblies

Firmament assemblies have two independent structures: the product tree owns definition/occurrence identity, while the Interface graph owns mechanical relationships. Tree nesting never implies a Mate.

`Concept<T>` describes truths that make one semantic object valid. `Interface<T>` describes truths that make several objects valid together. `Subassembly` is a reusable, locally solved Assembly definition. `Expose` names the small public semantic contract visible to a parent.

## Compiler-owned interface families

The Assembly profile admits this bounded matrix:

| Interface | Expansion | Admitted motion | Status |
|---|---|---|---|
| `Interface<Fixed>` | `FrameCoincident` | none | qualified |
| `Interface<Axial>` | `AxisCoincident` | translation along and rotation about the axis | qualified |
| `Interface<Revolute>` | `AxisCoincident` + `PlaneCoincident` | rotation about the axis | qualified |
| `Interface<Custom>` | authored atomic `Mate` lines and bounded numeric `Require` checks | explicitly authored | qualified escape hatch |
| `Interface<Gear>` | the existing Gear evaluator plus occurrence-space axis and center-distance evidence | ideal ratio/sign metadata | qualified for direct and exposed hierarchical Gear endpoints |

These are compiler-owned families, not user-defined generics. Atomic assembly relations remain the existing `AxisCoincident`, `AxisAligned`, `PlaneCoincident`, `PointCoincident`, `OffsetAlongAxis`, and `FrameCoincident` kinds. A typed family expands into those relations before the ordinary Assembly compiler runs.

```firmament
Interface<Revolute> ShaftBearing {
    A: Module.Shaft.Joint;
    B: Module.Bearing.Joint;
}

Interface<Custom> CarryLink {
    A: Register.Digit1.Input;
    B: Register.Digit0.Output;
    Mate AxisCoincident A.Axis B.Axis;
    Mate PlaneCoincident A.Seat B.Seat;
    Require PositiveClearance => 1mm > 0mm;
    Allow rotation:about-axis;
}
```

`asm inspect` reports the typed Interface, its endpoint paths, expanded atomic Mates, requirement status, shared definition counts, the hierarchical occurrence tree, and the source dependency hashes. JSON inspection retains the same data structurally.

## Multi-file subassemblies

`Include` is compile-time semantic composition. Paths resolve relative to the including file and must remain under the repository source root (or, outside a checkout, the root document directory). Missing files and include cycles are typed errors. Duplicate Interface or Subassembly definitions are rejected; aliasing is not admitted in X1.

```firmament
Include "digit-module.firmament";

Subassembly Register {
    <Assembly Register>
        <Assembly Digit0 = DigitModule></Assembly>
        <Assembly Digit1 = DigitModule></Assembly>
    </Assembly>
    Anchor: Register.Digit0.Output;
    Expose {
        Semantic Input = Digit0.Input;
        Semantic Output = Digit1.Output;
    }
}
```

Every occurrence has a stable hierarchical path, while repeated occurrences share one solved semantic definition. Internal child occurrences and semantic values are private across a Subassembly boundary. `Expose` aliases an internal semantic value; it does not copy geometry or mutate the shared definition.

Semantic type survives hierarchy: `Expose` changes visibility, not type. A Gear endpoint therefore remains backed by the original Gear AIR—including family, teeth, module, pressure angle, pitch geometry, axis, and phase—while each occurrence contributes only its own placement and identity.

```firmament
Subassembly DigitModule {
    <Assembly DigitModule><Part OutputGear = Wheel></Part></Assembly>
    Anchor: DigitModule.OutputGear;
    Expose { Gear Output = OutputGear; }
}

Interface<Gear> Transfer {
    A: Machine.RegisterA.Output;
    B: Machine.RegisterB.Input;
}
```

The parent cannot name `OutputGear` unless the child exposes it. The exposed alias does not restate gear parameters, and `Interface<Gear>` invokes the same compatibility evaluator used by a direct gear pair. Inspection reports both the public port path and the occurrence-qualified source path.

The executable example is [`fixtures/Canonical/AssemblyInterfaces/machine.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/machine.firmament). It includes a Register definition, which includes a DigitModule definition; instantiates two Registers and four Digits; and connects the Registers only through `Input` and `Output`.

The hierarchical Gear flagship is [`fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament); the two-level form is [`nested-register-gear.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/nested-register-gear.firmament).

Subassembly composition never exports a child STEP and imports it into its parent. Included definitions form one semantic source graph, the existing Assembly compiler solves the whole tree, and the existing AP242 exporter lowers the resulting hierarchy once when every leaf has executable geometry.

Legacy JSON `.firmasm` remains a compatibility/import lane. Canonical reusable semantic authoring uses `.firmament`; the compatibility format does not gain runtime inclusion or become the design authority.
