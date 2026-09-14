import * as THREE from 'three';
import {OrbitControls} from 'three/addons/OrbitControls.js';
import {RoomEnvironment} from 'three/addons/RoomEnvironment.js';
import {evaluate,hierarchyOffsets} from './presentation.mjs';
const $=id=>document.getElementById(id);
const renderer=new THREE.WebGLRenderer({antialias:true,alpha:false,preserveDrawingBuffer:true});
renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.setClearColor(0x111918);renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.05;$('machine').appendChild(renderer.domElement);
const scene=new THREE.Scene();scene.fog=new THREE.Fog(0x111918,1500,3000);
const pmrem=new THREE.PMREMGenerator(renderer),room=new RoomEnvironment();scene.environment=pmrem.fromScene(room,.04).texture;room.dispose();pmrem.dispose();scene.environmentIntensity=.65;
const camera=new THREE.PerspectiveCamera(33,1,.2,5000);camera.up.set(0,0,1);
const controls=new OrbitControls(camera,renderer.domElement);controls.enableDamping=true;controls.dampingFactor=.075;
scene.add(new THREE.HemisphereLight(0xdbe9e0,0x233932,1.3));
for(const [color,power,x,y,z] of [[0xffe3ad,3.8,-300,-400,800],[0xd9eaff,2.4,400,200,600],[0xffd28a,1.8,100,400,250]]){const light=new THREE.DirectionalLight(color,power);light.position.set(x,y,z);scene.add(light);}
const ground=new THREE.Mesh(new THREE.PlaneGeometry(20000,20000),new THREE.MeshStandardMaterial({color:0x070d09,roughness:1}));ground.position.z=-37;scene.add(ground);
const nodes=new Map(),definitions=new Map(),labels=new Map();let doc,manifest,receipts,offsets;
try{
 [doc,manifest,receipts]=await Promise.all(['machine.mesh.json','motion.json','receipts.json'].map(async f=>{const r=await fetch(f);if(!r.ok)throw Error(`${f}: HTTP ${r.status}`);return r.json();}));
 offsets=hierarchyOffsets(doc.occurrences,manifest.explosions);
 const bindings=new Map(manifest.bindings.map(b=>[b.occurrenceId,b]));
 for(const d of doc.definitions){const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.Float32BufferAttribute(d.positions,3));g.setAttribute('normal',new THREE.Float32BufferAttribute(d.normals,3));g.setIndex(d.indices);g.computeBoundingSphere();definitions.set(d.id,g);}
 for(const occurrence of doc.occurrences){if(!occurrence.definitionId)continue;
  const path=occurrence.path,name=path.split('.').at(-1);let color=0xbca16a,metalness=.82,roughness=.27;
  if(path.includes('.Frame.')){color=name==='Plaque'?0xc49b52:0x233b32;metalness=.62;roughness=.37;}
  if(/Shaft|Axle|Pivot|Tie|Pin|Link|Collar|Bearing/.test(name)){color=0x9aa6a4;metalness=.9;roughness=.28;}
  if(name==='Drum'){color=0xe8d7ad;metalness=.45;roughness=.37;}
  const material=new THREE.MeshStandardMaterial({color,metalness,roughness});
  const mesh=new THREE.Mesh(definitions.get(occurrence.definitionId),material);mesh.name=occurrence.id;mesh.matrix.fromArray(occurrence.transform);mesh.matrixAutoUpdate=false;mesh.userData={occurrence,rest:mesh.matrix.clone(),binding:bindings.get(occurrence.id)};scene.add(mesh);nodes.set(occurrence.id,mesh);
 }
 // Text is a presentation overlay. All mechanical geometry above is exported CAD.
 const digitMaterials=Array.from({length:10},(_,n)=>{const canvas=document.createElement('canvas');canvas.width=128;canvas.height=128;const c=canvas.getContext('2d');c.fillStyle='#2f291b';c.font='92px Georgia';c.textAlign='center';c.textBaseline='middle';c.fillText(String(n),64,67);const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;return new THREE.MeshBasicMaterial({map:texture,transparent:true,alphaTest:.15,depthWrite:false});});
 const labelPlane=new THREE.PlaneGeometry(14,14);
 for(const drum of manifest.drums){const group=new THREE.Group();group.matrixAutoUpdate=false;
  for(let n=0;n<10;n++){const theta=-Math.PI/2+n*Math.PI/5,label=new THREE.Mesh(labelPlane,digitMaterials[n]);label.position.set(drum.radiusMm*Math.cos(theta),drum.radiusMm*Math.sin(theta),drum.labelZMm);label.quaternion.setFromRotationMatrix(new THREE.Matrix4().makeBasis(new THREE.Vector3(-Math.sin(theta),Math.cos(theta),0),new THREE.Vector3(0,0,1),new THREE.Vector3(Math.cos(theta),Math.sin(theta),0)));group.add(label);}
  scene.add(group);labels.set(drum.occurrenceId,group);
 }
 $('receipt').textContent=`${receipts.physicalOccurrences} parts · ${receipts.uniqueDefinitions} shared bodies · Aetheris CAD`;
 const fields=[[receipts.physicalOccurrences,'PHYSICAL PARTS'],[receipts.uniqueDefinitions,'SHARED DEFINITIONS'],[receipts.gears,'SPUR GEARS'],[receipts.ratchets,'RATCHETS'],[receipts.pawls,'PAWLS'],[receipts.sharedModules.reduce((n,m)=>n+m.occurrences,0),'SUBASSEMBLY OCCURRENCES'],[(receipts.stepBytes/1e6).toFixed(2)+' MB','AP242 STEP'],[receipts.aetherisVersion,'AETHERIS VERSION']];
 for(const [value,label] of fields){const el=document.createElement('div');el.textContent=value;const small=document.createElement('small');small.textContent=label;el.append(small);$('inventory').append(el);}
 $('fingerprint').textContent='STEP SHA-256: '+receipts.receipts.find(r=>r.file==='difference-engine.step').sha256;
 $('loading').textContent='';
}catch(error){$('loading').textContent=`Assembly could not load: ${error.message}`;throw error;}
let program=manifest.programs[0],step=0,phase=0,playing=!matchMedia('(prefers-reduced-motion: reduce)').matches,singleTurn=false,speed=1,exploded=false,explosion=0,filter='full',destination=null,currentView='hero';
function playLabel(){$('play').textContent=playing?'Pause':'Play';}
function reset(){step=0;phase=0;singleTurn=false;}
$('play').onclick=()=>{if(!playing&&step>=program.states.length-1)reset();playing=!playing;singleTurn=false;playLabel();};
$('step').onclick=()=>{if(step>=program.states.length-1)reset();playing=true;singleTurn=true;playLabel();};
$('reset').onclick=()=>{reset();playing=false;playLabel();};
$('speed').onchange=e=>speed=Number(e.target.value);
$('program').onchange=e=>{program=manifest.programs.find(p=>p.id===e.target.value);reset();if(program.id==='carry'){view('carry');speed=.5;$('speed').value='.5';$('speed').value='0.5';}playing=true;playLabel();};
$('inspect').onclick=()=>{const open=$('inspection').hidden;$('inspection').hidden=!open;$('inspect').setAttribute('aria-expanded',String(open));};
function visibility(){for(const mesh of nodes.values()){const b=mesh.userData.binding;
 mesh.visible=filter==='register'?b.register===0:filter==='digit'?b.register===0&&b.digit===1:filter==='gears'?['SpurGear','RatchetGear','Pawl'].includes(b.role):filter==='carry'?b.register===0&&(b.digit===0||b.digit===1||b.digit===2):true;
 const transparent=filter==='transparent'&&b.role==='Frame';
 if(mesh.material.transparent!==transparent){mesh.material.transparent=transparent;mesh.material.needsUpdate=true;}
 mesh.material.opacity=transparent ? .13 : 1;mesh.material.depthWrite=!transparent;
 }for(const [id,label] of labels)label.visible=nodes.get(id).visible;}
