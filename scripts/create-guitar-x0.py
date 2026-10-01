"""Author the bounded X0 witness as ordinary Firmament, never as a render mesh.

Run from the repository root. All durable source goes into fixtures; generated
exports and renders belong under artifacts/local/guitar-x0.
"""
from pathlib import Path
import math
import json
import re
from textwrap import dedent, indent

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
    rows=['Include "body-outline.firmament";', f'Model {name} {{', ' Units: mm', ' Concept Struct Frames {']
    for i,(z,scale) in enumerate(stations):
        rows.append(f'  S{i}: Plane {{ Origin: [0mm,0mm,{z}mm]; Normal: [0,0,1]; Up: [0,1,0] }}')
    rows.append(' }')
    rows.append(' Concept Struct SectionLayout {')
    for i,(z,scale) in enumerate(stations):
        rows.append(f'  Curve2 Outline{i} {{ From: BodyOutline; On: XY; Scale: {number(scale)}; Pivot: [0mm,-25mm]; Translate: [0mm,0mm]; Rotate: 0deg }}')
    rows.append(' }')
    for i,(z,scale) in enumerate(stations):
        rows.append(f' Construction Plane F{i} {{ Trace: Frames.S{i} }}')
        rows.append(f' Profile P{i} Using SectionLayout {{ Loop Outer {{ Outline{i} |> TraceLoop }} }}')
    rows.append(f' SectionChain {name} {{ Continuity: {continuity}')
    for i in range(len(stations)):
        rows.append(f'  Section S{i} {{ Frame: F{i}\n   Profile: P{i}\n   Seam: LowerTreble_Span0 }}')
    rows.extend(['  Start: Cap', '  End: Cap', ' }'])
    if name == 'CarvedMaple':
        rows.append(' Expose { Semantic TopSeat { DatumFrame Frame = CarvedMaple.Section.S4.Frame; } }')
    rows.append('}')
    (ROOT/f'{name}.firmament').write_text('\n'.join(rows)+'\n')

def body_outline():
    # Named Concept chords are the closed seed. Pure Profile edits inherit their
    # endpoints; explicit Hermite derivatives preserve the existing silhouette.
    groups = [('Tail', 'LowerTreble', 0), ('TrebleWaist', 'HornRise', 4),
              ('Horn', 'Cutaway', 7), ('NeckSeat', 'Shoulder', 10),
              ('BassShoulder', 'UpperBass', 12), ('BassWaist', 'LowerBass', 14)]
    vector = lambda p: '[' + ','.join(number(v)+'mm' for v in p) + ']'
    rows = ['// Shared named body boundary; no authored spline control cage.',
            'Static Landmarks: Set<Point2> {']
    for landmark, _, index in groups:
        x,y = OUTLINE[index]
        rows.append(f' {landmark} => Point2({number(x)}mm,{number(y)}mm)')
    rows.extend(['}', 'Concept Struct BodyLayout {',
                 ' Polygon2<Explicit> Scaffold { Vertices: Landmarks }', '}',
                 'Profile BodyOutline Using BodyLayout {', ' From: Scaffold'])
    for k,(landmark,edit,start) in enumerate(groups):
        next_landmark,_,end = groups[(k+1)%len(groups)]
        if end <= start: end += len(OUTLINE)
        knots = list(range(start,end+1))
        through = ', '.join(vector(OUTLINE[i%len(OUTLINE)]) for i in knots[1:-1])
        derivatives = []
        for i in knots:
            before,after = OUTLINE[(i-1)%len(OUTLINE)],OUTLINE[(i+1)%len(OUTLINE)]
            derivatives.append(((after[0]-before[0])/2,(after[1]-before[1])/2))
        rows.extend([f' Replace {edit} {{', f'  On: Scaffold.{landmark}_{next_landmark}',
                     f'  Through: [{through}]',
                     '  Derivatives: ['+', '.join(vector(d) for d in derivatives)+']', ' }'])
    rows.append('}')
    return '\n'.join(rows)+'\n'

