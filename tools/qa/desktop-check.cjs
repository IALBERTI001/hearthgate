// Desktop side panels (Nov 6 2026): shown beside the stage when there is room, never over it, inside the window,
// hidden on phones and during a new player's opening. Usage: node tools/qa/desktop-check.cjs <url?qa>
const { chromium } = require('playwright');
const url = process.argv[2];
const SAVE = { firstSession: false, lastSeen: Date.now(), daily: { day: new Date().toISOString().slice(0, 10), streak: 1 }, restored: ['forge', 'frostSpire', 'stormBell', 'bakery'], res: 1e5, gateLevel: 12, wave: 14, bestWave: 14, shop: { open: true, shards: 5 } };
(async () => {
  const b = await chromium.launch(), problems = [];
  for (const [w, h, want, fresh] of [[1280, 720, true], [1920, 1080, true], [1366, 768, false, true], [821, 462, true], [390, 844, false], [700, 700, false]]) {
    const p = await b.newPage({ viewport: { width: w, height: h } }); const errs = []; p.on('pageerror', e => errs.push(e.message));
    if (!fresh) await p.addInitScript(sv => { if (!sessionStorage.s) { sessionStorage.s = 1; localStorage.setItem('hearthgate.profile.v1', JSON.stringify(sv)); } }, SAVE);
    await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'));
    await p.evaluate(() => { const H = Hearthgate; for (let k = 0; k < 4 && H.card(); k++) H.act('ok'); if (H.card()) H.act('close'); H.sim(1); }); await p.waitForTimeout(700);
    const r = await p.evaluate(() => { const st = document.getElementById('stage').getBoundingClientRect(); return ['sideL', 'sideR'].map(id => { const e = document.getElementById(id), q = e.getBoundingClientRect();
      return { id, shown: !e.hidden && q.width > 0, over: q.right > st.left && q.left < st.right && !e.hidden, inside: e.hidden || (q.left >= 0 && q.right <= innerWidth && q.top >= 0 && q.bottom <= innerHeight), rows: e.querySelectorAll('.srow, .sline').length }; }); });
    for (const s of r) { if (s.shown !== want) problems.push(`${w}x${h}${fresh ? ' fresh' : ''}: ${s.id} shown=${s.shown}, wanted ${want}`); if (s.over) problems.push(`${w}x${h}: ${s.id} overlaps the stage`); if (!s.inside) problems.push(`${w}x${h}: ${s.id} leaves the window`); if (s.shown && !s.rows) problems.push(`${w}x${h}: ${s.id} is empty`); }
    if (errs.length) problems.push(`${w}x${h}: errors ${errs.join('; ')}`); await p.close();
  }
  console.log(problems.length ? 'desktop panels: ' + problems.join(' | ') : 'desktop panels ok'); await b.close();
})();
