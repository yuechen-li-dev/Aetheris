# Typed Record parameters for Firmament Templates (M2)

## Gap audit

Before M2, `Template <...> Struct ...` value parameters admitted only `Length`,
numeric/bool scalars, and enums. Binding was scalar text substitution. The older
canonical static lane understood `Record` declarations and `Static T[]` values,
but erased them before modern Template specialization. It had no scalar
`Static Name: T = T { ... }` IR. Consequently `Spec: HexBoltSpec` was treated as
an unknown value type, `Spec.HeadHeight` had no typed authority, and Require
could not evaluate record members.

M2 makes modern Template specialization the owner of typed Record binding. It
runs while Record and Static declarations are present; canonical static erasure
then removes them before Feature AIR. This is the same Template system, not a
parallel runtime or macro lane.

## Authoring pattern

```firmament
Record WidgetSpec {
    Width: Length
    Height: Length
}

Static TallWidget: WidgetSpec = WidgetSpec {
    Width: 40mm
    Height: 25mm
}

Template < Spec: WidgetSpec >
Struct Widget: WidgetConcept {
    Require Positive => Spec.Width > 0mm && Spec.Height > 0mm
    // ordinary declarations using Spec.Width and Spec.Height
}

Struct Example = Widget < Spec: TallWidget >
```

The binder resolves the declared Record type and scalar Static value, requires
exact Record-type identity, validates all fields, resolves member expressions,
and evaluates Require after member resolution. Direct record-literal arguments
remain unsupported in M2; use a named Static value so provenance is stable.

`ConceptIrTemplateInstantiation.RecordArguments` retains parameter name, Record
type, Static value name, member values, source span, and provenance.
`RequireResults` records passed/failed expression evidence. These records are
source-map evidence only: Template, Static, Record values, Match, and Require are
erased before Feature AIR.

## Diagnostics

M2 adds bounded typed diagnostics for unknown Record parameter types, unknown
Static values, wrong Static Record types, collection-to-scalar mismatches,
materialized values passed as compile-time arguments, unsupported value forms,
unknown members, field type mismatches, and Require failure after member
resolution. Existing parameter/default/constraint diagnostics are unchanged.

The bolt-independent regression fixture in
`FirmamentV2TemplateExpansionTests` proves binding, member access, Require,
deterministic specialization, provenance, and rejection behavior.

