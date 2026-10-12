import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { SpreadsheetFile, Workbook } from '@oai/artifact-tool';

// Numbers are read exclusively from Unity's runtime export. This script never parses C# or asset YAML.
const project = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const inputPath = path.resolve(project, process.argv[2] ?? 'docs/balance/balance-data.json');
const outputPath = path.resolve(project, process.argv[3] ?? 'docs/balance/PaperDefense-Mechanics-Balance.xlsx');
const previewDir = path.join(project, 'Tools/BalanceWorkbook/.previews');
const inputBytes = await fs.readFile(inputPath);
const data = JSON.parse(inputBytes.toString('utf8').replace(/^\uFEFF/, ''));
assert.equal(data.SchemaVersion, 1, 'Unsupported balance export schema');
assert.equal(data.Configurations.length, 2, 'Both configured balances are required');
const configs = data.Configurations;
const evidencePaths = [
  'docs/validation/mechanics-editmode-results.json',
  'docs/validation/mechanics-playmode-results.json',
];
const names = { NotebookBalance: 'Notebook', TargetNotebookBalance: 'Target' };
const name = config => names[config.Id] ?? config.Id;
const value = p => p.Kind === 'number' ? p.Number : p.Kind === 'bool' ? p.Boolean : p.Text;
const dict = values => Object.fromEntries(values.map(p => [p.Path, value(p)]));
const requireParameter = (config, key) => {
  const found = config.Parameters.find(p => p.Path === key);
  assert.ok(found, `Missing runtime parameter: ${config.Id}/${key}`);
  return value(found);
};
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const evidence = await Promise.all(evidencePaths.map(async file => ({ Path: file, Sha256: sha256(await fs.readFile(path.join(project, file))) })));
for (const source of data.Sources) {
  assert.equal(sha256(await fs.readFile(path.join(project, source.Path))), source.Sha256,
    `Source changed after Unity export: ${source.Path}. Export again.`);
}
for (const config of configs) {
  assert.equal(new Set(config.Parameters.map(p => p.Path)).size, config.Parameters.length, 'Duplicate parameter path');
  for (const parameter of config.Parameters) {
    if (parameter.Kind === 'number') assert.ok(Number.isFinite(parameter.Number), `Nonfinite ${parameter.Path}`);
  }
}

