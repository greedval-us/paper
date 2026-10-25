import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

// Builds current evidence only. Missing captures remain missing; no prior art is reused.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = path.join(root, 'docs/validation/modular-album');
const captureFolder = path.join(folder, 'art3-screenshots');
const expectedRevision = 'modular-full-catalog-2026-10-24-v8';
const json = file => JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
const relative = file => path.relative(root, file).split(path.sep).join('/');
const url = file => path.relative(folder, file).split(path.sep).map(encodeURIComponent).join('/');
const sha = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const { states, sizes } = json(path.join(folder, 'task13-visual-states.json'));
if (states.length !== 30 || sizes.length !== 5 || new Set(states.map(s => s.id)).size !== 30 || new Set(sizes).size !== 5)
  throw new Error('Canonical manifest must contain 30 distinct scenes and five sizes.');
const catalogRevision = json(path.join(root, 'Assets/PaperDefense/Resources/Art/NotebookMounts.json')).revision;
const references = ['A1-standard-foundations.png', 'A2-standard-weapons.png', 'A4-weapon-components.png']
  .map(name => path.join(root, 'ArtSource/Notebook/References/2026-10-17/sheets', name))
  .concat(['01-towers.png', '02-weapon-parts.png', '03-structures.png']
    .map(name => path.join(root, 'docs/design/album-assembly-2026-10-17/art', name)));

function inspect(file, expected = null) {
  const result = { path: relative(file), url: url(file), status: 'MISSING', errors: [], png: null, metadata: null };
  if (!fs.existsSync(file)) return result;
  try {
    const bytes = fs.readFileSync(file);
    result.sha256 = sha(file);
    if (bytes.length < 45 || !bytes.subarray(0, 8).equals(Buffer.from([137,80,78,71,13,10,26,10]))) throw new Error('Invalid PNG signature.');
    let offset = 8, ended = false;
    while (offset + 12 <= bytes.length) {
      const length = bytes.readUInt32BE(offset), type = bytes.toString('ascii', offset + 4, offset + 8);
      if (offset + length + 12 > bytes.length) throw new Error('Truncated PNG chunk.');
      if (offset === 8 && (type !== 'IHDR' || length !== 13)) throw new Error('Missing IHDR.');
      offset += length + 12;
      if (type === 'IEND') { ended = length === 0 && offset === bytes.length; break; }
    }
    if (!ended) throw new Error('Missing PNG end.');
    const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
    if (width < 1 || height < 1) throw new Error('Invalid PNG dimensions.');
    result.png = { width, height, sha256: result.sha256 };
    if (expected && (width !== expected.width || height !== expected.height)) result.errors.push('PNG dimensions do not match capture name.');
  } catch (error) { result.errors.push(error.message); }
  if (expected) {
    const sidecar = file.replace(/\.png$/, '.json'); result.metadataPath = relative(sidecar);
    if (!fs.existsSync(sidecar)) result.errors.push('Missing capture provenance.');
    else try {
      result.metadata = json(sidecar); result.metadataSha256 = sha(sidecar);
      const m = result.metadata;
      if (m.name !== expected.name || m.width !== expected.width || m.height !== expected.height ||
          m.artRevision !== expectedRevision || m.activeCanvases !== 1 || m.scene !== '' || !Number.isInteger(m.frame) || m.frame < 0)
        result.errors.push('Wrong capture name, dimensions, art revision, canvas count, temporary scene or frame.');
    } catch (error) { result.errors.push('Unreadable capture provenance: ' + error.message); }
  }
  result.status = result.errors.length ? 'INVALID' : 'READY'; return result;
}