$('filter').onchange=e=>{filter=e.target.value;visibility();};
function setExplosion(value){exploded=value;$('explode').setAttribute('aria-pressed',String(value));$('explode').textContent=value?'Reassemble':'Explode subassemblies';}
$('explode').onclick=()=>{setExplosion(!exploded);if(exploded)view('exploded');};
$('camera').onchange=e=>view(e.target.value);
function view(name,instant=false){currentView=name;document.body.dataset.inspecting=String(!['hero','full'].includes(name));const z=manifest.design.firstLevel,pitch=manifest.design.digitPitch,top=manifest.design.top;
 filter=name==='register'?'register':name==='digit'?'digit':name==='carry'?'carry':'full';$('filter').value=filter;visibility();setExplosion(name==='exploded');$('camera').value=name;
 const views={hero:[[850,-1350,760],[75,0,top*.48]],full:[[850,-1350,760],[75,0,top*.48]],front:[[115,-1260,220],[115,0,top*.48]],side:[[1150,20,240],[110,0,top*.48]],register:[[210,-500,370],[0,0,top*.55]],digit:[[160,-255,z+pitch+155],[0,0,z+pitch+25]],carry:[[210,-340,z+pitch+165],[20,5,z+pitch+20]],exploded:[[1060,-1640,950],[80,0,top*.51]]};
 const [position,target]=views[name]??views.hero;destination={position:new THREE.Vector3(...position),target:new THREE.Vector3(...target)};
 if(name==='register'||name==='carry')destination.position.sub(destination.target).multiplyScalar(1.35).add(destination.target);
 if(innerWidth<=700){const scale=['digit','carry','register'].includes(name)?1.35:1.8;destination.position.sub(destination.target).multiplyScalar(scale).add(destination.target);destination.target.z+=55;camera.clearViewOffset();}
 else {const scale=innerWidth/innerHeight>1.4?(['hero','full'].includes(name)?.78:name==='exploded'?.88:1):1;destination.position.sub(destination.target).multiplyScalar(scale).add(destination.target);camera.setViewOffset(innerWidth,innerHeight,-innerWidth*.11,0,innerWidth,innerHeight);}
 document.body.dataset.cameraSettled='false';
 if(instant){camera.position.copy(destination.position);controls.target.copy(destination.target);destination=null;document.body.dataset.cameraSettled='true';}
}
controls.addEventListener('start',()=>{destination=null;document.body.dataset.cameraSettled='true';});
for(const button of document.querySelectorAll('[data-dialog]'))button.onclick=()=>{$(button.dataset.dialog).showModal();};
for(const dialog of document.querySelectorAll('dialog')){dialog.querySelector('.close').onclick=()=>dialog.close();dialog.addEventListener('click',e=>{if(e.target===dialog){const r=dialog.getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)dialog.close();}});}
function resize(){renderer.setSize(innerWidth,innerHeight);camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();scene.fog.near=innerWidth<=700?3500:1500;scene.fog.far=innerWidth<=700?7000:3000;view(currentView,true);}window.addEventListener('resize',resize);resize();playLabel();
const rotation=new THREE.Matrix4();
function apply(p){for(const mesh of nodes.values()){const b=mesh.userData.binding,d=b.register>=0&&b.digit>=0?p.digits[b.register*4+b.digit]:null;
 let angle=b.kind==='GearRatio'?p.turn*Math.PI*2*b.ratio:b.kind==='IndexedWheel'?d.angle:b.kind==='CarryLever'?d.carry*.23:0;
 mesh.matrix.copy(mesh.userData.rest);
 if(angle!==0){const [x,y,z]=b.pivot;rotation.makeRotationZ(angle);rotation.setPosition(x-Math.cos(angle)*x+Math.sin(angle)*y,y-Math.sin(angle)*x-Math.cos(angle)*y,0);mesh.matrix.premultiply(rotation);}
 if(b.kind==='CarryLift')mesh.matrix.elements[14]+=d.carry*5;
 const offset=offsets.get(b.occurrenceId);for(let i=0;i<3;i++)mesh.matrix.elements[12+i]+=offset[i]*explosion;
 mesh.matrixWorldNeedsUpdate=true;
 if(b.kind==='CarryLever'){mesh.material.emissive.setHex(0xb66e1d);mesh.material.emissiveIntensity=d.carry*.55;}
 }for(const [id,label] of labels){label.matrix.copy(nodes.get(id).matrix);label.matrixWorldNeedsUpdate=true;}}
