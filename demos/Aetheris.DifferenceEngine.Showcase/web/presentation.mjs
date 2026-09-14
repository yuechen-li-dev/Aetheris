// A pure projection of authored demonstration states. No mechanism simulation.
const clamp=x=>Math.max(0,Math.min(1,x));
const ease=x=>{x=clamp(x);return x*x*(3-2*x);};
const keys=['result','firstDifference','secondDifference'];
export function evaluate(program,step,phase){
 step=Math.max(0,Math.min(program.states.length-1,Math.floor(step)));
 phase=clamp(phase);
 const before=program.states[step],after=program.states[Math.min(step+1,program.states.length-1)];
 const digits=[],values=[];
 for(let r=0;r<3;r++){
  const increment=(after[keys[r]]-before[keys[r]]+10000)%10000;
  let incoming=0,value=0;
  for(let d=0;d<4;d++){
   const place=10**d,startDigit=Math.floor(before[keys[r]]/place)%10;
   const delta=Math.floor(increment/place)%10+incoming;
   incoming=Math.floor((startDigit+delta)/10);
   const start=r===0?.10+d*.13:.67+d*.045,duration=r===0?.25:.15;
   const progress=ease((phase-start)/duration),position=startDigit+delta*progress;
   const crossing=delta>0?(10-startDigit)/delta:2;
   // The lever follows a rollover, then returns before the next cycle.
   const carry=incoming&&progress>=crossing?Math.sin(Math.PI*clamp((progress-crossing)/Math.max(.001,1-crossing))):0;
   const pulse=incoming?Math.max(carry,Math.sin(Math.PI*clamp((phase-start-duration*.8)/.16))):0;
   digits.push({register:r,digit:d,angle:-position*Math.PI/5,carry:pulse,rollover:incoming>0});
   value+=((Math.round(position)%10+10)%10)*place;
  }
  values.push(value);
 }
 return {step,phase,values,digits,turn:step+phase,complete:step===program.states.length-1};
}
export function hierarchyOffsets(occurrences,explosions){
 const byId=new Map(occurrences.map(o=>[o.id,o])),offsets=new Map(explosions.map(e=>[e.occurrenceId,e.offset]));
 return new Map(occurrences.map(o=>{
  const sum=[0,0,0],seen=new Set();let current=o;
  while(current){if(seen.has(current.id))throw Error('Cyclic occurrence hierarchy');seen.add(current.id);
   const delta=offsets.get(current.id);if(delta)delta.forEach((v,i)=>sum[i]+=v);
   if(current.parentId&&!byId.has(current.parentId))throw Error('Missing occurrence parent');
   current=byId.get(current.parentId);
  }
  return [o.id,sum];
 }));
}
