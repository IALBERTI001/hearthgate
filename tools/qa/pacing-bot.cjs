// Pacing bot: a fresh save, a player who checks in every 5 game-seconds and buys the cheaper of Upgrade and Restore,
// levels up any building whose next level costs under a third of that (the city as a side snack, not the main spend),
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
      if (C.pendingFork(P)) H.act('up'); else if (C.canAscend(P)) H.act('re'); else if (P.res >= Math.min(uc, rc)) H.act(rc <= uc ? 're' : 'up');
      if (H.card()) H.act(H.card() === 'ascend' ? 'ok' : 'pick');
      for (let i = 0; i < 40; i++) {
        const B2 = C.bonuses(P), lim = Math.min(C.upgradeCost(P.gateLevel, B2), C.canAscend(P) ? Infinity : C.restoreCost(P.restored.length)) / 3;
        const cheap = P.restored.map(id => [id, C.levelCost(P, id, B2)]).sort((a, b) => a[1] - b[1])[0];
        if (!cheap || cheap[1] > lim || cheap[1] > P.res) break; H.act('level', cheap[0]);
      }
      const B3 = C.bonuses(P), city = C.cityRate(P, B3), fight = C.fightRate(P, B3);
      return { share: city / (city + fight), lvls: P.restored.reduce((s, id) => s + C.levelOf(P, id), 0), w: P.wave, best: P.bestWave, lvl: P.gateLevel, city: P.restored.length, asc: P.ascensions, res: P.res, combos: P.combos.length, forks: Object.keys(P.forks).length };
    });
    const m = (k, v) => { if (!seen[k]) { seen[k] = 1; log.push(`${(t / 60).toFixed(1)} min  ${k}${v ? '  ' + v : ''}`); } };
    if (st.best >= 11) m('first boss beaten (wave 10)'); if (st.best >= 21) m('wave 20 boss beaten'); if (st.best >= 31) m('wave 30 boss beaten');
    if (st.best >= 41) m('wave 40 boss beaten'); if (st.best >= 51) m('wave 50');
    if (st.city >= 3) m('3 buildings'); if (st.city >= 5) m('5 buildings (fountain)'); if (st.city >= 10) m('city whole 10/10');
    if (st.combos >= 3) m('all 3 combos'); if (st.forks >= 1) m('stage 2 fork', 'gate lvl ' + st.lvl); if (st.forks >= 2) m('stage 3 fork', 'gate lvl ' + st.lvl);
    if (st.asc >= 1) m('first Ascension'); if (st.asc >= 2) m('second Ascension');
    if (st.w < (seen.lastW || 0) - 1) falls.push(`${(t / 60).toFixed(1)} min: fell back from ${seen.lastW} to ${st.w}`);
    seen.lastW = st.w;
    if (t % 300 === 0) log.push(`${(t / 60).toFixed(0)} min  wave ${st.w}, gate ${st.lvl}, city ${st.city}, building levels ${st.lvls}, city share of income ${(st.share * 100).toFixed(0)}%`);
  }
  const end = await p.evaluate(() => { const { P } = Hearthgate.state(); return { wave: P.wave, best: P.bestWave, gate: P.gateLevel, city: P.restored.length, asc: P.ascensions }; });
  console.log(log.join('\n')); console.log('fall-backs:', falls.length, falls.slice(0, 8).join(' | ')); console.log('end', JSON.stringify(end), 'errors', errors.length, errors.slice(0, 3));
  await b.close();
})();
