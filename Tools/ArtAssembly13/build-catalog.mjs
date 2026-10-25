// Art integration only. Source PNGs are decoded to inspect alpha bounds and are never rewritten.
// Mechanical anchors below are manually authored on the full, untrimmed cell canvas.
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
const root = path.resolve(import.meta.dirname, '../..');
const art = 'ArtSource/Notebook/NewStyle/';
const read = p => JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const write = (p,v) => fs.writeFileSync(path.join(root,p),JSON.stringify(v,null,2)+'\n');
const source = read(art+'sprites.source.json');
const assembly = read(art+'assembly-parts.source.json');
const revision = 'modular-full-catalog-2026-10-24-v8';
const asset = fs.readFileSync(path.join(root,'Assets/PaperDefense/Configuration/Game/PlayerAssemblyCatalog.asset'),'utf8').split('    Starters:')[0];
const definitions = [...asset.matchAll(/^    - Id: (.+)\r?\n([\s\S]*?)(?=^    - Id:|$(?![\s\S]))/gm)].map(m=>({
  partId:m[1].trim(),revision:Number(m[2].match(/Revision: (\d+)/)[1]),
  category:Number(m[2].match(/Category: (\d+)/)[1]),weaponFamilies:Number(m[2].match(/WeaponFamilies: (\d+)/)[1]),
  structureFamilies:Number(m[2].match(/StructureFamilies: (\d+)/)[1]),assetRevision:revision,neutral:'',fragments:[]
}));
if(definitions.length!==54)throw new Error('Expected all 54 production definitions, got '+definitions.length);
const parts = new Map(definitions.map(p=>[p.partId,p]));
const rect = (x,y,width,height)=>({x,y,width,height});
const point = (x,y)=>({x,y});
const rotate = (p,r)=>point(p.x*Math.cos(r)-p.y*Math.sin(r),p.x*Math.sin(r)+p.y*Math.cos(r));
const clone = x => structuredClone(x);
// Native v2 crops and actual mechanical landmarks were measured on the new
// ink-accessory drawing (art3-accessory-candidate-review.json). Keep the old
// physical widths, except armour whose height is the limiting safe extent.
// Reapply original constants so rebuilding is idempotent, never cumulative.
const accessorySpecs=[
 ['sight',33,530,508,419,.065*347/508,[257.5,15.5],[-14.9675,117.6025]],
 ['magazine',662,514,375,461,.11*273/375,[38.5,169.5],[-32,95]],
 ['trigger',1150,547,345,423,.065*343/345,[149.5,123.5],[20,84]],
 ['bearing',574,69,512,395,.3*505/512,[260.5,214.5],[0,35]],
 ['feed',51,75,496,372,.00034*473/496],
 ['armor',1153,74,324,416,.00065*413/416]
];
for(const [id,x,y,width,height,scale,attach,offset]of accessorySpecs){
 const spriteId='part.pilot.'+id,s=source.sprites.find(s=>s.id===spriteId);
 if(!s)throw new Error('Missing retained accessory ID '+spriteId);
 Object.assign(s,{source:art+'AssemblyAccessories-v2.png',x,y,width,height,bounds:rect(0,0,width/1000,height/1000)});
 const f=assembly.pilots[0].fragments.find(f=>f.spriteId===spriteId)??assembly.foundationParts.find(f=>f.spriteId===spriteId);
 Object.assign(f,{canvas:point(width,height),trim:rect(0,0,width,height),scale});
 if(attach){f.attach=point(...attach);f.offset=point(...offset);}
 else f.region=rect(0,0,width,height);
}
parts.get('armor.none').neutral='Без брони'; parts.get('utility.none').neutral='Без оснащения';
for(const f of assembly.foundationParts){
  parts.get(f.partId).fragments.push({spriteId:f.spriteId,socketId:f.partId+'.'+f.layer,motion:'fixed',collisionMode:f.collisionMode,
    layer:f.layer,family:0,canvas:f.canvas,attach:point(0,0),position:f.position,trim:f.trim,region:f.region,scale:f.scale,radians:0});
}
const pilot = assembly.pilots[0];
const foundation = assembly.foundations.find(f=>f.id==='chassis.standard');
const gun = assembly.weapons.find(w=>w.id==='weapon.gun');
const units = assembly.frameBounds.width*foundation.sockets[0].diameter/gun.diameter;
for(const f of pilot.fragments){
  const fragment = {spriteId:f.spriteId,socketId:f.componentId+'.'+f.id,motion:['fixed','rotating','recoiling'][f.group],collisionMode:'mount-contact',
    layer:3+f.layer,family:0,canvas:f.canvas,attach:f.attach,position:point((f.offset.x-pilot.pivotOffset.x)*units,(f.offset.y-pilot.pivotOffset.y)*units),
    trim:f.trim,region:clone(f.trim),scale:f.scale*units,radians:f.radians,hasMuzzle:f.id===pilot.muzzleFragment};
  if(fragment.hasMuzzle)fragment.muzzle=pilot.muzzlePoint;
  parts.get(f.componentId).fragments.push(fragment);
}

