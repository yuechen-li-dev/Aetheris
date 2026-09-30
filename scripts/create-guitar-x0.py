"""Author the bounded X0 witness as ordinary Firmament, never as a render mesh.

Run from the repository root. All durable source goes into fixtures; generated
exports and renders belong under artifacts/local/guitar-x0.
"""
from pathlib import Path
import math
import json

ROOT = Path('fixtures/Canonical/AssemblyInterfaces/GuitarX0')
ROOT.mkdir(parents=True, exist_ok=True)

# Original single-cut outline, millimetres; photographs are proportion references.
OUTLINE = [(0,-220),(115,-195),(168,-115),(148,-35),(105,28),
           (118,95),(124,140),(113,160),(91,142),(48,137),
           (28,204),(-42,205),(-111,174),(-119,115),(-95,40),
           (-137,-32),(-168,-115),(-115,-195)]

def number(v):
    return f'{v:.6f}'.rstrip('0').rstrip('.') if abs(v)>1e-7 else '0'

def profile(name, pts):
    # Uniform periodic cubic interpolation gives shared tangent directions.
    rows=[]
    n=len(pts)
    for i,p in enumerate(pts):
        before,after=pts[(i-1)%n],pts[(i+1)%n]
        h=((after[0]-before[0])/6,(after[1]-before[1])/6)
        for label,q in [('P',p),('A',(p[0]+h[0],p[1]+h[1])),('B',(p[0]-h[0],p[1]-h[1]))]:
            rows.append(f' Point2 {name}{label}{i} {{ Position: [{number(q[0])}mm,{number(q[1])}mm] }}')
    for i in range(n):
        j=(i+1)%n
        rows.append(f' CubicBezier2 {name}C{i} {{ From: {name}P{i}; Control1: {name}A{i}; Control2: {name}B{j}; To: {name}P{j} }}')
    rows.append(f' Profile {name} {{ Loop Outer {{')
    for i in range(n):
        rows.append(f'  Segment Edge{i} {{ Trace: {name}C{i}; From: {name}P{i}; To: {name}P{(i+1)%n} }}')
    rows.append(' } }')
    return '\n'.join(rows)

def body_chain(name, stations, continuity):
    rows=[f'Model {name} {{', ' Units: mm', ' Concept Struct Frames {']
    for i,(z,scale) in enumerate(stations):
        rows.append(f'  S{i}: Plane {{ Origin: [0mm,0mm,{z}mm]; Normal: [0,0,1]; Up: [0,1,0] }}')
    rows.append(' }')
    for i,(z,scale) in enumerate(stations):
        rows.append(f' Construction Plane F{i} {{ Trace: Frames.S{i} }}')
        rows.append(profile(f'P{i}',[(x*scale,(y+25)*scale-25) for x,y in OUTLINE]))
    rows.append(f' SectionChain {name} {{ Continuity: {continuity}')
    for i in range(len(stations)):
        rows.append(f'  Section S{i} {{ Frame: F{i}\n   Profile: P{i}\n   Seam: Edge0 }}')
    rows.extend(['  Start: Cap', '  End: Cap', ' }','}'])
    (ROOT/f'{name}.firmament').write_text('\n'.join(rows)+'\n')

body_chain('MahoganyBack',[(0,1),(38,1)],'G0')
body_chain('IvoryBinding',[(38,1),(40,1)],'G0')
body_chain('CarvedMaple',[(40,.992),(43,.955),(48,.84),(52,.64),(53,.38)],'G1')

# A closed D-section neck: flat fretboard seat, elliptical back, shaped heel and
# a shallow nut transition that actually meets the tilted headstock base.
def neck_profile(name, width, depth):
    r=width/2; k=.55228475
    curves=[((-r,0),(-r/3,0),(r/3,0),(r,0)),
            ((r,0),(r,-k*depth),(k*r,-depth),(0,-depth)),
            ((0,-depth),(-k*r,-depth),(-r,-k*depth),(-r,0))]
    curves=[tuple(reversed(c)) for c in reversed(curves)]
    rows=[]
    for i,curve in enumerate(curves):
        for j,(x,z) in enumerate(curve):
            rows.append(f' Point2 {name}C{i}P{j} {{ Position: [{number(x)}mm,{number(z)}mm] }}')
        rows.append(f' CubicBezier2 {name}C{i} {{ From: {name}C{i}P0; Control1: {name}C{i}P1; Control2: {name}C{i}P2; To: {name}C{i}P3 }}')
    rows.append(f' Profile {name} {{ Loop Outer {{')
    for i in range(3):
        rows.append(f'  Segment Edge{i} {{ Trace: {name}C{i}; From: {name}C{i}P0; To: {name}C{i}P3 }}')
    rows.append(' } }')
    return '\n'.join(rows)

