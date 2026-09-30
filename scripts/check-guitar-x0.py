"""Check the actual exported display mesh; no reconstructed guitar geometry."""
import collections
import json
import sys
from pathlib import Path

root=Path(sys.argv[1] if len(sys.argv)>1 else 'artifacts/local/guitar-x0')
document=json.loads((root/'display.json').read_text())
results=[]
for definition in document['mesh']['definitions']:
    if 'File<' not in definition['identity'] and not definition['identity'].startswith('GuitarString<'):
        continue
    raw=definition['positions']
    points=[tuple(round(v,6) for v in raw[i:i+3]) for i in range(0,len(raw),3)]
    edges=collections.Counter()
    indices=definition['indices']
    normals=definition['normals']
    nonpositive=[]
    for i in range(0,len(indices),3):
        triangle=[points[j] for j in indices[i:i+3]]
        for a,b in zip(triangle,triangle[1:]+triangle[:1]):
            edges[tuple(sorted((a,b)))]+=1
        p=[raw[j*3:j*3+3] for j in indices[i:i+3]]
        a=[x-y for x,y in zip(p[1],p[0])];b=[x-y for x,y in zip(p[2],p[0])]
        cross=[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
        n=normals[indices[i]*3:indices[i]*3+3]
        dot=sum(x*y for x,y in zip(cross,n))
        if dot<=0:nonpositive.append(dict(triangle=i//3,dot=dot,areaSquared=sum(v*v for v in cross)))
    results.append(dict(identity=definition['identity'],pipeline=definition['meshPipeline'],
                        vertices=len(points),triangles=len(indices)//3,
                        unmatchedEdges=sum(v!=2 for v in edges.values()),
                        edgeIncidence=dict(collections.Counter(edges.values())),nonpositiveNormalTriangles=nonpositive))
(root/'mesh-check.json').write_text(json.dumps(results,indent=2)+'\n')
print(json.dumps(results,indent=2))
sys.exit(1 if any(r['unmatchedEdges'] or r['nonpositiveNormalTriangles'] for r in results) else 0)
