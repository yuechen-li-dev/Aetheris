# Legacy migration result

Automatic semantic migration was rejected as unsafe: `.firmasm` stores definitions, flat instance IDs, and transforms but contains no evidence from which to infer Interface meaning or Roles. It remains a compatibility-only import lane.

Migration recipe:

1. Convert the flat instance list into the intended nested `<Assembly>`/`<Part>` product hierarchy.
2. Preserve old `id` values as occurrence names where stable.
3. Expose definition semantics (`Axis`, `Plane`, `Point`, toleranced `Dimension`).
4. Replace authored transforms with reusable Interface definitions and Mates.
5. Keep an explicit transform only where no semantic relationship is known, and mark it as a legacy escape hatch in future IR.

Current AP242 export is also bounded: per-instance STEP plus package metadata. True AP242 product structure authoring is the exact missing interchange capability.
