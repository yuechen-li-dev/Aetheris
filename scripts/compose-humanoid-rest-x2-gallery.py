"""Compose labeled five-column REST-X2 comparison evidence from ignored local renders."""
import json
import textwrap
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'artifacts/local/humanoid-rest-x2';G=OUT/'gallery';G.mkdir(exist_ok=True)
read=lambda p:json.loads(Path(p).read_text(encoding='utf-8-sig'))
corpus=read(OUT/'pose-corpus.json');ablation=read(OUT/'weight-ablation.json');mixamo=read(OUT/'mixamo-adapter.json')
aetheris=read(OUT/'aetheris/evidence.json');genesis=read(OUT/'genesis-local/pose-observations.json')
by_a={x['pose']:x for x in aetheris['semantic']};by_m={x['pose']:x for x in mixamo['cases']};by_g={x['pose']:x for x in genesis}
by_b={v['pipeline']:{x['pose']:x for x in v['cases']} for v in ablation['variants']}
font=ImageFont.load_default(size=15);small=ImageFont.load_default(size=13)
cols=[('Aetheris','aetheris','canonical A-pose / open'),('Blender auto A','apose-auto','canonical A-pose / open'),
      ('Blender cleaned A','apose-cleaned','canonical A-pose / open'),('Mixamo local','mixamo','FBX native bind / local oracle'),
      ('Genesis 9 local','genesis','Genesis native A-pose / local oracle')]

def request_text(case):
    if not case['requested']:return 'neutral canonical A-pose'
    return '; '.join(f"{r['joint'].replace('Left','L.').replace('Right','R.')} F{r.get('flexionDegrees',0):g} A{r.get('abductionDegrees',0):g}" for r in case['requested'])
def residual(source,pose):
    if source=='aetheris':return by_a[pose]['maximumSemanticResidualDegrees']
    if source in ('apose-auto','apose-cleaned'):return by_b[source][pose]['maximumSemanticResidualDegrees']
    if source=='mixamo':return by_m[pose]['maximumSemanticResidualDegrees']
    return by_g[pose]['maximumResidualDegrees']
def actual(source,pose,case):
    if source=='genesis':
        if pose=='canonical-apose':
            measured=by_g[pose]['measured']
            return '; '.join((f"shoulder F{measured['LeftShoulder']['flexionDegrees']:.2f} A{measured['LeftShoulder']['abductionDegrees']:.2f}",
                             f"elbow F{measured['LeftElbow']['flexionDegrees']:.2f}",
                             f"hip F{measured['LeftHip']['flexionDegrees']:.2f} A{measured['LeftHip']['abductionDegrees']:.2f}",
                             f"knee F{measured['LeftKnee']['flexionDegrees']:.2f}"))
        values=by_g[pose].get('roundtrip',[])
        return '; '.join(f"{v['semantic']} {v['actual']:.2f}°" for v in values) or 'neutral'
    if source=='aetheris':
        requested={r['joint']:r for r in case['requested']}
        return '; '.join(f"{r['joint'].replace('Left','L.').replace('Right','R.')} F{requested[r['joint']].get('flexionDegrees',0)+r['flexionDegrees']:.2f} A{requested[r['joint']].get('abductionDegrees',0)+r['abductionDegrees']:.2f}" for r in by_a[pose]['semanticResiduals']) or 'neutral'
    rs=(by_b[source][pose]['semanticResiduals'] if source.startswith('apose') else by_m[pose]['semanticResiduals'])
    if source=='mixamo':
        wanted={r['joint'] for r in case['requested']} or {'LeftShoulder','RightShoulder'}
        rs=[r for r in rs if r['joint'] in wanted]
    return '; '.join(f"{r['joint'].replace('Left','L.').replace('Right','R.')} F{r['actual']['flexionDegrees']:.2f} A{r['actual']['abductionDegrees']:.2f}" for r in rs) or 'neutral'

rows=[]
for case in corpus:
    pose=case['name'];panels=[]
    for title,source,native in cols:
        path=OUT/f'{source}--{pose}.png';im=Image.open(path).convert('RGB');im.thumbnail((300,338))
        panel=Image.new('RGB',(320,480),'white');d=ImageDraw.Draw(panel);d.text((8,7),title,font=font,fill='black');y=29
        for label,value in [('requested',request_text(case)),('actual',actual(source,pose,case))]:
            lines=textwrap.wrap(label+': '+value,width=47,break_long_words=False) or [label+':']
            d.multiline_text((8,y),'\n'.join(lines),font=small,fill='black',spacing=1);y+=16*len(lines)+3
        d.text((8,y),f"residual: {residual(source,pose):.3f}°",font=small,fill='black');y+=19
        d.text((8,y),'native rest: '+native,font=small,fill='black');y+=19
        im.thumbnail((300,470-y));panel.paste(im,((320-im.width)//2,y+(470-y-im.height)//2));panels.append(panel)
    row=Image.new('RGB',(1600,510),(225,225,225));ImageDraw.Draw(row).text((8,485),pose,font=font,fill='black')
    for i,panel in enumerate(panels):row.paste(panel,(320*i,0))
    row.save(G/f'{pose}.png');rows.append(row)
sheet=Image.new('RGB',(1600,510*len(rows)),'white')
for i,row in enumerate(rows):sheet.paste(row,(0,510*i))
sheet.save(OUT/'five-column-gallery.png')
print(OUT/'five-column-gallery.png')
