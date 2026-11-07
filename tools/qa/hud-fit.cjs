// Polish gate (owner, Nov 6 2026: "the wave is 202 but we see 20"): with the biggest numbers the game can reach, every
// HUD text sits fully inside its own column at the smallest and the widest window sizes, and no two HUD items overlap.
// usage: node tools/qa/hud-fit.cjs "http://localhost:8765/index.html?qa" [shotDir]
const { chromium } = require('playwright');
const SIZES = [[360, 740], [390, 844], [821, 462], [1085, 990], [1920, 1080]];
(async () => { const b = await chromium.launch(); let bad = 0;
  for (const [w, h] of SIZES) {
    const p = await b.newPage({ viewport: { width: w, height: h } });
    await p.goto(process.argv[2]); await p.waitForFunction(() => !document.getElementById('boot'));
    for (const stage of ['first', 'late']) {
      const r = await p.evaluate(async st => { const { P } = Hearthgate.state();
        if (st === 'late') { P.firstSession = false; P.wave = 9999; P.runWave = 9999; P.res = 9.99e62; P.restored = [...Hearthgate.state().P.restored, 'forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower'].filter((v, i, a) => a.indexOf(v) === i);
          P.ascensions = 61; for (const id of P.restored) P.levels[id] = 400; if (Hearthgate.refresh) Hearthgate.refresh(); C_SURGE: { const now = Date.now(); P.surge.until = now + 60e3; } }
        while (Hearthgate.card()) { Hearthgate.act('ok'); Hearthgate.act('close'); if (Hearthgate.card()) break; }
        await new Promise(r => setTimeout(r, 400));
        const out = [], q = s => [...document.querySelectorAll(s)].filter(e => !e.hidden && e.offsetParent);
        for (const e of q('#hud .stat b, #hud .stat .sub, #counters .stat, #hud .lbl')) { const box = e.closest('.stat'), col = box.parentElement === document.getElementById('hud') ? box : box;
          const r1 = e.getBoundingClientRect(), r2 = col.getBoundingClientRect();
          if (e.scrollWidth > e.clientWidth + 1 && getComputedStyle(e).overflow !== 'visible') out.push(`${e.id || e.className} clipped`);
          if (r1.right > r2.right + 2 || r1.left < r2.left - 2) out.push(`${e.id || e.className} spills out of its column (${Math.round(r1.left)}-${Math.round(r1.right)} vs ${Math.round(r2.left)}-${Math.round(r2.right)})`); }
        const items = q('#hud > .stat, #hud .btns > button, #counters > .stat').map(e => [e.id || e.className, e.getBoundingClientRect()]);
        for (let i = 0; i < items.length; i++) for (let j = i + 1; j < items.length; j++) { const [a, A] = items[i], [c, B] = items[j];
          if (A.left < B.right - 1 && B.left < A.right - 1 && A.top < B.bottom - 1 && B.top < A.bottom - 1) out.push(`${a} overlaps ${c}`); }
        const st0 = document.getElementById('stage').getBoundingClientRect();
        for (const [n, R] of items) if (R.left < st0.left - 1 || R.right > st0.right + 1) out.push(`${n} leaves the stage`);
        return out; }, stage);
      if (process.argv[3]) await p.screenshot({ path: `${process.argv[3]}/hudfit-${w}x${h}-${stage}.png` });
      if (r.length) { bad++; console.log(`${w}x${h} ${stage}: ${r.join('; ')}`); }
    }
    await p.close();
  }
  console.log(bad ? `HUD FIT PROBLEMS in ${bad} views` : `hud fit ok (${SIZES.length} sizes, first and late game)`); await b.close(); process.exit(bad ? 1 : 0); })();