(ROOT/'body-outline.firmament').write_text(body_outline())
body_chain('MahoganyBack',[(0,1),(38,1)],'G0')
body_chain('IvoryBinding',[(38,1),(40,1)],'G0')
body_chain('CarvedMaple',[(40,.992),(43,.955),(48,.84),(52,.76),(53,.72)],'G1')

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
rows.extend(['  Start: Cap','  End: Cap',' }',
             ' Expose { Semantic NutMount { DatumFrame Frame = Neck.Section.S5.Frame; }',
             ' Semantic HeelMount { DatumFrame Frame = Neck.Section.S0.Frame; } }','}'])
(ROOT/'Neck.firmament').write_text('\n'.join(rows)+'\n')

templates='''// GUITAR-SURFACING-X0. Original design inspired by the single-cut family.
// Every visible product part is compiled Firmament geometry. Finish is downstream.
Template<D: Length, Lead: Length, Tail: Length, Deflection: Angle> Struct GuitarString {
 WireForm String {
  Diameter: D
  Material: Standard.Materials.StainlessSteel.304_Annealed
  StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [0,0,1]; Up: [1,0,0] }
  Straight SpeakingLength { Length: Lead }
  Bend NutBreak { Radius: 4mm; Angle: Deflection; Plane: Up }
  Straight TunerLead { Length: Tail }
 }
}
Template<L: Length, W: Length, H: Length, R: Length> Struct Panel {
 Expose { Semantic BottomSeat { DatumFrame Frame = [0mm,0mm,0mm] x [1,0,0] y [0,1,0] z [0,0,1]; } }
 RoundedRect2 Outline { Center: [0mm,0mm] Size: [L,W] Radius: R }
 Profile P { Loop Outer { Outline |> TraceLoop } }
 Extrude Body { Profile: P From: 0mm To: H }
}
Template<R: Length,H: Length> Struct Drum {
 Expose { Semantic BottomSeat { DatumFrame Frame = [0mm,0mm,0mm] x [1,0,0] y [0,1,0] z [0,0,1]; }
  Semantic SpindleSeat { Axis Axis = [0mm,0mm,0mm] -> [0,0,1];
   DatumFrame Frame = [0mm,0mm,0mm] x [1,0,0] y [0,1,0] z [0,0,1]; } }
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
 Expose { Semantic Base { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
  Semantic Front { DatumFrame Frame = [0mm,0mm,H] x [1,0,0] y [0,1,0] z [0,0,1]; } }
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
    rotate=f' RotateLocal: {{ Axis: {axis.upper()}; Angle: {number(angle)}deg }}' if angle else ''
    translation=','.join(number(v)+'mm' for v in (x,y,z))
    parts.append(f'  <Part {name} = {definition}>\n   Placement {{ From: Origin; To: World; TranslateLocal: [{translation}];{rotate} }}\n  </Part>')

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
    # A 4mm nut bend keeps the revised post leads clear at every string gauge.
    setback=4*math.tan(angle/2)
    definition=f'GuitarString<D:{number(diameter)}mm,Lead:{number(lead_length-setback)}mm,Tail:{number(tail_length-setback)}mm,Deflection:{number(math.degrees(angle))}deg>'
    vector=lambda vs:','.join(f'{v:.15g}' for v in vs)
    parts.append(f'  <Part {name} = {definition}>\n   Placement {{ From: Origin; To: World; TranslateLocal: [{",".join(number(v)+"mm" for v in start)}]; Normal: [{vector(zaxis)}]; Up: [{vector(yaxis)}]; }}\n  </Part>')
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
  Bend NutBreak {{ Radius: 4mm; Angle: {number(math.degrees(angle))}deg; Plane: Up }}
  Straight TunerLead {{ Length: {number(tail_length-setback)}mm }}
 }}
}}
''')

for name in ['MahoganyBack','IvoryBinding','CarvedMaple','Neck']:
    part(name,f'SectionChainFile<"{name}.firmament">')
