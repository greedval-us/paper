import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = path.join(root, 'docs/validation/modular-album');
const captureFolder = path.join(folder, 'art2-screenshots');
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const manifest = readJson(path.join(folder, 'task13-visual-states.json'));
const revision = readJson(path.join(root, 'Assets/PaperDefense/Resources/Art/NotebookMounts.json')).revision;
const relative = file => path.relative(root, file).split(path.sep).join('/');
const url = file => path.relative(folder, file).split(path.sep).join('/').split('/').map(encodeURIComponent).join('/');
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const fail = message => { throw new Error(message); };
const resolveRepo = file => {
  if (typeof file !== 'string') fail('Missing repository path.');
  const resolved = path.resolve(root, file);
  if (!resolved.startsWith(root + path.sep)) fail('Path leaves repository: ' + file);
  return resolved;
};
function inspectPng(file, width, height) {
  const bytes = fs.readFileSync(file);
  if (!bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))) fail('Invalid PNG: ' + file);
  let offset = 8, ended = false;
  while (offset + 12 <= bytes.length) {
    const size = bytes.readUInt32BE(offset), type = bytes.toString('ascii', offset + 4, offset + 8);
    if (offset + size + 12 > bytes.length) fail('Truncated PNG: ' + file);
    if (offset === 8 && (type !== 'IHDR' || size !== 13)) fail('Missing IHDR: ' + file);
    offset += size + 12;
    if (type === 'IEND') { ended = size === 0 && offset === bytes.length; break; }
  }
  if (!ended) fail('Missing IEND: ' + file);
  const actualWidth = bytes.readUInt32BE(16), actualHeight = bytes.readUInt32BE(20);
  if (width && (width !== actualWidth || height !== actualHeight)) fail('Wrong PNG size: ' + file);
  return { width: actualWidth, height: actualHeight, sha256: hash(file) };
}
function inspectCapture(file, name, width, height) {
  const png = inspectPng(file, width, height);
  const metadataPath = file.replace(/\.png$/, '.json'), metadata = readJson(metadataPath);
  if (metadata.name !== name || metadata.width !== width || metadata.height !== height ||
      metadata.artRevision !== revision || metadata.activeCanvases !== 1 || metadata.scene !== '' ||
      !Number.isInteger(metadata.frame) || metadata.frame < 0) fail('Invalid capture provenance: ' + metadataPath);
  return { ...png, metadataPath: relative(metadataPath), metadataSha256: hash(metadataPath) };
}

// Review records are authored after physically opening images. This builder
// validates their identity; it never supplies a visual verdict on its own.
const reviews = new Map();
for (const scope of ['root', 'shell', 'workshop', 'battle']) {
  const reportPath = path.join(folder, 'art2-' + scope + '-visual-review.json');
  const report = readJson(reportPath);
  for (const record of report.records) {
    const key = relative(resolveRepo(record.capturePath));
    if (reviews.has(key)) fail('Duplicate review: ' + key);
    if (record.inspected !== true || !report.reviewer ||
        !['PASS', 'PASS_WITH_DIFFERENCES', 'PASS_WITH_RESIDUALS'].includes(record.verdict) ||
        !Array.isArray(record.observations) || !Array.isArray(record.residuals))
      fail('Uninspected or incomplete review: ' + key);
    reviews.set(key, { ...record, reviewer: report.reviewer, reportPath: relative(reportPath) });
  }
}
const captures = fs.readdirSync(captureFolder).filter(name => name.endsWith('.png')).sort();
if (captures.length !== 163) fail('Expected 163 scoped capture PNGs; found ' + captures.length);
const records = captures.map(name => {
  const match = /^art2-(.+)-(\d+)x(\d+)\.png$/.exec(name);
  if (!match) fail('Invalid capture name: ' + name);
  const state = match[1], width = Number(match[2]), height = Number(match[3]);
  const id = state.startsWith('terrain-') ? 15 : Number(state.slice(0, 2));
  const sourceState = manifest.states.find(item => item.id === id);
  if (!sourceState || !manifest.sizes.includes(width + 'x' + height)) fail('Unknown scene/size: ' + name);
  const capture = path.join(captureFolder, name);
  const before = path.join(folder, 'refinement-screenshots', name.replace(/^art2-/, 'refinement-'));
  const source = path.join(root, 'docs/design/game-concept-art-2026-10-16/screens', sourceState.source);
  const review = reviews.get(relative(capture));
  if (!review) fail('Missing physical review: ' + name);
  const sourcePng = inspectPng(source);
  const currentPng = inspectCapture(capture, name.slice(0, -4), width, height);
  const beforePng = inspectCapture(before, path.basename(before, '.png'), width, height);
  if (resolveRepo(review.sourcePath) !== source || review.sourceSha256.toLowerCase() !== sourcePng.sha256 ||
      review.captureSha256.toLowerCase() !== currentPng.sha256) fail('Stale source/capture review: ' + name);
  if (review.metadataSha256 && review.metadataSha256.toLowerCase() !== currentPng.metadataSha256)
    fail('Stale reviewed capture metadata: ' + name);
  return { state, id, title: sourceState.title, size: width + 'x' + height, sourcePath: relative(source),
    beforePath: relative(before), capturePath: relative(capture), sourcePng, beforePng, currentPng,
    sourceUrl: url(source), beforeUrl: url(before), captureUrl: url(capture),
    review };
});
if (reviews.size !== records.length) fail('Orphaned review records.');
const summary = { captureCount: records.length, sceneCount: new Set(records.map(r => r.id)).size,
  canonicalSizes: manifest.sizes, physicallyReviewed: records.length, integrity: 'PASS',
  fidelity: 'PASS_WITH_DIFFERENCES', scope: '20 affected scenes, their recovery/support variants and 8 terrain views; unchanged scenes retain prior acceptance.' };