function decodePng(relative){
 const b=fs.readFileSync(path.join(root,relative));let at=8,w,h,colorType,depth;const chunks=[];
 while(at<b.length){const n=b.readUInt32BE(at),type=b.toString('ascii',at+4,at+8),p=b.subarray(at+8,at+8+n);if(type==='IHDR'){w=p.readUInt32BE(0);h=p.readUInt32BE(4);depth=p[8];colorType=p[9];}if(type==='IDAT')chunks.push(p);at+=n+12;}
 if(colorType!==6||depth!==8)throw new Error('Expected unmodified RGBA8 PNG '+relative);
 const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=w*4,pixels=Buffer.alloc(h*stride);let pos=0;
 const paeth=(a,b,c)=>{const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;};
 for(let y=0;y<h;y++){const filter=raw[pos++];for(let x=0;x<stride;x++){const a=x>=4?pixels[y*stride+x-4]:0,up=y?pixels[(y-1)*stride+x]:0,c=y&&x>=4?pixels[(y-1)*stride+x-4]:0;let v=raw[pos++];if(filter===1)v+=a;else if(filter===2)v+=up;else if(filter===3)v+=Math.floor((a+up)/2);else if(filter===4)v+=paeth(a,up,c);else if(filter!==0)throw new Error('Unknown PNG filter');pixels[y*stride+x]=v&255;}}
 return{w,h,pixels};
}
const sheets=new Map();
function cell(relative,index){
 if(!sheets.has(relative))sheets.set(relative,decodePng(relative));const im=sheets.get(relative),grid=/StandardFoundation-v10\.png$|TurnHub-v1\.png$/.test(relative)?1:/SupportAttachments-v1\.png$|RepairParts-v1\.png$|BatteryFoundation-v4\.png$/.test(relative)?2:4,col=index%grid,row=Math.floor(index/grid);
 let left=Math.floor(col*im.w/grid),right=Math.floor((col+1)*im.w/grid);
 let top=Math.floor(row*im.h/grid),bottom=Math.floor((row+1)*im.h/grid);
 // The long nib's tip extends above its nominal fourth-row cell. The transparent
 // gutter at y=904 separates it from the stapler; retain that full author canvas.
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===9)bottom=904;
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===13)top=904;
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===0)right=340;
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===1)left=340;
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===2)bottom=350;
 if(relative.endsWith('FullWeaponParts-v1.png')&&index===6)top=350;
 if(relative.endsWith('FullVariantParts-v1.png')){
   const columns=[[0,358,615,940,1254],[0,365,607,932,1254],[0,330,610,917,1254],[0,325,625,939,1254]][row];
   const rows=[0,350,635,960,1254];left=columns[col];right=columns[col+1];top=rows[row];bottom=rows[row+1];
 }
 if(relative.endsWith('FullStructureParts-v1.png')){
   const columns=[[0,330,645,955,1254],[0,331,651,958,1254],[0,335,675,1007,1254],[0,350,710,960,1254]][row];
   left=columns[col];right=columns[col+1];
   if([0,2,3].includes(index))bottom=360;
   if([4,6,7].includes(index))top=360;
   if(index===12)top=980;
   if(index===13||index===14)top=960;
 }
 if(relative.endsWith('FullSupportAttachments-v1.png')&&index===3)left=820;
 let minX=right,minY=bottom,maxX=left,maxY=top,count=0;
 for(let y=top;y<bottom;y++)for(let x=left;x<right;x++)if(im.pixels[(y*im.w+x)*4+3]>12){minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);count++;}
 if(count<100)throw new Error('Empty source cell '+relative+' '+index);
 minX=Math.max(left,minX-2);maxX=Math.min(right-1,maxX+2);minY=Math.max(top,minY-2);maxY=Math.min(bottom-1,maxY+2);
 return{source:relative,x:minX,y:im.h-1-maxY,width:maxX-minX+1,height:maxY-minY+1,
   canvas:point(right-left,bottom-top),trim:rect(minX-left,bottom-1-maxY,maxX-minX+1,maxY-minY+1),count};
}
source.sprites=source.sprites.filter(s=>!s.id.startsWith('part.full.'));
function add(partId,relative,index,options={}){
 const c=cell(relative,index),id='part.full.'+(options.spriteSuffix??partId),canvas=c.canvas;
 const a=point(canvas.x*(options.attach?.[0]??.5),canvas.y*(options.attach?.[1]??.12));
 const region=clone(c.trim);let scale=options.scale;
 if(!scale){let r=0;for(const p of [point(region.x,region.y),point(region.x+region.width,region.y),point(region.x+region.width,region.y+region.height),point(region.x,region.y+region.height)])r=Math.max(r,Math.hypot(p.x-a.x,p.y-a.y));scale=(options.radius??.145)/r;}
 const f={spriteId:id,socketId:partId+'.'+(options.socket??'root'),motion:options.motion??'fixed',collisionMode:options.collisionMode??'mount-contact',layer:options.layer??6,family:options.family??0,
  canvas,attach:a,position:point(...(options.position??[0,0])),trim:c.trim,region,scale,radians:options.radians??0,hasMuzzle:!!options.muzzle};
 if(options.muzzle)f.muzzle=point(canvas.x*options.muzzle[0],canvas.y*options.muzzle[1]);
 const existing=source.sprites.find(s=>s.source===relative&&s.x===c.x&&s.y===c.y&&s.width===c.width&&s.height===c.height);
 if(existing)f.spriteId=existing.id;
 else source.sprites.push({id,source:relative,x:c.x,y:c.y,width:c.width,height:c.height,bounds:{x:region.x*scale,y:region.y*scale,width:region.width*scale,height:region.height*scale}});
 parts.get(partId).fragments.push(f);
 return f;
}
const weapons=art+'FullWeaponParts-v1.png';
const families=['piercer','mortar','fan','ricochet','stapler','blotter'];
// Anchors were inspected on the full source cells. They are not alpha-centre guesses.
const coreAnchors=[[.53,.10],[.50,.12],[.52,.15],[.50,.15],[.50,.10],[.50,.10]];
const headAnchors=[[.463,.10],[.554,.10],[.513,.12],[.557,.46],[.172,.44],[.557,.14]];
const muzzles=[[.463,.98],[.554,.72],[.513,.968],[.557,.83],[.90,.585],[.557,.85]];
for(let i=0;i<families.length;i++){
 const family=families[i],core=add('core.'+family,weapons,i*2,{attach:coreAnchors[i],radius:.105,motion:'rotating',layer:6,position:[0,-.012]});
 const head=add('head.'+family,weapons,i*2+1,{attach:headAnchors[i],radius:family==='ricochet'?.165:.185,motion:'recoiling',layer:5,position:[0,.025],muzzle:muzzles[i]});
 // Aim and recoil use the authored mechanical exit axis. Stapler art points right;
 // rotate the entire head, including its real exit point, to +Y uniformly.
 if(family==='stapler')head.radians=Math.PI/2-Math.atan2(head.muzzle.y-head.attach.y,head.muzzle.x-head.attach.x);
}
// The historical gun cradle is visibly a mortar tube. Modern guns use the
// clean gearbox shared with the piercer; the genuine four-barrel head carries
// the gun silhouette. The original pilot and source pixels remain intact.
const gunMechanism=clone(parts.get('core.piercer').fragments[0]);gunMechanism.socketId='core.gun.shared-gearbox';
parts.get('core.gun').fragments=[gunMechanism];
// These central lowest opaque pixels are the actual receiver feet. The larger
// receiver meets the front inner lip of the fixed bearing, rather than merely
// overlapping the distant rear edge of its open blue cavity.
const receiverFeet=[[181,9],[181,9],[157,40],[163,21],[157,27],[157,24],[157,33]];
for(const [i,family]of['gun',...families].entries()){
 const f=parts.get('core.'+family).fragments[0],foot=receiverFeet[i];f.scale*=1.4;
 f.position=point(-(foot[0]-f.attach.x)*f.scale,-.08525-(foot[1]-f.attach.y)*f.scale);
}
const gunHead=parts.get('head.gun').fragments[0];gunHead.scale*=1.4;gunHead.position.x*=1.4;gunHead.position.y=.015;
for(const [id,index,radius,a,m] of [['head.short',12,.13,[.537,.09],[.537,.904]],['head.long',13,.195,[.52,.083],[.52,.97]],['head.wide',14,.195,[.518,.11],[.518,.84]]])
 add(id,weapons,index,{attach:a,radius,motion:'recoiling',layer:5,position:[0,.02],muzzle:m});