const workbook = Workbook.create();
const sheetNames = ['Механики', 'Расчеты', 'Оружие', 'Детали', 'Материалы и враги', 'Волны', 'Параметры', 'Источники'];
const sheets = Object.fromEntries(sheetNames.map(title => [title, workbook.worksheets.add(title)]));
const palette = { ink: '#253348', accent: '#3E5975', light: '#EDF2F7', line: '#CBD5E1', formula: '#E9F0F7', muted: '#607185' };
const formulaChecks = [];
const views = [];
let tableId = 0;
const col = n => {
  let result = '';
  for (let i = n + 1; i > 0; i = Math.floor((i - 1) / 26)) result = String.fromCharCode(65 + (i - 1) % 26) + result;
  return result;
};
function base(sheet, title, widths) {
  sheet.showGridLines = false;
  sheet.tabColor = sheet.name === 'Механики' ? palette.ink : sheet.name === 'Расчеты' ? palette.accent : '#BBC8D6';
  sheet.getRange(`A1:${col(widths.length - 1)}4`).format.font = { name: 'Arial', size: 10, color: palette.ink };
  sheet.getRange('A2').values = [[title]];
  sheet.getRange('A2').format.font = { name: 'Arial', size: 14, bold: true, color: palette.ink };
  sheet.getRange(`A3:${col(widths.length - 1)}3`).format.borders = { bottom: { style: 'thin', color: palette.line } };
  widths.forEach((width, index) => { sheet.getRange(`${col(index)}1:${col(index)}500`).format.columnWidth = width; });
}
function note(sheet, row, text, end = 'J') {
  sheet.getRange(`A${row}`).values = [[text]];
  sheet.getRange(`A${row}:${end}${row}`).format.font = { name: 'Arial', size: 10, italic: true, color: palette.muted };
  sheet.getRange(`A${row}:${end}${row}`).format.rowHeight = 22;
}
function table(sheet, row, headers, rows, { height = 25, wrap = true, numeric = [], decimals = [] } = {}) {
  const end = row + rows.length;
  const range = sheet.getRange(`A${row}:${col(headers.length - 1)}${end}`);
  range.values = [headers, ...rows];
  range.format.font = { name: 'Arial', size: 10, color: palette.ink };
  range.format.verticalAlignment = 'center';
  range.format.rowHeight = height;
  range.format.wrapText = wrap;
  const header = sheet.getRange(`A${row}:${col(headers.length - 1)}${row}`);
  header.format = { fill: palette.ink, font: { name: 'Arial', size: 10, bold: true, color: '#FFFFFF' },
    horizontalAlignment: 'center', verticalAlignment: 'center', wrapText: true, rowHeight: 40 };
  const nativeTable = sheet.tables.add(`A${row}:${col(headers.length - 1)}${end}`, true, `BalanceTable${++tableId}`);
  nativeTable.showFilterButton = true;
  for (const index of numeric) {
    sheet.getRange(`${col(index)}${row + 1}:${col(index)}${end}`).setNumberFormat('#,##0');
    sheet.getRange(`${col(index)}${row + 1}:${col(index)}${end}`).format.horizontalAlignment = 'right';
  }
  for (const index of decimals) {
    sheet.getRange(`${col(index)}${row + 1}:${col(index)}${end}`).setNumberFormat('0.000');
    sheet.getRange(`${col(index)}${row + 1}:${col(index)}${end}`).format.horizontalAlignment = 'right';
  }
  return { start: row + 1, end, headers: row };
}
function formula(sheet, cell, text, expected, tolerance = 0.0001) {
  sheet.getRange(cell).formulas = [[text]];
  sheet.getRange(cell).format.fill = palette.formula;
  if (expected !== undefined) formulaChecks.push({ sheet: sheet.name, cell, expected, tolerance });
}

const mechanicDescriptions = [
  ['EnemyDrawingTiers', 'Прорисовка врагов', 'Core.EnemyEffects', 'Спавн и защита врага', 'Обычный эскиз. Нет добавки HP и защиты от прорисовки.', 'Legacy enemyMechanics'],
  ['CorpseBlots', 'Кляксы и очистка', 'CorpseBlotSystem / IBlotContext', 'Смерть врага, область очистки', 'Трупы не создают кляксы. Ручная очистка недоступна. Автоматическая промокашка простаивает.', 'Legacy enemyMechanics'],
  ['RangedEnemies', 'Дальний бой врагов', 'GameSession.Enemies', 'Спавн плевунов и сгустки', 'Доля плевунов переходит обычным каракулям. Сгустки не создаются.', 'Legacy enemyMechanics'],
  ['InkWells', 'Чернильницы', 'GameSession.Objectives', 'Активация, цель, выплата цвета', 'Точек и их выплат нет. Все враги идут к базе.', 'Legacy inkWells'],
  ['ColorCartridges', 'Цветные картриджи', 'ColorEffectPayload', 'Окраска и эффект попадания', 'Цвет нельзя купить или применить. Оружие сохраняет обычный урон и собственный эффект степлера.', 'Независима от чернильниц'],
  ['RunUpgrades', 'Улучшения партии', 'GameSession.Progression', 'Выбор после волны', 'Нет ожидания выбора. Между волнами игра продолжается.', 'Независима от экономики'],
  ['DrawingQuality', 'Качество рисунка', 'TowerCatalog.QualityMultiplier', 'Множитель темпа оружия', 'Множитель качества равен единице. Рисунок и оценка сохраняются.', 'Независима от геометрии мортиры'],
];
const main = sheets['Механики'];
base(main, 'ДЕРЖИ ЛИНИЮ. Механики и границы', [27, 25, 31, 63, 13, 13, 32]);
note(main, 4, 'Переключатели задаёт разработчик до старта партии. В интерфейсе игрока выбора механик нет.', 'G');
table(main, 6, ['Механика', 'Владелец', 'Граница', 'Поведение при отключении', 'Notebook', 'Target', 'Связь'], mechanicDescriptions.map(([id, title, owner, boundary, off, dependency]) => [
  title, owner, boundary, off,
  ...configs.map(c => c.Mechanics.find(item => item.Id === id).Effective ? 'Вкл.' : 'Выкл.'), dependency
]), { height: 66 });
note(main, 16, 'Конфигурация разрешается один раз. Отключение сохраняет ID деталей, каталог и совместимость профиля.', 'G');
note(main, 15, 'Legacy enemyMechanics=false сохраняет прежний рост HP по волнам. Нейтральное выключение задаёт новый отдельный флаг.', 'G');
table(main, 18, ['Слой', 'Ответственность', 'Данные / контракт'], [
  ['Core', 'Правила и состояние партии', 'Механики, команды, формулы, события, read model'],
  ['Application', 'Сценарии и хранение', 'GameBalanceAsset, RunController, IO профиля'],
  ['Presentation', 'Отображение и ввод', 'Показывает доступные команды. Игровые формулы получает из Core'],
  ['Editor / Validation', 'Проверки и экспорт', 'Экспортирует эффективный баланс. Не меняет профиль или текущую партию'],
], { height: 44 });
note(main, 25, 'Target — текущая настройка баланса. Таблица фиксирует правила, а не оценку проходимости или FPS.', 'G');
views.push([main.name, 'A1:G14']);

