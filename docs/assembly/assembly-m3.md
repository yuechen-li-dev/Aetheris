# Assembly M3: template-produced assembly definitions

M3 extends the existing `Template` mechanism; it does not add a `Module` or
`Port` keyword. A declaration of the following form creates a reusable local
Assembly definition:

```firmament
Template < Spec: BearingModuleSpec >
Assembly BearingModule {
  <Assembly BearingModule>
    <Part Housing = HousingPart> Semantic Mount { Axis Axis = [0,0,0] -> [0,0,1]; } </Part>
  </Assembly>
  Expose { Semantic Mount = Housing.Mount; }
}
```

An occurrence is then authored as `<Assembly Left = BearingModule<Spec:
StandardSpec>>`. The normalized application is its definition identity, so
identical applications reuse one definition identity while occurrence-scoped
semantic values remain distinct.

Assembly internals are private for external semantic resolution. `Left.Mount`
is valid when exposed; `Left.Housing.Mount` produces
`assembly-internal-member-hidden` and advises exposing an intentional semantic
member. Product-tree visibility remains unaffected.

Each normalized specialization is compiled once into an `AssemblyDefinitionIr`.
That artifact owns the local product tree, Anchor, Mates, placements,
dimensional relations, local assertions, public semantics, and public summary
relations. An internally underconstrained, overconstrained, or failing
specialization is rejected before the parent Assembly is compiled. Parent
occurrences only compose the cached local transforms:

`World(child) = World(AssemblyOccurrence) * Local(child)`.

Public datum bindings are first converted from the implementing child's local
frame into the Assembly definition frame. The occurrence transform then yields
the exact world Axis, Plane, or Point.

An `Expose` block may publish one bounded dimensional summary without exposing
its private edges:

```firmament
Expose {
  Semantic Mount = Housing.Mount;
  Semantic Drive = Shaft.Drive;
  Relation MountToDriveOffset: Mount -> Drive = Housing.Mount -> Shaft.Drive;
}
```

The internal endpoints must have exactly one local relation path. The public
edge stores the accumulated nominal/tolerance interval plus structured signed
contributors. Parent stackups traverse that single edge and may expand it for
diagnostics or Cadmata details.

AP242 emits one reusable product definition for each Template Assembly
specialization and separate occurrences beneath the root. Definition-local
child usages are emitted once and expanded per occurrence on reimport, retaining
the nested logical occurrence tree and exact part geometry.
