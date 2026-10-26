import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = path.join(root, 'docs/validation/modular-album');
const sourceFolder = path.join(root, 'docs/design/game-concept-art-2026-10-16/screens');
const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'task13-visual-states.json'), 'utf8'));
const expectedRevision = 'modular-full-catalog-2026-10-22-v7';
const beforeRevision = expectedRevision;
const catalogRevision = JSON.parse(fs.readFileSync(path.join(root, 'Assets/PaperDefense/Resources/Art/NotebookMounts.json'), 'utf8')).revision;
const { sizes, states } = manifest;
if (states.length !== 30 || sizes.length !== 5 || new Set(sizes).size !== 5 || new Set(states.map(s => s.id)).size !== 30)
  throw new Error('The canonical manifest must contain 30 distinct states and five sizes.');
for (const size of sizes) if (!/^\d+x\d+$/.test(size)) throw new Error('Invalid size: ' + size);
for (const state of states)
  if (!/^task13-\d\d-[a-z0-9-]+$/.test(state.capture) || path.basename(state.source) !== state.source)
    throw new Error('Invalid canonical capture/source: ' + state.id);

const relative = file => path.relative(folder, file).split(path.sep).join('/');
function pngInfo(file) {
  const bytes = fs.readFileSync(file);
  if (bytes.length < 45 || !bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])))
    throw new Error('Invalid PNG signature');
  let offset = 8, first = true, ended = false;
  while (offset + 12 <= bytes.length) {
    const length = bytes.readUInt32BE(offset), type = bytes.toString('ascii', offset + 4, offset + 8);
    if (offset + length + 12 > bytes.length) throw new Error('Truncated PNG chunk');
    if (first && (type !== 'IHDR' || length !== 13)) throw new Error('Missing PNG IHDR');
    first = false; offset += length + 12;
    if (type === 'IEND') { if (length !== 0 || offset !== bytes.length) throw new Error('Invalid PNG end'); ended = true; break; }
  }
  if (!ended) throw new Error('Missing PNG end');
  const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
  if (width < 1 || height < 1) throw new Error('Invalid PNG dimensions');
  return { width, height, sha256: crypto.createHash('sha256').update(bytes).digest('hex') };
}

function inspect(file, expected = null) {
  const result = { url: relative(file), exists: fs.existsSync(file), status: 'MISSING', errors: [], png: null, metadata: null };
  if (!result.exists) { result.errors.push('PNG отсутствует.'); return result; }
  try { result.png = pngInfo(file); }
  catch (error) { result.errors.push(error.message); }
  if (expected) {
    const metadataFile = file.replace(/\.png$/, '.json');
    result.metadataUrl = relative(metadataFile);
    if (!fs.existsSync(metadataFile)) result.errors.push('JSON происхождения отсутствует.');
    else {
      try { result.metadata = JSON.parse(fs.readFileSync(metadataFile, 'utf8')); }
      catch { result.errors.push('JSON происхождения не читается.'); }
    }
    if (result.png && (result.png.width !== expected.width || result.png.height !== expected.height))
      result.errors.push('PNG: неверный размер ' + result.png.width + '×' + result.png.height + '.');
    const m = result.metadata;
    if (m) {
      if (m.width !== expected.width || m.height !== expected.height) result.errors.push('JSON: неверный размер.');
      if (m.name !== expected.name) result.errors.push('JSON: неверное имя кадра.');
      if (m.artRevision !== expected.revision) result.errors.push('JSON: устаревшая ревизия ' + String(m.artRevision) + '.');
      if (m.activeCanvases !== 1) result.errors.push('JSON: ожидается ровно один активный Canvas.');
      if (m.scene !== '') result.errors.push('JSON: кадр получен вне изолированной временной сцены.');
      if (!Number.isInteger(m.frame) || m.frame < 0) result.errors.push('JSON: отсутствует номер кадра.');
    }
  }
  result.status = result.errors.length ? 'INVALID' : 'READY';
  return result;
}

