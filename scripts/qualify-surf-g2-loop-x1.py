"""Neutral comparisons from authoritative CLI assembly meshes. No CAD reconstruction."""
import argparse, json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
parser=argparse.ArgumentParser()
parser.add_argument('--out',type=Path,default=Path('artifacts/local/surf-g2-loop-x1'))
out=parser.parse_args().out
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
    size=np.array([1000,780] if "closeup" in label else [850,1050]); scale=min((size-80)/(hi-lo)); center=(hi+lo)/2
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
    draw.text((20,15),label+' | CLI-derived geometry',fill='#30363b')
    im.save(out/(label+'.png'))


for variant in ['before','after','generic']:
    mesh=json.loads((out/(variant+'.mesh.json')).read_text())
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
               else '#33434d' if name=='Display' else '#cdd1d4' if name=='CameraPlateau'
               else '#f3f0df' if name=='Flash' else '#cdd1d4')
        rgb=np.array(tuple(bytes.fromhex(color[1:])))/255
        normals=np.cross(ts[:,1]-ts[:,0],ts[:,2]-ts[:,0])
        lengths=np.linalg.norm(normals,axis=1)
        normals=normals/np.maximum(lengths[:,None],1e-12)
        light=np.array([.3,.4,1.]); light/=np.linalg.norm(light)
        shade=.70+.30*np.abs(normals@light)
        triangles.extend(ts); colors.extend(rgb[None,:]*shade[:,None]); names.extend([name]*len(ts))
    triangles=np.array(triangles); colors=np.array(colors); names=np.array(names)
    

    if variant=='generic':
        render('generic-shaded',[1,-.8,1.8])
        continue
    render(variant+'-rear-three-quarter',[1,-.65,2],[-5,-170,115,15])
    render(variant+'-camera-closeup',[.45,-.45,2],[-2,-59,91,10])
    render(variant+'-side-profile',[1,0,0],[-14,-169,3,3])
    # Intersect display triangles with the identical physical Y plane.
    section=[]
    for triangle,name in zip(triangles,names):
        if name not in ['Body','CameraPlateau']: continue
        points=[]
        for a,b in zip(triangle,np.roll(triangle,-1,axis=0)):
            da=a[1]+23.99; db=b[1]+23.99
            if da*db<0:
                p=a+(b-a)*(-da)/(db-da); points.append(p[[0,2]])
        if len(points)==2: section.append(np.array(points))
    if variant=='after':
        # Exact isoparametric section of the exported operation's surface control net.
        report=json.loads((out/'phone-body-build.json').read_text())['plateau']
        span=next(s for s in report['contacts']['spans'] if s['sourceSpanId']=='Left')
        section=[]
        for patch in span['patches']:
            net=np.array([[[p['x'],p['y'],p['z']] for p in row] for row in patch['controlPoints']])
            controls=net.mean(axis=0) # degree-U one, at u=0.5, Y=-23.99
            samples=[]
            for t in np.linspace(0,1,257):
                q=controls.copy()
                while len(q)>1: q=q[:-1]*(1-t)+q[1:]*t
                samples.append(q[0][[0,2]])
            section.append(np.array(samples))
        section.insert(0,np.array([[0,8.75],section[0][0]]))
        section.append(np.array([section[-1][-1],[12,11.3]]))
    fig,ax=plt.subplots(figsize=(8,4))
    for line in section: ax.plot(line[:,0],line[:,1],color='#475e6b',lw=1.6)
    ax.set(xlim=(0,12),ylim=(7.8,12),xlabel='X from left envelope (mm)',ylabel='Rear Z (mm)',title=variant+' | CAD section at Y=-23.99 mm',aspect='equal')
    ax.grid(alpha=.2);fig.tight_layout();fig.savefig(out/(variant+'-section.png'),dpi=150);plt.close(fig)
for view in ['rear-three-quarter','camera-closeup','side-profile','section']:
    a=Image.open(out/('before-'+view+'.png'));b=Image.open(out/('after-'+view+'.png'))
    result=Image.new('RGB',(a.width+b.width,max(a.height,b.height)),(247,247,247));result.paste(a,(0,0));result.paste(b,(a.width,0));result.save(out/('comparison-'+view+'.png'))
