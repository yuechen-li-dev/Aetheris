# Forge proof

The SecretGeometry capability emits exact prism BRep plus semantic body members
`TopFace` and `LoadRegion`. Each member has exact face binding and Forge
capability/version provenance. ForgeHost exposes a kernel-free descriptor.

`ForgeSemanticMemberUsesOrdinaryPathSelectionAndFeaConsumers` resolves
`TopFace` with `SemanticPathSegment`, then passes that same reference to ordinary
Selection and FEA normalization. No sample-specific consumer branch exists.
The host artifact hash is
`7282965B703FEB85478AC8B651B6F6255D3BC922D3B2A7EBE73726C611CB4D36`.
