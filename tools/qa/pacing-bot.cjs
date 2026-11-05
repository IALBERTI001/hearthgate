// Pacing bot: a fresh save, a player who checks in every 5 game-seconds and buys the cheaper of Upgrade and Restore,
// picks the first option on every card, and Ascends when it can. Runs the real game sped up (?qa). Prints milestones.
// Usage: node tools/qa/pacing-bot.cjs <url> [minutes]
const { chromium } = require('playwright');
const [url, mins = '60'] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 390, height: 844 } });
  const errors = []; p.on('pageerror', e => errors.push(e.message));
  await p.goto(url + (url.includes('?') ? '&' : '?') + 'qa'); await p.waitForFunction(() => !document.getElementById('boot'));
  const seen = {}, log = [], falls = [];
  for (let t = 0; t < +mins * 60; t += 5) {
    const st = await p.evaluate(() => { const H = Hearthgate; H.sim(5); const { P } = H.state();
      if (H.card()) H.act(H.card() === 'ascend' ? 'ok' : 'pick'); if (H.card()) H.act('ok');
      const C = HGCore, B = C.bonuses(P), uc = C.upgradeCost(P.gateLevel, B), rc = C.canAscend(P) ? 0 : C.restoreCost(P.restored.length);
      if (C.pendingFork(P)) H.act('up'); else if (C.canAscend(P)) H.act('re'); else if (P.parts >= Math.min(uc, rc)) H.act(rc <= uc ? 're' : 'up');
      if (H.card()) H.act(H.card() === 'ascend' ? 'ok' : 'pick');
      return { w: P.wave, best: P.bestWave, lvl: P.gateLevel, city: P.restored.length, asc: P.ascensions, parts: P.parts, combos: P.combos.length, forks: Object.keys(P.forks).length };
    });
    const m = (k, v) => { if (!seen[k]) { seen[k] = 1; log.push(`${(t / 60).toFixed(1)} min  ${k}${v ? '  ' + v : ''}`); } };
    if (st.best >= 11) m('first boss beaten (wave 10)'); if (st.best >= 21) m('wave 20 boss beaten'); if (st.best >= 31) m('wave 30 boss beaten');
    if (st.best >= 41) m('wave 40 boss beaten'); if (st.best >= 51) m('wave 50');
    if (st.city >= 3) m('3 buildings'); if (st.city >= 5) m('5 buildings (fountain)'); if (st.city >= 10) m('city whole 10/10');
    if (st.combos >= 3) m('all 3 combos'); if (st.forks >= 1) m('stage 2 fork', 'gate lvl ' + st.lvl); if (st.forks >= 2) m('stage 3 fork', 'gate lvl ' + st.lvl);
    if (st.asc >= 1) m('first Ascension'); if (st.asc >= 2) m('second Ascension');
    if (st.w < (seen.lastW || 0) - 1) falls.push(`${(t / 60).toFixed(1)} min: fell back from ${seen.lastW} to ${st.w}`);
    seen.lastW = st.w;
  }
  const end = await p.evaluate(() => { const { P } = Hearthgate.state(); return { wave: P.wave, best: P.bestWave, gate: P.gateLevel, city: P.restored.length, asc: P.ascensions }; });
  console.log(log.join('\n')); console.log('fall-backs:', falls.length, falls.slice(0, 8).join(' | ')); console.log('end', JSON.stringify(end), 'errors', errors.length, errors.slice(0, 3));
  await b.close();
})();
