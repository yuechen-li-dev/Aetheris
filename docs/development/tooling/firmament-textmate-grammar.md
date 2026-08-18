# Firmament TextMate grammar

The Preview 1 VS Code grammar lives at `tools/vscode-firmament/syntaxes/firmament.tmLanguage.json`. Its keyword, type, generic-variant, literal, field-position, and member categories are the canonical editor-highlighting vocabulary. The public `FirmamentCode` component should retain equivalent categories; changes to either highlighter should update the other and their representative fixtures together.

This grammar is intentionally lexical. It may seed documentation highlighting and future editor integrations, but it is not the compiler grammar, a parser, or semantic truth. The Aetheris CLI remains authoritative for diagnostics and supported geometry.
