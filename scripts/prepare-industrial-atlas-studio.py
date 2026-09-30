"""Compose a presentation layer over the exact IndustrialAtlas USD export.

The floor, lights, camera, chrome target and printed maker label are presentation
assets. Product solids, hierarchy, instancing and motion are unchanged.
Run with NVIDIA's OpenUSD Python distribution, as in qualify-usd-export-x0.ps1.
"""
import argparse
import importlib.util
import math
import os
from pathlib import Path
from pxr import Gf, Sdf, Usd, UsdGeom, UsdLux, UsdShade
_spec = importlib.util.spec_from_file_location("studio", Path(__file__).with_name("prepare-usd-x0-studio.py"))
_studio = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_studio)
material = _studio.material


def maker_texture(path):
    # Deterministic typography from the same bundled font used by Aetheris.
    from PySide6.QtGui import QFont, QFontDatabase, QImage, QPainter, QColor
    from PySide6.QtWidgets import QApplication
    app = QApplication.instance() or QApplication([])
    font_path = Path(__file__).resolve().parents[1] / "Aetheris.Kernel.Firmament/Resources/Inter-Regular.ttf"
    font_id = QFontDatabase.addApplicationFont(str(font_path))
    assert font_id >= 0
    family = QFontDatabase.applicationFontFamilies(font_id)[0]
    image = QImage(1024, 192, QImage.Format_RGB32)
    image.fill(QColor(192, 200, 211))
    painter = QPainter(image)
    painter.setRenderHint(QPainter.Antialiasing)
    painter.setPen(QColor(24, 30, 39))
    font = QFont(family); font.setPixelSize(78)
    painter.setFont(font); painter.drawText(48, 98, "GPT 6.1 SOL CODEX")
    font.setPixelSize(30); painter.setFont(font)
    painter.drawText(51, 157, "AETHERIS   /   ATLAS-02   /   EXACT CAD")
    painter.end()
    assert image.save(str(path))


