import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
const root=process.cwd(),errors=[];
async function walk(dir){const out=[];for(const e of await fs.readdir(dir,{withFileTypes:true})){if(['node_modules','.git','References'].includes(e.name))continue;const p=path.join(dir,e.name);if(e.isDirectory())out.push(...await walk(p));else if(e.name.endsWith('.md'))out.push(p);}return out;}
const docs=['README.md',...await walk('docs'),...await walk('ArtSource'),...await walk('Tools')];let links=0;
for(const file of docs){const s=(await fs.readFile(file,'utf8')).replace(/```[\s\S]*?```/g,'');for(const m of s.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){let url=m[1].replace(/^<|>$/g,'').split(' "')[0];if(/^(?:https?:|mailto:|#)/.test(url))continue;url=decodeURIComponent(url.split('#')[0]);if(!url)continue;links++;try{await fs.access(path.resolve(path.dirname(file),url));}catch{errors.push({file,link:url});}}}
const before=JSON.parse(await fs.readFile('docs/audit/2026-10-17-album/source-hashes.json','utf8'));let unchanged=0;
for(const f of before){try{const hash=crypto.createHash('sha256').update(await fs.readFile(f.Path)).digest('hex').toUpperCase();if(hash===f.Hash)unchanged++;else errors.push({sourceChanged:f.Path});}catch{errors.push({sourceMissing:f.Path});}}
const d=JSON.parse(await fs.readFile('docs/balance/album-balance-v1.json','utf8')),gallery=await fs.readFile('docs/design/album-assembly-2026-10-17/design-data.js','utf8');
if(gallery!=='window.ALBUM_DATA = '+JSON.stringify(d)+';\n')errors.push('Gallery balance differs');
const browser=JSON.parse(await fs.readFile('docs/design/album-assembly-2026-10-17/browser-validation.json','utf8'));if(browser.errors.length)errors.push(...browser.errors);
const book=JSON.parse(await fs.readFile('docs/balance/workbook-validation.json','utf8'));if(book.parts!==54||book.sheets.length!==11||!book.inputMutation)errors.push('Workbook coverage');
const references=JSON.parse(await fs.readFile('ArtSource/Notebook/References/manifest.json','utf8'));for(const f of references.files){if(crypto.createHash('sha256').update(await fs.readFile(f.preservedPath)).digest('hex')!==f.sha256)errors.push({referenceHash:f.preservedPath});}
const manifest=JSON.parse(await fs.readFile('docs/design/album-assembly-2026-10-17/manifest.json','utf8'));for(const f of manifest.files){if(crypto.createHash('sha256').update(await fs.readFile('docs/design/album-assembly-2026-10-17/'+f.file)).digest('hex')!==f.sha256)errors.push({galleryHash:f.file});}
const fullDir='docs/design/game-concept-art-2026-10-16',full=JSON.parse(await fs.readFile(fullDir+'/manifest.json','utf8')),fullBrowser=JSON.parse(await fs.readFile(fullDir+'/browser-validation.json','utf8')),coverage=JSON.parse(await fs.readFile(fullDir+'/coverage.json','utf8'));
if(full.screens.length!==33||coverage.updatedOriginalScreens!==28)errors.push('Full gallery coverage');
for(const f of full.screens){if(crypto.createHash('sha256').update(await fs.readFile(path.resolve(fullDir,f.file))).digest('hex')!==f.sha256)errors.push({fullGalleryHash:f.id});}
if(fullBrowser.errors.length)errors.push(...fullBrowser.errors);
const report={date:'2026-10-18',markdownFiles:docs.length,localLinks:links,unchangedSourceFiles:unchanged,baselineSourceFiles:before.length,balanceSharedWithGallery:true,workbookSheets:book.sheets.length,parts:d.partCount,browserCaptures:browser.captures.length+browser.states,preservedReferences:references.files.length,fullGalleryImages:full.screens.length,updatedOriginalScreens:coverage.updatedOriginalScreens,fullGalleryBrowserCaptures:fullBrowser.captures.length,errors};
await fs.writeFile('docs/audit/2026-10-17-album/delivery-validation.json',JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));if(errors.length)process.exitCode=1;
