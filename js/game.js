// Hearthgate: hold the gate, and the city rebuilds itself behind you.
// Portrait stage 720x1280: the field on top, the city's north wall with its gatehouse and four tower platforms, the city,
// then Upgrade and Restore. CrazyGames' smallest desktop frame (800x450) shows the stage at 0.35, so every label is at
// least 30 design px. The simulation runs on a fixed 60 Hz step and never pauses on a tab switch; only the player pauses.
(function (root) {
  'use strict';
  const C = root.HGCore, A = root.HGAudio || { play() {}, music() {}, unlock() {}, set() {}, boss() {} };
  const SDK = root.GameSDK || { gameplay() {}, loadingStop() {}, happytime() {}, rewarded: () => Promise.resolve(false), rewardsOn: false };
  const W = 720, H = Math.round(Math.max(1280, Math.min(1560, 720 * root.innerHeight / Math.max(1, root.innerWidth)))), DT = 1 / 60, SAVE_KEY = 'hearthgate.profile.v1', BUILD = '2026-11-06q';

  // ---------- layout from the city painting (art/masters/city-north-gate, cropped at exactly half scale) ----------
  const CITY = C.CITY, CITY_H = CITY.band[1], CITY_Y = H - 168 - CITY_H, K = CITY.band[0] / CITY.crop[2];
  const sx = x => (x - CITY.crop[0]) * K, sy = y => CITY_Y + (y - CITY.crop[1]) * K;
  const GATE_X = sx(CITY.gate[0]), WALL_FRONT = sy(CITY.wallTop);          // enemies press against the wall here
  const PLATFORMS = CITY.platforms.map(([x, y]) => ({ x: sx(x), y: sy(y) })); // the four round platforms in the wall
  const FIELD_TOP = 250;   // nothing that moves or floats goes above this line: the HUD lives there
  const RANGE_Y = 196;   // towers reach the whole field below the treeline, on every screen height
  const ELEMENT = { forge: 'fire', frostSpire: 'frost', stormBell: 'storm' };
  const COLOR = { fire: '#ff8a3a', frost: '#9fe8ff', storm: '#c98bff', earth: '#e0b062', none: '#efe2c0' };
  const L = root.HGLooks, SHARD = '#6ff2ff';   // Starshards: the Shop's currency, a bright cyan star
  const RES = '#ff8ae6', RES_GLOW = 'rgba(255,110,220,';   // Resonance: the city's magic, rose-pink so it reads on green grass
  const PLAZA = { x: sx(CITY.focus[0]), y: sy(CITY.focus[1]) };   // the fountain every wisp flies into
  const GLOW = { fire: 'rgba(255,140,50,', frost: 'rgba(140,220,255,', storm: 'rgba(200,140,255,', earth: 'rgba(225,175,90,', none: 'rgba(255,240,200,' };

  // ---------- state ----------
  let P = load(), B = C.bonuses(P);
  const S = { t: 0, enemies: [], shots: [], pops: [], fx: [], hits: [], wisps: [], motes: [], spawnQ: [], spawnT: 0, gateHp: 0, gateHit: 0, paused: false,
    sessionT: 0, saveT: 0, reveal: null, card: null, rings: [], shake: 0, wavePop: null, cheerNext: null, savedByAd: false };
  function load() { try { return C.sanitize(localStorage.getItem(SAVE_KEY)); } catch (e) { return C.defaults(); } }
  function save() { P.lastSeen = Date.now(); try { localStorage.setItem(SAVE_KEY, JSON.stringify(P)); } catch (e) {} }
  let RATE = C.cityRate(P, B);   // the city's Resonance per second, recomputed whenever the city or a bonus changes
  // today's Omen joins the bonuses from the second session on (the opening stays as tested)
  const refresh = () => { P.omen = P.firstSession ? null : C.omenFor(C.dayKey(new Date())).id; B = C.bonuses(P); RATE = C.cityRate(P, B); };
  refresh();
  const rfmt = r => r < 10 ? String(+r.toFixed(r < 1 ? 2 : 1)) : C.fmt(r);
  root.Hearthgate = { reloadSave() { P = load(); refresh(); S.enemies = []; S.wisps = []; startWave(); }, state: () => ({ P, S, B }) };
  // QA only (?qa in the address): run the simulation fast and drive the buttons, for the pacing bot in tools/qa.
  if (/[?&]qa\b/.test(location.search)) Object.assign(root.Hearthgate, {
    sim(sec) { for (let i = 0; i < sec * 60; i++) if (!S.paused) step(); },
    act(what, id) { if (what === 'up') upgrade(); if (what === 're') restore(); if (what === 'pick') { const o = document.querySelector('#card .opt' + (id ? `[data-id="${id}"]` : '')); if (o) o.click(); } if (what === 'ok') { const o = document.querySelector('#card #c-ok'); if (o) o.click(); } if (what === 'level') levelUp(id); if (what === 'gold' && S.gold) tapGold(S.gold.x, S.gold.y); if (what === 'card') showCard(id.kind, id.data); if (what === 'shop') openShopCard(); if (what === 'buy') { const b = document.querySelector(`#card .buy[data-id="${id}"]`); if (b) b.click(); } if (what === 'tab') { const b = document.querySelector(`#card .tab[data-tab="${id}"]`); if (b) b.click(); } if (what === 'close') { const o = document.querySelector('#card #c-no'); if (o) o.click(); } },
    card: () => S.card
  });

  const gateMax = () => C.gateMax(P, B);
  const has = id => P.restored.includes(id);
  const evIs = id => !!S.event && (S.event.id === id || (!!S.event.ids && S.event.ids.includes(id)));   // twin events carry two ids
  // the Earth element joins the towers from the 4th Ascension on (Nov 6 2026)
  const elements = () => { const e = P.restored.map(b => ELEMENT[b]).filter(Boolean); if (e.length && C.unlocked(P, 'earth')) e.push('earth'); return e; };
  const reduced = () => P.settings.reduced || matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---------- waves ----------
  const CHEERS = ['Keep it up!', 'Great job!', 'WOOOAHH!', 'Unstoppable!', 'Hold the line!', 'Nice work!', 'You got this!',
    'Crushing it!', 'On fire!', 'Legendary!', 'The city cheers you on!', 'Brick by brick!'];
  function startWave() {
    const n = P.wave, boss = C.isBoss(n); S.trial = C.isTrial(P, n); S.auroraN = 0;
    S.event = C.eventFor(P, n); const evNew = S.event && !P.seenEvents.includes(S.event.id); if (S.event && evNew) P.seenEvents.push(S.event.id);
    const cheer = S.cheerNext || (n === 1 ? ['Good luck!', 'How far will you go?'][Math.floor(Math.random() * 2)]
      : boss ? 'Boss incoming. Hold the gate!' : n % 50 === 0 ? 'HALFWAY TO GLORY!' : CHEERS[(n * 7 + 3) % CHEERS.length]);
    S.cheerNext = null;
    const mix = C.eventMix(C.waveMix(n, P.ascensions), S.event);
    // the first wave of each session (after the first) names today's Omen
    const om = !S.omenTold && P.omen && C.OMENS.find(o => o.id === P.omen); if (om) S.omenTold = true;
    S.wavePop = { n, cheer: om ? `Today: ${om.name}! ${om.line}` : S.trial ? 'TRIAL! Win it for Starshards' : S.event ? `${evNew ? 'NEW! ' : ''}${S.event.name}: ${S.event.line}` : cheer, big: boss || S.trial || !!S.event, boss, t: 0, roles: mix.map(([r]) => r) };
    A.boss(boss);   // boss waves get the battle theme; every other wave the calm playlist
    if (boss) { S.bossWarn = 0; S.shake = 0.35; A.play('boss_horn', 0.6); }   // a boss wave is an event: red edges, a rumble
    S.gateHp = S.gateHp > 0 ? Math.min(S.gateHp, gateMax()) : gateMax(); if (S.event && evIs('laststand')) S.gateHp = Math.min(S.gateHp, gateMax() * 0.5);
    // a shuffled queue, so runners and armoured arrive among the slimes rather than in blocks; the boss comes last
    const q = []; for (const [role, c] of mix) for (let i = 0; i < c; i++) q.push(role);
    for (let i = q.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [q[i], q[j]] = [q[j], q[i]]; }
    S.spawnQ = q.sort((a, b) => C.isBossRole(a) - C.isBossRole(b)); S.spawnT = 1.0;
  }
  function spawn(role, at) {
    const R = C.ROLES[role], hp = C.enemyHp(P.wave, role) * (S.trial ? 1.6 : 1) * (S.event && evIs('bloodmoon') ? 1.5 : 1) * (C.isBossRole(role) ? 1 + 0.5 * bossRank() : 1) * ((S.event && evIs('giants')) && !C.isBossRole(role) ? 2 : 1);
    if (R.tip && !P.seenRoles.includes(role)) { P.seenRoles.push(role); setTimeout(() => toast(`New: ${R.label}! ${R.tip}`), 400); }
    // enemies step out of the long grass below the HUD with a puff of dust, never from under the top panel
    const lead = (S.packN || 0) % 3 === 0 || C.isBossRole(role) || role === 'swarm';
    let x0 = lead ? (S.packX = 80 + Math.random() * 560) : Math.max(70, Math.min(650, S.packX + (Math.random() - 0.5) * 140)), y0 = FIELD_TOP + (R.size > 150 ? 70 : 0) + Math.random() * 40;
    if (at) { x0 = Math.max(60, Math.min(660, at[0])); y0 = Math.min(WALL_FRONT - 4, at[1]); }
    for (let i = 0; i < (reduced() ? 3 : 9); i++) { const a = Math.random() * Math.PI * 2; S.fx.push({ x: x0 + Math.cos(a) * 20, y: y0 - 6, vx: Math.cos(a) * 50, vy: -20 - Math.random() * 30, t: 0, life: 0.5, col: 'rgba(220,200,150,.9)', r: 4 + Math.random() * 4, dust: true }); }
    S.enemies.push({ role, sheet: R.sheet, x: x0, y: y0, off: (Math.random() - 0.5) * 330, hp, max: hp,
      sp: R.speed * (WALL_FRONT - FIELD_TOP) / 414 * (S.event && (evIs('stampede') || evIs('eclipse') || evIs('frenzy')) ? 1.3 : S.event && evIs('blizzard') ? 0.75 : 1), size: R.size * ((S.event && evIs('giants')) && !C.isBossRole(role) ? 1.3 : 1), armour: R.armour || 0, weak: R.weak, boss: C.isBossRole(role), frame: Math.random() * 6,
      chill: (S.event && evIs('winter')) ? 4 : 0, burn: (S.event && evIs('embers')) ? 4 : 0, flash: 0, born: S.t - (at ? 0.6 : 0), dying: 0, stun: 0, summonT: R.summon || 0, totemT: R.totem || 0, blinkT: Math.random() * 2.4, tint: S.event && evIs('golden') ? 'gold' : R.tint, shield: !!R.shield || !!(S.event && evIs('mirror')) });
  }

  // ---------- simulation ----------
  const TOWERS4 = PLATFORMS.map((p, i) => ({ x: p.x, y: p.y, cd: 0.2 + i * 0.13, recoil: 0, flash: 0, n: i }));
  // the fifth tower is the Watchtower's own beacon: a light on its roof that fires over the wall (owner, Nov 6 2026: a
  // tower floating on the gate arch did not look natural)
  const WT = CITY.spots.watchtower, TOWER5 = { x: sx(WT[0]) + WT[2] * K * 0.02, y: sy(WT[1]) - WT[3] * K * 0.43, cd: 0.4, recoil: 0, flash: 0, n: 4, gate: true, beacon: true };
  let TOWERS = TOWERS4.slice();
  const syncTowers = () => { TOWERS = has('watchtower') ? TOWERS4.concat(TOWER5) : TOWERS4.slice(); };
  function step() {
    if ((S.featT = (S.featT || 0) + DT) > 0.5) { S.featT = 0; checkFeats(); }
    if (S.bossWarn !== undefined && (S.bossWarn += DT) > 2.6) S.bossWarn = undefined;
    if (S.medal && (S.medal.t += DT) > 2.25) S.medal = null;
    S.t += DT; S.sessionT += DT; if (S.growTip) S.growTipT += DT; if (S.flash && (S.flash.t += DT) > 0.18) S.flash = null; if (S.wavePop) S.wavePop.t += DT;
    S.gateHit = Math.max(0, S.gateHit - DT * 4); S.shake = Math.max(0, S.shake - DT);
    for (const r of S.rings) r.t += DT; S.rings = S.rings.filter(r => r.t < 0.5);
    for (const h of S.hits) h.t += DT; S.hits = S.hits.filter(h => h.t < L.HIT_LIFE);
    if (S.spawnQ.length && (S.spawnT -= DT) <= 0) { const r = S.spawnQ.shift(); spawn(r); S.packN = (S.packN || 0) + 1; const gap = Math.max(0.45, 1.35 - P.wave * 0.02);
      S.spawnT = (r === 'swarm' ? 0.28 : S.packN % 3 ? 0.3 : gap * 3 - 0.6) * ((S.event && evIs('rush')) ? 0.5 : 1); }   // packs of three, same total pace
    if (B.regen) S.gateHp = Math.min(gateMax(), S.gateHp + gateMax() * B.regen * DT);
    syncTowers();
    const els = elements(), interval = C.fireInterval(B) * (S.cheerT > 0 ? 0.5 : 1) * (S.event && evIs('frenzy') ? 0.7 : 1);   // the Tavern's Cheer: towers fire twice as fast
    tickExtras();
    if ((S.event && (evIs('meteor') || evIs('starstorm'))) && (S.meteorT = (S.meteorT || 2) - DT) <= 0) { S.meteorT = 2.5;
      const live = S.enemies.filter(e => !e.dying && e.y > FIELD_TOP + 40); const tg = live[Math.floor(Math.random() * live.length)];
      if (tg) { burst(tg, '#ffb36b', 24, 260); S.shake = 0.18; S.rings.push({ x: tg.x, y: tg.y - 20, t: 0, col: '#ffb36b' }); pop(tg.x, tg.y - tg.size - 30, 'METEOR!', '#ffb36b', 0.9, 40); A.play('shatter_combo', 0.3);
        S.enemies.filter(o => !o.dying && Math.hypot(o.x - tg.x, o.y - tg.y) < 100).forEach(o => { o.hp -= C.towerDamage(P, B) * 6; o.flash = 1; o.burn = 2; }); } }
    for (const t of TOWERS) {
      t.recoil = Math.max(0, t.recoil - DT * 6); t.flash = Math.max(0, t.flash - DT * 5);
      if ((t.cd -= DT) > 0) continue;
      // each tower takes one of the two enemies nearest the gate
      const live = S.enemies.filter(e => !e.dying && e.y > RANGE_Y).sort((a, b) => b.y - a.y);
      if (!live.length) continue;
      const tgt = live[Math.min(live.length - 1, t.n % 2)];
      t.cd = interval * (t.gate && B.beacon ? 0.5 : 1); t.recoil = 1; t.flash = 1; t.shots = (t.shots || 0) + 1;
      const el = els.length ? els[(t.shots + t.n) % els.length] : 'none';
      S.shots.push({ x: t.x, y: t.beacon ? t.y : t.y - 70, tx: tgt, el, sp: t.beacon ? 1000 : 820, trail: [] });
      A.play(el === 'frost' ? 'ice_launch_0' : el === 'storm' ? 'lightning_launch_0' : 'fire_launch_0', 0.16);
    }
    for (const s of S.shots) {
      const tx = s.tx.x, ty = s.tx.y - s.tx.size * 0.45, dx = tx - s.x, dy = ty - s.y, L = Math.hypot(dx, dy);
      s.trail.push([s.x, s.y]); if (s.trail.length > 7) s.trail.shift();
      if (L < 16 || s.tx.dying) { s.done = true; if (!s.tx.dying) hit(s.tx, s.el); continue; }
      s.x += dx / L * s.sp * DT; s.y += dy / L * s.sp * DT;
    }
    S.shots = S.shots.filter(s => !s.done);
    for (const e of S.enemies) {
      if (e.dying) { e.dying += DT; continue; }
      if (e.burn > 0) { e.burn -= DT; e.hp -= C.towerDamage(P, B) * (1 + B.fire) * 0.5 * DT * (e.weak === 'fire' ? 2 : 1); }
      e.chill = Math.max(0, e.chill - DT); e.flash = Math.max(0, e.flash - DT * 6); e.stun = Math.max(0, (e.stun || 0) - DT);
      const RR = C.ROLES[e.role];
      if (RR.heal && !(e.burn > 0)) for (const o of S.enemies) if (o !== e && !o.dying && o.hp < o.max && Math.hypot(o.x - e.x, o.y - e.y) < 130) { o.hp = Math.min(o.max, o.hp + o.max * RR.heal * DT); o.healed = 0.3; }
      if (RR.summon && (e.summonT -= DT) <= 0) { e.summonT = RR.summon; const sy0 = RR.summonRole ? Math.min(e.y, WALL_FRONT) - 150 : e.y; spawnAt(RR.summonRole || 'runner', e.x + 50, sy0); spawnAt(RR.summonRole || 'runner', e.x - 50, sy0); }   // thieves start well up the field so they can be stopped
      if (e.healed) e.healed = Math.max(0, e.healed - DT);
      if (RR.blink) { e.blinkT = (e.blinkT + DT) % 2.4; e.faded = e.blinkT > 1.4; }   // Phantoms: 1.4 s seen, 1 s faded
      if (RR.totem && (e.totemT -= DT) <= 0) { e.totemT = RR.totem; S.rings.push({ x: e.x, y: e.y - e.size * 0.45, t: 0, col: '#bfe8ff' });
        S.enemies.filter(o => o !== e && !o.dying && !o.shield && !o.boss && Math.hypot(o.x - e.x, o.y - e.y) < 160).slice(0, 3).forEach(o => { o.shield = true; }); }
      const slow = e.stun > 0 ? 0 : e.chill > 0 ? (e.weak === 'frost' ? 0.3 : 0.45) : 1;
      e.frame += DT * 8 * slow;
      if (e.y < WALL_FRONT) {
        e.y = Math.min(WALL_FRONT, e.y + e.sp * slow * DT);
        if (e.y > 230) e.x += (GATE_X + e.off - e.x) * Math.min(1, 0.8 * DT);
      } else if (RR.thief) { const amt = Math.min(P.res * 0.5, (C.fightRate(P, B) * 1.6 + RATE) * 6); P.res -= amt; e.dying = DT; S.thefts = (S.thefts || 0) + 1;
        pop(e.x, WALL_FRONT - 90, `Stolen -${C.fmt(amt)}`, '#ff8a6a', 1.3, 36); continue; }
      else if (RR.bomb) { S.gateHp -= C.enemyBite(P.wave, e.role) * 10; S.gateHit = 1; S.shake = 0.3; e.dying = DT; burst(e, '#ff9a3a', 26, 280); pop(e.x, WALL_FRONT - 90, 'BOOM!', '#ff9a3a', 1.1, 44); continue; }
      else { S.gateHp -= C.enemyBite(P.wave, e.role) * DT; S.gateHit = 1; }
      if (e.hp <= 0) kill(e);
    }
    S.enemies = S.enemies.filter(e => !e.dying || e.dying < 0.45);
    for (const p of S.pops) p.t += DT; S.pops = S.pops.filter(p => p.t < p.life);
    for (const p of S.fx) { p.t += DT; p.x += p.vx * DT; p.y += p.vy * DT; p.vy += (p.g || 0) * DT; }
    S.fx = S.fx.filter(p => p.t < p.life);
    // the city hums: Resonance every tick, and a mote now and then rising from each restored building
    P.res += RATE * surgeX() * DT; S.plazaGlow = Math.max(0, (S.plazaGlow || 0) - DT * 2.5);
    for (const id of P.restored) if (Math.random() < DT * (reduced() ? 0.25 : 0.9)) {
      const sp = CITY.spots[id]; S.motes.push({ x: sx(sp[0]) + (Math.random() - 0.5) * sp[2] * K * 0.7, y: sy(sp[1]) + (Math.random() - 0.3) * sp[3] * K * 0.5, t: 0, life: 1.6, r: 2 + Math.random() * 2 });
    }
    for (const m of S.motes) { m.t += DT; m.y -= 26 * DT; } S.motes = S.motes.filter(m => m.t < m.life);
    // wisps: each kill's Resonance flies over the wall into the fountain, and only counts when it lands
    for (const w of S.wisps) {
      w.t += DT; const u = Math.min(1, Math.pow(w.t / w.dur, 1.7)), v = 1 - u;
      w.x = v * v * w.x0 + 2 * v * u * w.cx + u * u * PLAZA.x; w.y = v * v * w.y0 + 2 * v * u * w.cy + u * u * PLAZA.y;
      w.trail.push([w.x, w.y]); if (w.trail.length > 10) w.trail.shift();
      if (u >= 1) { w.done = true; P.res += w.amt; S.plazaGlow = 1; }
    }
    S.wisps = S.wisps.filter(w => !w.done);
    if (S.gateHp <= 0) gateBroken();
    else if (!S.spawnQ.length && !S.enemies.length) waveCleared();
    if ((S.saveT += DT) > 5) { S.saveT = 0; save(); }
    if ((S.sideT = (S.sideT || 0) + DT) > 0.5) { S.sideT = 0; drawSides(); }
  }

  function hit(e, el) {
    let d = C.towerDamage(P, B) * (1 + (B[el] || 0)) * (e.boss ? 1 + B.bossDmg : 1) * (S.event && ((evIs('thunder') && el === 'storm') || (evIs('blizzard') && el === 'frost') || (evIs('inferno') && el === 'fire') || (evIs('rockslide') && el === 'earth')) ? 2 : 1) * (S.event && evIs('hail') && (el === 'frost' || el === 'storm') ? 1.5 : 1) * ((S.event && evIs('stormfront')) && el === 'storm' ? 1.5 : 1);
    if (e.faded && el !== 'storm') d *= 0.25;   // a faded Phantom: only Storm finds it
    const comboMul = (1 + B.combo) * (S.event && evIs('eclipse') ? 2 : 1);
    if (e.shield) { if (el === 'storm') { e.shield = false; burst(e, '#bfe8ff', 16, 220); pop(e.x, e.y - e.size - 30, 'SHIELD BROKEN', '#bfe8ff', 1, 30); } else d *= 0.2; }
    if (el === 'earth') { e.stun = S.event && evIs('tremor') ? 1.2 : 0.6; if (e.chill > 0) { d *= 2.5 * comboMul; combo('quake', e, 'QUAKE!', COLOR.earth); S.shake = 0.25; e.bigHit = true;
      S.enemies.filter(o => o !== e && !o.dying && Math.hypot(o.x - e.x, o.y - e.y) < 90).forEach(o => { o.hp -= d * 0.5; o.flash = 1; o.stun = 0.4; }); } }
    if (e.armour && el !== 'fire') d *= 1 - e.armour;                 // armour: only Fire bites fully
    if (el === 'fire') {
      if (e.chill > 0) { d *= 3 * comboMul; combo('shatter', e, 'SHATTER!', COLOR.frost); burst(e, COLOR.frost, 22, 260); A.play('shatter_combo', 0.5); e.bigHit = true; }
      e.burn = 2.2;
    }
    if (el === 'frost') { e.chill = B.deepFrost ? 3.3 : 2.2; if (evIs('avalanche') && !e.boss) e.stun = Math.max(e.stun || 0, 0.8); if (e.weak === 'frost') d *= 1.3; }
    if (el === 'storm') {
      const jumps = (e.chill > 0 ? 4 : 2) + B.chainPlus + ((S.event && evIs('stormfront')) ? 3 : 0);
      if (e.chill > 0) { combo('frostStorm', e, 'SUPERCHAIN!', COLOR.storm); A.play('conduction_combo', 0.45); if (C.ROLES[e.role].comboWeak) e.bigHit = true; }
      if (e.burn > 0) { combo('fireStorm', e, 'FIRESTORM!', COLOR.fire); A.play('freeze_combo', 0.45); }
      if (S.event && evIs('gale') && !e.boss) e.y = Math.max(FIELD_TOP, e.y - 30);
      const chainD = d * 0.6 * (e.chill > 0 || e.burn > 0 ? comboMul : 1) * (e.weak === 'storm' ? 2 : 1);
      S.enemies.filter(o => o !== e && !o.dying).sort((a, b) => Math.hypot(a.x - e.x, a.y - e.y) - Math.hypot(b.x - e.x, b.y - e.y))
        .slice(0, jumps).forEach(o => { o.hp -= chainD; o.flash = 1; if (e.burn > 0) o.burn = 2; S.pops.push({ arc: [e.x, e.y - e.size * 0.45, o.x, o.y - o.size * 0.45], t: 0, life: 0.2 }); });
    }
    if (C.ROLES[e.role].comboWeak && e.bigHit) d *= C.ROLES[e.role].comboWeak;
    e.hp -= d; e.flash = 1;
    const hs = P.shop.equip.hit;
    burst(e, COLOR[el], el === 'none' ? 4 : hs === 'hit-classic' ? 6 : 3, 120);
    if (hs !== 'hit-classic' && S.hits.length < 30) S.hits.push({ x: e.x, y: e.y - e.size * 0.45, t: 0, col: COLOR[el], style: hs, seed: Math.random() * 6 });
    A.play(el === 'frost' ? 'ice_contact_0' : el === 'storm' ? 'lightning_contact_0' : el === 'fire' || el === 'earth' ? 'fire_contact_0' : 'ice_contact_0', 0.12);
    if (e.dp && e.dp.t < 0.25 && S.pops.includes(e.dp) && !e.bigHit) { e.dp.val += d; e.dp.text = C.fmt(e.dp.val); }
    else { e.dp = pop(e.x, e.y - e.size * 0.55, C.fmt(d), e.bigHit ? '#ffffff' : COLOR[el], e.bigHit ? 0.9 : 0.7, e.bigHit ? 56 : 30); e.dp.val = d; }
    e.bigHit = false;
  }
  function combo(id, e, word, col) {
    quest('combo'); if (id === 'shatter') quest('shatter');
    if (has('tavern') && !(S.cheerT > 0)) { S.cheer = Math.min(1, (S.cheer || 0) + 0.06 * (S.event && evIs('feast') ? 2 : 1)); if (S.cheer >= 1) startCheer(); }
    // one combo word on screen at a time, so words never pile into each other
    // above the enemy, or below it near the treeline, so it never sits on the +N
    S.pops = S.pops.filter(p => !p.combo);
    S.pops.push({ x: Math.max(150, Math.min(570, e.x)), y: (e.y - e.size - 80 < (S.wavePop ? 500 : FIELD_TOP + 50)) ? Math.max(e.y + 70, S.wavePop ? 500 : 0) : e.y - e.size - 80, text: word, col, t: 0, life: 1.4, size: 58, combo: true, hold: 0.75 });
    S.rings.push({ x: e.x, y: e.y - e.size * 0.45, t: 0, col }); S.shake = 0.14;
    if ((S.event && (evIs('aurora') || evIs('starstorm'))) && P.shop.open && (S.auroraN || 0) < 8 && Math.random() < 0.35) { S.auroraN = (S.auroraN || 0) + 1; P.shop.shards += 1; pop(e.x, e.y - e.size - 40, '+1 Starshard', SHARD, 1.1, 32); }
    if (id === 'shatter' && S.t - (S.lastFlash || -9) > 1.2) { S.flash = { col, t: 0 }; S.lastFlash = S.t; }   // the signature combo lights the screen
    if (!P.combos.includes(id)) { P.combos.push(id); if (P.shop.open) P.shop.shards += C.SHARDS.combo; toast(`New combo: ${word.replace('!', '')}  ·  ${P.combos.length}/${comboTotal()}`); save(); }
  }
  function kill(e) {
    e.dying = DT; const gain = C.killRes(P.wave, e.boss, B) * surgeX() * (e.boss ? 1 + bossRank() : 1) * (S.event && evIs('golden') ? 2 : S.event && evIs('bloodmoon') ? 3 : 1) * ((S.event && evIs('giants')) ? 2 : (S.event && evIs('laststand')) ? 3 : 1); quest('kills');
    const R = C.ROLES[e.role];
    if ((S.event && evIs('echo')) && !e.boss && !e.echo && e.role !== 'swarm') { spawnAt('swarm', e.x, e.y - 4); S.enemies[S.enemies.length - 1].echo = true; }
    if (R.split) for (let k = 0; k < R.split; k++) spawnAt('swarm', e.x + (k - (R.split - 1) / 2) * 34, e.y - 4);
    S.kills = (S.kills || 0) + 1;
    if (B.molten && e.burn > 0 && !e.boss) { S.molten = (S.molten || 0) + 1; burst(e, COLOR.fire, 14, 200);
      S.enemies.filter(o => o !== e && !o.dying && Math.hypot(o.x - e.x, o.y - e.y) < 110).forEach(o => { o.burn = 2.2; o.hp -= C.towerDamage(P, B) * 0.6; o.flash = 1; }); }
    if (R.bomb) { S.shake = 0.2; burst(e, '#ff9a3a', 26, 280); pop(e.x, e.y - e.size - 30, 'BOOM!', '#ff9a3a', 1, 44); A.play('shatter_combo', 0.4);
      S.enemies.filter(o => o !== e && !o.dying && Math.hypot(o.x - e.x, o.y - e.y) < 150).forEach(o => { o.hp -= C.towerDamage(P, B) * 4; o.flash = 1; }); }
    if (R.chest || (S.event && evIs('jackpot') && S.kills % 5 === 0)) { const amt = C.fightRate(P, B) * 1.6 * 5 * surgeX() * (1 + B.chestX); P.res += amt; pop(e.x, e.y - e.size - 40, `Treasure! +${C.fmt(amt)}`, '#ffd56b', 1.3, 40);
      if (P.shop.open && Math.random() < 0.3) { P.shop.shards += 1; pop(e.x, e.y - e.size - 80, '+1 Starshard', SHARD, 1.3, 34); } A.play('buy', 0.45); }
    wisp(e, gain); pop(e.x + 28, e.y - e.size * 0.2, '+' + C.fmt(gain), '#ff9ce8', 0.9, 34);
    burst(e, e.sheet === 'golem' ? '#b9b2a2' : e.sheet === 'beetle' ? '#ff9a4a' : '#8fe05a', e.boss ? 30 : 12, e.boss ? 260 : 160);
    if (e.boss || e.role === 'armoured' || e.role === 'ironhide') A.play('golem_death', 0.5);
  }
  function wisp(e, amt) {
    if (S.wisps.length > 40) { P.res += amt; S.plazaGlow = 1; return; }   // a huge swarm: pay at once rather than flood the sky
    const x0 = e.x, y0 = e.y - e.size * 0.5, side = x0 < PLAZA.x ? -1 : 1;
    S.wisps.push({ x0, y0, x: x0, y: y0, cx: (x0 + PLAZA.x) / 2 + side * (50 + Math.random() * 90), cy: Math.min(y0, WALL_FRONT) - 70 - Math.random() * 60,
      t: 0, dur: 0.75 + Math.random() * 0.25, amt, trail: [] });
  }
  // what is still in the air is paid before the page hides or the game saves for good
  function flushWisps() { for (const w of S.wisps) P.res += w.amt; S.wisps = []; }
  function waveCleared() {
    quest('waves');
    if (S.trial) { const n = 3 + Math.floor(P.wave / 20); if (P.shop.open) P.shop.shards += n; pop(W / 2, 380, `TRIAL WON! +${n} Starshards`, SHARD, 1.6, 44); A.play('feat', 0.5); S.trialsWon = (S.trialsWon || 0) + 1; }
    if (has('cottages') && S.gateHp < gateMax()) repairGate();
    if (B.harvest) { const amt = (C.fightRate(P, B) * 1.6 + RATE) * B.harvest * surgeX(); P.res += amt; pop(W / 2, WALL_FRONT + 120, `Harvest +${C.fmt(amt)}`, '#ffd56b', 1.1, 32); }
    if (C.isBoss(P.wave)) {
      quest('boss');
      pop(W / 2, 330, 'BOSS DOWN!', '#ffd56b', 1.8, 60); SDK.happytime(); A.play('tier_up', 0.6);
      if (P.shop.open) { const n = (C.SHARDS.boss + B.bossPlus) * (1 + B.bossShards); P.shop.shards += n; pop(W / 2, 400, `+${n} Starshards`, SHARD, 1.6, 40); }
      if (P.wave === 10 && !P.tomorrowShown) { P.tomorrowShown = true; setTimeout(() => toast('Come back tomorrow: a daily Resonance reward waits'), 1900); }
    }
    P.wave++; P.bestWave = Math.max(P.bestWave, P.wave);
    if (C.cityWhole(P) && P.runWave < C.ascendWave(P) && P.wave >= C.ascendWave(P)) setTimeout(() => toast('Wave ' + P.wave + ' reached: you can Ascend!'), 1200);
    P.runWave = Math.max(P.runWave || 1, P.wave); lightStars(); openShop(); startWave(); save();
  }
  // a constellation star lights when the run reaches its wave: a big gold star, a lasting boon, the count of 40
  function lightStars() {
    const due = C.starsDue(P); if (!due.length) return;
    for (const st of due) P.stars.push(st.id); refresh();
    const st = due[due.length - 1], first = P.stars.length === due.length;
    A.play('feat', 0.55); burst({ x: W / 2, y: 330, size: 40 }, '#ffe08a', 30, 260);
    pop(W / 2, 300, `${st.name}!`, '#ffe08a', 2, 50); pop(W / 2, 352, `Star lit · ${st.line}`, '#fff3c4', 2, 32);
    setTimeout(() => toast(first ? `A star lights in the ${st.name.split("'")[0]} constellation! Reach waves to light all five · ${P.stars.length}/${C.STARS.length}`
      : `Star lit: ${st.name}, ${st.line} for good · ${P.stars.length}/${C.STARS.length}`), 900);
  }
  // The Shop opens when the first boss falls, with a welcome gift; older saves past wave 10 get it on load.
  function openShop() {
    if (P.shop.open || P.bestWave < C.SHOP_OPENS) return;
    P.shop.open = true; P.shop.shards += C.SHARDS.welcome; save();
    setTimeout(() => { toast(`The Shop is open in the Market! +${C.SHARDS.welcome} Starshards`); S.pointAt = { id: 'market', t: 0 }; A.play('buy', 0.5); }, 2600);
  }
  function gateBroken(noAd) {
    // a boss wave can be saved once by an optional ad (Full Launch only); otherwise the gate falls back, nothing lost
    if (!noAd && C.isBoss(P.wave) && SDK.rewardsOn && !S.savedByAd && !S.card) { S.paused = true; showCard('save'); return; }
    const to = Math.max(1, P.wave - 3);
    toast(`The gate fell back to wave ${to}. Nothing lost.`); S.cheerNext = 'Regroup. You\'ve got this!';
    P.wave = to; S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = gateMax(); S.savedByAd = false; startWave(); save();
  }
  // Floating numbers never pile up: a new one that lands on a fresh one steps above it, and at most 8 small numbers
  // live at once (the oldest goes first). Final-review fix, Nov 6 2026: "4.82K" sat on "3.81K".
  function pop(x, y, text, col, life, size) {
    y = Math.max(FIELD_TOP - 20 + size, y);
    for (let k = 0; k < 4; k++) { const hit = S.pops.find(q => q.text && !q.combo && q.t < 0.35 && Math.abs(q.x - x) < 60 && Math.abs(q.y - q.t * 40 - y) < size * 0.9); if (!hit) break; y -= size * 0.95; }
    const small = S.pops.filter(q => q.text && !q.combo && q.size <= 40); if (size <= 40 && small.length >= 8) S.pops.splice(S.pops.indexOf(small[0]), 1);
    const p = { x, y, text, col, t: 0, life, size }; S.pops.push(p); return p;
  }
  function burst(e, col, n, sp) {
    if (reduced()) n = Math.ceil(n / 3);
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, v = sp * (0.4 + Math.random() * 0.6);
      S.fx.push({ x: e.x, y: e.y - e.size * 0.45, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 40, g: 260, t: 0, life: 0.35 + Math.random() * 0.3, col, r: 2 + Math.random() * 3 });
    }
  }

  // ---------- actions: Upgrade, Restore (and Ascend, on the same button at 10/10) ----------
  function upgrade() {
    A.unlock();
    if (S.card) return;
    const fork = C.pendingFork(P); if (fork) return showCard('fork', fork);
    const c = C.upgradeCost(P.gateLevel, B);
    if (P.res < c) return refuse('up', c);
    const before = C.towerDamage(P, B), stage = C.stageOf(P.gateLevel);
    P.res -= c; P.gateLevel++; quest('upgrade'); refresh(); S.gateHp = gateMax(); S.upsSinceRestore = (S.upsSinceRestore || 0) + 1;
    if (C.stageOf(P.gateLevel) > stage) { toast(`Gate stage ${C.stageOf(P.gateLevel)}! The towers grow.`); A.play('tier_up', 0.6); TOWERS.forEach(t => (t.flash = 1.5)); }
    else { pop(GATE_X, WALL_FRONT - 240, `Damage ${rfmt(before)} -> ${rfmt(C.towerDamage(P, B))}`, '#ffd56b', 1.2, 38); TOWERS.forEach(t => (t.flash = 1.2)); A.play('tier_up', 0.25); }
    save(); ui();
  }
  function restore() {
    A.unlock();
    if (S.card) return;
    if (C.canAscend(P)) return showCard('ascend');
    if (C.cityWhole(P)) return toast(`Reach wave ${C.ascendWave(P)} to Ascend · best this run ${Math.max(P.runWave || 1, P.wave)}`);
    const offer = C.restoreOffer(P); if (!offer.length) return;
    const c = C.restoreCost(P.restored.length);
    if (P.res < c) return refuse('re', c);
    if (offer.length > 1) return showCard('pick', offer);
    doRestore(offer[0]);
  }
  function doRestore(id) {
    const c = C.restoreCost(P.restored.length); if (P.res < c) return;
    P.res -= c; P.restored.push(id); refresh(); S.upsSinceRestore = 0;
    const b = C.byId(id), r2 = P.ascensions >= 1 && C.RANK2[id];
    toast(r2 ? `${b.name} returns Ascended${P.ascensions >= 2 ? ' and gilded' : ''}! ${r2.name}: ${r2.line}` : `${b.name} restored: ${b.line}`); S.reveal = { b: id, t: 0 };
    if (r2) { S.rank2 = (S.rank2 || []).concat(id); pop(sx(CITY.spots[id][0]), sy(CITY.spots[id][1]) - 50, r2.name + '!', '#ffd56b', 1.8, 40); }
    A.play('tier_up', 0.55); SDK.happytime();
    if (P.restored.length === 5) setTimeout(() => { toast('The fountain flows again!'); S.reveal = { b: 'plaza', t: 0 }; }, 2900);
    if (C.cityWhole(P)) setTimeout(() => toast(C.canAscend(P) ? 'The city is whole! Restore is now Ascend.' : `The city is whole! Reach wave ${C.ascendWave(P)} to Ascend.`), 2900);
    save(); ui();
  }
  function refuse(id, cost) {
    const b = el(id); b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake');
    const rate = C.fightRate(P, B) * 1.6 + RATE;
    b.querySelector('.wait').textContent = `~${Math.max(1, Math.ceil((cost - P.res) / rate))}s`;
  }

  // ---------- the city: tap a restored building to level it up (more Resonance every second) ----------
  function levelUp(id) {
    if (!P.restored.includes(id)) return false;
    const c = C.levelCost(P, id, B); if (P.res < c) return false;
    const L = C.levelOf(P, id) + 1; P.res -= c; P.levels[id] = L; P.grown = true; quest('level'); refresh();
    const sp = CITY.spots[id], x = sx(sp[0]), y = sy(sp[1]);
    burst({ x, y: y + 20, size: 40 }, RES, 14, 150);
    if (C.MILESTONES.includes(L)) { pop(x, y - 40, `Level ${L}: x2!`, RES, 1.4, 44); A.play('tier_up', 0.55); S.reveal = { b: id, t: 0 }; }
    else { pop(x, y - 30, `Level ${L}`, RES, 0.9, 34); A.play('level_up', 0.5); }
    save(); return true;
  }



  // sound is muted only while an ad plays
  const adHooks = { mute: () => A.set('muted', true), unmute: () => A.set('muted', false) };

  // ---------- buildings with jobs (owner's idea, Nov 6 2026): the Market is the Shop, the Library keeps the quests and
  // Feats, the Watchtower the Ascension perks. They work even in ruins; a badge over each opens it, with a dot for news.
  const HALLS = {
    market: { kind: 'shop', name: 'Shop', ok: () => P.shop.open, news: () => C.shopNews(P) },
    library: { kind: 'quests', name: 'Quests and Feats', ok: () => P.shop.open, news: () => C.questClaimable(P) },
    watchtower: { kind: 'perks', name: 'Perks', ok: () => P.ascensions > 0, news: () => C.PERKS.some(k => (P.perks[k.id] || 0) < k.max && P.perkPts >= C.perkCost(P, k.id)) }
  };
  const badgeAt = id => { const s = CITY.spots[id]; return [sx(s[0]) + s[2] * K * 0.5, sy(s[1]) - s[3] * K * 0.42]; };
  function openHall(id) { const h = HALLS[id]; if (!h || !h.ok() || S.card) return false; A.unlock(); S.pointAt = null; if (h.kind === 'shop') openShopCard(); else showCard(h.kind); return true; }
  function drawBadges() {
    if (S.card || root.__storeCapture) return;
    for (const id in HALLS) {
      const h = HALLS[id]; if (!h.ok()) continue;
      const [x, y0] = badgeAt(id), y = y0 + (reduced() ? 0 : Math.sin(S.t * 2.2 + x) * 3), point = S.pointAt && S.pointAt.id === id;
      cx.save();
      if (point && !reduced()) glow(x, y, 60, 'rgba(111,242,255,', 0.35 + 0.25 * Math.sin(S.t * 6));
      cx.fillStyle = '#4a2a4f'; cx.strokeStyle = '#ffd56b'; cx.lineWidth = 4; cx.beginPath(); cx.arc(x, y, 27, 0, 7); cx.fill(); cx.stroke();
      if (h.kind === 'shop') { cx.fillStyle = '#6ff2ff'; L.star(cx, x, y + 1, 17, 7, 5); }
      else if (h.kind === 'perks') { cx.fillStyle = '#ffd56b'; L.star(cx, x, y + 1, 17, 7, 5); }
      else { cx.fillStyle = '#f6e7c4'; cx.fillRect(x - 13, y - 10, 26, 20); cx.fillStyle = '#c9a24a'; cx.fillRect(x - 16, y - 12, 5, 24); cx.fillRect(x + 11, y - 12, 5, 24);
        cx.fillStyle = '#8b6b3a'; cx.fillRect(x - 8, y - 5, 16, 3); cx.fillRect(x - 8, y + 2, 16, 3); }
      if (h.news()) { cx.fillStyle = '#ff5a8a'; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 3; cx.beginPath(); cx.arc(x + 20, y - 20, 9, 0, 7); cx.fill(); cx.stroke(); }
      cx.restore();
    }
  }


  // ---------- the stats overlay (owner's request, Nov 6 2026): a see-through layer with only the numbers ----------
  // Each building's level and Resonance a second, each tower's damage and shots a second, the gate's health and every
  // enemy's health. Toggled by the bars button in the top bar (or I); the fight goes on underneath and taps still work.
  // labels never overlap (owner's screenshot, Nov 6 2026: late-game numbers grew wide and piled into each other):
  // each label is placed, and if it touches one already drawn this frame it steps down, then up, then sideways
  let tagRects = [];
  function tag(x, y, lines, col, align = 'center') {
    cx.font = '24px "HG Display", sans-serif'; const w = Math.max(...lines.map(l => cx.measureText(l).width)) + 16, h = 27 * lines.length + 6;
    let x0 = Math.max(6, Math.min(W - 6 - w, align === 'center' ? x - w / 2 : x));   // labels stay on screen
    const hits = (a, b) => a[0] < b[0] + b[2] + 4 && b[0] < a[0] + a[2] + 4 && a[1] < b[1] + b[3] + 4 && b[1] < a[1] + a[3] + 4;
    const tries = [[0, 0], [0, h + 6], [0, -(h + 6)], [w * 0.6, 0], [-w * 0.6, 0], [0, 2 * (h + 6)]];
    for (const [dx, dy] of tries) { const r = [Math.max(6, Math.min(W - 6 - w, x0 + dx)), y - h / 2 + dy, w, h]; if (!tagRects.some(o => hits(r, o))) { x0 = r[0]; y = r[1] + h / 2; break; } }
    tagRects.push([x0, y - h / 2, w, h]);
    cx.fillStyle = 'rgba(24,12,20,.82)'; cx.strokeStyle = col; cx.lineWidth = 2;
    cx.beginPath(); cx.roundRect(x0, y - h / 2, w, h, 10); cx.fill(); cx.stroke();
    cx.textAlign = 'center'; cx.textBaseline = 'middle';
    lines.forEach((l, i) => { cx.fillStyle = i ? '#f1e3c8' : col; cx.fillText(l, x0 + w / 2, y - h / 2 + 17 + i * 27); });
  }
  function drawStats() {
    if (!P.settings.stats || S.card || root.__storeCapture) return;
    cx.save(); tagRects = [];
    cx.fillStyle = 'rgba(20,10,18,.45)'; cx.fillRect(0, CITY_Y - 40, W, CITY_H + 40);   // dim the town so the numbers read
    const bld = () => { for (const id of C.BUILDING_IDS) {
      const sp = CITY.spots[id], x = sx(sp[0]), y = sy(sp[1]) + 8, b = C.byId(id);
      if (P.restored.includes(id)) tag(x, y, [`Lv ${C.levelOf(P, id)}`, `+${rfmt(C.buildingRate(P, id, B) * surgeX())}/s`], '#ff9ce8');
      else { cx.globalAlpha = 0.7; tag(x, y, [b.name], '#a99a86'); cx.globalAlpha = 1; }
    } };
    // towers: damage per hit for the element each tower is firing now, and shots a second
    const els = elements(), dmg = C.towerDamage(P, B), rate = 1 / C.fireInterval(B);
    TOWERS.forEach((t, i) => {
      const e = els.length ? els[i % els.length] : 'none';
      tag(t.x, t.beacon ? t.y - 46 : t.y - 118, [`${rfmt(dmg * (1 + (B[e] || 0)))}`, `${rate.toFixed(1)}/s`], COLOR[e]);
    });
    // the gate
    tag(GATE_X, WALL_FRONT + 58, [`Gate ${C.fmt(Math.max(0, S.gateHp))}/${C.fmt(gateMax())}`], '#7bd15a');   // over the arch, clear of the tower labels
    // totals in one line under the counters
    const fight = C.fightRate(P, B) * surgeX();
    tag(W / 2, 175, [`City +${rfmt(RATE * surgeX())}/s · fight about +${rfmt(fight)}/s · damage ${rfmt(dmg)}`], '#ffd56b');
    bld(); S.tagRects = tagRects;   // kept for tools/qa/overlay-check.cjs
    cx.restore();
  }
  function drawEnemyHp(e) {
    if (!P.settings.stats || e.dying || root.__storeCapture) return;
    cx.font = '22px "HG Display", sans-serif'; cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineWidth = 4; cx.strokeStyle = '#1a0f15';
    const t = C.fmt(Math.max(0, e.hp)); cx.strokeText(t, e.x, e.y - e.size - 22); cx.fillStyle = '#fff4dc'; cx.fillText(t, e.x, e.y - e.size - 22);
  }
  function toggleStats() { A.unlock(); P.settings.stats = !P.settings.stats; el('stats').classList.toggle('on', P.settings.stats); A.play('claim', 0.25); save(); }

  // ---------- the 30-second beats (Nov 6 2026): Golden Wisp, Cheer, villagers, boss ranks, crowns ----------
  const comboTotal = () => C.unlocked(P, 'earth') ? 4 : 3;
  const bossRank = () => Math.max(0, P.ascensions - 4);   // from the 5th Ascension, bosses gain a rank each time
  function spawnAt(role, x, y) { spawn(role, [x, y]); }
  // a Golden Wisp crosses the field every 30 to 60 s once three buildings stand (every 15 to 30 s with the Bakery)
  function tickExtras() {
    if (S.cheerT > 0 && (S.cheerT -= DT) <= 0) { S.cheerT = 0; S.cheer = 0; }
    if (S.villagers) { S.villagers.t += DT; if (S.villagers.t > 2.6) S.villagers = null; }
    if (P.restored.length < 3) return;
    if (S.event && (evIs('harvest') || evIs('goldrush')) && !S.gold && S.goldT > 6) S.goldT = 6;
    if (S.gold) { const g = S.gold; g.t += DT; g.x += g.vx * DT; g.y = g.y0 + Math.sin(g.t * 2.4) * 26; if (g.t > g.life || g.x < -60 || g.x > W + 60) S.gold = null; return; }
    if (S.goldT === undefined) S.goldT = 8;
    if ((S.goldT -= DT) <= 0) {
      S.goldT = (30 + Math.random() * 30) * (has('bakery') ? 0.5 : 1);
      const fromLeft = Math.random() < 0.5;
      S.gold = { x: fromLeft ? -40 : W + 40, y0: 330 + Math.random() * 300, y: 0, vx: (fromLeft ? 1 : -1) * (95 + Math.random() * 30), t: 0, life: 9 };
      if (!P.goldSeen) { P.goldSeen = true; toast('A Golden Wisp! Tap it before it flies away'); }
    }
  }
  function tapGold(x, y) {
    const g = S.gold; if (!g || Math.hypot(x - g.x, y - g.y) > 70) return false;
    S.gold = null; S.goldCaught = (S.goldCaught || 0) + 1; A.play('claim', 0.6);
    const roll = Math.random(), mult = (1 + (B.wispX || 0)) * (S.event && evIs('goldrush') ? 3 : 1);
    burst({ x: g.x, y: g.y + 20, size: 40 }, '#ffd56b', 22, 220);
    if ((roll < 0.15 || (S.event && evIs('starfall'))) && P.shop.open) { const n = 2 * mult; P.shop.shards += n; pop(g.x, g.y - 20, `+${n} Starshards`, SHARD, 1.4, 42); }
    else if (roll < 0.3) { S.goldSurge = Date.now() + 15000; pop(g.x, g.y - 20, 'x2 for 15 s!', '#ff9ce8', 1.4, 42); }
    else { const amt = C.fightRate(P, B) * 1.6 * 10 * mult * surgeX(); P.res += amt; pop(g.x, g.y - 20, `+${C.fmt(amt)}`, '#ffd56b', 1.4, 46); }
    save(); return true;
  }
  function drawGold() {
    const g = S.gold; if (!g) return;
    cx.save(); glow(g.x, g.y, 76, 'rgba(255,214,107,', 0.6 + 0.2 * Math.sin(S.t * 8));
    for (let k = 1; k <= 4; k++) { cx.globalAlpha = 0.5 - k * 0.1; cx.fillStyle = '#ffd56b'; cx.beginPath(); cx.arc(g.x - Math.sign(g.vx) * k * 14, g.y + Math.sin((g.t - k * 0.08) * 2.4) * 4, 6 - k, 0, 7); cx.fill(); }
    cx.globalAlpha = 1; cx.fillStyle = '#fff6c8'; cx.strokeStyle = '#a0701a'; cx.lineWidth = 3; L.star(cx, g.x, g.y, 28, 12, 5, S.t * 2); cx.stroke();
    cx.restore();
  }
  // the Tavern: combos fill the Cheer meter; full, the towers fire twice as fast for 6 s
  function startCheer() { S.cheerT = S.cheerMax = B.cheerLong ? 10 : 6; S.cheersDone = (S.cheersDone || 0) + 1; pop(W / 2, 420, 'CHEER! Towers fire twice as fast', '#ffd56b', 1.8, 44); A.play('feat', 0.5); }
  function drawCheer() {
    if (!has('tavern') || root.__storeCapture) return;
    const sp = CITY.spots.tavern, x = sx(sp[0]), y = sy(sp[1]) + sp[3] * K * 0.42, w = 92, f = S.cheerT > 0 ? S.cheerT / (S.cheerMax || 6) : S.cheer || 0;
    if (f <= 0) return;   // an empty meter read as a stray bar on the Tavern (fresh-eyes review): it shows once combos start filling it
    cx.save(); cx.fillStyle = 'rgba(24,12,20,.8)'; cx.beginPath(); cx.roundRect(x - w / 2 - 3, y - 3, w + 6, 16, 8); cx.fill();
    cx.fillStyle = S.cheerT > 0 ? '#ffd56b' : '#e59a3a'; cx.beginPath(); cx.roundRect(x - w / 2, y, Math.max(6, w * f), 10, 5); cx.fill(); cx.restore();
    if (S.cheerT > 0 && !reduced()) for (const t of TOWERS) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(t.x, t.y - 40, 50, 'rgba(255,214,107,', 0.25); cx.restore(); }
  }
  // the Cottages: three villagers walk to the gate and mend it after every wave
  function repairGate() {
    const add = Math.min(gateMax() - S.gateHp, gateMax() * 0.35 * (1 + B.repairX)); S.gateHp += add; S.repairs = (S.repairs || 0) + 1;
    S.villagers = { t: 0 }; pop(GATE_X, WALL_FRONT - 110, `Repaired +${C.fmt(add)}`, '#7bd15a', 1.4, 34);
  }
  function drawVillagers() {
    const v = S.villagers; if (!v || root.__storeCapture) return;
    const sp = CITY.spots.cottages, x0 = sx(sp[0]), y0 = sy(sp[1]), x1 = GATE_X, y1 = WALL_FRONT + 60, u = Math.min(1, v.t / 1.6);
    for (let k = 0; k < 3; k++) {
      const uu = Math.max(0, Math.min(1, u - k * 0.08)), x = x0 + (x1 - x0) * uu + (k - 1) * 16, y = y0 + (y1 - y0) * uu - Math.abs(Math.sin(v.t * 10 + k)) * 4;
      cx.save(); cx.fillStyle = ['#c0603a', '#3a6fc0', '#4a9a4a'][k]; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 2;
      cx.beginPath(); cx.roundRect(x - 6, y - 14, 12, 14, 4); cx.fill(); cx.stroke();
      cx.fillStyle = '#f2c79a'; cx.beginPath(); cx.arc(x, y - 19, 6, 0, 7); cx.fill(); cx.stroke(); cx.restore();
      if (uu >= 1 && Math.random() < 0.3) S.fx.push({ x: x1 + (Math.random() - 0.5) * 60, y: y1 - 20, vx: (Math.random() - 0.5) * 80, vy: -80, g: 260, t: 0, life: 0.4, col: '#ffe08a', r: 2.5 });
    }
  }
  // gate stages 4 and 5: a gold crown over each tower, then a turning halo
  function drawCrown(x, y, stage, i) {
    cx.save(); cx.translate(x, y + Math.sin(S.t * 2 + i) * 2);
    if (stage >= 5) { cx.save(); cx.rotate(S.t * 0.8); cx.strokeStyle = 'rgba(255,230,150,.8)'; cx.lineWidth = 3; cx.setLineDash([6, 6]); cx.beginPath(); cx.ellipse(0, 4, 26, 9, 0, 0, 7); cx.stroke(); cx.restore(); }
    cx.fillStyle = '#ffd56b'; cx.strokeStyle = '#7a4f18'; cx.lineWidth = 2;
    cx.beginPath(); cx.moveTo(-12, 0); cx.lineTo(-12, -10); cx.lineTo(-6, -4); cx.lineTo(0, -13); cx.lineTo(6, -4); cx.lineTo(12, -10); cx.lineTo(12, 0); cx.closePath(); cx.fill(); cx.stroke();
    cx.restore();
  }

  // ---------- daily quests and the Resonance Surge ----------
  const today = () => C.dayKey(new Date());
  // quests start with the Shop (their reward is Starshards); a new day brings three new ones
  const STAT_OF = { kills: 'kills', boss: 'bosses', combo: 'combos', shatter: 'shatter', upgrade: 'upgrades', level: 'levels', surge: 'surges' };
  // every counted event feeds the lifetime stats (Feats) and, once the Shop is open, today's quests
  function quest(id, n = 1) { if (STAT_OF[id]) P.stats[STAT_OF[id]] += n; if (P.shop.open) { C.questsFor(P, today()); C.questTick(P, id, n); } }
  // Feats are checked twice a second; during the first session they are earned quietly, so the opening keeps one voice
  function checkFeats(quiet) {
    const got = C.featsEarned(P); if (!got.length) return;
    const pay = got.reduce((a, f) => a + C.FEAT_PAY[f.tier], 0);
    if (quiet) return;   // at load: never save before the return cards, a save stamps lastSeen and would erase offline earnings
    save(); if (P.firstSession) return;
    toast(got.length === 1 ? `Feat: ${got[0].name} · +${pay} Starshards` : `${got.length} Feats earned · +${pay} Starshards`);
    S.medal = { tier: Math.max(...got.map(f => f.tier)), name: got.length === 1 ? got[0].name : `${got.length} Feats`, t: 0 }; A.play('feat', 0.6);
  }
  const surgeX = () => C.surgeOn(P, Date.now()) || (S.goldSurge || 0) > Date.now() ? 2 : 1;
  function adDay() { const d = today(); if (P.surge.adDay !== d) { P.surge.adDay = d; P.surge.ads = 0; P.surge.shardAds = 0; } }
  // Basic Launch: free every 15 minutes. Full Launch: a rewarded ad, any time one is not running, 8 a day.
  function surgeTap() {
    A.unlock(); const now = Date.now(); if (C.surgeOn(P, now) || S.card) return;
    const go = () => { C.startSurge(P, Date.now()); quest('surge'); A.play('surge', 0.6); toast('Surge! Double Resonance for 3 minutes'); SDK.happytime(); save(); };
    if (SDK.rewardsOn) { adDay(); if (P.surge.ads >= C.SURGE_ADS_PER_DAY) return toast('No more Surges today'); S.paused = true;
      SDK.rewarded(adHooks).then(ok => { S.paused = false; if (ok) { P.surge.ads++; go(); } }); return; }
    if (C.surgeReadyFree(P, now)) go(); else toast(`The next Surge charges in ${mmss(P.surge.until + C.surgeCooldown(P) - now)}`);
  }
  const mmss = ms => { const t = Math.max(0, Math.ceil(ms / 1000)); return `${Math.floor(t / 60)}:${String(t % 60).padStart(2, '0')}`; };
  const qTabs = on => `<div class="tabs">${[['daily', 'Daily quests'], ['feats', `Feats ${P.feats.length}/${C.FEATS.length}`]].map(([k, n]) => `<button class="tab qtab${k === on ? ' on' : ''}" data-q="${k}">${n}</button>`).join('')}</div>`;
  function featsHtml() {
    const rows = C.FEATS.map(f => ({ f, done: P.feats.includes(f.id), n: C.featProgress(P, f) }))
      .sort((a, b) => (a.done - b.done) || (b.n / b.f.need - a.n / a.f.need));   // closest unfinished first, finished last
    return `<div class="cardhead"><h2>Feats</h2><button id="c-no" class="x" aria-label="Close"></button></div>${qTabs('feats')}<div class="feats">` + rows.map(({ f, done, n }) => `<div class="feat${done ? ' done' : ''}"><i class="medal t${f.tier}"></i>`
      + `<div class="ft"><b>${f.name}</b><span>${f.text}</span>${done ? '' : `<div class="qbar"><i style="width:${Math.round(n / f.need * 100)}%"></i></div>`}</div>`
      + `<span class="fr">${done ? 'Done' : `${C.fmt(n)}/${C.fmt(f.need)}`}<br><i class="star"></i>${C.FEAT_PAY[f.tier]}</span></div>`).join('')
      + `</div>`;
  }
  const untilMidnight = () => { const n = new Date(), m = new Date(n); m.setHours(24, 0, 0, 0); const mins = Math.ceil((m - n) / 60e3); return `${Math.floor(mins / 60)}h ${mins % 60}m`; };
  function questsHtml() {
    if (S.qTab === 'feats') return featsHtml();
    const qs = C.questsFor(P, today()), all = qs.list.every(q => q.claimed);
    const om = P.omen && C.OMENS.find(o => o.id === P.omen);
    return `<div class="cardhead"><h2>Quests</h2><button id="c-no" class="x" aria-label="Close"></button></div>${qTabs('daily')}${om ? `<p class="omen">Today's omen: <b>${om.name}</b> · ${om.line}</p>` : ''}<p class="small">New quests in ${untilMidnight()}</p><div class="quests">`
      + qs.list.map((q, i) => `<div class="quest${q.claimed ? ' done' : ''}"><div class="qt"><b>${C.questText(q)}</b><span>${Math.min(q.n, q.need)}/${q.need}</span></div>`
        + `<div class="qbar"><i style="width:${Math.round(q.n / q.need * 100)}%"></i></div>`
        + (q.claimed ? `<span class="qok">Claimed</span>` : `<button class="buy ${q.n >= q.need ? 'can' : 'off'} qclaim" data-i="${i}"><i class="star"></i>${C.QUEST_REWARD}</button>`) + `</div>`).join('')
      + `<div class="quest bonus${qs.bonus ? ' done' : ''}"><div class="qt"><b>All three</b><span>${qs.list.filter(q => q.claimed).length}/3</span></div>`
      + (qs.bonus ? `<span class="qok">Claimed</span>` : `<button class="buy ${all ? 'can' : 'off'} qbonus"><i class="star"></i>${C.QUEST_BONUS}</button>`) + `</div></div>`
      + `<p class="small">Spend Starshards in the Shop.</p>`;
  }
  function wireQuests(body) {
    body.querySelectorAll('.qtab').forEach(b => (b.onclick = () => { S.qTab = b.dataset.q; A.play('tier_up', 0.08); showCard('quests'); }));
    body.querySelectorAll('.qclaim').forEach(b => (b.onclick = () => { const n = C.questClaim(P, +b.dataset.i);
      if (n) { A.play('claim', 0.6); pop(W / 2, 500, `+${n} Starshards`, SHARD, 1.2, 40); save(); showCard('quests'); } else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); } }));
    const bb = body.querySelector('.qbonus');
    if (bb) bb.onclick = () => { const n = C.questBonus(P); if (n) { A.play('feat', 0.6); save(); showCard('quests'); } else { bb.classList.remove('shake'); void bb.offsetWidth; bb.classList.add('shake'); } };
  }


  // ---------- Ascension perks: the star chip in the top bar opens them ----------
  function perksHtml() {
    return `<div class="cardhead"><h2>Perks</h2><span class="chip shards perkpts"><i class="star gold"></i><b>${P.perkPts}</b></span><button id="c-no" class="x" aria-label="Close"></button></div>`
      + `<p class="small">Ascension ${P.ascensions}. Each Ascension pays 1 perk point, plus 1 for every 25 waves reached first.</p>`
      + (P.ascensions >= 1 ? `<p class="small">Constellation stars ${P.stars.length}/${C.STARS.length}${(() => { const nx = C.STARS.find(st => st.asc === P.ascensions && !P.stars.includes(st.id)); return nx ? ` · next: ${nx.name} at wave ${C.starWave(P, nx.k)}` : ''; })()}</p>` : '') + `<div class="perks">`
      + C.PERKS.map(k => { const n = P.perks[k.id] || 0, max = n >= k.max, c = C.perkCost(P, k.id);
        return `<div class="perkrow"><b>${k.name}<small>${n}/${k.max}</small></b><span>${k.line}</span>`
          + (max ? `<span class="qok">Max</span>` : `<button class="buy ${P.perkPts >= c ? 'can' : 'off'} perk" data-id="${k.id}"><i class="star gold"></i>${c}</button>`) + `</div>`; }).join('')
      + `</div>`
      + (C.unlocked(P, 'relics') ? `<h3 class="rel">Relics · choose 2</h3><div class="perks">` + C.RELICS.map(r => { const on = P.relics.includes(r.id);
          return `<div class="perkrow"><b>${r.name}</b><span>${r.line}</span><button class="buy ${on ? 'eq' : 'can'} relic" data-id="${r.id}">${on ? 'Equipped' : 'Equip'}</button></div>`; }).join('') + `</div>`
        : `<p class="small">Ascension 3 unlocks Relics.</p>`);
  }
  function wirePerks(body) {
    body.querySelectorAll('.relic').forEach(b => (b.onclick = () => { if (C.equipRelic(P, b.dataset.id)) { A.play('buy', 0.5); refresh(); save(); showCard('perks'); } }));
    body.querySelectorAll('.perk').forEach(b => (b.onclick = () => {
      if (C.buyPerk(P, b.dataset.id)) { A.play('feat', 0.5); refresh(); save(); showCard('perks'); }
      else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); }
    }));
  }

  // ---------- the Shop: tower looks, shot trails, hit effects, the city's look and wisps ----------
  const TABS = [['tower', 'Towers'], ['trail', 'Shots'], ['hit', 'Hits'], ['town', 'City'], ['wisp', 'Wisps']];
  let previews = [];
  function openShopCard() {
    if (S.card || !P.shop.open) return;
    A.unlock(); el('shop').classList.remove('fresh');
    for (const i of C.SHOP) if (i.price <= P.shop.shards && !P.shop.seen.includes(i.id)) P.shop.seen.push(i.id);   // the dot clears
    showCard('shop');
  }
  // what a trade costs in time, at the income the player actually has now (fight and city together)
  const tradeMins = tc => { const m = tc / (C.fightRate(P, B) * 1.6) / 60; return m < 1.5 ? '1 minute' : `${Math.round(m)} minutes`; };
  function shopHtml() {
    const tab = S.shopTab || 'tower', sh = P.shop, tc = C.exchangeCost(P, B), items = C.SHOP.filter(i => i.cat === tab);
    const sel = C.shopItem(S.shopSel) && C.shopItem(S.shopSel).cat === tab ? C.shopItem(S.shopSel) : C.shopItem(sh.equip[C.slotOf(items[0])]);
    const tile = it => {
      const own = sh.owned.includes(it.id), on = sh.equip[C.slotOf(it)] === it.id;
      const state = on ? 'eq' : own ? 'own' : sh.shards >= it.price ? 'can' : 'off';
      return `<div class="item ${state}${it.id === sel.id ? ' sel' : ''}" data-id="${it.id}"><canvas width="360" height="202"></canvas><b>${it.name}</b>`
        + `<button class="buy ${state}" data-id="${it.id}">${on ? 'Equipped' : own ? 'Equip' : `<i class="star"></i>${it.price}`}</button></div>`;
    };
    return `<div class="shophead"><h2>Shop</h2><span class="chip shards"><i class="star"></i><b>${sh.shards}</b></span><button id="c-no" class="x" aria-label="Close"></button></div>`
      + `<p class="earn"><i class="star"></i> Boss +${C.SHARDS.boss} · combo +${C.SHARDS.combo} · daily +${C.SHARDS.daily} · Ascend +${C.SHARDS.ascend}</p>`
      + `<div class="trade">${SDK.rewardsOn ? '' : `<span>or trade about<br>${tradeMins(tc)} of kills</span>`}`
      + `<button id="c-trade" class="cbtn ${P.res >= tc ? 'gold' : 'off'}">Trade <i class="gem"></i>${C.fmt(tc)}<span class="to"></span><i class="star"></i>1</button>`
      // Full Launch only: an ad for Starshards, side by side with the trade so the panel keeps its height
      + (SDK.rewardsOn ? (adDay(), `<button id="c-shardad" class="cbtn ${P.surge.shardAds < C.SHARD_ADS_PER_DAY ? 'gold' : 'off'}"><span class="play"></span>+${C.SHARD_AD}<i class="star"></i><span class="small">${C.SHARD_ADS_PER_DAY - P.surge.shardAds} left</span></button>`) : '') + `</div>`
      + `<div class="tabs">${TABS.map(([k, n]) => `<button class="tab${k === tab ? ' on' : ''}${C.SHOP.some(i => i.cat === k && !sh.owned.includes(i.id) && i.price <= sh.shards) ? ' news' : ''}" data-tab="${k}">${n}</button>`).join('')}</div>`
      + `<div class="grid">${items.map(tile).join('')}</div>`
      + `<p class="detail"><b>${sel.name}</b> · ${sel.line}</p>`;
  }
  function wireShop(body, rerender) {
    body.querySelectorAll('.tab').forEach(b => (b.onclick = () => { A.unlock(); S.shopTab = b.dataset.tab; S.shopSel = null; A.play('tier_up', 0.08); rerender(); }));
    body.querySelectorAll('.item').forEach(t => (t.onclick = ev => { if (ev.target.closest('.buy')) return; S.shopSel = t.dataset.id; rerender(); }));
    body.querySelectorAll('.buy').forEach(b => (b.onclick = () => {
      A.unlock(); const it = C.shopItem(b.dataset.id); S.shopSel = it.id;
      if (P.shop.owned.includes(it.id)) { C.equip(P, it.id); A.play('tier_up', 0.15); }
      else if (C.buy(P, it.id)) { A.play('buy', 0.6); toast(`${it.name} unlocked!`); }
      else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); b.textContent = `${it.price - P.shop.shards} more`; return; }
      applyLooks(); save(); rerender();
    }));
    const sa = body.querySelector('#c-shardad');
    if (sa) sa.onclick = () => { adDay(); if (P.surge.shardAds >= C.SHARD_ADS_PER_DAY) return;
      SDK.rewarded(adHooks).then(ok => { if (ok) { P.surge.shardAds++; P.shop.shards += C.SHARD_AD; A.play('tier_up', 0.5); save(); } rerender(); }); };
    const tr = body.querySelector('#c-trade');
    if (tr) tr.onclick = () => {
      A.unlock();
      if (C.trade(P, B)) { A.play('buy', 0.4); save(); rerender(); }
      else { tr.classList.remove('shake'); void tr.offsetWidth; tr.classList.add('shake'); }
    };
    previews = [...body.querySelectorAll('.item canvas')].map(c => ({ c, id: c.parentNode.dataset.id }));
    drawPreviews(performance.now() / 1000);
  }
  // Live previews: the real tower sprite in its skin, a shot flying with the trail, a hit landing, the city thumbnail, a wisp.
  function drawPreviews(t) {
    for (const { c, id } of previews) {
      const g = c.getContext('2d'), it = C.shopItem(id), Wp = c.width, Hp = c.height;
      g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.globalCompositeOperation = 'source-over';
      if (it.cat === 'town') {
        const key = 'thumb' + id;
        if (!lookCache[key] && orig.restored) { const q = document.createElement('canvas'); q.width = 360; q.height = 202;
          q.getContext('2d').drawImage(orig.restored, orig.restored.width * 0.25, 0, orig.restored.width * 0.5, orig.restored.width * 0.5 * 202 / 360, 0, 0, 360, 202); lookCache[key] = L.townGrade(id, q); }
        if (lookCache[key]) g.drawImage(lookCache[key], 0, 0); continue;
      }
      if (it.cat === 'tower') {
        const bgd = g.createRadialGradient(Wp / 2, Hp * 0.7, 10, Wp / 2, Hp * 0.6, Wp * 0.6); bgd.addColorStop(0, '#5c3a5a'); bgd.addColorStop(1, '#2a1a22');
        g.fillStyle = bgd; g.fillRect(0, 0, Wp, Hp);
        const im = lookCache[id + '3'] || (orig[it.el + 3] && (lookCache[id + '3'] = L.towerGrade(id, orig[it.el + 3]))); if (!im) continue;
        g.fillStyle = 'rgba(10,6,10,.45)'; g.beginPath(); g.ellipse(Wp / 2, Hp - 16, 60, 12, 0, 0, 7); g.fill();
        const h = Hp - 26, sc = h / (im.height - 16), w = im.width * sc; g.drawImage(im, Wp / 2 - w / 2, Hp - 12 - (im.height - 8) * sc, w, im.height * sc);
        g.globalCompositeOperation = 'lighter'; glow(Wp / 2, Hp - 12 - h * 0.8, 40, L.TOWER[id].glow, 0.35 + 0.12 * Math.sin(t * 3), g); continue;
      }
      // a close-up of the meadow at twice the game's scale, so the effect reads at thumbnail size
      if (img.meadow) g.drawImage(img.meadow, 0, 0, img.meadow.width * 0.25, img.meadow.width * 0.25 * Hp / Wp, 0, 0, Wp, Hp); else { g.fillStyle = '#6f9a3c'; g.fillRect(0, 0, Wp, Hp); }
      g.setTransform(2, 0, 0, 2, 0, 0);
      const els = ['fire', 'frost', 'storm'];
      if (it.cat === 'trail') {
        const loop = t / 0.9, u = loop % 1, el2 = els[Math.floor(loop) % 3], path = v => [20 + v * 140, 84 - v * 56 + Math.sin(v * 6) * 7];
        const head = path(u), tr = []; for (let k = 7; k >= 1; k--) tr.push(path(u - k * 0.07));
        L.drawTrail(g, id, { x: head[0], y: head[1], trail: tr }, COLOR[el2], GLOW[el2], glow, t);
      } else if (it.cat === 'hit') {
        const sl = img.slime; if (sl) { const fw = sl.width / 6, sc = 64 / sl.height; g.drawImage(sl, Math.floor(t * 8) % 6 * fw, 0, fw, sl.height, 90 - fw * sc / 2, 98 - sl.height * sc, fw * sc, sl.height * sc); }
        const u = (t % 0.6) / 0.6, el2 = els[Math.floor(t / 0.6) % 3], hx = 90, hy = 64;
        if (id === 'hit-classic') { g.fillStyle = COLOR[el2]; for (let k = 0; k < 8; k++) { const a = k * 0.8 + Math.floor(t / 0.6), d = u * 30; g.globalAlpha = 1 - u; g.beginPath(); g.arc(hx + Math.cos(a) * d, hy + Math.sin(a) * d + u * u * 30, 4, 0, 7); g.fill(); } }
        else if (u < 0.9) { g.translate(hx, hy); g.scale(1.6, 1.6); g.translate(-hx, -hy); L.drawHit(g, { x: hx, y: hy, t: u / 0.9 * L.HIT_LIFE * 0.85, col: COLOR[el2], style: id, seed: Math.floor(t / 0.6) }); }   // a little bigger than in play, so it reads in a tile
      } else if (it.cat === 'wisp') {
        const u = (t % 1.2) / 1.2, path = v => [16 + v * 148, 90 - Math.sin(v * Math.PI) * 64], hd = path(u), tr = [];
        for (let k = 10; k >= 1; k--) tr.push(path(Math.max(0, u - k * 0.03)));
        drawWisp(g, { x: hd[0], y: hd[1], trail: tr }, L.WISP[id]);
      }
    }
  }

  // ---------- cards: daily reward, offline earnings, restore pick, fork, Ascend, save the gate, settings ----------
  const cardQueue = [];
  function nextCard() { const c = cardQueue.shift(); if (c) showCard(c[0], c[1]); }
  const hours = s => s >= 3600 ? `${Math.floor(s / 3600)}h ${Math.floor((s % 3600) / 60)}m` : `${Math.max(1, Math.floor(s / 60))} min`;
  function showCard(kind, data) {
    const card = el('card'), body = card.querySelector('.body'); S.card = kind; card.className = 'on ' + kind;
    SDK.gameplay(false);   // cards are menus: the fight holds still and CrazyGames hears gameplayStop
    el('toast').classList.remove('show'); toastT = 0;
    const btn = (id, label, cls = '') => `<button id="${id}" class="cbtn ${cls}">${label}</button>`;
    let html = '';
    if (kind === 'daily') html = `<span class="eyebrow">Day ${data.day} reward</span><h2>+${C.fmt(data.amount)} Resonance</h2><p>${data.big ? 'Day 7 bonus: five times the reward!' : 'Day 7 pays five times as much.'}</p>${P.shop.open ? `<p class="small">and ${data.big ? C.SHARDS.daily7 : C.SHARDS.daily} Starshards for the Shop</p>` : ''}${btn('c-ok', 'Claim')}`;
    if (kind === 'offline') html = `<span class="eyebrow">While you were away</span><h2>+${C.fmt(data.res)} Resonance</h2><p>The gate held and the city hummed for ${hours(data.secs)}${data.secs >= C.OFFLINE_CAP_H * 3600 - 1 ? ', the most it stores' : ''}.</p><p class="small">From the fight ${C.fmt(data.fight)} · from the city ${C.fmt(data.city)}</p>${SDK.rewardsOn ? btn('c-ad', 'Watch an ad: double it', 'gold') : ''}${btn('c-ok', 'Collect')}`;
    if (kind === 'pick') html = `<span class="eyebrow">Restore · ${C.fmt(C.restoreCost(P.restored.length))} Resonance</span><h2>Pick a building</h2><div class="pick">${data.map(id => { const b = C.byId(id); return `<button class="opt" data-id="${id}"><b>${b.name}</b><span>${b.line}</span></button>`; }).join('')}</div>${btn('c-no', 'Not yet', 'quiet')}`;
    if (kind === 'fork') html = `<span class="eyebrow">Gate stage ${data}</span><h2>Choose a path</h2><div class="pick">${C.FORKS[data].map(f => `<button class="opt" data-id="${f.id}"><b>${f.name}</b><span>${f.line}</span></button>`).join('')}</div>`;
    if (kind === 'ascend') { const un = C.ASCEND_UNLOCKS.find(u => u.at === P.ascensions + 1), next = Math.min(90, 35 + 5 * P.ascensions), pp = C.perkPtsFor(P), n = P.ascensions + 1;
      // three short lines instead of a paragraph (fresh-eyes review, Nov 6): what you gain, what stays, what starts over
      html = `<span class="eyebrow">${n < C.ETERNAL_AT ? `Ascension ${n} of ${C.ETERNAL_AT} on the road to the Eternal City` : n === C.ETERNAL_AT ? 'This Ascension completes the Eternal City' : `Ascension ${n}`}</span><h2>Ascend?</h2>`
        + `<ul class="asc"><li><b>Gain:</b> Resonance and damage x${C.ASCEND_MULT} for good (x${rfmt(Math.pow(C.ASCEND_MULT, n))} in all), ${pp} perk point${pp === 1 ? '' : 's'}${P.shop.open ? `, ${C.SHARDS.ascend} Starshards` : ''}${un ? `, and <b>${un.name}</b>` : ''}.</li>`
        + `<li><b>Keep:</b> best wave, combos, Feats, perks, Relics, stars and Shop looks.</li>`
        + `<li><b>Start over:</b> the city returns to ruins at wave 1. The next Ascension needs wave ${next}.</li></ul>${btn('c-ok', 'Ascend', 'gold')}${btn('c-no', 'Not yet', 'quiet')}`; }
    if (kind === 'eternal') html = `<span class="eyebrow">Ascension ${C.ETERNAL_AT}</span><h2>The Eternal City</h2><p>Ten times the city fell to ruin and ten times you raised it again. The Hearthgate stands for good, and its light will never go out.</p>`
      + `<p class="small">Best wave ${C.fmt(P.bestWave)} · ${C.fmt(P.stats.kills)} enemies held back · ${C.fmt(P.stats.bosses)} bosses · ${P.feats.length}/${C.FEATS.length} Feats · ${P.stars.length}/${C.STARS.length} stars</p>`
      + `<p class="small">The waves keep coming, and the city keeps growing: every Ascension from here still makes you stronger, with new twin events each run.</p>${btn('c-ok', 'Keep the light burning', 'gold')}`;
    if (kind === 'save') html = `<span class="eyebrow">Wave ${C.fmt(P.wave)} · boss</span><h2>The gate is breaking!</h2><p>Watch an ad to rebuild the gate and keep fighting this boss.</p>${btn('c-ad', 'Watch an ad: save the gate', 'gold')}${btn('c-no', 'Fall back', 'quiet')}`;
    if (kind === 'building') {
      const b = C.byId(data), L = C.levelOf(P, data), r = C.buildingRate(P, data, B), r2 = C.buildingRate(P, data, B, L + 1), c = C.levelCost(P, data, B), m = C.nextMilestone(L);
      const hall = HALLS[data] && HALLS[data].ok() ? HALLS[data] : null;
      html = `<span class="eyebrow">Level ${L} · ${rfmt(r)} Resonance a second</span><h2>${b.name}</h2>${hall ? btn('c-hall', `Open the ${hall.name}`, 'gold') : ''}<p>${b.line}</p>${P.ascensions >= 1 ? `<p><b>Ascended · ${C.RANK2[data].name}:</b> ${C.RANK2[data].line}</p>` : ''}`
        + `<p class="small">Next level: ${rfmt(r)} -> ${rfmt(r2)} a second${m ? `<br>Level ${m} doubles it` : ''}</p>`
        + btn('c-ok', `Level up · <i class="gem"></i>${C.fmt(c)}`, P.res >= c ? 'gold' : 'off') + `<span class="wait"></span>` + btn('c-no', 'Close', 'quiet');
    }
    if (kind === 'shop') html = shopHtml();
    if (kind === 'quests') html = questsHtml();
    if (kind === 'perks') html = perksHtml();
    if (kind === 'settings') html = `<h2>Paused</h2>${['music', 'sfx', 'reduced'].map(k => `<label class="tog"><input type="checkbox" id="s-${k}" ${P.settings[k] ? 'checked' : ''}><span>${{ music: 'Music', sfx: 'Sound effects', reduced: 'Reduced motion' }[k]}</span></label>`).join('')}<p class="small">Best wave ${C.fmt(P.bestWave)} · ★ ${P.ascensions}</p><p class="small">Hearthgate · build ${BUILD}</p>${btn('c-ok', 'Resume')}`;
    body.innerHTML = html;
    const close = () => { card.className = ''; S.card = null; if (!S.paused) SDK.gameplay(true); ui(); nextCard(); };
    const on = (id, f) => { const b = body.querySelector('#' + id); if (b) b.onclick = () => { A.unlock(); f(); }; };
    if (kind === 'daily') on('c-ok', () => { P.res += data.amount; if (P.shop.open) P.shop.shards += data.big ? C.SHARDS.daily7 : C.SHARDS.daily; P.daily = { day: data.today, streak: data.day, claimedTotal: P.daily.claimedTotal + data.amount }; A.play('tier_up', 0.5); save(); close(); });
    if (kind === 'offline') {
      on('c-ok', () => { P.res += data.res; save(); close(); });
      on('c-ad', () => SDK.rewarded(adHooks).then(ok => { P.res += data.res * (ok ? 2 : 1); if (ok) toast('Doubled!'); save(); close(); }));
    }
    if (kind === 'pick' || kind === 'fork') body.querySelectorAll('.opt').forEach(o => (o.onclick = () => {
      A.unlock();
      if (kind === 'pick') { close(); doRestore(o.dataset.id); return; }
      P.forks[data] = o.dataset.id; refresh(); S.gateHp = gateMax();
      toast(`${C.FORKS[data].find(f => f.id === o.dataset.id).name} chosen`); A.play('tier_up', 0.5); save(); close();
    }));
    if (kind === 'pick' || kind === 'ascend' || kind === 'building' || kind === 'shop' || kind === 'quests') on('c-no', close);
    if (kind === 'quests') wireQuests(body);
    if (kind === 'perks') { on('c-no', close); wirePerks(body); }
    if (kind === 'shop') wireShop(body, () => showCard('shop'));
    if (kind === 'building') {
      on('c-ok', () => {
        if (levelUp(data)) return showCard('building', data);
        const b = body.querySelector('#c-ok'), c = C.levelCost(P, data, B); b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake');
        body.querySelector('.wait').textContent = `About ${Math.max(1, Math.ceil((c - P.res) / (C.fightRate(P, B) * 1.6 + RATE)))}s of fighting`;
      });
      card.onclick = ev => { if (ev.target === card) close(); };
      on('c-hall', () => { close(); openHall(data); });
    } else card.onclick = kind === 'shop' ? (ev => { if (ev.target === card) close(); }) : null;
    if (kind === 'ascend') on('c-ok', () => {
      P = C.ascend(P); refresh(); S.wisps = []; S.motes = []; S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = 0; A.play('ascension', 0.7);
      S.cheerNext = 'A new dawn. Rebuild!'; startWave(); save(); close();
      const un = C.ASCEND_UNLOCKS.find(u => u.at === P.ascensions);
      toast(un ? `Ascension ${P.ascensions} unlocks ${un.name}! ${un.line}` : `Ascension ${P.ascensions}: Resonance and damage x${rfmt(Math.pow(C.ASCEND_MULT, P.ascensions))}`);
      if (un && un.id === 'relics') S.pointAt = { id: 'watchtower', t: 0 };
      if (P.ascensions === C.ETERNAL_AT) { const show = () => (S.card ? setTimeout(show, 1000) : showCard('eternal')); setTimeout(show, 1600); }
    });
    if (kind === 'eternal') { on('c-ok', close); S.flash = { col: '#ffe08a', t: 0 }; S.shake = 0.3; A.play('feat', 0.7); SDK.happytime(); burst({ x: W / 2, y: PLAZA.y, size: 60 }, '#ffe08a', 60, 340); }
    if (kind === 'save') {
      on('c-ad', () => SDK.rewarded(adHooks).then(ok => { S.paused = false; close(); if (ok) { S.savedByAd = true; S.gateHp = gateMax(); toast('The gate is rebuilt!'); } else gateBroken(true); }));
      on('c-no', () => { S.paused = false; close(); gateBroken(true); });
    }
    if (kind === 'settings') {
      for (const k of ['music', 'sfx', 'reduced']) body.querySelector('#s-' + k).onchange = ev => { P.settings[k] = ev.target.checked; A.set(k, ev.target.checked); save(); };
      on('c-ok', () => { S.paused = false; el('pause').classList.remove('on'); SDK.gameplay(true); close(); });
    }
  }

  // ---------- rendering ----------
  const cv = document.getElementById('field'), cx = cv.getContext('2d');
  const img = {}; let k = 1, cityRuined = null; const cityRestored = {};
  function loadImg(name, src) { return new Promise(ok => { const i = new Image(); i.onload = () => { img[name] = i; ok(); }; i.onerror = ok; i.src = src; }); }

  // The painting's top edge fades into the meadow so field and city are one ground. Each restored building is cut once
  // from the restored painting with a soft edge and laid over the ruined one when it comes back.
  // ---------- the cached ground layer ----------
  let ground = null, groundKey = '', groundVer = 0;
  const GROUND_H = () => CITY_Y + CITY_H;
  function groundLayer() {
    const reveal = S.reveal && S.reveal.t < 0.9 ? S.reveal.b : '';
    const parts = (P.restored.length >= 5 ? P.restored.concat('plaza') : P.restored).filter(id => id !== reveal);
    const gild = P.ascensions >= 2, key = [groundVer, k, H, gild, parts.join(',')].join('|'); if (ground && key === groundKey) return ground;
    const c = ground && ground.width === Math.round(W * k) && ground.height === Math.round(GROUND_H() * k) ? ground : document.createElement('canvas');
    c.width = Math.round(W * k); c.height = Math.round(GROUND_H() * k); const g = c.getContext('2d'); g.setTransform(k, 0, 0, k, 0, 0);
    if (img.meadow) g.drawImage(img.meadow, 0, 0, W, CITY_Y + 30); else { g.fillStyle = '#6f9a3c'; g.fillRect(0, 0, W, CITY_Y + 30); }
    if (img.forest) { const f = img.forest, fw = f.width * 1.5, fh = f.height * 1.5; g.drawImage(f, (W - fw) / 2, -14, fw, fh); g.drawImage(f, (W - fw) / 2 + 60, 40, fw, fh * 0.9); }
    if (cityRuined) g.drawImage(cityRuined, 0, CITY_Y);
    for (const id of parts) if (cityRestored[id]) g.drawImage(gild && id !== 'plaza' ? gilded(id) : cityRestored[id], 0, CITY_Y);
    ground = c; groundKey = key; return c;
  }
  // from the 2nd Ascension the restored city is gilded: warm gold light on every rebuilt building (made in code)
  const gildCache = {};
  function gilded(id) {
    if (gildCache[id] && gildCache[id].v === groundVer) return gildCache[id].c;
    const src = cityRestored[id], c = document.createElement('canvas'); c.width = src.width; c.height = src.height; const g = c.getContext('2d');
    g.filter = 'saturate(1.15) contrast(1.05)'; g.drawImage(src, 0, 0); g.filter = 'none';
    g.globalCompositeOperation = 'source-atop'; g.fillStyle = 'rgba(255,190,70,.2)'; g.fillRect(0, 0, c.width, c.height);
    gildCache[id] = { v: groundVer, c }; return c;
  }
  function prepareCity() {
    groundVer++;
    if (!img.ruined) return;
    const c = document.createElement('canvas'); c.width = W; c.height = CITY_H; const g = c.getContext('2d');
    g.drawImage(img.ruined, 0, 0, W, CITY_H);
    // one mask in one pass: clear at the very top, solid from 22 px down
    const fade = g.createLinearGradient(0, 0, 0, 22); fade.addColorStop(0, 'rgba(0,0,0,0)'); fade.addColorStop(1, 'rgba(0,0,0,1)');
    g.globalCompositeOperation = 'destination-in'; g.fillStyle = fade; g.fillRect(0, 0, W, CITY_H);
    cityRuined = c;
    if (!img.restored) return;
    for (const id of C.BUILDING_IDS.concat('plaza')) {
      const s = CITY.spots[id]; if (!s) continue;
      const bx = (s[0] - CITY.crop[0]) * K, by = (s[1] - CITY.crop[1]) * K, rx = s[2] * K, ry = s[3] * K;
      const p = document.createElement('canvas'); p.width = W; p.height = CITY_H; const q = p.getContext('2d');
      q.drawImage(img.restored, 0, 0, W, CITY_H); q.globalCompositeOperation = 'destination-in';
      q.save(); q.translate(bx, by); q.scale(1, ry / rx);
      const rg = q.createRadialGradient(0, 0, rx * 0.74, 0, 0, rx); rg.addColorStop(0, 'rgba(0,0,0,1)'); rg.addColorStop(1, 'rgba(0,0,0,0)');
      q.fillStyle = rg; q.beginPath(); q.arc(0, 0, rx, 0, 7); q.fill(); q.restore();
      cityRestored[id] = p;
    }
  }

  // ---------- looks from the Shop: the originals stay untouched, each skin is graded once and cached ----------
  const orig = {}, lookCache = {};
  const GROUND = ['ruined', 'restored', 'meadow', 'wall', 'forest'];
  function applyLooks() {
    const town = P.shop.equip.town;
    for (const n of GROUND) if (orig[n]) img[n] = lookCache[town + n] || (lookCache[town + n] = L.townGrade(town, orig[n]));
    for (const e of ['fire', 'frost', 'storm']) for (const t of [1, 2, 3]) {
      const id = P.shop.equip[e], key = id + t; if (orig[e + t]) img[e + t] = lookCache[key] || (lookCache[key] = L.towerGrade(id, orig[e + t]));
    }
    // Earth towers: the Frost Spire's crystal tiers recoloured in amber and moss (made in code from the shipped sheet)
    for (const t of [1, 2, 3]) if (orig['frost' + t]) img['earth' + t] = lookCache['earth' + t] || (lookCache['earth' + t] = L.grade(orig['frost' + t], (h, s, l) => s < 0.25 ? [30, 0.25 + s, l * 0.85] : [h > 150 && h < 260 ? 38 : 95, Math.min(1, s * 0.8), l * 0.9]));
    prepareCity();
    const bd = el('backdrop'); if (bd) bd.style.filter = `blur(26px) brightness(.42) saturate(1.15) ${(L.TOWN[town] || {}).backdrop || ''}`;
  }

  // Enemies get a dark outline once at load, so a green slime always reads against green grass.
  function outline() {
    for (const n of ['slime', 'beetle', 'golem']) { const im = img[n]; if (!im) continue;
      const c = document.createElement('canvas'); c.width = im.width; c.height = im.height; const g = c.getContext('2d');
      // a solid dark rim about 2.5 px wide at play size (5 sheet px): enemies are drawn at about 0.45 of the sheet,
      // so a hairline here would vanish (fresh-eyes review, Nov 5 2026: the grey golem disappeared on the grass)
      const sil = document.createElement('canvas'); sil.width = im.width; sil.height = im.height; const sg = sil.getContext('2d');
      sg.drawImage(im, 0, 0); sg.globalCompositeOperation = 'source-in'; sg.fillStyle = 'rgb(34,14,24)'; sg.fillRect(0, 0, im.width, im.height);
      for (let a = 0; a < 16; a++) for (const r of [2.5, 5]) g.drawImage(sil, Math.cos(a * Math.PI / 8) * r, Math.sin(a * Math.PI / 8) * r);
      g.drawImage(im, 0, 0); img[n] = c; }
  }
  // Draw a sprite from its own sheet, anchored bottom-centre on (x, baseY); pad = the sheet's clear margin.
  function sprite(im, x, baseY, h, frame = 0, frames = 1) {
    const pad = 8, fw = im.width / frames, ch = im.height - 2 * pad, s = h / ch;
    cx.drawImage(im, frame * fw, 0, fw, im.height, x - fw * s / 2, baseY - (ch + pad) * s, fw * s, im.height * s);
  }
  function shadow(x, y, rx, ry, a) {
    cx.save(); cx.translate(x, y); cx.scale(1, ry / rx);
    const g = cx.createRadialGradient(0, 0, 1, 0, 0, rx); g.addColorStop(0, `rgba(20,24,10,${a})`); g.addColorStop(1, 'rgba(20,24,10,0)');
    cx.fillStyle = g; cx.beginPath(); cx.arc(0, 0, rx, 0, 7); cx.fill(); cx.restore();
  }
  function glow(x, y, r, rgba, a, c = cx) {
    const g = c.createRadialGradient(x, y, 0, x, y, r); g.addColorStop(0, rgba + a + ')'); g.addColorStop(1, rgba + '0)');
    c.fillStyle = g; c.beginPath(); c.arc(x, y, r, 0, 7); c.fill();
  }

  // Towers stand on the painted platforms: a contact shadow on the stone, a slow breath, a kick and a flash when they
  // fire, and their element's light at the crest. Dormant (grey) until their building is restored; a tier taller per stage.
  function drawTowers() {
    const els = elements(), stage = C.stageOf(P.gateLevel), idle = ['fire', 'frost', 'frost', 'storm'], calm = reduced();
    TOWERS.forEach((t, i) => {
      if (t.beacon) return drawBeacon(t, els.length ? els[(t.shots || 0) % els.length] : null, calm);
      const tier = Math.min(3, stage), e = els.length ? els[i % els.length] : null, im = img[(e || idle[i % 4]) + tier]; if (!im) return;
      const grow = Math.min(0.16, (P.gateLevel - C.STAGES[stage - 1]) * 0.012);   // a little taller with every level of the stage
      const breathe = calm ? 0 : Math.sin(S.t * 2 + i) * 0.012, h = CITY.towerH * (1 + (tier - 1) * 0.1 + grow) * (t.gate ? 0.86 : 1) * (1 + breathe - t.recoil * 0.05);
      shadow(t.x + 5, t.y + 2, CITY.platformR * 0.95, CITY.platformR * 0.36, 0.42);
      if (!e) cx.filter = 'grayscale(0.85) brightness(0.8)';
      sprite(im, t.x, t.y + CITY.towerSink, h);
      cx.filter = 'none';
      if (t.flash > 1) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(t.x, t.y - h * 0.5, 60, 'rgba(255,230,150,', 0.5 * (t.flash - 1) * 5); cx.restore(); }
      if (stage >= 4) drawCrown(t.x, t.y - h - 6, stage, i);
      if (e) {
        cx.save(); cx.globalCompositeOperation = 'lighter';
        glow(t.x, t.y - h * 0.78, 24 + t.flash * 16, (L.TOWER[P.shop.equip[e]] || {}).glow || GLOW[e], 0.16 + (calm ? 0 : 0.05 * Math.sin(S.t * 3 + i)) + t.flash * 0.4);
        cx.restore();
      }
    });
  }

  // the Watchtower's beacon: an element-coloured flame in the tower's open lantern room, flaring when it fires
  function drawBeacon(t, e, calm) {
    const col = e ? ((L.TOWER[P.shop.equip[e]] || {}).glow || GLOW[e]) : 'rgba(255,214,107,', pulse = calm ? 0 : Math.sin(S.t * 4) * 0.08;
    cx.save(); cx.globalCompositeOperation = 'lighter'; glow(t.x, t.y, 34 + t.flash * 26, col, 0.35 + pulse + t.flash * 0.5); cx.restore();
    cx.save(); cx.translate(t.x, t.y); cx.scale(1.5, 1.5);
    cx.fillStyle = e ? COLOR[e] : '#ffd56b'; cx.beginPath(); cx.arc(0, 0, 5.5 + t.flash * 2, 0, 7); cx.fill();
    cx.fillStyle = '#fff6d8'; cx.beginPath(); cx.arc(0, -1, 2.4, 0, 7); cx.fill();
    cx.restore();
    if (C.stageOf(P.gateLevel) >= 4) drawCrown(t.x, t.y - 22, C.stageOf(P.gateLevel), 4);
  }
  function drawEnemy(e) {
    const sh = img[e.sheet]; if (!sh) return;
    const fade = Math.min(1, (S.t - e.born) / 0.6), die = e.dying ? 1 - e.dying / 0.45 : 1;
    cx.save(); cx.globalAlpha = fade * die * (e.faded ? 0.28 : 1) * (evIs('fog') && e.y < WALL_FRONT - 330 ? 0.22 : 1);
    shadow(e.x + 4, e.y - 2, e.size * 0.42, e.size * 0.13, 0.35);
    if (e.dying) { cx.translate(e.x, e.y); cx.scale(1 + e.dying, 1 - e.dying * 0.8); cx.translate(-e.x, -e.y); }
    if (GATE_X + e.off < e.x - 20) { cx.translate(e.x, 0); cx.scale(-1, 1); cx.translate(-e.x, 0); }   // face the way it walks
    const TINT = { gold: 'sepia(1) saturate(3.2) hue-rotate(-8deg) brightness(1.15)', shield: 'saturate(0.6) hue-rotate(170deg) brightness(1.05)', split: 'hue-rotate(150deg) saturate(1.3)', mend: 'hue-rotate(-75deg) saturate(0.7) brightness(1.3)', queen: 'hue-rotate(205deg) saturate(1.5) brightness(1.05)', pale: 'saturate(0.3) brightness(1.45)', stone: 'saturate(0.2) brightness(0.8) contrast(1.2)', ghost: 'saturate(0.2) brightness(1.6) hue-rotate(180deg)', thief: 'saturate(0.4) brightness(0.55)', bomb: 'hue-rotate(-95deg) saturate(1.8) brightness(1.05)', totem: 'sepia(0.7) hue-rotate(160deg) saturate(1.4)' };
    if (e.flash > 0) cx.filter = `brightness(${1 + e.flash * 0.9})`;
    else if (e.chill > 0) cx.filter = 'saturate(0.55) brightness(1.12)';
    else if (e.tint) cx.filter = TINT[e.tint];
    sprite(sh, e.x, e.y, e.size, Math.floor(e.frame) % 6, 6);
    cx.filter = 'none'; cx.restore();
    if (e.dying) return;
    if (e.chill > 0) { cx.save(); cx.globalAlpha = 0.5; glow(e.x, e.y - e.size * 0.4, e.size * 0.55, GLOW.frost, 0.35); cx.restore(); }
    if (e.shield) { cx.save(); cx.strokeStyle = 'rgba(170,225,255,.9)'; cx.fillStyle = 'rgba(150,210,255,.18)'; cx.lineWidth = 3; cx.beginPath(); cx.ellipse(e.x, e.y - e.size * 0.45, e.size * 0.62, e.size * 0.58, 0, 0, 7); cx.fill(); cx.stroke(); cx.restore(); }
    if (C.ROLES[e.role].heal && !(e.burn > 0)) { cx.save(); cx.globalAlpha = 0.25 + 0.15 * Math.sin(S.t * 5); cx.strokeStyle = '#9dffb0'; cx.lineWidth = 3; cx.beginPath(); cx.arc(e.x, e.y - e.size * 0.3, 130, 0, 7); cx.stroke(); cx.restore(); }
    if (e.healed) { cx.save(); cx.globalAlpha = e.healed * 2; cx.fillStyle = '#9dffb0'; cx.font = '26px "HG Display", sans-serif'; cx.textAlign = 'center'; cx.fillText('+', e.x + e.size * 0.3, e.y - e.size * 0.8); cx.restore(); }
    if (e.burn > 0 && !reduced() && Math.random() < 0.3) S.fx.push({ x: e.x + (Math.random() - 0.5) * e.size * 0.5, y: e.y - e.size * 0.5, vx: 0, vy: -50, t: 0, life: 0.4, col: COLOR.fire, r: 2.5 });
    if (evIs('fog') && e.y < WALL_FRONT - 330) return;   // in the mist: no bar either
    cx.save(); cx.globalAlpha = Math.min(1, (S.t - e.born) / 0.6) * (e.faded ? 0.3 : 1);   // the bar fades in with its enemy, never alone
    const bw = e.boss ? 120 : 44, by = e.y - e.size - 8;
    cx.fillStyle = 'rgba(42,26,34,.85)'; cx.fillRect(e.x - bw / 2 - 2, by - 2, bw + 4, 9);
    cx.fillStyle = e.boss ? '#ff6a3a' : e.armour ? '#c8c2b4' : '#e5533d'; cx.fillRect(e.x - bw / 2, by, bw * Math.max(0, e.hp / e.max), 5);
    drawEnemyHp(e);
    cx.restore();
  }

  function drawShot(s) {
    cx.save();   // normal blending: added light turns every colour yellow-white on the bright meadow
    L.drawTrail(cx, P.shop.equip.trail, s, COLOR[s.el], GLOW[s.el], glow, S.t);   // the trail the player chose in the Shop
    cx.restore();
  }

  function drawWisp(c, w, k) {
    w.trail.forEach(([x, y], i) => { c.globalAlpha = (i + 1) / w.trail.length * 0.45; c.fillStyle = k.col; c.beginPath(); c.arc(x, y, 3 + i * 1.3, 0, 7); c.fill(); });
    c.globalAlpha = 1; glow(w.x, w.y, 38, k.glow, 0.8, c); c.fillStyle = k.core; c.strokeStyle = k.rim; c.lineWidth = 3; c.beginPath(); c.arc(w.x, w.y, 9, 0, 7); c.fill(); c.stroke();
  }
  // Each kill's Resonance as a wisp with a short trail, flying over the wall into the fountain.
  function drawWisps() {
    cx.save();   // normal blending: added light washes out to white on the bright meadow, pink must stay pink
    for (const w of S.wisps) {
      drawWisp(cx, w, L.WISP[P.shop.equip.wisp] || L.WISP['wisp-rose']);
    }
    cx.restore();
  }
  // A small gold arrow bobs over every restored building the player can afford to level up.
  function drawMarkers() {
    if (S.card || root.__storeCapture) return;   // store-cover captures hide them
    for (const id of P.restored) {
      if (P.res < C.levelCost(P, id, B)) continue;
      // on the building's upper-left corner, clear of its roof and spires (owner's screenshot, Nov 6 2026: the arrow sat on the
      // Frost Spire's crystal with the tip poking out above it); the job badges sit on the upper-right
      const sp = CITY.spots[id], x = sx(sp[0]) - sp[2] * K * 0.5, y = sy(sp[1]) - sp[3] * K * 0.3 + (reduced() ? 0 : Math.sin(S.t * 4 + x) * 3);
      cx.save(); cx.fillStyle = '#ffd56b'; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 4;
      cx.beginPath(); cx.arc(x, y, 19, 0, 7); cx.fill(); cx.stroke();
      cx.fillStyle = '#2a1a22'; cx.beginPath(); cx.moveTo(x, y - 11); cx.lineTo(x + 10, y + 2); cx.lineTo(x + 4, y + 2); cx.lineTo(x + 4, y + 10); cx.lineTo(x - 4, y + 10); cx.lineTo(x - 4, y + 2); cx.lineTo(x - 10, y + 2); cx.closePath(); cx.fill();
      cx.restore();
    }
  }

  // The boss's own health bar across the top of the field, and red pulsing edges while it arrives.
  function drawBossBar() {
    if (S.bossWarn !== undefined && !reduced()) {
      const a = 0.35 * Math.abs(Math.sin(S.bossWarn * 5)) * (1 - S.bossWarn / 2.6);
      const g = cx.createRadialGradient(W / 2, H / 2, H * 0.3, W / 2, H / 2, H * 0.75); g.addColorStop(0, 'rgba(200,20,30,0)'); g.addColorStop(1, `rgba(200,20,30,${a})`);
      cx.fillStyle = g; cx.fillRect(0, 0, W, H);
    }
    const b = S.enemies.find(e => e.boss && !e.dying); if (!b || S.card) return;
    const x = 70, y = 190, w = W - 140, f = Math.max(0, b.hp / b.max);
    cx.save(); cx.fillStyle = 'rgba(42,26,34,.88)'; cx.strokeStyle = '#c9a24a'; cx.lineWidth = 3;
    cx.beginPath(); cx.roundRect(x - 6, y - 6, w + 12, 46, 23); cx.fill(); cx.stroke();
    const g2 = cx.createLinearGradient(x, 0, x + w, 0); g2.addColorStop(0, '#ff3a3a'); g2.addColorStop(1, '#ff9a3a');
    cx.fillStyle = g2; cx.beginPath(); cx.roundRect(x, y, Math.max(16, w * f), 34, 17); cx.fill();
    cx.font = '30px "HG Display", sans-serif'; cx.textAlign = 'center'; cx.lineWidth = 6; cx.strokeStyle = '#2a1a22';
    const label = `${C.ROLES[b.role].label}${bossRank() ? ` · Rank ${bossRank()}` : ''} · ${Math.ceil(f * 100)}%`; cx.strokeText(label, W / 2, y + 28); cx.fillStyle = '#fff4dc'; cx.fillText(label, W / 2, y + 28);
    cx.restore();
  }
  // A Feat: the medal pops in big over the field with turning rays and its name on a ribbon, then flies into the
  // Feats counter, which pulses as it lands. Drawn last, over every number, so nothing hides it.
  function drawMedal() {
    const m = S.medal; if (!m || S.card || root.__storeCapture) return;
    const t = m.t, col = ['', '#d08a4a', '#e8eef4', '#ffd56b'][m.tier], dark = ['', '#7a4316', '#7d8896', '#b0740e'][m.tier];
    const pop = t < 0.3 ? 1.35 * t / 0.3 : t < 0.45 ? 1.35 - (t - 0.3) / 0.15 * 0.35 : 1;
    const fly = t > 1.7 ? Math.min(1, (t - 1.7) / 0.5) : 0, e = fly * fly;
    const tx = 600, ty = 128, x = W / 2 + (tx - W / 2) * e, y = 340 + (ty - 340) * e, sc = pop * (1 - 0.7 * e);
    if (fly >= 1 && !m.landed) { m.landed = true; const c = el('combos'); c.classList.remove('pulse'); void c.offsetWidth; c.classList.add('pulse'); }
    cx.save(); cx.translate(x, y); cx.scale(sc, sc);
    if (!fly) {   // rays and a dark plate behind, so it reads over any fight
      cx.fillStyle = 'rgba(30,14,26,.55)'; cx.beginPath(); cx.arc(0, 0, 96, 0, 7); cx.fill();
      cx.save(); cx.rotate(t * 0.8); cx.fillStyle = m.tier === 3 ? 'rgba(255,213,107,.45)' : 'rgba(255,255,255,.3)';
      for (let i = 0; i < 12; i++) { cx.rotate(Math.PI / 6); cx.beginPath(); cx.moveTo(-8, 44); cx.lineTo(8, 44); cx.lineTo(0, 92); cx.closePath(); cx.fill(); } cx.restore();
    }
    glow(0, 0, 80, m.tier === 3 ? 'rgba(255,213,107,' : 'rgba(255,255,255,', 0.5);
    const g = cx.createRadialGradient(-14, -16, 4, 0, 0, 46); g.addColorStop(0, '#fffbe8'); g.addColorStop(0.35, col); g.addColorStop(1, dark);
    cx.fillStyle = g; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 6; cx.beginPath(); cx.arc(0, 0, 44, 0, 7); cx.fill(); cx.stroke();
    cx.fillStyle = '#2a1a22'; L.star(cx, 0, 2, 26, 11, 5);
    if (!fly) {   // the ribbon with the Feat's name
      cx.font = '34px "HG Display", sans-serif'; cx.textAlign = 'center'; cx.textBaseline = 'middle';
      const w = Math.max(180, cx.measureText(m.name).width + 50);
      cx.fillStyle = '#8a2a4a'; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 4;
      cx.beginPath(); cx.moveTo(-w / 2 - 18, 66); cx.lineTo(-w / 2, 82); cx.lineTo(-w / 2 - 18, 98); cx.lineTo(w / 2 + 18, 98); cx.lineTo(w / 2, 82); cx.lineTo(w / 2 + 18, 66); cx.closePath(); cx.fill(); cx.stroke();
      cx.lineWidth = 6; cx.strokeText(m.name, 0, 83); cx.fillStyle = '#fff4dc'; cx.fillText(m.name, 0, 83);
    }
    cx.restore();
  }

  function drawGateBar() {
    const hp = Math.max(0, S.gateHp / gateMax()), bw = 120, by = sy(CITY.gateBar);
    cx.fillStyle = 'rgba(42,26,34,.85)'; cx.fillRect(GATE_X - bw / 2 - 3, by - 3, bw + 6, 15);
    cx.fillStyle = hp > 0.35 ? '#7bd15a' : '#e5533d'; cx.fillRect(GATE_X - bw / 2, by, bw * hp, 9);
    if (S.gateHit > 0) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(GATE_X, WALL_FRONT + 8, 60, 'rgba(255,90,60,', 0.25 * S.gateHit); cx.restore(); }
  }

  function drawWavePop() {
    const w = S.wavePop; if (!w) return;
    const age = w.t, L = 2.8; if (age > L) { S.wavePop = null; return; }
    const calm = reduced(), pop = calm ? 1 : age < 0.22 ? 0.55 + 0.6 * age / 0.22 : age < 0.36 ? 1.15 - (age - 0.22) / 0.14 * 0.15 : 1;
    const fade = age > L - 0.5 ? Math.max(0, (L - age) / 0.5) : 1;
    cx.save(); cx.globalAlpha = fade; cx.translate(W / 2, 236 - (calm ? 0 : age * 6)); cx.scale(pop, pop);
    cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineJoin = 'round';
    const title = (w.boss ? 'BOSS WAVE ' : S.trial && !w.boss ? 'TRIAL WAVE ' : 'WAVE ') + C.fmt(w.n) + '!';   // event waves are big, but only a boss wave says BOSS
    cx.font = (w.big ? 76 : 66) + 'px "HG Display", sans-serif'; cx.lineWidth = 12; cx.strokeStyle = '#24162c'; cx.strokeText(title, 0, 0);
    const g = cx.createLinearGradient(0, -34, 0, 34); g.addColorStop(0, '#fff3c2'); g.addColorStop(0.55, '#ffd36b'); g.addColorStop(1, '#f08a2c');
    cx.fillStyle = g; cx.fillText(title, 0, 0);
    cx.globalAlpha = fade * Math.max(0, Math.min(1, (age - 0.18) / 0.2));
    // the line under the title fits the stage: long event lines break after the name, then shrink if still too wide
    let fs = w.big ? 38 : 34; cx.font = fs + 'px "HG Display", sans-serif';
    let lines = [w.cheer]; if (cx.measureText(w.cheer).width > 660) { const k = w.cheer.indexOf(': '); if (k > 0) lines = [w.cheer.slice(0, k + 1), w.cheer.slice(k + 2)]; }
    const widest = Math.max(...lines.map(l => cx.measureText(l).width)); if (widest > 660) { fs = Math.floor(fs * 660 / widest); cx.font = fs + 'px "HG Display", sans-serif'; }
    cx.lineWidth = 9; cx.fillStyle = w.big ? '#ffb3f0' : '#bff3ff';
    const extra = (lines.length - 1) * (fs + 4);
    lines.forEach((l, i) => { cx.strokeText(l, 0, 56 + i * (fs + 4)); cx.fillText(l, 0, 56 + i * (fs + 4)); });
    cx.translate(0, extra);
    // what is coming: a portrait per new or special enemy role, named in its counter element's colour (approved fix 3)
    // at most four portraits: the boss and the newest kinds first, so a crowded wave never runs off the stage
    const order = r => C.isBossRole(r) ? 99 : C.ASC_ROLES[r] ? 50 + C.ASC_ROLES[r][0] : C.VARIANT_FROM[r] ? C.VARIANT_FROM[r] / 2 : 1;
    const roles = w.roles.filter(r => r !== 'slime').sort((a, b) => order(b) - order(a)).slice(0, 4), n = roles.length, gap = 156;
    roles.forEach((r, i) => {
      const R = C.ROLES[r], im = img[R.sheet], x = (i - (n - 1) / 2) * gap;
      cx.globalAlpha = fade * Math.max(0, Math.min(1, (age - 0.3) / 0.25));
      cx.fillStyle = 'rgba(42,26,34,.8)'; cx.beginPath(); cx.roundRect(x - 70, 98, 140, 104, 18); cx.fill();
      cx.strokeStyle = 'rgba(201,162,74,.9)'; cx.lineWidth = 3; cx.stroke();
      if (im) { if (r === 'swarm') { sprite(im, x - 18, 160, 34, 0, 6); sprite(im, x + 18, 160, 34, 2, 6); sprite(im, x, 168, 34, 4, 6); } else sprite(im, x, 164, r === 'boss' ? 62 : 52, 0, 6); }
      cx.font = '30px "HG Display", sans-serif'; cx.lineWidth = 6; cx.strokeStyle = '#24162c'; cx.strokeText(R.label, x, 186);
      cx.fillStyle = R.weak ? COLOR[R.weak] : '#ffd56b'; cx.fillText(R.label, x, 186);
    });
    cx.restore();
  }

  function render() {
    const sh = S.shake > 0 && !reduced() ? (Math.random() - 0.5) * 10 * (S.shake / 0.14) : 0;
    cx.setTransform(k, 0, 0, k, sh * k, sh * 0.6 * k);
    // the ground (meadow, treeline, the city and every restored building) comes from one cached canvas; only a building
    // that is fading in right now is drawn on its own. Eleven full-band draws a frame became one (Nov 6 2026, perf pass).
    cx.drawImage(groundLayer(), 0, 0, W, GROUND_H());
    if (S.reveal) { const part = cityRestored[S.reveal.b] && (P.ascensions >= 2 && S.reveal.b !== 'plaza' ? gilded(S.reveal.b) : cityRestored[S.reveal.b]); if (part) { cx.save(); cx.globalAlpha = Math.min(1, S.reveal.t / 0.9); cx.drawImage(part, 0, CITY_Y); cx.restore(); } }
    if (S.reveal && !reduced()) { const s = CITY.spots[S.reveal.b]; if (s && S.reveal.t < 1.2) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(sx(s[0]), sy(s[1]), 95, 'rgba(255,220,140,', 0.5 * (1 - S.reveal.t / 1.2)); cx.restore(); } }
    // the city's Resonance: the fountain flares as wisps land, motes rise from every restored building
    cx.save();
    if (S.plazaGlow > 0) glow(PLAZA.x, PLAZA.y, 80, RES_GLOW, 0.4 * S.plazaGlow);
    if (P.ascensions >= C.ETERNAL_AT && P.restored.length >= 5) glow(PLAZA.x, PLAZA.y - 10, 120, 'rgba(255,224,138,', 0.22 + (reduced() ? 0 : 0.06 * Math.sin(S.t * 1.5)));   // the Eternal City's light
    for (const m of S.motes) { const a = Math.sin(Math.PI * m.t / m.life); glow(m.x, m.y, m.r * 4, RES_GLOW, 0.35 * a); cx.globalAlpha = a; cx.fillStyle = '#fff0fb'; cx.beginPath(); cx.arc(m.x, m.y, m.r * 0.6, 0, 7); cx.fill(); cx.globalAlpha = 1; }
    cx.restore();
    drawMarkers(); drawBadges(); drawCheer();
    // the field, back to front
    for (const e of S.enemies.slice().sort((a, b) => a.y - b.y)) drawEnemy(e);
    if (img.wall) cx.drawImage(img.wall, 0, CITY_Y, W, CITY_H);   // the wall is nearer the camera than anyone outside it
    drawGateBar();
    drawVillagers(); drawGold();
    drawBossBar(); drawStats();
    drawTowers();
    for (const s of S.shots) drawShot(s);
    for (const h of S.hits) L.drawHit(cx, h);
    drawWisps();
    for (const p of S.fx) if (p.dust) { cx.globalAlpha = 0.6 * (1 - p.t / p.life); cx.fillStyle = p.col; cx.beginPath(); cx.arc(p.x, p.y, p.r * (1 + p.t * 2), 0, 7); cx.fill(); }
    cx.globalAlpha = 1;
    cx.save(); cx.globalCompositeOperation = 'lighter';
    for (const p of S.fx) if (!p.dust) { cx.globalAlpha = 1 - p.t / p.life; cx.fillStyle = p.col; cx.beginPath(); cx.arc(p.x, p.y, p.r, 0, 7); cx.fill(); }
    cx.restore();
    cx.textAlign = 'center'; cx.textBaseline = 'alphabetic'; cx.lineJoin = 'round';
    for (const p of S.pops) {
      if (root.__storeCapture && p.text && !p.combo) continue;   // covers and trailers: words, not number soup
      if (p.arc) {
        const [x0, y0, x1, y1] = p.arc, mx = (x0 + x1) / 2 + (Math.random() - 0.5) * 30, my = (y0 + y1) / 2 + (Math.random() - 0.5) * 30;
        cx.save(); cx.strokeStyle = COLOR.storm; cx.lineWidth = 3; cx.shadowColor = COLOR.storm; cx.shadowBlur = 10;
        cx.beginPath(); cx.moveTo(x0, y0); cx.lineTo(mx, my); cx.lineTo(x1, y1); cx.stroke(); cx.restore(); continue;
      }
      cx.globalAlpha = p.hold ? (p.t < p.life * p.hold ? 1 : 1 - (p.t - p.life * p.hold) / (p.life * (1 - p.hold))) : 1 - p.t / p.life;
      const grow = p.combo ? Math.min(1, 0.6 + p.t * 4) : 1; cx.font = `${Math.round(p.size * grow)}px "HG Display", sans-serif`;
      const py = p.y - (p.combo ? p.t * 18 : p.t * 40);
      cx.lineWidth = p.combo ? 10 : 6; cx.strokeStyle = '#2a1a22'; cx.strokeText(p.text, p.x, py); cx.fillStyle = p.col; cx.fillText(p.text, p.x, py);
      cx.globalAlpha = 1;
    }
    for (const r of S.rings) { cx.save(); cx.globalAlpha = 1 - r.t / 0.5; cx.strokeStyle = r.col; cx.lineWidth = 8 * (1 - r.t / 0.5) + 2; cx.beginPath(); cx.arc(r.x, r.y, 20 + r.t * 260, 0, 7); cx.stroke(); cx.restore(); }
    if (S.flash && !reduced()) { cx.save(); cx.globalAlpha = 0.16 * (1 - S.flash.t / 0.18); cx.fillStyle = S.flash.col; cx.fillRect(0, 0, W, H); cx.restore(); }
    if (!S.card) drawWavePop();
    drawMedal();   // one voice at a time: nothing banners behind a card
    if (S.reveal && (S.reveal.t += 1 / 60) > 1.4) S.reveal = null;
  }

  // ---------- DOM UI ----------
  const el = id => document.getElementById(id);
  let toastT = 0;
  function toast(text) { const t = el('toast'); t.textContent = text; t.classList.add('show'); toastT = 2.8; }
  // set markup only when it changes: ui() runs every frame
  const html = (node, h) => { if (node._h !== h) { node._h = h; node.innerHTML = h; } };
  function ui() {
    el('wave').textContent = `Wave ${C.fmt(P.wave)}`;
    document.documentElement.classList.toggle('calm', P.settings.reduced);   // the in-game Reduced motion switch stills CSS motion too
    el('res').textContent = C.fmt(P.res);
    el('unit').hidden = !P.firstSession;   // after the opening the crystal says it, and the top bar has room for the Shop
    el('city').textContent = `City ${P.restored.length}/10`;
    el('combos').textContent = P.combos.length < comboTotal() ? `Combos ${P.combos.length}/${comboTotal()}` : `Feats ${P.feats.length}/${C.FEATS.length}`;   // a collection to finish, always in view
    el('shop').hidden = true;   // the Shop lives in the Market now
    if (P.shop.open) C.questsFor(P, today());
    el('quests').hidden = true;   // quests and Feats live in the Library
    // the Surge button: running (x2 and time left), ready (free, or an ad in Full Launch), or charging
    const sb = el('surge'), now = Date.now(); sb.hidden = P.firstSession;
    if (!sb.hidden) {
      const on = C.surgeOn(P, now), ready = !on && (SDK.rewardsOn || C.surgeReadyFree(P, now));
      sb.className = on ? 'on' : ready ? 'ready' : 'wait';
      html(sb, on ? `<b>x2</b><span>${mmss(P.surge.until - now)}</span>` : ready ? `${SDK.rewardsOn ? '<span class="play"></span>' : ''}<b>Surge</b><span>x2</span>`
        : `<b>Surge</b><span>${mmss(P.surge.until + C.surgeCooldown(P) - now)}</span>`);
      if (ready && !S.surgeTold) { S.surgeTold = true; toast('Surge ready: tap it for double Resonance'); }
    }
    el('rate').hidden = RATE <= 0; el('rateN').textContent = `+${rfmt(RATE)}/s`;
    html(el('stars'), `<i class="star gold"></i>${P.perkPts || ''}`); el('stars').hidden = true;   // perks live in the Watchtower
    if (P.perkPts && P.ascensions && !S.perkTold && !S.card) { S.perkTold = true; toast('Perk points to spend: tap the Watchtower'); S.pointAt = { id: 'watchtower', t: 0 }; }
    if (P.shop.open && C.questClaimable(P) && !S.questTold && !S.card) { S.questTold = true; toast('Quest done! Collect it at the Library'); S.pointAt = { id: 'library', t: 0 }; }
    el('stars').classList.toggle('news', C.PERKS.some(k => (P.perks[k.id] || 0) < k.max && P.perkPts >= C.perkCost(P, k.id)));
    const fork = C.pendingFork(P), uc = C.upgradeCost(P.gateLevel, B), up = el('up');
    up.querySelector('.label').textContent = fork ? 'New path!' : 'Upgrade';
    const gem = '<i class="gem sm"></i>', dmgUp = Math.round((Math.pow(1.235, 1) - 1) * 100);
    html(up.querySelector('.cost'), fork ? `Stage ${fork}` : `${gem}${C.fmt(uc)} <span class="what">· damage +${dmgUp}%</span>`);
    up.querySelector('.fill').style.width = fork ? '100%' : Math.min(100, P.res / uc * 100) + '%';
    up.classList.toggle('ready', !!fork || P.res >= uc);
    const re = el('re'), whole = C.cityWhole(P), asc = C.canAscend(P), n = P.restored.length, rc = whole ? 0 : C.restoreCost(n), aw = C.ascendWave(P), rw = Math.max(P.runWave || 1, P.wave);
    re.classList.toggle('ascend', whole);
    re.querySelector('.label').textContent = whole ? 'Ascend' : 'Restore';
    const offer = asc ? [] : C.restoreOffer(P);
    html(re.querySelector('.cost'), asc ? `x${C.ASCEND_MULT} forever` : whole ? `at wave ${aw} · now ${rw}` : `${offer.length === 1 ? C.byId(offer[0]).name : 'Pick 1 of 2'} · ${gem}${C.fmt(rc)}`);
    re.querySelector('.fill').style.width = asc ? '100%' : whole ? Math.min(100, rw / aw * 100) + '%' : Math.min(100, P.res / rc * 100) + '%';
    re.classList.toggle('ready', asc || (!whole && P.res >= rc));
    for (const b of [up, re]) if (b.classList.contains('ready')) b.querySelector('.wait').textContent = '';
    // pointing hand: first session only, one button at a time, never while a card is open
    let target = null;
    if (P.firstSession && !S.card) {
      // restore first when it is affordable (it changes the fight); otherwise keep pointing at an affordable Upgrade
      if (n < C.FIXED_ORDER && P.res >= rc && (P.gateLevel > 1 || n > 0)) target = 're';
      else if (S.sessionT > 5 && P.res >= uc && !fork && (n >= C.FIXED_ORDER || (S.upsSinceRestore || 0) < 2)) target = 'up';
      if (n >= C.FIXED_ORDER) { P.firstSession = false; P.daily.day = P.daily.day || C.dayKey(new Date()); save(); }
    }
    // after the opening: point once at a building that can grow, until the player has levelled one
    let spot = null;
    if (S.pointAt && (S.pointAt.t += 1 / 60) < 10 && !S.card && !target) { const [bx, by] = badgeAt(S.pointAt.id); spot = { hx: bx, hy: by }; target = 'bld'; }
    if (!target && !P.firstSession && !P.grown && !S.card && P.restored.length >= C.FIXED_ORDER && (toastT <= 0 || S.growTip)) {
      const id = P.restored.find(b => P.res >= C.levelCost(P, b, B));
      if (id && (S.growTipT || 0) < 20) { spot = CITY.spots[id]; target = 'bld'; if (!S.growTip) { S.growTip = true; S.growTipT = 0; toast('Tap a building to grow its Resonance'); } }
    }
    const hand = el('hand'); hand.className = target ? 'on ' + target : '';
    hand.style.left = spot ? ((spot.hx !== undefined ? spot.hx : sx(spot[0])) - 48) + 'px' : '';
    hand.style.top = spot ? ((spot.hy !== undefined ? spot.hy + 40 : sy(spot[1]) + 6)) + 'px' : '';   // finger up, from under the building or badge
  }

  // ---------- loop (never pauses on tab switch; only the player pauses) ----------
  let acc = 0, last = performance.now();
  function advance(now) {
    acc += Math.min(10, (now - last) / 1000); last = now;
    let n = 0; while (acc >= DT && n < 600) { if (!S.paused && !S.card) step(); acc -= DT; n++; }
    if ((toastT -= n * DT) <= 0) el('toast').classList.remove('show');
  }
  function frame(now) { advance(now); render(); ui(); if (S.card === 'shop') drawPreviews(now / 1000); requestAnimationFrame(frame); }
  setInterval(() => { if (document.hidden) advance(performance.now()); }, 250);
  document.addEventListener('visibilitychange', () => { SDK.gameplay(!document.hidden && !S.paused); if (document.hidden) { flushWisps(); save(); } });
  root.addEventListener('pagehide', () => { flushWisps(); save(); });

  // ---------- stage fit ----------
  el('stage').style.height = H + 'px'; cv.style.height = H + 'px';
  function fit() {
    const vw = root.innerWidth, vh = root.innerHeight, f = Math.min(vw / W, vh / H);
    el('stage').style.transform = `translate(${(vw - W * f) / 2}px,${(vh - H * f) / 2}px) scale(${f})`;
    k = Math.max(0.5, Math.min(2, f * (root.devicePixelRatio || 1)));
    cv.width = Math.round(W * k); cv.height = Math.round(H * k);
    placeSides(vw, vh, W * f);
  }
  // ---------- desktop side panels (fresh-eyes review, Nov 6 2026: on a wide window the portrait stage left two thirds
  // of the screen as blurred filler). When there is room, the city's buildings sit on the left (level them with a click)
  // and the goals on the right (next step, quests, the next star, collections). Phones never see them. ----------
  const sides = [el('sideL'), el('sideR')].filter(Boolean);
  let sidesOn = false;
  function placeSides(vw, vh, sw) {
    const room = (vw - sw) / 2; sidesOn = sides.length === 2 && room >= 250 && vh >= 420;
    sides.forEach((p, i) => { p.hidden = !sidesOn || P.firstSession; if (!sidesOn) return;
      const z = Math.max(0.75, Math.min(1.5, vh / 720)), w = Math.min(360, (room - 36) / z);   // panels grow with the window like the stage
      p.style.width = w + 'px'; p.style.maxHeight = (vh - 40) / z + 'px'; p.style.transform = `translateY(-50%) scale(${z})`;
      p.style.left = (i === 0 ? (room - w * z) / 2 : vw - room + (room - w * z) / 2) + 'px'; });
    if (sidesOn) drawSides();
  }
  function drawSides() {
    if (!sidesOn) return;
    // a brand-new player sees only the fight and one pointed button: the panels arrive once the opening is over
    for (const p of sides) if (p.hidden !== !!P.firstSession) p.hidden = !!P.firstSession;
    if (P.firstSession) return;
    const gem = '<i class="gem sm"></i>', offer = C.restoreOffer(P), rc = C.cityWhole(P) ? 0 : C.restoreCost(P.restored.length);
    const rows = C.BUILDINGS.map(b => {
      if (P.restored.includes(b.id)) { const L = C.levelOf(P, b.id), c = C.levelCost(P, b.id, B);
        return `<div class="srow on"><span class="sn">${b.name}<small>Lv ${L} · +${rfmt(C.buildingRate(P, b.id, B) * surgeX())}/s</small></span><button class="slv ${P.res >= c ? 'can' : ''}" data-lv="${b.id}" aria-label="Level up ${b.name}">${gem}${C.fmt(c)}</button></div>`; }
      const next = offer.includes(b.id);
      return `<div class="srow${next ? ' next' : ''}"><span class="sn">${b.name}<small>${next ? `Restore next · ${C.fmt(rc)}` : 'In ruins'}</small></span></div>`; }).join('');
    html(el('sideL'), `<h3>The city <small>${P.restored.length}/10</small></h3>${rows}<p class="shint">Click a level button, or a building in the city.</p>`);
    const rw = Math.max(P.runWave || 1, P.wave), aw = C.ascendWave(P);
    const step = C.canAscend(P) ? '<b>Ascend is ready!</b> Press Restore.' : C.cityWhole(P) ? `Reach wave <b>${aw}</b> to Ascend (now ${rw}).` : `Restore ${offer.map(id => C.byId(id).name).join(' or ')} for ${gem}${C.fmt(rc)}.`;
    const stage = C.stageOf(P.gateLevel), nextStage = C.STAGES[stage];
    const qs = P.shop.open && P.quests.list.length ? `<h4>Today's quests</h4>${P.quests.list.map(q => `<div class="sq${q.claimed ? ' done' : ''}"><span>${C.questText(q)}</span><b>${Math.min(q.n, q.need)}/${q.need}</b></div>`).join('')}` : '';
    const star = P.ascensions >= 1 ? C.STARS.find(st => st.asc === P.ascensions && !P.stars.includes(st.id)) : null;
    html(el('sideR'), `<h3>Goals</h3><div class="sstep">${step}</div>`
      + `<div class="sline">Gate level <b>${P.gateLevel}</b>${nextStage ? ` · stage ${stage + 1} at level ${nextStage}` : ' · top stage'}</div>`
      + `<div class="sline">Best wave <b>${C.fmt(P.bestWave)}</b> · this run ${rw}</div>`
      + (star ? `<div class="sline">Next star: <b>${star.name}</b> at wave ${C.starWave(P, star.k)}</div>` : '') + qs
      + `<h4>Collections</h4><div class="sgrid"><span>Feats<b>${P.feats.length}/${C.FEATS.length}</b></span><span>Combos<b>${P.combos.length}/${comboTotal()}</b></span>`
      + `<span>Enemies met<b>${(P.seenRoles || []).length}</b></span><span>Ascension<b>${P.ascensions}/${C.ETERNAL_AT}</b></span>${P.ascensions >= 1 ? `<span>Stars<b>${P.stars.length}/${C.STARS.length}</b></span>` : ''}</div>`);
  }
  if (sides[0]) sides[0].addEventListener('click', ev => { const b = ev.target.closest('[data-lv]'); if (!b || S.card) return; A.unlock(); if (levelUp(b.dataset.lv)) drawSides(); else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); } });
  root.addEventListener('resize', fit);

  // ---------- boot ----------
  function bind() {
    el('up').addEventListener('click', upgrade); el('re').addEventListener('click', restore); el('surge').addEventListener('click', surgeTap); el('stats').addEventListener('click', toggleStats); el('stats').classList.toggle('on', P.settings.stats); el('stars').addEventListener('click', () => { A.unlock(); if (!S.card && P.ascensions > 0) showCard('perks'); }); el('quests').addEventListener('click', () => { A.unlock(); if (!S.card && P.shop.open) showCard('quests'); });
    el('shop').addEventListener('click', () => { if (S.card === 'shop') { const x = el('card').querySelector('#c-no'); if (x) x.click(); } else openShopCard(); });
    el('pause').addEventListener('click', () => {
      A.unlock();
      if (S.card === 'settings') { el('card').querySelector('#c-ok').click(); return; }
      if (S.card) return;
      S.paused = true; el('pause').classList.add('on'); SDK.gameplay(false); showCard('settings');
    });
    root.addEventListener('keydown', e => {
      if (e.repeat) return; const key = e.key.toLowerCase();
      if (S.card) { if (key === 'enter') { const b = el('card').querySelector('#c-ok'); if (b) { e.preventDefault(); b.click(); } } return; }
      if (key === 'u') upgrade(); else if (key === 'r') restore(); else if (key === 'p') el('pause').click(); else if (key === 's') openShopCard(); else if (key === 'q' && P.shop.open && !S.card) showCard('quests'); else if (key === 'x') surgeTap(); else if (key === 'i') toggleStats();
    });
    root.addEventListener('pointerdown', () => A.unlock(), { once: true });
    // tap a restored building to level it up; a ruined one says what it will do once restored
    cv.addEventListener('click', ev => {
      if (S.card) return; const r = cv.getBoundingClientRect(), x = (ev.clientX - r.left) / r.width * W, y = (ev.clientY - r.top) / r.height * H;
      if (tapGold(x, y)) return;
      for (const id in HALLS) { const [bx, by] = badgeAt(id); if (HALLS[id].ok() && Math.hypot(x - bx, y - by) < 46 && openHall(id)) return; }
      for (const id of C.BUILDING_IDS) { const s = CITY.spots[id]; const dx = (x - sx(s[0])) / (s[2] * K * 0.8), dy = (y - sy(s[1])) / (s[3] * K * 0.8);
        if (dx * dx + dy * dy < 1) { A.unlock(); if (P.restored.includes(id)) showCard('building', id); else if (!openHall(id)) { const b = C.byId(id); toast(`${b.name} (ruined). Restore it: ${b.line}`); } return; } }
    });
    if (matchMedia('(hover: hover) and (pointer: fine)').matches) document.documentElement.classList.add('keys');
  }
  function returnCards() {
    // the daily reward is the first thing a returning player sees, then what the gate earned while they were away
    const now = Date.now(), d = C.dailyDue(P, now), off = C.offlineEarnings(P, B, now);
    if (d) cardQueue.push(['daily', d]);
    if (off.res > 0 && !P.firstSession) cardQueue.push(['offline', off]);
    nextCard();
  }
  fit(); bind(); A.set('music', P.settings.music); A.set('sfx', P.settings.sfx); startWave();
  const T = s => `assets/towers/${s}.webp`;
  Promise.all([loadImg('ruined', 'assets/city/city-ruined.webp'), loadImg('restored', 'assets/city/city-restored.webp'),
    loadImg('meadow', 'assets/ground/meadow.webp'), loadImg('wall', 'assets/city/city-wall.webp'), loadImg('forest', 'assets/kit/forest-edge-top.webp'),
    loadImg('slime', 'assets/enemies/slime-walk.webp'), loadImg('beetle', 'assets/enemies/beetle-walk.webp'), loadImg('golem', 'assets/enemies/golem-walk.webp'),
    ...['fire', 'frost', 'storm'].flatMap(e => [1, 2, 3].map(t => loadImg(e + t, T(`${e}-${t}`))))])
    .then(() => { outline(); for (const n of GROUND.concat(['fire', 'frost', 'storm'].flatMap(e => [e + 1, e + 2, e + 3]))) orig[n] = img[n]; applyLooks(); el('boot').remove(); SDK.loadingStop(); SDK.gameplay(true); last = performance.now(); checkFeats(true); returnCards(); openShop(); /* after the return cards: opening saves, which stamps lastSeen */ requestAnimationFrame(frame); });
})(typeof globalThis !== 'undefined' ? globalThis : this);
