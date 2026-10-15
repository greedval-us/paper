// Optional local preview. The album also opens directly from index.html.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=__dirname;
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.png':'image/png','.json':'application/json','.ttf':'font/ttf','.md':'text/plain; charset=utf-8'};
http.createServer((req,res)=>{
 const name=decodeURIComponent(req.url.split('?')[0]);
 const file=path.resolve(root,'.'+(name.endsWith('/')?name+'index.html':name));
 if(!file.startsWith(root+path.sep)){res.writeHead(403);return res.end()}
 fs.readFile(file,(err,data)=>{if(err){res.writeHead(404);return res.end('Not found')}res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});res.end(data)});
}).listen(18766,'127.0.0.1',()=>console.log('Design album: http://127.0.0.1:18766'));
