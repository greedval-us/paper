import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(import.meta.dirname,'../..');
const folder=path.join(root,'docs/validation/modular-album');
const {sizes,states}=JSON.parse(fs.readFileSync(path.join(folder,'task13-visual-states.json'),'utf8'));
const currentRevision=JSON.parse(fs.readFileSync(path.join(root,'Assets/PaperDefense/Resources/Art/NotebookMounts.json'),'utf8')).revision;
const verdictPath=path.join(folder,'task13-visual-verdicts.json');
const verdicts=fs.existsSync(verdictPath)?JSON.parse(fs.readFileSync(verdictPath,'utf8')):{};
const missing=[],errors=[];
const rows=states.map(s=>{
 const cells=sizes.map(size=>{
  const filename=s.capture+'-'+size+'.png';
  const filenameJson=s.capture+'-'+size+'.json';
  const file=path.join(folder,'screenshots',filename);
  let present=fs.existsSync(file),valid=present;
  if(!present)missing.push(filename);
  else{
   const b=fs.readFileSync(file),[w,h]=size.split('x').map(Number);
   if(b.readUInt32BE(16)!==w||b.readUInt32BE(20)!==h){errors.push(filename+' dimensions');valid=false;}
   const metadataFile=path.join(folder,'screenshots',filenameJson);
   if(!fs.existsSync(metadataFile)){errors.push(filename+' missing provenance');valid=false;}
   else{
    const m=JSON.parse(fs.readFileSync(metadataFile,'utf8'));
    if(m.width!==w||m.height!==h||m.scene!==''||m.name!==s.capture+'-'+size||m.activeCanvases<1||m.artRevision!==currentRevision){errors.push(filename+' wrong or stale provenance');valid=false;}
   }
  }
  const item=verdicts[s.id]?.[size];
  if(item && (!['PASS','FAIL'].includes(item.status)||typeof item.defect!=='string'||!item.defect.trim())) errors.push(filename+' invalid verdict');
  const status=!valid?'MISSING':item?.status??'PENDING';
  return{size,filename,status,defect:item?.defect??'Визуальная проверка ещё не закончена.'};
 });
 return{...s,cells};
});
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('"','&quot;');
let html='<!doctype html><html lang="ru"><meta charset="utf-8"><title>Визуальная приёмка · задача 13</title><style>body{margin:0;background:#f8f4e7;color:#162e66;font:16px system-ui}header{position:sticky;top:0;background:#f8f4e7ee;border-bottom:2px solid #3157a2;padding:14px 24px;z-index:2;display:flex;gap:20px;align-items:center}h1{font-size:23px;margin:0}select,button{font:inherit;padding:6px 12px}article{margin:24px;padding:16px;border:1px solid #7890b4}h2{margin:0 0 12px;font-size:21px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:16px}figure{margin:0;min-width:0}img{display:block;width:100%;height:auto;box-shadow:0 1px 6px #12254930}figcaption{padding:7px 0}p{margin-bottom:0}.PASS{color:#13683d}.FAIL,.MISSING{color:#a02235}.PENDING{color:#a0641b}nav{display:flex;gap:5px;flex-wrap:wrap}nav a{color:inherit;padding:2px 5px}footer{padding:24px}</style><header><h1>30 экранов · сравнение с эскизами</h1><select id="size">'+sizes.map(x=>'<option>'+x+'</option>').join('')+'</select><span id="count"></span></header><nav style="padding:12px 24px">'+rows.map(s=>'<a href="#s'+s.id+'">'+s.id+'</a>').join('')+'</nav>';
for(const s of rows)html+='<article id="s'+s.id+'"><h2>'+s.id+'. '+esc(s.title)+'</h2><div class="pair"><figure><a target="_blank" href="../../design/game-concept-art-2026-10-16/screens/'+s.source+'"><img loading="lazy" src="../../design/game-concept-art-2026-10-16/screens/'+s.source+'"></a><figcaption>Утверждённый эскиз</figcaption></figure><figure><a class="shotlink" target="_blank" data-prefix="'+s.capture+'"><img class="shot" data-prefix="'+s.capture+'"></a><figcaption class="verdict" data-id="'+s.id+'"></figcaption></figure></div><p class="defect" data-id="'+s.id+'"></p></article>';
html+='<footer>Кадры получены из настоящих UI и симуляции в пустой временной сцене с изолированным тестовым профилем. Размеры, ревизия графики и пустой путь сцены сохранены рядом с каждым PNG в JSON. Наличие кадра не означает визуальную приёмку.</footer><script>const rows='+JSON.stringify(rows)+';function update(){let size=document.querySelector("#size").value,passed=0;for(let r of rows){let c=r.cells.find(c=>c.size===size),article=document.querySelector("#s"+r.id),link=article.querySelector(".shotlink");link.href="screenshots/"+c.filename;article.querySelector(".shot").src=link.href;let v=article.querySelector(".verdict");v.textContent=c.status+" · "+size;v.className="verdict "+c.status;article.querySelector(".defect").textContent=c.defect;if(c.status==="PASS")passed++;}document.querySelector("#count").textContent=passed+" / 30 принято";}document.querySelector("#size").onchange=update;update();</script></html>';
fs.writeFileSync(path.join(folder,'visual-gallery.html'),html);
const markdown=['| № · экран | '+sizes.join(' | ')+' | Остаточный дефект |','|---|'+sizes.map(()=>'---').join('|')+'|---|'];
for(const s of rows)markdown.push('| '+s.id+' · '+s.title+' | '+s.cells.map(c=>'['+c.status+'](screenshots/'+c.filename+')').join(' | ')+' | '+[...new Set(s.cells.filter(c=>c.status!=='PASS'||c.defect!=='Нет.').map(c=>c.defect))].join(' / ')+' |');
fs.writeFileSync(path.join(folder,'task13-visual-matrix.md'),markdown.join('\n')+'\n');
const passes=rows.flatMap(r=>r.cells).filter(c=>c.status==='PASS').length;
console.log(JSON.stringify({revision:currentRevision,states:rows.length,expected:rows.length*sizes.length,missing:missing.length,errors,passes},null,2));
if(process.argv.includes('--require-complete')&&(missing.length||errors.length||passes!==rows.length*sizes.length))process.exitCode=1;
