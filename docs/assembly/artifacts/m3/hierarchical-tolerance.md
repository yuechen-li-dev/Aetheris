# Hierarchical tolerance proof

Public edge `MountToDriveOffset`:

- nominal: 45.00 mm
- interval: 44.92–45.10 mm
- definition: `BearingModule<Spec:StandardModuleSpec>`

Structured expansion:

| contributor | nominal | lower | upper |
| --- | ---: | ---: | ---: |
| Housing seat | 10 | -0.01 | +0.02 |
| Bearing width / BlockStandards | 20 | -0.03 | +0.03 |
| Spacer width / StandardModuleSpec with | 15 | -0.04 | +0.05 |

Parent assertion `MachineOffset` traverses `FrameToLeft` and the one public
module edge. Its required minimum is 44.90 mm; worst-case minimum is 44.92 mm,
so the assertion passes. The public edge retains `static-table`,
`static-record`, `static-with`, Template specialization, occurrence, and signed
internal-relation evidence.
