"""Add presentation-only studio, camera, lights and chrome sphere as a separate USD layer.

Engineering geometry and motion remain referenced from the unmodified exported assembly.
Run using the external OpenUSD SDK. No physics or engineering model is authored here.
"""
import argparse
from pathlib import Path
from pxr import Gf, Sdf, Usd, UsdGeom, UsdLux, UsdShade


def material(stage, name, color, metallic, roughness):
    m = UsdShade.Material.Define(stage, "/Presentation/Looks/" + name)
    shader = UsdShade.Shader.Define(stage, str(m.GetPath()) + "/Surface")
    shader.CreateIdAttr("UsdPreviewSurface")
    shader.CreateInput("diffuseColor", Sdf.ValueTypeNames.Color3f).Set(Gf.Vec3f(*color))
    shader.CreateInput("metallic", Sdf.ValueTypeNames.Float).Set(metallic)
    shader.CreateInput("roughness", Sdf.ValueTypeNames.Float).Set(roughness)
    m.CreateSurfaceOutput().ConnectToSource(shader.ConnectableAPI(), "surface")
    return m


def prepare(source, output, environment=None):
    source, output = Path(source).resolve(), Path(output).resolve()
    assert source != output
    output.parent.mkdir(parents=True, exist_ok=True)
    stage = Usd.Stage.CreateNew(str(output))
    import os
    stage.GetRootLayer().subLayerPaths = [os.path.relpath(source, output.parent).replace("\\", "/")]
    assembly_stage = Usd.Stage.Open(str(source))
    stage.SetStartTimeCode(assembly_stage.GetStartTimeCode())
    stage.SetEndTimeCode(assembly_stage.GetEndTimeCode())
    stage.SetTimeCodesPerSecond(assembly_stage.GetTimeCodesPerSecond())
    UsdGeom.SetStageMetersPerUnit(stage, UsdGeom.GetStageMetersPerUnit(assembly_stage))
    UsdGeom.SetStageUpAxis(stage, UsdGeom.GetStageUpAxis(assembly_stage))
    stage.SetDefaultPrim(stage.GetPrimAtPath("/Assembly"))
    UsdGeom.Xform.Define(stage, "/Presentation").GetPrim().CreateAttribute("aetheris:authority", Sdf.ValueTypeNames.String).Set("presentation-only")
    floor = UsdGeom.Cube.Define(stage, "/Presentation/Stage")
    floor.CreateSizeAttr(1)
    floor.AddTranslateOp().Set(Gf.Vec3d(70,75,-8))
    floor.AddScaleOp().Set(Gf.Vec3d(470,430,12))
    UsdShade.MaterialBindingAPI.Apply(floor.GetPrim()).Bind(material(stage,"Stage",(.025,.035,.055),.15,.32))
    # Sphere is a visual target; it is deliberately outside Aetheris's product tree.
    sphere = UsdGeom.Sphere.Define(stage, "/Presentation/ChromeTarget")
    sphere.CreateRadiusAttr(14)
    target = sphere.AddTranslateOp()
    wrist = next(p for p in stage.Traverse() if p.GetAttribute("aetheris:sourcePath").Get() == "Atlas.Wrist")
    target.Set(UsdGeom.XformCache().GetLocalToWorldTransform(wrist).Transform(Gf.Vec3d(28,0,-12)))
    for frame in range(int(stage.GetStartTimeCode()),int(stage.GetEndTimeCode())+1):
        target.Set(UsdGeom.XformCache(frame).GetLocalToWorldTransform(wrist).Transform(Gf.Vec3d(28,0,-12)),frame)
    UsdShade.MaterialBindingAPI.Apply(sphere.GetPrim()).Bind(material(stage,"Chrome",(.82,.86,.92),1,.09))
    for name, position, intensity, color, scale in [
        ("Key",(40,-180,400),3,(.85,.92,1),180),
        ("Rim",(0,360,300),2,(.3,.8,1),120),
        ("Fill",(350,80,240),1,(1,.8,.6),150)]:
        light=UsdLux.RectLight.Define(stage,"/Presentation/Lights/"+name)
        light.CreateIntensityAttr(intensity)
        light.CreateColorAttr(Gf.Vec3f(*color))
        light.CreateWidthAttr(scale);light.CreateHeightAttr(scale)
        light.AddTransformOp().Set(Gf.Matrix4d().SetLookAt(Gf.Vec3d(*position),Gf.Vec3d(85,100,55),Gf.Vec3d(0,0,1)).GetInverse())
    dome=UsdLux.DomeLight.Define(stage,"/Presentation/Lights/SoftEnvironment")
    dome.CreateIntensityAttr(.5)
    if environment:
        import shutil
        asset = output.parent / "studio-environment.hdr"
        shutil.copyfile(environment,asset)
        dome.CreateTextureFileAttr(Sdf.AssetPath(asset.name))
    camera=UsdGeom.Camera.Define(stage,"/Presentation/HeroCamera")
    camera.AddTransformOp().Set(Gf.Matrix4d().SetLookAt(Gf.Vec3d(440,510,400),Gf.Vec3d(100,100,65),Gf.Vec3d(0,0,1)).GetInverse())
    camera.CreateFocalLengthAttr(35)
    camera.CreateClippingRangeAttr(Gf.Vec2f(.1,10000))
    stage.GetRootLayer().Save()
    print(output)


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("output",nargs="?",default="artifacts/local/usd-x0/demo-studio.usda")
    parser.add_argument("--environment",help="Optional local HDR supplied by the installed NVIDIA tool distribution")
    args=parser.parse_args()
    prepare(args.source,args.output,args.environment)
