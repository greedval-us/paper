import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const root=path.resolve(import.meta.dirname,'../..');

// Authoritative balance inputs. Runtime exports are verification evidence, never generator inputs.
export function buildDesignData(){
const version='album-v1.4';
const common={level:1,gold:0,cost:0,load:0,build:0,hpMult:1,capacityAdd:0,feedMult:1,defense:0,frontDefense:0,acquisitionMult:1};
const foundations=[];
function f(id,name,slot,values={}){foundations.push({...common,id,name,slot,compatible:'all',...values});}
const frames=[
 ['standard','Обычная рамка',1,0,16,55,10,2,1.6,0],
 ['heavy','Тяжёлая рамка',3,90,26,82.5,14,2,1.6,.45],
 ['precision','Точная рамка',4,90,18,55,16,1,1,.25],
 ['battery','Батарейная рамка',6,140,28,77,22,3,2.4,.65],
 ['outpost','Дозорная рамка',2,35,12,72,8,1,1,.25],
 ['siege','Осадная рамка',6,130,32,110,12,1,1,.9]
];
for(const [id,name,level,gold,cost,hp,capacity,mounts,feed,build] of frames)
 f('frame.'+id,name,'Рамка',{level,gold,cost,hp,capacity,mounts,feed,build,source:'chassis.'+id});
f('hull.standard','Обычный корпус','Корпус');
f('hull.light','Облегчённый корпус','Корпус',{level:2,gold:40,cost:-4,hpMult:.8,capacityAdd:-2,build:-.2});
f('hull.reinforced','Усиленный корпус','Корпус',{level:4,gold:80,cost:8,load:1,hpMult:1.3,build:.3});
f('hull.ribbed','Ребристый корпус','Корпус',{level:3,gold:55,cost:4,load:2,hpMult:1.15,capacityAdd:-1,build:.25,notes:'Прочнее штатного и дешевле усиленного; тяжелее и уменьшает вместимость.'});
f('armor.none','Без брони','Броня');
f('armor.plate','Бронепластины','Броня',{cost:14,load:2,defense:.3,build:.2});
f('armor.front','Фронтальный щит','Броня',{level:5,gold:100,cost:12,load:2,frontDefense:.4,build:.2,notes:'90° спереди; общая защита 0. Не складывается с пластинами.'});
f('feed.standard','Обычная подача','Подача');
f('feed.boost','Улучшенная подача','Подача',{level:3,gold:60,cost:8,load:2,feedMult:1.25,build:.2});
f('turn.standard','Обычный поворот','Поворот');
f('turn.quick','Быстрый поворот','Поворот',{level:2,gold:40,cost:8,load:1,acquisitionMult:.32});
f('turn.deliberate','Выверенный привод','Поворот',{level:5,gold:85,cost:6,load:1,feedMult:1.15,acquisitionMult:1.6,notes:'Подача ×1,15, но захват цели ×1,6: удерживает огонь по тяжёлым целям, хуже переключается.'});
const mechanisms=[
 ['gun','Пулемёт',24,4,12,.42,8,0,.1,1.4,160],
 ['piercer','Пробивающая пушка',44,6,20,1,12.5,0,.38,1.9,135],
 ['mortar','Мортира',69,7,28,1.9,18,6,2.4,2.4,120],
 ['fan','Веерный ствол',39,5,14,.85,6,0,.1,1.6,180],
 ['ricochet','Рикошет',52,5,12,.9,10,0,.1,1.6,145],
 ['stapler','Степлер',12,2,5,.6,9,0,.1,1.2,170],
 ['blotter','Промокашка',8,2,0,1.4,9,0,2,1.2,180]
];
const cores=mechanisms.map(([id,name,cost,load,damage,interval,range,minRange,radius,build,firingArcDegrees],i)=>({id:'core.'+id,source:'weapon.'+id,name,level:[1,1,1,4,6,2,3][i],gold:[0,0,0,80,120,40,50][i],cost,load,damage,interval,range,minRange,firingArcDegrees,targets:[1,7,8,8,4,1,0][i],radius,build,attack:['Одиночный','Пробитие','Площадь','Веер','Рикошет','Замедление','Очистка'][i],clip:[6,1,1,1,1,4,1][i],reload:interval,slow:i===5?.6:1,slowSeconds:i===5?2:0,flight:i===2?.8:0,jump:i===4?3:0,notes:['Частые одиночные попадания','До 7 разных целей, броня действует','Оценка по 8 целям; фактическое число в радиусе зависит от сцены','До 8 целей в конусе 90°','4 разные цели; переход до 3 клеток','Движение ×0,6 на 2 с; сильнейшее замедление','Нулевой урон; очистка клякс радиусом 2'][i]}));
const wc={level:1,gold:0,cost:0,load:0,damageMult:1,intervalMult:1,rangeMult:1,clipMult:1,clipOverride:0,reloadMult:1,acquisitionMult:1,radiusMult:1,compatible:'all'};
const weaponParts=[];
function w(id,name,slot,values={}){weaponParts.push({...wc,id,name,slot,...values});}
for(const c of cores)w('head.'+c.id.slice(5),['core.gun','core.piercer','core.mortar','core.fan'].includes(c.id)?'Штатный ствол: '+c.name:'Штатная головка: '+c.name,'Ствол / головка',{compatible:c.id});
w('head.short','Короткий ствол','Ствол / головка',{level:1,gold:18,cost:-4,load:-1,damageMult:.92,intervalMult:.76,reloadMult:.8,rangeMult:.8,compatible:'core.gun,core.piercer'});
w('head.long','Длинный ствол','Ствол / головка',{level:4,gold:80,cost:12,load:2,damageMult:1.1,intervalMult:1.2,reloadMult:1.2,rangeMult:1.25,compatible:'core.gun,core.piercer'});
w('head.wide','Широкий ствол','Ствол / головка',{level:3,gold:65,cost:14,load:1,damageMult:1.15,intervalMult:1.25,reloadMult:1.25,rangeMult:.9,radiusMult:1.45,compatible:'core.mortar'});
w('head.fan.focus','Собранный веер','Ствол / головка',{level:4,gold:75,cost:10,load:1,damageMult:1.1,rangeMult:1.2,intervalMult:1.2,reloadMult:1.2,compatible:'core.fan',notes:'Дальше и сильнее одно попадание; ниже темп, выше цена и нагрузка.'});
w('head.ricochet.long','Дальняя рикошетная головка','Ствол / головка',{level:6,gold:110,cost:12,load:2,damageMult:.9,rangeMult:1.25,intervalMult:1.1,reloadMult:1.1,compatible:'core.ricochet',notes:'Дальше первая цель; слабее и реже попадания. Дистанция перехода остаётся 3.'});
w('head.absorb','Впитывающая головка','Ствол / головка',{level:4,gold:70,cost:6,load:1,radiusMult:1.5,rangeMult:.85,intervalMult:1.3,reloadMult:1.3,compatible:'core.blotter',notes:'Радиус очистки ×1,5; короче дальность, дольше цикл. Урона врагам не наносит.'});
w('sight.iron','Мушка / указатель','Прицел');
w('sight.scope','Оптика','Прицел',{level:3,gold:60,cost:10,load:1,rangeMult:1.15,acquisitionMult:1.3,compatible:'core.gun,core.piercer,core.mortar,core.ricochet'});
w('sight.tracking','Следящий прицел','Прицел',{level:5,gold:90,cost:8,load:1,rangeMult:.95,acquisitionMult:.4});
w('sight.snap','Быстрый указатель','Прицел',{level:2,gold:45,cost:6,load:1,rangeMult:.9,acquisitionMult:.25,notes:'Очень быстрый захват ценой 10% дальности.'});
w('sight.survey','Обзорный прицел','Прицел',{level:4,gold:80,cost:14,load:2,rangeMult:1.2,acquisitionMult:1.5,notes:'Дальность ×1,2; более долгий захват, выше цена и нагрузка.'});
w('magazine.standard','Штатная обойма / кассета','Магазин');
w('magazine.drum','Барабан','Магазин',{level:2,gold:55,cost:9,load:1,intervalMult:.72,clipMult:2,reloadMult:1.9,compatible:'core.gun,core.stapler'});
w('magazine.belt','Лента','Магазин',{level:7,gold:115,cost:14,load:2,intervalMult:.8,reloadMult:.45,compatible:'core.gun'});
w('magazine.mortar.pair','Спаренная кассета мортиры','Магазин',{level:8,gold:140,cost:18,load:1,clipOverride:2,intervalMult:.42,reloadMult:1.25,compatible:'core.mortar',notes:'Две мины с коротким интервалом, затем смена обеих гильз. Для штатной бронированной мортиры с широким стволом нужна вместимость 11.'});
w('magazine.quick','Быстросменная кассета','Магазин',{level:3,gold:60,cost:6,load:1,clipOverride:3,intervalMult:1.15,reloadMult:.65,compatible:'core.gun,core.stapler',notes:'3 выстрела, быстрая смена; ниже темп внутри очереди. Не сочетается со спуском очереди.'});
w('magazine.reserve','Запасная кассета','Магазин',{level:5,gold:90,cost:8,load:2,clipMult:2,intervalMult:1.1,reloadMult:2.2,compatible:'core.gun,core.stapler',notes:'Вдвое больше выстрелов; медленнее огонь и смена. Дешевле барабана, но тяжелее.'});
w('trigger.standard','Обычный спуск','Спуск');
w('trigger.burst','Спуск: очередь 3','Спуск',{level:5,gold:100,cost:10,load:1,intervalMult:.45,clipOverride:3,reloadMult:2.1,compatible:'core.gun',notes:'Ровно 3 выстрела за цикл. Только штатная обойма.'});
w('trigger.economy','Бережный спуск','Спуск',{level:3,gold:50,cost:-4,load:-1,damageMult:.85,intervalMult:1.1,reloadMult:1.1,notes:'Дешевле и легче; урон ×0,85, оба времени цикла ×1,1.'});
w('mount.standard','Штатное крепление','Крепление');
w('mount.light','Лёгкое крепление','Крепление',{level:2,gold:35,cost:-3,load:-1,damageMult:.95,acquisitionMult:1.2,notes:'Меньше цена и нагрузка; урон ×0,95, наведение медленнее.'});
w('mount.stabilized','Стабилизированное крепление','Крепление',{level:4,gold:75,cost:8,load:1,rangeMult:1.08,acquisitionMult:.7,intervalMult:1.08,notes:'Дальше и быстрее наводится; тяжелее, дороже и медленнее стреляет.'});
const sc={level:1,gold:0,cost:0,load:0,capacity:0,hp:0,hpMult:1,range:0,rangeMult:1,rate:0,rateMult:1,slow:1,defense:0,build:0,compatible:'all'};
const buildingParts=[];
function s(id,name,slot,values={}){buildingParts.push({...sc,id,name,slot,...values});}
s('site.wall','Опора баррикады','Основание',{compatible:'wall',cost:8,hp:200,capacity:6,build:.15});
s('site.slow','Поддон замедлителя','Основание',{compatible:'slow',cost:10,hp:55,capacity:6,build:.3});
s('site.repair','Фундамент мастерской','Основание',{compatible:'repair',cost:20,hp:70,capacity:6,build:.7});
s('site.generator','Фундамент насоса','Основание',{compatible:'generator',cost:20,hp:80,capacity:6,build:.7});
s('function.wall','Секция заграждения','Функция',{compatible:'wall',cost:8,load:2,build:.2,notes:'Блокирует путь; враги могут разрушить.'});
s('function.slow','Чернильный состав','Функция',{compatible:'slow',cost:12,load:2,range:1,slow:.6,build:.5,notes:'Движение ×0,6 в зоне и 0,6 с после выхода. Не складывается.'});
s('function.repair','Ремонтный набор','Функция',{compatible:'repair',cost:45,load:3,range:5,rate:6,build:1.3,notes:'6 HP/с одной ближайшей повреждённой постройке. Без себя и других ремонтных постов.'});
s('function.generator','Насос штрихов','Функция',{compatible:'generator',cost:40,load:3,rate:18,build:1.5,notes:'18 штрихов за пережитую волну; не золото. Штатная сборка 60, окупаемость 4 волны. Максимум 2 насоса на поле.'});
s('shell.standard','Обычная оболочка','Оболочка');
s('shell.light','Лёгкая оболочка','Оболочка',{level:2,gold:35,cost:-4,hpMult:.75,build:-.1});
s('shell.reinforced','Усиленная оболочка','Оболочка',{level:4,gold:70,cost:10,load:1,hpMult:1.5,build:.3});
s('utility.none','Без оснащения','Оснащение');
s('utility.armor','Защитные панели','Оснащение',{level:3,gold:60,cost:12,load:2,defense:.2,compatible:'wall,repair,generator'});
s('utility.range','Расширитель зоны','Оснащение',{level:5,gold:90,cost:15,load:2,rangeMult:1.3,rateMult:.8,compatible:'repair,spikes,beacon,prism',notes:'Радиус ×1,3, сила действия ×0,8.'});
s('utility.economy','Экономичный насос','Оснащение',{level:8,gold:180,cost:12,load:2,rateMult:1.5,hpMult:.85,compatible:'generator',notes:'Доход ×1,5: 27 у штатного, 21 у быстрого; прочность ×0,85.'});
s('site.wall.compact','Компактная опора баррикады','Основание',{level:2,gold:35,compatible:'wall',cost:6,hp:150,capacity:5,build:.1,notes:'Дешевле и быстрее; меньше прочность и предел нагрузки.'});
s('site.slow.compact','Компактный поддон замедлителя','Основание',{level:2,gold:35,compatible:'slow',cost:8,hp:41.25,capacity:5,build:.2,notes:'Дешевле и быстрее; меньше прочность и предел нагрузки.'});
s('site.repair.compact','Компактный фундамент мастерской','Основание',{level:2,gold:35,compatible:'repair',cost:15,hp:52.5,capacity:5,build:.45,notes:'Дешевле и быстрее; меньше прочность и предел нагрузки.'});
s('site.generator.compact','Компактный фундамент насоса','Основание',{level:2,gold:35,compatible:'generator',cost:15,hp:60,capacity:5,build:.45,notes:'Дешевле и быстрее; меньше прочность и предел нагрузки.'});
s('function.wall.braced','Укреплённая секция заграждения','Функция',{level:3,gold:55,compatible:'wall',cost:14,load:3,hpMult:1.25,build:.4,notes:'Прочность ×1,25; дороже, тяжелее и дольше строится.'});
s('function.slow.diluted','Разбавленный чернильный состав','Функция',{level:3,gold:55,compatible:'slow',cost:12,load:2,range:1.3,slow:.75,build:.5,notes:'Радиус 1,3 вместо 1; движение ×0,75 вместо ×0,6.'});
s('function.repair.focused','Точечный ремонтный набор','Функция',{level:3,gold:60,compatible:'repair',cost:55,load:4,range:3.5,rate:9,build:1.5,notes:'9 HP/с вместо 6; радиус 3,5 вместо 5, выше цена и нагрузка.'});
s('function.generator.quick','Быстрый насос штрихов','Функция',{level:3,gold:60,compatible:'generator',cost:30,load:3,rate:14,build:.9,notes:'Дешевле и быстрее строится; 14 штрихов за волну вместо 18. Штатная сборка 50, окупаемость 4 волны; с экономичным оснащением — 21.'});
s('utility.concentrate','Концентратор чернил','Оснащение',{level:3,gold:50,compatible:'slow',cost:8,load:1,rangeMult:.8,slow:.5,notes:'Движение ×0,5; радиус ×0,8, выше цена и нагрузка.'});
s('site.spikes','Настил ловушки','Основание',{compatible:'spikes',cost:10,hp:45,capacity:6,build:.3});
s('function.spikes','Пружинные шипы','Функция',{compatible:'spikes',cost:22,load:2,range:1.15,rate:11,build:.6,notes:'Раз в секунду бьют всех врагов в зоне.'});
s('site.beacon','Тренога маяка','Основание',{compatible:'beacon',cost:16,hp:65,capacity:6,build:.5});
s('function.beacon','Линза наводки','Функция',{compatible:'beacon',cost:32,load:3,range:3,rate:.10,build:1,notes:'Ослабляет броню врагов в зоне. При перекрытии действует сильнейший маяк.'});
s('site.spikes.compact','Складной настил','Основание',{compatible:'spikes',level:2,gold:35,cost:7,hp:32,capacity:5,build:.15});
s('function.spikes.barbed','Зазубренные шипы','Функция',{compatible:'spikes',level:3,gold:55,cost:27,load:3,range:.9,rate:16,build:.7,notes:'Бьют сильнее в меньшем радиусе.'});
s('site.beacon.compact','Походная тренога','Основание',{compatible:'beacon',level:2,gold:35,cost:12,hp:48,capacity:5,build:.3});
s('function.beacon.focused','Фокусирующая линза','Функция',{compatible:'beacon',level:3,gold:60,cost:39,load:4,range:2.3,rate:.20,build:1.2,notes:'Сильнее ослабляет броню в меньшем радиусе.'});
s('site.prism','Колыбель призмы','Основание',{compatible:'prism',cost:20,hp:72,capacity:6,build:.5});
s('function.prism','Трёхгранная линза','Функция',{compatible:'prism',cost:44,load:3,range:3,rate:2,slow:.6,build:1.2,notes:'Каждые 2 с меняет цвет импульса: ожог, замедление, ослабление брони.'});
s('site.prism.compact','Складная колыбель','Основание',{compatible:'prism',level:3,gold:40,cost:15,hp:54,capacity:5,build:.3});
s('function.prism.focused','Собирающая огранка','Функция',{compatible:'prism',level:4,gold:65,cost:52,load:4,range:2.25,rate:3.5,slow:.5,build:1.4,notes:'Узкое поле даёт более сильный ожог и замедление.'});
const allParts=[...foundations,...cores.map(c=>({...c,slot:'Механизм',compatible:'weapon'})),...weaponParts,...buildingParts];
const rules={slots:8,maxAlbumPages:64,initialGold:0,startingInk:640,waves:8,waveGold:8,victoryGold:30,xpPerWave:10,xpVictory:40,maxFeed:1.25,acquisition:.25,maxDefense:.6,maxGenerators:2,generatorRefund:0,generatorMaterial:'pencil',towerRefund:.8,slowFloor:.4,geometryPower:false};
const levels=[0,100,230,390,540,720,890,1050].map((xp,i)=>({level:i+1,xp,parts:allParts.filter(p=>p.level===i+1&&p.gold>0).map(p=>p.id)}));
const defaultW=(name,core,head='head.'+core.slice(5),sight='sight.iron',magazine='magazine.standard',trigger='trigger.standard',mount='mount.standard')=>({id:'weaponRecipe.'+name,name,core,head,sight,magazine,trigger,mount});
const weapons=[...cores.map(c=>defaultW(c.name,c.id)),defaultW('Короткий дозор','core.gun','head.short'),defaultW('Дальний пробой','core.piercer','head.long','sight.scope'),defaultW('Очередь дозора','core.gun','head.gun','sight.iron','magazine.standard','trigger.burst'),defaultW('Барабанный дозор','core.gun','head.gun','sight.iron','magazine.drum')];
weapons.push(defaultW('Собранный веер','core.fan','head.fan.focus'),
 defaultW('Дальний рикошет','core.ricochet','head.ricochet.long'),
 defaultW('Впитывание','core.blotter','head.absorb'),
 defaultW('Быстрый захват','core.gun','head.gun','sight.snap'),
 defaultW('Обзорный пробой','core.piercer','head.piercer','sight.survey'),
 defaultW('Быстрая кассета','core.gun','head.gun','sight.iron','magazine.quick'),
 defaultW('Запасная кассета','core.gun','head.gun','sight.iron','magazine.reserve'),
 defaultW('Бережный дозор','core.gun','head.gun','sight.iron','magazine.standard','trigger.economy'),
 defaultW('Спаренная мортира','core.mortar','head.mortar','sight.iron','magazine.mortar.pair'),
 defaultW('Ленточный дозор','core.gun','head.gun','sight.iron','magazine.belt'));
const defaultT=(id,name,weapon,count=1,values={})=>({id,name,frame:'frame.standard',hull:'hull.standard',armor:'armor.none',feed:'feed.standard',turn:'turn.standard',weapon,count,starter:false,role:'',...values});
const towers=[defaultT('album.guard','Дозор',0,1,{starter:true,role:'Дешёвая точечная оборона'}),defaultT('album.double','Двойной дозор',0,2,{starter:true,role:'Плотный огонь, общая подача 0,8 на ствол'}),defaultT('album.piercer','Пробой',1,1,{starter:true,role:'Прямой участок и несколько целей'}),defaultT('album.mortar','Защищённая мортира',2,1,{starter:true,armor:'armor.plate',role:'Группа врагов, мёртвая зона 6 клеток'}),defaultT('album.short','Ближний дозор',7,1,{role:'Ближе и дешевле, выше темп'}),defaultT('album.sniper','Дальний пробой',8,1,{frame:'frame.precision',role:'Дальность за меньший темп'}),defaultT('album.burst','Очередь дозора',9,1,{role:'Три быстрых попадания и пауза'}),defaultT('album.drum','Барабанный дозор',10,1,{role:'Длинная очередь, долгая смена магазина'}),defaultT('album.battery','Батарея',0,3,{frame:'frame.battery',role:'Три ствола с общей подачей'}),defaultT('album.stapler','Степлер',5,1,{role:'Контроль быстрых целей'})];
towers.push(defaultT('example.outpost','Дозорная точка',0,1,{frame:'frame.outpost',role:'Дешевле и прочнее, один ствол и меньше вместимость'}),
 defaultT('example.siege','Осадная мортира',2,1,{frame:'frame.siege',armor:'armor.plate',role:'Прочнее, дороже и дольше строится; один ствол'}),
 defaultT('example.ribbed','Ребристый дозор',0,1,{frame:'frame.precision',hull:'hull.ribbed',role:'Прочность за нагрузку и меньшую вместимость; поднятое крепление обходит щёки корпуса'}),
 defaultT('example.deliberate','Выверенный двойной дозор',0,2,{turn:'turn.deliberate',role:'Выше подача, медленнее захват'}));
for(let i=11;i<weapons.length;i++)towers.push(defaultT('example.weapon.'+i,weapons[i].name,i,1,{role:'Проверяемый числовой компромисс альтернативной детали'}));
const buildings=['wall','slow','repair','generator'].map((family,i)=>({id:'album.'+family,name:['Баррикада','Замедлитель','Ремонтный пост','Генератор штрихов'][i],family,site:'site.'+family,function:'function.'+family,shell:'shell.standard',utility:'utility.none',starter:true,cap:family==='generator'?2:0}));
for(const [family,name] of [['spikes','Шиповая ловушка'],['beacon','Маяк наводки'],['prism','Чернильная призма']])
 buildings.push({id:'album.'+family,name,family,site:'site.'+family,function:'function.'+family,shell:'shell.standard',utility:'utility.none',starter:false,workshop:true,cap:0});
buildings.push({...buildings[0],id:'album.wall-heavy',name:'Бронированная баррикада',shell:'shell.reinforced',utility:'utility.armor',starter:false}, {...buildings[2],id:'album.repair-wide',name:'Дальний ремонт',utility:'utility.range',starter:false}, {...buildings[3],id:'album.generator-economy',name:'Экономичный насос',utility:'utility.economy',starter:false});
const get=id=>{const p=allParts.find(p=>p.id===id);if(!p)throw new Error('Unknown '+id);return p;};
function resolveWeapon(r){const c=get(r.core),ps=[r.head,r.sight,r.magazine,r.trigger,...r.mount?[r.mount]:[]].map(get);for(const p of ps)if(p.compatible!=='all'&&!p.compatible.split(',').includes(c.id))throw new Error('Compatibility '+p.id);if(r.trigger==='trigger.burst'&&r.magazine!=='magazine.standard')throw new Error('Burst magazine');const mul=k=>ps.reduce((a,p)=>a*p[k],1);const clip=ps.find(p=>p.clipOverride>0)?.clipOverride??c.clip*mul('clipMult');const damage=c.damage*mul('damageMult'),interval=c.interval*mul('intervalMult'),reload=c.reload*mul('reloadMult'),cycle=(clip-1)*interval+reload;return {damage,interval,reload,clip,cycle,dps:damage*clip/cycle,range:c.range*mul('rangeMult'),minRange:c.minRange,firingArcDegrees:c.firingArcDegrees,cost:c.cost+ps.reduce((a,p)=>a+p.cost,0),load:c.load+ps.reduce((a,p)=>a+p.load,0),level:Math.max(c.level,...ps.map(p=>p.level)),acquisition:rules.acquisition*mul('acquisitionMult'),radius:c.radius*mul('radiusMult'),targets:c.targets};}
weapons.forEach(r=>r.resolved=resolveWeapon(r));
towers.forEach(t=>{const fr=get(t.frame),h=get(t.hull),a=get(t.armor),fe=get(t.feed),tu=get(t.turn),we=weapons[t.weapon].resolved;const parts=[fr,h,a,fe,tu],feedModifier=parts.reduce((x,p)=>x*p.feedMult,1);const feed=Math.min(rules.maxFeed,feedModifier,fr.feed*feedModifier/t.count);t.resolved={cost:Math.ceil(parts.reduce((x,p)=>x+p.cost,0)+we.cost*t.count),hp:fr.hp*h.hpMult,load:parts.reduce((x,p)=>x+p.load,0)+we.load*t.count,capacity:fr.capacity+h.capacityAdd,defense:a.defense,frontDefense:a.frontDefense,feed,dps:we.dps*t.count*feed,range:we.range,level:Math.max(...parts.map(p=>p.level),we.level),acquisition:we.acquisition*tu.acquisitionMult};if(t.resolved.load>t.resolved.capacity||t.count>fr.mounts)throw new Error('Invalid tower '+t.id);});
buildings.forEach(b=>{const p=[b.site,b.function,b.shell,b.utility].map(get);for(const x of p)if(x.compatible!=='all'&&!x.compatible.split(',').includes(b.family))throw new Error('Building compatibility');const multiply=key=>p.reduce((value,part)=>value*part[key],1);b.resolved={cost:Math.ceil(p.reduce((a,p)=>a+p.cost,0)),hp:p[0].hp*multiply('hpMult'),load:p.reduce((a,p)=>a+p.load,0),capacity:p[0].capacity,range:p[1].range*multiply('rangeMult'),rate:p[1].rate*multiply('rateMult'),slow:Math.max(rules.slowFloor,Math.min(...p.map(part=>part.slow))),defense:p[3].defense,build:p.reduce((a,p)=>a+p.build,0),level:Math.max(...p.map(p=>p.level))};if(b.resolved.load>b.resolved.capacity)throw new Error('Building load');});
const result={version,date:'2026-11-06',status:'Числовой источник текущего каталога; подтверждение импорта и боя — отдельные проверки',rules,foundations,cores,weaponParts,buildingParts,weapons,towers,buildings,levels,partCount:allParts.length};
function finite(value,at){
 if(typeof value==='number')assert(Number.isFinite(value),'Non-finite balance number at '+at);
 else if(value&&typeof value==='object')for(const [key,child] of Object.entries(value))finite(child,at+'.'+key);
}
finite(result,'balance');
return result;
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
const result=buildDesignData(),{towers,buildings}=result;
await fs.writeFile(path.join(root,'docs/balance/album-balance-v1.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({parts:result.partCount,towers:towers.map(t=>({name:t.name,...t.resolved})),buildings:buildings.map(b=>({name:b.name,...b.resolved})),totalUnlock:[...result.foundations,...result.cores,...result.weaponParts,...result.buildingParts].reduce((a,p)=>a+p.gold,0)},null,2));
}