const sourceFor = state => inspect(path.join(root, 'docs/design/game-concept-art-2026-10-16/screens', state.source));
const modularReferences = references.map(file => inspect(file));
const actualFiles = fs.existsSync(captureFolder) ? fs.readdirSync(captureFolder).filter(name => name.endsWith('.png')).sort() : [];
const captures = actualFiles.map(filename => {
  const match = /^art3-(.+)-(\d+)x(\d+)\.png$/.exec(filename);
  if (!match) throw new Error('Unrecognized Art3 capture filename: ' + filename);
  const [, state, w, h] = match, size = w + 'x' + h;
  if (!sizes.includes(size)) throw new Error('Noncanonical capture dimensions: ' + filename);
  const kind = state.startsWith('variants-') ? 'variants' : state.startsWith('parts-') ? 'parts' : state.startsWith('terrain-') ? 'terrain' : 'scene';
  const sceneId = kind === 'scene' ? Number(state.slice(0, 2)) : kind === 'terrain' ? 15 : null;
  const scene = states.find(s => s.id === sceneId);
  if (kind === 'scene' && !scene) throw new Error('Unknown scene: ' + filename);
  const current = inspect(path.join(captureFolder, filename), { name: filename.slice(0, -4), width: Number(w), height: Number(h) });
  return { state, size, kind, sceneId, title: scene?.title || state, current,
    sketchReferences: scene ? [sourceFor(scene)] : modularReferences, visualReview: { status: 'UNREVIEWED' } };
});

// Physical reviews are optional input, bound to the exact freshly read file hashes.
const reviewFiles = fs.readdirSync(folder).filter(name => /^art3-.*-visual-review\.json$/.test(name));
const reviewErrors = [], reviewedPaths = new Set();
for (const filename of reviewFiles) {
  const file = path.join(folder, filename), report = json(file);
  for (const record of report.records || []) {
    const capture = captures.find(c => c.current.path === record.capturePath);
    if (!capture || reviewedPaths.has(record.capturePath)) { reviewErrors.push(filename + ': missing/duplicate capture ' + record.capturePath); continue; }
    reviewedPaths.add(record.capturePath);
    const source = capture.sketchReferences.find(s => s.path === record.sourcePath);
    const valid = record.inspected === true && Boolean(report.reviewer || record.reviewer) &&
      ['PASS','PASS_WITH_DIFFERENCES','PASS_WITH_RESIDUALS','FAIL'].includes(record.verdict) &&
      record.captureSha256?.toLowerCase() === capture.current.sha256 && source?.sha256 === record.sourceSha256?.toLowerCase() &&
      (!record.metadataSha256 || record.metadataSha256.toLowerCase() === capture.current.metadataSha256) && capture.current.status === 'READY' && source?.status === 'READY';
    capture.visualReview = { status: valid ? 'REVIEWED' : 'STALE_OR_INVALID', reviewer: report.reviewer || record.reviewer,
      verdict: valid ? record.verdict : null, reportPath: relative(file), reportSha256: sha(file),
      observations: record.observations || [], residuals: record.residuals || [] };
    if (!valid) reviewErrors.push(filename + ': stale/invalid review ' + record.capturePath);
  }
}

