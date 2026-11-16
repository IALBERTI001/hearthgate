// A browser kept open for the player bot: HTTP commands drive the game at 1280x720.
const {chromium}=require('playwright'); const http=require('http'); const url=require('url');
(async()=>{
  const b=await chromium.launch({args:['--use-gl=angle','--use-angle=swiftshader','--enable-unsafe-swiftshader','--ignore-gpu-blocklist','--autoplay-policy=no-user-gesture-required']});
  const ctx=await b.newContext({viewport:{width:1280,height:720}}); const p=await ctx.newPage();
  const logs=[]; p.on('console',m=>{logs.push(m.type()+': '+m.text()); if(logs.length>400)logs.shift();}); p.on('pageerror',e=>logs.push('PAGEERROR '+e.message));
  await p.goto('http://127.0.0.1:8900/index.html');
  let n=0;
  http.createServer(async(req,res)=>{
    const q=url.parse(req.url,true).query, path=url.parse(req.url).pathname; let out='ok';
    try{
      if(path==='/shot'){ const f='/home/claude/hgbot/shots/'+String(++n).padStart(4,'0')+(q.name?'_'+q.name:'')+'.png'; await p.screenshot({path:f}); out=f; }
      else if(path==='/click'){ await p.mouse.move(+q.x,+q.y); await p.waitForTimeout(80); await p.mouse.down(); await p.waitForTimeout(80); await p.mouse.up(); }
      else if(path==='/hold'){ await p.mouse.move(+q.x,+q.y); await p.mouse.down(); await p.waitForTimeout(+(q.ms||800)); if(q.tx){await p.mouse.move(+q.tx,+q.ty,{steps:8}); await p.waitForTimeout(200);} await p.mouse.up(); }
      else if(path==='/move'){ await p.mouse.move(+q.x,+q.y); }
      else if(path==='/key'){ await p.keyboard.press(q.k); }
      else if(path==='/wait'){ await p.waitForTimeout(+(q.ms||1000)); }
      else if(path==='/eval'){ out=JSON.stringify(await p.evaluate(q.js)); }
      else if(path==='/logs'){ out=logs.slice(-(+q.n||30)).join('\n'); }
      else if(path==='/reload'){ await p.goto('http://127.0.0.1:8900/index.html?'+Date.now()); }
      else if(path==='/fresh'){ await p.evaluate(()=>new Promise(r=>{const d=indexedDB.deleteDatabase('/idbfs');d.onsuccess=d.onerror=d.onblocked=()=>r();})); await p.goto('http://127.0.0.1:8900/index.html?'+Date.now()); }
    }catch(e){ out='ERR '+e.message; }
    res.end(out);
  }).listen(8901);
  console.log('bot ready');
})();
