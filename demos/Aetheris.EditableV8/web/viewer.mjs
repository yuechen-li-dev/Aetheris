import * as THREE from 'three';
import { OrbitControls } from 'three/addons/OrbitControls.js';
import { evaluate, sliderCrank, valveLift } from './engine-motion.mjs';
const $ = id => document.getElementById(id);
const scene = new THREE.Scene(); scene.background = new THREE.Color('#e8e9e6');
const camera = new THREE.PerspectiveCamera(35, innerWidth / innerHeight, 1, 5000);
const renderer = new THREE.WebGLRenderer({ antialias:true, preserveDrawingBuffer:true });
renderer.setSize(innerWidth, innerHeight); renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.shadowMap.enabled = true; renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.toneMapping = THREE.ACESFilmicToneMapping; renderer.toneMappingExposure = 1.25;
$('scene').append(renderer.domElement);
const controls = new OrbitControls(camera, renderer.domElement); controls.enableDamping = true;
scene.add(new THREE.HemisphereLight('#f5faff', '#657568', 2.7));
for (const [position, intensity] of [[[200,700,-300],4], [[-500,250,300],3], [[400,150,600],2]]) {
  const light = new THREE.DirectionalLight('#ffffff', intensity); light.position.set(...position); scene.add(light);
  if (intensity === 4) { light.castShadow = true; light.shadow.mapSize.set(2048,2048); Object.assign(light.shadow.camera,{left:-600,right:600,top:600,bottom:-600,near:1,far:1800}); light.shadow.bias = -.0002; }
}
const floor = new THREE.Mesh(new THREE.PlaneGeometry(5000,5000),new THREE.ShadowMaterial({opacity:.12}));
floor.rotation.x = -Math.PI/2; floor.receiveShadow = true; scene.add(floor);
const group = new THREE.Group(); scene.add(group);
const palette = {Block:'#536961',Head:'#50675e',Piston:'#d4d7d3',Rod:'#c1c7c5',Crank:'#343e3a',Intake:'#5892ac',Exhaust:'#bb6955',Spring:'#9a9482',Cam:'#8f7a55',Metal:'#adb4b1'};
let manifest, packet, validation, angle=0, playing=true, selected=1, mode='glass', bank=false, isolate=false, current='baseline', loadingToken=0;
let objects=new Map();
async function load(label) {
  const token=++loadingToken;
  const values = await Promise.all(['mesh','motion','validation'].map(async kind => {
    const response = await fetch(`${label}.${kind}.json`); if(!response.ok) throw Error(`${kind}: ${response.status}`); return response.json();
  }));
  if(token!==loadingToken) return;
  [packet,manifest,validation]=values; current=label;
  if(packet.schema!=='aetheris/assembly-display-mesh/1'||packet.units!=='mm'||manifest.schema!=='aetheris/engine-motion/1'||validation.status!=='pass') throw Error('Unsupported or unqualified artifact set');
  for(const object of group.children) { object.geometry.dispose(); object.material.dispose(); }
  group.clear(); objects.clear();
  const definitions = new Map(packet.definitions.map(def=> {
    const geometry=new THREE.BufferGeometry();
    geometry.setAttribute('position',new THREE.Float32BufferAttribute(def.positions,3));
    geometry.setAttribute('normal',new THREE.Float32BufferAttribute(def.normals,3));
    geometry.setIndex(def.indices); geometry.computeBoundingSphere(); return [def.id,geometry];
  }));
  const bindings=new Map(manifest.bindings.map(b=>[b.occurrenceId,b]));
  if(bindings.size!==manifest.bindings.length||bindings.size!==packet.occurrences.filter(o=>o.definitionId).length) throw Error('Occurrence/binding count mismatch');
  for(const occurrence of packet.occurrences.filter(o=>o.definitionId)) {
    const binding=bindings.get(occurrence.id); if(!binding) throw Error(`Missing mechanism binding: ${occurrence.path}`);
    if(!definitions.has(occurrence.definitionId)) throw Error(`Missing mesh definition: ${occurrence.path}`);
    const material=new THREE.MeshStandardMaterial({color:palette[binding.category]??palette.Metal,metalness:binding.category==='Block'?.35:.65,roughness:binding.category==='Piston'?.27:.36});
    const object=new THREE.Mesh(definitions.get(occurrence.definitionId),material); object.name=occurrence.id; object.matrixAutoUpdate=false;
    object.castShadow=!['Block','Spring'].includes(binding.category); object.receiveShadow=true; object.userData={binding,occurrence};
    objects.set(occurrence.id,object); group.add(object);
  }
  const s=manifest.spec;
  $('dimensions').innerHTML=[['Bore',`${s.bore.toFixed(0)} mm`],['Stroke',`${s.stroke.toFixed(0)} mm`],['Connecting rod',`${s.rodLength.toFixed(0)} mm`],['Crank radius',`${s.crankRadius.toFixed(0)} mm`]].map(([a,b])=>`<div><dt>${a}</dt><dd>${b}</dd></div>`).join('');
  $('variant-title').innerHTML=label==='baseline'?'V8—A <span>BASELINE</span>':'V8—B <span>REVISED</span>';
  for(const name of ['baseline','revised']) $(name).classList.toggle('active',name===label);
  $('source-link').href=`${label}.firmament`;
  $('proof').textContent=`${validation.samples.toLocaleString()} samples · PASS`;
  $('proof-detail').textContent=`Rod closure < ${validation.rodClosureMm.toExponential(1)} mm. ${packet.definitions.length} shared definitions. ${objects.size} part occurrences.`;
  $('cylinder').innerHTML=manifest.cylinders.map(c=>`<option value="${c.number}" ${c.number===selected?'selected':''}>CYLINDER ${c.number}</option>`).join('');
  $('firing-order').innerHTML=manifest.firingOrder.map(n=>`<button data-cylinder="${n}" aria-label="Track cylinder ${n}">${n}</button>`).join('');
  floor.position.y=s.crankcaseFloor-16;
  preset(isolate?'cylinder':'hero'); update(); $('loading').style.display='none';
}
function preset(name) {
  if(!manifest) return;
  const center=new THREE.Vector3(0,110,manifest.cylinders.reduce((sum,c)=>sum+c.z,0)/manifest.cylinders.length);
  if(name==='cylinder') {
    const c=manifest.cylinders.find(c=>c.number===selected);
    center.set(Math.sin(c.bankRadians)*130,110,c.z); camera.position.copy(center).add(new THREE.Vector3(360,150,-260));
  } else camera.position.copy(center).add(new THREE.Vector3(...({hero:[570,360,-670],front:[0,150,-920],side:[920,190,0]}[name]??[570,360,-670])));
  const fit = Math.max(1.1, innerHeight / Math.max(400, innerWidth - 610) * 1.14);
  camera.position.sub(center).multiplyScalar(fit).add(center);
  controls.target.copy(center); controls.update();
}
function update() {
  if(!manifest)return;
  const cylinder=manifest.cylinders.find(c=>c.number===selected), spec=manifest.spec;
  const cycle=((angle-cylinder.ignitionDegrees)%720+720)%720, phase=Math.floor(cycle/180);
  $('phase').textContent=['Power','Exhaust','Intake','Compression'][phase];
  [...document.querySelectorAll('.phase-strip span')].forEach((item,i)=>item.classList.toggle('active',i===phase));
  const pose=sliderCrank(spec.crankRadius,spec.rodLength,angle*Math.PI/180,cylinder.bankRadians,cylinder.crankpinRadians);
  $('piston-value').textContent=`${(spec.crankRadius+spec.rodLength-pose.position).toFixed(1)} mm`;
  const intake=valveLift(cycle,spec.intakeOpeningDegrees,spec.valveDurationDegrees,spec.valveLift);
  const exhaust=valveLift(cycle,spec.exhaustOpeningDegrees,spec.valveDurationDegrees,spec.valveLift);
  $('lift-value').textContent=`${intake.toFixed(1)} / ${exhaust.toFixed(1)} mm`;
  $('angle').value=angle; $('angle-value').textContent=`${angle.toFixed(1)}°`;
  for(const button of $('firing-order').children) {
    const n=Number(button.dataset.cylinder), c=manifest.cylinders.find(c=>c.number===n);
    const p=((angle-c.ignitionDegrees)%720+720)%720;
    button.classList.toggle('firing-now',p<90); button.classList.toggle('selected',n===selected);
  }
  for(const object of objects.values()) {
    const b=object.userData.binding;
    object.matrix.fromArray(evaluate(manifest,b,angle)); object.matrixWorldNeedsUpdate=true;
    const bankMembers=manifest.cylinders.filter(c=>Math.sign(c.bankRadians)===Math.sign(cylinder.bankRadians)).map(c=>c.number);
    const inBank=b.cylinderMembership.length===0||b.cylinderMembership.some(n=>bankMembers.includes(n));
    object.visible=(!isolate || b.kind!=='Cam'&&b.cylinderMembership.includes(selected)) && (!bank || inBank);
    const enclosure=['Block','Head'].includes(b.category);
    if(mode==='cutaway'&&enclosure)object.visible=false;
    object.material.transparent=mode==='glass'&&enclosure;
    object.material.opacity=object.material.transparent?.12:1; object.material.depthWrite=!object.material.transparent;
    if(b.category==='Piston') {
      const c=manifest.cylinders.find(c=>c.number===b.cylinder), p=((angle-c.ignitionDegrees)%720+720)%720;
      object.material.emissive.set(p<90?'#9b3824':'#000000'); object.material.emissiveIntensity=p<90?.12:0;
    }
  }
  const path=opening=>Array.from({length:145},(_,i)=>`${i?'L':'M'}${i/144*240},${88-valveLift(i*5,opening,spec.valveDurationDegrees,spec.valveLift)/spec.valveLift*70}`).join(' ');
  $('chart').innerHTML=`<path d="M0 88H240M0 18H240" stroke="#dce2db" fill="none"/><path d="${path(spec.intakeOpeningDegrees)}" fill="none" stroke="#3b85ab" stroke-width="2"/><path d="${path(spec.exhaustOpeningDegrees)}" fill="none" stroke="#bb6955" stroke-width="2"/><path d="M${cycle/720*240} 7V95" stroke="#344d40" stroke-width="1" stroke-dasharray="3 3"/>`;
}
$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'Ⅱ Pause':'▶ Play';};
$('angle').oninput=()=>{playing=false;$('play').textContent='▶ Play';angle=Number($('angle').value);update();};
for(const [id,delta] of [['back',-15],['forward',15]])$(id).onclick=()=>{playing=false;$('play').textContent='▶ Play';angle=(angle+delta+720)%720;update();};
for(const name of ['baseline','revised'])$(name).onclick=()=>load(name).catch(fail);
$('cylinder').onchange=()=>{selected=Number($('cylinder').value);if(isolate)preset('cylinder');update();};
$('firing-order').onclick=e=>{if(e.target.dataset.cylinder){selected=Number(e.target.dataset.cylinder);$('cylinder').value=selected;if(isolate)preset('cylinder');update();}};
$('cameras').onclick=e=>{if(e.target.dataset.camera){preset(e.target.dataset.camera);for(const b of $('cameras').children)b.classList.toggle('active',b===e.target);}};
$('visibility').onclick=e=>{if(e.target.dataset.mode){mode=e.target.dataset.mode;for(const b of $('visibility').children)b.classList.toggle('active',b===e.target);update();}};
$('bank').onclick=()=>{bank=!bank;$('bank').classList.toggle('active',bank);update();};
$('isolate').onclick=()=>{isolate=!isolate;$('isolate').classList.toggle('active',isolate);preset(isolate?'cylinder':'hero');update();};
addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight);});
let previous=performance.now();
function animate(now){requestAnimationFrame(animate);const dt=Math.min((now-previous)/1000,.1);previous=now;if(playing){const rpm=Math.min(8000,Math.max(60,Number($('rpm').value)||700));angle=(angle+dt*rpm*6*Number($('speed').value))%720;update();}controls.update();renderer.render(scene,camera);}
function fail(error){$('loading').style.display='grid';$('loading').className='error';$('loading').textContent=`Artifact load failed: ${error.message}`;console.error(error);}
load('baseline').then(()=>requestAnimationFrame(animate)).catch(fail);
