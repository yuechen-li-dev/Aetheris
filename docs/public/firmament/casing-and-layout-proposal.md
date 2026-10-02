# Firmament casing and layout proposal

Status: accepted convention; bounded formatter and guitar sweep implemented,
2026-10-01. The current contract is [language style](language-style.md).
The audit below records the pre-change behavior. This is not an exhaustive
syntax migration: authored ports and template arguments require paired manual
edits; unknown scopes and legacy spellings may remain. The guitar STEP, USD and
display geometry are unchanged by the implemented sweep.

Keep the artifact vocabulary prominent, and let its descriptions read quietly.
Firmament should still read as a specification of objects, scaffolding, and
construction recipes. PascalCase on every property and reference currently makes
all those different roles compete for attention.

## Pre-change audit

- `language-style.md` prescribes PascalCase for both constructs and built-in
  fields. Authored names remain stylistically free.
- `FirmamentV2Parser` mixes case-sensitive canonical construct patterns with
  selected case-insensitive compatibility paths. Lowercasing a file is not a
  supported general conversion.
- `AssemblyM0Parser`, `AssemblyFrameAuthoring`, Concept point expansion, and
  Template/Feature binding also match specific spellings. For example,
  `TranslateLocal`, `Over`, `On XY`, and named Record members do not all share
  one spelling policy.
- The actual V2 editor formatter is
  `FirmamentLanguageAnalysisService.Format`. It preserves token text, changes
  spacing/layout, and reparses with `FirmamentV2Parser`. It does not currently
  perform semantic renaming or select all the assembly/project authoring paths.
  The older `FirmamentFormatter` and `FirmamentCanonicalFormatter` belong to
  the compatibility frontend and are not the V2 migration seam.
- Generated semantic schemas already separate stable construct/field IDs from
  their displayed names. That is a useful existing seam for preferred spelling,
  completions, hover, and formatting; it is not yet a universal grammar registry.
- Symbol and published-port lookups use case-sensitive identities. Names also
  feed generated pattern identities, STEP product names, and provenance.

The current standalone functional pickup still validates through Aetheris.CLI.
The proposed spellings have not been compiled. Existing source remains the
compatibility baseline.

## Recommended convention

| Semantic role | Convention | Examples |
| --- | --- | --- |
| Artifact/declaration and construction kinds | PascalCase | `Model`, `Assembly`, `Part`, `Concept Struct`, `Profile`, `Compose`, `Feature`, `WireRoute`, `FrameTransform` |
| Reusable definition/type names | PascalCase | `Humbucker`, `ControlKnob`, `PickupSpec`, `Length`, `Point2` |
| Concrete values, local geometry, occurrences, patterns, public ports | camelCase | `standardPickup`, `layout`, `outline`, `neckPickup`, `poles`, `bottomSeat` |
| Property labels and value parameters | camelCase | `width`, `polePitch`, `from`, `translateLocal`, `minimumBendRadius`, `spec` |
| Linking words and scope directives | lowercase | `using`, `on`, `over`, `with`, `return`, `include`, `expose`, `bind` |
| Scalar math functions and their parameters | camelCase | `fretDistance(n, scale)`, `pow(...)` |
| Built-in variants, domain symbols, and external identities | Preserve domain spelling | `Fixed`, `Revolute`, `Periodic`, `Stock`, `XY`, `+Z`, `mm`, `304_Annealed` |

This is a recommendation for authored style, not a rule that infers a symbol's
type from its capital letter. Definitions and occurrences are distinguished by
their declaration/binding context. A root product may retain its definition's
name, such as `Assembly GuitarX0` and `<Assembly GuitarX0>`; nested occurrences
then read as `body`, `neck`, and `electronics`.

Keep `Static`, `Template`, `Record`, and `Function` as explicit declaration
kinds. Keep geometry and engineering forms such as `Placement`, `Interface`,
`Mate`, `Linear`, `Series`, `Require`, and `Assert` prominent too. An anonymous
`Placement` is still a domain construct; it does not become lowercase simply
because it has no authored name. `expose` and `bind` introduce publication and
input scopes rather than a new physical object.

Reusable Feature constructors remain PascalCase, e.g. `Coil(...) -> Boss`.
Their returned construction and named invocation results read as ordinary
values. Scalar Functions use camelCase, e.g. `fretDistance(...) -> Length`.
Both remain bounded compile-time evaluation; this adds no runtime execution,
`Comptime` keyword, inheritance, or general statement language.

## Pickup: quieter fields, visible construction

Current excerpt:

```firmament
Feature Coil(Center: Point2, Width: Length, Depth: Length,
             Height: Length, Support: Plane) -> Boss {
  RoundedRect2 Outline { Center: Center; Size: [Width,Depth]; Radius: 3mm }
  Profile Section { Loop Outer { Outline |> TraceLoop } }
  return Boss { On: Support; Profile: Section; Height: Height }
}
```

Proposed spelling and layout:

```firmament
Feature Coil(center: Point2, width: Length, depth: Length,
             height: Length, support: Plane) -> Boss {
  RoundedRect2 outline {
    center: center;
    size: [width, depth];
    radius: 3mm;
  }

  Profile section {
    Loop outer { outline |> TraceLoop }
  }

  return Boss {
    on: support;
    profile: section;
    height: height;
  }
}
```

The same distinction helps the specification and its instances:

