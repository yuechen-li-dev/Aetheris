"""Build a local HTML gallery and contact sheets; requires Pillow, no source assets."""
import argparse
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw

p=argparse.ArgumentParser()
p.add_argument('--out',default=str(Path(__file__).resolve().parents[1]/'artifacts/local/humanoid-rerig-x0'))
args=p.parse_args()
out=Path(args.out)
metrics=json.loads((out/'metrics.json').read_text())
names=['x5','pass-a','pass-b','pass-c-volume','pass-c-corrective','pass-c-local','mixamo']
poses=list(dict.fromkeys(x['pose'] for x in metrics))
lookup={(x['pipeline'],x['pose']):x['metrics'] for x in metrics}
body=['<!doctype html><meta charset="utf-8"><title>HUMANOID-RERIG-X0 local comparison</title>',
      '<style>body{font:15px system-ui;background:#181a1d;color:#eee;margin:24px}table{border-collapse:collapse}td,th{padding:8px;border:1px solid #555}img{width:240px}a{color:#ade}h2{margin-top:40px}</style>',
      '<h1>EXPERIMENTAL / BRANCH STUDY — local-only comparison</h1>',
      '<p>Same orthographic camera. Angles are deltas from the authored rest pose. Mixamo data and renders are local evidence only.</p>',
      '<p>Values: maximum bidirectional edge distortion / transported-normal reversal proxy / flagged triangles. These are screening proxies, not certified self-intersections.</p>']
for pose in poses:
    body.append('<h2>'+html.escape(pose)+'</h2><table><tr>')
    for name in names:body.append('<th>'+name+'</th>')
    body.append('</tr><tr>')
    for name in names:
        file=name+'--'+pose+'.png'
        m=lookup[name,pose]
        body.append(f'<td><a href="{file}"><img src="{file}"></a><br>{m["maxEdgeDistortion"]:.2f} / {m["normalReversalProxy"]} / {m["flaggedTriangles"]}</td>')
    body.append('</tr></table>')
(out/'comparison.html').write_text('\n'.join(body),encoding='utf-8')
for label,subset in [('gates',['hip-flexion-70','knee-flexion-90','shoulder-abduction-90']),
                     ('extremes',['hip-flexion-90','elbow-flexion-90','combined-raised-arm']),
                     ('remaining',['neutral','hip-abduction-45','shoulder-abduction-60','combined-balance'])]:
    columns=['x5','pass-a','mixamo']
    sheet=Image.new('RGB',(450*len(columns),532*len(subset)),'white')
    draw=ImageDraw.Draw(sheet)
    for y,pose in enumerate(subset):
        for x,name in enumerate(columns):
            image=Image.open(out/(name+'--'+pose+'.png')).convert('RGB').resize((450,506))
            sheet.paste(image,(x*450,y*532+26))
            draw.text((x*450+8,y*532+6),name+' / '+pose,fill='black')
    sheet.save(out/('comparison-'+label+'.png'))
print(out/'comparison.html')