# Collinear shaft stations keep the lengthwise back straight. The extra station
# before y=240 isolates heel fairing from the long shaft derivative.
neck_stations=[(145,40),(195,36),(220,13+2/7),(240,13),(440,13-20/7),(660,7)]
rows=['Model Neck {',' Units: mm',' Concept Struct Frames {']
for i,(y,depth) in enumerate(neck_stations):
    rows.append(f'  S{i}: Plane {{ Origin: [0mm,{y}mm,55mm]; Normal: [0,1,0]; Up: [0,0,1] }}')
rows.append(' }')
for i,(y,depth) in enumerate(neck_stations):
    rows.append(f' Construction Plane F{i} {{ Trace: Frames.S{i} }}')
    width=56-13*(y-120)/540
    rows.append(neck_profile(f'P{i}',width,depth))
rows.append(' SectionChain Neck { Continuity: G1')
for i in range(len(neck_stations)):
    rows.append(f'  Section S{i} {{ Frame: F{i}\n   Profile: P{i}\n   Seam: Edge0 }}')
rows.extend(['  Start: Cap','  End: Cap',' }','}'])
(ROOT/'Neck.firmament').write_text('\n'.join(rows)+'\n')

templates='''// GUITAR-SURFACING-X0. Original design inspired by the single-cut family.
// Every visible product part is compiled Firmament geometry. Finish is downstream.
Template<D: Length, Lead: Length, Tail: Length, Deflection: Angle> Struct GuitarString {
 WireForm String {
  Diameter: D
  Material: Standard.Materials.StainlessSteel.304_Annealed
  StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [0,0,1]; Up: [1,0,0] }
  Straight SpeakingLength { Length: Lead }
  Bend NutBreak { Radius: 3mm; Angle: Deflection; Plane: Up }
  Straight TunerLead { Length: Tail }
 }
}
Template<L: Length, W: Length, H: Length, R: Length> Struct Panel {
 RoundedRect2 Outline { Center: [0mm,0mm] Size: [L,W] Radius: R }
 Profile P { Loop Outer { Outline |> TraceLoop } }
 Extrude Body { Profile: P From: 0mm To: H }
}
Template<R: Length,H: Length> Struct Drum {
 Circle2 Outline { Center: [0mm,0mm] Radius: R }
 Profile P { Loop Outer { Outline |> TraceLoop } }
 Extrude Body { Profile: P From: 0mm To: H }
}
Template<L: Length,W: Length,H: Length> Struct Surround {
 RoundedRect2 Outer { Center: [0mm,0mm] Size: [L,W] Radius: 4mm }
 // Solid decorative seat; the black bobbins sit on it. No routed opening claim.
 Profile P { Loop Outer { Outer |> TraceLoop } }
 Extrude Body { Profile: P From: 0mm To: H }
}
Template<H: Length> Struct Board {
 Point2 RightNut { Position: [21.5mm,660mm] }
 Point2 LeftNut { Position: [-21.5mm,660mm] }
 Concept Path Outline { Start: Point2(-28mm,120mm) Heading: 0deg
  Line End { Length: 56mm }
  Line Right { To: RightNut }
  Line Nut { To: LeftNut }
  Close Left
 }
 Profile P From Outline
 Extrude Body { Profile: P From: 0mm To: H }
}
Template<H: Length> Struct Head {
 Point2 RFlare { Position: [34mm,40mm] }
 Point2 RSide { Position: [39mm,154mm] }
 Point2 RCrown { Position: [12mm,160mm] }
 Point2 LCrown { Position: [-12mm,160mm] }
 Point2 LSide { Position: [-39mm,154mm] }
 Point2 LFlare { Position: [-34mm,40mm] }
 Concept Path Outline { Start: Point2(-21.5mm,0mm) Heading: 0deg
  Line Base { Length: 43mm }
  Line FlareR { To: RFlare }
  Line SideR { To: RSide }
  Line CrownR { To: RCrown }
  Line Crown { To: LCrown }
  Line CrownL { To: LSide }
  Line SideL { To: LFlare }
  Close FlareL
 }
 Profile P From Outline
 Extrude Body { Profile: P From: 0mm To: H }
}
'''
parts=[]
def part(name,definition,x=0,y=0,z=0,angle=0,axis='z'):
    c,s=math.cos(math.radians(angle)),math.sin(math.radians(angle))
    rot=[c,s,0,0,-s,c,0,0,0,0,1,0] if axis=='z' else [1,0,0,0,0,c,s,0,0,-s,c,0]
    matrix=','.join(f'{v:.15g}' for v in rot+[x,y,z,1])
    parts.append(f'  <Part {name} = {definition}>\n   Placement LegacyExplicit = [{matrix}];\n  </Part>')