```firmament
Record PickupSpec {
  width: Length;
  depth: Length;
  polePitch: Length;
}

Static standardPickup: PickupSpec {
  width: 88mm;
  depth: 46mm;
  polePitch: 10.2mm;
}

Static bridgePickupSpec = standardPickup with { polePitch: 10.4mm }

// Inside the full Humbucker recipe's Compose body:
Feature poleSeed = Pole(center: layout.firstPole, support: coilTop)
Linear Pattern poles {
  source: poleSeed;
  direction: layout.longitudinal.direction;
  count: 6;
  spacing: spec.polePitch;
}
```

These are excerpts, not standalone complete pickup definitions. `width` in the
Record is an authored member; `size` in `RoundedRect2` is a compiler-owned field.
They look consistent but require different migration mechanisms.

## Assembly: preserve the product tree

```firmament
Subassembly ControlKnob {
  <Assembly ControlKnob>
    <Part skirt = Drum<R: 12mm, H: 3mm>>
      material: Brass;
    </Part>
    <Part grip = Drum<R: 9mm, H: 9mm>>
      material: AmberPolymer;
    </Part>
  </Assembly>

  Interface<Fixed> coaxialStack {
    datum: knobLayout.spindle;
    members: [ControlKnob.skirt.spindleSeat, ControlKnob.grip.spindleSeat];
  }

  Mate gripOnSpindle: coaxialStack {
    member: ControlKnob.grip.spindleSeat;
    at: 3mm;
    clocking: 0deg;
  }
}
```

This excerpt assumes the referenced scaffolding and ports exist. The XML-like
containment still makes the assembly explicit. The lowercase properties explain
how its objects relate. Existing `Drum` parameter names `R` and `H` are preserved
here deliberately: replacing them with `radius` and `height` requires changing
the definition and its bound call sites, rather than formatting a use site.

Likewise, keep current published `.Frame`, `.BottomSeat`, and other authored
members until a definition-aware rename changes their declarations and every
reference. New definitions should prefer `frame` and `bottomSeat`. Intrinsic
point projections such as `point.Position.X` can gain preferred
`point.position.x` spelling only through their owning point binder; a formatter
must not lowercase arbitrary dotted references.

## Layout rules

Use two-space indentation, preserving the current V2 convention. Give substantial
objects and multi-field interfaces one field per line, and separate scaffolding,
material construction, and publication with blank lines. Keep tiny single-field
blocks compact where they remain legible. Keep closing XML tags on their own
lines for multi-line occurrences.

Use spaces after commas and around assignments, arrows, and arithmetic. Keep
unit suffixes attached (`0.1mm`, `13deg`) and dotted references uninterrupted.
Prefer descriptive new parameters to abbreviated `L/W/H/R`, with explicit
refactoring for existing definitions. Use a soft 100-column wrapping target;
never reorder profile spans, route events, section stations, keyed Sets, or
assembly children to satisfy layout.

Preserve semicolons required by the owning grammar. A formatter may standardize
optional field terminators only in grammars that admit them; this is not a reason
to redesign delimiters in the casing milestone. Preserve comments, quoted text,
numeric literals, engineering units, and material/standard identifiers. In
particular, do not round derived surface controls while formatting. The guitar's
0.1 mm authoring convention does not authorize changing exact geometry values.

## Implementation boundary

1. **Compiler-owned spelling aliases.** Give constructs, fields, and admitted
   linking words preferred source spellings and explicit legacy aliases, with
   stable semantic IDs unchanged. Resolve a field under its owning construct;
   reject duplicate aliases such as `From` plus `from` in the same block. Keep
   user names and external identities case-sensitive. Do not make every regex
   case-insensitive or globally lowercase the source.
2. **Safe formatting.** Extend the existing V2 formatting service using bound
   token roles and source spans, not string replacement or BRep reconstruction.
   Apply preferred spelling only to positively identified compiler-owned tokens.
   Use the appropriate existing frontend/project context for admission before
   and after formatting. Preserve unknown/ambiguous tokens or leave that document
   unformatted with a specific diagnostic. No second grammar in the editor.
3. **Separate authored-name refactoring.** Offer an explicit, previewable rename
   for Record fields, parameters, concrete geometry, ports, and occurrences.
   Include imported definitions and references; refuse incomplete project
   context and collisions. Ordinary Format Document must not change public
   identities, generated pattern keys, STEP product names, or exported provenance.
   A reviewed migration may intentionally change those names; compare geometry
   and report the identity changes rather than calling them byte-equivalent.
4. **Dogfood and qualify.** First apply supported spelling/layout to the guitar's
   pickup, knob, and composition root. Qualify each newly admitted construct or
   frontend before advertising its spelling. Generate completions/help from the
   same authority and then update canonical documentation and fixtures. Keep
   accepted legacy source working during migration; do not repoint the current
   style guide at unsupported snippets.

Verification should check legacy/preferred semantic equivalence, unchanged
geometry and transforms, unchanged identities for formatting alone, idempotence,
comments/strings, alias collisions, and binding-sensitive renames. Include the
guitar, profile editing, section chains, WireForm routes, and PMI release records,
then run the repository's Release build, fast lane, and full serial gate.

The first bounded implementation should be preferred field labels and linking
words plus layout, with authored-name migrations kept explicit. It should not
rename the language's artifact constructs or redesign its authoring grammar.
