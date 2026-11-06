// The stats overlay on a maxed save (owner's screenshot, Nov 6 2026: late-game labels piled into each other).
// Fails if any two labels overlap or a label leaves the screen. Usage: node tools/qa/overlay-check.cjs <url?qa>
const { chromium } = require('playwright');
(async () => {
  const b = await chromium.launch(); let bad = 0;
  for (const [w, h] of [[360, 640], [390, 844], [412, 915], [1280, 720]]) {
    const ctx = await b.newContext({ viewport: { width: w, height: h } });
    await ctx.addInitScript(() => { if (!sessionStorage.s) { sessionStorage.s = 1; const all = ['forge','frostSpire','stormBell','bakery','market','tavern','granary','library','cottages','watchtower'];
      localStorage.setItem('hearthgate.profile.v1', JSON.stringify({ firstSession: false, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0,10), streak: 1 }, restored: all, levels: Object.fromEntries(all.map(k => [k, 100])),
        res: 1e40, gateLevel: 90, wave: 140, bestWave: 140, ascensions: 9, settings: { stats: true, music: false, sfx: false } })); } });
    const p = await ctx.newPage(); await p.goto(process.argv[2]); await p.waitForFunction(() => !document.getElementById('boot')); await p.waitForTimeout(1500);
    const r = await p.evaluate(() => { const R = Hearthgate.state().S.tagRects || [], out = [];
      for (let i = 0; i < R.length; i++) { const a = R[i]; if (a[0] < 0 || a[0] + a[2] > 720) out.push('off screen ' + i);
        for (let j = i + 1; j < R.length; j++) { const c = R[j]; if (a[0] < c[0] + c[2] && c[0] < a[0] + a[2] && a[1] < c[1] + c[3] && c[1] < a[1] + a[3]) out.push(`overlap ${i}/${j}`); } }
      return { n: R.length, out }; });
    if (r.out.length || r.n < 15) { bad++; console.log(w, h, r.n, r.out.slice(0, 5)); }
    if (w === 390) await p.screenshot({ path: (process.argv[3] || '.') + '/overlay-max.png' });
    await ctx.close();
  }
  console.log(bad ? 'OVERLAY PROBLEMS' : 'overlay labels clear'); await b.close(); process.exit(bad ? 1 : 0);
})();
