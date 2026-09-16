# Web SDK X1 capability matrix

| Capability | Native | Browser WASM X1 | Notes |
|---|---:|---:|---|
| Firmament V2 compile from memory | Yes | Yes | Same compiler/materializer |
| Solid modeling | Yes | Yes | Qualified canonical box-with-hole fixture |
| Assembly hierarchy and mesh | Yes | Yes | Same M1 assembly pipeline; external-file definitions excluded |
| Display mesh | Yes | Yes | Same B-rep tessellator, synchronous browser route |
| Face/semantic selection | Yes | Yes | Triangle ranges plus occurrence IDs |
| Property inspection/edit | Yes | Bounded | Scalar template arguments and canonical box/hole properties |
| Transactional rebuild | Yes | Yes | Last valid snapshot retained |
| AP242 STEP export in memory | Yes | Yes | UTF-8 bytes; no temporary file |
| Sheet metal | Yes | No | Not qualified in browser X1 |
| FEA | Yes | No | Deferred |
| SQLite materials | Yes | No | Native vararg calls are not browser-safe |
| External STEP file import | Yes | No | Browser filesystem/resource abstraction deferred |
| Forge process/subprocess operations | Yes | No | Browser host exposes editor operations only |
| Web Worker | N/A | No | Transport initialization does not complete in Chromium; typed rejection |
| Cancellation | Yes | Pre-start only | No mid-kernel cancellation |

The browser package is currently large because it carries untrimmed transitive assemblies. Package-size optimization is deferred; capability claims do not depend on removing those assemblies.
