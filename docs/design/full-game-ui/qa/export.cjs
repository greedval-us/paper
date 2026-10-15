async (page) => {
  const base='http://127.0.0.1:18766/';
  const root='D:/Projekt Unity/paper/docs/design/full-game-ui/';
  await page.goto(base+'index.html');
  const catalog=await page.evaluate(()=>SCREENS);
  const errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  const results=[];
  const inspect=()=>{
    const w=innerWidth,h=innerHeight;
    const outside=[...document.querySelectorAll('button,input,select')].filter(e=>{
      if(!e.getClientRects().length)return false;
      const r=e.getBoundingClientRect();
      let parent=e.parentElement;
      while(parent&&parent!==document.body){
        if(['auto','scroll'].includes(getComputedStyle(parent).overflowY))return false;
        parent=parent.parentElement;
      }
      return r.left< -1||r.right>w+1||r.top< -1||r.bottom>h+1;
    }).map(e=>({label:e.textContent.trim().slice(0,45)||e.ariaLabel,box:JSON.stringify(e.getBoundingClientRect())}));
    const horizontal=[...document.querySelectorAll('section,aside,.pagecontent,.battle-bottom,.battle-top,.wizard-main,.modal')].filter(e=>e.clientWidth>0&&e.scrollWidth>e.clientWidth+2).map(e=>({class:e.className,over:e.scrollWidth-e.clientWidth}));
    return {bodyOverflow:document.documentElement.scrollWidth>w+1,outside,horizontal};
  };
  for(const [id] of catalog){
    await page.setViewportSize({width:1600,height:1000});
    await page.goto(base+'screen.html#'+id);
    await page.evaluate(()=>document.fonts.ready);
    await page.screenshot({path:root+'screens/'+id+'.png'});
    results.push({id,width:1600,height:1000,scale:1,...await page.evaluate(inspect)});
    await page.setViewportSize({width:1280,height:720});
    results.push({id,width:1280,height:720,scale:1,...await page.evaluate(inspect)});
  }
  for(const [width,height,scale] of [[1024,768,1],[1920,1080,1],[2560,1080,1],[1600,1000,1.25],[1600,1000,1.5],[1280,720,1.5]]){
    for(const id of ['home','drawing','battle','preparation','settings']){
      await page.setViewportSize({width,height});
      await page.goto(base+'screen.html#'+id);
      await page.evaluate(s=>document.documentElement.style.setProperty('--text-scale',s),scale);
      await page.evaluate(()=>document.fonts.ready);
      if(['home','drawing','battle'].includes(id))await page.screenshot({path:root+'qa/'+id+'-'+width+'x'+height+'-'+scale+'.png'});
      results.push({id,width,height,scale,...await page.evaluate(inspect)});
    }
  }
  await page.goto('about:blank');
  const handle=await page.evaluateHandle(data=>new Blob([JSON.stringify(data,null,2)],{type:'application/json'}),{checked:results.length,errors,results});
  const downloadPromise=page.waitForEvent('download');
  await page.evaluate(blob=>{let a=document.createElement('a');a.href=URL.createObjectURL(blob);a.download='layout-report.json';a.click()},handle);
  const download=await downloadPromise;
  await download.saveAs(root+'qa/layout-report.json');
  await page.goto(base+'index.html');
  return {checked:results.length,errors,issues:results.filter(r=>r.bodyOverflow||r.outside.length||r.horizontal.length)};
}
