using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

public enum SyntaxTokenKind { Identifier, Number, String, Punctuation, Whitespace, Comment, End }
public sealed record SyntaxToken(SyntaxTokenKind Kind, string Text, FirmamentV2SourceSpan Span);
public enum DirectiveKind { Schema, Include, Use }
public enum ClauseKind { On, Using, Over, From, Key }
public enum LiteralKind { Number, String, Boolean, Version, EntityReference }
public enum SequenceKind { List, Tuple }
public enum UnaryOperator { Positive, Negative, Not, Reverse }
public enum BinaryOperator { Pipeline, Direction, Or, And, Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, Add, Subtract, Multiply, Divide, Remainder, Power }

// This is source syntax, not engineering AIR. Open type names and fields are retained
// for their semantic owners to admit; structural success is never called validity.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "node")]
[JsonDerivedType(typeof(DeclarationSyntax), "declaration")]
[JsonDerivedType(typeof(FieldSyntax), "field")]
[JsonDerivedType(typeof(DirectiveSyntax), "directive")]
[JsonDerivedType(typeof(ExpressionStatementSyntax), "expressionStatement")]
[JsonDerivedType(typeof(ReturnSyntax), "return")]
[JsonDerivedType(typeof(MappingSyntax), "mapping")]
[JsonDerivedType(typeof(StatementBlockSyntax), "statementBlock")]
[JsonDerivedType(typeof(RoleRequirementSyntax), "roleRequirement")]
[JsonDerivedType(typeof(InterfaceRelationSyntax), "interfaceRelation")]
[JsonDerivedType(typeof(InterfaceFitSyntax), "interfaceFit")]
[JsonDerivedType(typeof(InterfaceMotionSyntax), "interfaceMotion")]
[JsonDerivedType(typeof(InterfacePolicySyntax), "interfacePolicy")]
[JsonDerivedType(typeof(ReplacementSyntax), "replacement")]
public abstract record StatementSyntax(FirmamentV2SourceSpan Span);
public sealed record IdentifierSyntax(string Text, FirmamentV2SourceSpan Span);
public sealed record ArgumentSyntax(IdentifierSyntax? Name, ExpressionSyntax Value, ExpressionSyntax? Default, FirmamentV2SourceSpan Span)
{
    // Retain the authored value boundary when a binder replaces Value. Transport
    // serializers can preserve identity-bearing application spelling without
    // interpreting the source or guessing the extent of an expression.
    [JsonIgnore] public FirmamentV2SourceSpan AuthoredValueSpan { get; init; } = Value.Span;
}
public sealed record TypeSyntax(IdentifierSyntax Name, IReadOnlyList<ArgumentSyntax> Arguments, FirmamentV2SourceSpan Span);
public sealed record ClauseSyntax(ClauseKind Kind, ExpressionSyntax Value, FirmamentV2SourceSpan Span);
public sealed record BlockSyntax(IReadOnlyList<StatementSyntax> Statements, FirmamentV2SourceSpan Span)
{
    [JsonIgnore] public bool Delimited { get; init; } = true;
    [JsonIgnore] public bool IsClosed { get; init; } = true;
    [JsonIgnore] public FirmamentV2SourceSpan ContentSpan => Delimited ? new(Span.Start + 1, Math.Max(0, Span.Length - (IsClosed ? 2 : 1))) : Span;
}
public sealed record DeclarationSyntax(TypeSyntax Type, IdentifierSyntax? Name, IReadOnlyList<TypeSyntax> Modifiers,
    IReadOnlyList<ArgumentSyntax> Parameters, ExpressionSyntax? Annotation, ExpressionSyntax? ResultType,
    IReadOnlyList<ClauseSyntax> Clauses, ExpressionSyntax? Initializer, BlockSyntax? Body, bool Occurrence,
    FirmamentV2SourceSpan Span) : StatementSyntax(Span)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpressionSyntax? Target { get; init; }
    [JsonIgnore] public bool HasParameterList { get; init; }
}
public sealed record FieldSyntax(IdentifierSyntax Name, ExpressionSyntax Value, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record DirectiveSyntax(DirectiveKind Kind, ExpressionSyntax Value, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record ExpressionStatementSyntax(ExpressionSyntax Value, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record ReturnSyntax(ExpressionSyntax Value, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record StatementBlockSyntax(BlockSyntax Body, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record MappingSyntax(IdentifierSyntax Key, StatementSyntax Value, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record RoleRequirementSyntax(IdentifierSyntax Name, IReadOnlyList<IdentifierSyntax> Capabilities, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record InterfaceRelationSyntax(IdentifierSyntax Kind, ExpressionSyntax First, ExpressionSyntax Second,
    ExpressionSyntax? Option, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record InterfaceFitSyntax(ExpressionSyntax Shaft, ExpressionSyntax Bore, bool PerSide, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record InterfaceMotionSyntax(IdentifierSyntax Kind, IdentifierSyntax Axis, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record InterfacePolicySyntax(IdentifierSyntax Kind, IReadOnlyList<ArgumentSyntax> Values, FirmamentV2SourceSpan Span) : StatementSyntax(Span);
public sealed record ReplacementSyntax(ExpressionSyntax Target, DeclarationSyntax Replacement, FirmamentV2SourceSpan Span) : StatementSyntax(Span);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "node")]
[JsonDerivedType(typeof(ReferenceSyntax), "reference")]
[JsonDerivedType(typeof(LiteralSyntax), "literal")]
[JsonDerivedType(typeof(UnarySyntax), "unary")]
[JsonDerivedType(typeof(BinarySyntax), "binary")]
[JsonDerivedType(typeof(SequenceSyntax), "sequence")]
[JsonDerivedType(typeof(MemberSyntax), "member")]
[JsonDerivedType(typeof(IndexSyntax), "index")]
[JsonDerivedType(typeof(CallSyntax), "call")]
[JsonDerivedType(typeof(SpecializationSyntax), "specialization")]
[JsonDerivedType(typeof(ObjectSyntax), "object")]
[JsonDerivedType(typeof(WithSyntax), "with")]
[JsonDerivedType(typeof(QuantitySyntax), "quantity")]
[JsonDerivedType(typeof(FrameSyntax), "frame")]
[JsonDerivedType(typeof(AliasSyntax), "alias")]
[JsonDerivedType(typeof(TraceSyntax), "trace")]
[JsonDerivedType(typeof(MemberSetSyntax), "memberSet")]
[JsonDerivedType(typeof(TypedValueSyntax), "typedValue")]
[JsonDerivedType(typeof(ProvenanceSyntax), "provenance")]
[JsonDerivedType(typeof(TypeParameterSyntax), "typeParameter")]
[JsonDerivedType(typeof(MatchSyntax), "match")]
[JsonDerivedType(typeof(ToleranceSyntax), "tolerance")]
public abstract record ExpressionSyntax(FirmamentV2SourceSpan Span);
public sealed record ReferenceSyntax(IdentifierSyntax Name, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record LiteralSyntax(LiteralKind Kind, string Raw, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record ProvenanceSyntax(ExpressionSyntax Value, LiteralSyntax Source, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record TypeParameterSyntax(IdentifierSyntax Name, ExpressionSyntax? Constraint, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record QuantitySyntax(ExpressionSyntax Value, IdentifierSyntax Unit, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record UnarySyntax(UnaryOperator Operator, ExpressionSyntax Operand, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record BinarySyntax(ExpressionSyntax Left, BinaryOperator Operator, ExpressionSyntax Right, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record SequenceSyntax(SequenceKind Kind, IReadOnlyList<ArgumentSyntax> Items, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record MemberSyntax(ExpressionSyntax Target, IdentifierSyntax Member, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record IndexSyntax(ExpressionSyntax Target, ExpressionSyntax? Index, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record CallSyntax(ExpressionSyntax Target, IReadOnlyList<ArgumentSyntax> Arguments, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record SpecializationSyntax(ExpressionSyntax Target, IReadOnlyList<ArgumentSyntax> Arguments, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record ObjectSyntax(ExpressionSyntax? Type, BlockSyntax Body, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IdentifierSyntax? Name { get; init; }
}
public sealed record WithSyntax(ExpressionSyntax Target, BlockSyntax Body, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record FrameSyntax(ExpressionSyntax Origin, IReadOnlyList<ArgumentSyntax> Axes, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record AliasSyntax(ExpressionSyntax Value, IdentifierSyntax Name, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record TraceSyntax(ExpressionSyntax Source, ExpressionSyntax? From, ExpressionSyntax? To, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record MemberSetSyntax(ExpressionSyntax Target, IReadOnlyList<ArgumentSyntax> Members, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record TypedValueSyntax(ExpressionSyntax Type, ExpressionSyntax Value, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record MatchArmSyntax(ExpressionSyntax Pattern, ExpressionSyntax Value, FirmamentV2SourceSpan Span);
public sealed record MatchSyntax(ExpressionSyntax Scrutinee, IReadOnlyList<MatchArmSyntax> Arms, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
/// <summary>Compile-time dimensional requirement; it never changes nominal geometry.</summary>
public sealed record ToleranceSyntax(ExpressionSyntax Nominal, ExpressionSyntax Plus, ExpressionSyntax? Minus,
    FirmamentV2SourceSpan ToleranceSpan, FirmamentV2SourceSpan Span) : ExpressionSyntax(Span);
public sealed record DocumentSyntax(string SourceFile, IReadOnlyList<StatementSyntax> Statements,
    IReadOnlyList<SyntaxToken> Tokens, FirmamentV2SourceSpan Span);
public sealed record SyntaxParseResult(DocumentSyntax Document, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.All(d => d.Severity is not ("fatal" or "error"));
    public string ValidationLevel => "StructuralOnlyUnbound";
}
public sealed record SyntaxExpressionParseResult(ExpressionSyntax? Expression, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);
