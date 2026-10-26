// Read-only source inspection. Reports connected alpha bounds; never edits image pixels.
import fs from 'node:fs';
import zlib from 'node:zlib';
for(const file of process.argv.slice(2)){
 const b=fs.readFileSync(file),chunks=[];let at=8,w,h,type,depth;
 while(at<b.length){const n=b.readUInt32BE(at),t=b.toString('ascii',at+4,at+8),p=b.subarray(at+8,at+8+n);if(t==='IHDR'){w=p.readUInt32BE(0);h=p.readUInt32BE(4);depth=p[8];type=p[9];}if(t==='IDAT')chunks.push(p);at+=n+12;}
 if(type!==6||depth!==8)throw Error('Expected RGBA8 source');
 const raw=zlib.inflateSync(Buffer.concat(chunks)),stride=w*4,pixels=Buffer.alloc(h*stride);let pos=0;
 const paeth=(a,b,c)=>{const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;};
 for(let y=0;y<h;y++){const f=raw[pos++];for(let x=0;x<stride;x++){const a=x>=4?pixels[y*stride+x-4]:0,u=y?pixels[(y-1)*stride+x]:0,c=y&&x>=4?pixels[(y-1)*stride+x-4]:0;let v=raw[pos++];if(f===1)v+=a;else if(f===2)v+=u;else if(f===3)v+=Math.floor((a+u)/2);else if(f===4)v+=paeth(a,u,c);else if(f!==0)throw Error('Unknown PNG filter');pixels[y*stride+x]=v&255;}}
 const visited=new Uint8Array(w*h),queue=new Int32Array(w*h),components=[];
 for(let p=0;p<w*h;p++){
  if(visited[p]||pixels[p*4+3]<=12)continue;
  let head=0,tail=1,minX=w,minY=h,maxX=0,maxY=0;queue[0]=p;visited[p]=1;
  while(head<tail){const current=queue[head++],x=current%w,y=Math.floor(current/w);minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);
   for(const n of [x>0?current-1:-1,x+1<w?current+1:-1,y>0?current-w:-1,y+1<h?current+w:-1])if(n>=0&&!visited[n]&&pixels[n*4+3]>12){visited[n]=1;queue[tail++]=n;}
  }
  if(tail>=100)components.push({pixels:tail,left:minX,top:minY,right:maxX,bottom:maxY});
 }
 components.sort((a,b)=>b.pixels-a.pixels);
 console.log(JSON.stringify({file,width:w,height:h,components},null,2));
}
