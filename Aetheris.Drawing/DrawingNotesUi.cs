using System.Net;
using System.Text;
using System.Text.Json;

namespace Aetheris.Drawing;

public static class DrawingNotesUi
{
    public static void WriteAssets(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "index.html"), Html, new UTF8Encoding(false));
    }

    public static async Task ServeAsync(string projectPath, string pdfPath, int port, TextWriter log, CancellationToken cancellationToken)
    {
        if (port is < 1024 or > 65535) throw new DrawingNotesException("drawing-ui-port-invalid", "UI port must be between 1024 and 65535.");
        var initial = DrawingNotesPersistence.Load(projectPath);
        var notebook = new DrawingNotebook(initial);
        notebook.RequireSourceMatch(pdfPath);
        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        await log.WriteLineAsync($"Drawing Notes UI: {prefix}");
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            try { await HandleAsync(context, projectPath, pdfPath, cancellationToken); }
            catch (Exception exception)
            {
                context.Response.StatusCode = 500;
                await WriteText(context.Response, JsonSerializer.Serialize(new { error = exception.Message }), "application/json", cancellationToken);
            }
        }
    }

    private static async Task HandleAsync(HttpListenerContext context, string projectPath, string pdfPath, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (context.Request.HttpMethod == "GET" && path == "/")
        {
            await WriteText(context.Response, Html, "text/html; charset=utf-8", cancellationToken);
            return;
        }
        if (context.Request.HttpMethod == "GET" && path == "/api/project")
        {
            await WriteText(context.Response, DrawingNotesPersistence.Serialize(DrawingNotesPersistence.Load(projectPath)), "application/json", cancellationToken);
            return;
        }
        if (context.Request.HttpMethod == "POST" && path == "/api/project")
        {
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);
            var incoming = JsonSerializer.Deserialize<DrawingNotesProject>(body, DrawingNotesPersistence.JsonOptions)
                ?? throw new DrawingNotesException("drawing-project-empty", "Submitted project is empty.");
            var notebook = new DrawingNotebook(incoming);
            notebook.RequireSourceMatch(pdfPath);
            DrawingNotesPersistence.Save(projectPath, incoming);
            await WriteText(context.Response, DrawingNotesPersistence.Serialize(incoming), "application/json", cancellationToken);
            return;
        }
        if (context.Request.HttpMethod == "GET" && path == "/api/export.md")
        {
            await WriteText(context.Response, DrawingNotesMarkdown.Export(DrawingNotesPersistence.Load(projectPath)), "text/markdown; charset=utf-8", cancellationToken);
            return;
        }
        var isPage = path.StartsWith("/api/page/", StringComparison.Ordinal);
        var isThumbnail = path.StartsWith("/api/thumb/", StringComparison.Ordinal);
        if (context.Request.HttpMethod == "GET" && (isPage || isThumbnail) && path.EndsWith(".png", StringComparison.Ordinal))
        {
            var token = path[(isThumbnail ? 11 : 10)..^4];
            if (!int.TryParse(token, out var page)) { context.Response.StatusCode = 404; context.Response.Close(); return; }
            var temporary = Path.Combine(Path.GetTempPath(), $"aetheris-drawing-ui-{Guid.NewGuid():N}.png");
            try
            {
                DrawingPdf.RenderPage(pdfPath, page, temporary, isThumbnail ? 36 : 180);
                var bytes = await File.ReadAllBytesAsync(temporary, cancellationToken);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
                context.Response.Close();
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return;
        }
        context.Response.StatusCode = 404;
        context.Response.Close();
    }

    private static async Task WriteText(HttpListenerResponse response, string text, string contentType, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }

    public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Aetheris Drawing Notes</title>
