import { sliderCrank, valveLift } from './prescribed-motion.mjs';
export { sliderCrank, valveLift };
export function evaluate(manifest, binding, degrees) {
  if (!Number.isFinite(degrees) || binding.rest.length!==16 || !binding.rest.every(Number.isFinite)) throw Error('engine-motion-invalid-transform-or-angle');
  if (!['Crank','Cam','Fixed','Rod','Piston','Intake','Exhaust','IntakeSpring','ExhaustSpring'].includes(binding.kind)) throw Error('engine-motion-unknown-kind:'+binding.kind);
  const rest = binding.rest, result = [...rest], angle = degrees * Math.PI / 180;
  if (binding.kind === 'Crank' || binding.kind === 'Cam') {
    const turn = binding.kind === 'Cam' ? angle * manifest.camRatio : angle;
    const s = Math.sin(turn), c = Math.cos(turn);
    for (let i = 0; i < 12; i += 4) {
      result[i] = c * rest[i] + s * rest[i + 1];
      result[i + 1] = -s * rest[i] + c * rest[i + 1];
    }
    if (binding.kind === 'Crank') {
      result[12] = c * rest[12] + s * rest[13]; result[13] = -s * rest[12] + c * rest[13];
    }
    return result;
  }
  if (binding.kind === 'Fixed') return result;
  const cylinder = manifest.cylinders.find(c => c.number === binding.cylinder), spec = manifest.spec;
  const pose = sliderCrank(spec.crankRadius, spec.rodLength, angle, cylinder.bankRadians, cylinder.crankpinRadians);
  if (binding.kind === 'Rod') {
    const s = Math.sin(pose.rodAngle), c = Math.cos(pose.rodAngle);
    return [c,-s,0,0,s,c,0,0,0,0,1,0,...pose.pin.slice(0,2),cylinder.z,1];
  }
  let displacement;
  if (binding.kind === 'Piston')
    displacement = pose.position - sliderCrank(spec.crankRadius, spec.rodLength, 0, cylinder.bankRadians, cylinder.crankpinRadians).position;
  else {
    const opening = binding.kind.startsWith('Intake') ? spec.intakeOpeningDegrees : spec.exhaustOpeningDegrees;
    const lift = valveLift(degrees - cylinder.ignitionDegrees, opening, spec.valveDurationDegrees, spec.valveLift, manifest.cycleDegrees);
    const initial = valveLift(-cylinder.ignitionDegrees, opening, spec.valveDurationDegrees, spec.valveLift, manifest.cycleDegrees);
    if (binding.kind.endsWith('Spring')) {
      const datum=binding.springAxis; if(!datum)throw Error('engine-spring-axis-missing:'+binding.name);
      const apply=(v,point)=>[0,1,2].map(i=>v[0]*rest[i]+v[1]*rest[4+i]+v[2]*rest[8+i]+(point?rest[12+i]:0));
      const origin=apply(datum.localOrigin,true), direction=apply(datum.localDirection,false);
      const norm=Math.hypot(...direction); for(let i=0;i<3;i++)direction[i]/=norm;
      const scale = (spec.springFreeHeight - lift) / (spec.springFreeHeight - initial);
      for(let i=0;i<16;i+=4) {
        const relative=[0,1,2].map(j=>rest[i+j]-(i===12?origin[j]:0));
        const projection=relative.reduce((sum,v,j)=>sum+v*direction[j],0);
        for(let j=0;j<3;j++)result[i+j]+=direction[j]*((scale-1)*projection);
      }
      return result;
    }
    displacement = -(lift - initial);
  }
  result[12] += Math.sin(cylinder.bankRadians) * displacement;
  result[13] += Math.cos(cylinder.bankRadians) * displacement;
  return result;
}
