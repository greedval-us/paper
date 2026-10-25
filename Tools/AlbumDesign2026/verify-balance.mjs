import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {buildDesignData} from './build-data.mjs';
const root=path.resolve(import.meta.dirname,'../..');

export function runtimeDifferences(design,runtime){
 const differences=[];
 const difference=(path,expected,actual)=>differences.push({path,expected,actual});
 const equal=(at,expected,actual)=>{
  if(typeof expected==='number'){
   // Unity stores floats; retain enough tolerance for one serialization without hiding a balance edit.
   if(typeof actual!=='number'||!Number.isFinite(expected)||!Number.isFinite(actual)||Math.abs(actual-expected)>1e-5+Math.abs(expected)*1e-6)
    difference(at,expected,actual);
  }else if(actual!==expected)difference(at,expected,actual);
 };
 const finite=(value,at)=>{
  if(typeof value==='number'&&!Number.isFinite(value))difference(at,'finite number',value);
  else if(value&&typeof value==='object')for(const [key,child] of Object.entries(value))finite(child,at+'.'+key);
 };
 finite(design,'design');finite(runtime,'runtime');
 if(!runtime||typeof runtime!=='object'){
  difference('runtime','Player runtime export',runtime);return differences;
 }
 if(runtime.schemaVersion!==1)difference('schemaVersion',1,runtime.schemaVersion);
 equal('balanceRevision',design.version,runtime.balanceRevision);
 for(const [key,name] of Object.entries({balance:'PlayerBalance.asset',rules:'PlayerRules.asset',content:'PlayerContent.asset'}))
  equal('source.'+key,'Assets/PaperDefense/Configuration/Game/'+name,runtime.source?.[key]);
 const catalogPath=runtime.source?.assemblyCatalog;
 if(typeof catalogPath!=='string'||!catalogPath.startsWith('Assets/PaperDefense/')||!catalogPath.endsWith('.asset')||catalogPath.includes('..')||catalogPath.includes('\\'))
  difference('source.assemblyCatalog','resolved Assets/PaperDefense/*.asset reference',catalogPath);
 for(const [key,value] of Object.entries(design.rules))equal('rules.'+key,value,runtime.rules?.[key]);
 if(!Array.isArray(runtime.levels)||runtime.levels.length!==design.levels.length)difference('levels','eight runtime level thresholds',runtime.levels);
 for(const [i,level] of design.levels.entries())for(const key of ['level','xp'])equal('levels.'+i+'.'+key,level[key],runtime.levels?.[i]?.[key]);

 const index=(rows,at)=>{
  const map=new Map();
  if(!Array.isArray(rows)){difference(at,'array',rows);return map;}
  for(const row of rows){
   if(!row||typeof row.id!=='string'||map.has(row.id)){difference(at+'.id','unique string ID',row?.id);continue;}
   map.set(row.id,row);
  }
  return map;
 };
 const sameIds=(expected,actual,at)=>{
  const wanted=[...expected].sort(),found=[...actual.keys()].sort();
  if(JSON.stringify(wanted)!==JSON.stringify(found))difference(at+'.ids',wanted,found);
 };
 const parts=[...design.foundations,...design.cores,...design.weaponParts,...design.buildingParts];
 const definitions=index(runtime.definitions,'definitions');
 sameIds(parts.map(p=>p.id),definitions,'definitions');
 for(const part of parts){
  const actual=definitions.get(part.id),at='definitions.'+part.id;
  if(actual?.revision!==1)difference(at+'.revision',1,actual?.revision);
  equal(at+'.level',part.level,actual?.level);equal(at+'.gold',part.gold,actual?.gold);
  for(const [key,value] of Object.entries(part))if(typeof value==='number'&&key!=='level'&&key!=='gold')
   equal(at+'.numeric.'+key,value,actual?.numeric?.[key]);
 }
 for(const group of ['weapons','towers','buildings']){
  const actual=index(runtime[group],group);sameIds(design[group].map(p=>p.id),actual,group);
  for(const entry of design[group])for(const [key,value] of Object.entries(entry.resolved))if(typeof value==='number')
   equal(group+'.'+entry.id+'.resolved.'+key,value,actual.get(entry.id)?.resolved?.[key]);
  if(group!=='weapons')for(const entry of design[group].filter(e=>e.starter)){
   const weapon=entry.frame?design.weapons[entry.weapon]:null;
   const ids=entry.frame?[entry.frame,entry.hull,entry.armor,entry.feed,entry.turn,...Array.from({length:entry.count},()=>['core','head','sight','magazine','trigger'].map(k=>weapon[k])).flat()]:[entry.site,entry.function,entry.shell,entry.utility];
   const expected=ids.map(id=>({id,revision:1}));
   if(JSON.stringify(actual.get(entry.id)?.parts)!==JSON.stringify(expected))difference(group+'.'+entry.id+'.parts',expected,actual.get(entry.id)?.parts);
  }
 }
 const starterIds=[...design.towers,...design.buildings].filter(p=>p.starter).map(p=>p.id);
 if(JSON.stringify(runtime.starters)!==JSON.stringify(starterIds))difference('starters',starterIds,runtime.starters);
 return differences;
}

// The negative case changes an in-memory clone only. It must not repair either input.
export function assertRuntimeCloneMutationDetected(design,runtime){
 assert.deepEqual(runtimeDifferences(design,runtime),[],'Unmodified runtime must match design before the negative test');
 const before=JSON.stringify(runtime),candidate=structuredClone(runtime);
 const gun=candidate.definitions.find(p=>p.id==='core.gun');
 gun.numeric.damage+=1;
 const differences=runtimeDifferences(design,candidate);
 assert(differences.some(d=>d.path==='definitions.core.gun.numeric.damage'),'A valid numeric input mutation must be detected');
 assert.equal(JSON.stringify(runtime),before,'Negative test must leave the runtime export unchanged');
 return differences;
}

async function main(){
const d=JSON.parse(await fs.readFile(path.join(root,'docs/balance/album-balance-v1.json'),'utf8'));
assert.deepEqual(d,buildDesignData(),'Design JSON must be regenerated from build-data.mjs before verification');
const runtime=JSON.parse(await fs.readFile(path.join(root,'docs/balance/assembly-runtime.json'),'utf8'));
const numericDifferences=runtimeDifferences(d,runtime);
assert.deepEqual(numericDifferences,[],'Player runtime export must match every design numeric field, ID and revision');
const mutationDifferences=assertRuntimeCloneMutationDetected(d,runtime);
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
const report={date:d.date,method:'Перебор совместимости и независимая арифметика; не игровая симуляция',parts:all.length,legalWeaponRecipes:variants.length,rejectedWeaponCombinations:rejected,legalHomogeneousTowers:legalTowers,rejectedOverloadedTowers:overloads,legalBuildings,reachableParts:used.size,starterCost:426,baselineWeaponsPreserved:7,generatorVariants:gen,runtimeSource:runtime.source,balanceRevision:runtime.balanceRevision,numericDifferences,controlledMutation:{field:'definitions.core.gun.numeric.damage',detected:true,differences:mutationDifferences},errors:[]};
await fs.writeFile(path.join(root,'docs/balance/analytical-validation.json'),JSON.stringify(report,null,2));console.log(JSON.stringify({...report,generatorVariants:gen.length}));
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))await main();
