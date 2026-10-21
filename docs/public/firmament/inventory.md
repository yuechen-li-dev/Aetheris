# Inventory Records and module references

Use `include` for a short composition whose declarations can share names in one
module. Use an `Inventory Record` when named, qualified dependencies make a larger
Assembly or Scene easier to organize. Neither form has a one- or two-part limit.

For a short typed import, name the resource directly:

```firmament
include bearing: Part = Step("vendor/bearing");
include drive: Assembly = Step("vendor/drive");
include bracket: Part = Firmament("parts/bracket").Bracket;
```

Use `bearing`, `drive` or `bracket` as an occurrence's initializer. These are
module references, with the same admission and identity rules as catalog members.
They do not inject the imported module's private declarations into the caller.
The original `include "parts/bolt";` remains declaration composition.

Firmament's importable module kinds are Part, Subassembly, Assembly and Scene.
Inventory references inside occurrence Patterns bind before expansion.
An Inventory member is a reference to an exported definition or admitted typed family. Adding it to a catalog
does not place geometry or establish a Mate; occurrences and relationships do that.

## A typed catalog

```firmament
Inventory Record Components {
  bracket: Part = Firmament("parts/bracket").Bracket
  drive: Subassembly = Firmament("assemblies/drive").Drive
  machine: Assembly = Firmament("assemblies/machine").Machine
  laboratory: Scene = Firmament("scenes/laboratory").Laboratory
  bearing: Part = Step("vendor/bearing")
  gearbox: Assembly = Step("vendor/gearbox")
}
```

This is a declaration fragment: provide the named files and exports in your project.
`Firmament(...)` selects a named root declaration; `Step(...)` supplies one exact
Part through the existing STEP importer. An explicit `Assembly = Step(...)`
retains the imported product tree, local occurrence transforms and shared leaf
geometry. Foreign hierarchy must be trustworthy: this form rejects the importer's
fallback that normalizes ambiguous multiplicity to a flat assembly. Missing or
ambiguous exports and kind mismatches reject. A Part still requires one body;
there is no implicit first-body selection.

Imported annotation passthrough is not qualified. A STEP resource containing PMI
rejects unless the binding explicitly says `Step("vendor/gearbox", geometryOnly:
true)`. That exclusion is retained in the binding receipt; the original file is
untouched. This permission does not relax geometry admission.

The normal spelling omits the extension. `Firmament("parts/bracket")` resolves
`.firmament` or `.firmasm`; `Step("vendor/bearing")` resolves `.step` or `.stp`.
Exactly one candidate must exist. An explicit extension is also accepted and is
the repair when both candidates exist. No search path, directory scan or global
package registry chooses a resource behind the author's back.

Locators resolve relative to the document containing the declaration and must
stay inside the existing allowed source root. Source snapshots resolve from their
provided document map, without reading files from disk. Snapshots can include
`.step`/`.stp` text assets and the optional lock alongside Firmament source.

## Place the referenced definitions

Given the catalog above, an Assembly can place its Part and Subassembly entries
through the existing product tree:

```firmament
Assembly Machine {
  <Assembly Machine>
    <Part Bracket = Components.bracket>
      Placement { from: Origin; to: World; }
    </Part>
    <Assembly Drive = Components.drive>
      Placement { from: Origin; to: World; translateLocal: [100mm, 0mm, 0mm]; }
    </Assembly>
  </Assembly>
  Anchor: Machine;
}
```

Use `<Assembly ...>` for both Assembly and Subassembly entries. They retain their
own locally solved product tree and published semantic contracts. Definition-owned
ports participate in the ordinary Interface/Mate solver. Tree nesting supplies no
mechanical relationship by itself. Existing interference checks still apply.

A Scene can place Part, Assembly/Subassembly or Scene entries. A nested Scene uses
its own units, rooms, definitions and placements, composed into its parent's world
frame. Its cameras remain private to that Scene; they do not replace parent cameras.
Nested Scene placement currently starts at `Origin`. For example, inside a Scene:

```firmament
<Scene laboratory = Components.laboratory>
  Placement { from: Origin; to: World; translateLocal: [10m, 0m, 0m]; }
</Scene>
```

Complete executable projects are [the machine](../../../fixtures/Regression/Inventory/machine.firmasm)
and [the factory Scene](../../../fixtures/Regression/Inventory/factory.firmament).

## Scope, exports and compatibility

Inventory names use PascalCase; member names use camelCase. Imported export names
and other domain/user symbols retain their authored spelling. Names resolve in
lexical scopes: a local Inventory shadows an outer Inventory, and sibling catalogs
remain private. Parameters, local values and Pattern iteration names protect their
own names from an enclosing catalog. Members are qualified, such as `Components.bracket`. Unknown
members, repeated names, invalid headers and unsupported adapters produce errors.

Root Model, Struct and Part declarations can satisfy a Part entry. Model and
Struct retain their existing geometry meanings inside the defining source; the
import does not redesign those frontends. Other root products in that file are
excluded from the selected Part's engineering compilation. Supporting Records,
Statics, Features, Templates and Concept scaffolding remain in their owning module.
Identical export or Record names in different imported modules do not collide.

