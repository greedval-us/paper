// Read-only alpha contact diagnostics. Never rewrites source pixels or metadata.
import fs from 'node:fs';
import zlib from 'node:zlib';
const art='ArtSource/Notebook/NewStyle/';
const assembly=JSON.parse(fs.readFileSync(art+'assembly-parts.source.json'));
const sprites=JSON.parse(fs.readFileSync(art+'sprites.source.json')).sprites;
const images=new Map();
function decode(file){
 if(images.has(file))return images.get(file);
 const b=fs.readFileSync(file),chunks=[];let at=8,w,h,type,depth;
 while(at<b.length){const n=b.readUInt32BE(at),t=b.toString('ascii',at+4,at+8),p=b.subarray(at+8,at+8+n);if(t==='IHDR'){w=p.readUInt32BE(0);h=p.readUInt32BE(4);depth=p[8];type=p[9];}if(t==='IDAT')chunks.push(p);at+=n+12;}
 if(type!==6||depth!==8)throw Error('Expected RGBA8 source');
 const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=w*4,pixels=Buffer.alloc(h*stride);let pos=0;
 const paeth=(a,b,c)=>{const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;};
 for(let y=0;y<h;y++){const f=raw[pos++];for(let x=0;x<stride;x++){const a=x>=4?pixels[y*stride+x-4]:0,u=y?pixels[(y-1)*stride+x]:0,c=y&&x>=4?pixels[(y-1)*stride+x-4]:0;let v=raw[pos++];if(f===1)v+=a;else if(f===2)v+=u;else if(f===3)v+=Math.floor((a+u)/2);else if(f===4)v+=paeth(a,u,c);else if(f!==0)throw Error('Unknown PNG filter');pixels[y*stride+x]=v&255;}}
 const result={w,h,pixels};images.set(file,result);return result;
}
if(process.argv.includes('--battery-landmarks')){
 const im=decode(art+'BatteryFoundation-v3.png'),visited=new Uint8Array(im.w*im.h),queue=new Int32Array(im.w*im.h),gold=[];
 const ok=p=>{const o=p*4,r=im.pixels[o],g=im.pixels[o+1],b=im.pixels[o+2];return im.pixels[o+3]>128&&r>100&&g>70&&g<r*.85&&b<g*.7;};
 for(let y=250;y<400;y++)for(let x=30;x<600;x++){
  const start=y*im.w+x;if(visited[start]||!ok(start))continue;let n=1,head=0,sx=0,sy=0;visited[start]=1;queue[0]=start;
  while(head<n){const at=queue[head++],x=at%im.w,y=Math.floor(at/im.w);sx+=x;sy+=y;for(const p of[at-1,at+1,at-im.w,at+im.w])if(p>=0&&p<visited.length&&!visited[p]&&ok(p)){visited[p]=1;queue[n++]=p;}}
  if(n>4)gold.push({pixels:n,x:sx/n,y:sy/n});
 }
 console.log(JSON.stringify(gold,null,2));process.exit(0);
}
const part=id=>assembly.visualParts.find(p=>p.partId===id);
function alpha(f,x,y){const s=sprites.find(s=>s.id===f.spriteId),im=decode(s.source);x=Math.floor(s.x+x-f.trim.x);y=im.h-1-Math.floor(s.y+y-f.trim.y);return x<0||x>=im.w||y<0||y>=im.h?0:im.pixels[(y*im.w+x)*4+3];}
function points(f){const out=[];for(let y=Math.ceil(f.region.y);y<f.region.y+f.region.height;y++)for(let x=Math.ceil(f.region.x);x<f.region.x+f.region.width;x++)if(alpha(f,x,y)>=128)out.push([x,y]);return out;}
function tf(f,p,angle=0,recoil=0){const r=f.radians,dx=(p[0]-f.attach.x)*f.scale,dy=(p[1]-f.attach.y)*f.scale;let x=f.position.x+dx*Math.cos(r)-dy*Math.sin(r),y=f.position.y+dx*Math.sin(r)+dy*Math.cos(r)+(f.motion==='recoiling'?recoil:0);if(f.motion==='fixed')angle=0;return[x*Math.cos(angle)-y*Math.sin(angle),x*Math.sin(angle)+y*Math.cos(angle)];}
function sampleWorld(f,p){const dx=p[0]-f.position.x,dy=p[1]-f.position.y,r=-f.radians;return alpha(f,(dx*Math.cos(r)-dy*Math.sin(r))/f.scale+f.attach.x,(dx*Math.sin(r)+dy*Math.cos(r))/f.scale+f.attach.y);}
function visibleAlpha(f,p){const dx=p[0]-f.position.x,dy=p[1]-f.position.y,r=-f.radians,x=(dx*Math.cos(r)-dy*Math.sin(r))/f.scale+f.attach.x,y=(dx*Math.sin(r)+dy*Math.cos(r))/f.scale+f.attach.y,q=f.region;return x>=q.x&&y>=q.y&&x<q.x+q.width&&y<q.y+q.height?alpha(f,x,y):0;}
if(process.argv.includes('--visible-variants')){
 for(const id of['sight.iron','sight.scope','sight.tracking','trigger.standard','trigger.burst']){
  const selected=['core.gun','head.gun',id.startsWith('sight.')?id:'sight.iron','magazine.standard',id.startsWith('trigger.')?id:'trigger.standard'];
  const fs=selected.flatMap(id=>part(id).fragments),f=part(id).fragments[0];
  let total=0,visible=0;const rendered=[];
  for(const p of points(f)){total++;const world=tf(f,p);if(fs.filter(g=>g!==f&&g.layer>f.layer).every(g=>visibleAlpha(g,world)<128)){visible++;rendered.push(world);}}
  console.log(JSON.stringify({id,total,visible,fraction:visible/total,visibleHeight:Math.max(...rendered.map(p=>p[1]))-Math.min(...rendered.map(p=>p[1]))}));
 }
 process.exit(0);
}
if(process.argv.includes('--accessory-feet')){
 for(const id of['sight.scope','sight.tracking','trigger.burst']){const f=part(id).fragments[0],mid=points(f).filter(p=>Math.abs(p[0]-f.attach.x)<3),foot=mid.reduce((a,b)=>a[1]<b[1]?a:b);console.log(JSON.stringify({id,attach:f.attach,foot,world:tf(f,foot),alpha:alpha(f,...foot)}));}
 process.exit(0);
}
if(process.argv.includes('--foundation-contact')){
 const feed=part('feed.standard').fragments[0];
 const point=tf(feed,[200,250]);
 for(const id of['frame.standard','frame.heavy','frame.precision','frame.battery']){const f=part(id).fragments[0];console.log(JSON.stringify({frame:id,feedPoint:point,feedAlpha:alpha(feed,200,250),frameAlpha:sampleWorld(f,point)}));}
 process.exit(0);
}
if(process.argv.includes('--foundation-seats')){
 for(const id of['frame.standard','frame.heavy','frame.precision','frame.battery']){
  const p=part(id),f=p.fragments[0];
  for(const mount of p.mounts){const point=[mount.position.x,mount.position.y];
   const probes=[[0,0],[-2,0],[2,0],[0,-2],[0,2]].map(([dx,dy])=>alpha(f,point[0]+dx,point[1]+dy));
   if(Math.min(...probes)<128)throw new Error('A foundation seat is not opaque: '+id+'/'+mount.slot);
   console.log(JSON.stringify({frame:id,slot:mount.slot,source:point,world:tf(f,point),minimumAlpha:Math.min(...probes)}));
  }
 }
 process.exit(0);
}
if(process.argv.includes('--probes')){
 const sight=part('sight.iron').fragments[0],foot=tf(sight,[173,16]);
 for(const family of ['gun','piercer','mortar','fan','ricochet','stapler','blotter']){const core=part('core.'+family).fragments[0],native=points(core),pts=native.map(p=>tf(core,p)),centre=native.filter(p=>Math.abs(p[0]-core.attach.x)<=1),bottom=centre.reduce((a,b)=>a[1]<b[1]?a:b);console.log(JSON.stringify({family,foot:bottom,footWorld:tf(core,bottom),sightFoot:foot,coreAlphaAtSightFoot:sampleWorld(core,foot),bounds:[Math.min(...pts.map(p=>p[0])),Math.min(...pts.map(p=>p[1])),Math.max(...pts.map(p=>p[0])),Math.max(...pts.map(p=>p[1]))]}));}
 process.exit(0);
}
function distance(a,b){let best=Infinity,witness;const grid=new Map(),q=.004;for(const p of b){const k=Math.floor(p[0]/q)+','+Math.floor(p[1]/q);if(!grid.has(k))grid.set(k,[]);grid.get(k).push(p);}for(const p of a){const gx=Math.floor(p[0]/q),gy=Math.floor(p[1]/q);for(let y=gy-20;y<=gy+20;y++)for(let x=gx-20;x<=gx+20;x++)for(const b of grid.get(x+','+y)||[]){const d=Math.hypot(p[0]-b[0],p[1]-b[1]);if(d<best){best=d;witness=[p,b];}}}return{best,witness};}
for(const id of ['turn.standard','core.gun','core.mortar','sight.iron','magazine.standard','trigger.standard'])for(const f of part(id).fragments){
 const pts=points(f),centre=pts.filter(p=>Math.abs(p[0]-f.attach.x)<=6);
 const runs=[];let start=null;for(let y=0;y<=f.canvas.y;y++){if(y<f.canvas.y&&alpha(f,Math.floor(f.attach.x),y)>=128){if(start===null)start=y;}else if(start!==null){runs.push([start,y-1]);start=null;}}
 console.log(JSON.stringify({id,fragment:f,attachAlpha:alpha(f,f.attach.x,f.attach.y),centreRuns:runs,centreY:centre.length?[Math.min(...centre.map(p=>p[1])),Math.max(...centre.map(p=>p[1]))]:[],opaqueBounds:[Math.min(...pts.map(p=>p[0])),Math.min(...pts.map(p=>p[1])),Math.max(...pts.map(p=>p[0])),Math.max(...pts.map(p=>p[1]))]},null,2));
}
if(!process.argv.includes('--distance'))process.exit(0);
const hub=part('turn.standard').fragments[0],hp=points(hub).map(p=>tf(hub,p));
for(const family of ['gun','piercer','mortar','fan','ricochet','stapler','blotter']){
 const core=part('core.'+family).fragments[0];
 console.log(JSON.stringify({family,hubCore:distance(points(core).map(p=>tf(core,p)),hp)}));
}
