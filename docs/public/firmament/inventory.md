# Inventory Records and module references

Use `include` for a short composition whose declarations can share names in one
module. Use an `Inventory Record` when named, qualified dependencies make a larger
Assembly or Scene easier to organize. Neither form has a one- or two-part limit.

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
}
```

This is a declaration fragment: provide the named files and exports in your project.
`Firmament(...)` selects a named root declaration; `Step(...)` supplies one exact
Part through the existing STEP importer. Missing or ambiguous exports and kind
mismatches reject. STEP assemblies and multi-body assets must satisfy the existing
single-Part importer contract; there is no implicit first-body selection.

The normal spelling omits the extension. `Firmament("parts/bracket")` resolves
`.firmament` or `.firmasm`; `Step("vendor/bearing")` resolves `.step` or `.stp`.
Exactly one candidate must exist. An explicit extension is also accepted and is
the repair when both candidates exist. No search path, directory scan or global
package registry chooses a resource behind the author's back.

Locators resolve relative to the document containing the declaration and must
stay inside the existing allowed source root. Source snapshots resolve from their
provided document map, without reading files from disk. Snapshots currently hold
Firmament source only; a STEP Inventory entry requires a file-backed project.

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
