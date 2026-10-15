async page=>{
 const base='http://127.0.0.1:18766/screens/index.html';
 await page.setViewportSize({width:1920,height:1080});
 for(const sheet of [0,1,2,3,4,'cover']){
  await page.goto(base+(sheet==='cover'?'?cover=1':'?sheet='+sheet));
  await page.evaluate(()=>document.fonts.ready);
  await page.locator('img').evaluateAll(async imgs=>{await Promise.all(imgs.map(i=>i.decode()))});
  await page.screenshot({path:'D:/Projekt Unity/paper/docs/design/full-game-ui/qa/'+(sheet==='cover'?'overview':'sheet-'+sheet)+'.png',fullPage:true});
 }
 return 'Five review sheets and overview exported';
}
