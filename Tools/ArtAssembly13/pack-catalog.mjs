// Read-only native rectangle audit. No pixels or Unity assets are changed.
import fs from 'node:fs';
import { relocateNative } from './repair-native-layout.mjs';
const source=JSON.parse(fs.readFileSync('ArtSource/Notebook/NewStyle/sprites.source.json','utf8'));
const size=4096,gap=4,bin=size+gap;
const items=source.sprites.map((s,index)=>({id:s.id,index,w:s.width+gap,h:s.height+gap}));
items.push({id:'$pen-white',index:items.length,w:9+gap,h:9+gap});
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
function score(free,r,method,placed){
 const dx=free.w-r.w,dy=free.h-r.h;
 if(method==='short')return[Math.min(dx,dy),Math.max(dx,dy),free.y,free.x];
 if(method==='long')return[Math.max(dx,dy),Math.min(dx,dy),free.y,free.x];
 if(method==='area')return[free.w*free.h-r.w*r.h,Math.min(dx,dy),free.y,free.x];
 if(method==='bottom')return[free.y+r.h,free.x,Math.min(dx,dy),Math.max(dx,dy)];
 let contact=0;
 if(free.x===0||free.x+r.w===bin)contact+=r.h;
 if(free.y===0||free.y+r.h===bin)contact+=r.w;
 for(const p of placed){
  if(p.x===free.x+r.w||p.x+p.w===free.x)contact+=Math.max(0,Math.min(p.y+p.h,free.y+r.h)-Math.max(p.y,free.y));
  if(p.y===free.y+r.h||p.y+p.h===free.y)contact+=Math.max(0,Math.min(p.x+p.w,free.x+r.w)-Math.max(p.x,free.x));
 }
 return[-contact,Math.min(dx,dy),free.y,free.x];
}
const less=(a,b)=>!b||a.some((v,i)=>v!==b[i]&&a.slice(0,i).every((v,j)=>v===b[j])&&v<b[i]);
function pack(order,method,global){
 let free=[{x:0,y:0,w:bin,h:bin}],remaining=[...order],placed=[];
 while(remaining.length){
  let best,chosen,box,bestIndex;
  const length=global?remaining.length:1;
  for(let i=0;i<length;i++){
   const r=remaining[i];
   for(const f of free)if(r.w<=f.w&&r.h<=f.h){
    const s=score(f,r,method,placed);
    if(less(s,best)){best=s;chosen=r;box={x:f.x,y:f.y,w:r.w,h:r.h};bestIndex=i;}
   }
  }
  if(!chosen)return{placed,remaining};
  remaining.splice(bestIndex,1);placed.push({...box,id:chosen.id,index:chosen.index});free=insert(free,box);
 }
 return{placed,remaining};
}
const comparators={area:(a,b)=>b.w*b.h-a.w*a.h,long:(a,b)=>Math.max(b.w,b.h)-Math.max(a.w,a.h),short:(a,b)=>Math.min(b.w,b.h)-Math.min(a.w,a.h),height:(a,b)=>b.h-a.h,width:(a,b)=>b.w-a.w,perimeter:(a,b)=>b.w+b.h-a.w-a.h,ratio:(a,b)=>Math.max(b.w/b.h,b.h/b.w)-Math.max(a.w/a.h,a.h/a.w)};
const methods=['short','long','area','bottom','contact'];let best,attempts=0;
function tryPack(order,method,global,label){
 attempts++;const result=pack(order,method,global);
 if(!best||result.placed.length>best.placed.length)best={...result,label,method,global};
 if(result.remaining.length)return false;
 const placed=result.placed;
 for(let i=0;i<placed.length;i++){
  const p=placed[i];if(p.x<0||p.y<0||p.x+p.w-gap>size||p.y+p.h-gap>size)throw new Error('Out of bounds '+p.id);
  for(let j=0;j<i;j++)if(intersect(p,placed[j]))throw new Error('Gutter overlap '+p.id);
 }
 const output={atlas:size,gutter:gap,algorithm:'MaxRects',method,order:label,global,attempts,
  nativePixelArea:items.reduce((n,r)=>n+(r.w-gap)*(r.h-gap),0),
  rectangles:placed.sort((a,b)=>a.index-b.index).map(p=>({id:p.id,index:p.index,x:p.x,y:p.y,width:p.w-gap,height:p.h-gap}))};
 fs.writeFileSync('Tools/ArtAssembly13/packing-4096.json',JSON.stringify(output,null,2)+'\n');
 console.log(JSON.stringify({success:true,...output,rectangles:output.rectangles.length},null,2));return true;
}
for(const[name,compare]of Object.entries(comparators))for(const method of methods)for(const global of[false,true]){
 const order=[...items].sort((a,b)=>compare(a,b)||b.w*b.h-a.w*a.h||a.index-b.index);
 if(tryPack(order,method,global,name))process.exit(0);
}
// A small native crop change can destabilise an otherwise excellent complete
// layout. Deterministically relocate only its neighbours from the immutable
// accepted baseline before attempting a broader seeded search.
const baseline='Tools/ArtAssembly13/packing-baseline-v3.json';
if(fs.existsSync(baseline)){
 const repaired=relocateNative(source,JSON.parse(fs.readFileSync(baseline,'utf8')));
 if(repaired){fs.writeFileSync('Tools/ArtAssembly13/packing-4096.json',JSON.stringify(repaired,null,2)+'\n');console.log(JSON.stringify({success:true,...repaired,rectangles:repaired.rectangles.length},null,2));process.exit(0);}
}
let seed=1;const random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};
const limit=Number(process.argv[2]??3000),deadline=Date.now()+45000;
for(let i=0;i<limit&&Date.now()<deadline;i++){
 const exponent=random()*2+0.25,noise=random()*.4;
 const order=items.map(r=>({...r,priority:Math.pow(r.w*r.h,exponent)*(1+noise*random())})).sort((a,b)=>b.priority-a.priority||a.index-b.index);
 // Local perturbations preserve large-first packing without limiting the
 // search to one static sort. Fixed PRNG seed makes each run reproducible.
 for(let k=0;k<3+i%8;k++){const at=Math.floor(random()*order.length),other=Math.min(order.length-1,Math.max(0,at+Math.floor((random()-.5)*12)));[order[at],order[other]]=[order[other],order[at]];}
 if(tryPack(order,methods[i%methods.length],false,'seeded-'+i))process.exit(0);
}
console.log(JSON.stringify({success:false,attempts,best:{placed:best.placed.length,total:items.length,order:best.label,method:best.method,global:best.global,
 remaining:best.remaining.map(r=>({id:r.id,width:r.w-gap,height:r.h-gap}))},nativePixelArea:items.reduce((n,r)=>n+(r.w-gap)*(r.h-gap),0)},null,2));