part('BoardBinding','Board<H:5mm>',z=55)
part('Rosewood','Board<H:2mm>',z=60)
parts.append('''<Part Headstock = Head<H:14mm>>
 Placement { From: Base.Frame; To: HeadTilt.Frame; }
</Part>
<Part HeadVeneer = Head<H:1mm>>
</Part>''')
part('Nut','Panel<L:44mm,W:4mm,H:3mm,R:1mm>',y=658,z=62)
part('Bridge','Panel<L:82mm,W:12mm,H:10mm,R:5mm>',y=-35,z=53)
part('Tailpiece','Panel<L:88mm,W:15mm,H:7mm,R:6mm>',y=-75,z=53)
for i in range(6):
    x=(i-2.5)*10.2
    part(f'Saddle{i}','Panel<L:7mm,W:9mm,H:2mm,R:1mm>',x=x,y=-35,z=63)
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
        y=660+yy*math.cos(math.radians(13))+20*math.sin(math.radians(13))
        z=48-yy*math.sin(math.radians(13))+20*math.cos(math.radians(13))
        string_index=i if side==-1 else 5-i
        formed_string(f'String{string_index}',((string_index-2.5)*10.2,-75,65),((string_index-2.5)*7.2,658,65),(side*27,y,z),.28+string_index*.08)
# Finite keyed occurrence families. Values remain ordinary checked Firmament
# Records; the compiler owns expansion, identities, and definition sharing.
catalog=[]
for family,matcher in [
    ('Saddles',r'Saddle[0-5]')]:
    selected=[]
    for declaration in parts:
        match=re.fullmatch(r'\s*<Part (\w+) = (.+)>\n\s*Placement \{ From: Origin; To: World; TranslateLocal: \[([^,]+),([^,]+),([^]]+)\]; \}\n\s*</Part>',declaration)
        if match and re.fullmatch(matcher,match[1]):
            selected.append((declaration,match))
    assert selected, family
    definitions={m[2] for _,m in selected}
    assert len(definitions)==1, family
    catalog.append(f'Static {family}Sites: Set<HardwareSite> {{\n'+ '\n'.join(
        f' {m[1]} => HardwareSite {{ X: {m[3]}; Y: {m[4]}; Z: {m[5]} }}' for _,m in selected)+'\n}')
    for declaration,_ in selected: parts.remove(declaration)
    leaf=family.removesuffix('s')
    parts.append(f'''Pattern {family} Over {family}Sites {{
 site => <Part {leaf} = {next(iter(definitions))}>
  Placement {{ From: Origin; To: World; TranslateLocal: [site.X,site.Y,site.Z]; }}
 </Part>
}}''')
def write_module(filename, name, members, rows=(), includes=(), relations='', exposes=''):
    prefix=''.join(f'Include "{item}";\n' for item in includes)
    text=prefix+'\n'.join(rows)+f'\nSubassembly {name} {{\n <Assembly {name}>\n'
    text+='\n'.join(indent(dedent(member).strip(),'  ') for member in members)+f'\n </Assembly>\n{relations}\n Anchor: {name};\n'
    if exposes:text+=' Expose { '+exposes+' }\n'
    text+='}\n'
    (ROOT/filename).write_text(text)

groups={name:[] for name in ('Body','Neck','Bridge','Strings')}
for declaration in parts:
    name=re.search(r'(?:<Part|Pattern)\s+(\w+)',declaration)[1]
    if name in ('MahoganyBack','IvoryBinding','CarvedMaple'): group='Body'
    elif name in ('Bridge','Tailpiece','Saddles'):group='Bridge'
    elif name.startswith('String'):group='Strings'
    else:group='Neck'
    groups[group].append(declaration)

def occurrence(name,definition):
    return f'<Assembly {name} = {definition}>\n Placement {{ From: Origin; To: World; }}\n</Assembly>'

