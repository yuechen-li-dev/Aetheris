# Piping field spelling compatibility

These eight sources preserve the pre-migration canonical files byte-for-byte
from revision `9de80654`. Current cookbook versions live under
`fixtures/Canonical/Piping/` and use camelCase fields.

`PipingAstAdmissionTests` compiles each original/current pair through the same
production owner and checks exact STEP, ports, equipment identities, routing,
BOM, cut lists and bounds. The normal CLI qualification also compares the new
canonical artifacts against outputs captured before the owner migration.

`tee.firmament` is a catalogue-reference witness with an explicit straight
route. Its catalogue aliases do not request a three-way fitting.