// Only explicit reviewer records can supply a visual verdict. Integrity READY
// never implies that a person/agent has looked at a source or capture.
export function assessVisualReview(record, expected) {
  const proof = { status: 'MISSING_PROOF', verdict: null, reviewer: record?.reviewer || null,
    reportPath: record?.reportPath || null, reportUrl: record?.reportUrl || null,
    residuals: record?.residuals || [], observations: record?.observations || [], errors: [] };
  if (!record) { proof.errors.push('Нет записи визуального просмотра этого кадра.'); return proof; }
  const normalizePath = value => typeof value === 'string' ? path.resolve(root, value).split(path.sep).join('/') : null;
  const validHash = value => typeof value === 'string' && /^[a-f0-9]{64}$/i.test(value);
  const acceptedVerdicts = ['PASS', 'PASS_WITH_DIFFERENCES', 'PASS_WITH_RESIDUALS'];
  if (!record.reviewer || record.inspected !== true || !validHash(record.sourceSha256) || !validHash(record.captureSha256) ||
      !acceptedVerdicts.includes(record.verdict)) {
    proof.status = 'INVALID_PROOF';
    proof.errors.push('Требуются автор, явная отметка просмотра, SHA256 исходника/кадра и допустимый вердикт.');
    return proof;
  }
  if (record.revision !== expected.revision || record.captureRevision !== expected.revision)
    proof.errors.push('Ревизия записи просмотра устарела.');
  if (normalizePath(record.sourcePath) !== normalizePath(expected.sourcePath) || record.sourceSha256.toLowerCase() !== expected.sourceSha256)
    proof.errors.push('Исходный эскиз не совпадает с записью просмотра.');
  if (normalizePath(record.capturePath) !== normalizePath(expected.capturePath) || record.captureSha256.toLowerCase() !== expected.captureSha256)
    proof.errors.push('Кадр не совпадает с записью просмотра.');
  if (proof.errors.length) { proof.status = 'STALE_PROOF'; return proof; }
  if (!expected.integrityReady) {
    proof.status = 'INVALID_PROOF'; proof.errors.push('Целостность исходника или кадра не подтверждена.'); return proof;
  }
  proof.status = 'REVIEWED';
  proof.verdict = record.verdict === 'PASS' && proof.residuals.length === 0 ? 'PASS' : 'PASS_WITH_DIFFERENCES';
  return proof;
}

const reviewReports = ['refinement'].map(scope => {
  const file = path.join(folder, 'refinement-scene-reviews.json');
  const report = { scope, path: path.relative(root, file).split(path.sep).join('/'), url: relative(file), exists: fs.existsSync(file), errors: [] };
  if (report.exists) {
    try { report.data = JSON.parse(fs.readFileSync(file, 'utf8')); }
    catch (error) { report.errors.push('Не читается отчёт: ' + error.message); }
  } else report.errors.push('Отчёт визуального просмотра отсутствует.');
  return report;
});
const reviewRecords = reviewReports.flatMap(report => (report.data?.states || []).flatMap(state => {
  const source = typeof state.source === 'object' ? state.source : null;
  const sourcePath = source?.path || state.sourcePath || (typeof state.source === 'string' ? 'docs/design/game-concept-art-2026-10-16/screens/' + state.source : null);
  return (state.captures || []).map(capture => ({ reviewer: state.reviewer || report.data.reviewer,
    reportPath: state.reportPath || report.path, reportUrl: state.reportPath ? relative(path.join(root,state.reportPath)) : report.url, revision: report.data.revision || report.data.artRevision,
    sourcePath, sourceSha256: source?.sha256 || state.sourceSha256,
    capturePath: capture.path, captureSha256: capture.sha256, captureRevision: capture.revision || capture.artRevision,
    inspected: capture.manuallyInspected === true || capture.visuallyInspected === true || capture.pixelsViewed === true,
    verdict: capture.verdict || state.verdict,
    observations: state.observations || [], residuals: [...(report.data.sharedResiduals || []), ...(state.residuals || [])] }));
}));

