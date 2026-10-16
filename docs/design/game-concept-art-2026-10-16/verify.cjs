const fs = require('node:fs'), crypto = require('node:crypto'), vm=require('node:vm');
const manifest=JSON.parse(fs.readFileSync('manifest.json','utf8').replace(/^\uFEFF/,''));
const catalog=vm.runInNewContext(fs.readFileSync('catalog.js','utf8')+';SCREENS');
if(catalog.length!==28||manifest.screens.length!==28) throw Error('Incorrect screen count');
for(const x of manifest.screens) { const b=fs.readFileSync(x.file); if(b.subarray(0,8).toString('hex')!=='89504e470d0a1a0a')throw Error(x.id+' invalid PNG'); Object.assign(x,{width:b.readUInt32BE(16),height:b.readUInt32BE(20),bytes:b.length,sha256:crypto.createHash('sha256').update(b).digest('hex')});if(x.width<1600||Math.abs(x.width/x.height-16/9)>.01)throw Error(x.id+' size');}
for(const c of catalog)if(!fs.existsSync(c.src))throw Error('Missing '+c.src);
for(const f of ['prompts.json','correction-prompts.json']){const txt=fs.readFileSync(f,'utf8').replace(/^\uFEFF/,'');JSON.parse(txt);fs.writeFileSync(f,txt);}
fs.writeFileSync('manifest.json',JSON.stringify(manifest,null,2)+'\n');
console.log(JSON.stringify({screens:manifest.screens.length,catalog:catalog.length,dimensions:[...new Set(manifest.screens.map(x=>x.width+'x'+x.height))],totalMB:Math.round(manifest.screens.reduce((a,x)=>a+x.bytes,0)/1024/1024),allPngLinksExist:true}));

