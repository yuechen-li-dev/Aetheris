# Firmament Tables and immutable Record derivation (M1)

## Audit and decision

Before M1, Firmament had typed `Record` declarations, scalar `Static` record
literals, a limited static array expansion route, member substitution for typed
Template parameters, and erase-before-AIR semantics. It did **not** have a
`with` expression. Static Records were represented during template binding as a
typed immutable field map; arrays were separately represented as lists of those
maps. This M1 extends that binding path rather than adding a runtime data system.

The canonical Table form is:

```firmament
Static Table ISO4017: HexBoltStandardRow Key: Size {
    Size: [M8, M10]
    NominalDiameter: [8mm, 10mm]
    Pitch: [1.25mm, 1.5mm]
}
```

`Table` is a finite, compile-time-only, columnar declaration. Its one row type
must be a `Record`; every Record field has exactly one typed column; columns have
one shared row count (zero is allowed). Table storage remains columnar in the
static IR and inspection document. Indexing creates an ordinary immutable Record
view, not a runtime table value:

```firmament
Static M8 = ISO4017[M8]
Static M8Long = M8 with { Length: 80mm }
```

Keys are optional. When declared, the key field must be `Enum`, `String`, or
`int`, exist as a column, and have unique values. Numeric lookup uses existing
bounded static-index semantics; keyed lookup is exact only.

## `with`

`BaseRecord with { Field: Value }` returns a fresh value with exactly the base
Record type. It is immutable, preserves all omitted fields, composes in source
order, and is erased before Feature AIR. The binder rejects non-Records, unknown
or duplicate fields, incompatible scalar/collection values, wrong Record types,
and materialized values. Static dependency resolution detects unresolved/cyclic
derivations as static-record failures.

Provenance is retained on the bound Record argument: Table name, row, key, base
Record, and sorted overridden fields are carried to `TemplateInstance` and
Cadmata metadata. It is source-map evidence, not an opaque generated literal.

## Diagnostics and limits

M1 diagnoses unknown/non-Record row types, missing/unknown columns, column type
mismatches, unequal lengths, invalid/duplicate keys, lookup type/key/bounds
errors, and all `with` type errors. It deliberately has no filter, join, query,
CSV import, runtime iteration, mutation, or dataframe semantics.

Tables are dependency-sensitive: a specialization identity hashes the resolved
bound Record leaves, so an unrelated row does not change an instance selected
from a different row. Row order and keys are deterministic.