const calc = sheets['Расчеты'];
base(calc, 'Расчеты сборки и геометрии', [16, 16, 16, 16, 16, 16, 16, 18, 18, 16]);
note(calc, 4, 'Пример: обычное основание, один пулемёт и броня. Формулы Excel сверяются с TowerCatalog.', 'J');
const references = configs.flatMap(c => c.References.map(ref => ({ ...ref, configuration: name(c) })));
const costs = table(calc, 6, ['Пресет', 'Материал', 'Основание', 'Оружие', 'Кредит', 'Броня', 'Цена мат. ×', 'Цена Excel', 'Цена Core', 'Δ'],
  references.map(ref => [ref.configuration, ref.Material, ref.BaseCost, ref.WeaponCost, ref.AssemblyCredit, ref.ModuleCost, ref.MaterialCost, null, ref.Cost, null]),
  { numeric: [2, 3, 4, 5, 7, 8, 9], decimals: [6] });
references.forEach((ref, index) => {
  const r = costs.start + index;
  formula(calc, `H${r}`, `=ROUNDUP((C${r}+MAX(0,D${r}-E${r})+F${r})*G${r},0)`, ref.Cost, 0);
  formula(calc, `J${r}`, `=H${r}-I${r}`, 0, 0);
});
const hpRow = costs.end + 4;
const health = table(calc, hpRow, ['Пресет', 'Материал', 'Основание HP', 'Броня ×', 'Материал HP ×', 'HP Excel', 'HP Core', 'Δ HP', 'Защита', 'Эфф. HP'],
  references.map(ref => [ref.configuration, ref.Material, ref.FoundationHealth, ref.ArmorHealthMultiplier, ref.MaterialHealth, null, ref.Health, null, ref.Defense, null]),
  { decimals: [2, 3, 4, 5, 6, 7, 8, 9] });
calc.getRange(`I${health.start}:I${health.end}`).setNumberFormat('0.0%');
references.forEach((ref, index) => {
  const r = health.start + index;
  formula(calc, `F${r}`, `=MAX(1,C${r}*D${r}*E${r})`, ref.Health);
  formula(calc, `H${r}`, `=F${r}-G${r}`, 0);
  formula(calc, `J${r}`, `=F${r}/(1-I${r})`, ref.Health / (1 - ref.Defense));
});
const damageRow = health.end + 4;
const damage = table(calc, damageRow, ['Пресет', 'Материал', 'Урон оружия', 'Мат. урон ×', 'Урон Excel', 'Урон Core', 'Δ урон', 'Интервал, с', 'DPS одной цели'],
  references.map(ref => [ref.configuration, ref.Material, ref.WeaponDamage, ref.MaterialDamage, null, ref.DamagePerHit, null, ref.ActionInterval, null]),
  { decimals: [2, 3, 4, 5, 6, 7, 8] });
