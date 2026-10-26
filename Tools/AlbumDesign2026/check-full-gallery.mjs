import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {chromium} from 'playwright';
const dir=path.resolve(import.meta.dirname,'../../docs/design/game-concept-art-2026-10-16'),url=process.argv[2]||pathToFileURL(path.join(dir,'index.html')).href;
const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[],captures=[];
page.on('pageerror',e=>errors.push(e.message));
await page.goto(url);await page.locator('.card').first().waitFor();
const catalog=await page.evaluate(()=>SCREENS);
if(catalog.length!==33)errors.push('33 cards expected');
await page.evaluate(async()=>{for(const im of document.querySelectorAll('.art img')){im.loading='eager';await im.decode();}});
const filters=await page.locator('.filter').allTextContents();
for(const group of filters){await page.getByRole('button',{name:group,exact:true}).click();const count=await page.locator('.card').count(),expected=group==='Все'?33:catalog.filter(c=>c.group===group).length;if(count!==expected)errors.push('Filter '+group);}
await page.getByRole('button',{name:'Все',exact:true}).click();
for(const width of [1600,1024,390]){await page.setViewportSize({width,height:1000});if(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth))errors.push('Overflow '+width);await page.screenshot({path:path.join(dir,width===1600?'gallery-preview.png':`gallery-preview-${width}.png`)});captures.push('gallery-'+width);}
await page.setViewportSize({width:1600,height:1000});
for(const id of ['01-home','02-map','16-battle','18-pause','22-settings','29-structure']){const c=catalog.find(c=>c.id===id);await page.getByRole('button',{name:'Открыть: '+c.title,exact:true}).click();await page.locator('#vimage').evaluate(im=>im.decode());if(!await page.locator('#viewer').evaluate(d=>d.open))errors.push('Viewer '+id);await page.screenshot({path:path.join(dir,'preview-'+id+'.png')});captures.push(id);await page.keyboard.press('Escape');}
await page.goto(url+'#22-settings');await page.locator('#viewer[open]').waitFor();if(!await page.locator('#vtitle').innerText().then(t=>t.includes('Настройки')))errors.push('Deep link');await page.keyboard.press('ArrowRight');if(!await page.locator('#vtitle').innerText().then(t=>t.includes('Помощь')))errors.push('Next');await page.keyboard.press('ArrowLeft');if(!await page.locator('#vtitle').innerText().then(t=>t.includes('Настройки')))errors.push('Previous');await page.keyboard.press('Escape');if(await page.locator('#viewer').evaluate(d=>d.open))errors.push('Escape');
await fs.writeFile(path.join(dir,'browser-validation.json'),JSON.stringify({date:'2026-10-18',cards:33,decodedImages:33,filters,widths:[1600,1024,390],captures,actions:['filter groups','open every tested modal','deep link','next','previous','Escape'],errors},null,2)+'\n');await browser.close();console.log(JSON.stringify({cards:33,captures:captures.length,errors}));if(errors.length)process.exitCode=1;
