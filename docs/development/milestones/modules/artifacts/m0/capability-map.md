# Capability map

| Capability | Owner | Since | Lowering |
|---|---|---:|---|
| `Piping.PathPipe` | `Aetheris.Piping` | 0.1.0 | straight centerline + circular section -> exact cylinder BRep |
| `Piping.PipeRoute` | `Aetheris.Piping` | 0.1.0 | line/arc/line route -> cylinder/torus/cylinder BRep |
| `Surfacing.RuledSurface` | `Aetheris.Surfacing` | 0.1.0 | compatible boundaries -> exact analytic/degree-(1,1) surface |
| `Surfacing.RuledTransition` | `Aetheris.Surfacing` | 0.1.0 | compatible sections -> ruled surface with transition identity |

`Aetheris.SheetMetal` is registered but owns no implemented M0 capability.
