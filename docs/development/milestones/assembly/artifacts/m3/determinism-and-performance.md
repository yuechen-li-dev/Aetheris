# Determinism and performance

Two complete compilations preserve:

- AssemblyDefinition ID `assembly-definition:232D83A6D04B59D4`
- specialization identity `BearingModule<Spec:StandardModuleSpec>`
- deterministic geometry hash
  `090EC5DD2BA098CE3AAF4138260A03E3EC988F5B9FAFED7F71DDA60C90834E26`
- local/world transforms
- public relation interval and three expanded contributors
- AP242 hierarchy and shared definition references

The evidence run measured the single local solve at approximately 29 ms. Both
occurrences reuse that artifact; occurrence work is transform composition.
Cadmata publishes `packetMilliseconds`, definition/module counts, and occurrence
count in its performance map. The exact timing is environment telemetry and is
not included in deterministic hashes.
