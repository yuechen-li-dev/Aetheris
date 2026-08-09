# Abaqus verification export

`AbaqusInpExporter` consumes the same AnalysisIR as the native solve. It emits deterministic nodes and C3D8 elements, `SOLID` ELSET, semantic NSETs derived from constraint/load identities, material/elastic/section definitions, boundary conditions, loads, a linear static step, and `U`, `RF`, `S`, and `E` output requests.

Native partial Cut cells are never represented as ordinary full C3D8 bricks. The M5 verification lowering emits only cells proven fully occupied by CIR and omits partial cells. This produces a conservative stair-step conventional mesh whose geometry and stiffness differ from the native Cut-cell solve. The deck says so in its header.

The focused validator checks unique node/element IDs, connectivity references, positive brick volumes, and required material/section/boundary/step keywords. It does not replace Abaqus parsing or execution. Abaqus is not installed and no commercial-solver result is claimed.

Run the generated deck in Abaqus/Standard, then compare maximum displacement, reaction equilibrium, and stress near the hole against `native-results.json`. Because the conventional deck omits Cut cells, refine its lattice before treating differences as mechanics-backend discrepancies.