references.forEach((ref, index) => {
  const r = damage.start + index;
  formula(calc, `E${r}`, `=C${r}*D${r}`, ref.DamagePerHit);
  formula(calc, `G${r}`, `=E${r}-F${r}`, 0);
  formula(calc, `I${r}`, `=E${r}/H${r}`, ref.DamagePerHit / ref.ActionInterval);
});
note(calc, damage.end + 2, 'DPS — теоретический темп по одной цели без защиты, промахов и задержки захвата. Не прогноз результата боя.', 'J');
const refundRow = damage.end + 4;
const refunds = table(calc, refundRow, ['Пресет', 'Материал', 'Оплачено', 'Доля возврата', 'Предел', 'Возврат Excel'],
  references.map(ref => [ref.configuration, ref.Material, ref.Cost, ref.RefundFraction, ref.MaximumRefundFraction, null]),
  { numeric: [2, 5], decimals: [3, 4] });
references.forEach((ref, index) => {
  const r = refunds.start + index;
  formula(calc, `F${r}`, `=INT(C${r}*MIN(D${r},E${r})+0.00001)`, Math.floor(ref.Cost * Math.min(ref.RefundFraction, ref.MaximumRefundFraction) + 0.00001), 0);
});
note(calc, refunds.end + 2, 'Возврат показан при полном HP и без Salvage. В игре он дополнительно умножается на текущую долю HP.', 'J');
const footprintRow = refunds.end + 4;
const footprints = configs.flatMap(c => c.Footprints.map(f => ({ ...f, configuration: name(c) })));
const footprint = table(calc, footprintRow, ['Пресет', 'Радиус', 'Пропорция q', 'Вдоль Excel', 'Вдоль Core', 'Поперёк Excel', 'Поперёк Core', 'Площадь овала', 'Площадь круга', 'Δ площади'],
  footprints.map(f => [f.configuration, f.Radius, f.AspectRatio, null, f.ForwardRadius, null, f.SideRadius, null, null, null]),
  { decimals: [1, 2, 3, 4, 5, 6, 7, 8, 9] });
footprints.forEach((f, index) => {
  const r = footprint.start + index;
  formula(calc, `D${r}`, `=B${r}*SQRT(C${r})`, f.ForwardRadius);
  formula(calc, `F${r}`, `=B${r}/SQRT(C${r})`, f.SideRadius);
  formula(calc, `H${r}`, `=PI()*D${r}*F${r}`, Math.PI * f.Radius ** 2);
  formula(calc, `I${r}`, `=PI()*B${r}^2`, Math.PI * f.Radius ** 2);
  formula(calc, `J${r}`, `=H${r}-I${r}`, 0);
});
for (const range of [`J${costs.start}:J${costs.end}`, `H${health.start}:H${health.end}`, `G${damage.start}:G${damage.end}`, `J${footprint.start}:J${footprint.end}`])
  calc.getRange(range).conditionalFormats.add('cellIs', { operator: 'notBetween', formula: [-0.0001, 0.0001], format: { fill: '#FCE7E7', font: { color: '#9F2323' } } });
views.push([calc.name, 'A1:J24'], [calc.name, `A${damageRow - 1}:J${refunds.end + 2}`], [calc.name, `A${footprintRow - 1}:J${footprint.end + 1}`]);

