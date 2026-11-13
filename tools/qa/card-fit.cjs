// Every card on every phone shape: the card body stays inside the screen and its close/primary button can be tapped.
const { chromium } = require('playwright');
const KINDS = [['perks'], ['quests'], ['shop'], ['daily', { day: 3, big: false, amount: 1234 }], ['offline', { secs: 9000, res: 5e5, fight: 4e5, city: 1e5 }],
  ['ascend'], ['building', 'forge'], ['settings'], ['pick', ['library', 'granary']], ['fork', 2]];
(async () => {
  const b = await chromium.launch(); const bad = [];
  for (const [w, h] of [[360, 640], [375, 667], [390, 844], [412, 915], [430, 932], [800, 450], [1280, 720]]) {
    const ctx = await b.newContext({ viewport: { width: w, height: h }, hasTouch: w < 500 });
    await ctx.addInitScript(() => { if (!sessionStorage.s) { sessionStorage.s = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify({ firstSession: false, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0,10), streak: 1 }, restored: ['forge','frostSpire','stormBell'], combos: ['shatter'], res: 1e5, gateLevel: 20, wave: 30, bestWave: 30, ascensions: 4, perkPts: 6, shop: { open: true, shards: 50 } })); } });
    const p = await ctx.newPage(); await p.goto('http://localhost:8765/index.html?qa'); await p.waitForFunction(() => !document.getElementById('boot'));
    // every on-screen control sits inside the stage (a CSS rule once moved the Surge button off screen, Nov 6 2026)
    await p.waitForTimeout(400);
    const off = await p.evaluate(() => { const st = document.getElementById('stage').getBoundingClientRect();
      return ['#pause', '#up', '#re', '#wave', '#city', '#combos'].filter(q => { const e = document.querySelector(q); if (!e || e.hidden) return false; const r = e.getBoundingClientRect();
        return r.left < st.left - 1 || r.right > st.right + 1 || r.top < st.top - 1 || r.bottom > st.bottom + 1 || !r.width; }); });
    if (off.length) bad.push(`${w}x${h} outside the stage: ${off.join(', ')}`);
    for (const [kind, data] of KINDS) {
      if (kind === 'quests') { await p.evaluate(() => Hearthgate.act('card', { kind: 'quests' })); }
      else await p.evaluate(([kind, data]) => Hearthgate.act('card', { kind, data }), [kind, data]);
      await p.waitForTimeout(350);
      const r = await p.evaluate(() => { const st = document.getElementById('stage').getBoundingClientRect(), bd = document.querySelector('#card .body').getBoundingClientRect();
        const btns = [...document.querySelectorAll('#card #c-no, #card #c-ok, #card .opt')]; const vis = btns.filter(x => { const r = x.getBoundingClientRect(); return r.top >= st.top - 1 && r.bottom <= st.bottom + 1 && r.height > 0; });
        const bar = document.getElementById('bar').getBoundingClientRect(); return { wide: document.getElementById('stage').classList.contains('wide'), overBar: Math.round(bd.bottom - bar.top), over: Math.round(bd.bottom - st.bottom), top: Math.round(bd.top - st.top), buttons: btns.length, visible: vis.length }; });
      if (r.over > 1 || (r.overBar > 1 && !r.wide) || r.top < -1 || !r.visible) bad.push(`${w}x${h} ${kind} ${JSON.stringify(r)}`);
      await p.evaluate(() => { const x = document.querySelector('#card #c-no') || document.querySelector('#card #c-ok'); if (x) x.click(); else { document.getElementById('card').className = ''; } });
      await p.evaluate(() => { const s = Hearthgate.state().S; s.card = null; document.getElementById('card').className = ''; });
    }
    if (w === 360) { await p.evaluate(() => Hearthgate.act('card', { kind: 'perks' })); await p.waitForTimeout(350); await p.screenshot({ path: (process.argv[2] || '.') + '/perks360.png' }); }
    await ctx.close();
  }
  console.log(bad.length ? 'PROBLEMS\n' + bad.join('\n') : 'all cards fit'); await b.close();
})();