Part and Subassembly entries can reference typed Template exports. Supply named
arguments at the occurrence, for example `<Assembly Cylinder = Units.cylinder<config: config>>`.
The common Template/Record binder checks the values against the definition's
signature. The definition keeps its own private Records, Statics, Inventory and
supporting declarations. Caller data does not replace that environment. A missing
specialization or an unknown argument rejects. Imported Part Templates currently
admit scalar/enum parameters; bind Record/type arguments in their defining module
or a typed Subassembly. The compiler rejects that unsupported boundary explicitly
rather than capturing a same-named private Record. Assembly and Scene Template imports
remain outside this bounded extension; export concrete products of those kinds.
Caller-defined type arguments also require specialization in the defining module.

`with` derives immutable Record values. It does not edit a module or mutate an
occurrence. A keyed `Set<CylinderConfig>` can contain `standardCylinder with { ... }`
entries and feed one Assembly Pattern. See [the V8 source](../../../fixtures/Canonical/EditableV8/engine.firmasm)
and its [single Cylinder definition](../../../fixtures/Canonical/EditableV8/assemblies/cylinder.firmament).

`include "parts/bolt";` remains admitted, including the established explicit
`.firmament` spelling. Includes share declarations in the consuming module. The
existing snapshot-only project-root-relative include spelling remains accepted
when a document-relative target is absent. Inventory always uses document-relative
locators. New code should use that rule for both forms.

The graph retains source files, dependency hashes, selected exports and Inventory
binding receipts in Assembly IR and the compiled Scene source model. Syntax, resource and Inventory lookup errors
retain original spans. Existing engineering lowerers still have bounded source
adapters; when they cannot provide a precise expression location, errors identify
the enclosing exported Part and disclose that limitation. This pass does not
claim complete generated-source provenance across every domain lowerer.

Unused Inventory entries are resolved and kind-checked; engineering geometry is
constructed for placed occurrences. Inventory availability does not imply an
occurrence count or a bill of materials.

## Typed resource metadata

Use ordinary typed Records, not another metadata dictionary syntax:

```firmament
Record VendorInfo { manufacturer: String partNumber: String revision: String }
Static BearingInfo: VendorInfo {
  manufacturer: "Example supplier"
  partNumber: "BRG-01"
  revision: "B"
}
Inventory Record Components {
  bearing: Part = Step("vendor/bearing", metadata: BearingInfo)
}
```

`metadata:` also works on `Firmament(...)` and typed short includes. The normal
Record binder checks the selected Static value. The binding receipt exposes its
type and flattened fields in Assembly IR. Resource metadata is descriptive; it
does not infer fits, manufacturing tolerances, materials or PMI, and it is not
automatically translated into vendor-specific AP242 properties. Two aliases for
the same imported Part retain one geometry definition even with different receipts.

Assembly `provenance: Release;` selects a typed release Record for the existing
AP242 header and product annotations. Canonical fields are `author: String`
(or a nested Record with `name: String`), `date: Date`, `version: Version`, and
`description: String`; `organization: String` is optional. Nested values refer to
other typed Static Records. Additional typed fields remain inspectable. Legacy
PascalCase fields remain accepted; providing both aliases rejects. Authors and
approval states must be supplied truthfully; the compiler invents neither.

## Optional dependency lock

Run `aetheris lock main.firmasm` to generate `firmament.lock.toml` beside the root.
Commit it with a reproducible project. Normal builds work without one; if a lock
exists, both validation and compilation enforce it. `aetheris validate main.firmasm
--locked` and `aetheris build main.firmasm --locked` additionally require its presence.
Compilation never rewrites the lock. Use `aetheris lock main.firmasm --update` for
an intentional dependency refresh.

Version 1 receipts contain the root identity and sorted dependency entries with
`path`, `format`, `hashKind` and `sha256`. STEP hashes cover exact file bytes;
Firmament hashes cover decoded UTF-8 source text. Snapshot STEP text is represented
by its UTF-8 bytes, so byte-identical receipts require identical encoding/BOM and
line endings. Paths use the loader's allowed source root: the Aetheris repository
root for repository files, otherwise the root document's directory; snapshot paths
are relative to the supplied project. A receipt belongs to one compilation root.
Unknown TOML fields, missing entries, changed dependencies and a different root
reject. Unused old entries are tolerated, and a lock command refresh removes them.

A changed STEP with bound `Recognize`/`Replace`/`Remove` intent refuses even an
explicit update. Reinspect and regenerate its selections in a new reviewed
revision/project receipt. Refreshing a hash cannot establish that a face still
has the same engineering meaning. Locks establish identity, not geometric quality
or permission to discard PMI. This version uses one conventional sidecar name;
custom lock locations and cross-root lock workspaces are outside this pass.

## InlineStep: local annotation and surgery

For mixed products, use Part/Assembly imports. Use `InlineStep` when a Model needs
to annotate or revise one imported body. It can select an existing STEP Part binding:

```firmament
Inventory Record Components { housing: Part = Step("vendor/housing", geometryOnly: true) }
Model Revision {
  units: mm
  InlineStep Source {
    from: Components.housing
    geometryOnly: true
    importTolerance: 0.01mm
  }
}
```

This fragment requires a matching generated lock. `from` and `path` are mutually
exclusive; `from` admits a STEP Part reference, not a native Firmament Part or
STEP Assembly. Direct `path: "vendor/housing.step"` remains supported. Explicit
`sha256` and `textSha256` remain compatible for self-contained pinned source;
the lock can supply the pin instead. Import tolerance is a numerical budget,
separate from authored dimensions. A locked, qualified body can be re-exported
unchanged without pretending to perform a replacement. Surgery still uses the
bounded [Recognize/Replace contracts](imported-feature-recognition.md).
