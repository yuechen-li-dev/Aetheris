"""Exercise camera orbit in real usdview/Hydra without any compiler process.

Run with the installed OpenUSD SDK environment, passing guitar.usda and --timing.
Output defaults to artifacts/local/guitar-x0/viewport. No geometry is generated.
"""
import hashlib
import json
import os
import time
from pathlib import Path
from pxr import Tf, Usd, UsdGeom, Usdviewq

def geometry_hash(stage):
    digest=hashlib.sha256();count=0
    roots=[stage.GetPseudoRoot()]+stage.GetPrototypes()
    for root in roots:
        for prim in Usd.PrimRange(root):
            if not prim.IsA(UsdGeom.Mesh):continue
            count+=1
            for name in ['points','normals','faceVertexCounts','faceVertexIndices']:
                digest.update(str(prim.GetAttribute(name).Get()).encode())
    return digest.hexdigest(),count

class Capture(Usdviewq.Launcher):
    def LaunchProcess(self,args,app,controller):
        api=controller._usdviewApi
        output=Path(os.getenv('GUITAR_X0_CAPTURE_DIR','artifacts/local/guitar-x0/viewport'))
        output.mkdir(parents=True,exist_ok=True)
        api.qMainWindow.resize(1400,1000)
        api.dataModel.viewSettings.showBBoxes=False
        api.dataModel.viewSettings.showHUD=True
        api.viewerMode=True
        # Force a free camera; the orbit below changes only viewer camera state.
        assert api.cameraPrim is None, 'Pass the geometry USD without a presentation camera.'
        api.dataModel.viewSettings.freeCamera.rotPhi=30
        before,mesh_count=geometry_hash(api.stage)
        notices=[]
        def changed(notice,sender):
            notices.append(dict(resynced=[str(p) for p in notice.GetResyncedPaths()],
                                changed=[str(p) for p in notice.GetChangedInfoOnlyPaths()]))
        listener=Tf.Notice.Register(Usd.Notice.ObjectsChanged,changed,api.stage)
        for _ in range(12):app.processEvents();api.UpdateViewport()
        times=[]
        for i in range(36):
            start=time.perf_counter()
            api.dataModel.viewSettings.freeCamera.rotTheta=-30+i*10
            app.processEvents();api.UpdateViewport();app.processEvents()
            # Reading the rendered frame forces real viewport display completion.
            shot=api.GrabViewportShot()
            assert not shot.isNull()
            times.append((time.perf_counter()-start)*1000)
            if i in [0,9,18,27]:assert shot.save(str(output/f'orbit-{i:02d}.png'))
        after,after_count=geometry_hash(api.stage)
        listener.Revoke()
        assert before==after and mesh_count==after_count
        assert not notices, notices
        report=dict(tool='usdview',version=Usd.GetVersion(),renderer=api.GetViewportCurrentRendererId(),
                    stage=api.stageIdentifier,orbitSteps=36,geometryHashBefore=before,
                    geometryHashAfter=after,meshDefinitions=mesh_count,stageChangeNotices=len(notices),
                    compileCallsDuringOrbit=0,geometryRebuildsDuringOrbit=0,
                    reason='Standalone USD viewer has no Firmament compiler connection; camera orbit changes viewer state only.',
                    meanOrbitAndReadbackMilliseconds=sum(times)/len(times),
                    maxOrbitAndReadbackMilliseconds=max(times))
        (output/'viewer.json').write_text(json.dumps(report,indent=2)+'\n')
        print(json.dumps(report))
        app.closeAllWindows()

Capture().Run()
