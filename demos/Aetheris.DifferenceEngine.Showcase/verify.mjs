import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
import {evaluate,hierarchyOffsets} from './web/presentation.mjs';
const root=process.argv[2]??'artifacts/local/demos/difference-engine-showcase';
const read=f=>JSON.parse(readFileSync(`${root}/${f}`,'utf8'));
const manifest=read('motion.json'),mesh=read('machine.mesh.json'),receipts=read('receipts.json');
let transitions=0;
for(const program of manifest.programs)for(let step=0;step<program.states.length-1;step++){
 const start=evaluate(program,step,0),end=evaluate(program,step,1),next=program.states[step+1];
 assert.deepEqual(start.values,Object.values(program.states[step]));
 assert.deepEqual(end.values,Object.values(next));
 for(const phase of [0,.13,.28,.45,.6,.79,.93,1]){
  const p=evaluate(program,step,phase);assert.deepEqual(p,evaluate(program,step,phase));
  assert.ok(p.digits.every(d=>Number.isFinite(d.angle)&&d.carry>=0&&d.carry<=1));
 }
 transitions++;
}
const carry=manifest.programs.find(p=>p.id==='carry');
const pulses=[0,1].map(d=>Array.from({length:1001},(_,i)=>({t:i/1000,p:evaluate(carry,0,i/1000).digits[d].carry})).find(s=>s.p>.1).t);
assert.ok(pulses[0]<pulses[1],'Units carry must precede tens carry');
assert.deepEqual(evaluate(carry,0,1).values,[100,1,0]);
const bindings=new Map(manifest.bindings.map(b=>[b.occurrenceId,b]));
for(const link of manifest.gearLinks){
 assert.ok(Math.abs(link.ratio+link.teethA/link.teethB)<1e-10);
 assert.ok(Math.abs(bindings.get(link.b).ratio-bindings.get(link.a).ratio*link.ratio)<1e-10);
}
const offsets=hierarchyOffsets(mesh.occurrences,manifest.explosions);
assert.deepEqual(offsets.get('assembly-instance:DifferenceEngine.ResultRegister.Digit0.Wheel.Drum'),[-90,0,-30]);
assert.equal(new Set(manifest.bindings.map(b=>b.occurrenceId)).size,mesh.occurrences.filter(o=>o.definitionId).length);
assert.equal(manifest.drums.length,12);
for(const c of receipts.gantryClearances){assert.equal(c.clearanceMm,10);assert.equal(c.shaftProjectionMm,4);}
for(const definition of mesh.definitions){
 const p=definition.positions,n=definition.normals,ix=definition.indices;
 assert.ok(p.every(Number.isFinite));assert.ok(n.every(Number.isFinite));assert.ok(ix.every(i=>i>=0&&i<p.length/3));
 for(let axis=0;axis<3;axis++){const coordinates=p.filter((_,i)=>i%3===axis);assert.ok(Math.max(...coordinates)>Math.min(...coordinates),'Positive display extent');}
 for(let i=0;i<n.length;i+=3)assert.ok(Math.abs(Math.hypot(n[i],n[i+1],n[i+2])-1)<1e-9,'Unit surface normal');
 let volume=0;
 for(let i=0;i<ix.length;i+=3){const a=ix[i]*3,b=ix[i+1]*3,c=ix[i+2]*3;
  volume+=(p[a]*(p[b+1]*p[c+2]-p[b+2]*p[c+1])+p[a+1]*(p[b+2]*p[c]-p[b]*p[c+2])+p[a+2]*(p[b]*p[c+1]-p[b+1]*p[c]))/6;
 }
 assert.ok(volume>0,'Positive oriented display volume: '+definition.identity);
}
console.log(JSON.stringify({passed:true,transitions,gearLinks:manifest.gearLinks.length,carryPulseTimes:pulses,occurrences:bindings.size,gantryClearances:receipts.gantryClearances.map(c=>c.clearanceMm)}));
