"""Capture the real usdview window and Hydra viewport via usdview's own API.

USD_X0_CAPTURE_DIR sets output (default artifacts/local/usd-x0/captures).
USD_X0_FRAMES sets comma-separated frames (default 0,48,96).
Pass ordinary usdview arguments, including --camera for a presentation stage.
"""
import json
import os
import time
from pathlib import Path
from pxr import Usd, Usdviewq


class Capture(Usdviewq.Launcher):
    def LaunchProcess(self, args, app, controller):
        api = controller._usdviewApi
        output = Path(os.getenv("USD_X0_CAPTURE_DIR", "artifacts/local/usd-x0/captures"))
        output.mkdir(parents=True, exist_ok=True)
        api.qMainWindow.resize(1600, 1000)
        api.dataModel.viewSettings.showBBoxes = False
        api.dataModel.viewSettings.showHUD = False
        api.dataModel.viewSettings.domeLightTexturesVisible = False
        controller._ui.primView.expandAll()
        if not api.cameraPrim:
            api.dataModel.viewSettings.freeCamera.rotTheta = -25
            api.dataModel.viewSettings.freeCamera.rotPhi = 55
        api.viewerMode = True
        frames = [float(f) for f in os.getenv("USD_X0_FRAMES", "0,48,96").split(",")]
        for frame in frames:
            if controller._timeSamples:
                controller.setFrame(frame)
            for _ in range(30):
                app.processEvents()
                api.UpdateViewport()
                time.sleep(.04)
            assert api.GrabViewportShot().save(str(output / f"render-{frame:g}.png"))
        # Select a real joint so the properties pane proves its schema/relationships.
        joint = next((p for p in api.stage.Traverse() if p.HasAttribute("aetheris:interfaceFamily")), None)
        api.viewerMode = False
        for prim in api.stage.Traverse():
            if prim.GetName() == "Geometry":
                controller._getItemAtPath(prim.GetPath()).setExpanded(False)
        for path in ["/Looks", "/Presentation"]:
            if api.stage.GetPrimAtPath(path):
                controller._getItemAtPath(path).setExpanded(False)
        if joint:
            api.dataModel.selection.setPrimPath(joint.GetPath())
            api.UpdateGUI()
            controller._ui.primView.scrollToItem(controller._getItemAtPath(joint.GetPath(), ensureExpanded=True))
            for _ in range(10):
                app.processEvents()
                time.sleep(.03)
        assert api.GrabWindowShot().save(str(output / "hierarchy.png"))
        report = dict(tool="usdview", version=Usd.GetVersion(), stage=api.stageIdentifier,
                      renderer=api.GetViewportCurrentRendererId(), capturedFrames=frames,
                      jointPrim=str(joint.GetPath()) if joint else None)
        (output / "viewer.json").write_text(json.dumps(report, indent=2) + "\n")
        print(json.dumps(report))
        app.closeAllWindows()


Capture().Run()
