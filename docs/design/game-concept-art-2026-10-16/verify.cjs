const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),vm=require('node:vm'),assert=require('node:assert/strict');
const dir=__dirname,read=f=>fs.readFileSync(path.join(dir,f),'utf8').replace(/^\uFEFF/,'');
const manifest=JSON.parse(read('manifest.json')),catalog=vm.runInNewContext(read('catalog.js')+';SCREENS'),coverage=JSON.parse(read('coverage.json'));
assert.equal(catalog.length,33);assert.equal(manifest.screens.length,33);assert.equal(new Set(catalog.map(c=>c.id)).size,33);assert.equal(coverage.screens.length,28);
for(const c of catalog){const item=manifest.screens.find(i=>i.id===c.id);assert(item);assert.equal(item.file,c.src);const b=fs.readFileSync(path.resolve(dir,c.src));assert.equal(b.subarray(0,8).toString('hex'),'89504e470d0a1a0a');assert.equal(crypto.createHash('sha256').update(b).digest('hex'),item.sha256,c.id);assert(b.readUInt32BE(16)>=1600);assert(Math.abs(b.readUInt32BE(16)/b.readUInt32BE(20)-16/9)<.02);}
for(const s of coverage.screens){assert(s.changed);assert.notEqual(s.originalSha256,s.currentSha256);assert.equal(s.currentSha256,manifest.screens.find(x=>x.id===s.id).sha256);}
for(const f of ['prompts.json','correction-prompts.json','revision-progress.json'])JSON.parse(read(f));
console.log(JSON.stringify({screens:30,artSheets:3,originalsUpdated:28,allHashesMatch:true,allPngLinksExist:true}));
