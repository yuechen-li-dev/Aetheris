import { describe, it, expect, vi } from 'vitest';
import { MeasurementTool, bindMeasurementPicking, measurementLines, type MeasurementRequest, type MeasurementResult } from '@aetheris/three-telos';
const pick = {occurrenceId:'part', faceId:1, rayOrigin:[0,0,10],rayDirection:[0,0,-1]};
const answer = (request: MeasurementRequest): MeasurementResult => ({mode:request.mode,first:{occurrenceId:'part',faceId:1,surfaceKind:'Plane',point:[0,0,0],quality:'Analytic BRep'},second:request.second ? {occurrenceId:'part',faceId:2,surfaceKind:'Plane',point:[0,0,6],quality:'Analytic BRep'}:null,distanceMm:request.second?6:null,deltaMm:request.second?[0,0,6]:null,witnessA:[0,0,0],witnessB:request.second?[0,0,6]:null,angleDegrees:null,quality:'Analytic BRep',explanation:'picked points'});
function setup(query:(request:MeasurementRequest)=>Promise<MeasurementResult>) {
  const root=document.createElement('div'),host=document.createElement('div');root.append(host);document.body.append(root);
  const tool = new MeasurementTool(host,query,()=>{});tool.setContext('revision-1');host.querySelector('button')!.click();
  return {root,host,tool,close:()=>{tool.dispose();root.remove();}};
}
describe('shared measurement interaction',()=>{
  it('advances A to B, replaces either reference, converts display units, and clears',async()=>{
    const query=vi.fn(async(request:MeasurementRequest)=>answer(request)), {tool,host,close}=setup(query);
    await tool.pick(pick);expect(host.textContent).toContain('Pick B');expect(tool.result?.distanceMm).toBeNull();
    await tool.pick({...pick,faceId:2});expect(tool.result?.distanceMm).toBe(6);expect(host.textContent).toContain('ΔZ 6 mm');
    Array.from(host.querySelectorAll('button')).find(b=>b.textContent?.includes('A:'))!.click();
    await tool.pick({...pick,faceId:3});expect(query.mock.lastCall![0].first.faceId).toBe(3);expect(query.mock.lastCall![0].second?.faceId).toBe(2);
    Array.from(host.querySelectorAll('button')).find(b=>b.textContent==='Units: mm')!.click();expect(host.textContent).toContain('0.23622 in');
    tool.setContext('revision-2');expect(tool.result).toBeNull();expect(host.textContent).not.toContain('Distance:');
    tool.setContext('revision-3','Build the source before measuring.');expect(host.querySelector('button')!.disabled).toBe(false);
    host.querySelector('button')!.click();expect(tool.active).toBe(false);expect(host.querySelector('button')!.disabled).toBe(true);close();
  });
  it('keeps rapid clicks as A and B while the first reply is pending',async()=>{
    const replies: Array<()=>void> = [];
    const query=vi.fn((request:MeasurementRequest)=>new Promise<MeasurementResult>(resolve=>replies.push(()=>resolve(answer(request)))));
    const {tool,close}=setup(query);
    const a=tool.pick(pick), b=tool.pick({...pick,faceId:2});
    expect(query.mock.lastCall![0].first.faceId).toBe(1);expect(query.mock.lastCall![0].second?.faceId).toBe(2);
    replies[1]!();await b;replies[0]!();await a;
    expect(tool.result?.distanceMm).toBe(6);expect(tool.result?.second?.faceId).toBe(2);close();
  });
  it('discards a late reply after rebuild and removes an old number on query failure',async()=>{
    let resolve!:(result:MeasurementResult)=>void;
    const {tool,host,close}=setup(()=>new Promise(r=>{resolve=r;}));
    const pending=tool.pick(pick);tool.setContext('revision-2');resolve(answer({mode:'points',first:pick,allowApproximate:false}));await pending;
    expect(tool.result).toBeNull();expect(host.textContent).not.toContain('Distance:');close();
    let fail=false;const next=setup(async r=>{if(fail)throw new Error('unsupported trim');return answer(r);});
    await next.tool.pick(pick);await next.tool.pick(pick);expect(next.tool.result?.distanceMm).toBe(6);fail=true;await next.tool.pick(pick);
    expect(next.tool.result).toBeNull();expect(next.host.textContent).toContain('unsupported trim');expect(next.host.textContent).not.toContain('Distance:');next.close();
  });
  it('does not turn an orbit drag into a pick; witness guides never intercept picks',async()=>{
    const query=vi.fn(async r=>answer(r)),{tool,root,close}=setup(query);const canvas=document.createElement('canvas');root.append(canvas);
    const unbind=bindMeasurementPicking(canvas,()=>tool,()=>pick);
    const pointer=(type:string,x:number)=>canvas.dispatchEvent(new MouseEvent(type,{clientX:x,clientY:20,button:0,bubbles:true}));
    pointer('pointerdown',10);pointer('pointermove',50);pointer('click',50);expect(query).not.toHaveBeenCalled();
    pointer('pointerdown',10);pointer('click',10);await Promise.resolve();expect(query).toHaveBeenCalledOnce();
    const guides=measurementLines(answer({mode:'points',first:pick,second:pick,allowApproximate:false}));
    expect(guides.length).toBe(7);expect(guides.every(g=>g.pickable===false && g.points.length===6)).toBe(true);unbind();close();
  });
});
