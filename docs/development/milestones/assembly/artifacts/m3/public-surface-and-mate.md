# Public Assembly surface and parent Mate

| public name | capability | implementation provenance |
| --- | --- | --- |
| Mount | AxisCapable, PlaneCapable, PointCapable | Housing.Mount |
| Drive | AxisCapable, PlaneCapable, PointCapable | Shaft.Drive |
| DriveAxis | AxisCapable | Shaft.Drive.Axis |
| MountFace | PlaneCapable | Housing.Mount.Base |
| MountPoint | PointCapable | Housing.Mount.Point |

Parent Mate `PlaceLeft` records its public participant as
`Moving: Machine.LeftModule.Mount`. Direct source dependency on
`Machine.LeftModule.Housing.Mount` remains rejected with
`assembly-internal-member-hidden`.
