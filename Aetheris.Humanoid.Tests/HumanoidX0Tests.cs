using System.Numerics;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Xunit;

namespace Aetheris.Humanoid.Tests;

public sealed class HumanoidX0Tests
{
    private static readonly Lazy<CanonicalHumanoid> Fixture=new(CanonicalAdultTemplate.Create);

    [Fact]
    public void AuthoredTemplate_HasStableIdentityAndValidInvariants()
    {
        var h=Fixture.Value;var result=HumanoidValidator.Validate(h);
        Assert.True(result.IsValid,string.Join("\n",result.Diagnostics));
        Assert.Equal("aetheris.humanoid.adult.standard.v1",h.Surface.TopologyId);
        Assert.InRange(h.Surface.Vertices.Count,8000,20000);
        Assert.Equal(h.Surface.Faces.Count,h.Surface.BindingTriangles.Count);
        Assert.All(h.Surface.Vertices.Select((v,i)=>(v,i)),x=>Assert.Equal(x.i,h.Surface.Vertices[x.v.SymmetryPartnerIndex].SymmetryPartnerIndex));
    }

    [Fact]
    public void AuthoredTemplate_IsDeterministic()
    {
        var first=Fixture.Value;var second=CanonicalAdultTemplate.Create();
        Assert.Equal(first.Surface.ConnectivityHash,second.Surface.ConnectivityHash);
        Assert.Equal(first.Provenance.ConfigHash,second.Provenance.ConfigHash);
        Assert.Equal(first.Surface.Vertices.Select(x=>x.Position),second.Surface.Vertices.Select(x=>x.Position));
    }

    [Fact]
    public void Measurements_AreTypedRepeatableAndExplicitlyPosed()
    {
        var a=HumanoidMeasurements.Evaluate(Fixture.Value);var b=HumanoidMeasurements.Evaluate(Fixture.Value);
        Assert.Equal(a,b);Assert.Contains(a,x=>x.Measurement==HumanoidMeasurementId.Height&&x.ValueMm==1750&&x.IsValid);
        Assert.All(a,x=>Assert.Equal(CanonicalAdultStandardV1.MeasurementPoseId,x.PoseId));
        Assert.Contains(a,x=>x.Measurement==HumanoidMeasurementId.ArmLength&&x.ValueMm>500);
    }

    [Fact]
    public void Morphs_KeepTopologyAndMoveExpectedMeasurements()
    {
        var h=Fixture.Value;var baseline=HumanoidMeasurements.Evaluate(h).ToDictionary(x=>x.Measurement,x=>x.ValueMm!.Value);
        var tall=HumanoidMorphing.Apply(h,new Dictionary<HumanoidMorphId,double>{{HumanoidMorphId.Height,1800},{HumanoidMorphId.LegLength,35},{HumanoidMorphId.ArmLength,25}});
        var measured=HumanoidMeasurements.Evaluate(tall).ToDictionary(x=>x.Measurement,x=>x.ValueMm!.Value);
        Assert.Equal(h.Surface.ConnectivityHash,tall.Surface.ConnectivityHash);Assert.Equal(1,tall.ShapeRevision);
        Assert.InRange(measured[HumanoidMeasurementId.Height],1799.999,1800.001);
        Assert.Equal(1800,tall.Frame.NormalizedHeightMm);
        Assert.True(measured[HumanoidMeasurementId.LegLength]>baseline[HumanoidMeasurementId.LegLength]);
        Assert.True(measured[HumanoidMeasurementId.ArmLength]>baseline[HumanoidMeasurementId.ArmLength]);
        Assert.True(HumanoidValidator.Validate(tall).IsValid);
    }

    [Fact]
    public void MorphBounds_FailClosedWithStructuredDiagnostic()
    {
        var ex=Assert.Throws<HumanoidDomainException>(()=>HumanoidMorphing.Apply(Fixture.Value,new Dictionary<HumanoidMorphId,double>{{HumanoidMorphId.Height,2100}}));
        Assert.Equal(HumanoidDiagnosticCode.HUM013_MorphOutOfBounds,ex.Diagnostic.Code);
    }

