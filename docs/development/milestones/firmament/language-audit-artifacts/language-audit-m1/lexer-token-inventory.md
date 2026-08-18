# Lexer/token inventory

Firmament V2 has no standalone lexer/token enum. `FirmamentV2Parser` and its cooperating binders use anchored regular-expression productions. The user-facing spelling inventory is therefore:

- roots/declarations: `Model`, `Units`, `Concept`, `Struct`, `Concept Struct`, `Enum`, `Record`, `Static`, `Table`, `Key`, `let`, `Template`, `Require`, `Match`, `Pattern`;
- semantic/construction: `Expose`, `Concept Path`, `Start`, `Heading`, `Line`, `Arc`, `Close`, `Profile`, `Compose`, `Selection`, `Modify`, `Hole`, `Slot`, `Pattern`, `EdgeFinish`, `InlineStep`, `Recognize`, `Replace`, `PMI`, `Analysis`;
- assembly: `Semantic`, `Point`, `Axis`, `Plane`, `Dimension`, `Interface`, `Role`, `requires`, `Lower`, `Fit`, `inside`, `Allow`, `Assembly`, `Part`, `Anchor`, `Mate`, `Relation`, `Assert`, `ToleranceStackup`, `Between`, `Clearance`;
- operators/punctuation: `{ } [ ] ( ) < > : ; , . = -> => + - * / == != < <= > >=`, member access `.`, comments `//`;
- literal/unit words: `mm`, `deg`, `true`, `false`, `tol`, `PlusMinus`.

Parser consumers are mapped in `language-features.json`. `satisfies` is consumed by Template type parameters; lowercase `requires` only by Role declarations. Keywords found only in legacy V1 parser/fixture lanes are not included as V2-supported tokens.

