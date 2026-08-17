# Firmament V2 overview

Firmament is a semantic engineering authoring language. It expresses named bodies, dimensions and units, manufacturable features, reusable typed Templates, engineering requirements, Sheet Metal intent, and analyses. Aetheris is the compiler, kernel, and runtime that validates that intent and lowers supported constructions into artifacts such as STEP AP242, flat STEP, SVG, and FEA results.

A Template is compile-time typed engineering reuse. It accepts values or Records, checks its requirements, specializes deterministically, and then uses the ordinary Firmament lowering path. It is not a text macro or a hidden C# geometry generator.

Units are part of values and types. Lengths such as `8mm`, forces such as `500N`, and angles such as `90deg` are checked by the binder. Static Records, arrays, Tables, `with` derivation, and patterns organize finite engineering data and are erased after specialization.

Named semantic features survive beyond geometry construction. Supported PMI is emitted as AP242 product-definition semantics; feature and topology correspondence is available to inspection consumers such as Cadmata. Presentation orientation is not authoritative engineering state.

Canonical authoring is Firmament V2. V1 and JSON-shaped `.firmasm` exist only as explicitly identified compatibility inputs. See [syntax](syntax.md), [targets](../reference/targets.md), and the [support matrix](../reference/supported-features.md).
