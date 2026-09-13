import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const directory=path.resolve(process.argv[2]??'artifacts/local/demos/editable-v8');
const {evaluate}=await import(pathToFileURL(path.join(directory,'engine-motion.mjs')));
const reports=[];
for(const label of ['baseline','revised']) {
  const read=kind=>JSON.parse(fs.readFileSync(path.join(directory,`${label}.${kind}.json`),'utf8'));
  const motion=read('motion'), mesh=read('mesh'), samples=read('samples');
  const objects=new Map(mesh.occurrences.filter(o=>o.definitionId).map(o=>[o.id,o]));
  const bindings=new Map(motion.bindings.map(b=>[b.occurrenceId,b]));
  if(objects.size!==bindings.size||bindings.size!==motion.bindings.length)throw Error('mesh-binding-count-mismatch');
  for(const b of bindings.values()) if(!objects.has(b.occurrenceId))throw Error(`missing-object:${b.name}`);
  let maximum=0, compared=0;
  for(const sample of samples)for(const expected of sample.transforms) {
    const actual=evaluate(motion,bindings.get(expected.id),sample.degrees);
    actual.forEach((value,i)=>{if(!Number.isFinite(value))throw Error('nonfinite-transform');maximum=Math.max(maximum,Math.abs(value-expected.matrix[i]));compared++;});
  }
  if(maximum>1e-9)throw Error(`browser-transform-parity:${maximum}`);
  for(const name of ['Piston3','Crankpin1','IntakeValve3']) {
    const b=motion.bindings.find(b=>b.name===name), occurrence=objects.get(b.occurrenceId);
    if(!mesh.definitions.some(d=>d.id===occurrence.definitionId))throw Error(`missing-mesh:${name}`);
  }
  reports.push({label,compared,maximumError:maximum,tolerance:1e-9,objects:objects.size,status:'pass'});
}
fs.writeFileSync(path.join(directory,'browser-parity.json'),JSON.stringify(reports,null,2));
console.log(JSON.stringify(reports,null,2));