const weapons = sheets['Оружие'];
base(weapons, 'Оружие и укрепления', [15, 22, 12, 12, 14, 14, 12, 14, 18, 58]);
note(weapons, 4, 'Характеристики до материала, подачи, качества и улучшений. HP отдельного инструмента отличается от HP основания.', 'J');
const weaponNotes = {
  Wall: 'Препятствие. Урона не наносит.', Gun: 'Одна цель.',
  Piercer: 'Линия. Radius — полуширина. MaxPierce — конечный предел целей.',
  Mortar: 'Зафиксированная точка попадания, задержка полёта, круг или равновеликий овал.',
  Spikes: 'Все цели в радиусе. Поиск использует Radius.', Fan: 'Конус с конечным числом целей.',
  Ricochet: 'Цепочка соседних целей без повторного попадания в одну цель.', Stapler: 'Собственное замедление действует отдельно от картриджа.',
  Blotter: 'Очищает кляксы. Урон врагам отсутствует. Цикл и цена не связаны с ручной очисткой.',
};
const weaponRows = configs.flatMap(c => c.Structures.map(item => {
  const v = dict(item.Values);
  return [name(c), item.Id, v.Cost, v.MaxHealth, v.BuildSeconds, v.Range, v.Damage, v.ShotInterval, null, weaponNotes[item.Id] ?? ''];
}));
const wt = table(weapons, 6, ['Пресет', 'Вид', 'Цена', 'HP', 'Стройка, с', 'Дальность', 'Урон', 'Интервал, с', 'DPS одной цели', 'Действие'], weaponRows,
  { height: 43, numeric: [2, 3], decimals: [4, 5, 6, 7, 8] });
weaponRows.forEach((row, index) => {
  const r = wt.start + index;
  if (row[1] === 'Wall' || row[1] === 'Blotter') weapons.getRange(`I${r}`).values = [['не применяется']];
  else formula(weapons, `I${r}`, `=G${r}/MAX(0.05,H${r})`, row[6] / Math.max(0.05, row[7]));
});
weapons.freezePanes.freezeRows(6);
views.push([weapons.name, 'A1:J15']);
const partsSheet = sheets['Детали'];
base(partsSheet, 'Каталог деталей мастерской', [15, 27, 25, 18, 18, 12, 18, 62]);
note(partsSheet, 4, 'Открытие оплачивается золотом один раз. Постройка каждого экземпляра оплачивается штрихами отдельно.', 'H');
const partStart = 6;
const parts = configs.flatMap(c => c.Parts.map(part => [name(c), part.Name, part.Id, part.Mount || (part.Module !== 'None' ? part.Module : part.Chassis),
  part.InitiallyUnlocked ? 'Да' : 'Нет', part.UnlockGold, part.IsCombatMount ? 'Боевое' : part.Mount ? 'Сервисное' : '', part.Description]));
table(partsSheet, partStart, ['Пресет', 'Деталь', 'Устойчивый ID', 'Тип', 'Открыта сразу', 'Золото', 'Крепление', 'Описание'], parts, { height: 58, numeric: [5] });
partsSheet.freezePanes.freezeRows(6);
views.push([partsSheet.name, 'A1:H15']);

const entities = sheets['Материалы и враги'];
base(entities, 'Материалы и противники', [16, 20, 16, 16, 17, 18, 18, 18, 18, 18]);
note(entities, 4, 'Все множители относятся к определению соответствующей роли. Target применяет свои переопределения последним.', 'J');
const mt = table(entities, 6, ['Пресет', 'Материал', 'Цена ×', 'HP ×', 'Урон ×', 'Возврат', 'Время ×'], configs.flatMap(c => c.Materials.map(item => {
  const v = dict(item.Values); return [name(c), item.Id, v.CostMultiplier, v.HealthMultiplier, v.DamageMultiplier, v.RefundFraction, v.BuildTimeMultiplier];
})), { decimals: [2, 3, 4, 5, 6] });
entities.getRange(`F${mt.start}:F${mt.end}`).setNumberFormat('0.0%');
const enemyStart = mt.end + 4;
table(entities, enemyStart, ['Пресет', 'Враг', 'HP', 'Клетки/с', 'Урон', 'Интервал, с', 'Награда', 'Строения ×', 'Дальность'], configs.flatMap(c => c.Enemies.map(item => {
  const v = dict(item.Values); return [name(c), item.Id, v.MaxHealth, v.Speed, v.Damage, v.AttackInterval, v.Reward, v.StructureDamageMultiplier, v.Range];
})), { numeric: [2, 4, 6], decimals: [3, 5, 7, 8] });
note(entities, enemyStart + configs.reduce((n, c) => n + c.Enemies.length, 0) + 2, 'Специфические параметры плевуна, прорисовки, цвета и разброса скорости приведены в полном словаре «Параметры».', 'J');
views.push([entities.name, 'A1:I27']);