def formed_string(name, start, nut, end, diameter):
    def subtract(a,b):return [x-y for x,y in zip(a,b)]
    def length(v):return math.sqrt(sum(x*x for x in v))
    lead=subtract(nut,start);tail=subtract(end,nut)
    lead_length,tail_length=length(lead),length(tail)
    zaxis=[v/lead_length for v in lead];target=[v/tail_length for v in tail]
    normal=[zaxis[1]*target[2]-zaxis[2]*target[1],zaxis[2]*target[0]-zaxis[0]*target[2],zaxis[0]*target[1]-zaxis[1]*target[0]]
    xaxis=[v/length(normal) for v in normal]
    yaxis=[zaxis[1]*xaxis[2]-zaxis[2]*xaxis[1],zaxis[2]*xaxis[0]-zaxis[0]*xaxis[2],zaxis[0]*xaxis[1]-zaxis[1]*xaxis[0]]
    angle=math.acos(sum(a*b for a,b in zip(zaxis,target)))
    setback=3*math.tan(angle/2)
    matrix=','.join(f'{v:.15g}' for v in xaxis+[0]+yaxis+[0]+zaxis+[0]+list(start)+[1])
    definition=f'GuitarString<D:{number(diameter)}mm,Lead:{number(lead_length-setback)}mm,Tail:{number(tail_length-setback)}mm,Deflection:{number(math.degrees(angle))}deg>'
    parts.append(f'  <Part {name} = {definition}>\n   Placement LegacyExplicit = [{matrix}];\n  </Part>')
    if name=='String5':
        # The same formed route as a standalone CLI inspection/STEP witness.
        vector=lambda vs:','.join(f'{v:.15g}' for v in vs)
        (ROOT/'string-low-e.firmament').write_text(f'''schema WireForm
Model GuitarLowE {{
 Units: mm
 WireForm String {{
  Diameter: {number(diameter)}mm
  Material: Standard.Materials.StainlessSteel.304_Annealed
  StartFrame {{ Origin: [{','.join(number(v)+'mm' for v in start)}]; Tangent: [{vector(zaxis)}]; Up: [{vector(xaxis)}] }}
  Straight SpeakingLength {{ Length: {number(lead_length-setback)}mm }}
  Bend NutBreak {{ Radius: 3mm; Angle: {number(math.degrees(angle))}deg; Plane: Up }}
  Straight TunerLead {{ Length: {number(tail_length-setback)}mm }}
 }}
}}
''')

for name in ['MahoganyBack','IvoryBinding','CarvedMaple','Neck']:
    part(name,f'SectionChainFile<"{name}.firmament">')
part('BoardBinding','Board<H:5mm>',z=55)
part('Rosewood','Board<H:2mm>',z=60)
part('Headstock','Head<H:14mm>',y=660,z=48,angle=-13,axis='x')
part('HeadVeneer','Head<H:1mm>',y=660+14*math.sin(math.radians(13)),z=48+14*math.cos(math.radians(13)),angle=-13,axis='x')
part('Nut','Panel<L:44mm,W:4mm,H:3mm,R:1mm>',y=658,z=62)
for name,y in [('NeckPickup',98),('BridgePickup',0)]:
    part(name+'Cream','Surround<L:88mm,W:46mm,H:4mm>',y=y,z=53)
    for j,dy in enumerate([-10,10]):
        part(name+f'Coil{j}','Panel<L:70mm,W:17mm,H:5mm,R:3mm>',y=y+dy,z=57)
    for i in range(6):
        part(name+f'Pole{i}','Drum<R:2.2mm,H:1mm>',x=(i-2.5)*10.2,y=y+10,z=62)
