# Section-stack Mouth seam split X1

`SECTION-STACK-MOUTH-SEAM-SPLIT-X1` admits one blind DrillPoint Hole whose circular Mouth crosses one internal straight section-stack planning seam between two coplanar side faces. A planning seam may divide face ownership. It must not divide the semantic Hole.

The operation extends `SectionStackBlindDrillCavityPlanner`; it does not use a Boolean or a generic face splitter. Construction-owned face mappings prove the two side faces have the same source side, adjacent slab extents, matching planar support, and one shared planned line edge. Other source identities, non-coplanarity, missing/ambiguous shared seams, and tangent contacts are rejected.

The exact line/circle solve orders the two seam parameters and creates exactly two shared vertices. The physical circle is split into two `Circle3` arc edges, with angular trims selected by the slab region containing each arc midpoint. Each affected face receives a replacement outer loop: its old seam use is changed to the retained seam segment, its owned circular arc, and the other retained segment. Thus the seam has no edge through the open Mouth and no planar cap is introduced.

The two exact arcs retain a single semantic `HoleEntryLoop`; inspection selects one MouthLoop and two MouthEdges, while the shaft cylinder, DrillPoint cone, and Tip remain the pre-existing blind-cavity topology. The Mouth is one physical circle even when its exact arc descendants belong to several coplanar planned faces.

Admitted scope is one straight internal seam with exactly two intersections in the existing construction-plane blind-hole lane. Curved/non-coplanar hosts, physical edges, void boundaries, arbitrary arrangements, overlapping holes, and tangent/coincident contacts remain deferred.