const waves = sheets['Волны'];
base(waves, 'Волны и конечные награды', [16, 10, 14, 15, 13, 13, 13, 14, 14, 18, 19, 43]);
note(waves, 4, 'Доли ролей и доли прорисовки — разные броски. Значения включают Target и явные переопределения волн.', 'L');
note(waves, 5, 'Доли заданы в балансе. Developer-флаги могут подавлять плевунов или прорисовку, сохраняя эти исходные настройки.', 'L');
const waveRows = configs.flatMap(c => c.Waves.map(item => {
  const v = dict(item.Values); return [name(c), item.Index, v.Count, v.SpawnInterval, v.RunnerFraction, v.GnawerFraction, v.SpitterFraction,
    v.OutlinedFraction, v.HatchedFraction, item.ResolvedInkGrant, null, v.Title];
}));
const waveTable = table(waves, 6, ['Пресет', 'Волна', 'Врагов', 'Интервал, с', 'Бегуны', 'Грызуны', 'Плевуны', 'Обведённые', 'Штрихованные', 'Штрихи за волну', 'Штрихи накопл.', 'Название'], waveRows,
  { height: 42, numeric: [1, 2, 9, 10], decimals: [3, 4, 5, 6, 7, 8] });
waves.getRange(`E${waveTable.start}:I${waveTable.end}`).setNumberFormat('0.0%');
waveRows.forEach((row, index) => {
  const r = waveTable.start + index;
  const start = waveTable.start + waveRows.findIndex(item => item[0] === row[0]);
  formula(waves, `K${r}`, `=SUM(J${start}:J${r})`, waveRows.slice(start - waveTable.start, index + 1).reduce((sum, item) => sum + item[9], 0), 0);
});
waves.freezePanes.freezeRows(6);
views.push([waves.name, 'A1:L14']);

const parameters = sheets['Параметры'];
base(parameters, 'Полный словарь эффективных параметров', [60, 22, 24, 24, 24]);
note(parameters, 4, 'Каждое public-поле GameBalance экспортируется автоматически. Числа не переписываются из C# или YAML.', 'E');
note(parameters, 5, 'Песочный цвет отмечает различия между эффективными конфигурациями.', 'E');
const paramMaps = configs.map(c => new Map(c.Parameters.map(p => [p.Path, p])));
const parameterPaths = [...new Set(configs.flatMap(c => c.Parameters.map(p => p.Path)))].sort();
const paramRows = parameterPaths.map(key => {
  const found = paramMaps.map(map => map.get(key));
  assert.ok(found.every(Boolean), `Unequal runtime schema: ${key}`);
  return [key, found[0].Unit, ...found.map(value), found[0].Kind];
});
const pt = table(parameters, 6, ['Параметр', 'Единица', name(configs[0]), name(configs[1]), 'Тип'], paramRows, { height: 23, wrap: false });
parameters.getRange(`C${pt.start}:D${pt.end}`).setNumberFormat('0.0000');
parameterPaths.forEach((key, index) => {
  const r = pt.start + index;
  if (paramMaps[0].get(key).Kind === 'bool') {
    parameters.getRange(`C${r}:D${r}`).values = [paramMaps.map(map => value(map.get(key)) ? 'Вкл.' : 'Выкл.')];
  }
  if (paramMaps[0].get(key).Kind === 'number' && Number.isInteger(value(paramMaps[0].get(key))) && Number.isInteger(value(paramMaps[1].get(key))))
    parameters.getRange(`C${r}:D${r}`).setNumberFormat('#,##0');
  if (paramMaps[0].get(key).Kind === 'string') {
    parameters.getRange(`C${r}:D${r}`).format.wrapText = true;
    parameters.getRange(`A${r}:E${r}`).format.rowHeight = 50;
  }
  parameters.getRange(`C${r}:D${r}`).conditionalFormats.addCustom(`=$C${r}<>$D${r}`, { fill: '#FFF2D6' });
});
parameters.freezePanes.freezeRows(6);
views.push([parameters.name, 'A1:E20']);

