# Third-party notices and data provenance

Aetheris source code is licensed separately under the GNU Affero General
Public License v3.0 (`AGPL-3.0`). The assets below retain their respective
licenses, terms, and provenance and are not relicensed as Aetheris source code.

## Antonia Polygon original base figure

The HUMANOID-X1 adoption candidate is based on **Antonia Polygon** by
**Olaf Delgado-Friedrichs**, with contributions from phantom3D, MikeJ and
others credited in the original package. The original figure is licensed under
[Creative Commons Attribution 3.0 Unported](https://creativecommons.org/licenses/by/3.0/).
Source: [Antonia 1.2.0](https://github.com/odf/Antonia.Polygon/tree/08c9767691daad1382dfc6980ee83e31514b4879).
The original notice is preserved without modification at
`fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt`; exact input hashes
and component scope are in the adjacent `antonia-original.sidecar.json`.

Aetheris modifies/adapts selected original body and eye geometry by converting
the coordinate frame, freezing binding triangles, assigning stable identities
and provisional semantic regions, constructing joint proxies and skin weights,
and testing a neutral A-pose conversion. Original vertex/polygon mappings are
retained. **No endorsement is implied**, and Aetheris makes no exclusive
authorship claim over the adopted topology. These derived geometric assets
retain their source license and attribution; they are not relicensed as AGPL code.

Source and expanded derived geometry are generated under ignored
`artifacts/local/humanoid-x1/`; this repository tracks the original notice,
admission manifest, reproducible implementation and compact evidence. The
candidate has **not** been promoted to the canonical adult runtime. No
CharMorph/Blender conversion, source rig/weights, textures, morph arrays,
shared libraries, or separately distributed extras are consumed by this path.

## Stanford Bunny

The reconstructed Bunny evidence under
`docs/development/milestones/geometry/artifacts/bunny-m4/` derives from the Stanford Bunny
`bun_zipper.ply`, credited to the Stanford University Computer Graphics
Laboratory. The repository's existing provenance record states that Stanford
permits free mirroring and redistribution with acknowledgment and that
commercial use requires Stanford's permission. The dataset terms and original
model are linked from the preserved provenance record:
`docs/development/milestones/geometry/artifacts/bunny-m4/README.md`.

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

Drawing Notes uses the MIT-licensed `PDFtoImage` .NET package, which wraps the
BSD-licensed PDFium renderer and MIT-licensed SkiaSharp graphics library. Their
binary packages and license metadata are restored from NuGet and retain their
authors' terms.

NuGet dependencies and JavaScript dependencies resolved by TSPack are restored
from their respective package feeds and retain the licenses and notices
supplied by their authors. This file does not attempt to replace the dependency
metadata carried by those ecosystems.
