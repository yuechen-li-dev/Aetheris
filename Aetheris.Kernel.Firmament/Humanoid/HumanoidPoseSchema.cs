using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Humanoid;

[FirmamentConstruct("Pose", "Pose", Context = "Document", Entry = "Pose fitting { figure: Anatolia; from: TPose; }", Description = "Finite named anatomical pose recipe using the original figure skeleton.")]
[FirmamentField("Figure", "Figure", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("From", "From", FirmamentSchemaValueKind.ConstructReference, Default = "Rest")]
public static class HumanoidPoseDeclaration { }

[FirmamentConstruct("Joint", "Joint", Context = "Pose", Entry = "Joint LeftElbow { flexion: 15deg; }", Description = "Absolute anatomical joint overrides, validated by the shared humanoid solver.")]
[FirmamentField("Flexion", "Flexion", FirmamentSchemaValueKind.Angle, Unit = FirmamentUnitKind.Angle)]
[FirmamentField("Abduction", "Abduction", FirmamentSchemaValueKind.Angle, Unit = FirmamentUnitKind.Angle)]
[FirmamentField("Twist", "Twist", FirmamentSchemaValueKind.Angle, Unit = FirmamentUnitKind.Angle)]
public static class HumanoidPoseJointDeclaration { }

[FirmamentConstruct("PoseTransition", "PoseTransition", Context = "Document", Entry = "PoseTransition lowerArms { from: fitting; to: wearing; duration: 2s; easing: SmoothStep; }", Description = "Explicit finite anatomical transition, sampled at cloth substep times without rebinding the skeleton.")]
[FirmamentField("From", "From", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("To", "To", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Duration", "Duration", FirmamentSchemaValueKind.Scalar, Required = true)]
[FirmamentField("Easing", "Easing", FirmamentSchemaValueKind.Choice, Choices = ["SmoothStep", "Linear"], Default = "SmoothStep")]
public static class HumanoidPoseTransitionDeclaration { }
