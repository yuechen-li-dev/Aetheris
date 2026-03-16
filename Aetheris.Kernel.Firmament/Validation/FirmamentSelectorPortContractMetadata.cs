namespace Aetheris.Kernel.Firmament.Validation;

internal enum FirmamentSelectorResultKind
{
    Face,
    FaceSet,
    EdgeSet,
    VertexSet
}

internal enum FirmamentSelectorCardinality
{
    One,
    Many
}

internal sealed record FirmamentSelectorPortContract(
    FirmamentSelectorResultKind ResultKind,
    FirmamentSelectorCardinality Cardinality);