const rows = states.map(state => {
  const source = inspect(path.join(sourceFolder, state.source));
  const cells = sizes.map(size => {
    const [width, height] = size.split('x').map(Number);
    const beforeName = state.capture.replace(/^task13-/, 'fidelity-') + '-' + size;
    const afterName = state.capture.replace(/^task13-/, 'refinement-') + '-' + size;
    const cell = { size, width, height,
      before: inspect(path.join(folder, 'fidelity-screenshots', beforeName + '.png'), { width, height, name: beforeName, revision: beforeRevision }),
      after: inspect(path.join(folder, 'refinement-screenshots', afterName + '.png'), { width, height, name: afterName, revision: expectedRevision }) };
    const capturePath = path.relative(root, path.join(folder, 'refinement-screenshots', afterName + '.png')).split(path.sep).join('/');
    const records = reviewRecords.filter(record => record.capturePath === capturePath);
    cell.manualReview = assessVisualReview(records.length === 1 ? records[0] : null, {
      revision: expectedRevision, sourcePath: path.join(sourceFolder, state.source), sourceSha256: source.png?.sha256,
      capturePath, captureSha256: cell.after.png?.sha256, integrityReady: source.status === 'READY' && cell.after.status === 'READY' });
    if (records.length > 1) { cell.manualReview.status = 'INVALID_PROOF'; cell.manualReview.errors = ['Для кадра найдены неоднозначные записи просмотра.']; }
    cell.visualReview = cell.manualReview.verdict || cell.manualReview.status;
    return cell;
  });
  return { ...state, source, cells };
});
function counts(items) {
  return { ready: items.filter(x => x.status === 'READY').length,
    missing: items.filter(x => x.status === 'MISSING').length, invalid: items.filter(x => x.status === 'INVALID').length };
}
const allCells = rows.flatMap(row => row.cells);
const reviewCount = status => allCells.filter(cell => cell.manualReview.status === status).length;
const manualReview = { reviewed: reviewCount('REVIEWED'), missing: reviewCount('MISSING_PROOF'),
  stale: reviewCount('STALE_PROOF'), invalid: reviewCount('INVALID_PROOF'),
  withDifferences: allCells.filter(cell => cell.manualReview.verdict === 'PASS_WITH_DIFFERENCES').length,
  reports: reviewReports.map(({ data, ...report }) => report) };
const summary = { generatedAt: new Date().toISOString(), expectedRevision, beforeRevision, catalogRevision,
  states: rows.length, sizes, expectedCaptures: allCells.length, source: counts(rows.map(row => row.source)),
  before: counts(allCells.map(cell => cell.before)), after: counts(allCells.map(cell => cell.after)),
  manualReview, visualReview: manualReview.reviewed === allCells.length ? (manualReview.withDifferences ? 'REVIEWED_WITH_DIFFERENCES' : 'REVIEWED') : 'INCOMPLETE_REVIEW',
  warnings: catalogRevision === expectedRevision ? [] : ['Current catalog revision differs from the requested fidelity revision.'] };
