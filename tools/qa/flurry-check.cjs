// A butterfly flurry (Nov 6 2026) comes after the first catch, all five can be caught, the last pays a bonus, no errors.
// usage: node tools/qa/flurry-check.cjs "http://localhost:8765/index.html?qa"
const { chromium } = require('playwright');
(async () => { const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 390, height: 844 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message)); p.on('console', m => m.type() === 'error' && errs.push(m.text()));
  await p.goto(process.argv[2]); await p.waitForFunction(() => !document.getElementById('boot'));
  const r = await p.evaluate(() => { const H = Hearthgate;
    for (let t = 1; t <= 900; t++) { H.sim(1); if (t % 2 === 0) { H.act('re'); H.act('up'); } while (H.card()) { H.act('pick'); H.act('ok'); H.act('close'); if (H.card()) break; }
      if (t % 5 === 0 && !H.flurry().list.length) H.act('fly');
      const F = H.flurry(); if (F.list.length && F.list.filter(b => b.x > 30 && b.x < 690).length >= 1) {
        const P = H.state().P, r0 = P.res; for (let i = 0; i < 40 && F.got < 5; i++) { H.act('fly'); H.sim(0.25); }
        return { t, got: F.got, gain: P.res - r0 }; } }
    return { t: -1 }; });
  const ok = r.t > 0 && r.got === 5 && r.gain > 0 && !errs.length;
  console.log(ok ? `flurry ok (at ${r.t} s, gain ${Math.round(r.gain)})` : `FLURRY PROBLEM ${JSON.stringify(r)} ${errs.join(' | ')}`); await b.close(); process.exit(ok ? 0 : 1); })();
