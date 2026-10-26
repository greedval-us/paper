import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const folder = path.join(root, 'docs/validation/modular-album');
const revision = JSON.parse(fs.readFileSync(path.join(root, 'Assets/PaperDefense/Resources/Art/NotebookMounts.json'), 'utf8')).revision;
const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'task13-visual-states.json'), 'utf8'));
const reportNames = [
  'refinement-first3-visual-review.json', 'refinement-04-05-08-visual-review.json',
  'refinement-06-07-visual-review.json', 'refinement-09-24-visual-review.json',
  'refinement-battle-15-19-review.json', 'refinement-components-11-30-visual-review.json',
  'refinement-10-26-visual-review.json', 'refinement-20-23-visual-review.json',
];
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(path.join(root, file))).digest('hex');
function normalizeProof(value, key = '') {
  if (Array.isArray(value)) return value.map(item => normalizeProof(item));
  if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value).map(([name, item]) => [name, normalizeProof(item, name)]));
  if (typeof value !== 'string') return value;
  if (/sha256$/i.test(key)) return value.toLowerCase();
  if (/path$/i.test(key)) {
    const portable = value.replaceAll('\\', '/'), prefix = root.replaceAll('\\', '/') + '/';
    return portable.toLowerCase().startsWith(prefix.toLowerCase()) ? portable.slice(prefix.length) : portable;
  }
  return value;
}
const reports = reportNames.filter(name => fs.existsSync(path.join(folder, name))).map(name => ({
  name, path: 'docs/validation/modular-album/' + name,
  data: normalizeProof(JSON.parse(fs.readFileSync(path.join(folder, name), 'utf8').replace(/^\uFEFF/, ''))),
}));
const states = [], stale = [], missing = [];
for (const state of manifest.states) {
  const sourcePath = 'docs/design/game-concept-art-2026-10-16/screens/' + state.source;
  const captures = [], owners = new Set(), residuals = new Set(), observations = new Set(), paths = new Set();
  for (const size of manifest.sizes) {
    const capturePath = 'docs/validation/modular-album/refinement-screenshots/' + state.capture.replace(/^task13-/, 'refinement-') + '-' + size + '.png';
    const matches = [];
    for (const report of reports) {
      const d = report.data;
      const grouped = d.scenes || d.states || [{ captures: d.captures || d.frames || [] }];
      for (const group of grouped) for (const capture of group.captures || group.frames || []) {
        if (capture.path !== capturePath) continue;
        const status = capture.status || group.status || d.status || '';
        if (/require|pending|fail|superseded/i.test(status)) continue;
        const inspected = capture.visuallyOpened === true || capture.visuallyInspected === true || capture.manuallyInspected === true || capture.reviewed === true || capture.pixelsViewed === true;
        if (!inspected || !capture.sha256) continue;
        const source = typeof group.source === 'object' ? group.source : (d.sourceImages || []).find(item => item.path === sourcePath);
        if (!source || source.path !== sourcePath || source.sha256 !== hash(sourcePath)) {
          stale.push({ capturePath, report: report.path, reason: 'Source proof missing or stale' }); continue;
        }
        if (!fs.existsSync(path.join(root, capturePath)) || capture.sha256 !== hash(capturePath)) {
          stale.push({ capturePath, report: report.path, reason: 'Capture proof stale' }); continue;
        }
        if (capture.metadataSha256 && capture.metadataSha256 !== hash(capturePath.replace(/\.png$/, '.json'))) {
          stale.push({ capturePath, report: report.path, reason: 'Capture metadata proof stale' }); continue;
        }
        const reviewer = capture.reviewer || (typeof d.reviewer === 'string' ? d.reviewer : d.reviewer?.executor);
        if (!reviewer) throw new Error('Accepted proof has no reviewer: ' + report.path);
        matches.push({ report, group, capture, reviewer });
      }
    }
    if (matches.length > 1) throw new Error('Ambiguous accepted visual proofs for ' + capturePath);
    if (matches.length === 0) { missing.push(capturePath); continue; }
    const { report, group, capture, reviewer } = matches[0];
    owners.add(reviewer); paths.add(report.path);
    for (const text of [...(dArray(report.data.residuals)), ...(dArray(report.data.scopedDeviations)), ...(dArray(group.residuals))]) residuals.add(text);
    for (const text of dArray(group.observations || capture.observations || capture.observation || capture.reason)) observations.add(text);
    captures.push({ path: capturePath, sha256: capture.sha256, revision, visuallyInspected: true, verdict: 'PASS_WITH_RESIDUALS', reviewer, reportPath: report.path });
  }
  if (captures.length) states.push({ id: state.id, title: state.title, source: { path: sourcePath, sha256: hash(sourcePath) },
    reviewer: [...owners].join(', '), reportPath: [...paths][0], observations: [...observations], residuals: [...residuals], captures });
}
function dArray(value) { return Array.isArray(value) ? value : typeof value === 'string' ? [value] : []; }
const output = { revision, reviewSource: 'agent', reviewer: 'Coordinated agent visual review; individual report links retained',
  method: 'Only explicit physically inspected source/capture proofs whose stored hashes still match current files. Integrity alone never grants visual acceptance.',
  summary: { acceptedCaptures: states.reduce((count, state) => count + state.captures.length, 0), expectedCaptures: 150, missing, stale }, states };
fs.writeFileSync(path.join(folder, 'refinement-scene-reviews.json'), JSON.stringify(output, null, 2) + '\n');
console.log(JSON.stringify({ acceptedCaptures: output.summary.acceptedCaptures, expectedCaptures: 150, missing: missing.length, stale: stale.length }));
if (process.argv.includes('--require-complete') && (missing.length || stale.length)) process.exitCode = 1;
