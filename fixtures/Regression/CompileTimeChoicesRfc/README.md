# Compile-time choices regression gate

These preserve the approved RFC requirements and the production regression gate. `cases.json` states the intended acceptance, optional stable
diagnostic code family, and geometry reference for each source. Invalid specimens must
reject even when their problematic arm is not selected.

From the repository root, after a Release CLI build:

```powershell
python fixtures/Regression/CompileTimeChoicesRfc/run.py
```

The runner uses the production CLI for `validate`, `build`, and STEP `analyze`.
It compares imported bounds, body/shell/face/edge/vertex counts and surface
families against explicit reference source. It also records byte equality of
the STEP artifacts as stronger evidence where available. Outputs go to ignored
`artifacts/local/compile-time-choices/`; `--output` can select another directory.
No fixture or reference is updated by running it.

Exit 1 means at least one required behavior is unmet; the qualification gate requires exit 0. In particular, accepting
an invalid specimen is a failure, even if it emits plausible geometry. A rejection
with a required diagnostic missing is also a failure. The assessment retains the historical baseline gaps separately from current implementation evidence. Do not turn the
known gaps into expected successes to obtain a green result.

Code-family checks accept the existing colon-suffixed validation codes. The
results separately record `canonicalDiagnosticCodes`; accepting the family does
not claim that validate/build JSON code normalization is consistent.

`Options` and lowercase `match` are production spellings; `Enum` and `Match` remain compatible. The Feature specimen uses
the already-supported `Hole<Shaft>` return type, and the Concept specimens use
the established Box/Modify/EdgeFinish consumer. The explicit references establish
that these are real engineering consumers rather than speculative kernel tests.

See [the assessment](../../../docs/development/FIRMAMENT-COMPILE-TIME-CHOICES-RFC.md).

The static-choice specimen now includes the required Model units header; its
original expression-binding failure had hidden that separate source defect.
Other original negative sources and their code-family requirements remain intact.
Additional cases cover selected-only evaluation, scopes and nominal identity,
nested matches, private Template values, defaults and locals, Record/with results,
tolerance/PMI parity and keyword/name collisions. Numeric Pattern-source selection
retains its existing bounded compatibility owner; general Int matches reject.