<style>
:root{font:14px Inter,Segoe UI,sans-serif;color:#dfe7ef;background:#111820}*{box-sizing:border-box}body{margin:0;height:100vh;display:grid;grid-template-rows:48px 1fr;background:#111820}header{display:flex;align-items:center;gap:12px;padding:8px 14px;background:#18232e;border-bottom:1px solid #314354}header strong{color:#fff}button,input,select,textarea{font:inherit;color:inherit;background:#22313e;border:1px solid #425768;border-radius:4px;padding:6px}button{cursor:pointer}button:hover{background:#2e4658}.app{display:grid;grid-template-columns:240px minmax(420px,1fr) 320px;min-height:0}.pane{overflow:auto;border-right:1px solid #314354;padding:10px}.right{border-right:0;border-left:1px solid #314354}.page{display:flex;width:100%;text-align:left;margin:5px 0;align-items:center;gap:8px}.page img{width:72px;height:47px;object-fit:cover;background:white}.region{padding:5px;margin:2px 0;border-left:3px solid #55a7d9;cursor:pointer}.viewer{position:relative;overflow:hidden;background:#39434c;touch-action:none}.stage{position:absolute;transform-origin:0 0}.stage img{display:block;user-select:none;-webkit-user-drag:none}.overlay{position:absolute;inset:0;width:100%;height:100%;overflow:visible}.box{fill:#4bc0ff22;stroke:#4bc0ff;stroke-width:1.2;vector-effect:non-scaling-stroke}.box.Dimension{fill:#ffc85730;stroke:#ffc857}.box.Datum{fill:#67d39133;stroke:#67d391}.box.Keepout{fill:#ff6b6b28;stroke:#ff6b6b}.box.related{stroke:#f6f8fa;stroke-width:2}.box.selected{stroke:#fff;stroke-width:3}.draft{fill:#fff2;stroke:#fff;stroke-dasharray:5 3}.form{display:grid;gap:7px}.form label{display:grid;gap:3px;color:#9fb2c2}.form textarea{min-height:76px}.muted{color:#91a3b3;font-size:12px}.status{margin-left:auto;color:#8ed39d}.tree,pre{font-family:ui-monospace,Consolas,monospace;white-space:pre-wrap;background:#0d141a;padding:8px;border-radius:4px}.empty{padding:12px;color:#91a3b3}@media(max-width:900px){.app{grid-template-columns:180px 1fr}.right{position:absolute;right:0;top:48px;bottom:0;width:300px;background:#111820;z-index:3}}
</style>
</head>
<body>
<header><strong>Aetheris Drawing Notes</strong><button id="fit">Fit page</button><button id="width">Fit width</button><button id="minus">-</button><span id="zoom">100%</span><button id="plus">+</button><button id="preview">Markdown</button><span class="muted">Drag highlight · Shift-drag pan · D dimension · V view · Q question</span><span class="status" id="status">Loading…</span></header>
<main class="app"><aside class="pane"><h3>Pages / regions</h3><div id="pages"></div><h3>Relationship tree</h3><div id="tree" class="tree"></div></aside><section class="viewer" id="viewer"><div class="stage" id="stage"><img id="pageImage" alt="PDF page"><svg class="overlay" id="overlay"></svg></div></section><aside class="pane right"><h3>Selected note</h3><div id="editor" class="empty">Drag a rectangle or select an overlay.</div></aside></main>
<script>
let project,page=1,scale=1,pan={x:20,y:20},selected=null,draft=null,start=null,panStart=null;
const $=id=>document.getElementById(id),viewer=$('viewer'),stage=$('stage'),img=$('pageImage'),svg=$('overlay');
const enums={category:['Datum','Dimension','Feature','View','Section','Keepout','Note','Relationship','Uncertain','Question'],confidence:['High','Medium','Low','Unresolved'],semantic:['ProductGeometry','AccessoryKeepout','SensorKeepout','MaterialRestriction','CosmeticReference','FunctionalReference','Unknown'],dimension:['Overall','Offset','Diameter','Radius','Depth','Thickness','Angle','Coordinate','Keepout','AllAround','Unknown']};
async function load(){project=await (await fetch('/api/project')).json();page=project.document.pages[0].number;showPage();renderLists();$('status').textContent='Saved';}
function showPage(){img.src=`/api/page/${page}.png`;img.onload=()=>{const p=project.document.pages.find(x=>x.number===page);img.width=p.widthPoints*2.5;img.height=p.heightPoints*2.5;svg.setAttribute('viewBox',`0 0 ${p.widthPoints} ${p.heightPoints}`);svg.style.width=img.width+'px';svg.style.height=img.height+'px';draw();fit();};}
function apply(){stage.style.transform=`translate(${pan.x}px,${pan.y}px) scale(${scale})`;$('zoom').textContent=Math.round(scale*100)+'%';}
function fit(){scale=Math.min((viewer.clientWidth-30)/img.width,(viewer.clientHeight-30)/img.height);pan={x:15,y:15};apply()}
function fitWidth(){scale=(viewer.clientWidth-30)/img.width;pan={x:15,y:15};apply()}
function point(e){const r=svg.getBoundingClientRect(),p=project.document.pages.find(x=>x.number===page);return{x:(e.clientX-r.left)/r.width*p.widthPoints,y:(e.clientY-r.top)/r.height*p.heightPoints}}
function draw(){svg.innerHTML='';const related=new Set(project.relations.filter(r=>r.fromId===selected||r.toId===selected).flatMap(r=>[r.fromId,r.toId]));const all=[...project.regions.filter(x=>x.page===page).map(x=>({...x,category:x.type,_kind:'region'})),...project.annotations.filter(x=>x.page===page).map(x=>({...x,_kind:'annotation'}))];for(const x of all){const r=document.createElementNS('http://www.w3.org/2000/svg','rect');for(const [k,v] of Object.entries({x:x.bounds.x,y:x.bounds.y,width:x.bounds.width,height:x.bounds.height}))r.setAttribute(k,v);r.setAttribute('class',`box ${x.category} ${related.has(x.id)?'related':''} ${selected===x.id?'selected':''}`);r.onclick=e=>{e.stopPropagation();selected=x.id;edit(x);draw()};svg.appendChild(r)}if(draft){const r=document.createElementNS('http://www.w3.org/2000/svg','rect');for(const [k,v] of Object.entries(draft))r.setAttribute(k,v);r.setAttribute('class','draft');svg.appendChild(r)}}
function renderLists(){$('pages').innerHTML=project.document.pages.map(p=>`<button class="page" data-page="${p.number}"><img src="/api/thumb/${p.number}.png" alt="Page ${p.number} thumbnail"><span>Page ${p.number}</span></button>${project.regions.filter(r=>r.page===p.number).map(r=>`<div class="region" data-id="${r.id}">${esc(r.name)}</div>`).join('')}`).join('');document.querySelectorAll('[data-page]').forEach(x=>x.onclick=()=>{page=+x.dataset.page;showPage()});document.querySelectorAll('[data-id]').forEach(x=>x.onclick=()=>{selected=x.dataset.id;const item=find(selected);page=item.page;showPage();edit(item)});$('tree').textContent=project.relations.map(r=>`${r.fromId}\n  └─ ${r.kind} -> ${r.toId}`).join('\n')||'No relationships yet.'}
function find(id){return project.regions.find(x=>x.id===id)||project.annotations.find(x=>x.id===id)}
function esc(v){return String(v??'').replace(/[&<>\"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]))}
function options(values,current){return values.map(x=>`<option ${x===current?'selected':''}>${x}</option>`).join('')}
function edit(item){const region=item._kind==='region'||project.regions.includes(item);$('editor').className='form';$('editor').innerHTML=`<label>ID<input id="e-id" value="${esc(item.id)}" disabled></label><label>Label<input id="e-label" value="${esc(item.label||item.name)}"></label>${region?`<label>Type<select id="e-category">${options(['View','Detail','Section','NoteBlock','DimensionCluster','Keepout','DatumArea','Other'],item.type)}</select></label>`:`<label>Category<select id="e-category">${options(enums.category,item.category)}</select></label><label>Classification<select id="e-semantic">${options(enums.semantic,item.semanticClass)}</select></label>`}<label>Confidence<select id="e-confidence">${options(enums.confidence,item.confidence)}</select></label><label>Note<textarea id="e-note">${esc(item.note||item.notes)}</textarea></label>${!region&&item.category==='Dimension'?`<label>Value text<input id="e-value" value="${esc(item.dimension?.valueText)}"></label><label>Dimension type<select id="e-dtype">${options(enums.dimension,item.dimension?.type||'Unknown')}</select></label>`:''}<button id="save">Save</button><hr><label>Relation target<select id="rel-target">${[...project.regions,...project.annotations].filter(x=>x.id!==item.id).map(x=>`<option value="${x.id}">${esc(x.id)}</option>`).join('')}</select></label><label>Relation kind<select id="rel-kind">${options(['Targets','ReferencedFrom','LocatedIn','DetailOf','SectionOf','KeepoutFor','CoordinateIn','RelatedTo'],'RelatedTo')}</select></label><button id="add-rel">Add relation</button>`;$('save').onclick=async()=>{if(region){item.name=$('e-label').value;item.type=$('e-category').value;item.notes=$('e-note').value}else{item.label=$('e-label').value;item.category=$('e-category').value;item.semanticClass=$('e-semantic').value;item.note=$('e-note').value;if(item.category==='Dimension')item.dimension={valueText:$('e-value')?.value||'?',parsedValue:null,units:null,type:$('e-dtype')?.value||'Unknown',axis:null,datumId:null,sourceFeatureId:null,targetFeatureId:null,interpretation:null}}item.confidence=$('e-confidence').value;await save();draw();renderLists()};$('add-rel').onclick=async()=>{const to=$('rel-target').value;if(!to)return;project.relations.push({id:`rel-${item.id}-${to}-${project.relations.length+1}`,fromId:item.id,toId:to,kind:$('rel-kind').value,note:null,confidence:'High'});await save();renderLists()}}
async function save(){$('status').textContent='Saving…';const r=await fetch('/api/project',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify(project)});if(!r.ok){$('status').textContent='Save failed';throw new Error(await r.text())}project=await r.json();$('status').textContent='Saved'}
function create(kind){if(!draft)return;const prefix=kind==='View'?'View':kind;let ordinal=1,id=`${prefix}.p${page}.${ordinal}`;while(find(id))id=`${prefix}.p${page}.${++ordinal}`;const label=id;if(kind==='View')project.regions.push({id,name:label,page,bounds:draft,type:'View',parentRegionId:null,notes:null,confidence:'High',cropFile:null});else project.annotations.push({id,label,category:kind,page,bounds:draft,regionId:null,note:null,originalText:null,confidence:kind==='Question'?'Unresolved':'High',semanticClass:'Unknown',tags:[],aliases:[],dimension:kind==='Dimension'?{valueText:'?',parsedValue:null,units:null,type:'Unknown',axis:null,datumId:null,sourceFeatureId:null,targetFeatureId:null,interpretation:null}:null,pass:null});draft=null;selected=id;save().then(()=>{draw();renderLists();edit(find(id));$('e-label')?.focus()})}
svg.onpointerdown=e=>{if(e.shiftKey){panStart={x:e.clientX,y:e.clientY,pan:{...pan}};return}start=point(e);draft={x:start.x,y:start.y,width:0.1,height:0.1};draw()};svg.onpointermove=e=>{if(panStart){pan={x:panStart.pan.x+e.clientX-panStart.x,y:panStart.pan.y+e.clientY-panStart.y};apply();return}if(!start)return;const p=point(e);draft={x:Math.min(start.x,p.x),y:Math.min(start.y,p.y),width:Math.max(.1,Math.abs(p.x-start.x)),height:Math.max(.1,Math.abs(p.y-start.y))};draw()};svg.onpointerup=()=>{if(panStart){panStart=null;$('status').textContent='Panned';return}start=null;$('status').textContent='Press D for dimension, V for view, or Q for question'};
viewer.onwheel=e=>{e.preventDefault();scale=Math.max(.1,Math.min(5,scale*(e.deltaY<0?1.12:.89)));apply()};
document.onkeydown=e=>{if(['INPUT','TEXTAREA','SELECT'].includes(document.activeElement.tagName))return;if(e.key.toLowerCase()==='d')create('Dimension');if(e.key.toLowerCase()==='v')create('View');if(e.key.toLowerCase()==='q')create('Question')};
$('fit').onclick=fit;$('width').onclick=fitWidth;$('plus').onclick=()=>{scale=Math.min(5,scale*1.2);apply()};$('minus').onclick=()=>{scale=Math.max(.1,scale/1.2);apply()};$('preview').onclick=async()=>{$('editor').className='';$('editor').innerHTML=`<pre>${esc(await (await fetch('/api/export.md')).text())}</pre>`};load();
</script>
</body></html>
""";
}