const rows = states.map(state => ({ ...state, source: sourceFor(state), cells: sizes.map(size => {
  const name = state.capture.replace(/^task13-/, 'art3-');
  const found = captures.find(c => c.state === name.slice(5) && c.size === size);
  const [width,height] = size.split('x').map(Number);
  return { size, current: found?.current || inspect(path.join(captureFolder, name + '-' + size + '.png'), { name: name + '-' + size, width,height }),
    visualReview: found?.visualReview || { status: 'UNREVIEWED' } };
}) }));
function geometry(name, expectedRows, uniqueKey) {
  const file = path.join(folder, name), record = { path: relative(file), status: 'MISSING', expectedRows, rowCount: 0, records: [], errors: [] };
  if (!fs.existsSync(file)) return record;
  record.sha256 = sha(file);
  const lines = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '').trim().split(/\r?\n/), headers = lines.shift().split(',');
  record.records = lines.filter(Boolean).map(line => {
    const fields = line.split(','); if (fields.length !== headers.length) record.errors.push('CSV field count does not match header.');
    return Object.fromEntries(headers.map((key, index) => [key, fields[index]]));
  });
  record.rowCount = record.records.length;
  if (record.rowCount !== expectedRows) record.errors.push('Wrong measured geometry row count.');
  const keys = record.records.map(uniqueKey);
  if (new Set(keys).size !== keys.length) record.errors.push('Duplicate measured composition.');
  for (const row of record.records) {
    for (const key of ['MinimumPixelGap','DetailMinimumPixelGap']) if (key in row && !(row.Neutral === 'True' && key === 'DetailMinimumPixelGap'))
      if (!Number.isFinite(Number(row[key])) || Number(row[key]) < 1) record.errors.push('Invalid/clipped geometry: ' + keys[record.records.indexOf(row)]);
  }
  record.status = record.errors.length ? 'INVALID' : 'READY'; return record;
}
const modular = {
  sketchReferences: modularReferences,
  variantGeometry: geometry('art3-variants-geometry.csv', 54, r => r.Definition),
  poseGeometry: geometry('art3-parts-geometry.csv', 72, r => r.Resolution + '/' + r.Pose + '/' + r.Definition),
  sheets: captures.filter(c => ['variants','parts'].includes(c.kind))
};
const expectedSupplemental = [
  ...Array.from({length:6}, (_,i) => 'art3-variants-' + (i+1) + '-1920x1080.png'),
  ...['1920x1080','1024x768'].flatMap(size => ['rest','turn90','max-recoil'].map(pose => 'art3-parts-' + pose + '-' + size + '.png')),
  ...['1280x720','1024x768'].flatMap(size => ['zoom1','zoom3','zoom6','pan6'].map(view => 'art3-terrain-' + view + '-' + size + '.png')),
  ...sizes.flatMap(size => ['comparison','single'].map(view => 'art3-10-trial-blots-' + view + '-' + size + '.png'))
];
const canonical = rows.flatMap(row => row.cells.map(cell => cell.current));
const missingSupplemental = expectedSupplemental.filter(file => !actualFiles.includes(file));
const count = list => Object.fromEntries(['READY','MISSING','INVALID'].map(status => [status.toLowerCase(),list.filter(r => r.status === status).length]));
const summary = { generatedAt: new Date().toISOString(), expectedRevision, catalogRevision,
  canonicalSceneCount: states.length, canonicalSizes: sizes, expectedCanonicalCaptures: canonical.length,
  canonicalCaptures: count(canonical), actualCaptureCount: captures.length, actualCaptures: count(captures.map(c => c.current)),
  supplementalExpected: expectedSupplemental.length, missingSupplemental,
  actualVariantSheetCount: modular.sheets.filter(c => c.kind === 'variants').length,
  actualPoseSheetCount: modular.sheets.filter(c => c.kind === 'parts').length,
  measuredVariantDefinitions: modular.variantGeometry.rowCount, measuredPoseCompositions: modular.poseGeometry.rowCount,
  physicallyReviewed: captures.filter(c => c.visualReview.status === 'REVIEWED').length,
  visualFailures: captures.filter(c => c.visualReview.verdict === 'FAIL').length, reviewErrors,
  integrity: canonical.every(c => c.status === 'READY') && captures.every(c => c.current.status === 'READY') &&
    rows.every(r => r.source.status === 'READY') && modularReferences.every(r => r.status === 'READY') &&
    modular.variantGeometry.status === 'READY' && modular.poseGeometry.status === 'READY' &&
    missingSupplemental.length === 0 && catalogRevision === expectedRevision ? 'READY' : 'INCOMPLETE',
  visualAcceptance: 'Requires explicit physical review records; integrity does not imply visual acceptance.' };
const data = { schemaVersion: 1, summary, rows, captures, modular };

const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const figure = (item, title) => '<figure><figcaption>' + esc(title) + ' · ' + item.status + '</figcaption>' +
  (item.png ? '<a href="' + item.url + '"><img loading="lazy" src="' + item.url + '" alt="' + esc(title) + '"></a><small>' + item.png.width + ' × ' + item.png.height + ' · SHA256 ' + item.sha256 + '</small>' : '<p>Кадр отсутствует.</p>') + '</figure>';