part('Bridge','Panel<L:82mm,W:12mm,H:6mm,R:5mm>',y=-35,z=57)
part('Tailpiece','Panel<L:88mm,W:15mm,H:7mm,R:6mm>',y=-75,z=53)
for i in range(6):
    x=(i-2.5)*10.2
    part(f'Saddle{i}','Panel<L:7mm,W:9mm,H:2mm,R:1mm>',x=x,y=-35,z=63)
for i,(x,y,z) in enumerate([(79,-95,50),(123,-70,45),(75,-156,47),(121,-131,43)]):
    part(f'KnobSkirt{i}','Drum<R:12mm,H:3mm>',x,y,z)
    part(f'AmberKnob{i}','Drum<R:9mm,H:9mm>',x,y,z+3)
    part(f'KnobCap{i}','Drum<R:5mm,H:1mm>',x,y,z+12)
part('SelectorRing','Drum<R:16mm,H:1.5mm>',x=-73,y=137,z=43)
part('SelectorStem','Drum<R:2.5mm,H:12mm>',x=-73,y=137,z=44.5)
part('SelectorTip','Drum<R:4mm,H:7mm>',x=-73,y=137,z=54)
part('ControlCover','Panel<L:63mm,W:93mm,H:1mm,R:15mm>',x=85,y=-115,z=-1)
part('SelectorCover','Drum<R:24mm,H:1mm>',x=-73,y=137,z=-1)
for i in range(1,23):
    y=658-628*(1-2**(-i/12))
    width=43+(660-y)/540*13
    part(f'Fret{i}',f'Panel<L:{number(width-1)}mm,W:1.2mm,H:1mm,R:0.5mm>',y=y,z=62)
    if i in [3,5,7,9,12,15,17,19,21]:
        previous=658-628*(1-2**(-(i-1)/12))
        part(f'PearlInlay{i}','Panel<L:25mm,W:12mm,H:0.3mm,R:1mm>',y=(y+previous)/2,z=62)
for side in [-1,1]:
    for i in range(3):
        yy=58+i*36
        y=660+yy*math.cos(math.radians(13))
        z=48-yy*math.sin(math.radians(13))+15*math.cos(math.radians(13))
        part(f'TunerPost{side+1}{i}','Drum<R:4mm,H:8mm>',x=side*27,y=y,z=z)
        part(f'TunerWasher{side+1}{i}','Drum<R:7mm,H:1.5mm>',x=side*27,y=y,z=z)
        part(f'TunerButton{side+1}{i}','Panel<L:15mm,W:19mm,H:6mm,R:5mm>',x=side*49,y=y,z=z-7)
        string_index=i if side==-1 else 5-i
        formed_string(f'String{string_index}',((string_index-2.5)*10.2,-75,65),((string_index-2.5)*7.2,658,65),(side*27,y,z+5),.28+string_index*.08)
assembly=templates+'Assembly GuitarX0 {\n <Assembly GuitarX0>\n'+'\n'.join(parts)+'\n </Assembly>\n Anchor: GuitarX0;\n}\n'
(ROOT/'guitar-x0.firmament').write_text(assembly)
looks={
 'SectionChainFile<"CarvedMaple.firmament">':(.65,.28,.035,0,.35),
 'SectionChainFile<"MahoganyBack.firmament">':(.15,.025,.012,0,.4),
 'SectionChainFile<"IvoryBinding.firmament">':(.72,.59,.36,0,.4),
 'SectionChainFile<"Neck.firmament">':(.15,.025,.012,0,.4),
 'Board<H:5mm>':(.72,.59,.36,0,.4),
 'Board<H:2mm>':(.055,.017,.008,0,.4),
 'Head<H:1mm>':(.008,.008,.008,0,.4),
 'Head<H:14mm>':(.15,.025,.012,0,.4),
 'Panel<L:70mm,W:17mm,H:5mm,R:3mm>':(.009,.007,.006,0,.4),
 'Surround<L:88mm,W:46mm,H:4mm>':(.72,.59,.36,0,.4),
 'Drum<R:9mm,H:9mm>':(.38,.13,.013,.2,.3),
 'Panel<L:25mm,W:12mm,H:0.3mm,R:1mm>':(.74,.73,.62,.1,.3),
}
(ROOT/'preview-materials.json').write_text(json.dumps({k:dict(zip(['red','green','blue','metallic','roughness'],v)) for k,v in looks.items()},indent=2)+'\n')
print(f'Authored {len(parts)} part occurrences in {ROOT}')
