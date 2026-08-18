# Template/Record/Mate tolerance transition evidence

`TemplateFitTransition` in `fixtures/AssemblyM1/template-block-pair.firmament` crosses the dimensional edge generated automatically by `SeatedAxis.Fit`.

- from: `TemplateBlockPair.Moving.Interface.Height` (`MovingSpec`, Template specialization `template:88509cacec6ff417`)
- to: `TemplateBlockPair.Fixed.Interface.Height` (`FixedSpec`, Template specialization `template:0a30ccff1bd91583`)
- Mate: `mate:TemplateBlockPair:Seat`
- Interface: `interface:SeatedAxis`
- nominal/worst case: `-5mm`
- requirement: `Clearance >= -5mm`
- result: passed

The `DimensionalRelationIr.SourceProvenance` and resulting `StackupContributionIr.SourceProvenance` retain both `static-record` and `template-specialization` entries. M0's separate six-edge bearing stack still proves asymmetric tolerances and a 3+ part chain.

