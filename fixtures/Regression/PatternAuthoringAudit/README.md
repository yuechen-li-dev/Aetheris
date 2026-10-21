# Pattern authoring audit specimens

These preserve the original audit and repair witnesses. The table below records
**audit-time** behavior, not the repaired compiler. Rejected bug reproducers were
not invalid engineering intent. The original admitted geometry defect is now
covered by a required geometry regression.

The report is `docs/release/PATTERN-AUTHORING-AUDIT-2026-10-20.md`.
`expectations.json` records the independent expected fret coordinates for the
critical Function-parameter shadowing pair. Both must produce local [0,-314]mm
and world [0,344,62]mm. Before repair, the same-named Static specimen produced
[0,-50]mm and [0,608,62]mm even though its Function call still supplies 628mm.

| Specimen | Audit admission | Original disposition |
| --- | --- | --- |
| `equation-plate-modern-math` | Admitted | Correct accepted control |
| `equation-plate-resized` | Admitted | Correct accepted control |
| `equation-plate` | Admitted | Correct accepted control |
| `function-captures-static` | Rejected | Outside current Function contract |
| `function-comment` | Admitted | Correct accepted control |
| `function-cycle` | Rejected | Expected invalid-input rejection |
| `function-named-arguments` | Rejected | Outside current Function contract |
| `function-parameter-shadow-assembly-control` | Admitted | Correct accepted control |
| `function-parameter-shadow-assembly` | Admitted | Admitted wrong geometry: P0 Function parameter capture |
| `keyed-stations-plate` | Admitted | Correct accepted control |
| `keyed-stations-reordered` | Admitted | Correct accepted control |
| `series-assembly-count-static` | Admitted | Correct accepted control |
| `series-assembly-count` | Admitted | Correct accepted control |
| `series-assembly-pitch-static` | Admitted | Correct accepted control |
| `series-assembly-resized` | Admitted | Correct accepted control |
| `series-assembly` | Admitted | Correct accepted control |
| `series-count-static` | Rejected | Rejected binding defect reproducer |
| `series-count-too-large` | Rejected | Expected invalid-input rejection |
| `series-count-zero` | Rejected | Expected invalid-input rejection |
| `series-empty-set` | Admitted | Correct accepted control |
| `series-module-static` | Rejected | Rejected binding defect reproducer |
| `series-part-count-static-module` | Rejected | Rejected binding defect reproducer |
| `series-pitch-static-unshadowed` | Rejected | Rejected binding defect reproducer |
| `series-pitch-static` | Rejected | Rejected binding defect reproducer |
| `series-point3-assembly` | Admitted | Correct accepted control |
| `series-position-arithmetic` | Admitted | Correct accepted control |
| `series-string-reference` | Admitted | Correct accepted control |
| `series-trigonometry` | Rejected | Outside current Function contract |
| `series-unknown-field` | Rejected | Expected invalid-input rejection |
| `series-wrong-unit` | Rejected | Expected invalid-input rejection |
| `ui-edited-law` | Admitted | Correct accepted control |
| `ui-promoted-count` | Rejected | Rejected binding defect reproducer |

`ui-promoted-count` is actual source saved after Helios Make count a parameter.
`ui-edited-law` is recovered browser source after literal count 3 and a Function
expression edit. Other sources are minimized CLI probes. All 32 agree between
normal validate and build admission in the original audit; admission alone did
not close the admitted geometry defect. Raw JSON, source hashes, STEP and screenshots remain under ignored
`artifacts/local/pattern-authoring-audit/`.


## Repair qualification

Both Function-shadowing specimens must now produce the expected coordinates,
with identical occurrence transforms and leaf geometry. The six rejected binding
witnesses (`series-count-static`, `series-pitch-static`,
`series-pitch-static-unshadowed`, `series-module-static`,
`series-part-count-static-module`, `ui-promoted-count`) must now build correctly.

Additional specimens qualify scoped values and tolerance honesty:

| Specimen | Required result |
| --- | --- |
| `series-record-inputs` | Four original keyed holes using checked Record fields |
| `series-template-inputs` | Same layout after typed Template specialization |
| `ui-created-series` | Actual browser-created second Pattern: five holes total, original law/keys retained |
| `ui-created-stations` | Actual browser-created named Stations Pattern: seven holes total, earlier keys retained |
| `series-owner-frame-rejected` | Reject an unconsumed `on XZ` owner header; never erase it into XY geometry |
| `series-tolerance-input-rejected` | Reject unsupported tolerated point input; never discard its tolerance |

The repaired 38-source corpus expects 28 admitted and ten accurately rejected.
Pure Functions still require positional explicit arguments; implicit Static
capture, named Function arguments and unimplemented trigonometry remain outside
the contract. Sources were not rewritten to hide the original failures.

Durable tests live in `FirmamentPatternBindingTests`, `ConceptPointAuthoringTests`
and `LinearStationAuthoringTests`. The implementation report is
`docs/release/PATTERN-AUTHORING-IMPLEMENTATION-2026-10-20.md`.