const variants=art+'FullVariantParts-v1.png';
const frameSpecs=[['frame.heavy',0,1.24,[[.34,180/350],[.74,180/350]]],['frame.precision',1,.88,[[.50,.528]]]];
for(const [id,index,width,sockets] of frameSpecs){
 const c=cell(variants,index),scale=width/c.canvas.x;
 // Heavy's actual broad disk is centred at source x193, not the cell centre.
 // Both small hub feet sit on that shared plate with a .496U axis separation.
 const f=add(id,variants,index,{attach:[0,0],position:[id==='frame.heavy'?-193*scale:-width/2,-.38],scale,layer:1,collisionMode:'solid'});
 f.region=rect(f.trim.x,f.trim.y,f.trim.width,f.trim.height*.18);
 const frame={FrameId:id,Revision:1,Mounts:sockets.map(([x,y],slot)=>({SlotIndex:slot,SocketId:'mount.'+slot,SocketType:'gun.standard',X:f.position.x+c.canvas.x*x*scale,Y:f.position.y+c.canvas.y*y*scale})),Keepouts:[]};
 // Actual base and foreground wall are solid; the rear roof is the seating band.
 frame.Keepouts.push({X:f.position.x+f.region.x*scale,Y:f.position.y+f.region.y*scale,Width:f.region.width*scale,Height:f.region.height*scale,ArmorOnly:false});
 frame.Keepouts.push({X:f.position.x+f.trim.x*scale,Y:f.position.y+(f.trim.y+f.trim.height*.18)*scale,Width:f.trim.width*scale,Height:f.trim.height*.14*scale,ArmorOnly:false});
 assembly.fits.Frames=assembly.fits.Frames.filter(x=>x.FrameId!==id);assembly.fits.Frames.push(frame);
 parts.get(id).mounts=sockets.map(([x,y],slot)=>({slot,position:point(c.canvas.x*x,c.canvas.y*y)}));
}
// Modern recipes use a low plinth. The historical pilot canvases and its two
// original socket coordinates remain above in foundations/pilots for legacy.
const standard=art+'StandardFoundation-v10.png',base=cell(standard,0);
// Preserve the accepted logical seats while replacing the front-facing beam
// with the more faithful isometric plinth. Source disk centres were inspected
// on the full 1536x1024 canvas; no source pixels are rescaled or rewritten.
const oldLeft=-.5+483/1942,oldRight=-.5+1452/1942,oldSeatY=-.27+458/1942;
// The refreshed matte stone drawing retains both cream seating faces:
// measured centres BL(370.5,641) and (1165.5,641). The accepted controls
// below remain inside those faces within 2.7px annotation uncertainty.
// Refresh the complete lower crop: v10's front stone extends 30px farther.
const seatLeft=368,seatRight=1163,seatY=640;
const baseScale=(oldRight-oldLeft)/(seatRight-seatLeft);
const basePosition=[oldLeft-seatLeft*baseScale,oldSeatY-seatY*baseScale];
parts.get('frame.standard').fragments=[];parts.get('hull.standard').fragments=[];
const standardFrame=add('frame.standard',standard,0,{attach:[0,0],position:basePosition,scale:baseScale,layer:1,collisionMode:'solid'});
// The vertical corner faces begin near top-origin y515. Include them in a
// conservative foreground band beginning at y512 (native bottom-left 511),
// rather than dropping the outer walls to manufacture clearance. Native row
// 384 separates the lowest front course from the upper foreground. All three
// bands remain contiguous and retain the complete original crop.
const baseTop=384,frontTop=511;
const baseFraction=(baseTop-base.trim.y)/base.trim.height,frontFraction=(frontTop-base.trim.y)/base.trim.height;
const modernFrame=parts.get('frame.standard'); modernFrame.hullBaseFraction=baseFraction;modernFrame.hullFrontFraction=frontFraction;
standardFrame.region=rect(base.trim.x,base.trim.y,base.trim.width,baseTop-base.trim.y);
modernFrame.mounts=[{slot:0,position:point(seatLeft,seatY)},{slot:1,position:point(seatRight,seatY)}];
// A base's upper seating surface and its foreground are separate ordered
// fragments. Their actual source bands also supply the physical keepouts.
for(const split of [{bottom:frontFraction,height:1-frontFraction,layer:0},{bottom:baseFraction,height:frontFraction-baseFraction,layer:14}]){
 const f=clone(standardFrame);f.socketId='hull.standard.'+split.layer;f.layer=split.layer;
 f.collisionMode=split.layer===14?'solid':'mount-contact';
 f.region=rect(base.trim.x,base.trim.y+base.trim.height*split.bottom,base.trim.width,base.trim.height*split.height);
 parts.get('hull.standard').fragments.push(f);
}
// A genuine low three-pad foundation replaces the old tall battery block.
// All three disk centres were inspected on the unchanged top-left 627px cell.
// Refreshed navy hatching preserves all three faces within 1-2px of the
// accepted authored centres. Retain logical mounts; refresh the native crop.
const battery=art+'BatteryFoundation-v4.png',batteryCell=cell(battery,0),batteryScale=1.95/batteryCell.canvas.x;
const batteryFrame=add('frame.battery',battery,0,{attach:[0,0],position:[-.975,-.88],scale:batteryScale,layer:1,collisionMode:'solid'});
const batteryPart=parts.get('frame.battery');batteryPart.hullBaseFraction=.18;batteryPart.hullFrontFraction=.35;
batteryFrame.region=rect(batteryFrame.trim.x,batteryFrame.trim.y,batteryFrame.trim.width,batteryFrame.trim.height*.18);
batteryPart.mounts=[{slot:0,position:point(168,292)},{slot:1,position:point(312,344)},{slot:2,position:point(459,290)}];
for(const id of ['armor.plate','feed.standard'])for(const f of parts.get(id).fragments){
 f.scale*=.5;f.position=point(id==='armor.plate'?.0002625-f.canvas.x*f.scale/2:-.04,id==='armor.plate'?-.218:-.2075);
 if(id==='feed.standard')f.layer=16;
}
const modernPivot=.158;
// The original rectangular extraction retained a patch of the broad dish.
// A separate native cutout keeps the curved lower edge and front bolt only.
// Measured foot BL(625.5,405.5), front inner lip(625.5,643.5) have the same
// 0.07275U world separation as the accepted receiver contact. Legacy pilot
// retains its complete original dish and original part.gun.support binding.
parts.get('turn.standard').fragments=[];
const support=add('turn.standard',art+'TurnHub-v1.png',0,{attach:[625.5/1254,405.5/1254],position:[0,-modernPivot],scale:.07275/238,layer:3});
assembly.modernPivot=point(0,modernPivot);
add('hull.light',variants,3,{attach:[.5,.5],position:[0,-.18],radius:.24,layer:14,collisionMode:'solid'});
add('hull.reinforced',variants,4,{attach:[.5,.5],position:[0,-.18],radius:.24,layer:14,collisionMode:'solid'});
add('hull.light',variants,13,{attach:[.5,.5],position:[0,-.10],radius:.24,layer:0,collisionMode:'mount-contact',socket:'back',spriteSuffix:'hull.light.back'});
add('hull.reinforced',variants,14,{attach:[.5,.5],position:[0,-.10],radius:.24,layer:0,collisionMode:'mount-contact',socket:'back',spriteSuffix:'hull.reinforced.back'});
const shieldCell=cell(variants,5),shieldScale=.135/shieldCell.trim.height;
const frameFloor=standardFrame.position.y+base.trim.y*baseScale;
add('armor.front',variants,5,{attach:[.5,.5],position:[0,frameFloor-(shieldCell.trim.y-shieldCell.canvas.y*.5)*shieldScale],scale:shieldScale,layer:15,collisionMode:'armor'});
add('feed.boost',variants,6,{attach:[.5,.5],position:[0,-.10],radius:.10,layer:16,collisionMode:'solid'});
add('turn.quick',variants,7,{attach:[.5,.5],position:[0,-.025],radius:.08,layer:3});
const quickSupport=clone(support);quickSupport.socketId='turn.quick.support';parts.get('turn.quick').fragments.push(quickSupport);
// Preserve the previous opaque sight-foot contact in world space while
// attaching the refreshed drawing by its measured lower foot itself.
parts.get('sight.iron').fragments[0].position.y=.005-21.5*.065*units;
parts.get('magazine.standard').fragments[0].position.y=.020;
parts.get('trigger.standard').fragments[0].position.y=.020;
add('sight.scope',variants,8,{attach:[.5,.18],position:[-.035,.005],radius:.035*1.7,motion:'rotating',layer:7});
add('sight.tracking',variants,9,{attach:[.5,.18],position:[-.035,.005],radius:.035*1.7,motion:'rotating',layer:7});
add('magazine.drum',variants,10,{attach:[.65,.7],position:[-.045,.020],radius:.06,motion:'rotating',layer:8});
add('magazine.belt',variants,11,{attach:[.65,.7],position:[-.045,.020],radius:.06,motion:'rotating',layer:8});
add('trigger.burst',variants,12,{attach:[.4,.7],position:[.04,.020],radius:.035*1.7,motion:'rotating',layer:9});

