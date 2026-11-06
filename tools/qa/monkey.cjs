// Monkey test (Nov 6 2026): random taps on the field, the city, every button and card for a few minutes of play, at
// several saves and two window sizes; any page error fails. Usage: node tools/qa/monkey.cjs <url?qa> [taps=600]
const { chromium } = require('playwright');
const [url, n = '600'] = process.argv.slice(2);
const ALL = ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower'];
const SAVES = [null, { restored: ALL.slice(0, 5), gateLevel: 18, wave: 22, bestWave: 22, res: 5e5 }, { restored: ALL, gateLevel: 45, wave: 44, runWave: 44, bestWave: 70, res: 1e9, ascensions: 6, perkPts: 5, stars: ['anvil0'] }];
(async () => {
  const b = await chromium.launch(); let errors = [];
  for (const [w, h] of [[390, 844], [1280, 720]]) for (const sv of SAVES) {
    const p = await b.newPage({ viewport: { width: w, height: h } }); p.on('pageerror', e => errors.push(`${w}x${h} ${sv ? 'asc' + (sv.ascensions || 0) : 'fresh'}: ${e.message}`));
    if (sv) await p.addInitScript(s => { if (!sessionStorage.s) { sessionStorage.s = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify(Object.assign({ firstSession: false, lastSeen: Date.now() - 3600e3, daily: { day: '2026-01-01', streak: 2 }, shop: { open: true, shards: 80 } }, s))); } }, sv);
    await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'));
    for (let i = 0; i < +n; i++) {
      const r = Math.random();
      if (r < 0.45) { const els = await p.$$('#card button, #hud button, #bar button, .side button, #card .opt, #card .tab'); const vis = []; for (const e of els) if (await e.isVisible()) vis.push(e); if (vis.length) await vis[Math.floor(Math.random() * vis.length)].click({ force: true, timeout: 500 }).catch(() => {}); }
      else if (r < 0.9) await p.mouse.click(Math.random() * w, Math.random() * h);
      else await p.keyboard.press(['u', 'r', 'p', 'i', 'Escape', ' '][Math.floor(Math.random() * 6)]);
      if (i % 20 === 0) await p.evaluate(() => Hearthgate.sim(2));
    }
    await p.close();
  }
  console.log(errors.length ? 'monkey errors: ' + [...new Set(errors)].slice(0, 8).join(' | ') : 'monkey ok'); await b.close();
})();
