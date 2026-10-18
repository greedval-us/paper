import fs from 'node:fs/promises';
import path from 'node:path';
import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const root=path.resolve(import.meta.dirname,'../..'),out=path.join(root,'docs/balance');
const d=JSON.parse(await fs.readFile(path.join(out,'album-balance-v1.json'),'utf8'));
const actual=JSON.parse(await fs.readFile(path.join(root,'docs/audit/2026-10-17-album/current-runtime.json'),'utf8'));
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
source('Механизмы','Механизмы оружия',d.cores,cc,'Цена механизма уже учитывает сборочный кредит 16 штрихов текущей игры. Обойма задаёт цикл новой модели.');
const wc=[['id','ID'],['name','Деталь'],['slot','Категория'],['compatible','Совместимые механизмы'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр. +'],['load','Нагрузка +'],['damageMult','Урон ×'],['intervalMult','Интервал ×'],['rangeMult','Дальность ×'],['clipMult','Обойма ×'],['clipOverride','Обойма замена'],['reloadMult','Перезарядка ×'],['acquisitionMult','Захват ×'],['radiusMult','Радиус ×'],['notes','Особое правило']];
source('Детали оружия','Стволы, прицелы, магазины и спуски',d.weaponParts,wc,'all — все 7 механизмов; очередь 3 разрешена только со штатной обоймой. Урон не зависит от изображения.');
const bc=[['id','ID'],['name','Деталь'],['slot','Категория'],['compatible','Семейство'],['level','Уровень'],['gold','Открытие, зол.'],['cost','Цена, штр.'],['load','Нагрузка'],['capacity','Вместимость'],['hp','Базовые HP'],['hpMult','HP ×'],['range','Радиус, кл.'],['rangeMult','Радиус ×'],['rate','HP/с или штр./волну'],['rateMult','Выработка ×'],['slow','Скорость цели ×'],['defense','Защита'],['build','Сборка, с'],['notes','Правило']];
source('Детали зданий','Составные части сооружений',d.buildingParts,bc,'Ремонт: HP/с. Генератор: штрихи за пережитую волну. Значения не являются уроном.');
const ruleRows=[['Версия','album-v1.0','Проектный баланс, не импортирован в игру'],['Слоты',8,'Общие для башен и сооружений'],['Начальные штрихи',d.rules.startingInk,'Текущее значение основной игры'],['Волн в партии',8,'Текущая карта'],['Золото за волну',d.rules.waveGold,'Текущее значение'],['Золото за победу',d.rules.victoryGold,'Текущее значение'],['XP за волну',10,'Новая метапрогрессия'],['XP за победу',40,'Новая метапрогрессия'],['Максимум подачи',1.25,'Ограничивает каждый действующий ствол'],['Базовый захват, с',.25,'До первого попадания при смене цели'],['Максимум защиты',.6,'С учётом материала и эффектов'],['Генераторов на поле',2,'Общий лимит на все варианты'],['Возврат генератора',0,'За продажу; исключает доход от перестройки'],['Стоимость одного рисунка',0,'Сохранение и копия не расходуют золото'],['Вместимость альбома',64,'Пустые боевые слоты допустимы'],['Нижняя скорость цели',.4,'Сильнейший slow; не произведение'],['Материал: Карандаш',1,'Цена ×1, HP ×1, урон ×1'],['Материал: Ручка',1.8,'HP ×2,1; время ×1,3; урон ×1'],['Материал: Маркер',3.2,'HP ×4,2; время ×1,75; урон ×1'],['Оценка DPS','Без брони','Один ствол, непрерывная цель, без эффектов цвета'],['Цикл обоймы','(N−1) × интервал + перезарядка','Последняя пауза заменена перезарядкой; двойного ожидания нет'],['Цвет','Существующий бюджет','Цвет не меняет ID или цену открытия детали']];
ruleRows.push(['Материал генератора','Только карандаш','В v1 ручка и маркер не окупаются за 8 волн; усиление через детали']);
base('Правила','Правила расчёта и единицы',['Параметр','Значение','Пояснение'],ruleRows,'Постоянные покупки оплачиваются золотом; постройки во время боя — штрихами.');
ruleRows.forEach((row,i)=>{if(Number.isInteger(row[1]))sheets['Правила'].getRange(`B${i+6}`).setNumberFormat('0');});
sheets['Правила'].getRange('C5:C27').format.columnWidth=83;sheets['Правила'].getRange('B5:B27').format.columnWidth=48;sheets['Правила'].getRange('A5:A27').format.columnWidth=34;
const W=base('Оружие расчёт','Сборки оружия',['Вариант','Урон','Интервал, с','Обойма','Перезарядка, с','Цикл, с','DPS одной цели','Дальность','Мин. дальность','Цена, штр.','Нагрузка','Уровень','Захват, с','Радиус','Цели для оценки','Механизм','Ствол / головка','Прицел','Магазин','Спуск'],d.weapons.map(r=>[r.name,...Array(14).fill(null),r.core,r.head,r.sight,r.magazine,r.trigger]),'DPS = урон × обойма / цикл. Эффекты и дополнительное поражение целей оцениваются отдельно.');
for(let i=0;i<d.weapons.length;i++){
 const r=i+6,core=k=>lookup('Механизмы',k,`P${r}`),ps=k=>['Q','R','S','T'].map(c=>lookup('Детали оружия',k,`${c}${r}`)),mult=k=>ps(k).join('*'),sum=k=>ps(k).join('+');
 const formulas=[`=${core('damage')}*${mult('damageMult')}`,`=${core('interval')}*${mult('intervalMult')}`,`=IF(${lookup('Детали оружия','clipOverride',`T${r}`)}>0,${lookup('Детали оружия','clipOverride',`T${r}`)},${core('clip')}*${mult('clipMult')})`,`=${core('reload')}*${mult('reloadMult')}`,`=(D${r}-1)*C${r}+E${r}`,`=B${r}*D${r}/F${r}`,`=${core('range')}*${mult('rangeMult')}`,`=${core('minRange')}`,`=${core('cost')}+${sum('cost')}`,`=${core('load')}+${sum('load')}`,`=MAX(${[core('level'),...ps('level')].join(',')})`,`='Правила'!B15*${mult('acquisitionMult')}`,`=${core('radius')}*${mult('radiusMult')}`,`=${core('targets')}`];
 W.getRange(`B${r}:O${r}`).formulas=[formulas];
}
W.getRange('P5:T16').format.columnWidth=29;
const T=base('Башни','Сборки башен',['Рисунок','Цена, штр.','HP','Нагрузка','Вместимость','DPS одной цели','Дальность','Уровень','Подача / ствол','Защита','Эфф. HP','Рамка','Корпус','Броня','Подача','Поворот','Оружие','Стволов','Роль'],d.towers.map(t=>[t.name,...Array(10).fill(null),t.frame,t.hull,t.armor,t.feed,t.turn,d.weapons[t.weapon].name,t.count,t.role]),'DPS учитывает общую подачу. Эффективные HP рассчитаны против обычного прямого урона; фронтальный щит исключён.');
for(let i=0;i<d.towers.length;i++){
 const r=i+6,p=k=>['L','M','N','O','P'].map(c=>lookup('Детали основы',k,`${c}${r}`)),fr=k=>lookup('Детали основы',k,`L${r}`),h=k=>lookup('Детали основы',k,`M${r}`),f=k=>lookup('Детали основы',k,`O${r}`),weapon=c=>`INDEX('Оружие расчёт'!${c}$6:${c}$16,MATCH(Q${r},'Оружие расчёт'!$A$6:$A$16,0))`;
 T.getRange(`B${r}:K${r}`).formulas=[[`=ROUNDUP(${p('cost').join('+')}+${weapon('J')}*R${r},0)`,`=${fr('hp')}*${h('hpMult')}`,`=${p('load').join('+')}+${weapon('K')}*R${r}`,`=${fr('capacity')}+${h('capacityAdd')}`,`=${weapon('G')}*R${r}*I${r}`,`=${weapon('H')}`,`=MAX(${p('level').join(',')},${weapon('L')})`,`=MIN('Правила'!B14,${f('feedMult')},${fr('feed')}*${f('feedMult')}/R${r})`,`=${lookup('Детали основы','defense',`N${r}`)}`,`=C${r}/(1-J${r})`]];
}
T.getRange('L5:P15').format.columnWidth=27;T.getRange('S5:S15').format.columnWidth=62;
const B=base('Сооружения','Сборки сооружений',['Рисунок','Цена, штр.','HP','Нагрузка','Вместимость','Радиус','Выработка','Скорость цели ×','Защита','Уровень','Окупаемость, волн','Основание','Функция','Оболочка','Оснащение','Единица выработки'],d.buildings.map(b=>[b.name,...Array(10).fill(null),b.site,b.function,b.shell,b.utility,b.family==='repair'?'HP/с':b.family==='generator'?'штр./волну':'не применяется']),'Генератор без возврата; ремонт одной цели. Доход зависит от числа полностью пережитых волн.');
for(let i=0;i<d.buildings.length;i++){
 const r=i+6,part=(col,k)=>lookup('Детали зданий',k,`${col}${r}`),ps=k=>['L','M','N','O'].map(c=>part(c,k));
 B.getRange(`B${r}:K${r}`).formulas=[[`=ROUNDUP(${ps('cost').join('+')},0)`,`=${part('L','hp')}*${part('N','hpMult')}*${part('O','hpMult')}`,`=${ps('load').join('+')}`,`=${part('L','capacity')}`,`=${part('M','range')}*${part('O','rangeMult')}`,`=${part('M','rate')}*${part('O','rateMult')}`,`=${part('M','slow')}`,`=${part('O','defense')}`,`=MAX(${ps('level').join(',')})`,`=IF(P${r}="штр./волну",ROUNDUP(B${r}/G${r},0),"—")`]];
}
B.getRange('L5:O12').format.columnWidth=29;
const summaryRows=[...d.towers.filter(t=>t.starter).map(t=>[t.name,'Башня',null,null,null,null,null,t.role]),...d.buildings.filter(b=>b.starter).map(b=>[b.name,'Сооружение',null,null,null,null,null,b.family==='generator'?'Окупается за 5 волн':b.family==='repair'?'Поддержка одной цели':b.family==='slow'?'Замедление ×0,6':'Преграда на пути'])];
const S=base('Сводка','Альбом: стартовый набор',['Рисунок','Тип','Цена, штр.','HP','Уровень','Нагрузка','Вместимость','Роль'],summaryRows,'18.10.2026 · 8 общих слотов · расчётный баланс v1.0. Жёлтый фон — редактируемые входы, белый — формулы.');
for(let i=0;i<8;i++){const r=i+6,src=i<4?'Башни':'Сооружения',end=i<4?15:12;S.getRange(`C${r}:G${r}`).formulas=[['B','C',i<4?'H':'J','D','E'].map(c=>`=INDEX('${src}'!${c}$6:${c}${end},MATCH(A${r},'${src}'!$A$6:$A${end},0))`)];}
S.getRange('A16:B20').values=[['Один экземпляр каждого',null],['Стартовые штрихи',null],['Остаток после набора',null],['Всего деталей',d.partCount],['Полная победа, золото',null]];
S.getRange('B16').formulas=[['=SUM(C6:C13)']];S.getRange('B17').formulas=[["='Правила'!B8"]];S.getRange('B18').formulas=[['=B17-B16']];S.getRange('B20').formulas=[["='Правила'!B9*'Правила'!B10+'Правила'!B11"]];
S.getRange('A23:H23').values=[['Полный набор стоит 426 штрихов; останется 214 на дополнительные копии и перестройку.',null,null,null,null,null,null,null]];
S.getRange('A25:H25').values=[['Баланс новых деталей и сооружений требует игровых прогонов; текущие значения вынесены на отдельный лист.',null,null,null,null,null,null,null]];
S.getRange('H5:H13').format.columnWidth=58;S.getRange('A5:A20').format.columnWidth=33;S.tabColor='#173568';
const all=[...d.foundations,...d.cores,...d.weaponParts,...d.buildingParts];
const P=sheets['Прогрессия'];P.showGridLines=false;P.getRange('A1:J90').format.font={name:'Arial',size:11,color:'#19375D'};
P.getRange('A2').values=[['Уровень и однократные покупки']];P.getRange('A2').format.font={name:'Arial',size:16,bold:true};
P.getRange('A4:D5').values=[['Уровень игрока',1,'Золото игрока',94],['XP за победу',null,'Золото за победу',null]];
P.getRange('B5').formulas=[["='Правила'!B9*'Правила'!B12+'Правила'!B13"]];P.getRange('D5').formulas=[["='Правила'!B9*'Правила'!B10+'Правила'!B11"]];
P.getRange('B4').dataValidation={rule:{type:'whole',operator:'between',formula1:1,formula2:8}};P.getRange('D4').dataValidation={rule:{type:'whole',operator:'between',formula1:0,formula2:1000000}};P.getRange('B4').format.fill='#FFF4D6';P.getRange('D4').format.fill='#FFF4D6';
P.getRange('A7:F7').values=[['Уровень','XP всего','Побед до уровня','Цена открываемых','Цена всех до уровня','Побед на все покупки']];
d.levels.forEach((l,i)=>{const r=i+8;P.getRange(`A${r}:B${r}`).values=[[l.level,l.xp]];P.getRange(`C${r}:F${r}`).formulas=[[`=ROUNDUP(B${r}/$B$5,0)`,`=SUMIFS($E$20:$E$73,$D$20:$D$73,A${r})`,`=SUM($D$8:D${r})`,`=ROUNDUP(E${r}/$D$5,0)`]];});
P.getRange('A18').values=[['Стоимость уровня не списывается: таблица суммирует доступные покупки, выбор остаётся игроку.']];
P.getRange('A19:G19').values=[['ID','Деталь','Категория','Уровень','Золото','Куплено: 0/1','Доступность']];
all.forEach((p,i)=>{const r=i+20;const sn=d.foundations.includes(p)?'Детали основы':d.cores.includes(p)?'Механизмы':d.weaponParts.includes(p)?'Детали оружия':'Детали зданий';P.getRange(`A${r}:C${r}`).values=[[p.id,p.name,p.slot??'Механизм']];P.getRange(`D${r}:E${r}`).formulas=[[`=${cell(sn,p.id,'level')}`,`=${cell(sn,p.id,'gold')}`]];P.getRange(`F${r}`).values=[[p.gold===0?1:0]];P.getRange(`G${r}`).formulas=[[`=IF(F${r}=1,"Открыто",IF($B$4<D${r},"Нужен уровень",IF($D$4<E${r},"Не хватает золота","Можно купить")))`]];});
P.getRange('F20:F73').dataValidation={rule:{type:'whole',operator:'between',formula1:0,formula2:1}};P.getRange('F20:F73').format.fill='#FFF4D6';P.getRange('A4:G73').format.columnWidth=22;P.getRange('A19:B73').format.columnWidth=38;P.getRange('G19:G73').format.columnWidth=25;P.getRange('A7:F7').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},rowHeight:46,wrapText:true};P.getRange('A19:G19').format={fill:'#254C7C',font:{bold:true,color:'#FFFFFF'},rowHeight:32};P.freezePanes.freezeRows(5);P.freezePanes.freezeColumns(1);
const actualRows=[];const push=(id,name,field,value,unit)=>actualRows.push([id,name,field,value,unit,'Unity runtime export',actual.ExportedUtc]);
actual.Chassis.forEach(p=>['Capacity','BaseCost','BaseHealth','HealthMultiplier','MountCount','FeedCapacity','UnlockGold'].forEach(k=>push(p.Id,p.Name,k,p[k],'')));
actual.Weapons.forEach(p=>{['Load','AssemblyCredit','MinimumRange','UnlockGold'].forEach(k=>push(p.Id,p.Name,k,p[k],''));['Cost','Damage','ShotInterval','Range','Radius','MaxPierce','MaxTargets'].forEach(k=>push(p.Id,p.Name,'Stats.'+k,p.Stats[k],''));});
actual.Modules.forEach(p=>{['Cost','Load','UnlockGold'].forEach(k=>push(p.Id,p.Name,k,p[k],''));p.Modifiers.forEach((m,i)=>push(p.Id,p.Name,'Modifier '+i+' stat '+m.Stat,m.Value,'op '+m.Operation));});
actual.Structures.filter(p=>[0,9,10].includes(p.Kind)).forEach(p=>['Cost','MaxHealth','Radius','Range','SlowSpeedMultiplier','RepairPerSecond'].forEach(k=>push('StructureKind.'+p.Kind,'Существующее сооружение',k,p[k],'')));
actual.Materials.forEach((p,i)=>Object.entries(p).forEach(([k,v])=>push('material.'+i,['Карандаш','Ручка','Маркер'][i],k,v,'')));
['Slots','ProfileVersion','StartingInk','WaveGold','VictoryGold','WaveInkGrant','WellInkPerWave'].forEach(k=>push('rules','Основная игра',k,actual[k],''));
base('Текущая игра','Реальные значения основной игры',['ID','Объект','Параметр','Значение','Единица','Источник','Снято UTC'],actualRows,'Исходный снимок Unity. Не заменяется проектными значениями; повторный экспорт выполняется перед реализацией.');
sheets['Текущая игра'].getRange(`B5:C${actualRows.length+5}`).format.columnWidth=34;sheets['Текущая игра'].getRange(`G5:G${actualRows.length+5}`).format.columnWidth=31;
sheets['Текущая игра'].getRange(`G6:G${actualRows.length+5}`).setNumberFormat('yyyy-mm-dd hh:mm');
for(const name of ['Башни','Сооружения']){const s=sheets[name];s.getRange('D6:D15').conditionalFormats.addCustom('D6>E6',{fill:'#FCE4D6',font:{color:'#9C0006',bold:true}});}
T.getRange('Q5:Q15').format.columnWidth=33;
for(const [sheet,columns,end] of [['Башни',['B','D','E','H','R'],15],['Оружие расчёт',['D','J','K','L','O'],16],['Сооружения',['B','D','E','J','K'],12],['Сводка',['C','E','F','G'],13]])for(const col of columns)sheets[sheet].getRange(`${col}6:${col}${end}`).setNumberFormat('0');
await fs.mkdir(path.join(out,'preview'),{recursive:true});
wb.recalculate();
const checks=[];
function near(name,actual,expected){if(typeof actual!=='number'||Math.abs(actual-expected)>1e-5)throw new Error(name+': '+actual+' != '+expected);checks.push(name);}
d.weapons.forEach((v,i)=>near('weapon DPS '+i,W.getRange(`G${i+6}`).values[0][0],v.resolved.dps));
d.towers.forEach((v,i)=>{near('tower cost '+i,T.getRange(`B${i+6}`).values[0][0],v.resolved.cost);near('tower DPS '+i,T.getRange(`F${i+6}`).values[0][0],v.resolved.dps);});
d.buildings.forEach((v,i)=>{near('building cost '+i,B.getRange(`B${i+6}`).values[0][0],v.resolved.cost);near('building rate '+i,B.getRange(`G${i+6}`).values[0][0],v.resolved.rate);});
near('starter budget',S.getRange('B16').values[0][0],426);near('remaining ink',S.getRange('B18').values[0][0],214);near('gold victory',S.getRange('B20').values[0][0],94);
// A real edit must propagate from an input through both builds to the main output.
const gunCell=cell('Механизмы','core.gun','damage').split('!')[1],gunSheet=sheets['Механизмы'];
gunSheet.getRange(gunCell).values=[[24]];wb.recalculate();near('damage edit propagates',T.getRange('F6').values[0][0],2*d.towers[0].resolved.dps);gunSheet.getRange(gunCell).values=[[12]];
P.getRange('B4').values=[[1]];P.getRange('D4').values=[[1000]];wb.recalculate();const unlockRow=all.findIndex(p=>p.id==='head.long')+20;if(P.getRange('G'+unlockRow).values[0][0]!=='Нужен уровень')throw new Error('level gate');P.getRange('B4').values=[[8]];P.getRange('D4').values=[[0]];wb.recalculate();if(P.getRange('G'+unlockRow).values[0][0]!=='Не хватает золота')throw new Error('gold gate');P.getRange('D4').values=[[1000]];wb.recalculate();if(P.getRange('G'+unlockRow).values[0][0]!=='Можно купить')throw new Error('purchase gate');P.getRange('B4').values=[[1]];P.getRange('D4').values=[[94]];
wb.recalculate();
const errors=await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:30},maxChars:3500});
await fs.writeFile(path.join(out,'formula-scan.json'),errors.ndjson);
console.log('Formula scan',errors.ndjson);
for(const name of names){const preview=await wb.render({sheetName:name,range:name==='Прогрессия'?'A1:G15':name==='Правила'?'A1:C16':'A1:H14',scale:1.3,format:'png'});await fs.writeFile(path.join(out,'preview',name+'.png'),new Uint8Array(await preview.arrayBuffer()));}
const xlsx=await SpreadsheetFile.exportXlsx(wb);await xlsx.save(path.join(out,'PaperDefense-Album-Balance-v1.xlsx'));
await fs.writeFile(path.join(out,'workbook-validation.json'),JSON.stringify({date:d.date,engine:'@oai/artifact-tool',independentValueChecks:checks.length,checks,inputMutation:true,levelAndGoldBoundaries:true,parts:all.length,sheets:names,formulaScan:errors.ndjson,excelDesktopRecalculation:false},null,2));
console.log(JSON.stringify({file:'PaperDefense-Album-Balance-v1.xlsx',sheets:names.length,parts:all.length,checks:checks.length}));
