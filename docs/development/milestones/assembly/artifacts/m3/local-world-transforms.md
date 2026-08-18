# Local/world transform proof

The definition-local Shaft transform is translation Z = 10 mm. Parent Mates
place the two module occurrences at X = -30 mm and X = +30 mm.

| occurrence | world X | world Z |
| --- | ---: | ---: |
| LeftModule | -30 | 0 |
| LeftModule.Shaft | -30 | 10 |
| RightModule | 30 | 0 |
| RightModule.Shaft | 30 | 10 |

The exposed `DriveAxis` world origin is `[-30, 0, 10]` for LeftModule.
`MountFace` and `MountPoint` resolve at X = -30. These queries use the public
occurrence semantic identity, not the private child path.
