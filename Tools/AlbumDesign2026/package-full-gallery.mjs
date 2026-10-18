import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import vm from 'node:vm';
import {execFileSync} from 'node:child_process';
const root=path.resolve(import.meta.dirname,'../..'),rel='docs/design/game-concept-art-2026-10-16',dir=path.join(root,rel);
const catalog=vm.runInNewContext(await fs.readFile(path.join(dir,'catalog.js'),'utf8')+';SCREENS');
const outputs=JSON.parse(await fs.readFile(path.join(dir,'revision-progress.json'),'utf8'));
let original;
try{const previous=JSON.parse(await fs.readFile(path.join(dir,'coverage.json'),'utf8'));original={screens:previous.screens.map(s=>({id:s.id,title:s.originalTitle,sha256:s.originalSha256}))};}
catch{original=JSON.parse(execFileSync('git',['show','HEAD:'+rel+'/manifest.json'],{cwd:root,encoding:'utf8'}).replace(/^\uFEFF/,''));}
const entries=[];
for(const c of catalog){const b=await fs.readFile(path.resolve(dir,c.src));entries.push({...c,file:c.src,generatedSource:outputs[c.id]?.generatedSource??'See sibling album-assembly-2026-10-17/prompts.json',width:b.readUInt32BE(16),height:b.readUInt32BE(20),bytes:b.length,sha256:crypto.createHash('sha256').update(b).digest('hex')});}
const coverage=original.screens.map(o=>{const n=entries.find(n=>n.id===o.id);if(!n)throw Error('Missing original '+o.id);if(n.sha256===o.sha256)throw Error('Unedited original '+o.id);return{id:o.id,originalTitle:o.title,currentTitle:n.title,file:n.file,originalSha256:o.sha256,currentSha256:n.sha256,changed:true};});
if(entries.length!==33||coverage.length!==28)throw Error('Coverage count');
await fs.writeFile(path.join(dir,'coverage.json'),JSON.stringify({date:'2026-10-18',originalScreens:28,updatedOriginalScreens:28,additionalScreens:2,objectSheets:3,total:33,originalRecoveryArchive:'../../audit/2026-10-17-album/original-sketches-recovery.zip',screens:coverage},null,2)+'\n');
await fs.writeFile(path.join(dir,'manifest.json'),JSON.stringify({created:'2026-10-16',updated:'2026-10-18',kind:'full-player-screen-concepts-modular-album',generator:'image_gen',count:entries.length,screens:entries},null,2)+'\n');
console.log(JSON.stringify({originalsUpdated:coverage.length,additionalScreens:2,objectSheets:3,total:entries.length}));
