# Web SDK capability matrix

| Capability | Native | Browser WASM | Notes |
|---|---:|---:|---|
| Firmament V2 compile from memory | Yes | Yes | Same compiler/materializer |
| Solid modeling | Yes | Yes | Qualified canonical box-with-hole fixture |
| Assembly hierarchy and mesh | Yes | Yes | Same assembly pipeline; referenced source files must be supplied in explicit project snapshots |
| Display mesh | Yes | Yes | Same B-rep tessellator, synchronous browser route |
| Face/semantic selection | Yes | Yes | Triangle ranges plus occurrence IDs |
| Property inspection/edit | Yes | Bounded | Scalar template arguments and canonical box/hole properties |
| Transactional rebuild | Yes | Yes | Last valid snapshot retained |
| AP242 STEP export in memory | Yes | Yes | UTF-8 bytes; no temporary file |
| GLB / USDA download | Yes | Yes | Existing static Part/Assembly/Scene exporters; geometry, hierarchy, supported materials and cameras |
| STL / OBJ download | Yes | Bounded | Part-only SurfaceMeshIR route; STL requires a validated closed triangle mesh |
| PDF / SVG / PPTX drawing download | Yes | Bounded | Automatic dimensioned plate/box family; existing completeness and PMI checks; missing requirements remain explicit |
| Multiple export formats | Yes | Yes | One ZIP download; no partial files on failure; transient drawing/STL scratch files are removed |
| Sheet metal | Yes | No | Not qualified in the browser |
| FEA | Yes | No | Deferred |
| SQLite materials | Yes | No | Native vararg calls are not browser-safe |
| External STEP file import | Yes | No | Browser filesystem/resource abstraction deferred |
| Forge process/subprocess operations | Yes | No | Browser host exposes editor operations only |
| Web Worker | N/A | Yes | Dedicated module Worker; used by Helios for builds and exports |
| Cancellation | Yes | Pre-start only | No mid-kernel cancellation |

The browser package is currently large because it carries untrimmed transitive assemblies. Package-size optimization is deferred; capability claims do not depend on removing those assemblies.