const data = { summary, rows };
fs.writeFileSync(path.join(folder, 'visual-refinement-gallery-data.json'), JSON.stringify(data, null, 2) + '\n');
const json = JSON.stringify(data).replaceAll('<', '\\u003c');
const html = String.raw`<!doctype html>
<html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Сходство сцен с эскизами · исходник / до / после</title>
<style>
:root{color-scheme:light;--paper:#f4f0e4;--ink:#152b58;--blue:#2458a6;--line:#b8c4d3;--muted:#53617a;--good:#19684b;--bad:#a52836;--warn:#98641d}
*{box-sizing:border-box}body{margin:0;background:var(--paper);color:var(--ink);font:15px/1.45 system-ui,sans-serif}button,select{font:inherit;color:inherit;background:#fffcf4;border:1px solid #9aadc8;border-radius:6px;padding:7px 10px}button{cursor:pointer}button:hover,a:hover{background:#dbe7f6}button[aria-pressed=true]{background:var(--blue);color:white;border-color:var(--blue)}button:focus-visible,select:focus-visible,a:focus-visible{outline:3px solid #edb530;outline-offset:3px}a{color:var(--blue)}header{position:sticky;top:0;z-index:4;padding:12px 22px;background:#f8f4eaf5;border-bottom:1px solid #9eafc7;backdrop-filter:blur(10px)}h1{font-size:22px;margin:0 0 8px}.tools{display:flex;align-items:center;gap:10px;flex-wrap:wrap}.tools label{display:flex;align-items:center;gap:7px}#summary{font-size:13px;color:var(--muted)}main{max-width:1920px;margin:auto;padding:20px 22px}.note{font-size:13px;color:var(--muted);margin:10px 0 18px}h2{font-size:21px;margin:0}.viewer{background:#fffdf6;border:1px solid #acbcd2;border-radius:10px;padding:15px;margin-bottom:20px}.viewer-head{display:flex;gap:12px;align-items:center;justify-content:space-between;flex-wrap:wrap;margin-bottom:10px}.modes{display:flex;gap:5px;flex-wrap:wrap}.triple{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.picture{margin:0;min-width:0}.picture figcaption{margin:6px 0;font-size:13px;color:var(--muted)}.imagebox{position:relative;display:flex;align-items:center;justify-content:center;aspect-ratio:var(--aspect,16/9);background:#e3dfd4;overflow:hidden;border:1px solid #b3bbca;border-radius:4px}.imagebox img{display:block;width:100%;height:100%;object-fit:contain}.imagebox .missing{position:absolute;text-align:center;padding:22px;max-width:100%;color:var(--bad)}.imagebox img[hidden]{display:none}.READY{color:var(--good)}.INVALID,.MISSING{color:var(--bad)}.badge{font-size:12px;font-weight:650;padding:3px 7px;border:1px solid currentColor;border-radius:4px;white-space:nowrap}.ab-stage{max-width:1600px;margin:auto}.ab-stage .imagebox{max-height:76vh}.ab-label{font-weight:650;font-size:17px}.ab-tools{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin:12px 0}.detail{font-size:13px;color:var(--muted);white-space:pre-line}.large .viewer{position:fixed;inset:0;z-index:10;margin:0;border:0;border-radius:0;overflow:auto;background:var(--paper);padding:12px 20px}.large .ab-stage .imagebox{max-height:calc(100vh - 170px)}.large .triple .imagebox{max-height:calc(100vh - 150px)}.large .viewer-head{position:sticky;top:-12px;background:var(--paper);z-index:2;padding:4px 0 8px}#exitLarge{display:none}.large #exitLarge{display:inline-block}.large #enterLarge{display:none}.matrix-scroll{overflow:auto;margin:14px 0 28px}table{border-collapse:collapse;width:100%;background:#fffdf6;font-size:13px}th,td{border:1px solid var(--line);padding:7px 9px;text-align:left}th{background:#e6edf7;white-space:nowrap}td button{border:0;padding:2px 5px;background:none;font-size:12px;font-weight:650}nav{display:flex;gap:6px;flex-wrap:wrap;margin:20px 0}nav a{padding:4px 8px;border:1px solid var(--line);border-radius:4px;text-decoration:none}article{padding:15px;border:1px solid #b8c4d4;border-radius:8px;margin:18px 0;background:#fffdf6}article h3{margin:0 0 9px;font-size:18px;display:flex;gap:12px;align-items:center;justify-content:space-between}article .imagebox{cursor:zoom-in}article .detail{margin-top:6px}footer{padding:0 22px 30px;color:var(--muted);font-size:13px}.sr{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0)}[hidden]{display:none!important}@media(max-width:900px){header{position:relative;padding:12px}.triple{grid-template-columns:1fr}.viewer .triple{grid-template-columns:repeat(3,minmax(0,1fr))}main{padding:14px 12px}.viewer{padding:10px}.viewer .picture figcaption{font-size:11px}.large .viewer .triple{grid-template-columns:1fr}}
</style></head><body>
<header><h1>Сцены и эскизы: исходник / Fidelity до / доработка после</h1><div class="tools"><label>Размер <select id="size"></select></label><label>Экран <select id="scene"></select></label><span id="summary"></span></div></header>
<main><p class="note">READY — проверенная целостность PNG и JSON. «Просмотрен с отличиями» — отдельный визуальный разбор, связанный с SHA256 эскиза и кадра, ревизией, автором и отчётом. Это не попиксельное совпадение и не одобрение пользователя. Исходники вписаны без обрезки; при разных пропорциях страницы видны поля. Клавиши: 1 / 2 — A / B; пробел — переключение; Esc — закрыть крупный вид.</p>
<section class="viewer" aria-label="Крупное сравнение"><div class="viewer-head"><h2 id="viewerTitle"></h2><div class="modes"><button id="tripleMode" aria-pressed="true">Три колонки</button><button id="abMode" aria-pressed="false">A / B</button><button id="enterLarge">Крупно</button><button id="exitLarge">Закрыть крупный вид</button></div></div><div id="viewerTriple" class="triple"></div><div id="viewerAB" hidden><div class="ab-tools"><label>Пара <select id="pair"><option value="source-after">Эскиз / После</option><option value="before-after">До / После</option><option value="source-before">Эскиз / До</option></select></label><button id="sideA" aria-pressed="true">A</button><button id="sideB" aria-pressed="false">B</button><span id="abLabel" class="ab-label"></span></div><div id="abImage" class="ab-stage"></div></div><p id="viewerDetail" class="detail"></p></section>
<details open><summary>Матрица целостности и визуального просмотра: 30 экранов × 5 размеров</summary><div id="matrix" class="matrix-scroll"></div></details><nav id="nav" aria-label="Все экраны"></nav><div id="allScenes"></div></main>
<footer id="footer"></footer><script type="application/json" id="galleryData">${json}</script><script>
const data=JSON.parse(document.getElementById('galleryData').textContent),rows=data.rows,sizes=data.summary.sizes;
const el=id=>document.getElementById(id),name={source:'Эскиз',before:'Fidelity — до',after:'Доработка — после'};
let currentId=1,mode='triple',side=0;
function option(value,text){const n=document.createElement('option');n.value=value;n.textContent=text;return n;}
for(const size of sizes)el('size').append(option(size,size));el('size').value=sizes.includes('1280x720')?'1280x720':sizes[0];
for(const row of rows)el('scene').append(option(row.id,String(row.id).padStart(2,'0')+' · '+row.title));
function cell(row,size=el('size').value){return row.cells.find(value=>value.size===size);}
function item(row,key,c=cell(row)){return key==='source'?row.source:c[key];}
function dimensions(info){return info.png?info.png.width+'×'+info.png.height:'—';}
function metadata(info){return info.metadata?.artRevision||'без capture JSON';}
function reviewLabel(c){const p=c.manualReview;return p.status==='REVIEWED'?(p.verdict==='PASS'?'Просмотрен':'Просмотрен с отличиями'):({MISSING_PROOF:'Нет записи просмотра',STALE_PROOF:'Просмотр устарел',INVALID_PROOF:'Недействительная запись просмотра'}[p.status]||p.status);}
function reviewDescription(c){const p=c.manualReview;const lines=['Визуальный разбор: '+reviewLabel(c)];if(p.reviewer)lines.push('Автор: '+p.reviewer);if(p.reportPath)lines.push('Отчёт: '+p.reportPath);if(p.errors.length)lines.push(p.errors.join(' '));if(p.observations.length)lines.push('Проверено: '+p.observations.join(' '));if(p.residuals.length)lines.push('Отличия: '+p.residuals.join(' '));return lines.join('\n');}
function picture(info,label,aspect,onClick=null){const figure=document.createElement('figure');figure.className='picture';const box=document.createElement('div');box.className='imagebox';box.style.setProperty('--aspect',aspect);const image=document.createElement('img');image.alt=label;image.loading='lazy';image.decoding='async';image.hidden=!info.exists;if(info.exists)image.src=info.url;const missing=document.createElement('span');missing.className='missing';missing.textContent=info.status==='MISSING'?'MISSING · снимок отсутствует':info.errors.join(' ');missing.hidden=info.exists;image.onerror=()=>{image.hidden=true;missing.hidden=false;missing.textContent='Ошибка загрузки PNG. Пересоберите галерею после завершения записи.';};box.append(image,missing);const caption=document.createElement('figcaption');caption.textContent=label+' · '+info.status+' · '+dimensions(info);caption.className=info.status;if(onClick){box.tabIndex=0;box.setAttribute('role','button');box.setAttribute('aria-label','Открыть крупно: '+label);box.onclick=onClick;box.onkeydown=e=>{if(e.key==='Enter'){e.preventDefault();onClick();}};}figure.append(box,caption);if(info.exists){const a=document.createElement('a');a.href=info.url;a.target='_blank';a.rel='noopener';a.textContent='PNG';a.style.fontSize='12px';figure.append(a);}return figure;}
function describe(key,info){const details=[name[key]+': '+info.status+' · '+dimensions(info)];if(info.metadata)details.push(info.metadata.artRevision+' · Canvas '+info.metadata.activeCanvases+' · frame '+info.metadata.frame);if(info.errors.length)details.push(info.errors.join(' '));return details.join(' — ');}
function select(id,size=null,large=false){currentId=Number(id);el('scene').value=String(currentId);if(size)el('size').value=size;if(large)document.body.classList.add('large');updateViewer();}
function updateViewer(){const row=rows.find(value=>value.id===currentId),c=cell(row),aspect=c.width+'/'+c.height;el('viewerTitle').textContent=String(row.id).padStart(2,'0')+' · '+row.title+' · '+c.size;el('tripleMode').setAttribute('aria-pressed',String(mode==='triple'));el('abMode').setAttribute('aria-pressed',String(mode==='ab'));el('viewerTriple').hidden=mode!=='triple';el('viewerAB').hidden=mode!=='ab';if(mode==='triple'){el('viewerTriple').replaceChildren(...['source','before','after'].map(key=>picture(item(row,key,c),name[key],aspect)));}else{const pair=el('pair').value.split('-'),key=pair[side],info=item(row,key,c);el('sideA').textContent='A · '+name[pair[0]];el('sideB').textContent='B · '+name[pair[1]];el('sideA').setAttribute('aria-pressed',String(side===0));el('sideB').setAttribute('aria-pressed',String(side===1));el('abLabel').textContent=(side===0?'A':'B')+' · '+name[key];el('abImage').replaceChildren(picture(info,name[key],aspect));}el('viewerDetail').textContent=['source','before','after'].map(key=>describe(key,item(row,key,c))).join('\n')+'\n'+reviewDescription(c);}
function updateAll(){const selected=el('size').value;let ready=0,invalid=0;el('allScenes').replaceChildren();for(const row of rows){const c=cell(row,selected);if(c.after.status==='READY')ready++;if(c.after.status==='INVALID')invalid++;const article=document.createElement('article');article.id='s'+row.id;const heading=document.createElement('h3');const caption=document.createElement('span');caption.textContent=String(row.id).padStart(2,'0')+' · '+row.title;const badge=document.createElement('span');badge.textContent=c.after.status+' · '+reviewLabel(c);badge.className='badge '+c.after.status;heading.append(caption,badge);const triple=document.createElement('div');triple.className='triple';for(const key of ['source','before','after'])triple.append(picture(item(row,key,c),name[key],c.width+'/'+c.height,()=>{select(row.id,null,true);mode='ab';side=key==='source'?0:1;el('pair').value=key==='before'?'source-before':'source-after';updateViewer();}));const detail=document.createElement('div');detail.className='detail';detail.textContent=(c.after.errors.length?c.after.errors.join(' '):'PNG+JSON проверены · '+metadata(c.after))+'\n'+reviewDescription(c);if(c.manualReview.reportUrl){const link=document.createElement('a');link.href=c.manualReview.reportUrl;link.textContent='Открыть отчёт просмотра';link.style.display='block';detail.append(link);}article.append(heading,triple,detail);el('allScenes').append(article);}el('summary').textContent='После: '+ready+'/30 READY · '+invalid+' INVALID · просмотрено '+data.summary.manualReview.reviewed+'/'+data.summary.expectedCaptures+' · '+data.summary.expectedRevision;updateViewer();}
const table=document.createElement('table'),head=document.createElement('thead'),hr=document.createElement('tr');for(const title of ['Экран',...sizes]){const th=document.createElement('th');th.textContent=title;hr.append(th);}head.append(hr);table.append(head);const body=document.createElement('tbody');for(const row of rows){const tr=document.createElement('tr'),title=document.createElement('td');title.textContent=String(row.id).padStart(2,'0')+' · '+row.title;tr.append(title);for(const c of row.cells){const td=document.createElement('td'),button=document.createElement('button');button.className=c.after.status;button.textContent=c.after.status+' · '+reviewLabel(c);button.title=[...c.after.errors,reviewDescription(c)].join('\n');button.onclick=()=>{select(row.id,c.size);updateAll();el('viewerTitle').scrollIntoView({block:'center'});};td.append(button);tr.append(td);}body.append(tr);}table.append(body);el('matrix').append(table);
for(const row of rows){const a=document.createElement('a');a.href='#s'+row.id;a.textContent=String(row.id).padStart(2,'0');a.title=row.title;el('nav').append(a);}
el('size').onchange=updateAll;el('scene').onchange=()=>select(el('scene').value);el('tripleMode').onclick=()=>{mode='triple';updateViewer();};el('abMode').onclick=()=>{mode='ab';updateViewer();};el('pair').onchange=()=>{side=0;updateViewer();};el('sideA').onclick=()=>{side=0;updateViewer();};el('sideB').onclick=()=>{side=1;updateViewer();};el('enterLarge').onclick=()=>document.body.classList.add('large');el('exitLarge').onclick=()=>document.body.classList.remove('large');document.addEventListener('keydown',e=>{if(/^(INPUT|SELECT|TEXTAREA)$/.test(document.activeElement?.tagName))return;if(e.key==='Escape')document.body.classList.remove('large');if(mode==='ab'&&['1','2',' '].includes(e.key)){e.preventDefault();side=e.key==='1'?0:e.key==='2'?1:1-side;updateViewer();}});
el('footer').textContent='Создано '+data.summary.generatedAt+'. Целостность '+data.summary.expectedCaptures+' кадров: READY '+data.summary.after.ready+', MISSING '+data.summary.after.missing+', INVALID '+data.summary.after.invalid+'. Визуально просмотрено '+data.summary.manualReview.reviewed+', с отличиями '+data.summary.manualReview.withDifferences+'; нет записей '+data.summary.manualReview.missing+', устарели '+data.summary.manualReview.stale+', недействительны '+data.summary.manualReview.invalid+'. Автоматика проверяет привязку отчётов, а не сходство изображений. Ревизия каталога: '+data.summary.catalogRevision+'. '+data.summary.warnings.join(' ');updateAll();
</script></body></html>`;
fs.writeFileSync(path.join(folder, 'visual-refinement-gallery.html'), html);
console.log(JSON.stringify(summary, null, 2));
if (process.argv.includes('--require-complete') && (summary.after.ready !== allCells.length || summary.before.ready !== allCells.length || summary.source.ready !== rows.length || summary.warnings.length))
  process.exitCode = 1;

if (process.argv.includes('--require-reviewed') && manualReview.reviewed !== allCells.length) process.exitCode = 1;