def rows_for(prefixes):
    return [row for row in catalog if any(row.startswith('Static '+prefix) for prefix in prefixes)]

# Common geometry definitions are separate from product containment. The string
# definition belongs to its route module rather than the common hardware catalog.
string_end=templates.index('Template<L: Length')
string_template=templates[:string_end]
(ROOT/'hardware-definitions.firmament').write_text(templates[string_end:]+'\nRecord HardwareSite { X: Length; Y: Length; Z: Length }\n')
# Concept layout is authored once and shared by each owning subassembly.
(ROOT/'hardware-layout.firmament').write_text('''Concept Struct GuitarLayout {
 Plane HardwareDeck { From: SectionChainFile<"CarvedMaple.firmament">.TopSeat.Frame; Offset: 0mm; Clocking: 0deg }
 DatumFrame BodySeat { On: HardwareDeck; At: [0mm,0mm]; X: [1,0] }
 DatumFrame NeckPickupSeat { On: HardwareDeck; At: [0mm,98mm]; X: [1,0] }
 DatumFrame BridgePickupSeat { On: HardwareDeck; At: [0mm,0mm]; X: [1,0] }
 DatumFrame BridgeSeat { On: HardwareDeck; At: [0mm,-35mm]; X: [1,0] }
}
''')
groups['Body']=[re.sub(r'\s*Placement\s*\{[^}]*\}', '', p) if '<Part CarvedMaple ' in p else p for p in groups['Body']]
write_module('body-assembly.firmament','GuitarBody',groups['Body'],includes=('hardware-layout.firmament',),relations='''
 Interface<Fixed> BodyDeckSeat { Datum: GuitarLayout.HardwareDeck; Members: [GuitarBody.CarvedMaple.TopSeat] }
 Mate BodyOnDeck: BodyDeckSeat { Member: GuitarBody.CarvedMaple.TopSeat; At: GuitarLayout.BodySeat; Orientation: SameDirection; Support: true; }
''')
# Tuner, knob and selector modules are directly authored Firmament components.
# Preserve their site recipes and local mounting ports rather than generating coordinates.
neck_relations=''' FrameTransform NutWorld { From: GuitarNeck.Neck.NutMount.Frame; Normal: [0,1,0]; Up: [0,0,1]; TranslateLocal: [0mm,-7mm,0mm]; }
 FrameTransform HeadTilt { From: NutWorld.Frame; RotateLocal: { Axis: X; Angle: -13deg } }
 Interface<Fixed> VeneerSeat { A: GuitarNeck.Headstock.Front.Frame; B: GuitarNeck.HeadVeneer.Base.Frame; Gap: 0mm; Clocking: 0deg; }'''
write_module('neck-assembly.firmament','GuitarNeck',groups['Neck']+['''<Assembly Tuners = GuitarTuners>
 // Front includes the 14mm headstock; 1mm seats on its veneer.
 Placement { From: Origin; To: GuitarNeck.Headstock.Front.Frame; TranslateLocal: [0mm,0mm,1mm]; }
</Assembly>'''],
             includes=('tuners-assembly.firmament',),relations=neck_relations,
             exposes='DatumFrame NutMount = Neck.NutMount.Frame; DatumFrame HeelMount = Neck.HeelMount.Frame; DatumFrame HeadFront = Headstock.Front.Frame;')
