# CTC-03 source independence

`../m2/ctc03-idiomatic.firmament` contains no `EvidenceSource`, `FromEvidence`, source path, face ID, edge ID, or recovered polygon. The M3 test copies it to an isolated temporary directory and compiles there. Generation succeeds with 15 regions, seven bends, two cuts, a closed formed body, and a valid flat pattern.

The generated part reports empty source-face bindings for every region and `sole construction authority` as its source authority. The imported NIST STEP enters only the subsequent `sheetmetal compare` invocation.
