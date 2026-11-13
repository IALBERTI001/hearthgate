// Villagers really walk, and walk where the painting has paving (owner, Nov 13 2026: "I WANT THE WALKING ANIMATION").
// Pins three things a reviewer caught or the owner saw: villagers frozen in place (a route that never got assigned),
// villagers on the flower beds and fountain (the walk ring was too small), and walking legs while standing still.
// usage: node folk-walk.cjs "http://localhost:8765/index.html?qa"
const { chromium } = require('playwright');
(async () => {
  const url = process.argv[2] || 'http://localhost:8765/index.html?qa';
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1920, height: 1080 } });
  const errs = []; p.on('pageerror', e => errs.push(e.message));
  await p.goto(url); await p.waitForFunction(() => !document.getElementById('boot'), null, { timeout: 30000 });
  await p.evaluate(() => { const H = Hearthgate; for (let t = 1; t <= 900; t++) { H.sim(1); if (t % 2 === 0) { H.act('re'); H.act('up'); } let g = 0; while (H.card() && g++ < 5) { H.act('pick'); H.act('ok'); H.act('close'); } } });
  await p.waitForFunction(() => Hearthgate.state().FOLK.length >= 4, null, { timeout: 20000 });
  const r = await p.evaluate(() => {
    const H = Hearthgate, F = H.state().FOLK, [cx0, cy0, RX, RY] = H.view().plaza, dist = F.map(() => 0), prev = F.map(f => [f.x, f.y]);
    let samples = 0, inside = 0, legsStill = 0;
    for (let i = 0; i < 60 * 60; i++) { H.sim(1 / 60);
      F.forEach((f, k) => { const dx = f.x - prev[k][0], dy = f.y - prev[k][1], m = Math.hypot(dx, dy); dist[k] += m; prev[k] = [f.x, f.y];
        if (f.work > 0) return; samples++;
        if (Math.hypot((f.x - cx0) / RX, (f.y - cy0) / RY) < 0.88) inside++;   // the flower beds, steps and basin start inside 0.9
        if (m < 0.01 && f.moving > 0.12 - 1 / 60 + 1e-6 && f.i < f.pts.length) legsStill++; }); }
    return { kinds: F.map(f => f.kind), dist: dist.map(d => Math.round(d)), insidePct: +(inside / Math.max(1, samples) * 100).toFixed(2), legsStill };
  });
  const frozen = r.kinds.filter((k, i) => r.dist[i] < 150);
  const bad = [];
  if (frozen.length) bad.push('frozen villagers: ' + frozen.join(', ') + ' (' + r.dist.join('/') + ' px in 60 s)');
  if (r.insidePct > 1) bad.push(`feet inside the paved walk (flower beds, steps, fountain) ${r.insidePct}% of the time`);
  if (errs.length) bad.push('page errors: ' + errs.join(' | '));
  console.log(bad.length ? 'FOLK WALK PROBLEMS: ' + bad.join('; ') : `folk walk ok (${r.kinds.length} villagers, ${r.dist.join('/')} px in 60 s, ${r.insidePct}% inside)`);
  await b.close(); process.exit(bad.length ? 1 : 0);
})();
