# DFM review artifacts

Drawing compilation produces two optional review projections when review data exists:

- an A4 Review PPTX containing the clean drawing plus editable `Review.<id>.Callout`, leader, highlight, author/date, status, and current/proposed values;
- a 16:9 DFM deck with one slide per Issue or Proposal, a large projected view, highlighted semantic target, big arrow, concise engineering text, identity, status, author/date, and current/proposed values.

Callout anchors come from the DrawingIR annotation whose semantic reference matches the review target. Firmament contains no slide coordinates. Placement is bounded to the page content region and deterministic. Production PPTX explicitly excludes all review groups.

The canonical fixture is `fixtures/Canonical/Drawings/bearing-block-production-drawing.firmament`. Its fictional supplier story proposes loosening a bore tolerance to avoid a secondary grinding operation. The proposal is collaboration data only; the authoritative `MountDiameterConstraint` remains unchanged.

Microsoft 365 is not required. The Dominatus audit found a useful conceptual capability-profile/allowlist boundary and fake Outlook workflow, but no live Graph authentication, OneDrive/SharePoint upload, or PowerPoint file transport implementation suitable for direct reuse. M0 therefore adds no cloud dependency. A future adapter should accept already-generated artifacts and expose upload/send operations behind credentials and mocked transport boundaries.
