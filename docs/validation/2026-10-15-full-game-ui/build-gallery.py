"""Contact sheets and a local gallery of unaltered Unity Game View captures."""
from pathlib import Path
from html import escape
from PIL import Image, ImageDraw, ImageFont
import json

folder = Path(__file__).parent
images = sorted(p for p in folder.glob('*.png') if not p.name.startswith('overview'))
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 19)
records = []
for p in images:
    with Image.open(p) as im:
        records.append(dict(file=p.name, width=im.width, height=im.height))
for start in range(0, len(images), 8):
    sheet = Image.new('RGB', (1280, 4 * 450), '#e5eaf3')
    draw = ImageDraw.Draw(sheet)
    for i, p in enumerate(images[start:start + 8]):
        with Image.open(p) as im:
            im.thumbnail((620, 408))
            x, y = (i % 2) * 640, (i // 2) * 450
            sheet.paste(im.convert('RGB'), (x + 10, y + 34))
            draw.text((x + 10, y + 8), p.stem, fill='#233953', font=font)
    sheet.save(folder / f'overview-{start // 8 + 1:02}.png')
featured = [('home-1600.png', 'Главное меню'), ('preparation-1600.png', 'Подготовка'),
            ('drawing-1920.png', 'Мастерская'), ('battle-1600.png', 'Бой'),
            ('upgrades-1600.png', 'Усиления'), ('victory-1600.png', 'Результат')]
sheet = Image.new('RGB', (1280, 3 * 450), '#e5eaf3')
draw = ImageDraw.Draw(sheet)
for i, (name, label) in enumerate(featured):
    with Image.open(folder / name) as im:
        im.thumbnail((620, 408))
        x, y = (i % 2) * 640, (i // 2) * 450
        sheet.paste(im.convert('RGB'), (x + 10, y + 34))
        draw.text((x + 10, y + 8), label, fill='#233953', font=font)
sheet.save(folder / 'overview-main.png')
cards = ''.join(f'<a href="{escape(r["file"])}"><img loading="lazy" src="{escape(r["file"])}"><b>{escape(r["file"][:-4])}</b><small>{r["width"]} × {r["height"]}</small></a>' for r in records)
(folder / 'index.html').write_text('''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Держи линию · Unity UI</title><style>body{margin:40px;background:#f5f7fc;color:#233953;font:17px system-ui}h1{font-size:36px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(400px,1fr));gap:24px}a{color:inherit;text-decoration:none;background:white;border-radius:16px;overflow:hidden;padding:12px}img{width:100%;display:block}b,small{display:block;margin:8px}small{color:#596c86}p{max-width:900px;line-height:1.6}</style><h1>Держи линию — интерфейс в Unity</h1><p>Снимки реального uGUI из Game View. Проверочный профиль хранится только в памяти. Победа, поражение и ошибка записи — явно заданные тестовые состояния; текущие значения и обработчики берутся из игры. Нажми на снимок, чтобы открыть полный размер.</p><main>''' + cards + '</main></html>', encoding='utf-8')
(folder / 'capture-manifest.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'{len(images)} captures, {(len(images)+7)//8} contact sheets')