const sources = sheets['Источники'];
base(sources, 'Источник и воспроизведение', [43, 96, 70]);
const sourceRows = [
  ['Режим', data.Mode, ''],
  ['Экспорт UTC', data.ExportedAtUtc, ''],
  ['Версия Unity', data.UnityVersion, ''],
  ['SHA256 JSON', sha256(inputBytes), ''],
  ['Данные', path.relative(project, inputPath).replaceAll('\\', '/'), ''],
  ...configs.map(c => [name(c), c.AssetPath, c.Fingerprint]),
  ['Обновление', 'Unity: Paper Defense > Export mechanics and balance JSON. Затем Tools/BalanceWorkbook/Build.ps1.', ''],
  ['Формулы', 'Сверка цены, HP и урона с runtime эталонами. Excel использует double, Core — float.', ''],
  ['Смысл DPS', 'Теоретический темп по одной цели. Массовые попадания, защита, промахи и захват исключены.', ''],
  ['Округление', 'Цена сборки: long × double. Цена отдельного инструмента сохраняет int × float.', ''],
  ['Сохранения', 'Developer configuration действует на новый запуск. Каталог и устойчивые ID остаются полными.', ''],
  ['Отчет рефакторинга', 'docs/validation/verification-mechanics-boundaries-2026-10-12.md', 'Итоги тестов и проверки книги'],
  ...evidence.map(item => ['Проверка рефакторинга', item.Path, item.Sha256]),
];
table(sources, 5, ['Запись', 'Значение', 'Fingerprint / примечание'], sourceRows, { height: 33 });
sources.getRange('B7').setNumberFormat('yyyy-mm-dd hh:mm:ss.000');
const hashStart = sourceRows.length + 9;
table(sources, hashStart, ['Источник', 'SHA256'], data.Sources.map(item => [item.Path, item.Sha256]), { height: 42 });
sources.freezePanes.freezeRows(5);
views.push([sources.name, 'A1:C22'], [sources.name, `A${hashStart}:B${hashStart + 8}`]);

// Recalculate exactly once after authoring, then verify the stored calculated values.
workbook.recalculate();
for (const check of formulaChecks) {
  const actual = sheets[check.sheet].getRange(check.cell).values[0][0];
  assert.equal(typeof actual, 'number', `Formula did not calculate: ${check.sheet}!${check.cell}: ${actual}`);
  assert.ok(Math.abs(actual - check.expected) <= check.tolerance, `${check.sheet}!${check.cell}: ${actual} vs ${check.expected}`);
}
const errors = await workbook.inspect({ kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',
  options: { useRegex: true, maxResults: 100 }, summary: 'Formula error scan', maxChars: 4000 });
console.log(errors.ndjson);
assert.ok(errors.ndjson.includes('Cell search matched 0 entries.'), 'Workbook formula error scan did not return zero matches');
await fs.mkdir(previewDir, { recursive: true });
for (const [viewIndex, [sheetName, range]] of views.entries()) {
  const preview = await workbook.render({ sheetName, range, scale: 1.25, format: 'png' });
  await fs.writeFile(path.join(previewDir, `${viewIndex + 1}.png`), new Uint8Array(await preview.arrayBuffer()));
}
await fs.mkdir(path.dirname(outputPath), { recursive: true });
await (await SpreadsheetFile.exportXlsx(workbook)).save(outputPath);
await fs.writeFile(path.join(path.dirname(outputPath), 'workbook-validation.json'), JSON.stringify({
  GeneratedAtUtc: new Date().toISOString(), BalanceDataSha256: sha256(inputBytes), WorkbookSha256: sha256(await fs.readFile(outputPath)),
  Sheets: sheetNames, Parameters: parameterPaths.length, RuntimeReferenceChecks: formulaChecks.length,
  VerificationEvidence: evidence,
  FormulaChecksPassed: true, FormulaErrorScan: errors.ndjson, VisualPreviews: views.map(([sheet, range]) => ({ sheet, range })),
  Engine: '@oai/artifact-tool. Native Microsoft Excel execution has not been performed.',
}, null, 2) + '\n');
console.log(JSON.stringify({ outputPath, sheets: sheetNames.length, parameters: parameterPaths.length, checkedFormulas: formulaChecks.length }));