def prepare(source, output):
    source, output = Path(source).resolve(), Path(output).resolve()
    assert source != output
    output.parent.mkdir(parents=True, exist_ok=True)
    stage = Usd.Stage.CreateNew(str(output))
    stage.GetRootLayer().subLayerPaths = [os.path.relpath(source, output.parent).replace("\\", "/")]
    product = Usd.Stage.Open(str(source))
    for set_value, get_value in [(stage.SetStartTimeCode, product.GetStartTimeCode),
                                 (stage.SetEndTimeCode, product.GetEndTimeCode),
                                 (stage.SetTimeCodesPerSecond, product.GetTimeCodesPerSecond)]:
        set_value(get_value())
    UsdGeom.SetStageMetersPerUnit(stage, UsdGeom.GetStageMetersPerUnit(product))
    UsdGeom.SetStageUpAxis(stage, UsdGeom.GetStageUpAxis(product))
    stage.SetDefaultPrim(stage.GetPrimAtPath("/Assembly"))
    UsdGeom.Xform.Define(stage, "/Presentation").GetPrim().CreateAttribute("aetheris:authority", Sdf.ValueTypeNames.String).Set("presentation-only")
    # Explicit presentation meshes keep material bindings across USD readers;
    # Blender 5.2 currently drops bindings on imported Cube/Sphere primitives.
    floor = UsdGeom.Mesh.Define(stage, "/Presentation/Floor")
    floor.CreatePointsAttr([Gf.Vec3f(-10000,-10000,-8),Gf.Vec3f(10000,-10000,-8),Gf.Vec3f(10000,10000,-8),Gf.Vec3f(-10000,10000,-8)])
    floor.CreateFaceVertexCountsAttr([4]); floor.CreateFaceVertexIndicesAttr([0,1,2,3])
    floor.CreateSubdivisionSchemeAttr("none"); floor.CreateNormalsAttr([Gf.Vec3f(0,0,1)]*4)
    UsdShade.MaterialBindingAPI.Apply(floor.GetPrim()).Bind(material(stage, "Graphite", (.018, .022, .03), .08, .31))
    gripper = next(p for p in stage.Traverse() if p.GetAttribute("aetheris:sourcePath").Get() == "AtlasIndustrial.Gripper.Body")
    sphere = UsdGeom.Mesh.Define(stage, "/Presentation/ChromeTarget")
    normals = [Gf.Vec3f(0,0,1)]
    for latitude in range(1,32):
        theta=math.pi*latitude/32
        for longitude in range(64):
            phi=2*math.pi*longitude/64
            normals.append(Gf.Vec3f(math.sin(theta)*math.cos(phi),math.sin(theta)*math.sin(phi),math.cos(theta)))
    normals.append(Gf.Vec3f(0,0,-1))
    points=[n*22 for n in normals]; faces=[]
    for i in range(64): faces.extend([0,1+i,1+(i+1)%64])
    for ring in range(30):
        for i in range(64):
            a=1+ring*64+i;b=1+ring*64+(i+1)%64;c=a+64;d=b+64
            faces.extend([a,c,b,b,c,d])
    for i in range(64): faces.extend([len(points)-1,1+30*64+(i+1)%64,1+30*64+i])
    sphere.CreatePointsAttr(points);sphere.CreateNormalsAttr(normals)
    sphere.CreateFaceVertexCountsAttr([3]*(len(faces)//3));sphere.CreateFaceVertexIndicesAttr(faces)
    sphere.CreateSubdivisionSchemeAttr("none");sphere.SetNormalsInterpolation("vertex")
    translate = sphere.AddTranslateOp()
    translate.Set(UsdGeom.XformCache().GetLocalToWorldTransform(gripper).Transform(Gf.Vec3d(58, 0, 14)))
    for frame in range(int(stage.GetStartTimeCode()), int(stage.GetEndTimeCode()) + 1):
        translate.Set(UsdGeom.XformCache(frame).GetLocalToWorldTransform(gripper).Transform(Gf.Vec3d(58, 0, 14)), frame)
    UsdShade.MaterialBindingAPI.Apply(sphere.GetPrim()).Bind(material(stage, "Chrome", (.8, .84, .9), 1, .07))
    maker = next(p for p in stage.Traverse() if p.GetAttribute("aetheris:sourcePath").Get() == "AtlasIndustrial.ForeArm.MakerPlate")
    label = UsdGeom.Mesh.Define(stage, str(maker.GetPath()) + "/PrintedLabel")
    label.CreatePointsAttr([Gf.Vec3f(4, -7, 2.3), Gf.Vec3f(80, -7, 2.3), Gf.Vec3f(80, 7, 2.3), Gf.Vec3f(4, 7, 2.3)])
    label.CreateFaceVertexCountsAttr([4]); label.CreateFaceVertexIndicesAttr([0, 1, 2, 3])
    label.CreateSubdivisionSchemeAttr("none")
    label.CreateNormalsAttr([Gf.Vec3f(0, 0, 1)] * 4)
    UsdGeom.PrimvarsAPI(label).CreatePrimvar("st", Sdf.ValueTypeNames.TexCoord2fArray, "vertex").Set([Gf.Vec2f(1, 1), Gf.Vec2f(0, 1), Gf.Vec2f(0, 0), Gf.Vec2f(1, 0)])
    label.GetPrim().CreateAttribute("aetheris:authority", Sdf.ValueTypeNames.String).Set("presentation-only printed maker mark")
    texture = output.parent / "maker-label.png"
    maker_texture(texture)
    m = material(stage, "MakerLabel", (.6, .64, .7), .45, .3)
    shader = UsdShade.Shader(stage.GetPrimAtPath(str(m.GetPath()) + "/Surface"))
    reader = UsdShade.Shader.Define(stage, str(m.GetPath()) + "/UV")
    reader.CreateIdAttr("UsdPrimvarReader_float2")
    reader.CreateInput("varname", Sdf.ValueTypeNames.String).Set("st")
    reader.CreateOutput("result", Sdf.ValueTypeNames.Float2)
    tex = UsdShade.Shader.Define(stage, str(m.GetPath()) + "/Texture")
    tex.CreateIdAttr("UsdUVTexture")
    tex.CreateInput("file", Sdf.ValueTypeNames.Asset).Set(Sdf.AssetPath(texture.name))
    tex.CreateInput("sourceColorSpace", Sdf.ValueTypeNames.Token).Set("sRGB")
    tex.CreateInput("st", Sdf.ValueTypeNames.Float2).ConnectToSource(reader.ConnectableAPI(), "result")
    tex.CreateOutput("rgb", Sdf.ValueTypeNames.Float3)
    shader.GetInput("diffuseColor").ConnectToSource(tex.ConnectableAPI(), "rgb")
    UsdShade.MaterialBindingAPI.Apply(label.GetPrim()).Bind(m)
    target = Gf.Vec3d(30, 0, 170)
    for name, position, intensity, color, size in [
        ("Key", (160, -330, 650), 4, (1, .9, .78), 300),
        ("Rim", (-240, 260, 440), 3, (.68, .8, 1), 250),
        ("Fill", (300, -400, 230), 1.8, (1, .98, .93), 200)]:
        light = UsdLux.RectLight.Define(stage, "/Presentation/Lights/" + name)
        light.CreateIntensityAttr(intensity); light.CreateColorAttr(Gf.Vec3f(*color))
        light.CreateWidthAttr(size); light.CreateHeightAttr(size)
        light.AddTransformOp().Set(Gf.Matrix4d().SetLookAt(Gf.Vec3d(*position), target, Gf.Vec3d(0, 0, 1)).GetInverse())
    dome = UsdLux.DomeLight.Define(stage, "/Presentation/Lights/Environment")
    dome.CreateIntensityAttr(.15); dome.CreateColorAttr(Gf.Vec3f(.3, .36, .46))
    camera = UsdGeom.Camera.Define(stage, "/Presentation/HeroCamera")
    camera.AddTransformOp().Set(Gf.Matrix4d().SetLookAt(Gf.Vec3d(-430, -900, 390), Gf.Vec3d(-125, 0, 145), Gf.Vec3d(0, 0, 1)).GetInverse())
    camera.CreateFocalLengthAttr(48)
    camera.CreateHorizontalApertureAttr(36); camera.CreateVerticalApertureAttr(20.25)
    camera.CreateClippingRangeAttr(Gf.Vec2f(.1, 10000))
    stage.GetRootLayer().Save()
    print(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source")
    parser.add_argument("output", nargs="?", default="artifacts/local/usd-industrial/atlas-studio.usda")
    args = parser.parse_args()
    prepare(args.source, args.output)
