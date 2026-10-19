# Convex stock and exact ellipsoid regression specimens

The five `polyhedron-N` sources declare regular convex stock with N faces and
20mm edges. Their `-rounded` partners apply a 1mm whole-stock fillet. `box-rounded`
is a 20mm cube rounded with a 2mm radius. `ellipsoid` has 18/12/25mm semiaxes.

The Core and CLI ConvexStock tests cover actual topology, exact STEP export and
reimport, full face display, mesh closure/orientation, and validate/build agreement.
Invalid mutations are tested separately without rewriting these accepted sources.

`AllEdges` applies only to untouched Box and the five regular Polyhedron stocks.
It does not admit arbitrary-body blending or silently ignore other operations.
