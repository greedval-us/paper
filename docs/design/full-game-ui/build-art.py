"""Copy the project's authored vector marks into a compact, offline UI sketch library."""
import json
from pathlib import Path
root = Path(__file__).parent
theme = json.loads((root / 'assets/notebook-theme.json').read_text(encoding='utf-8'))
colors = ['#253b58', '#738198', '#ffffff', '#ca4051', '#3159dc', '#26765c', '#d7e1ed', '#ffffff']
result = {}
for shape in theme['Shapes']:
    sid = shape['Id']
    if not (sid.startswith(('chassis.', 'weapon.', 'module.', 'object.', 'ui.tool.', 'ui.material.', 'terrain.')) or
            sid.startswith('enemy.') and sid.endswith('.outlined.idle')):
        continue
    marks = []
    for m in shape['Marks']:
        color = colors[m['Channel']]
        style = f'opacity="{m["Alpha"]}" stroke-linecap="round" stroke-linejoin="round"'
        if m['Primitive'] in (0, 1):
            points = ' '.join(f'{p["x"]},{-p["y"]}' for p in m['Points'])
            if m['Primitive'] == 0:
                marks.append(f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="{m["Width"]}" {style}/>')
            else:
                marks.append(f'<polygon points="{points}" fill="{color}" {style}/>')
        else:
            c = m['Center']; radius = m['Radius']; asp = m['Aspect']
            fill = color if m['Primitive'] == 2 else 'none'
            stroke = 'none' if m['Primitive'] == 2 else color
            marks.append(f'<ellipse cx="{c["x"]}" cy="{-c["y"]}" rx="{radius}" ry="{radius*asp}" fill="{fill}" stroke="{stroke}" stroke-width="{m["Width"]}" {style}/>')
    result[sid] = ''.join(marks)
(root / 'assets/art.js').write_text('window.ART='+json.dumps(result, ensure_ascii=False)+';', encoding='utf-8')
print(f'{len(result)} vector symbols exported from the project theme')
