// Hearthgate: hold the gate, and the city rebuilds itself behind you.
// Portrait stage 720x1280: the field on top, the city's north wall with its gatehouse and four tower platforms, the city,
// then Upgrade and Restore. CrazyGames' smallest desktop frame (800x450) shows the stage at 0.35, so every label is at
// least 30 design px. The simulation runs on a fixed 60 Hz step and never pauses on a tab switch; only the player pauses.
(function (root) {
  'use strict';
  const C = root.HGCore, A = root.HGAudio || { play() {}, music() {}, unlock() {}, set() {} };
  const SDK = root.GameSDK || { gameplay() {}, loadingStop() {}, happytime() {}, rewarded: () => Promise.resolve(false), rewardsOn: false };
  const W = 720, H = Math.round(Math.max(1280, Math.min(1560, 720 * root.innerHeight / Math.max(1, root.innerWidth)))), DT = 1 / 60, SAVE_KEY = 'hearthgate.profile.v1', BUILD = '2026-11-05e';

  // ---------- layout from the city painting (art/masters/city-north-gate, cropped at exactly half scale) ----------
  const CITY = C.CITY, CITY_H = CITY.band[1], CITY_Y = H - 168 - CITY_H, K = CITY.band[0] / CITY.crop[2];
  const sx = x => (x - CITY.crop[0]) * K, sy = y => CITY_Y + (y - CITY.crop[1]) * K;
  const GATE_X = sx(CITY.gate[0]), WALL_FRONT = sy(CITY.wallTop);          // enemies press against the wall here
  const PLATFORMS = CITY.platforms.map(([x, y]) => ({ x: sx(x), y: sy(y) })); // the four round platforms in the wall
  const FIELD_TOP = 250;   // nothing that moves or floats goes above this line: the HUD lives there
  const RANGE_Y = 196;   // towers reach the whole field below the treeline, on every screen height
  const ELEMENT = { forge: 'fire', frostSpire: 'frost', stormBell: 'storm' };
  const COLOR = { fire: '#ff8a3a', frost: '#9fe8ff', storm: '#c98bff', none: '#efe2c0' };
  const L = root.HGLooks, SHARD = '#6ff2ff';   // Starshards: the Shop's currency, a bright cyan star
  const RES = '#ff8ae6', RES_GLOW = 'rgba(255,110,220,';   // Resonance: the city's magic, rose-pink so it reads on green grass
  const PLAZA = { x: sx(CITY.focus[0]), y: sy(CITY.focus[1]) };   // the fountain every wisp flies into
  const GLOW = { fire: 'rgba(255,140,50,', frost: 'rgba(140,220,255,', storm: 'rgba(200,140,255,', none: 'rgba(255,240,200,' };

  // ---------- state ----------
  let P = load(), B = C.bonuses(P);
  const S = { t: 0, enemies: [], shots: [], pops: [], fx: [], hits: [], wisps: [], motes: [], spawnQ: [], spawnT: 0, gateHp: 0, gateHit: 0, paused: false,
    sessionT: 0, saveT: 0, reveal: null, card: null, rings: [], shake: 0, wavePop: null, cheerNext: null, savedByAd: false };
  function load() { try { return C.sanitize(localStorage.getItem(SAVE_KEY)); } catch (e) { return C.defaults(); } }
  function save() { P.lastSeen = Date.now(); try { localStorage.setItem(SAVE_KEY, JSON.stringify(P)); } catch (e) {} }
  let RATE = C.cityRate(P, B);   // the city's Resonance per second, recomputed whenever the city or a bonus changes
  const refresh = () => { B = C.bonuses(P); RATE = C.cityRate(P, B); };
  const rfmt = r => r < 10 ? String(+r.toFixed(r < 1 ? 2 : 1)) : C.fmt(r);
  root.Hearthgate = { reloadSave() { P = load(); refresh(); S.enemies = []; S.wisps = []; startWave(); }, state: () => ({ P, S, B }) };
  // QA only (?qa in the address): run the simulation fast and drive the buttons, for the pacing bot in tools/qa.
  if (/[?&]qa\b/.test(location.search)) Object.assign(root.Hearthgate, {
    sim(sec) { for (let i = 0; i < sec * 60; i++) if (!S.paused) step(); },
    act(what, id) { if (what === 'up') upgrade(); if (what === 're') restore(); if (what === 'pick') { const o = document.querySelector('#card .opt' + (id ? `[data-id="${id}"]` : '')); if (o) o.click(); } if (what === 'ok') { const o = document.querySelector('#card #c-ok'); if (o) o.click(); } if (what === 'level') levelUp(id); if (what === 'shop') openShopCard(); if (what === 'buy') { const b = document.querySelector(`#card .buy[data-id="${id}"]`); if (b) b.click(); } if (what === 'tab') { const b = document.querySelector(`#card .tab[data-tab="${id}"]`); if (b) b.click(); } if (what === 'close') { const o = document.querySelector('#card #c-no'); if (o) o.click(); } },
    card: () => S.card
  });

  const gateMax = () => C.gateMax(P, B);
  const elements = () => P.restored.map(b => ELEMENT[b]).filter(Boolean);
  const reduced = () => P.settings.reduced || matchMedia('(prefers-reduced-motion: reduce)').matches;

  // ---------- waves ----------
  const CHEERS = ['Keep it up!', 'Great job!', 'WOOOAHH!', 'Unstoppable!', 'Hold the line!', 'Nice work!', 'You got this!',
    'Crushing it!', 'On fire!', 'Legendary!', 'The city cheers you on!', 'Brick by brick!'];
  function startWave() {
    const n = P.wave, boss = C.isBoss(n);
    const cheer = S.cheerNext || (n === 1 ? ['Good luck!', 'How far will you go?'][Math.floor(Math.random() * 2)]
      : boss ? 'Boss incoming. Hold the gate!' : n % 50 === 0 ? 'HALFWAY TO GLORY!' : CHEERS[(n * 7 + 3) % CHEERS.length]);
    S.cheerNext = null;
    const mix = C.waveMix(n);
    S.wavePop = { n, cheer, big: boss, t: 0, roles: mix.map(([r]) => r) };
    if (boss) { S.bossWarn = 0; S.shake = 0.35; A.play('golem_death', 0.35); }   // a boss wave is an event: red edges, a rumble
    S.gateHp = S.gateHp > 0 ? Math.min(S.gateHp, gateMax()) : gateMax();
    // a shuffled queue, so runners and armoured arrive among the slimes rather than in blocks; the boss comes last
    const q = []; for (const [role, c] of mix) for (let i = 0; i < c; i++) q.push(role);
    for (let i = q.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [q[i], q[j]] = [q[j], q[i]]; }
    S.spawnQ = q.sort((a, b) => (a === 'boss') - (b === 'boss')); S.spawnT = 1.0;
  }
  function spawn(role) {
    const R = C.ROLES[role], hp = C.enemyHp(P.wave, role);
    // enemies step out of the long grass below the HUD with a puff of dust, never from under the top panel
    const lead = (S.packN || 0) % 3 === 0 || role === 'boss' || role === 'swarm';
    const x0 = lead ? (S.packX = 80 + Math.random() * 560) : Math.max(70, Math.min(650, S.packX + (Math.random() - 0.5) * 140)), y0 = FIELD_TOP + (R.size > 150 ? 70 : 0) + Math.random() * 40;
    for (let i = 0; i < (reduced() ? 3 : 9); i++) { const a = Math.random() * Math.PI * 2; S.fx.push({ x: x0 + Math.cos(a) * 20, y: y0 - 6, vx: Math.cos(a) * 50, vy: -20 - Math.random() * 30, t: 0, life: 0.5, col: 'rgba(220,200,150,.9)', r: 4 + Math.random() * 4, dust: true }); }
    S.enemies.push({ role, sheet: R.sheet, x: x0, y: y0, off: (Math.random() - 0.5) * 330, hp, max: hp,
      sp: R.speed * (WALL_FRONT - FIELD_TOP) / 414, size: R.size, armour: R.armour || 0, weak: R.weak, boss: role === 'boss', frame: Math.random() * 6,
      chill: 0, burn: 0, flash: 0, born: S.t, dying: 0 });
  }

  // ---------- simulation ----------
  const TOWERS = PLATFORMS.map((p, i) => ({ x: p.x, y: p.y, cd: 0.2 + i * 0.13, recoil: 0, flash: 0, n: i }));
  function step() {
    if ((S.featT = (S.featT || 0) + DT) > 0.5) { S.featT = 0; checkFeats(); }
    if (S.bossWarn !== undefined && (S.bossWarn += DT) > 2.6) S.bossWarn = undefined;
    if (S.medal && (S.medal.t += DT) > 2.4) S.medal = null;
    S.t += DT; S.sessionT += DT; if (S.growTip) S.growTipT += DT; if (S.flash && (S.flash.t += DT) > 0.18) S.flash = null; if (S.wavePop) S.wavePop.t += DT;
    S.gateHit = Math.max(0, S.gateHit - DT * 4); S.shake = Math.max(0, S.shake - DT);
    for (const r of S.rings) r.t += DT; S.rings = S.rings.filter(r => r.t < 0.5);
    for (const h of S.hits) h.t += DT; S.hits = S.hits.filter(h => h.t < L.HIT_LIFE);
    if (S.spawnQ.length && (S.spawnT -= DT) <= 0) { const r = S.spawnQ.shift(); spawn(r); S.packN = (S.packN || 0) + 1; const gap = Math.max(0.45, 1.35 - P.wave * 0.02);
      S.spawnT = r === 'swarm' ? 0.28 : S.packN % 3 ? 0.3 : gap * 3 - 0.6; }   // packs of three, same total pace
    if (B.regen) S.gateHp = Math.min(gateMax(), S.gateHp + gateMax() * B.regen * DT);
    const els = elements(), interval = C.fireInterval(B);
    for (const t of TOWERS) {
      t.recoil = Math.max(0, t.recoil - DT * 6); t.flash = Math.max(0, t.flash - DT * 5);
      if ((t.cd -= DT) > 0) continue;
      // each tower takes one of the two enemies nearest the gate
      const live = S.enemies.filter(e => !e.dying && e.y > RANGE_Y).sort((a, b) => b.y - a.y);
      if (!live.length) continue;
      const tgt = live[Math.min(live.length - 1, t.n % 2)];
      t.cd = interval; t.recoil = 1; t.flash = 1; t.shots = (t.shots || 0) + 1;
      const el = els.length ? els[(t.shots + t.n) % els.length] : 'none';
      S.shots.push({ x: t.x, y: t.y - 70, tx: tgt, el, sp: 820, trail: [] });
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
      if (e.burn > 0) { e.burn -= DT; e.hp -= C.towerDamage(P, B) * 0.5 * DT * (e.weak === 'fire' ? 2 : 1); }
      e.chill = Math.max(0, e.chill - DT); e.flash = Math.max(0, e.flash - DT * 6);
      const slow = e.chill > 0 ? (e.weak === 'frost' ? 0.3 : 0.45) : 1;
      e.frame += DT * 8 * slow;
      if (e.y < WALL_FRONT) {
        e.y = Math.min(WALL_FRONT, e.y + e.sp * slow * DT);
        if (e.y > 230) e.x += (GATE_X + e.off - e.x) * Math.min(1, 0.8 * DT);
      } else { S.gateHp -= C.enemyBite(P.wave, e.role) * DT; S.gateHit = 1; }
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
  }

  function hit(e, el) {
    let d = C.towerDamage(P, B); const comboMul = 1 + B.combo;
    if (e.armour && el !== 'fire') d *= 1 - e.armour;                 // armour: only Fire bites fully
    if (el === 'fire') {
      if (e.chill > 0) { d *= 3 * comboMul; combo('shatter', e, 'SHATTER!', COLOR.frost); burst(e, COLOR.frost, 22, 260); A.play('shatter_combo', 0.5); e.bigHit = true; }
      e.burn = 2.2;
    }
    if (el === 'frost') { e.chill = 2.2; if (e.weak === 'frost') d *= 1.3; }
    if (el === 'storm') {
      const jumps = e.chill > 0 ? 4 : 2;
      if (e.chill > 0) { combo('frostStorm', e, 'SUPERCHAIN!', COLOR.storm); A.play('conduction_combo', 0.45); }
      if (e.burn > 0) { combo('fireStorm', e, 'FIRESTORM!', COLOR.fire); A.play('freeze_combo', 0.45); }
      const chainD = d * 0.6 * (e.chill > 0 || e.burn > 0 ? comboMul : 1) * (e.weak === 'storm' ? 2 : 1);
      S.enemies.filter(o => o !== e && !o.dying).sort((a, b) => Math.hypot(a.x - e.x, a.y - e.y) - Math.hypot(b.x - e.x, b.y - e.y))
        .slice(0, jumps).forEach(o => { o.hp -= chainD; o.flash = 1; if (e.burn > 0) o.burn = 2; S.pops.push({ arc: [e.x, e.y - e.size * 0.45, o.x, o.y - o.size * 0.45], t: 0, life: 0.2 }); });
    }
    e.hp -= d; e.flash = 1;
    const hs = P.shop.equip.hit;
    burst(e, COLOR[el], el === 'none' ? 4 : hs === 'hit-classic' ? 6 : 3, 120);
    if (hs !== 'hit-classic' && S.hits.length < 30) S.hits.push({ x: e.x, y: e.y - e.size * 0.45, t: 0, col: COLOR[el], style: hs, seed: Math.random() * 6 });
    A.play(el === 'frost' ? 'ice_contact_0' : el === 'storm' ? 'lightning_contact_0' : el === 'fire' ? 'fire_contact_0' : 'ice_contact_0', 0.12);
    if (e.dp && e.dp.t < 0.25 && S.pops.includes(e.dp) && !e.bigHit) { e.dp.val += d; e.dp.text = C.fmt(e.dp.val); }
    else { e.dp = pop(e.x, e.y - e.size * 0.55, C.fmt(d), e.bigHit ? '#ffffff' : COLOR[el], e.bigHit ? 0.9 : 0.7, e.bigHit ? 56 : 30); e.dp.val = d; }
    e.bigHit = false;
  }
  function combo(id, e, word, col) {
    quest('combo'); if (id === 'shatter') quest('shatter');
    // one combo word on screen at a time, so words never pile into each other
    // above the enemy, or below it near the treeline, so it never sits on the +N
    S.pops = S.pops.filter(p => !p.combo);
    S.pops.push({ x: Math.max(150, Math.min(570, e.x)), y: (e.y - e.size - 80 < (S.wavePop ? 500 : FIELD_TOP + 50)) ? Math.max(e.y + 70, S.wavePop ? 500 : 0) : e.y - e.size - 80, text: word, col, t: 0, life: 1.4, size: 58, combo: true, hold: 0.75 });
    S.rings.push({ x: e.x, y: e.y - e.size * 0.45, t: 0, col }); S.shake = 0.14;
    if (id === 'shatter' && S.t - (S.lastFlash || -9) > 1.2) { S.flash = { col, t: 0 }; S.lastFlash = S.t; }   // the signature combo lights the screen
    if (!P.combos.includes(id)) { P.combos.push(id); if (P.shop.open) P.shop.shards += C.SHARDS.combo; toast(`New combo: ${word.replace('!', '')}  ·  ${P.combos.length}/3`); save(); }
  }
  function kill(e) {
    e.dying = DT; const gain = C.killRes(P.wave, e.boss, B) * surgeX(); quest('kills');
    wisp(e, gain); pop(e.x + 28, e.y - e.size * 0.2, '+' + C.fmt(gain), '#ff9ce8', 0.9, 34);
    burst(e, e.sheet === 'golem' ? '#b9b2a2' : e.sheet === 'beetle' ? '#ff9a4a' : '#8fe05a', e.boss ? 30 : 12, e.boss ? 260 : 160);
    if (e.boss || e.role === 'armoured') A.play('golem_death', 0.5);
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
    if (C.isBoss(P.wave)) {
      quest('boss');
      pop(W / 2, 330, 'BOSS DOWN!', '#ffd56b', 1.8, 60); SDK.happytime(); A.play('tier_up', 0.6);
      if (P.shop.open) { P.shop.shards += C.SHARDS.boss; pop(W / 2, 400, `+${C.SHARDS.boss} Starshards`, SHARD, 1.6, 40); }
      if (P.wave === 10 && !P.tomorrowShown) { P.tomorrowShown = true; setTimeout(() => toast('Come back tomorrow: a daily Resonance reward waits'), 1900); }
    }
    P.wave++; P.bestWave = Math.max(P.bestWave, P.wave); openShop(); startWave(); save();
  }
  // The Shop opens when the first boss falls, with a welcome gift; older saves past wave 10 get it on load.
  function openShop() {
    if (P.shop.open || P.bestWave < C.SHOP_OPENS) return;
    P.shop.open = true; P.shop.shards += C.SHARDS.welcome; save();
    setTimeout(() => { toast(`The Shop is open! +${C.SHARDS.welcome} Starshards`); el('shop').classList.add('fresh'); A.play('tier_up', 0.4); }, 2600);
  }
  function gateBroken(noAd) {
    // a boss wave can be saved once by an optional ad (Full Launch only); otherwise the gate falls back, nothing lost
    if (!noAd && C.isBoss(P.wave) && SDK.rewardsOn && !S.savedByAd && !S.card) { S.paused = true; showCard('save'); return; }
    const to = Math.max(1, P.wave - 3);
    toast(`The gate fell back to wave ${to}. Nothing lost.`); S.cheerNext = 'Regroup. You\'ve got this!';
    P.wave = to; S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = gateMax(); S.savedByAd = false; startWave(); save();
  }
  function pop(x, y, text, col, life, size) { const p = { x, y: Math.max(FIELD_TOP - 20 + size, y), text, col, t: 0, life, size }; S.pops.push(p); return p; }
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
    else { pop(GATE_X, WALL_FRONT - 240, `Damage ${C.fmt(before)} -> ${C.fmt(C.towerDamage(P, B))}`, '#ffd56b', 1.2, 38); TOWERS.forEach(t => (t.flash = 1.2)); A.play('tier_up', 0.25); }
    save(); ui();
  }
  function restore() {
    A.unlock();
    if (S.card) return;
    if (C.canAscend(P)) return showCard('ascend');
    const offer = C.restoreOffer(P); if (!offer.length) return;
    const c = C.restoreCost(P.restored.length);
    if (P.res < c) return refuse('re', c);
    if (offer.length > 1) return showCard('pick', offer);
    doRestore(offer[0]);
  }
  function doRestore(id) {
    const c = C.restoreCost(P.restored.length); if (P.res < c) return;
    P.res -= c; P.restored.push(id); refresh(); S.upsSinceRestore = 0;
    const b = C.byId(id); toast(`${b.name} restored: ${b.line}`); S.reveal = { b: id, t: 0 };
    A.play('tier_up', 0.55); SDK.happytime();
    if (P.restored.length === 5) setTimeout(() => { toast('The fountain flows again!'); S.reveal = { b: 'plaza', t: 0 }; }, 2900);
    if (C.canAscend(P)) setTimeout(() => toast('The city is whole! Restore is now Ascend.'), 2900);
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
    else { pop(x, y - 30, `Level ${L}`, RES, 0.9, 34); A.play('tier_up', 0.22); }
    save(); return true;
  }



  // sound is muted only while an ad plays
  const adHooks = { mute: () => A.set('muted', true), unmute: () => A.set('muted', false) };
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
    S.medal = { tier: Math.max(...got.map(f => f.tier)), name: got.length === 1 ? got[0].name : `${got.length} Feats`, t: 0 }; A.play('tier_up', 0.45);
  }
  const surgeX = () => C.surgeOn(P, Date.now()) ? 2 : 1;
  function adDay() { const d = today(); if (P.surge.adDay !== d) { P.surge.adDay = d; P.surge.ads = 0; P.surge.shardAds = 0; } }
  // Basic Launch: free every 15 minutes. Full Launch: a rewarded ad, any time one is not running, 8 a day.
  function surgeTap() {
    A.unlock(); const now = Date.now(); if (C.surgeOn(P, now) || S.card) return;
    const go = () => { C.startSurge(P, Date.now()); quest('surge'); A.play('tier_up', 0.6); toast('Surge! Double Resonance for 3 minutes'); SDK.happytime(); save(); };
    if (SDK.rewardsOn) { adDay(); if (P.surge.ads >= C.SURGE_ADS_PER_DAY) return toast('No more Surges today'); S.paused = true;
      SDK.rewarded(adHooks).then(ok => { S.paused = false; if (ok) { P.surge.ads++; go(); } }); return; }
    if (C.surgeReadyFree(P, now)) go(); else toast(`The next Surge charges in ${mmss(P.surge.until + C.SURGE_COOLDOWN_MS - now)}`);
  }
  const mmss = ms => { const t = Math.max(0, Math.ceil(ms / 1000)); return `${Math.floor(t / 60)}:${String(t % 60).padStart(2, '0')}`; };
  const qTabs = on => `<div class="tabs">${[['daily', 'Daily quests'], ['feats', `Feats ${P.feats.length}/${C.FEATS.length}`]].map(([k, n]) => `<button class="tab qtab${k === on ? ' on' : ''}" data-q="${k}">${n}</button>`).join('')}</div>`;
  function featsHtml() {
    const rows = C.FEATS.map(f => ({ f, done: P.feats.includes(f.id), n: C.featProgress(P, f) }))
      .sort((a, b) => (a.done - b.done) || (b.n / b.f.need - a.n / a.f.need));   // closest unfinished first, finished last
    return `<h2>Feats</h2>${qTabs('feats')}<div class="feats">` + rows.map(({ f, done, n }) => `<div class="feat${done ? ' done' : ''}"><i class="medal t${f.tier}"></i>`
      + `<div class="ft"><b>${f.name}</b><span>${f.text}</span>${done ? '' : `<div class="qbar"><i style="width:${Math.round(n / f.need * 100)}%"></i></div>`}</div>`
      + `<span class="fr">${done ? 'Done' : `${C.fmt(n)}/${C.fmt(f.need)}`}<br><i class="star"></i>${C.FEAT_PAY[f.tier]}</span></div>`).join('')
      + `</div><button id="c-no" class="cbtn quiet">Close</button>`;
  }
  function questsHtml() {
    if (S.qTab === 'feats') return featsHtml();
    const qs = C.questsFor(P, today()), all = qs.list.every(q => q.claimed);
    return `<h2>Quests</h2>${qTabs('daily')}<p class="small">Three new quests every day</p><div class="quests">`
      + qs.list.map((q, i) => `<div class="quest${q.claimed ? ' done' : ''}"><div class="qt"><b>${C.questText(q)}</b><span>${Math.min(q.n, q.need)}/${q.need}</span></div>`
        + `<div class="qbar"><i style="width:${Math.round(q.n / q.need * 100)}%"></i></div>`
        + (q.claimed ? `<span class="qok">Claimed</span>` : `<button class="buy ${q.n >= q.need ? 'can' : 'off'} qclaim" data-i="${i}"><i class="star"></i>${C.QUEST_REWARD}</button>`) + `</div>`).join('')
      + `<div class="quest bonus${qs.bonus ? ' done' : ''}"><div class="qt"><b>All three</b><span>${qs.list.filter(q => q.claimed).length}/3</span></div>`
      + (qs.bonus ? `<span class="qok">Claimed</span>` : `<button class="buy ${all ? 'can' : 'off'} qbonus"><i class="star"></i>${C.QUEST_BONUS}</button>`) + `</div></div>`
      + `<p class="small">Spend Starshards in the Shop.</p><button id="c-no" class="cbtn quiet">Close</button>`;
  }
  function wireQuests(body) {
    body.querySelectorAll('.qtab').forEach(b => (b.onclick = () => { S.qTab = b.dataset.q; A.play('tier_up', 0.08); showCard('quests'); }));
    body.querySelectorAll('.qclaim').forEach(b => (b.onclick = () => { const n = C.questClaim(P, +b.dataset.i);
      if (n) { A.play('tier_up', 0.4); pop(W / 2, 500, `+${n} Starshards`, SHARD, 1.2, 40); save(); showCard('quests'); } else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); } }));
    const bb = body.querySelector('.qbonus');
    if (bb) bb.onclick = () => { const n = C.questBonus(P); if (n) { A.play('tier_up', 0.6); save(); showCard('quests'); } else { bb.classList.remove('shake'); void bb.offsetWidth; bb.classList.add('shake'); } };
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
  const tradeMins = tc => { const m = tc / (C.fightRate(P, B) * 1.6 + RATE) / 60; return m < 1.5 ? '1 minute' : `${Math.round(m)} minutes`; };
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
      + `<div class="trade">${SDK.rewardsOn ? '' : `<span>or trade about<br>${tradeMins(tc)} of income</span>`}`
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
      else if (C.buy(P, it.id)) { A.play('tier_up', 0.55); toast(`${it.name} unlocked!`); }
      else { b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake'); b.textContent = `${it.price - P.shop.shards} more`; return; }
      applyLooks(); save(); rerender();
    }));
    const sa = body.querySelector('#c-shardad');
    if (sa) sa.onclick = () => { adDay(); if (P.surge.shardAds >= C.SHARD_ADS_PER_DAY) return;
      SDK.rewarded(adHooks).then(ok => { if (ok) { P.surge.shardAds++; P.shop.shards += C.SHARD_AD; A.play('tier_up', 0.5); save(); } rerender(); }); };
    const tr = body.querySelector('#c-trade');
    if (tr) tr.onclick = () => {
      A.unlock();
      if (C.trade(P, B)) { A.play('tier_up', 0.3); save(); rerender(); }
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
    if (kind === 'ascend') html = `<span class="eyebrow">The city is whole</span><h2>Ascend?</h2><p>The city returns to ruins and the waves start again, but Resonance and damage are <b>x${C.ASCEND_MULT}</b> for good${P.ascensions ? ` (now x${C.fmt(Math.pow(C.ASCEND_MULT, P.ascensions))})` : ''}. Best wave, combos and your Shop looks stay${P.shop.open ? `, and you get ${C.SHARDS.ascend} Starshards` : ''}.</p>${btn('c-ok', 'Ascend', 'gold')}${btn('c-no', 'Not yet', 'quiet')}`;
    if (kind === 'save') html = `<span class="eyebrow">Wave ${C.fmt(P.wave)} · boss</span><h2>The gate is breaking!</h2><p>Watch an ad to rebuild the gate and keep fighting this boss.</p>${btn('c-ad', 'Watch an ad: save the gate', 'gold')}${btn('c-no', 'Fall back', 'quiet')}`;
    if (kind === 'building') {
      const b = C.byId(data), L = C.levelOf(P, data), r = C.buildingRate(P, data, B), r2 = C.buildingRate(P, data, B, L + 1), c = C.levelCost(P, data, B), m = C.nextMilestone(L);
      html = `<span class="eyebrow">Level ${L} · ${rfmt(r)} Resonance a second</span><h2>${b.name}</h2><p>${b.line}</p>`
        + `<p class="small">Next level: ${rfmt(r)} -> ${rfmt(r2)} a second${m ? `<br>Level ${m} doubles it` : ''}</p>`
        + btn('c-ok', `Level up · ${C.fmt(c)}`, P.res >= c ? 'gold' : 'off') + `<span class="wait"></span>` + btn('c-no', 'Close', 'quiet');
    }
    if (kind === 'shop') html = shopHtml();
    if (kind === 'quests') html = questsHtml();
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
    if (kind === 'shop') wireShop(body, () => showCard('shop'));
    if (kind === 'building') {
      on('c-ok', () => {
        if (levelUp(data)) return showCard('building', data);
        const b = body.querySelector('#c-ok'), c = C.levelCost(P, data, B); b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake');
        body.querySelector('.wait').textContent = `About ${Math.max(1, Math.ceil((c - P.res) / (C.fightRate(P, B) * 1.6 + RATE)))}s of fighting`;
      });
      card.onclick = ev => { if (ev.target === card) close(); };
    } else card.onclick = kind === 'shop' ? (ev => { if (ev.target === card) close(); }) : null;
    if (kind === 'ascend') on('c-ok', () => {
      P = C.ascend(P); refresh(); S.wisps = []; S.motes = []; S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = 0; A.play('ascension', 0.7);
      S.cheerNext = 'A new dawn. Rebuild!'; startWave(); save(); close();
      toast(`Ascension ${P.ascensions}: Resonance and damage x${C.fmt(Math.pow(C.ASCEND_MULT, P.ascensions))}`);
    });
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
  function prepareCity() {
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
      const e = els.length ? els[i % els.length] : null, im = img[(e || idle[i]) + stage]; if (!im) return;
      const grow = Math.min(0.16, (P.gateLevel - C.STAGES[stage - 1]) * 0.012);   // a little taller with every level of the stage
      const breathe = calm ? 0 : Math.sin(S.t * 2 + i) * 0.012, h = CITY.towerH * (1 + (stage - 1) * 0.1 + grow) * (1 + breathe - t.recoil * 0.05);
      shadow(t.x + 5, t.y + 2, CITY.platformR * 0.95, CITY.platformR * 0.36, 0.42);
      if (!e) cx.filter = 'grayscale(0.85) brightness(0.8)';
      sprite(im, t.x, t.y + CITY.towerSink, h);
      cx.filter = 'none';
      if (t.flash > 1) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(t.x, t.y - h * 0.5, 60, 'rgba(255,230,150,', 0.5 * (t.flash - 1) * 5); cx.restore(); }
      if (e) {
        cx.save(); cx.globalCompositeOperation = 'lighter';
        glow(t.x, t.y - h * 0.78, 24 + t.flash * 16, (L.TOWER[P.shop.equip[e]] || {}).glow || GLOW[e], 0.16 + (calm ? 0 : 0.05 * Math.sin(S.t * 3 + i)) + t.flash * 0.4);
        cx.restore();
      }
    });
  }

  function drawEnemy(e) {
    const sh = img[e.sheet]; if (!sh) return;
    const fade = Math.min(1, (S.t - e.born) / 0.6), die = e.dying ? 1 - e.dying / 0.45 : 1;
    cx.save(); cx.globalAlpha = fade * die;
    shadow(e.x + 4, e.y - 2, e.size * 0.42, e.size * 0.13, 0.35);
    if (e.dying) { cx.translate(e.x, e.y); cx.scale(1 + e.dying, 1 - e.dying * 0.8); cx.translate(-e.x, -e.y); }
    if (GATE_X + e.off < e.x - 20) { cx.translate(e.x, 0); cx.scale(-1, 1); cx.translate(-e.x, 0); }   // face the way it walks
    if (e.flash > 0) cx.filter = `brightness(${1 + e.flash * 0.9})`;
    else if (e.chill > 0) cx.filter = 'saturate(0.55) brightness(1.12)';
    sprite(sh, e.x, e.y, e.size, Math.floor(e.frame) % 6, 6);
    cx.filter = 'none'; cx.restore();
    if (e.dying) return;
    if (e.chill > 0) { cx.save(); cx.globalAlpha = 0.5; glow(e.x, e.y - e.size * 0.4, e.size * 0.55, GLOW.frost, 0.35); cx.restore(); }
    if (e.burn > 0 && !reduced() && Math.random() < 0.3) S.fx.push({ x: e.x + (Math.random() - 0.5) * e.size * 0.5, y: e.y - e.size * 0.5, vx: 0, vy: -50, t: 0, life: 0.4, col: COLOR.fire, r: 2.5 });
    const bw = e.boss ? 120 : 44, by = e.y - e.size - 8;
    cx.fillStyle = 'rgba(42,26,34,.85)'; cx.fillRect(e.x - bw / 2 - 2, by - 2, bw + 4, 9);
    cx.fillStyle = e.boss ? '#ff6a3a' : e.armour ? '#c8c2b4' : '#e5533d'; cx.fillRect(e.x - bw / 2, by, bw * Math.max(0, e.hp / e.max), 5);
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
      const sp = CITY.spots[id], x = sx(sp[0]), y = sy(sp[1]) - sp[3] * K * 0.62 + (reduced() ? 0 : Math.sin(S.t * 4 + x) * 4);
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
    const x = 80, y = 196, w = W - 160, f = Math.max(0, b.hp / b.max);
    cx.save(); cx.fillStyle = 'rgba(42,26,34,.88)'; cx.strokeStyle = '#c9a24a'; cx.lineWidth = 3;
    cx.beginPath(); cx.roundRect(x - 6, y - 6, w + 12, 34, 17); cx.fill(); cx.stroke();
    const g2 = cx.createLinearGradient(x, 0, x + w, 0); g2.addColorStop(0, '#ff3a3a'); g2.addColorStop(1, '#ff9a3a');
    cx.fillStyle = g2; cx.beginPath(); cx.roundRect(x, y, Math.max(12, w * f), 22, 11); cx.fill();
    cx.font = '30px "HG Display", sans-serif'; cx.textAlign = 'center'; cx.lineWidth = 6; cx.strokeStyle = '#2a1a22';
    const label = `BOSS · ${Math.ceil(f * 100)}%`; cx.strokeText(label, W / 2, y + 22); cx.fillStyle = '#fff4dc'; cx.fillText(label, W / 2, y + 22);
    cx.restore();
  }
  // A medal drops in under the counters when a Feat is earned: bronze, silver or gold.
  function drawMedal() {
    const m = S.medal; if (!m || S.card) return;
    const u = Math.min(1, m.t / 0.35), out = m.t > 2 ? (m.t - 2) / 0.4 : 0, y = 260 - (1 - u) * 60, col = ['', '#d08a4a', '#dfe6ee', '#ffd56b'][m.tier];
    cx.save(); cx.globalAlpha = 1 - out; cx.translate(W / 2, y); cx.scale(0.6 + 0.4 * u, 0.6 + 0.4 * u);
    glow(0, 0, 70, m.tier === 3 ? 'rgba(255,213,107,' : 'rgba(255,255,255,', 0.45);
    cx.fillStyle = col; cx.strokeStyle = '#2a1a22'; cx.lineWidth = 5; cx.beginPath(); cx.arc(0, 0, 34, 0, 7); cx.fill(); cx.stroke();
    cx.fillStyle = '#2a1a22'; L.star(cx, 0, 0, 20, 8, 5); cx.restore();
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
    const title = 'WAVE ' + C.fmt(w.n) + '!';
    cx.font = (w.big ? 76 : 66) + 'px "HG Display", sans-serif'; cx.lineWidth = 12; cx.strokeStyle = '#24162c'; cx.strokeText(title, 0, 0);
    const g = cx.createLinearGradient(0, -34, 0, 34); g.addColorStop(0, '#fff3c2'); g.addColorStop(0.55, '#ffd36b'); g.addColorStop(1, '#f08a2c');
    cx.fillStyle = g; cx.fillText(title, 0, 0);
    cx.globalAlpha = fade * Math.max(0, Math.min(1, (age - 0.18) / 0.2));
    cx.font = (w.big ? 38 : 34) + 'px "HG Display", sans-serif'; cx.lineWidth = 9; cx.strokeText(w.cheer, 0, 56);
    cx.fillStyle = w.big ? '#ffb3f0' : '#bff3ff'; cx.fillText(w.cheer, 0, 56);
    // what is coming: a portrait per new or special enemy role, named in its counter element's colour (approved fix 3)
    const roles = w.roles.filter(r => r !== 'slime'), n = roles.length, gap = 156;
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
    // ground: the Outer Seal meadow, with the treeline at the very top behind everything that walks
    if (img.meadow) cx.drawImage(img.meadow, 0, 0, W, CITY_Y + 30); else { cx.fillStyle = '#6f9a3c'; cx.fillRect(0, 0, W, CITY_Y + 30); }
    // the wilderness: a deep treeline across the top that the waves walk out of
    if (img.forest) { const f = img.forest, fw = f.width * 1.5, fh = f.height * 1.5; cx.drawImage(f, (W - fw) / 2, -14, fw, fh); cx.drawImage(f, (W - fw) / 2 + 60, 40, fw, fh * 0.9); }
    // the city: the ruined painting, restored buildings over it as they come back
    if (cityRuined) cx.drawImage(cityRuined, 0, CITY_Y);
    // the plaza and its fountain come back to life once half the city stands again
    for (const id of P.restored.length >= 5 ? P.restored.concat('plaza') : P.restored) {
      const part = cityRestored[id]; if (!part) continue;
      cx.save(); cx.globalAlpha = S.reveal && S.reveal.b === id ? Math.min(1, S.reveal.t / 0.9) : 1; cx.drawImage(part, 0, CITY_Y); cx.restore();
    }
    if (S.reveal && !reduced()) { const s = CITY.spots[S.reveal.b]; if (s && S.reveal.t < 1.2) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(sx(s[0]), sy(s[1]), 95, 'rgba(255,220,140,', 0.5 * (1 - S.reveal.t / 1.2)); cx.restore(); } }
    // the city's Resonance: the fountain flares as wisps land, motes rise from every restored building
    cx.save();
    if (S.plazaGlow > 0) glow(PLAZA.x, PLAZA.y, 80, RES_GLOW, 0.4 * S.plazaGlow);
    for (const m of S.motes) { const a = Math.sin(Math.PI * m.t / m.life); glow(m.x, m.y, m.r * 4, RES_GLOW, 0.35 * a); cx.globalAlpha = a; cx.fillStyle = '#fff0fb'; cx.beginPath(); cx.arc(m.x, m.y, m.r * 0.6, 0, 7); cx.fill(); cx.globalAlpha = 1; }
    cx.restore();
    drawMarkers();
    // the field, back to front
    for (const e of S.enemies.slice().sort((a, b) => a.y - b.y)) drawEnemy(e);
    if (img.wall) cx.drawImage(img.wall, 0, CITY_Y, W, CITY_H);   // the wall is nearer the camera than anyone outside it
    drawGateBar();
    drawBossBar(); drawMedal();
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
    if (!S.card) drawWavePop();   // one voice at a time: nothing banners behind a card
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
    el('combos').textContent = P.combos.length < 3 ? `Combos ${P.combos.length}/3` : `Feats ${P.feats.length}/${C.FEATS.length}`;   // a collection to finish, always in view
    el('shop').hidden = !P.shop.open; el('shop').classList.toggle('news', C.shopNews(P));
    if (P.shop.open) C.questsFor(P, today());
    el('quests').hidden = !P.shop.open; el('quests').classList.toggle('news', P.shop.open && C.questClaimable(P));
    // the Surge button: running (x2 and time left), ready (free, or an ad in Full Launch), or charging
    const sb = el('surge'), now = Date.now(); sb.hidden = P.firstSession;
    if (!sb.hidden) {
      const on = C.surgeOn(P, now), ready = !on && (SDK.rewardsOn || C.surgeReadyFree(P, now));
      sb.className = on ? 'on' : ready ? 'ready' : 'wait';
      html(sb, on ? `<b>x2</b><span>${mmss(P.surge.until - now)}</span>` : ready ? `${SDK.rewardsOn ? '<span class="play"></span>' : ''}<b>Surge</b><span>x2</span>`
        : `<b>Surge</b><span>${mmss(P.surge.until + C.SURGE_COOLDOWN_MS - now)}</span>`);
      if (ready && !S.surgeTold) { S.surgeTold = true; toast('Surge ready: tap it for double Resonance'); }
    }
    el('rate').hidden = RATE <= 0; el('rateN').textContent = `+${rfmt(RATE)}/s`;
    el('stars').textContent = P.ascensions ? `★ ${P.ascensions}` : '';
    el('stars').hidden = !P.ascensions;
    const fork = C.pendingFork(P), uc = C.upgradeCost(P.gateLevel, B), up = el('up');
    up.querySelector('.label').textContent = fork ? 'New path!' : 'Upgrade';
    const gem = '<i class="gem sm"></i>', dmgUp = Math.round((Math.pow(1.235, 1) - 1) * 100);
    html(up.querySelector('.cost'), fork ? `Stage ${fork}` : `${gem}${C.fmt(uc)} <span class="what">· damage +${dmgUp}%</span>`);
    up.querySelector('.fill').style.width = fork ? '100%' : Math.min(100, P.res / uc * 100) + '%';
    up.classList.toggle('ready', !!fork || P.res >= uc);
    const re = el('re'), asc = C.canAscend(P), n = P.restored.length, rc = asc ? 0 : C.restoreCost(n);
    re.classList.toggle('ascend', asc);
    re.querySelector('.label').textContent = asc ? 'Ascend' : 'Restore';
    const offer = asc ? [] : C.restoreOffer(P);
    html(re.querySelector('.cost'), asc ? `x${C.ASCEND_MULT} forever` : `${offer.length === 1 ? C.byId(offer[0]).name : 'Pick 1 of 2'} · ${gem}${C.fmt(rc)}`);
    re.querySelector('.fill').style.width = asc ? '100%' : Math.min(100, P.res / rc * 100) + '%';
    re.classList.toggle('ready', asc || P.res >= rc);
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
    if (!target && !P.firstSession && !P.grown && !S.card && P.restored.length >= C.FIXED_ORDER && (toastT <= 0 || S.growTip)) {
      const id = P.restored.find(b => P.res >= C.levelCost(P, b, B));
      if (id && (S.growTipT || 0) < 20) { spot = CITY.spots[id]; target = 'bld'; if (!S.growTip) { S.growTip = true; S.growTipT = 0; toast('Tap a building to grow its Resonance'); } }
    }
    const hand = el('hand'); hand.className = target ? 'on ' + target : '';
    hand.style.left = spot ? (sx(spot[0]) - 48) + 'px' : ''; hand.style.top = spot ? (sy(spot[1]) + 6) + 'px' : '';   // finger up, from under the building
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
  }
  root.addEventListener('resize', fit);

  // ---------- boot ----------
  function bind() {
    el('up').addEventListener('click', upgrade); el('re').addEventListener('click', restore); el('surge').addEventListener('click', surgeTap); el('quests').addEventListener('click', () => { A.unlock(); if (!S.card && P.shop.open) showCard('quests'); });
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
      if (key === 'u') upgrade(); else if (key === 'r') restore(); else if (key === 'p') el('pause').click(); else if (key === 's') openShopCard(); else if (key === 'q' && P.shop.open && !S.card) showCard('quests'); else if (key === 'x') surgeTap();
    });
    root.addEventListener('pointerdown', () => A.unlock(), { once: true });
    // tap a restored building to level it up; a ruined one says what it will do once restored
    cv.addEventListener('click', ev => {
      if (S.card) return; const r = cv.getBoundingClientRect(), x = (ev.clientX - r.left) / r.width * W, y = (ev.clientY - r.top) / r.height * H;
      for (const id of C.BUILDING_IDS) { const s = CITY.spots[id]; const dx = (x - sx(s[0])) / (s[2] * K * 0.8), dy = (y - sy(s[1])) / (s[3] * K * 0.8);
        if (dx * dx + dy * dy < 1) { A.unlock(); if (P.restored.includes(id)) showCard('building', id); else { const b = C.byId(id); toast(`${b.name} (ruined). Restore it: ${b.line}`); } return; } }
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
