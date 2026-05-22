# OCCT nut-bolt extracted assembly fixtures (ASM-A1)

Source assembly: `testdata/step242/OCCT/nut-bolt-assembly.step`.

- `_bolt_extract.step`: transitive-closure extraction of `#37 = MANIFOLD_SOLID_BREP` from the source assembly.
- `_nut_extract.step`: transitive-closure extraction of `#316 = MANIFOLD_SOLID_BREP` from the source assembly.
- `bolt.step`: canonicalized via `aetheris canon` from `_bolt_extract.step`.
- `nut-bolt-assembly.firmasm`: bounded `.firmasm` contract fixture for the two extracted rigid STEP parts.
- `bolt-only.firmasm`: load-success fixture proving the canonicalized extracted bolt STEP seam.

Current bounded blocker:
- `_nut_extract.step` fails `Step242Importer.ImportBody` with loop-role normalization (`crosses_outer_boundary_with_all_vertices_inside`) and therefore cannot yet be canonicalized to `nut.step` through the current real import/export seam.
