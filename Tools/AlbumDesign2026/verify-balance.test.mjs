import fs from 'node:fs/promises';
import test from 'node:test';
import assert from 'node:assert/strict';
import {buildDesignData} from './build-data.mjs';
import {runtimeDifferences,assertRuntimeCloneMutationDetected} from './verify-balance.mjs';

const design=buildDesignData();
const runtime=JSON.parse(await fs.readFile(new URL('../../docs/balance/assembly-runtime.json',import.meta.url),'utf8'));
test('live Player export matches independently generated design',()=>assert.deepEqual(runtimeDifferences(design,runtime),[]));
test('controlled mutation leaves source and live export untouched',()=>{
 const before=JSON.stringify({design,runtime});
 assertRuntimeCloneMutationDetected(design,runtime);
 assert.equal(JSON.stringify({design,runtime}),before);
});
for(const [name,mutate,expected] of [
 ['NaN',r=>{r.definitions[0].numeric.cost=NaN;},'definitions.frame.standard.numeric.cost'],
 ['missing cost',r=>{delete r.definitions[0].numeric.cost;},'definitions.frame.standard.numeric.cost'],
 ['revision',r=>{r.definitions[0].revision=2;},'definitions.frame.standard.revision'],
 ['fractional revision',r=>{r.definitions[0].revision=1.000001;},'definitions.frame.standard.revision'],
 ['duplicate identity',r=>r.definitions.push(structuredClone(r.definitions[0])),'definitions.id'],
 ['extra identity',r=>r.definitions.push({...structuredClone(r.definitions[0]),id:'frame.unexpected'}),'definitions.ids'],
 ['missing identity',r=>r.definitions.pop(),'definitions.ids'],
 ['starter order',r=>r.starters.reverse(),'starters'],
 ['Player rule',r=>{r.rules.startingInk++;},'rules.startingInk'],
 ['XP threshold',r=>{r.levels[1].xp++;},'levels.1.xp'],
 ['starter recipe',r=>{r.towers[0].parts[5].id='core.piercer';},'towers.album.guard.parts'],
 ['resolved damage',r=>{r.weapons[0].resolved.damage++;},'weapons.weaponRecipe.Пулемёт.resolved.damage']
])test('rejects '+name+' in a clone',()=>{
 const candidate=structuredClone(runtime);mutate(candidate);
 assert(runtimeDifferences(design,candidate).some(d=>d.path===expected));
});
