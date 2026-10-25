# Native bundle qualification

The Linux x64 and macOS arm64 distributions reuse the qualified 2.0.0 viewer
assets and build the tagged product source. Packaging provenance is recorded
separately. Existing Windows and NuGet binaries are preserved.

## Apple Silicon corpus classification

The first native full suite found two serialized NIST snapshot differences;
all other functional suites passed. Native interchange probe
[40207396231](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40207396231)
exports FTC-09 and STC-09 using the unchanged tagged CLI. Disabling hardware
intrinsics produces the same Apple Silicon hashes, so disabling SIMD is not a
repair.

Independent Windows/macOS comparison found:

| Witness | Spatial vertices, edges and surface definitions | Largest recovered UV coordinate difference |
| --- | --- | ---: |
| FTC-09 | Exact identity-independent graph match | 1.5e-12 |
| STC-09 | Exact identity-independent graph match | 1.11e-11 |

The complete topology/association graph agrees at 1e-9 numeric resolution,
including strings and multiplicities. FTC-09 shares one additional immutable
point on macOS, changing subsequent serialized entity numbers. STC-09 differs
in one UV coordinate and matches the already qualified Linux hash.

The existing platform-specific snapshot owner now also reads two measured
Apple Silicon hashes. Every non-hash assertion remains unchanged. This is not
a production tolerance change, geometry rewrite, or permission to accept an
arbitrary new hash. Future changes must still match the strict platform baseline.
Raw STEP specimens and numeric comparison receipts are under ignored
`artifacts/local/release-2-final-check/native-corpus-*`.

## Build isolation and gates

Managed CLI/server publication is checked before the separate AOT Forge build,
preventing its intermediate compilation properties from contaminating shared
files. Shared-library byte mismatches remain errors.

Native package smoke checks run before the expensive full qualification suites.
The current qualification harness can differ from the tagged product only in
release tooling, documentation and test baselines; production source differences
reject. Packages still compile the unchanged tagged product, and full native
regressions gate artifact acceptance. Qualification evidence is attached to
[PR 820](https://github.com/yuechen-li-dev/Aetheris/pull/820).
