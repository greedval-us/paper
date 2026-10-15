async (page) => {
  await page.setViewportSize({width:1600,height:1000});
  const out='D:/Projekt Unity/paper/docs/design/full-game-ui/qa/';
  for(const id of ['home','drawing','battle']){
    await page.goto('http://127.0.0.1:18766/screen.html#'+id);
    await page.evaluate(()=>document.fonts.ready);
    await page.screenshot({path:out+id+'.png'});
  }
  console.log('Three previews exported');
}
