import fs from 'node:fs/promises';
import path from 'node:path';
import {createRequire} from 'node:module';
import {pathToFileURL} from 'node:url';
import os from 'node:os';
// Resolve the bundled runtime explicitly. Never fall back to repository dependencies.
const modules=path.resolve(process.env.CODEX_WORKSPACE_NODE_MODULES??path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules'));
const artifactRequire=createRequire(path.join(modules,'__album_workbook__.cjs'));
const {Workbook,SpreadsheetFile}=await import(pathToFileURL(artifactRequire.resolve('@oai/artifact-tool')).href);
const root=path.resolve(import.meta.dirname,'../..'),sourceDir=path.join(root,'docs/balance');
const out=path.resolve(process.env.ALBUM_WORKBOOK_OUTPUT_DIR??sourceDir);
const d=JSON.parse(await fs.readFile(path.join(sourceDir,'album-balance-v1.json'),'utf8'));
const actual=JSON.parse(await fs.readFile(path.join(sourceDir,'assembly-runtime.json'),'utf8'));
let measuredReport=null;
try{measuredReport=JSON.parse(await fs.readFile(path.join(root,'docs/validation/modular-album/assembly-balance-results.json'),'utf8'));}catch(error){if(error.code!=='ENOENT')throw error;}
function finiteTree(value,label){
 if(typeof value==='number'&&!Number.isFinite(value))throw new Error(label+': non-finite number');
 if(value&&typeof value==='object')for(const [key,child] of Object.entries(value))finiteTree(child,label+'.'+key);
}
finiteTree(d,'design');finiteTree(actual,'runtime');if(measuredReport)finiteTree(measuredReport,'measurements');
for(const key of ['balance','rules','content','assemblyCatalog'])if(!actual.source?.[key])throw new Error('Missing runtime Player source: '+key);
for(const key of ['definitions','weapons','towers','buildings','starters'])if(!Array.isArray(actual[key]))throw new Error('Missing runtime collection: '+key);
if(actual.balanceRevision==null)throw new Error('Missing runtime balanceRevision');
const wb=Workbook.create();
const names=['Сводка','Башни','Оружие расчёт','Сооружения','Прогрессия','Детали основы','Механизмы','Детали оружия','Детали зданий','Текущая игра','Правила'];
const sheets=Object.fromEntries(names.map(n=>[n,wb.worksheets.add(n)]));
const letter=n=>{let s='';for(n++;n>0;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;};
const refs={};
function base(name,title,headers,rows,note=''){
 const s=sheets[name],last=letter(headers.length-1),end=Math.max(6,rows.length+5);
 s.showGridLines=false;s.getRange(`A1:${last}${end}`).format.font={name:'Arial',size:11,color:'#19375D'};
 s.getRange('A2').values=[[title]];s.getRange('A2').format.font={name:'Arial',size:16,bold:true,color:'#173568'};
 if(note){s.getRange('A3').values=[[note]];s.getRange('A3').format.font={name:'Arial',size:10,italic:true,color:'#5B6574'};}
 s.getRange(`A5:${last}5`).values=[headers];s.getRange(`A5:${last}5`).format={fill:'#254C7C',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:46,horizontalAlignment:'center',verticalAlignment:'center'};
 if(rows.length)s.getRange(`A6:${last}${end}`).values=rows;
 s.getRange(`A6:${last}${end}`).format.rowHeight=28;s.getRange(`A5:${last}${end}`).format.columnWidth=17;
 s.getRange(`A5:A${end}`).format.columnWidth=29;
 s.getRange(`A6:${last}${end}`).setNumberFormat('0.00');
 s.getRange(`A6:${last}${end}`).format.verticalAlignment='center';
 for(let r=6;r<=end;r+=2)s.getRange(`A${r}:${last}${r}`).format.fill='#F0F4F9';
 s.freezePanes.freezeRows(5);s.freezePanes.freezeColumns(1);
 s.tables.add(`A5:${last}${end}`,true,'Table'+names.indexOf(name));
 return s;
}
function source(name,title,items,cols,note){
 const s=base(name,title,cols.map(c=>c[1]),items.map(p=>cols.map(([k])=>p[k]??'')),note);
 s.getRange(`B5:B${items.length+5}`).format.columnWidth=35;
 refs[name]={items,cols};
 cols.forEach(([key],i)=>{const values=items.map(p=>p[key]).filter(v=>typeof v==='number');if(values.length&&values.every(Number.isInteger))s.getRange(`${letter(i)}6:${letter(i)}${items.length+5}`).setNumberFormat('0');});
 for(let j=0;j<cols.length;j++){
  if(['id','name','slot','compatible','notes','attack','source'].includes(cols[j][0])){s.getRange(`${letter(j)}6:${letter(j)}${items.length+5}`).setNumberFormat('@');s.getRange(`${letter(j)}5:${letter(j)}${items.length+5}`).format.columnWidth=cols[j][0]==='compatible'?42:cols[j][0]==='notes'?65:cols[j][0]==='name'?35:26;}
  else s.getRange(`${letter(j)}6:${letter(j)}${items.length+5}`).format.fill='#FFF4D6';
 }
 return s;
}
function cell(name,id,key){const q=refs[name];const row=q.items.findIndex(p=>p.id===id)+6,col=q.cols.findIndex(c=>c[0]===key);if(row<6||col<0)throw new Error('Reference '+name+id+key);return `'${name}'!${letter(col)}${row}`;}
function lookup(name,key,input){const q=refs[name],c=letter(q.cols.findIndex(c=>c[0]===key)),end=q.items.length+5;return `INDEX('${name}'!$${c}$6:$${c}$${end},MATCH(${input},'${name}'!$A$6:$A$${end},0))`;}
const fc=[['id','ID'],['name','Деталь'],['slot','Категория'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр.'],['load','Нагрузка'],['hp','Базовые HP'],['hpMult','HP ×'],['capacity','Вместимость'],['capacityAdd','Вместимость +'],['mounts','Крепления'],['feed','Подача рамки'],['feedMult','Подача ×'],['defense','Защита'],['frontDefense','Защита спереди'],['acquisitionMult','Захват ×'],['build','Сборка, с +']];
source('Детали основы','Детали основы башни',d.foundations,fc,'Жёлтые ячейки — проектные входные значения. Пустое поле неприменимо; нейтральный множитель равен 1.');
const cc=[['id','ID'],['name','Механизм'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр.'],['load','Нагрузка'],['damage','Урон'],['interval','Интервал, с'],['range','Дальность, кл.'],['minRange','Мин. дальность'],['clip','Обойма, шт.'],['reload','Смена обоймы, с'],['targets','Цели для оценки'],['radius','Радиус, кл.'],['slow','Скорость цели ×'],['slowSeconds','Эффект, с'],['flight','Полёт, с'],['jump','Переход, кл.'],['attack','Поведение'],['notes','Условие']];
source('Механизмы','Механизмы оружия',d.cores,cc,'Цена механизма задана после сборочного кредита. Обойма задаёт цикл новой модели.');
const wc=[['id','ID'],['name','Деталь'],['slot','Категория'],['compatible','Совместимые механизмы'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр. +'],['load','Нагрузка +'],['damageMult','Урон ×'],['intervalMult','Интервал ×'],['rangeMult','Дальность ×'],['clipMult','Обойма ×'],['clipOverride','Обойма замена'],['reloadMult','Перезарядка ×'],['acquisitionMult','Захват ×'],['radiusMult','Радиус ×'],['notes','Особое правило']];
source('Детали оружия','Стволы, прицелы, магазины и спуски',d.weaponParts,wc,'all — все механизмы; очередь разрешена только со штатной обоймой. Урон не зависит от изображения.');
const bc=[['id','ID'],['name','Деталь'],['slot','Категория'],['compatible','Семейство'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр.'],['load','Нагрузка'],['capacity','Вместимость'],['hp','Базовые HP'],['hpMult','HP ×'],['range','Радиус, кл.'],['rangeMult','Радиус ×'],['rate','HP/с или штр./волну'],['rateMult','Выработка ×'],['slow','Скорость цели ×'],['defense','Защита'],['build','Сборка, с'],['notes','Правило']];
source('Детали зданий','Составные части сооружений',d.buildingParts,bc,'Ремонт: HP/с. Генератор: штрихи за пережитую волну. Значения не являются уроном.');
const ruleLabels={slots:['Слоты','Общие для башен и сооружений'],maxAlbumPages:['Вместимость альбома','Пустые боевые слоты допустимы'],initialGold:['Начальное золото','Новый профиль'],startingInk:['Начальные штрихи','На начало партии'],waves:['Волн в партии','Текущая карта'],waveGold:['Золото за волну','Награда за завершённую волну'],victoryGold:['Золото за победу','Добавляется к наградам за волны'],xpPerWave:['XP за волну','Метапрогрессия'],xpVictory:['XP за победу','Добавляется к XP за волны'],maxFeed:['Максимум подачи','На каждый действующий ствол'],acquisition:['Базовый захват, с','При смене цели'],maxDefense:['Максимум защиты','С учётом материала и эффектов'],maxGenerators:['Генераторов на поле','Общий лимит на все варианты'],generatorRefund:['Возврат генератора','Доля цены при продаже'],generatorMaterial:['Материал генератора','Разрешённый материал'],towerRefund:['Возврат башни','Доля цены при продаже'],slowFloor:['Нижняя скорость цели','Сильнейшее замедление'],geometryPower:['Урон от геометрии','Разрешён ли расчёт урона по изображению']};
const ruleKeys=Object.keys(d.rules),ruleRows=ruleKeys.map(key=>[ruleLabels[key]?.[0]??key,d.rules[key],ruleLabels[key]?.[1]??key]);
ruleRows.push(['Версия',d.version,d.status],['Ревизия баланса',actual.balanceRevision,'Активный каталог Player'],['Оценка DPS','Без брони','Один ствол и непрерывная цель'],['Цикл обоймы','(N−1) × интервал + перезарядка','Последняя пауза заменена перезарядкой']);
const ruleRef=key=>{const i=ruleKeys.indexOf(key);if(i<0)throw new Error('Missing design rule: '+key);return `'Правила'!$B$${i+6}`;};
base('Правила','Правила расчёта и единицы',['Параметр','Значение','Пояснение'],ruleRows,'Постоянные покупки оплачиваются золотом; постройки во время боя — штрихами. Источник: album-balance-v1.json.');
ruleRows.forEach((row,i)=>{if(Number.isInteger(row[1]))sheets['Правила'].getRange(`B${i+6}`).setNumberFormat('0');});
sheets['Правила'].getRange(`C5:C${ruleRows.length+5}`).format.columnWidth=83;sheets['Правила'].getRange(`B5:B${ruleRows.length+5}`).format.columnWidth=48;sheets['Правила'].getRange(`A5:A${ruleRows.length+5}`).format.columnWidth=34;
const W=base('Оружие расчёт','Сборки оружия',['Вариант','Урон','Интервал, с','Обойма','Перезарядка, с','Цикл, с','DPS одной цели','Дальность','Мин. дальность','Цена, штр.','Нагрузка','Уровень','Захват, с','Радиус','Цели для оценки','Механизм','Ствол / головка','Прицел','Магазин','Спуск'],d.weapons.map(r=>[r.name,...Array(14).fill(null),r.core,r.head,r.sight,r.magazine,r.trigger]),'DPS = урон × обойма / цикл. Эффекты и дополнительное поражение целей оцениваются отдельно.');
for(let i=0;i<d.weapons.length;i++){
 const r=i+6,core=k=>lookup('Механизмы',k,`P${r}`),ps=k=>['Q','R','S','T'].map(c=>lookup('Детали оружия',k,`${c}${r}`)),mult=k=>ps(k).join('*'),sum=k=>ps(k).join('+');
 const formulas=[`=${core('damage')}*${mult('damageMult')}`,`=${core('interval')}*${mult('intervalMult')}`,`=IF(${lookup('Детали оружия','clipOverride',`T${r}`)}>0,${lookup('Детали оружия','clipOverride',`T${r}`)},${core('clip')}*${mult('clipMult')})`,`=${core('reload')}*${mult('reloadMult')}`,`=(D${r}-1)*C${r}+E${r}`,`=B${r}*D${r}/F${r}`,`=${core('range')}*${mult('rangeMult')}`,`=${core('minRange')}`,`=${core('cost')}+${sum('cost')}`,`=${core('load')}+${sum('load')}`,`=MAX(${[core('level'),...ps('level')].join(',')})`,`=${ruleRef('acquisition')}*${mult('acquisitionMult')}`,`=${core('radius')}*${mult('radiusMult')}`,`=${core('targets')}`];
 W.getRange(`B${r}:O${r}`).formulas=[formulas];
}
W.getRange('P5:T16').format.columnWidth=29;
const T=base('Башни','Сборки башен',['Рисунок','Цена, штр.','HP','Нагрузка','Вместимость','DPS одной цели','Дальность','Уровень','Подача / ствол','Защита','Эфф. HP','Рамка','Корпус','Броня','Подача','Поворот','Оружие','Стволов','Роль','Захват, с','Защита спереди'],d.towers.map(t=>[t.name,...Array(10).fill(null),t.frame,t.hull,t.armor,t.feed,t.turn,d.weapons[t.weapon].name,t.count,t.role,null,null]),'DPS учитывает общую подачу. Эффективные HP рассчитаны против обычного прямого урона; фронтальный щит исключён.');
for(let i=0;i<d.towers.length;i++){
 const r=i+6,p=k=>['L','M','N','O','P'].map(c=>lookup('Детали основы',k,`${c}${r}`)),fr=k=>lookup('Детали основы',k,`L${r}`),h=k=>lookup('Детали основы',k,`M${r}`),f=k=>lookup('Детали основы',k,`O${r}`),weapon=c=>`INDEX('Оружие расчёт'!${c}$6:${c}$${d.weapons.length+5},MATCH(Q${r},'Оружие расчёт'!$A$6:$A$${d.weapons.length+5},0))`;
 T.getRange(`B${r}:K${r}`).formulas=[[`=ROUNDUP(${p('cost').join('+')}+${weapon('J')}*R${r},0)`,`=${fr('hp')}*${h('hpMult')}`,`=${p('load').join('+')}+${weapon('K')}*R${r}`,`=${fr('capacity')}+${h('capacityAdd')}`,`=${weapon('G')}*R${r}*I${r}`,`=${weapon('H')}`,`=MAX(${p('level').join(',')},${weapon('L')})`,`=MIN(${ruleRef('maxFeed')},${f('feedMult')},${fr('feed')}*${f('feedMult')}/R${r})`,`=${lookup('Детали основы','defense',`N${r}`)}`,`=C${r}/(1-J${r})`]];
}
T.getRange(`L5:P${d.towers.length+5}`).format.columnWidth=27;T.getRange(`S5:S${d.towers.length+5}`).format.columnWidth=62;
T.getRange('T5:U5').values=[['Захват, с','Защита спереди']];T.getRange('T5:U5').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:46,columnWidth:18};
d.towers.forEach((tower,i)=>{const row=i+6;T.getRange(`T${row}:U${row}`).formulas=[[`=INDEX('Оружие расчёт'!$M$6:$M$${d.weapons.length+5},MATCH(Q${row},'Оружие расчёт'!$A$6:$A$${d.weapons.length+5},0))*${lookup('Детали основы','acquisitionMult',`P${row}`)}`,`=${lookup('Детали основы','frontDefense',`N${row}`)}`]];});
const B=base('Сооружения','Сборки сооружений',['Рисунок','Цена, штр.','HP','Нагрузка','Вместимость','Радиус','Выработка','Скорость цели ×','Защита','Уровень','Окупаемость, волн','Основание','Функция','Оболочка','Оснащение','Единица выработки','Сборка, с'],d.buildings.map(b=>[b.name,...Array(10).fill(null),b.site,b.function,b.shell,b.utility,b.family==='repair'?'HP/с':b.family==='generator'?'штр./волну':'не применяется',null]),'Генератор без возврата; ремонт одной цели. Доход зависит от числа полностью пережитых волн.');
for(let i=0;i<d.buildings.length;i++){
 const r=i+6,part=(col,k)=>lookup('Детали зданий',k,`${col}${r}`),ps=k=>['L','M','N','O'].map(c=>part(c,k));
 B.getRange(`B${r}:K${r}`).formulas=[[`=ROUNDUP(${ps('cost').join('+')},0)`,`=${part('L','hp')}*${part('N','hpMult')}*${part('O','hpMult')}`,`=${ps('load').join('+')}`,`=${part('L','capacity')}`,`=${part('M','range')}*${part('O','rangeMult')}`,`=${part('M','rate')}*${part('O','rateMult')}`,`=${part('M','slow')}`,`=${part('O','defense')}`,`=MAX(${ps('level').join(',')})`,`=IF(P${r}="штр./волну",ROUNDUP(B${r}/G${r},0),"—")`]];
}
B.getRange(`L5:O${d.buildings.length+5}`).format.columnWidth=29;
B.getRange('Q5').values=[['Сборка, с']];B.getRange('Q5').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:46,columnWidth:18};
d.buildings.forEach((building,i)=>{const row=i+6;B.getRange(`Q${row}`).formulas=[[`=${['L','M','N','O'].map(col=>lookup('Детали зданий','build',`${col}${row}`)).join('+')}`]];});
const starterItems=[...d.towers.filter(t=>t.starter).map(item=>({item,sheet:'Башни',levelCol:'H'})),...d.buildings.filter(b=>b.starter).map(item=>({item,sheet:'Сооружения',levelCol:'J'}))];
const summaryRows=starterItems.map(({item,sheet})=>[item.name,sheet==='Башни'?'Башня':'Сооружение',null,null,null,null,null,item.role??({repair:'Поддержка одной цели',wall:'Преграда на пути',slow:'Замедление',generator:'Доход за пережитую волну'}[item.family])]);
const S=base('Сводка','Альбом: стартовый набор',['Рисунок','Тип','Цена, штр.','HP','Уровень','Нагрузка','Вместимость','Роль'],summaryRows,'Жёлтый фон — редактируемые проектные входы. Текущие значения Player и измерения приведены на листе «Текущая игра».');
starterItems.forEach(({item,sheet,levelCol},i)=>{
 const r=i+6,items=sheet==='Башни'?d.towers:d.buildings,end=items.length+5;
 const ref=col=>`INDEX('${sheet}'!${col}$6:${col}$${end},MATCH(A${r},'${sheet}'!$A$6:$A$${end},0))`;
 S.getRange(`C${r}:G${r}`).formulas=[['B','C',levelCol,'D','E'].map(col=>'='+ref(col))];
 if(item.family==='generator')S.getRange(`H${r}`).formulas=[[`="Окупается за "&${ref('K')}&" волн"`]];
 if(item.family==='slow')S.getRange(`H${r}`).formulas=[[`="Скорость цели ×"&${ref('H')}`]];
 if(item.count>1)S.getRange(`H${r}`).formulas=[[`="Стволов: "&${ref('R')}&". Подача на ствол: "&${ref('I')}`]];
 if(item.armor==='armor.plate'){
  const weapon=d.weapons[item.weapon],wr=d.weapons.indexOf(weapon)+6;
  S.getRange(`H${r}`).formulas=[[`="Группа врагов. Мин. дальность: "&'Оружие расчёт'!I${wr}&" кл."`]];
 }
});
S.getRange('A16:B20').values=[['Один экземпляр каждого',null],['Стартовые штрихи',null],['Остаток после набора',null],['Всего деталей',d.partCount],['Полная победа, золото',null]];
S.getRange('B16').formulas=[[`=SUM(C6:C${starterItems.length+5})`]];S.getRange('B17').formulas=[['='+ruleRef('startingInk')]];S.getRange('B18').formulas=[['=B17-B16']];S.getRange('B20').formulas=[[`=${ruleRef('waves')}*${ruleRef('waveGold')}+${ruleRef('victoryGold')}`]];
S.getRange('A23').formulas=[['="Полный набор: "&B16&" штрихов. Остаток: "&B18&" штрихов."']];
S.getRange('A25').values=[['DPS описывает непрерывную стрельбу. Контроль, ремонт, доход и результат партии оцениваются игровыми прогонами.']];
S.getRange('H5:H13').format.columnWidth=62;S.getRange('A5:A20').format.columnWidth=33;S.tabColor='#173568';
S.getRange('A27').values=[['Слоты в партии']];S.getRange('B27').formulas=[['='+ruleRef('slots')]];
S.getRange('A28').values=[['Ревизия каталога Player']];S.getRange('B28').values=[[actual.balanceRevision]];
const all=[...d.foundations,...d.cores,...d.weaponParts,...d.buildingParts];
const P=sheets['Прогрессия'];P.showGridLines=false;P.getRange('A1:J90').format.font={name:'Arial',size:11,color:'#19375D'};
P.getRange('A2').values=[['Уровень и однократные покупки']];P.getRange('A2').format.font={name:'Arial',size:16,bold:true};
P.getRange('A4:D5').values=[['Уровень игрока',1,'Золото игрока',d.rules.waves*d.rules.waveGold+d.rules.victoryGold],['XP за победу',null,'Золото за победу',null]];
P.getRange('B5').formulas=[[`=${ruleRef('waves')}*${ruleRef('xpPerWave')}+${ruleRef('xpVictory')}`]];P.getRange('D5').formulas=[[`=${ruleRef('waves')}*${ruleRef('waveGold')}+${ruleRef('victoryGold')}`]];
P.getRange('B4').dataValidation={rule:{type:'whole',operator:'between',formula1:1,formula2:d.levels.length}};P.getRange('D4').dataValidation={rule:{type:'whole',operator:'between',formula1:0,formula2:1000000}};P.getRange('B4').format.fill='#FFF4D6';P.getRange('D4').format.fill='#FFF4D6';
P.getRange('A7:F7').values=[['Уровень','XP всего','Побед до уровня','Цена открываемых','Цена всех до уровня','Побед на все покупки']];
d.levels.forEach((l,i)=>{const r=i+8;P.getRange(`A${r}:B${r}`).values=[[l.level,l.xp]];P.getRange(`C${r}:F${r}`).formulas=[[`=ROUNDUP(B${r}/$B$5,0)`,`=SUMIFS($E$20:$E$${all.length+19},$D$20:$D$${all.length+19},A${r})`,`=SUM($D$8:D${r})`,`=ROUNDUP(E${r}/$D$5,0)`]];});
P.getRange('A18').values=[['Стоимость уровня не списывается: таблица суммирует доступные покупки, выбор остаётся игроку.']];
P.getRange('A19:G19').values=[['ID','Деталь','Категория','Уровень','Золото','Куплено: 0/1','Доступность']];
all.forEach((p,i)=>{const r=i+20;const sn=d.foundations.includes(p)?'Детали основы':d.cores.includes(p)?'Механизмы':d.weaponParts.includes(p)?'Детали оружия':'Детали зданий';P.getRange(`A${r}:C${r}`).values=[[p.id,p.name,p.slot??'Механизм']];P.getRange(`D${r}:E${r}`).formulas=[[`=${cell(sn,p.id,'level')}`,`=${cell(sn,p.id,'gold')}`]];P.getRange(`F${r}`).values=[[p.gold===0?1:0]];P.getRange(`G${r}`).formulas=[[`=IF(F${r}=1,"Открыто",IF($B$4<D${r},"Нужен уровень",IF($D$4<E${r},"Не хватает золота","Можно купить")))`]];});
P.getRange(`F20:F${all.length+19}`).dataValidation={rule:{type:'whole',operator:'between',formula1:0,formula2:1}};P.getRange(`F20:F${all.length+19}`).format.fill='#FFF4D6';P.getRange(`A4:G${all.length+19}`).format.columnWidth=22;P.getRange(`A19:B${all.length+19}`).format.columnWidth=38;P.getRange(`G19:G${all.length+19}`).format.columnWidth=25;P.getRange('A7:F7').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},rowHeight:46,wrapText:true};P.getRange('A19:G19').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},rowHeight:32};P.freezePanes.freezeRows(5);P.freezePanes.freezeColumns(1);
const actualRows=[];
const push=(id,name,field,value,unit='',source='assembly-runtime.json')=>actualRows.push([id,name,field,value,unit,source,actual.balanceRevision]);
for(const key of ['balance','rules','content','assemblyCatalog'])push('Player source','Активная цепочка Player',key,actual.source[key]);
push('balanceRevision','Каталог Player','balanceRevision',actual.balanceRevision);
for(const [key,value] of Object.entries(actual.rules??{}))push('rules','Правила Player',key,value);
for(const part of actual.definitions){
 const name=all.find(p=>p.id===part.id)?.name??part.id;
 for(const key of ['revision','level','gold'])push(part.id,name,key,part[key]);
 for(const [key,value] of Object.entries(part.numeric??{}))push(part.id,name,key,value);
}
for(const collection of ['weapons','towers','buildings'])for(const recipe of actual[collection]){
 const name=d[collection].find(p=>p.id===recipe.id)?.name??recipe.id;
 for(const [key,value] of Object.entries(recipe.resolved??{}))push(recipe.id,name,key,value);
}
actual.starters.forEach((item,i)=>push(typeof item==='string'?item:item.id,'Стартовый рецепт','slot',i+1));
// Measurements remain observations: they never feed the design calculation.
if(measuredReport){
 const rows=measuredReport.Rows??measuredReport.rows??[];
 push('measurement','Измеренные прогоны','Всего записей',rows.length,'','assembly-balance-results.json');
 const metrics=['Fixture','RecipeId','Scenario','Seed','Budget','Cost','Copies','DurationSeconds','Damage','Leaks','BaseHealth','PreventedMovement','RepairedHealth','Income','NetResource','CompletedWaves','Outcome'];
 const applicable={Damage:'DamageApplicable',Leaks:'LeaksApplicable',BaseHealth:'CastleApplicable',PreventedMovement:'ControlApplicable',RepairedHealth:'RepairApplicable',Income:'IncomeApplicable'};
 const displayed=[...rows.filter(row=>row.Fixture==='Campaign'),...rows.filter(row=>row.Fixture!=='Campaign').slice(0,18)];
 for(const [index,row] of displayed.entries()){
  const id=row.RecipeId??row.recipeId??'run.'+index;
  const context=[row.Fixture??row.fixture,row.Scenario??row.scenario,'seed '+(row.Seed??row.seed??''),'budget '+(row.Budget??row.budget??'')].filter(Boolean).join(', ');
  for(const key of metrics){if(applicable[key]&&!row[applicable[key]])continue;const value=row[key]??row[key[0].toLowerCase()+key.slice(1)];if(value!=null)push(id,context,key,value,'','assembly-balance-results.json');}
 }
 push('measurement','Измеренные прогоны','Показано записей',displayed.length,'Все кампании и первые 18 испытаний','assembly-balance-results.json');
 push('measurement','Полный отчёт','Источник','Все сценарии и исходные условия находятся в полном машинном отчёте.','','assembly-balance-results.json');
}else push('measurement','Измеренные прогоны','Отчёт','Отчёт прогонов отсутствует','','assembly-balance-results.json');
base('Текущая игра','Текущие значения Player и измеренные прогоны',['ID','Объект','Параметр','Значение','Единица','Источник','Ревизия баланса'],actualRows,'Цепочка PlayerBalance → PlayerRules → PlayerContent → AssemblyCatalog. Экспорт активного каталога и результаты resolver.');
sheets['Текущая игра'].getRange(`A5:A${actualRows.length+5}`).format.columnWidth=37;
sheets['Текущая игра'].getRange(`B5:B${actualRows.length+5}`).format.columnWidth=34;
sheets['Текущая игра'].getRange(`C5:C${actualRows.length+5}`).format.columnWidth=51;
sheets['Текущая игра'].getRange(`D5:D${actualRows.length+5}`).format.columnWidth=78;
sheets['Текущая игра'].getRange(`F5:F${actualRows.length+5}`).format.columnWidth=34;
sheets['Текущая игра'].getRange(`G5:G${actualRows.length+5}`).format.columnWidth=31;
for(const name of ['Башни','Сооружения']){const s=sheets[name];s.getRange('D6:D15').conditionalFormats.addCustom('D6>E6',{fill:'#FCE4D6',font:{color:'#9C0006',bold:true}});}
T.getRange('Q5:Q15').format.columnWidth=33;
for(const [sheet,columns,end] of [['Башни',['B','D','E','H','R'],15],['Оружие расчёт',['D','J','K','L','O'],16],['Сооружения',['B','D','E','J','K'],12],['Сводка',['C','E','F','G'],13]])for(const col of columns)sheets[sheet].getRange(`${col}6:${col}${end}`).setNumberFormat('0');
await fs.mkdir(path.join(out,'preview'),{recursive:true});
wb.recalculate();
const checks=[];
function near(name,value,expected){
 if(typeof value!=='number'||!Number.isFinite(value)||typeof expected!=='number'||!Number.isFinite(expected)||Math.abs(value-expected)>1e-5)throw new Error(name+': '+value+' != '+expected);
 checks.push(name);
}
function same(name,value,expected){if(value!==expected)throw new Error(name+': '+value+' != '+expected);checks.push(name);}
function keyed(items,label){const result=new Map();for(const item of items){if(!item.id||result.has(item.id))throw new Error('Missing/duplicate '+label+' ID '+item.id);result.set(item.id,item);}return result;}
const definitions=keyed(actual.definitions,'runtime definition');
same('runtime definition count',definitions.size,all.length);
for(const part of all){
 const current=definitions.get(part.id);if(!current)throw new Error('Missing runtime definition '+part.id);
 near(part.id+' unlock level',current.level,part.level);near(part.id+' unlock gold',current.gold,part.gold);
 if(!Number.isInteger(current.revision)||current.revision<1)throw new Error('Invalid definition revision '+part.id);
 const expected=Object.fromEntries(Object.entries(part).filter(([key,value])=>typeof value==='number'&&!['level','gold'].includes(key)));
 for(const [key,value] of Object.entries(expected))near(part.id+' numeric '+key,current.numeric?.[key],value);

}
for(const [key,value] of Object.entries(d.rules)){
 if(typeof value==='number')near('runtime rule '+key,actual.rules?.[key],value);else same('runtime rule '+key,actual.rules?.[key],value);
}
const maps={weapons:{damage:'B',interval:'C',clip:'D',reload:'E',cycle:'F',dps:'G',range:'H',minRange:'I',cost:'J',load:'K',level:'L',acquisition:'M',radius:'N',targets:'O'},towers:{cost:'B',hp:'C',load:'D',capacity:'E',dps:'F',range:'G',level:'H',feed:'I',defense:'J',acquisition:'T',frontDefense:'U'},buildings:{cost:'B',hp:'C',load:'D',capacity:'E',range:'F',rate:'G',slow:'H',defense:'I',level:'J',build:'Q'}};
same('runtime progression count',actual.levels?.length,d.levels.length);
d.levels.forEach((level,i)=>{near('runtime level '+level.level,actual.levels[i].level,level.level);near('runtime XP '+level.level,actual.levels[i].xp,level.xp);near('formula XP '+level.level,P.getRange(`B${i+8}`).values[0][0],actual.levels[i].xp);});
for(const [collection,sheet] of [['weapons',W],['towers',T],['buildings',B]]){
 const runtime=keyed(actual[collection],'runtime '+collection);same(collection+' recipe count',runtime.size,d[collection].length);
 d[collection].forEach((recipe,i)=>{
  const current=runtime.get(recipe.id);if(!current)throw new Error('Missing runtime recipe '+recipe.id);
  for(const [key,col] of Object.entries(maps[collection])){
   near('formula '+recipe.id+'.'+key,sheet.getRange(`${col}${i+6}`).values[0][0],recipe.resolved[key]);
   near('runtime '+recipe.id+'.'+key,current.resolved?.[key],recipe.resolved[key]);
  }
  for(const key of Object.keys(recipe.resolved))if(!(key in maps[collection]))throw new Error('Unverified resolved field '+recipe.id+'.'+key);
  if(collection==='towers')near('effective HP '+recipe.id,sheet.getRange(`K${i+6}`).values[0][0],recipe.resolved.hp/(1-recipe.resolved.defense));
  if(collection==='buildings'&&recipe.family==='generator')near('payback '+recipe.id,sheet.getRange(`K${i+6}`).values[0][0],Math.ceil(recipe.resolved.cost/recipe.resolved.rate));
 });
}
const runtimeStarters=actual.starters.map(item=>typeof item==='string'?item:item.id),designStarters=starterItems.map(({item})=>item.id);
same('starter count',runtimeStarters.length,designStarters.length);
same('starter identifiers',JSON.stringify([...runtimeStarters].sort()),JSON.stringify([...designStarters].sort()));
const starterCost=starterItems.reduce((sum,{item})=>sum+item.resolved.cost,0),victoryGold=d.rules.waves*d.rules.waveGold+d.rules.victoryGold;
near('starter budget',S.getRange('B16').values[0][0],starterCost);near('remaining ink',S.getRange('B18').values[0][0],d.rules.startingInk-starterCost);near('gold victory',S.getRange('B20').values[0][0],victoryGold);
near('progression victory XP',P.getRange('B5').values[0][0],d.rules.waves*d.rules.xpPerWave+d.rules.xpVictory);
near('progression victory gold',P.getRange('D5').values[0][0],victoryGold);
// Mutation tests restore every saved input even if a dependent assertion fails.
const mutations=[];
function temporarily(sheet,address,value){const range=sheet.getRange(address);mutations.push({range,values:range.values.map(row=>[...row]),formulas:range.formulas?.map(row=>[...row])});range.values=[[value]];}
function restore(){for(const {range,values,formulas} of mutations.reverse())if(formulas?.some(row=>row.some(Boolean)))range.formulas=formulas;else range.values=values;mutations.length=0;wb.recalculate();}
try{
 const gun=d.cores.find(core=>core.id==='core.gun'),gunCell=cell('Механизмы',gun.id,'damage').split('!')[1],nextDamage=gun.damage*2;
 temporarily(sheets['Механизмы'],gunCell,nextDamage);wb.recalculate();
 d.weapons.forEach((weapon,i)=>{if(weapon.core===gun.id)near('weapon damage edit '+weapon.id,W.getRange(`G${i+6}`).values[0][0],weapon.resolved.dps*2);});
 d.towers.forEach((tower,i)=>{if(d.weapons[tower.weapon].core===gun.id)near('tower damage edit '+tower.id,T.getRange(`F${i+6}`).values[0][0],tower.resolved.dps*2);});
}finally{restore();}
try{
 temporarily(sheets['Правила'],ruleRef('startingInk').split('!')[1],d.rules.startingInk+100);wb.recalculate();near('starting ink edit',S.getRange('B18').values[0][0],d.rules.startingInk+100-starterCost);
 temporarily(sheets['Правила'],ruleRef('waveGold').split('!')[1],d.rules.waveGold+1);wb.recalculate();near('wave gold summary edit',S.getRange('B20').values[0][0],victoryGold+d.rules.waves);near('wave gold progression edit',P.getRange('D5').values[0][0],victoryGold+d.rules.waves);
}finally{restore();}
const locked=all.find(part=>part.level>1&&part.gold>0),unlockRow=all.indexOf(locked)+20;
if(!locked)throw new Error('No paid level-gated part for purchase verification');
try{
 temporarily(P,'B4',locked.level-1);temporarily(P,'D4',locked.gold);wb.recalculate();same('level gate',P.getRange('G'+unlockRow).values[0][0],'Нужен уровень');
 P.getRange('B4').values=[[locked.level]];P.getRange('D4').values=[[locked.gold-1]];wb.recalculate();same('gold boundary below',P.getRange('G'+unlockRow).values[0][0],'Не хватает золота');
 P.getRange('D4').values=[[locked.gold]];wb.recalculate();same('gold boundary exact',P.getRange('G'+unlockRow).values[0][0],'Можно купить');
}finally{restore();}
wb.recalculate();
near('restored starter budget',S.getRange('B16').values[0][0],starterCost);near('restored remaining ink',S.getRange('B18').values[0][0],d.rules.startingInk-starterCost);
const errors=await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:300},maxChars:20000});
const errorToken=/^#(?:REF!|DIV\/0!|VALUE!|NAME\?|N\/A|NUM!|NULL!|SPILL!|CALC!)$/;
const errorCells=[];
for(const name of names){const range=sheets[name].getUsedRange();range.values.forEach((row,r)=>row.forEach((value,c)=>{if(typeof value==='string'&&errorToken.test(value))errorCells.push({sheet:name,rowOffset:r,columnOffset:c,value});}));}
const scanRecords=errors.ndjson.trim().split(/\r?\n/).filter(Boolean).map(line=>JSON.parse(line));
function containsError(value){return typeof value==='string'?errorToken.test(value):value&&typeof value==='object'?Object.entries(value).some(([key,child])=>!['searchTerm','summary'].includes(key)&&containsError(child)):false;}
const actualMatches=scanRecords.filter(containsError);
await fs.writeFile(path.join(out,'formula-scan.json'),JSON.stringify({actualMatches,errorCells,inspect:scanRecords},null,2));
if(actualMatches.length||errorCells.length)throw new Error('Workbook formula errors: '+JSON.stringify({actualMatches,errorCells}));
const keyRanges=[];
for(const [name,range] of [['Сводка','A16:B20'],['Башни','A5:K8'],['Оружие расчёт','A5:O8'],['Сооружения','A5:K8'],['Прогрессия','A4:F10'],['Текущая игра','A5:G10']])keyRanges.push({sheet:name,inspection:(await wb.inspect({kind:'table',range:`'${name}'!${range}`,include:'values,formulas',tableMaxRows:8,tableMaxCols:15,maxChars:8000})).ndjson});
for(const name of names){const preview=await wb.render({sheetName:name,range:name==='Прогрессия'?'A1:G15':name==='Правила'?'A1:C16':'A1:H14',scale:1.3,format:'png'});await fs.writeFile(path.join(out,'preview',name+'.png'),new Uint8Array(await preview.arrayBuffer()));}
for(const [sheetName,label,range] of [['Сводка','Сводка-итоги','A16:H28'],['Башни','Башни-дополнительно','S5:U15'],['Сооружения','Сооружения-дополнительно','P5:Q12'],['Правила','Правила-продолжение',`A17:C${ruleRows.length+5}`]]){const preview=await wb.render({sheetName,range,scale:1.3,format:'png'});await fs.writeFile(path.join(out,'preview',label+'.png'),new Uint8Array(await preview.arrayBuffer()));}
const xlsx=await SpreadsheetFile.exportXlsx(wb);await xlsx.save(path.join(out,'PaperDefense-Album-Balance-v1.xlsx'));
await fs.writeFile(path.join(out,'workbook-validation.json'),JSON.stringify({date:d.date,engine:'@oai/artifact-tool',independentValueChecks:checks.length,checks,inputMutation:true,levelAndGoldBoundaries:true,parts:all.length,sheets:names,formulaScan:{actualMatches:actualMatches.length,errorCells:errorCells.length},runtimeSource:actual.source,balanceRevision:actual.balanceRevision,measurementReport:measuredReport!==null,keyRanges,excelDesktopRecalculation:false},null,2));
console.log(JSON.stringify({file:'PaperDefense-Album-Balance-v1.xlsx',sheets:names.length,parts:all.length,checks:checks.length}));