const reviewBlock = review => {
  const labels = { PASS:'Пройдено', PASS_WITH_DIFFERENCES:'Пройдено с отличиями', PASS_WITH_RESIDUALS:'Пройдено с замечаниями', FAIL:'Требует исправления' };
  const status = review.status === 'REVIEWED' ? labels[review.verdict] : 'Ещё не проверено';
  const list = items => '<ul>' + items.map(item => '<li>' + esc(item) + '</li>').join('') + '</ul>';
  return '<p>Визуальный просмотр: ' + esc(status) + '</p>' +
    ((review.observations?.length || review.residuals?.length) ? '<details><summary>Наблюдения и отличия от эскиза</summary>' +
      (review.observations?.length ? list(review.observations) : '') +
      (review.residuals?.length ? '<strong>Оставшиеся отличия</strong>' + list(review.residuals) : '') + '</details>' : '');
};
const canonicalCards = rows.flatMap(row => row.cells.map(cell => '<article data-size="' + cell.size + '" data-kind="scene"><h2>' + row.id + '. ' + esc(row.title) + ' · ' + cell.size + '</h2><div class="images">' + figure(row.source,'Эскиз') + figure(cell.current,'Текущий кадр Art3') + '</div>' + reviewBlock(cell.visualReview) + '</article>')).join('\n');
const extras = captures.filter(c => !rows.some(row => row.capture.replace(/^task13-/,'') === c.state));
const extraCards = extras.map(c => '<article data-size="' + c.size + '" data-kind="' + (c.kind === 'scene' ? 'extra' : c.kind) + '"><h2>' + esc(c.state) + ' · ' + c.size + '</h2><div class="images">' + figure(c.current,'Текущий кадр Art3') + '</div><p>Эскизы: ' + c.sketchReferences.map(s => '<a href="' + s.url + '">' + esc(path.basename(s.path)) + '</a>').join(' · ') + '</p>' + reviewBlock(c.visualReview) + '</article>').join('\n');
const script = "const size=document.querySelector('#size'),kind=document.querySelector('#kind');function filter(){let shown=0;document.querySelectorAll('article').forEach(c=>{c.hidden=Boolean((size.value&&c.dataset.size!==size.value)||(kind.value&&c.dataset.kind!==kind.value));if(!c.hidden)shown++;});document.querySelector('#shown').textContent=shown;}size.addEventListener('change',filter);kind.addEventListener('change',filter);filter();";
new vm.Script(script);
const html = '<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Art3 · 30 экранов и модульные сборки</title><style>body{margin:0;background:#f5eedb;color:#173365;font:16px/1.5 system-ui}header,main{max-width:1600px;margin:auto;padding:24px}header{position:sticky;top:0;background:#f5eedbf5;z-index:1;border-bottom:1px solid #244c86}h1{margin:0;font-size:26px}h2{font-size:19px}article{margin:28px 0;padding:16px;border:1px solid #9aa5b4;background:#fffaf0}article[hidden]{display:none}.images{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}figure{margin:0;min-width:0}img{display:block;width:100%;height:auto}small{display:block;overflow-wrap:anywhere}select{font:inherit;padding:5px;margin:6px 16px 0 0}@media(max-width:800px){.images{grid-template-columns:1fr}header{position:static}}</style><header><h1>Art3 · 30 экранов и модульные сборки</h1><p>Канонические кадры: ' + summary.canonicalCaptures.ready + ' / ' + canonical.length + '. Всего файлов PNG: ' + captures.length + '. Физически просмотрено: ' + summary.physicallyReviewed + '. Целостность: ' + summary.integrity + '.</p><select id="size"><option value="">Все размеры</option>' + sizes.map(s => '<option' + (s === '1920x1080' ? ' selected' : '') + '>' + s + '</option>').join('') + '</select><select id="kind"><option value="">Все кадры</option><option value="scene" selected>30 экранов</option><option value="variants">54 определения</option><option value="parts">Повороты и отдача</option><option value="terrain">Карта</option><option value="extra">Дополнительные состояния</option></select>Показано: <b id="shown"></b> · <a href="art3-visual-index.json">Индекс SHA256 и размеров</a></header><main>' + canonicalCards + extraCards + '</main><script>' + script + '</script></html>';
if (!process.argv.includes('--check-only')) {
  fs.writeFileSync(path.join(folder,'art3-visual-index.json'), JSON.stringify(data,null,2) + '\n');
  fs.writeFileSync(path.join(folder,'art3-gallery.html'), html);
}
console.log(JSON.stringify(summary,null,2));
if (process.argv.includes('--require-complete') && summary.integrity !== 'READY') process.exitCode = 1;
if (process.argv.includes('--require-reviewed') && (summary.integrity !== 'READY' || summary.physicallyReviewed !== captures.length || summary.visualFailures || reviewErrors.length)) process.exitCode = 1;