fs.writeFileSync(path.join(folder, 'art2-visual-index.json'), JSON.stringify({ schemaVersion: 1, revision, summary, records }, null, 2) + '\n');
const escape = value => String(value).replace(/[&<>"']/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
const cards = records.map(r => '<article data-size="' + r.size + '" data-scene="' + r.id + '"><h2>' +
  escape(r.state) + ' · ' + escape(r.size) + '</h2><div class="images">' +
  [['Эскиз',r.sourceUrl],['Предыдущая версия',r.beforeUrl],['Сейчас',r.captureUrl]].map(([title, src]) =>
    '<figure><figcaption>' + title + '</figcaption><a href="' + src + '"><img loading="lazy" src="' + src + '" alt="' + escape(title + ': ' + r.state) + '"></a></figure>').join('') +
  '</div><p>' + escape(r.review.observations.join(' ')) + '</p><details><summary>Сохраняющиеся отличия · ' +
  escape(r.review.reviewer) + '</summary><p>' + escape(r.review.residuals.join(' ')) + '</p></details></article>').join('\n');
const script = "const size=document.querySelector('#size'),scene=document.querySelector('#scene');function filter(){let shown=0;document.querySelectorAll('article').forEach(card=>{card.hidden=(size.value&&card.dataset.size!==size.value)||(scene.value&&card.dataset.scene!==scene.value);if(!card.hidden)shown++;});document.querySelector('#shown').textContent=shown;}size.addEventListener('change',filter);scene.addEventListener('change',filter);filter();";
new vm.Script(script);
const sceneOptions = [...new Set(records.map(r => r.id))].sort((a,b)=>a-b).map(id =>
  '<option value="' + id + '">' + escape(id + ' · ' + manifest.states.find(s=>s.id===id).title) + '</option>').join('');
const html = ['<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">',
  '<title>Графика · итерация2</title><style>body{margin:0;background:#f5eedb;color:#173365;font:16px/1.5 system-ui}header,main{max-width:1600px;margin:auto;padding:24px}header{position:sticky;top:0;background:#f5eedbf5;z-index:1;border-bottom:1px solid #244c86}h1{margin:0;font-size:26px}h2{font-size:19px}article{margin:28px 0;padding:16px;border:1px solid #9aa5b4;background:#fffaf0}article[hidden]{display:none}.images{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}figure{margin:0}img{display:block;width:100%;height:auto}figcaption{margin-bottom:6px}select{font:inherit;padding:5px;margin:6px 16px 0 0}summary{cursor:pointer}@media(max-width:800px){.images{grid-template-columns:1fr}header{position:static}}</style>',
  '<header><h1>Графика · итерация 2</h1><p>163 новых кадра: 20 затронутых сцен, дополнительные состояния и 8 проверок масштаба/перемещения карты. Каждая запись связана с физическим просмотром и SHA256. Полного совпадения с эскизами пока нет.</p>',
  '<label>Размер <select id="size"><option value="">Все</option>' + manifest.sizes.map(s=>'<option'+(s==='1920x1080'?' selected':'')+'>'+s+'</option>').join('') + '</select></label>',
  '<label>Сцена <select id="scene"><option value="">Все</option>' + sceneOptions + '</select></label><span>Показано: <b id="shown"></b></span> · <a href="art2-visual-index.json">Проверяемый индекс</a></header>',
  '<main>' + cards + '</main><script>' + script + '</script></html>'].join('\n');
fs.writeFileSync(path.join(folder, 'art2-gallery.html'), html);
console.log(JSON.stringify(summary));
