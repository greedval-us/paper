// Deterministic local relocation from an accepted native-pixel layout.
// Only rectangle coordinates are written; source pixels never change.
import fs from 'node:fs';
import { pathToFileURL } from 'node:url';
export function relocateNative(source,seed){
const size=4096,gap=4,bin=size+gap;
const defs=[...source.sprites.map((s,index)=>({id:s.id,index,w:s.width+gap,h:s.height+gap})),{id:'$pen-white',index:source.sprites.length,w:13,h:13}];
const resized=defs.filter(d=>{const p=seed.rectangles.find(p=>p.id===d.id);return !p||p.width+gap!==d.w||p.height+gap!==d.h;});
if(resized.length!==1)return null;
const target=resized[0].id;
const old=seed.rectangles.filter(p=>p.id!==target).map(p=>({...p,w:p.width+gap,h:p.height+gap}));
const changed=defs.find(p=>p.id===target);
if(old.length!==defs.length-1)return null;
for(const p of old){const d=defs.find(d=>d.id===p.id);if(!d||d.w!==p.w||d.h!==p.h)return null;}
const contains=(a,b)=>a.x<=b.x&&a.y<=b.y&&a.x+a.w>=b.x+b.w&&a.y+a.h>=b.y+b.h;
const intersect=(a,b)=>a.x<b.x+b.w&&a.x+a.w>b.x&&a.y<b.y+b.h&&a.y+a.h>b.y;
function insert(free,box){
 const split=[];
 for(const r of free){
  if(!intersect(r,box)){split.push(r);continue;}
  if(box.x>r.x)split.push({x:r.x,y:r.y,w:box.x-r.x,h:r.h});
  if(box.x+box.w<r.x+r.w)split.push({x:box.x+box.w,y:r.y,w:r.x+r.w-box.x-box.w,h:r.h});
  if(box.y>r.y)split.push({x:r.x,y:r.y,w:r.w,h:box.y-r.y});
  if(box.y+box.h<r.y+r.h)split.push({x:r.x,y:box.y+box.h,w:r.w,h:r.y+r.h-box.y-box.h});
 }
 return split.filter((r,i)=>!split.some((s,j)=>i!==j&&contains(s,r)&&(s.w!==r.w||s.h!==r.h||j<i)));
}
const xs=new Set([0,bin-changed.w]),ys=new Set([0,bin-changed.h]);
for(const p of old){for(const x of[p.x,p.x+p.w,p.x-changed.w,p.x+p.w-changed.w])if(x>=0&&x+changed.w<=bin)xs.add(x);for(const y of[p.y,p.y+p.h,p.y-changed.h,p.y+p.h-changed.h])if(y>=0&&y+changed.h<=bin)ys.add(y);}
const candidates=[];
for(const x of xs)for(const y of ys){const box={...changed,x,y},blocked=old.filter(p=>intersect(p,box));candidates.push({box,blocked,area:blocked.reduce((n,p)=>n+p.w*p.h,0)});}
candidates.sort((a,b)=>a.area-b.area||a.blocked.length-b.blocked.length||a.box.y-b.box.y||a.box.x-b.box.x);
let attempts=0;
for(const candidate of candidates.slice(0,5000)){
 const staticParts=old.filter(p=>!candidate.blocked.includes(p)),free=staticParts.reduce((free,p)=>insert(free,p),[{x:0,y:0,w:bin,h:bin}]);
 const initial=insert(free,candidate.box);
 for(const order of['area','width','height','long'])for(const score of['short','area','bottom']){
  attempts++;let available=initial,placed=[...staticParts,candidate.box],failed=false;
  const blocked=[...candidate.blocked].sort((a,b)=>order==='area'?b.w*b.h-a.w*a.h:order==='width'?b.w-a.w:order==='height'?b.h-a.h:Math.max(b.w,b.h)-Math.max(a.w,a.h));
  for(const p of blocked){
   const fits=available.filter(f=>p.w<=f.w&&p.h<=f.h).map(f=>({f,score:score==='short'?Math.min(f.w-p.w,f.h-p.h):score==='area'?f.w*f.h-p.w*p.h:f.y+p.h})).sort((a,b)=>a.score-b.score||a.f.y-b.f.y||a.f.x-b.f.x);
   if(!fits.length){failed=true;break;}const box={...p,x:fits[0].f.x,y:fits[0].f.y};placed.push(box);available=insert(available,box);
  }
  if(failed)continue;
  for(let i=0;i<placed.length;i++){const p=placed[i];if(p.x<0||p.y<0||p.x+p.w>bin||p.y+p.h>bin)throw Error('Bounds');for(let j=0;j<i;j++)if(intersect(p,placed[j]))throw Error('Overlap');}
  const output={atlas:size,gutter:gap,algorithm:'MaxRects local relocation',changed:target,order,score,attempts,nativePixelArea:defs.reduce((n,p)=>n+(p.w-gap)*(p.h-gap),0),
   rectangles:defs.map(d=>{const p=placed.find(p=>p.id===d.id);return{id:d.id,index:d.index,x:p.x,y:p.y,width:d.w-gap,height:d.h-gap};})};
  return output;
 }
}
return null;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const source=JSON.parse(fs.readFileSync('ArtSource/Notebook/NewStyle/sprites.source.json'));
 const seed=JSON.parse(fs.readFileSync(process.argv[2]||'Tools/ArtAssembly13/packing-baseline-v3.json'));
 const result=relocateNative(source,seed);
 if(result)fs.writeFileSync(process.argv[3]||'Tools/ArtAssembly13/packing-relocated-candidate.json',JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify({success:!!result,attempts:result?.attempts,rectangles:result?.rectangles.length,nativePixelArea:result?.nativePixelArea},null,2));
}
