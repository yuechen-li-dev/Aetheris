"""Local-only comparison gallery; references remain outside distributable artifacts."""
import html
import json
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
out = root / 'artifacts/local/humanoid-rerig-x1'
x0 = out.parent / 'humanoid-rerig-x0'
metrics = json.loads((out / 'metrics.json').read_text())
oracle = {'neutral':'neutral','hip-flexion-70':'hip70','hip-flexion-90':'hip90',
          'hip-abduction-45':'abduction45','knee-flexion-90':'knee90',
          'shoulder-abduction-60':'shoulder60','shoulder-abduction-90':'shoulder90','elbow-flexion-90':'elbow90'}
body = ['<!doctype html><meta charset="utf-8"><title>X1 local comparison</title>',
        '<style>body{font:16px system-ui;background:#202328;color:white;padding:20px}img{width:240px}td{vertical-align:top}a{color:#ace}</style>',
        '<h1>EXPERIMENTAL GOLDEN-PATH STUDY — local evidence</h1>',
        '<p>X5, X0, X1 and Mixamo share the camera. Genesis 9 uses matched projection and normalized height, approximate native control angles, unrelated topology. Oracle images remain local. Reversals are transported-normal proxies, not certified intersections.</p>']
for item in metrics:
    pose = item['pose']
    files = [('X5', x0 / f'x5--{pose}.png'), ('X0 auto', x0 / f'pass-a--{pose}.png'),
             ('X1 cleaned', out / f'cleaned--{pose}.png'), ('Mixamo local', x0 / f'mixamo--{pose}.png')]
    if pose in oracle:
        files.append(('Genesis 9 local', out / 'daz-reference' / (oracle[pose]+'.png')))
    body.append('<h2>'+html.escape(pose)+'</h2><table><tr>')
    for label, file in files:
        href = '../'+file.relative_to(out.parent).as_posix()
        body.append(f'<td>{label}<br><a href="{href}"><img src="{href}"></a></td>' if file.exists() else f'<td>{label}: unavailable</td>')
    body.append('</tr></table><p>X1: '+html.escape(json.dumps({k:v for k,v in item['metrics'].items() if k in ['p50','p95','p99','max','minCompression','reversalCount','degenerateTriangles']}))+'</p>')
    for view in ('front','side','rear'):
        file = out / f'detail--{pose}--{view}.png'
        if file.exists(): body.append(f'<a href="{file.name}"><img src="{file.name}" alt="{view}"></a>')
    if pose in oracle:
        sheet = Image.new('RGB', (320*len(files),390), 'white')
        draw = ImageDraw.Draw(sheet)
        for i,(label,file) in enumerate(files):
            draw.text((320*i+5,5),label+' / '+pose,fill='black')
            if file.exists():
                image=Image.open(file).convert('RGB');image.thumbnail((320,360))
                sheet.paste(image,(320*i,25))
        sheet.save(out / f'comparison--{pose}.jpg')
(out / 'comparison.html').write_text('\n'.join(body),encoding='utf-8')
print(out / 'comparison.html')