const supports=art+'FullStructureParts-v1.png';
const repair=art+'RepairParts-v1.png';
for(const [row,family]of['wall','slow','repair','generator'].entries()){
 const flag=1<<row;
 add('site.'+family,family==='repair'?repair:supports,family==='repair'?0:row*4,{attach:[.5,.35],position:[0,-.20],radius:.40,layer:0,family:flag,collisionMode:'solid'});
 add('function.'+family,family==='repair'?repair:supports,family==='repair'?1:row*4+1,{attach:[.5,.12],position:[0,-.10],radius:.47,layer:5,family:flag});
 const shellPlacement=family==='repair'?[0,.24]:family==='generator'?[0,.16]:family==='slow'?[0,.04]:[0,-.13];
 add('shell.standard',family==='repair'?repair:supports,family==='repair'?2:row*4+2,{attach:[.5,family==='slow'?.5:.20],position:shellPlacement,radius:family==='generator'?.26:.38,layer:8,family:flag,socket:family,spriteSuffix:'shell.standard.'+family});
 const shell=parts.get('shell.standard').fragments.at(-1);
 const light=clone(shell);light.socketId='shell.light.'+family;
 parts.get('shell.light').fragments.push(light);
 const reinforced=clone(shell);reinforced.socketId='shell.reinforced.'+family;
 parts.get('shell.reinforced').fragments.push(reinforced);
 if(family!=='slow'){
   const plate=add('utility.armor',supports,3,{attach:[.5,.25],position:[0,-.20],radius:.24,layer:10,family:flag,socket:family,spriteSuffix:'utility.armor.'+family});
 }
 if(family==='repair'){
   const site=parts.get('site.repair').fragments[0],body=parts.get('function.repair').fragments[0];
   // Both source parts include their own stone foot. Seat the wooden frame on
   // the same actual footprint rather than displaying two separated plinths.
   body.scale=site.trim.width*site.scale/body.trim.width;
   const floor=site.position.y+(site.trim.y-site.attach.y)*site.scale;
   body.position.y=floor-(body.trim.y-body.attach.y)*body.scale;
   body.position.x=site.position.x+(site.trim.x+site.trim.width/2-site.attach.x)*site.scale-(body.trim.x+body.trim.width/2-body.attach.x)*body.scale;
   shell.scale=body.scale*.82;shell.radians=.09;
   const post=point(body.position.x+(112-body.attach.x)*body.scale,body.position.y+(340-body.attach.y)*body.scale);
   const eave=rotate(point((89-shell.attach.x)*shell.scale,(215-shell.attach.y)*shell.scale),shell.radians);
   shell.position=point(post.x-eave.x,post.y-eave.y);
   // Shared shells retain the same true roof seating for every material.
   for(const id of ['shell.light','shell.reinforced']){
    const copy=parts.get(id).fragments.find(f=>f.family===flag);copy.scale=shell.scale;copy.radians=shell.radians;copy.position=clone(shell.position);
   }
   add('function.repair',supports,row*4+3,{attach:[.5,.25],position:[.28,-.18],radius:.15,layer:7,family:flag,socket:'toolbox',spriteSuffix:'function.repair.toolbox'});
 }
 if(family==='slow'){
   // Native rim centres on the same source sheet: bowl (162,139), closed
   // grate (154,130). Match their plane and scale instead of swinging the lid
   // forward by moving an unrelated source canvas centre.
   const bowl=parts.get('function.slow').fragments[0];shell.scale=bowl.scale;
   const centre=point(bowl.position.x+(162-bowl.attach.x)*bowl.scale,bowl.position.y+(139-bowl.attach.y)*bowl.scale);
   shell.position=point(centre.x-(154-shell.attach.x)*shell.scale,centre.y-(130-shell.attach.y)*shell.scale);
   for(const id of ['shell.light','shell.reinforced']){const copy=parts.get(id).fragments.find(f=>f.family===flag);copy.scale=shell.scale;copy.position=clone(shell.position);}
 }
 if(family==='generator'){
   add('utility.economy',supports,row*4+3,{attach:[.5,.25],position:[-.25,-.05],radius:.25,layer:10,family:flag,socket:'flywheel'});
 }
}
const attachments=art+'FullSupportAttachments-v1.png';
for(const [row,family]of['wall','slow','repair','generator'].entries()){
 const flag=1<<row;
 add('shell.light',attachments,0,{attach:[.5,.5],position:[0,-.19],radius:.31,layer:9,family:flag,socket:family+'.light',spriteSuffix:'shell.light.panel.'+family});
 add('shell.reinforced',attachments,1,{attach:[.5,.5],position:[0,-.19],radius:.31,layer:9,family:flag,socket:family+'.reinforced',spriteSuffix:'shell.reinforced.panel.'+family});
}
add('utility.range',repair,3,{attach:[.356,.23],position:[.35,-.13],radius:.32,layer:10,family:4,socket:'outboard-head'});
add('utility.economy',attachments,3,{attach:[.33,.16],position:[.24,-.04],radius:.23,layer:7,family:8,socket:'thin-pump',spriteSuffix:'utility.economy.pump'});
assembly.revision=revision;source.revision=revision;
assembly.visualParts=definitions;
assembly.fits.Revision=revision;
const categoryNames=['Frame','Hull','Armor','Feed','Turn','Mechanism','Head','Sight','Magazine','Trigger','Site','Function','Shell','Utility'];
assembly.fits.Parts=definitions.map(p=>{const socket=p.category===5?'gun.standard':'assembly.'+categoryNames[p.category];return{Id:p.partId,Revision:p.revision,AssetRevision:revision,SocketSchemaVersion:1,Category:p.category,AttachType:socket,ExpectedSocketType:socket};});
for(const p of definitions)if(!p.neutral&&!p.fragments.length)throw new Error('Missing real artwork '+p.partId);
write(art+'sprites.source.json',source);write(art+'assembly-parts.source.json',assembly);
write(art+'full-catalog-layout.json',{revision,sourceSheets:[...sheets].map(([file,im])=>({file,width:im.w,height:im.h})),definitions:definitions.length,
 note:'The original RGBA PNGs are copied without pixel changes. Alpha inspection trims import rectangles only; full cell canvases and manually authored mechanical anchors remain stable.',
 sprites:source.sprites.filter(s=>s.id.startsWith('part.full.')).map(s=>({id:s.id,source:s.source,x:s.x,y:s.y,width:s.width,height:s.height}))});
console.log(JSON.stringify({revision,definitions:definitions.length,newSpriteBindings:source.sprites.filter(s=>s.id.startsWith('part.full.')).length},null,2));
