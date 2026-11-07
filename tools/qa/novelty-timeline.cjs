// The 30-second check (a fellow CrazyGames designer's rule, Nov 6 2026): does the player see something NEW about every
// 30 s, and does each stretch between turning points ("levels", even in a levelless game) bring a new core?
// Plays the real game sped up (?qa) like a steady player: buys the cheaper of Upgrade and Restore, levels cheap
// buildings, uses the Surge when ready, picks the first option on every card, Ascends when it can. Samples every 5 s.
// NEW = a first-of-its-kind thing (an enemy type, a building, a combo, an upgrade path, a tower tier, a system, a first
// reward of a kind). BEAT = a repeat payoff worth noticing (a boss, a Feat, a level x2, a restore, an Ascension).
// Usage: node tools/qa/novelty-timeline.cjs <url> [minutes=45]   ->  a timeline, the gaps over 30 s, and a "levels" table
const { chromium } = require('playwright');
const [url, mins = '45'] = process.argv.slice(2);
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 390, height: 844 } });
  const errors = []; p.on('pageerror', e => errors.push(e.message));
  await p.goto(url + (url.includes('?') ? '&' : '?') + 'qa'); await p.waitForFunction(() => !document.getElementById('boot'));
  const ev = [], seen = new Set(), prev = {};
  const add = (t, kind, what) => ev.push({ t, kind, what });
  const once = (t, key, what) => { if (!seen.has(key)) { seen.add(key); add(t, 'NEW', what); return true; } return false; };
  for (let t = 0; t <= +mins * 60; t += 5) {
    const s = await p.evaluate(() => {
      const H = Hearthgate; H.sim(5); const { P, S } = H.state(), C = HGCore;
      for (let k = 0; k < 3 && H.card(); k++) H.act(['ascend', 'daily', 'offline', 'eternal'].includes(H.card()) ? 'ok' : 'pick');
      if (H.card()) H.act('close');
      const B = C.bonuses(P), uc = C.upgradeCost(P.gateLevel, B), rc = C.cityWhole(P) ? Infinity : C.restoreCost(P.restored.length);
      if (C.pendingFork(P)) H.act('up'); else if (C.canAscend(P)) H.act('re'); else if (P.res >= Math.min(uc, rc)) H.act(rc <= uc ? 're' : 'up');
      if (H.card()) H.act(H.card() === 'ascend' ? 'ok' : 'pick');
      for (let i = 0; i < 40; i++) { const B2 = C.bonuses(P), lim = Math.min(C.upgradeCost(P.gateLevel, B2), C.cityWhole(P) ? Infinity : C.restoreCost(P.restored.length)) / 3;
        const ch = P.restored.map(id => [id, C.levelCost(P, id, B2)]).sort((a, b) => a[1] - b[1])[0]; if (!ch || ch[1] > lim || ch[1] > P.res) break; H.act('level', ch[0]); }
      if (!P.firstSession && C.surgeReadyFree(P, Date.now())) Hearthgate.surge();
      if (S.gold) H.act('gold');
      for (const k of C.PERKS) if (P.perkPts >= C.perkCost(P, k.id)) C.buyPerk(P, k.id);
      if (C.unlocked(P, 'relics') && P.relics.length < 2) { C.equipRelic(P, 'ember'); C.equipRelic(P, 'hearth'); }
      return { wave: P.wave, best: P.bestWave, gate: P.gateLevel, stage: C.stageOf(P.gateLevel), restored: P.restored.slice(), combos: P.combos.slice(),
        forks: Object.keys(P.forks).length, roles: [...new Set(S.enemies.map(e => e.role))], shop: P.shop.open, feats: P.feats.length,
        quests: P.shop.open && C.questClaimable(P), surge: C.surgeOn(P, Date.now()), asc: P.ascensions, perks: Object.values(P.perks).reduce((a, b) => a + b, 0),
        event: S.event ? S.event.id : '', gold: S.goldCaught || 0, cheer: S.cheersDone || 0, repairs: S.repairs || 0, trials: S.trialsWon || 0, tower5: P.restored.includes('watchtower'), relics: (P.relics || []).length, rank2: (S.rank2 || []).slice(), seen: (P.seenRoles || []).slice(), stars: (P.stars || []).slice(),
        x2: P.restored.reduce((a, id) => a + C.MILESTONES.filter(m => (P.levels[id] || 1) >= m).length, 0), levels: P.restored.reduce((a, id) => a + (P.levels[id] || 1), 0) };
    });
    const T = t + 5;
    for (const r of s.roles.concat(s.seen)) once(T, 'role:' + r, `new enemy: ${r}`);   // seenRoles catches kinds killed between samples
    if (s.restored.length > (prev.restored || []).length) { const id = s.restored[s.restored.length - 1];
      if (!once(T, 'b:' + id, `new building: ${id}`)) {
        if (s.rank2.includes(id) && once(T, 'r2:' + id, `Ascended building: ${id} (a second ability)`)) {}
        else if (s.asc >= 2 && once(T, 'gild', 'gilded buildings (2nd Ascension)')) {}
        else add(T, 'BEAT', `restored ${id} again (after Ascension)`); } }
    for (const c of s.combos) once(T, 'c:' + c, `new combo: ${c}`);
    if (s.forks > (prev.forks || 0)) once(T, 'fork' + s.forks, `new upgrade path (stage ${s.forks + 1})`);
    if (s.stage > (prev.stage || 1)) once(T, 'stage' + s.stage, `towers grow a tier (stage ${s.stage})`);
    if (s.shop && !prev.shop) once(T, 'shop', 'the Shop opens');
    if (s.quests && !prev.quests) { if (!once(T, 'quest', 'first quest to claim')) add(T, 'BEAT', 'a quest is ready'); }
    if (s.surge && !prev.surge) { if (!once(T, 'surge', 'first Surge')) add(T, 'BEAT', 'Surge'); }
    if (s.feats > (prev.feats || 0)) { if (!once(T, 'feat', 'first Feat')) add(T, 'BEAT', `Feat (${s.feats})`); }
    if (s.x2 > (prev.x2 || 0)) { if (!once(T, 'x2', 'first building x2 milestone')) add(T, 'BEAT', 'building x2 milestone'); }
    if (s.levels > 0 && !(prev.levels > 0)) once(T, 'level', 'first building level-up');
    if (Math.floor((s.best - 1) / 10) > Math.floor(((prev.best || 1) - 1) / 10)) { if (!once(T, 'boss', 'first boss beaten')) add(T, 'BEAT', `boss beaten (wave ${s.best - 1})`); }
    if (s.asc >= 10 && !(prev.asc >= 10)) once(T, 'eternal', 'the Eternal City (10th Ascension)');
    if (s.asc > (prev.asc || 0)) { if (!once(T, 'asc', 'first Ascension')) add(T, 'BEAT', `Ascension ${s.asc}`); }
    if (s.perks > (prev.perks || 0)) once(T, 'perk', 'first perk');
    if (s.event && s.event !== prev.event) { if (!once(T, 'ev:' + s.event, `new wave event: ${s.event}`)) add(T, 'BEAT', `event ${s.event}`); }
    if (s.gold > (prev.gold || 0)) { if (!once(T, 'gold', 'first Golden Wisp caught')) add(T, 'BEAT', 'Golden Wisp'); }
    if (s.cheer > (prev.cheer || 0)) { if (!once(T, 'cheer', 'first Tavern Cheer')) add(T, 'BEAT', 'Cheer'); }
    if (s.repairs > (prev.repairs || 0)) once(T, 'repair', 'villagers repair the gate');
    if (s.tower5 && !prev.tower5) once(T, 'tower5', 'the Watchtower beacon joins the fight');
    if (s.trials > (prev.trials || 0)) { if (!once(T, 'trial', 'first Trial won')) add(T, 'BEAT', 'Trial won'); }
    if (s.relics > (prev.relics || 0)) once(T, 'relic', 'first Relic');
    for (const id of s.stars) once(T, 'star:' + id, `star lit: ${id}`);
    if (s.wave < (prev.wave || 0) - 1) add(T, 'WALL', `fell back from wave ${prev.wave}`);
    Object.assign(prev, s);
  }
  const fmt = t => `${Math.floor(t / 60)}:${String(t % 60).padStart(2, '0')}`;
  const gaps = (list, limit) => { const out = []; let last = 0; for (const e of list) { if (e.t - last > limit) out.push(`${fmt(last)}-${fmt(e.t)} (${e.t - last}s)`); last = e.t; } return out; };
  const news = ev.filter(e => e.kind === 'NEW'), any = ev.filter(e => e.kind !== 'WALL');
  console.log('TIMELINE'); for (const e of ev) console.log(`${fmt(e.t)}  ${e.kind.padEnd(4)}  ${e.what}`);
  console.log(`\nNEW things: ${news.length}; gaps over 30 s with nothing new: ${gaps(news, 30).length}`); console.log(gaps(news, 30).join('\n'));
  console.log(`\nAny payoff (new or beat): gaps over 30 s: ${gaps(any, 30).length}`); console.log(gaps(any, 30).join('\n'));
  // "levels": stretches between turning points (a new building, upgrade path, tower tier, boss, Ascension)
  const turns = ev.filter(e => /new building|upgrade path|tier|boss|Ascension/.test(e.what)).map(e => e.t);
  console.log('\nLEVELS (between turning points): start-end, length, new things inside, longest quiet stretch');
  let a = 0; for (const z of turns.concat(+mins * 60)) { if (z - a < 5) continue; const inside = ev.filter(e => e.t > a && e.t <= z && e.kind !== 'WALL');
    let lg = 0, l = a; for (const e of inside) { lg = Math.max(lg, e.t - l); l = e.t; } lg = Math.max(lg, z - l);
    console.log(`${fmt(a)}-${fmt(z)}  ${z - a}s  new ${inside.filter(e => e.kind === 'NEW').length}, beats ${inside.filter(e => e.kind === 'BEAT').length}, longest quiet ${lg}s`); a = z; }
  console.log('\nerrors', errors.length, errors.slice(0, 3)); await b.close();
})();