    [Fact]
    public void PoseAndAttachment_UseSameSkeletonSemantics()
    {
        var h=Fixture.Value;var pose=new HumanoidPoseState("test.elbow",[new(HumanoidJointKind.LeftElbow,Quaternion.CreateFromAxisAngle(Vector3.UnitY,(float)(Math.PI/2)))],HumanoidTransform.Identity);
        var neutral=HumanoidPosing.EvaluateAttachment(h,"site:Wrist.Left",h.PoseState);var posed=HumanoidPosing.EvaluateAttachment(h,"site:Wrist.Left",pose);var vertices=HumanoidPosing.EvaluateVertices(h,pose);
        Assert.NotEqual(neutral.Position,posed.Position);Assert.All(vertices,p=>Assert.True(double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z)));Assert.Equal(h.Surface.Vertices.Count,vertices.Count);
    }

    [Fact]
    public void RequiredDomainKindsRemainSeparate()
    {
        var h=Fixture.Value;
        Assert.NotEmpty(h.Landmarks.OfType<SurfaceLandmark>());Assert.NotEmpty(h.Landmarks.OfType<JointLandmark>());
        Assert.DoesNotContain(h.Landmarks,x=>x.GetType()==typeof(HumanoidLandmark));
        Assert.Contains(h.Attachments,x=>x.Id=="site:Chest");Assert.Contains(h.Attachments,x=>x.Id=="site:Wrist.Left");Assert.Contains(h.Attachments,x=>x.Id=="site:Foot.Left");
    }

    [Fact]
    public void CorruptedWeightAndBindingAreRejected()
    {
        var h=Fixture.Value;var brokenWeight=h.Surface.SkinWeights.ToArray();brokenWeight[0]=new(0,[new(0,.8)]);var result=HumanoidValidator.Validate(h with{Surface=h.Surface with{SkinWeights=brokenWeight}});Assert.Contains(result.Diagnostics,x=>x.Code==HumanoidDiagnosticCode.HUM005_SkinWeightsInvalid);
        var landmark=(SurfaceLandmark)h.Landmarks.OfType<SurfaceLandmark>().First();var bad=landmark with{Binding=landmark.Binding with{TopologyId="wrong"}};var landmarks=h.Landmarks.Select(x=>x.Id==bad.Id?bad:x).ToArray();result=HumanoidValidator.Validate(h with{Landmarks=landmarks});Assert.Contains(result.Diagnostics,x=>x.Code==HumanoidDiagnosticCode.HUM014_InvalidBinding);
    }

    [Fact]
    public void CorrespondingRegistrationRecoversKnownSmoothDeformationWithoutChangingIdentity()
    {
        var h=Fixture.Value;var targets=h.Surface.Vertices.Select(v=>v.Position+new Vector3D(12*Math.Sin(v.Position.Z/300),4*Math.Cos(v.Position.X/180),8*Math.Sin(v.Position.Y/100))).ToArray();var config=new CorrespondingRegistrationConfig("synthetic-known-v1",30,.5,20,.001);var result=HumanoidCorrespondingRegistration.Fit(h,h.Surface.TopologyId,targets,config);var replay=HumanoidCorrespondingRegistration.Fit(h,h.Surface.TopologyId,targets,config);
        Assert.True(result.Converged);Assert.True(result.FinalRmsMm<.001);Assert.True(result.InitialRmsMm>result.FinalRmsMm*1000);Assert.Equal(h.Surface.ConnectivityHash,result.Humanoid.Surface.ConnectivityHash);Assert.Equal(h.Surface.Faces,result.Humanoid.Surface.Faces);
        Assert.Equal(result.Humanoid.Surface.Vertices.Select(x=>x.Position),replay.Humanoid.Surface.Vertices.Select(x=>x.Position));var rejected=HumanoidCorrespondingRegistration.Fit(h,"wrong-topology",targets,config);Assert.Contains(rejected.Diagnostics,x=>x.Code==HumanoidDiagnosticCode.HUM015_ConnectivityChanged);
    }

    [Theory]
    [InlineData(HumanoidMorphId.Height,1650)][InlineData(HumanoidMorphId.Height,1850)]
    [InlineData(HumanoidMorphId.ArmLength,-50)][InlineData(HumanoidMorphId.ArmLength,50)]
    [InlineData(HumanoidMorphId.LegLength,-60)][InlineData(HumanoidMorphId.LegLength,60)]
    [InlineData(HumanoidMorphId.ShoulderWidth,-40)][InlineData(HumanoidMorphId.ShoulderWidth,40)]
    public void MorphEndpointsKeepTopologyAndCanonicalValidity(HumanoidMorphId id,double value)
    {
        var h=Fixture.Value;var shaped=HumanoidMorphing.Apply(h,new Dictionary<HumanoidMorphId,double>{{id,value}});Assert.Equal(h.Surface.ConnectivityHash,shaped.Surface.ConnectivityHash);Assert.True(HumanoidValidator.Validate(shaped).IsValid);Assert.Equal(h.Surface.Faces,shaped.Surface.Faces);
    }

    [Theory]
    [InlineData(HumanoidJointKind.LeftShoulder,35)][InlineData(HumanoidJointKind.LeftShoulder,70)]
    [InlineData(HumanoidJointKind.LeftElbow,45)][InlineData(HumanoidJointKind.LeftElbow,90)]
    [InlineData(HumanoidJointKind.LeftHip,35)][InlineData(HumanoidJointKind.LeftHip,70)]
    [InlineData(HumanoidJointKind.LeftKnee,45)][InlineData(HumanoidJointKind.LeftKnee,90)]
    public void PoseSweepProducesFiniteInspectableEvidence(HumanoidJointKind joint,double degrees)
    {
        var axis=joint is HumanoidJointKind.LeftHip or HumanoidJointKind.LeftKnee?Vector3.UnitX:Vector3.UnitY;var pose=new HumanoidPoseState($"sweep.{joint}.{degrees}",[new(joint,Quaternion.CreateFromAxisAngle(axis,(float)(degrees*Math.PI/180)))],HumanoidTransform.Identity);var evidence=HumanoidPoseQualification.Evaluate(Fixture.Value,pose);Assert.True(evidence.Finite);Assert.True(double.IsFinite(evidence.SurfaceAreaRatio));Assert.True(double.IsFinite(evidence.AbsoluteVolumeRatio));Assert.Contains("site:Wrist.Left",evidence.AttachmentPositions);
    }
}
