# Local solve and definition reuse

Canonical source: `fixtures/Regression/Assembly/bearing-module-family-with-legacy-placement.firmament`.

- specialization: `BearingModule<Spec:StandardModuleSpec>`
- AssemblyDefinition ID: `assembly-definition:232D83A6D04B59D4`
- local product occurrences: definition root plus Housing, Bearing, Spacer, Shaft
- local Mate: `ShaftSeat`, valid
- local assertion: `InternalOffset`, pass
- definition local solve: approximately 29 ms on the evidence run
- parent occurrences: `Machine.LeftModule`, `Machine.RightModule`
- compiled AssemblyDefinition count: 1
- occurrence count for that definition: 2

The specialization cache is keyed by normalized Template application. The
second occurrence clones the solved local tree and public surface; it does not
invoke the local compiler again.
