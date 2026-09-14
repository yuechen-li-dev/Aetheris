"""Drawing fit and neutral previews from the CLI's derived assembly mesh (never CAD authority)."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

parser = argparse.ArgumentParser()
parser.add_argument('--out', type=Path, default=Path('artifacts/local/iphone-recon-x0'))
args = parser.parse_args()
out = args.out
out.mkdir(parents=True, exist_ok=True)

# PDF p2 Detail A. Positive distances inward from the top-left envelope corner.
x = np.array([0, .04, .92, 3.80, 8.46, 13.90, 19.43])
y = x[::-1].copy()
extent = 19.43
def fit_y(x, n):
    return extent * (1 - np.maximum(0, 1 - (1 - np.asarray(x)/extent)**n)**(1/n))
def loss(n):
    return np.mean((fit_y(x, n)-y)**2)
# Bounded golden-section minimization of a single continuous fit parameter.
lo, hi = 2.01, 5.0
phi = (5**.5-1)/2
for _ in range(100):
    a, b = hi-phi*(hi-lo), lo+phi*(hi-lo)
    if loss(a) < loss(b): hi=b
    else: lo=a
n=(lo+hi)/2
predicted = fit_y(x, n)
errors = predicted-y
result = {'family': '(1-x/L)^n + (1-y/L)^n = 1', 'extentMm': extent,
          'exponent': float(n), 'metric': 'vertical ordinate deviation at published X; not normal distance',
          'maxDeviationMm': float(max(abs(errors))), 'rmsDeviationMm': float(np.sqrt(loss(n))),
          'continuity': 'Analytic family G2 to straight sides for n>2; not materialized in BRep',
          'samples': [dict(xMm=float(a), yMm=float(b), fittedYMm=float(c), deviationMm=float(d))
                      for a,b,c,d in zip(x,y,predicted,errors)]}
(out/'corner-fit.json').write_text(json.dumps(result, indent=2)+'\n')
t = np.linspace(0, extent, 800)
r = 13.9
circle = np.where(t<r, r-np.sqrt(np.maximum(0,r*r-(t-r)**2)), 0)
fig, ax = plt.subplots(figsize=(6,6))
ax.plot(t, circle, label='Blockout: R13.9 circular corner', color='#888888')
ax.plot(t, fit_y(t,n), label=f'Analytic candidate: n={n:.5f}', color='#12689a')
ax.scatter(x,y,color='#b74a32',label='Detail A paired ordinates', zorder=5)
ax.set(xlabel='X inward from left envelope (mm)', ylabel='Distance below top envelope (mm)',
       title='Corner fit candidate - not a refined CAD body', aspect='equal')
ax.invert_yaxis(); ax.grid(alpha=.2); ax.legend(fontsize=8)
fig.tight_layout(); fig.savefig(out/'corner-fit.png',dpi=160); plt.close(fig)

mesh=json.loads((out/'blockout.mesh.json').read_text())
definitions={d['id']: d for d in mesh['definitions']}
triangles=[]; colors=[]; names=[]
for occurrence in mesh['occurrences']:
    if occurrence.get('definitionId') not in definitions: continue
    d=definitions[occurrence['definitionId']]
    v=np.array(d['positions']).reshape(-1,3)
    m=np.array(occurrence['transform']).reshape(4,4)
    v=(np.c_[v,np.ones(len(v))]@m)[:,:3]
    ts=v[np.array(d['indices']).reshape(-1,3)]
    name=occurrence['path'].split('.')[-1]
    color=('#25323b' if name.startswith('RearCamera') or name in ['RearSensor','RearMic','FrontSensor'] or 'Proxy' in name
           else '#33434d' if name=='Display' else '#b8bfc4' if name=='CameraPlateau'
           else '#f3f0df' if name=='Flash' else '#cdd1d4')
    rgb=np.array(tuple(bytes.fromhex(color[1:])))/255
    normals=np.cross(ts[:,1]-ts[:,0],ts[:,2]-ts[:,0])
    lengths=np.linalg.norm(normals,axis=1)
    normals=normals/np.maximum(lengths[:,None],1e-12)
    light=np.array([.3,.4,1.]); light/=np.linalg.norm(light)
    shade=.70+.30*np.abs(normals@light)
    triangles.extend(ts); colors.extend(rgb[None,:]*shade[:,None]); names.extend([name]*len(ts))
triangles=np.array(triangles); colors=np.array(colors); names=np.array(names)

def render(label, direction, crop=None, front=False):
    forward=np.array(direction,dtype=float); forward/=np.linalg.norm(forward)
    up=np.array([0.,1.,0.])
    right=np.cross(up,forward); right/=np.linalg.norm(right)
    up=np.cross(forward,right)
    tri=triangles.copy(); col=colors.copy()
    # Front component slabs overlap by design in this blockout. Show their exposed
    # front faces in declared display order; all vertices still come from CLI geometry.
    mask=np.ones(len(tri),dtype=bool)
    if front:
        mask &= ~np.isin(names,['CameraPlateau','RearCamera1','RearCamera2','RearCamera3','Flash','RearMic','RearSensor'])
    tri=tri[mask]; col=col[mask]; ns=names[mask]
    q=tri@np.array([right,up,forward]).T
    lo=q[:,:,:2].min(axis=(0,1)); hi=q[:,:,:2].max(axis=(0,1))
    if crop is not None: lo=np.array(crop[:2]); hi=np.array(crop[2:])
    size=np.array([850,1050]); scale=min((size-80)/(hi-lo)); center=(hi+lo)/2
    q[:,:,:2]=(q[:,:,:2]-center)*scale+size/2
    q[:,:,1]=size[1]-q[:,:,1]
    depth=np.full((size[1],size[0]),-np.inf); pixels=np.full((size[1],size[0],3),247,dtype=np.uint8)
    for t,c,name in zip(q,col,ns):
        mn=np.maximum(0,np.floor(t[:,:2].min(axis=0)).astype(int)); mx=np.minimum(size-1,np.ceil(t[:,:2].max(axis=0)).astype(int))
        if np.any(mx<mn): continue
        a,b,d=t
        den=(b[1]-d[1])*(a[0]-d[0])+(d[0]-b[0])*(a[1]-d[1])
        if abs(den)<1e-9: continue
        xx,yy=np.meshgrid(np.arange(mn[0],mx[0]+1)+.5,np.arange(mn[1],mx[1]+1)+.5)
        u=((b[1]-d[1])*(xx-d[0])+(d[0]-b[0])*(yy-d[1]))/den
        v=((d[1]-a[1])*(xx-d[0])+(a[0]-d[0])*(yy-d[1]))/den
        w=1-u-v; z=u*a[2]+v*b[2]+w*d[2]
        if front: z=z+{'FrontGlass':.001,'Display':.002,'FrontSensor':.003}.get(name,0)
        region=depth[mn[1]:mx[1]+1,mn[0]:mx[0]+1]
        visible=(u>=0)&(v>=0)&(w>=0)&(z>region)
        region[visible]=z[visible]
        pixels[mn[1]:mx[1]+1,mn[0]:mx[0]+1][visible]=np.clip(c*255,0,255).astype(np.uint8)
    im=Image.fromarray(pixels); draw=ImageDraw.Draw(im)
    draw.text((20,15),'PASS A | '+label+' | CLI-derived geometry',fill='#30363b')
    im.save(out/('blockout-'+label+'.png'))

render('rear',[0,0,1]); render('front',[0,0,-1],front=True)
render('side',[1,0,0]); render('three-quarter',[1,-.65,2])
render('camera-closeup',[0,0,1],[-3,-51,81,3])
render('corner-closeup',[0,0,1],[-2,-23,23,2])
print(json.dumps(result,indent=2))
