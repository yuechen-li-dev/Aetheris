import assert from "node:assert/strict";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const [playwrightPath, corpus, output, address="http://127.0.0.1:5087", specimenId="mcm-01"] = process.argv.slice(2);
assert(playwrightPath && corpus && output,"Pass Playwright module, local McMaster corpus and ignored output directory.");
const { chromium } = await import(pathToFileURL(resolve(playwrightPath)).href);
await mkdir(output,{recursive:true});
const manifest = JSON.parse(await readFile("testdata/step242/manifests/mcmaster-legacy-x0.json","utf8"));
const specimen=manifest.specimens.find((s:{id:string})=>s.id===specimenId);assert(specimen);
const browser=await chromium.launch({executablePath:"C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",headless:true});
try {
  const page=await browser.newPage({viewport:{width:1440,height:1050}});
  const errors:string[]=[];page.on("pageerror",(e:Error)=>errors.push(String(e)));
  await page.goto(address);
  await page.locator('input[type="file"]').setInputFiles(join(corpus,specimen.filename));
  const response=page.waitForResponse((r:any)=>r.url().endsWith("/import/step"),{timeout:120000});
  await page.getByRole("button",{name:"Import STEP 242",exact:true}).click();assert((await response).ok());
  const canvas=page.getByLabel("Engineering WebGPU viewport");
  await page.waitForFunction(()=>JSON.parse(document.querySelector("canvas")?.getAttribute("data-telos-display")??"{}").meshSurfaces>0,undefined,{timeout:120000});
  const themes=await page.getByLabel("Viewport theme").locator("option").evaluateAll((options:HTMLOptionElement[])=>options.map(o=>({id:o.value,label:o.textContent})));
  const captures:any[]=[];
  for(const theme of themes) {
    await page.getByLabel("Viewport theme").selectOption(theme.id);
    await page.waitForFunction(id=>JSON.parse(document.querySelector("canvas")?.getAttribute("data-telos-presentation")??"{}").id===id,theme.id);
    await page.evaluate(async()=>{await new Promise(requestAnimationFrame);await new Promise(requestAnimationFrame)});
    assert.equal(await page.getByRole("alert").count(),0,"GPU shader validation must succeed");
    const presentation=JSON.parse(await canvas.getAttribute("data-telos-presentation")??"{}");
    const state=JSON.parse(await canvas.getAttribute("data-surface-inspection")??"{}");
    const display=JSON.parse(await canvas.getAttribute("data-telos-display")??"{}");
    if(captures.length){assert.deepEqual(state.camera,captures[0].camera,"Themes preserve camera");assert.equal(display.meshSurfaces,captures[0].display.meshSurfaces);assert.equal(display.topologyLines,captures[0].display.topologyLines)}
    await canvas.screenshot({path:join(output,`${specimenId}-${theme.id}.png`)});
    captures.push({...theme,presentation,camera:state.camera,display});
  }
  // Exercise real selection against every palette at the same pixel.
  const box=await canvas.boundingBox();assert(box);
  const picks:any[]=[];
  for(const theme of themes) {
    await page.getByLabel("Viewport theme").selectOption(theme.id);
    await canvas.click({position:{x:box.width*.48,y:box.height*.45}});
    const hit=JSON.parse(await canvas.getAttribute("data-surface-inspection-pick")??"null");
    assert(hit?.faceId!==undefined,"A source face must be pickable");
    if(picks.length){assert.equal(hit.faceId,picks[0].hit.faceId);assert.equal(hit.occurrenceId,picks[0].hit.occurrenceId)}
    await canvas.screenshot({path:join(output,`${specimenId}-${theme.id}-selected.png`)});picks.push({theme:theme.id,hit});
  }
  const pipelineCount=JSON.parse(await canvas.getAttribute("data-telos-display")??"{}").pipelines;
  for(let i=0;i<3;i++)for(const theme of themes)await page.getByLabel("Viewport theme").selectOption(theme.id);
  await page.evaluate(async()=>{await new Promise(requestAnimationFrame);await new Promise(requestAnimationFrame)});
  assert.equal(JSON.parse(await canvas.getAttribute("data-telos-display")??"{}").pipelines,pipelineCount,"Theme cycling must not grow GPU pipelines");
  assert.equal(errors.length,0,errors.join("\n"));
  await writeFile(join(output,`${specimenId}-themes.json`),JSON.stringify({specimenId,captures,picks,pipelineCount,errors},null,2));
  console.log(`${specimenId}: ${themes.length} rendered themes, same camera/source face, bounded pipeline cache`);
} finally {await browser.close()}
