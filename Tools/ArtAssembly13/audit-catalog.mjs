import fs from 'node:fs';
const data=JSON.parse(fs.readFileSync('ArtSource/Notebook/NewStyle/assembly-parts.source.json'));
const source=JSON.parse(fs.readFileSync('ArtSource/Notebook/NewStyle/sprites.source.json'));
const sprites=new Map(source.sprites.map(s=>[s.id,s]));const problems=[];
const rotate=(x,y,r)=>[x*Math.cos(r)-y*Math.sin(r),x*Math.sin(r)+y*Math.cos(r)];
const corners=f=>{const r=f.region??f.trim;return[[r.x,r.y],[r.x+r.width,r.y],[r.x+r.width,r.y+r.height],[r.x,r.y+r.height]].map(([x,y])=>{const [a,b]=rotate((x-f.attach.x)*f.scale,(y-f.attach.y)*f.scale,f.radians);return[a+f.position.x,b+f.position.y];});};
for(const p of data.visualParts)for(const f of p.fragments){
 const s=sprites.get(f.spriteId);if(!s)problems.push(p.partId+' missing '+f.spriteId);
 else if(s.width!==f.trim.width||s.height!==f.trim.height)problems.push(p.partId+' source trim does not match pixels');
 if(f.hasMuzzle){if(f.muzzle.x<f.trim.x||f.muzzle.y<f.trim.y||f.muzzle.x>f.trim.x+f.trim.width||f.muzzle.y>f.trim.y+f.trim.height)problems.push(p.partId+' muzzle outside source trim');const [x,y]=rotate((f.muzzle.x-f.attach.x)*f.scale,(f.muzzle.y-f.attach.y)*f.scale,f.radians);if(Math.abs(x+f.position.x)>.00001||y+f.position.y<=0)problems.push(p.partId+' muzzle axis differs from +Y');}
}
const envelopes=[];
for(const core of data.visualParts.filter(p=>p.category===5)){
 let radius=0;
 for(const part of data.visualParts.filter(p=>p.category>=5&&p.category<=9&&(p.weaponFamilies&core.weaponFamilies)))for(const f of part.fragments){if(f.motion==='fixed')continue;for(const[x,y]of corners(f)){radius=Math.max(radius,Math.hypot(x,y));if(f.motion==='recoiling')radius=Math.max(radius,Math.hypot(x,y-.085));}}
 envelopes.push({mechanism:core.partId,radius,minimumAxisSpacing:2*radius+.01});
}
let runtimeChecks,variantChecks;
if(process.argv.includes('--runtime')||process.argv.includes('--source-fit')){
 const fits=process.argv.includes('--source-fit')?structuredClone(data.fits):JSON.parse(fs.readFileSync('Assets/PaperDefense/Resources/Art/NotebookMounts.json')).fits;
 if(process.argv.includes('--source-fit')){
  const region=f=>({Motion:f.motion,Points:corners(f).map(([X,Y])=>({X,Y}))});
  const keepout=(f,id)=>{const p=corners(f),x=Math.min(...p.map(p=>p[0])),y=Math.min(...p.map(p=>p[1]));return{PartId:id,X:x,Y:y,Width:Math.max(...p.map(p=>p[0]))-x,Height:Math.max(...p.map(p=>p[1]))-y};};
  for(const frame of fits.Frames){
   const v=data.visualParts.find(p=>p.partId===frame.FrameId),f=v.fragments[0];
   frame.Mounts=v.mounts.map(m=>{const[x,y]=rotate((m.position.x-f.attach.x)*f.scale,(m.position.y-f.attach.y)*f.scale,f.radians);return{SlotIndex:m.slot,X:f.position.x+x,Y:f.position.y+y};});
   frame.Keepouts=v.fragments.filter(f=>f.collisionMode==='solid').map(f=>keepout(f,v.partId));
   const lower=v.hullBaseFraction||.18,upper=v.hullFrontFraction||.32;
   for(const hull of data.visualParts.filter(p=>p.category===1))frame.Keepouts.push(keepout({...f,region:{x:f.trim.x,y:f.trim.y+f.trim.height*lower,width:f.trim.width,height:f.trim.height*(upper-lower)}},hull.partId));
   for(const p of data.visualParts.filter(p=>p.category>=1&&p.category<=3&&p.partId!=='hull.standard'))for(const f of p.fragments.filter(f=>f.collisionMode==='solid'||f.collisionMode==='armor'))frame.Keepouts.push(keepout(f,p.partId));
  }
  fits.Weapons=data.visualParts.filter(p=>p.category===5).map(core=>({MechanismId:core.partId,PivotX:data.modernPivot.x,PivotY:data.modernPivot.y,Clearance:.01,RecoilLimit:.085,
   Regions:data.visualParts.filter(p=>p.category>=5&&p.category<=9&&(p.weaponFamilies&core.weaponFamilies)||p.category===4).flatMap(p=>p.fragments.map(region))}));
 }
 const rect=(r,p)=>({minX:Math.min(...r.Points.map(q=>q.X))+p.x,minY:Math.min(...r.Points.map(q=>q.Y))+p.y,maxX:Math.max(...r.Points.map(q=>q.X))+p.x,maxY:Math.max(...r.Points.map(q=>q.Y))+p.y});
 const circleNear=(p,r,b)=>Math.max(0,b.minX-p.x,p.x-b.maxX)**2+Math.max(0,b.minY-p.y,p.y-b.maxY)**2<=r*r;
 const rectsNear=(a,b,c)=>Math.max(0,a.minX-b.maxX,b.minX-a.maxX)**2+Math.max(0,a.minY-b.maxY,b.minY-a.maxY)**2<=c*c;
 const radius=w=>Math.max(...w.Regions.filter(r=>r.Motion!=='fixed').flatMap(r=>r.Points.flatMap(p=>[Math.hypot(p.X,p.Y),Math.hypot(p.X,p.Y-(r.Motion==='recoiling'?w.RecoilLimit:0))])));
 let configurations=0,placements=0;const collisions=[];
 for(const frame of fits.Frames)for(const weapon of fits.Weapons){
  configurations++; const selected=new Set([frame.FrameId,'hull.standard','armor.none','feed.standard','turn.standard']);
  const placed=frame.Mounts.map(s=>{const p={x:s.X+weapon.PivotX,y:s.Y+weapon.PivotY};return{slot:s.SlotIndex,p,r:radius(weapon),fixed:weapon.Regions.filter(r=>r.Motion==='fixed').map(r=>rect(r,p))};});placements+=placed.length;
  for(const a of placed)for(const k of frame.Keepouts){
   if(k.ArmorOnly||k.PartId&&!selected.has(k.PartId))continue;
   const b={minX:k.X,minY:k.Y,maxX:k.X+k.Width,maxY:k.Y+k.Height};
   const rotating=circleNear(a.p,a.r+weapon.Clearance,b),fixed=a.fixed.some(r=>rectsNear(r,b,weapon.Clearance));
   if(rotating||fixed)collisions.push({frame:frame.FrameId,weapon:weapon.MechanismId,slot:a.slot,part:k.PartId||'foundation',rotating,fixed});
  }
  for(let i=0;i<placed.length;i++)for(let j=i+1;j<placed.length;j++){
   const a=placed[i],b=placed[j],c=weapon.Clearance;
   if(Math.hypot(a.p.x-b.p.x,a.p.y-b.p.y)<=a.r+b.r+c||a.fixed.some(r=>circleNear(b.p,b.r+c,r))||b.fixed.some(r=>circleNear(a.p,a.r+c,r))||a.fixed.some(r=>b.fixed.some(q=>rectsNear(r,q,c))))collisions.push({frame:frame.FrameId,weapon:weapon.MechanismId,slots:[a.slot,b.slot],part:'neighbor'});
  }
 }
 runtimeChecks={configurations,placements,collisions};if(collisions.length)problems.push('Runtime fit has '+collisions.length+' physical clearance failures.');
 if(process.argv.includes('--variants')){
  const defaults=['frame.standard','hull.standard','armor.none','feed.standard','turn.standard'];
  const check=(frame,weapon,selected,sockets)=>{
   const placed=sockets.map(s=>{const p={x:s.X+weapon.PivotX,y:s.Y+weapon.PivotY};return{slot:s.SlotIndex,p,r:radius(weapon),fixed:weapon.Regions.filter(r=>r.Motion==='fixed').map(r=>rect(r,p))};}),failures=[];
   for(const a of placed)for(const k of frame.Keepouts){
    if(k.ArmorOnly&&!selected.has('armor.plate')||k.PartId&&!selected.has(k.PartId))continue;
    const b={minX:k.X,minY:k.Y,maxX:k.X+k.Width,maxY:k.Y+k.Height};
    if(circleNear(a.p,a.r+weapon.Clearance,b)||a.fixed.some(r=>rectsNear(r,b,weapon.Clearance)))failures.push({slot:a.slot,part:k.PartId||'foundation'});
   }
   for(let i=0;i<placed.length;i++)for(let j=i+1;j<placed.length;j++){
    const a=placed[i],b=placed[j],c=weapon.Clearance;
    if(Math.hypot(a.p.x-b.p.x,a.p.y-b.p.y)<=a.r+b.r+c||a.fixed.some(r=>circleNear(b.p,b.r+c,r))||b.fixed.some(r=>circleNear(a.p,a.r+c,r))||a.fixed.some(r=>b.fixed.some(q=>rectsNear(r,q,c))))failures.push({slots:[a.slot,b.slot],part:'neighbor'});
   }
   return failures;
  };
  variantChecks=[];
  for(const variant of data.visualParts.filter(p=>p.category>=0&&p.category<=4)){
   const selected=[...defaults];selected[variant.category]=variant.partId;
   const frames=fits.Frames.filter(frame=>variant.category!==0||frame.FrameId===variant.partId);
   let tested=0,valid=0,witness,firstFailure;const validFrames=new Set();
   for(const frame of frames)for(const weapon of fits.Weapons)for(let bits=1;bits<1<<frame.Mounts.length;bits++){
    tested++;selected[0]=frame.FrameId;const slots=frame.Mounts.filter((_,i)=>bits&1<<i),failures=check(frame,weapon,new Set(selected),slots);
    if(!failures.length){valid++;validFrames.add(frame.FrameId);witness??={frame:frame.FrameId,mechanism:weapon.MechanismId,slots:slots.map(s=>s.SlotIndex)};}
    else firstFailure??={frame:frame.FrameId,mechanism:weapon.MechanismId,slots:slots.map(s=>s.SlotIndex),failures};
   }
   variantChecks.push({part:variant.partId,tested,valid,witness,validFrames:[...validFrames],...(!valid?{firstFailure}:{})});
   if(!valid)problems.push('No valid real frame/socket witness for '+variant.partId);
  }
 }
}
console.log(JSON.stringify({definitions:data.visualParts.length,nativeCrops:source.sprites.length,sourcePixelArea:source.sprites.reduce((n,s)=>n+s.width*s.height,0),envelopes,runtimeChecks,variantChecks,problems},null,2));
if(problems.length)process.exitCode=1;
