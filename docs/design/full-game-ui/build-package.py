"""Validate the design handoff and create an offline archive (no dependencies)."""
from pathlib import Path
import json
import re
import zipfile

root = Path(__file__).resolve().parent
catalog = (root / 'catalog.js').read_text(encoding='utf-8')
screens = re.findall(r"^\['([^']+)','([^']+)'", catalog, re.M)
assert len(screens) == 30, len(screens)
for screen_id, title in screens:
    assert (root / 'screens' / (screen_id + '.png')).is_file(), screen_id
for file in ['index.html', 'screen.html', 'screens/index.html', 'DESIGN.md', 'tokens.json',
             'assets/art.js', 'assets/NotebookHand.ttf', 'qa/overview.png']:
    assert (root / file).is_file(), file
layout = json.loads((root / 'qa/layout-report.json').read_text(encoding='utf-8'))
assert layout['checked'] == 90 and not layout['errors'], layout['errors']
assert all(not r['bodyOverflow'] and not r['outside'] and not r['horizontal'] for r in layout['results'])
interactions = json.loads((root / 'qa/interaction-report.json').read_text(encoding='utf-8'))
assert len(interactions['checks']) == 17
assert all(c['passed'] for c in interactions['checks'])
manifest = {
    'title': 'Держи линию — Чистый лист', 'version': '1.0', 'date': '2026-10-15',
    'kind': 'UI design sketches, not a game build',
    'screenCount': len(screens), 'gameScreensAndStates': 28, 'designSystemPages': 2,
    'pngResolution': {'width': 1600, 'height': 1000},
    'layoutChecks': layout['checked'], 'interactionChecks': len(interactions['checks']),
    'entryPoint': 'index.html', 'specification': 'DESIGN.md', 'tokens': 'tokens.json',
    'screens': [{'id': i, 'title': t, 'png': 'screens/' + i + '.png'} for i, t in screens]
}
(root / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
archive = root.parent / 'full-game-ui.zip'
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED) as z:
    for file in sorted(root.rglob('*')):
        if file.is_file() and file.suffix not in ['.log', '.pyc'] and '__pycache__' not in file.parts:
            z.write(file, str(Path(root.name) / file.relative_to(root)))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    exported = [p for p in z.namelist() if p.startswith('full-game-ui/screens/') and p.endswith('.png')]
    assert len(exported) == 30
print(json.dumps({'screens': 30, 'layoutChecks': 90, 'interactionChecks': 17,
                  'archive': str(archive), 'bytes': archive.stat().st_size}, ensure_ascii=False))