let previous=performance.now(),lastUi='';
function frame(now){requestAnimationFrame(frame);const dt=Math.min((now-previous)/1000,.08);previous=now;
 if(playing&&!document.hidden&&!document.querySelector('dialog[open]')){phase+=dt*speed/manifest.cycleSeconds;if(phase>=1){phase=0;step++;if(step>=program.states.length-1){playing=false;phase=0;}if(singleTurn){playing=false;singleTurn=false;}playLabel();}}
 const p=evaluate(program,step,phase);explosion+=(Number(exploded)-explosion)*.08;if(Math.abs(explosion-Number(exploded))<.0001)explosion=Number(exploded);apply(p);
 const key=p.values.join(',')+step+Math.floor(phase*10);if(key!==lastUi){['result','first','second'].forEach((id,i)=>$(id).textContent=String(p.values[i]).padStart(4,'0'));$('step-count').textContent=`TURN ${step} / ${program.states.length-1}`;
 const before=program.states[step],after=program.states[Math.min(step+1,program.states.length-1)];$('equation').textContent=p.complete?`Result ${String(before.result).padStart(4,'0')}`:`${before.result} + ${before.firstDifference} → ${after.result}`;
 $('stage').textContent=p.complete?'Program complete. Reset to play again.':p.digits.some(d=>d.carry>.15)?'Carry: a rollover advances the next digit.':phase>.67?'Advance the first difference.':'Add the first difference to the result.';lastUi=key;}
 if(destination){camera.position.lerp(destination.position,.075);controls.target.lerp(destination.target,.075);if(camera.position.distanceTo(destination.position)<.05){camera.position.copy(destination.position);controls.target.copy(destination.target);destination=null;document.body.dataset.cameraSettled='true';}}
 controls.update();renderer.render(scene,camera);
 if(!document.body.dataset.readyMs)document.body.dataset.readyMs=String(Math.round(performance.now()));
}requestAnimationFrame(frame);

