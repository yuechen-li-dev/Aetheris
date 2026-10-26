# IntelliDraft: Arc2, Sketch merged in, readable output

## What changed

- **Arc2** is a construction guide: `Arc2 a { center: c; from: p; to: q }`, the
  counter-clockwise arc from `p` to `q` around `c`. `q` off the circle through `p`
  is rejected (`profile-layout-arc-radius-mismatch`). Named points on an arc split
  it into construction spans, so arcs trace by clicking like lines. IntelliDraft's
  `arc` operation writes it; Helios offers it under the circle tool's type menu.
- **Profile editing from the retired Sketch workspace** now goes through IntelliDraft:
  the sketch projection's Profile shapes (code-authored parts) are listed and edited
  with `dimensions` requests, a Profile menu picks among several, and the circular
  opening is available as before. Helios no longer ships the Sketch workspace.
- **Readable source**: new layouts are written with an open multi-line body, so later
  construction lands one declaration per line, and click-traced Profiles put one span
  per line. The language edit service is unchanged.
- **Web runtime tree** uses declared names (`Model Bracket`, `Extrude body`) instead of
  the file stem; the file stem remains the fallback.

## Evidence

- `FirmamentIntelliDraftTests`: Arc2 projection, spans, rejection, trace and build;
  existing tests updated to compare traces whitespace-insensitively.
- `aetheris validate` accepts a hand-written Arc2 + Line2 D profile.
- Helios: IntelliDraft, graph-authoring, editor-presentation and author e2e pass.

## Verdict

Success. Arc2 replaces the Circle2-plus-endpoints arc tool tried earlier; that guide
form still works for code that uses it.