# Pickup construction is authored in Firmament, not generated pole/coil coordinates.
write_module('pickups-assembly.firmament','GuitarPickups',[
    '<Part NeckPickup = Humbucker<Spec: StandardPickup>></Part>',
    '<Part BridgePickup = Humbucker<Spec: StandardPickup>></Part>'],
    includes=('pickup.firmament','hardware-layout.firmament'), relations="""
 Interface<Fixed> PickupsDeckSeat { Datum: GuitarLayout.HardwareDeck; Members: [GuitarPickups.NeckPickup.Mount,GuitarPickups.BridgePickup.Mount] }
 Mate NeckPickupOnDeck: PickupsDeckSeat { Member: GuitarPickups.NeckPickup.Mount; At: GuitarLayout.NeckPickupSeat; Orientation: SameDirection; }
 Mate BridgePickupOnDeck: PickupsDeckSeat { Member: GuitarPickups.BridgePickup.Mount; At: GuitarLayout.BridgePickupSeat; Orientation: SameDirection; }
""")
pickup_source=(ROOT/'pickup.firmament').read_text()
pickup_source=re.sub(r' Expose \{ Semantic Mount \{[^\n]+\n','',pickup_source)
(ROOT.parents[1]/'Feature'/'pickup-functional.firmament').write_text('schema Mechanical\nModel PickupFunctional {\n Units: mm\n'+pickup_source+'\n Struct Product = Humbucker<Spec: StandardPickup>\n}\n')
# electronics-assembly.firmament owns the named control seats and component occurrences.
groups['Bridge']=[re.sub(r'\s*Placement\s*\{[^}]*\}', '', p) if '<Part Bridge ' in p else p for p in groups['Bridge']]
write_module('bridge-assembly.firmament','GuitarBridge',groups['Bridge'],rows_for(('Saddles',)),includes=('hardware-layout.firmament',),relations='''
 Interface<Fixed> BridgeDeckSeat { Datum: GuitarLayout.HardwareDeck; Members: [GuitarBridge.Bridge.BottomSeat] }
 Mate BridgeOnDeck: BridgeDeckSeat { Member: GuitarBridge.Bridge.BottomSeat; At: GuitarLayout.BridgeSeat; Orientation: SameDirection; }
''')
write_module('strings-assembly.firmament','GuitarStrings',groups['Strings'],rows=(string_template,))
assembly='''// GUITAR-SURFACING-X0: composition root. Geometry and local layout live in modules.
Include "hardware-definitions.firmament";
Include "body-assembly.firmament";
Include "neck-assembly.firmament";
Include "electronics-assembly.firmament";
Include "bridge-assembly.firmament";
Include "strings-assembly.firmament";
Assembly GuitarX0 {
 <Assembly GuitarX0>
'''+ '\n'.join(indent(occurrence(name,'Guitar'+name),'  ') for name in ('Body','Neck','Electronics','Bridge','Strings'))+'''
 </Assembly>
 Anchor: GuitarX0;
}
'''
(ROOT/'guitar.firmasm').write_text(assembly)
# Retain the old fixture entry as a tiny source-compatible composition root.
(ROOT/'guitar-x0.firmament').write_text('// Compatibility entry; prefer guitar.firmasm.\n'+assembly)
looks={
 'SectionChainFile<"CarvedMaple.firmament">':(.65,.28,.035,0,.35),
 'SectionChainFile<"MahoganyBack.firmament">':(.15,.025,.012,0,.4),
 'SectionChainFile<"IvoryBinding.firmament">':(.72,.59,.36,0,.4),
 'SectionChainFile<"Neck.firmament">':(.15,.025,.012,0,.4),
 'Board<H:5mm>':(.72,.59,.36,0,.4),
 'Board<H:2mm>':(.055,.017,.008,0,.4),
 'Head<H:1mm>':(.008,.008,.008,0,.4),
 'Head<H:14mm>':(.15,.025,.012,0,.4),
 'Humbucker<Spec: StandardPickup>':(.025,.025,.025,.15,.3),
 'Drum<R:9mm,H:9mm>':(.38,.13,.013,.2,.3),
 'Panel<L:25mm,W:12mm,H:0.3mm,R:1mm>':(.74,.73,.62,.1,.3),
}
(ROOT/'preview-materials.json').write_text(json.dumps({k:dict(zip(['red','green','blue','metallic','roughness'],v)) for k,v in looks.items()},indent=2)+'\n')
print(f'Authored guitar with four keyed hardware patterns and two feature-built pickups in {ROOT}; inspect via Aetheris.CLI for expanded occurrence counts.')
