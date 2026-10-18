import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const d=JSON.parse(await fs.readFile('docs/balance/album-balance-v1.json','utf8'));
const all=[...d.foundations,...d.cores,...d.weaponParts,...d.buildingParts],ids=new Set(all.map(p=>p.id));
assert.equal(ids.size,54);assert.equal(all.length,54);
for(const p of all){assert.match(p.id,/^[a-z]+\.[a-z]+$/);assert(p.level>=1&&p.level<=8);assert(p.gold>=0);for(const v of Object.values(p))if(typeof v==='number')assert(Number.isFinite(v));}
const by=id=>all.find(p=>p.id===id),compat=(p,c)=>p.compatible==='all'||p.compatible.split(',').includes(c);
const groups=['Ствол / головка','Прицел','Магазин','Спуск'].map(slot=>d.weaponParts.filter(p=>p.slot===slot));
const variants=[],used=new Set();let rejected=0;
for(const c of d.cores)for(const h of groups[0])for(const s of groups[1])for(const m of groups[2])for(const t of groups[3]){
 const ps=[h,s,m,t];if(ps.some(p=>!compat(p,c.id))||(t.id==='trigger.burst'&&m.id!=='magazine.standard')){rejected++;continue;}
 const mul=k=>ps.reduce((n,p)=>n*p[k],1),clip=t.clipOverride||c.clip*mul('clipMult'),interval=c.interval*mul('intervalMult'),reload=c.reload*mul('reloadMult'),cycle=(clip-1)*interval+reload;
 const v={ids:[c,...ps].map(p=>p.id),clip,interval,reload,cycle,damage:c.damage*mul('damageMult'),cost:c.cost+ps.reduce((n,p)=>n+p.cost,0),load:c.load+ps.reduce((n,p)=>n+p.load,0),range:c.range*mul('rangeMult')};
 assert(cycle>0&&interval>0&&reload>0&&clip>=1&&Number.isInteger(clip)&&v.cost>0&&v.load>=0&&v.range>=c.minRange);v.dps=v.damage*clip/cycle;assert(Number.isFinite(v.dps));variants.push(v);
}
const slots=['Рамка','Корпус','Броня','Подача','Поворот'].map(slot=>d.foundations.filter(p=>p.slot===slot));let legalTowers=0,overloads=0;
for(const f of slots[0])for(const h of slots[1])for(const a of slots[2])for(const p of slots[3])for(const t of slots[4])for(const w of variants)for(let n=1;n<=f.mounts;n++){
 const ps=[f,h,a,p,t],load=ps.reduce((x,p)=>x+p.load,0)+w.load*n,capacity=f.capacity+h.capacityAdd;if(load>capacity){overloads++;continue;}
 const cost=ps.reduce((x,p)=>x+p.cost,0)+w.cost*n,feed=Math.min(d.rules.maxFeed,p.feedMult,f.feed*p.feedMult/n),dps=w.dps*n*feed;assert(cost>0&&f.hp*h.hpMult>0&&feed>0&&Number.isFinite(dps));legalTowers++;[...ps.map(p=>p.id),...w.ids].forEach(id=>used.add(id));
}
let legalBuildings=0;const gen=[];
for(const site of d.buildingParts.filter(p=>p.slot==='Основание'))for(const fun of d.buildingParts.filter(p=>p.slot==='Функция'))for(const shell of d.buildingParts.filter(p=>p.slot==='Оболочка'))for(const util of d.buildingParts.filter(p=>p.slot==='Оснащение')){
 const family=site.compatible,ps=[site,fun,shell,util];if(!ps.every(p=>compat(p,family)))continue;const cost=ps.reduce((n,p)=>n+p.cost,0),load=ps.reduce((n,p)=>n+p.load,0);if(load>site.capacity)continue;assert(cost>0&&site.hp*shell.hpMult*util.hpMult>0);ps.forEach(p=>used.add(p.id));legalBuildings++;if(family==='generator'){const rate=fun.rate*util.rateMult;gen.push({ids:ps.map(p=>p.id),cost,rate,breakEven:Math.ceil(cost/rate),eightWaveProfit:8*rate-cost});assert(cost/rate>4&&rate<=15);}
}
assert.equal(used.size,54,'Every part has at least one legal assembly');
const starters=[...d.towers,...d.buildings].filter(p=>p.starter);assert.equal(starters.length,8);assert.equal(starters.reduce((n,p)=>n+p.resolved.cost,0),426);
for(const p of starters){const partIds=p.frame?[p.frame,p.hull,p.armor,p.feed,p.turn,...['core','head','sight','magazine','trigger'].map(k=>d.weapons[p.weapon][k])]:[p.site,p.function,p.shell,p.utility];for(const id of partIds){assert.equal(by(id).gold,0);assert.equal(by(id).level,1);}}
for(let i=0;i<7;i++)assert(Math.abs(d.weapons[i].resolved.dps-d.cores[i].damage/d.cores[i].interval)<1e-9);
assert(Math.abs(d.weapons[9].resolved.dps-d.weapons[0].resolved.dps)<1e-9);assert(d.weapons[10].resolved.dps<d.weapons[0].resolved.dps);
assert.equal(d.rules.generatorRefund,0);assert.equal(d.rules.maxGenerators,2);
assert.equal(d.rules.generatorMaterial,'pencil');
const report={date:d.date,method:'Перебор совместимости и независимая арифметика; не игровая симуляция',parts:all.length,legalWeaponRecipes:variants.length,rejectedWeaponCombinations:rejected,legalHomogeneousTowers:legalTowers,rejectedOverloadedTowers:overloads,legalBuildings,reachableParts:used.size,starterCost:426,baselineWeaponsPreserved:7,generatorVariants:gen,errors:[]};
await fs.writeFile('docs/balance/analytical-validation.json',JSON.stringify(report,null,2));console.log(JSON.stringify({...report,generatorVariants:gen.length}));
