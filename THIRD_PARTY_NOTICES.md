# Third-party notices and data provenance

Aetheris source code is licensed separately under the GNU Affero General
Public License v3.0 (`AGPL-3.0`). The assets below retain their respective
licenses, terms, and provenance and are not relicensed as Aetheris source code.

## Stanford Bunny

The reconstructed Bunny evidence under
`docs/geometry/artifacts/bunny-m4/` derives from the Stanford Bunny
`bun_zipper.ply`, credited to the Stanford University Computer Graphics
Laboratory. The repository's existing provenance record states that Stanford
permits free mirroring and redistribution with acknowledgment and that
commercial use requires Stanford's permission. The dataset terms and original
model are linked from the preserved provenance record:
`docs/geometry/artifacts/bunny-m4/README.md`.

The original Stanford Bunny archive/model is not bundled in this repository;
the M4 directory contains Aetheris-generated derived evidence. Those derived
assets remain subject to the recorded Stanford dataset terms.

## NIST STEP AP242 test models

The STEP files under `testdata/step242/nist/` retain their original filenames
and identify themselves in their STEP headers as originating from the NIST MBE
PMI Validation and Conformance Testing Project. One preserved copy is also
distributed with the PMI injection demo at
`demos/Aetheris.PmiInjectionDemo/assets/nist_ftc_11_asme1_ap242-e2.stp`; its
path-level provenance is documented in that demo's `README.md`.

These test models are reference/test data made available by the National
Institute of Standards and Technology. NIST does not endorse Aetheris, no NIST
code is represented as part of Aetheris, and the models are not represented as
AGPL-licensed or authored by Aetheris.

## Other dependencies

NuGet dependencies and JavaScript dependencies resolved by TSPack are restored
from their respective package feeds and retain the licenses and notices
supplied by their authors. This file does not attempt to replace the dependency
metadata carried by those ecosystems.
