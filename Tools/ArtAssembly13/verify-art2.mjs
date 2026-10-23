import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = 'docs/validation/modular-album/';
const absolute = file => path.join(root, file);
const json = file => JSON.parse(fs.readFileSync(absolute(file), 'utf8').replace(/^\uFEFF/, ''));
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(absolute(file))).digest('hex');
const requireTrue = (condition, message) => { if (!condition) throw new Error(message); };
const index = json(folder + 'art2-visual-index.json');
requireTrue(index.summary.captureCount === 163 && index.summary.sceneCount === 20 &&
  index.summary.physicallyReviewed === 163 && index.summary.integrity === 'PASS', 'Incomplete visual index.');
for (const record of index.records) {
  requireTrue(hash(record.sourcePath) === record.sourcePng.sha256, 'Source changed: ' + record.sourcePath);
  requireTrue(hash(record.beforePath) === record.beforePng.sha256, 'Baseline changed: ' + record.beforePath);
  requireTrue(hash(record.capturePath) === record.currentPng.sha256, 'Capture changed: ' + record.capturePath);
  requireTrue(hash(record.currentPng.metadataPath) === record.currentPng.metadataSha256, 'Capture metadata changed.');
}
const protectedFiles = json(folder + 'art2-workshop-handoff.json').protectedFiles;
for (const file of protectedFiles) requireTrue(hash(file.path) === file.sha256, 'Protected asset changed: ' + file.path);
const imageAssets = ['HelpLandscapeV2', 'NotebookPaperCardV1'].map(name => {
  const source = 'ArtSource/Notebook/NewStyle/' + name + '.png';
  const runtime = 'Assets/PaperDefense/Resources/Art/Player/' + name + '.png';
  requireTrue(hash(source) === hash(runtime), 'Native PNG source/runtime mismatch: ' + name);
  return { source, runtime, sha256: hash(source) };
});
const codeReportPath = folder + 'art2-code-review.md';
const codeReport = fs.readFileSync(absolute(codeReportPath), 'utf8');
const reviewedFiles = [...codeReport.matchAll(/\| \x60([^\x60]+)\x60 \| \x60([a-f0-9]{64})\x60 \|/g)]
  .map(match => ({ path: match[1], sha256: match[2] }));
requireTrue(reviewedFiles.length === 23 && codeReport.includes('No blocking findings'), 'Incomplete code review.');
for (const file of reviewedFiles) requireTrue(hash(file.path) === file.sha256, 'Code changed after review: ' + file.path);
const runs = [['EditMode', 'art2-final-editmode.json', 1583], ['PlayMode', 'art2-final-playmode.json', 855]].map(([mode, name, count]) => {
  const file = folder + name, data = json(file).result.data, result = data.result, summary = result?.summary;
  requireTrue(data.status === 'succeeded' && result?.mode === mode && summary?.total === count &&
    summary.passed === count && summary.failed === 0 && summary.skipped === 0, 'Final suite incomplete: ' + file);
  requireTrue(result.results?.length === count && result.results.every(test => test.state === 'Passed'),
    'Final suite lacks passing detailed results: ' + file);
  return { mode, file, sha256: hash(file), jobId: data.job_id, summary };
});
const endPath = folder + 'art2-editor-end-state.json', end = json(endPath);
requireTrue(end.editor.isPlaying === false && end.editor.isCompiling === false &&
  end.editor.sceneDirty === false && end.editor.scene === 'Assets/PaperDefense/Scenes/Game/PaperDefenseGame.unity',
  'Editor is not restored to the clean stopped Game scene.');
requireTrue(end.console.success === true && Array.isArray(end.console.data) && end.console.data.length === 0,
  'Final console error check did not return zero errors.');
const evidencePaths = [
  'Tools/ArtAssembly13/build-art2-gallery.mjs', 'Tools/ArtAssembly13/verify-art2.mjs',
  folder + 'art2-gallery.html', folder + 'art2-visual-index.json', codeReportPath, endPath,
  ...['root','shell','workshop','battle'].map(scope => folder + 'art2-' + scope + '-visual-review.json'),
  ...['workshop','shell','terrain'].map(scope => folder + 'art2-' + scope + '-capture.cs'),
  folder + 'art2-report.md',
  folder + 'art2-expected-test-diagnostics.json',
  folder + 'art2-workshop-fixes-proof.txt', folder + 'art2-help-key-proof.txt',
  ...['terrain-red','production-terrain-red','terrain-green','shot-red','shot-green','structure-family-red','structure-family-green'].map(name=>folder+'art2-'+name+'.json'),
  ...imageAssets.map(asset => asset.source.replace(/\.png$/, '.provenance.json'))
];
const verification = { schemaVersion: 1, baseline: 'c4d89c533a00c8f8e3fb9e3a3755c3a7afb73228',
  status: 'PASS_WITH_DIFFERENCES', checks: { visual: index.summary, codeReview: 'No blocking findings',
    protectedFiles: protectedFiles.length, byteIdenticalNativeAssets: imageAssets.length, finalConsoleErrors: 0,
    editorRestored: true, browserInteractiveRun: 'NOT_VERIFIED' },
  testRuns: runs, imageAssets, reviewedFiles, protectedFiles,
  evidence: evidencePaths.map(file=>({path:file,sha256:hash(file)})) };
fs.writeFileSync(absolute(folder + 'art2-final-verification.json'), JSON.stringify(verification, null, 2) + '\n');
console.log(JSON.stringify({status:verification.status,frames:163,scenes:20,
  tests:runs.map(run=>({mode:run.mode,...run.summary})),reviewedFiles:reviewedFiles.length}));
