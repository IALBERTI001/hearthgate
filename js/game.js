// Hearthgate: hold the gate, and the city rebuilds itself behind you.
// Portrait stage 720x1280: the field on top, the city's north wall with its gatehouse and four tower platforms, the city,
// then Upgrade and Restore. CrazyGames' smallest desktop frame (800x450) shows the stage at 0.35, so every label is at
// least 30 design px. The simulation runs on a fixed 60 Hz step and never pauses on a tab switch; only the player pauses.
(function (root) {
  'use strict';
  const C = root.HGCore, A = root.HGAudio || { play() {}, music() {}, unlock() {}, set() {} };
  const SDK = root.GameSDK || { gameplay() {}, loadingStop() {}, happytime() {}, rewarded: () => Promise.resolve(false), rewardsOn: false };
  const W = 720, H = 1280, DT = 1 / 60, SAVE_KEY = 'hearthgate.profile.v1';

  // ---------- layout from the city painting (art/masters/city-north-gate, cropped at exactly half scale) ----------
  const CITY = C.CITY, CITY_H = CITY.band[1], CITY_Y = 1112 - CITY_H, K = CITY.band[0] / CITY.crop[2];
  const sx = x => (x - CITY.crop[0]) * K, sy = y => CITY_Y + (y - CITY.crop[1]) * K;
  const GATE_X = sx(CITY.gate[0]), WALL_FRONT = sy(CITY.wallTop);          // enemies press against the wall here
  const PLATFORMS = CITY.platforms.map(([x, y]) => ({ x: sx(x), y: sy(y) })); // the four round platforms in the wall
  const RANGE_Y = 300;                                                        // towers reach enemies below this line
  const ELEMENT = { forge: 'fire', frostSpire: 'frost', stormBell: 'storm' };
  const COLOR = { fire: '#ff8a3a', frost: '#9fe8ff', storm: '#c98bff', none: '#efe2c0' };
  const GLOW = { fire: 'rgba(255,140,50,', frost: 'rgba(140,220,255,', storm: 'rgba(200,140,255,', none: 'rgba(255,240,200,' };

  // ---------- state ----------
  let P = load(), B = C.bonuses(P);
  const S = { t: 0, enemies: [], shots: [], pops: [], parts: [], spawnQ: [], spawnT: 0, gateHp: 0, gateHit: 0, paused: false,
    sessionT: 0, saveT: 0, reveal: null, card: null, wavePop: null, cheerNext: null, savedByAd: false };
  function load() { try { return C.sanitize(localStorage.getItem(SAVE_KEY)); } catch (e) { return C.defaults(); } }
  function save() { P.lastSeen = Date.now(); try { localStorage.setItem(SAVE_KEY, JSON.stringify(P)); } catch (e) {} }
  const refresh = () => { B = C.bonuses(P); };
  root.Hearthgate = { reloadSave() { P = load(); refresh(); S.enemies = []; startWave(); }, state: () => ({ P, S, B }) };
  // QA only (?qa in the address): run the simulation fast and drive the buttons, for the pacing bot in tools/qa.
  if (/[?&]qa\b/.test(location.search)) Object.assign(root.Hearthgate, {
    sim(sec) { for (let i = 0; i < sec * 60; i++) if (!S.paused) step(); },
    act(what, id) { if (what === 'up') upgrade(); if (what === 're') restore(); if (what === 'pick') { const o = document.querySelector('#card .opt' + (id ? `[data-id="${id}"]` : '')); if (o) o.click(); } if (what === 'ok') { const o = document.querySelector('#card #c-ok'); if (o) o.click(); } },
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
    S.gateHp = S.gateHp > 0 ? Math.min(S.gateHp, gateMax()) : gateMax();
    // a shuffled queue, so runners and armoured arrive among the slimes rather than in blocks; the boss comes last
    const q = []; for (const [role, c] of mix) for (let i = 0; i < c; i++) q.push(role);
    for (let i = q.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [q[i], q[j]] = [q[j], q[i]]; }
    S.spawnQ = q.sort((a, b) => (a === 'boss') - (b === 'boss')); S.spawnT = 1.0;
  }
  function spawn(role) {
    const R = C.ROLES[role], hp = C.enemyHp(P.wave, role);
    // enemies step out of the long grass below the HUD with a puff of dust, never from under the top panel
    const x0 = 80 + Math.random() * 560, y0 = 172 + Math.random() * 40;
    for (let i = 0; i < (reduced() ? 3 : 9); i++) { const a = Math.random() * Math.PI * 2; S.parts.push({ x: x0 + Math.cos(a) * 20, y: y0 - 6, vx: Math.cos(a) * 50, vy: -20 - Math.random() * 30, t: 0, life: 0.5, col: 'rgba(220,200,150,.9)', r: 4 + Math.random() * 4, dust: true }); }
    S.enemies.push({ role, sheet: R.sheet, x: x0, y: y0, off: (Math.random() - 0.5) * 330, hp, max: hp,
      sp: R.speed, size: R.size, armour: R.armour || 0, weak: R.weak, boss: role === 'boss', frame: Math.random() * 6,
      chill: 0, burn: 0, flash: 0, born: S.t, dying: 0 });
  }

  // ---------- simulation ----------
  const TOWERS = PLATFORMS.map((p, i) => ({ x: p.x, y: p.y, cd: 0.2 + i * 0.13, recoil: 0, flash: 0, n: i }));
  function step() {
    S.t += DT; S.sessionT += DT; if (S.wavePop) S.wavePop.t += DT;
    S.gateHit = Math.max(0, S.gateHit - DT * 4);
    if (S.spawnQ.length && (S.spawnT -= DT) <= 0) { const r = S.spawnQ.shift(); spawn(r); S.spawnT = r === 'swarm' ? 0.28 : Math.max(0.45, 1.35 - P.wave * 0.02); }
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
      } else { S.gateHp -= (e.boss ? 16 : e.role === 'armoured' ? 7 : 4) * DT; S.gateHit = 1; }
      if (e.hp <= 0) kill(e);
    }
    S.enemies = S.enemies.filter(e => !e.dying || e.dying < 0.45);
    for (const p of S.pops) p.t += DT; S.pops = S.pops.filter(p => p.t < p.life);
    for (const p of S.parts) { p.t += DT; p.x += p.vx * DT; p.y += p.vy * DT; p.vy += (p.g || 0) * DT; }
    S.parts = S.parts.filter(p => p.t < p.life);
    if (S.gateHp <= 0) gateBroken();
    else if (!S.spawnQ.length && !S.enemies.length) waveCleared();
    if ((S.saveT += DT) > 5) { S.saveT = 0; save(); }
  }

  function hit(e, el) {
    let d = C.towerDamage(P, B); const comboMul = 1 + B.combo;
    if (e.armour && el !== 'fire') d *= 1 - e.armour;                 // armour: only Fire bites fully
    if (el === 'fire') {
      if (e.chill > 0) { d *= 3 * comboMul; combo('shatter', e, 'SHATTER!', COLOR.frost); burst(e, COLOR.frost, 14, 220); A.play('shatter_combo', 0.5); }
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
    burst(e, COLOR[el], el === 'none' ? 4 : 6, 120);
    A.play(el === 'frost' ? 'ice_contact_0' : el === 'storm' ? 'lightning_contact_0' : el === 'fire' ? 'fire_contact_0' : 'ice_contact_0', 0.12);
    pop(e.x + (Math.random() - 0.5) * 30, e.y - e.size - 6, C.fmt(d), COLOR[el], 0.7, 30);
  }
  function combo(id, e, word, col) {
    // one combo word on screen at a time, so words never pile into each other
    S.pops = S.pops.filter(p => !p.combo);
    S.pops.push({ x: Math.max(130, Math.min(590, e.x)), y: e.y - e.size - 46, text: word, col, t: 0, life: 1.1, size: 46, combo: true });
    if (!P.combos.includes(id)) { P.combos.push(id); toast(`New combo: ${word.replace('!', '')}  ·  ${P.combos.length}/3`); save(); }
  }
  function kill(e) {
    e.dying = DT; const gain = C.killParts(P.wave, e.boss, B);
    P.parts += gain; pop(e.x, e.y - e.size * 0.6, '+' + C.fmt(gain), '#ffd56b', 0.9, 32);
    burst(e, e.sheet === 'golem' ? '#b9b2a2' : e.sheet === 'beetle' ? '#ff9a4a' : '#8fe05a', e.boss ? 30 : 12, e.boss ? 260 : 160);
    if (e.boss || e.role === 'armoured') A.play('golem_death', 0.5);
  }
  function waveCleared() {
    if (C.isBoss(P.wave)) {
      pop(W / 2, 330, 'BOSS DOWN!', '#ffd56b', 1.8, 60); SDK.happytime(); A.play('tier_up', 0.6);
      if (P.wave === 10 && !P.tomorrowShown) { P.tomorrowShown = true; setTimeout(() => toast('Come back tomorrow: a daily Parts reward waits'), 1900); }
    }
    P.wave++; P.bestWave = Math.max(P.bestWave, P.wave); startWave(); save();
  }
  function gateBroken(noAd) {
    // a boss wave can be saved once by an optional ad (Full Launch only); otherwise the gate falls back, nothing lost
    if (!noAd && C.isBoss(P.wave) && SDK.rewardsOn && !S.savedByAd && !S.card) { S.paused = true; showCard('save'); return; }
    const to = Math.max(1, P.wave - 3);
    toast(`The gate fell back to wave ${to}. Nothing lost.`); S.cheerNext = 'Regroup. You\'ve got this!';
    P.wave = to; S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = gateMax(); S.savedByAd = false; startWave(); save();
  }
  function pop(x, y, text, col, life, size) { S.pops.push({ x, y, text, col, t: 0, life, size }); }
  function burst(e, col, n, sp) {
    if (reduced()) n = Math.ceil(n / 3);
    for (let i = 0; i < n; i++) {
      const a = Math.random() * Math.PI * 2, v = sp * (0.4 + Math.random() * 0.6);
      S.parts.push({ x: e.x, y: e.y - e.size * 0.45, vx: Math.cos(a) * v, vy: Math.sin(a) * v - 40, g: 260, t: 0, life: 0.35 + Math.random() * 0.3, col, r: 2 + Math.random() * 3 });
    }
  }

  // ---------- actions: Upgrade, Restore (and Ascend, on the same button at 10/10) ----------
  function upgrade() {
    A.unlock();
    if (S.card) return;
    const fork = C.pendingFork(P); if (fork) return showCard('fork', fork);
    const c = C.upgradeCost(P.gateLevel, B);
    if (P.parts < c) return refuse('up', c);
    const before = C.towerDamage(P, B), stage = C.stageOf(P.gateLevel);
    P.parts -= c; P.gateLevel++; refresh(); S.gateHp = gateMax();
    if (C.stageOf(P.gateLevel) > stage) { toast(`Gate stage ${C.stageOf(P.gateLevel)}! The towers grow.`); A.play('tier_up', 0.6); TOWERS.forEach(t => (t.flash = 1.5)); }
    else toast(`Damage ${C.fmt(before)} -> ${C.fmt(C.towerDamage(P, B))}`);
    save(); ui();
  }
  function restore() {
    A.unlock();
    if (S.card) return;
    if (C.canAscend(P)) return showCard('ascend');
    const offer = C.restoreOffer(P); if (!offer.length) return;
    const c = C.restoreCost(P.restored.length);
    if (P.parts < c) return refuse('re', c);
    if (offer.length > 1) return showCard('pick', offer);
    doRestore(offer[0]);
  }
  function doRestore(id) {
    const c = C.restoreCost(P.restored.length); if (P.parts < c) return;
    P.parts -= c; P.restored.push(id); refresh();
    const b = C.byId(id); toast(`${b.name} restored: ${b.line}`); S.reveal = { b: id, t: 0 };
    A.play('tier_up', 0.55); SDK.happytime();
    if (P.restored.length === 5) setTimeout(() => { toast('The fountain flows again!'); S.reveal = { b: 'plaza', t: 0 }; }, 2900);
    if (C.canAscend(P)) setTimeout(() => toast('The city is whole! Restore is now Ascend.'), 2900);
    save(); ui();
  }
  function refuse(id, cost) {
    const b = el(id); b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake');
    const rate = C.offlineRate(P, B) / (1 + B.offline) * 1.6;
    b.querySelector('.wait').textContent = `~${Math.max(1, Math.ceil((cost - P.parts) / rate))}s`;
  }

  // ---------- cards: daily reward, offline earnings, restore pick, fork, Ascend, save the gate, settings ----------
  const cardQueue = [];
  function nextCard() { const c = cardQueue.shift(); if (c) showCard(c[0], c[1]); }
  const hours = s => s >= 3600 ? `${Math.floor(s / 3600)}h ${Math.floor((s % 3600) / 60)}m` : `${Math.max(1, Math.floor(s / 60))} min`;
  function showCard(kind, data) {
    const card = el('card'), body = card.querySelector('.body'); S.card = kind; card.className = 'on ' + kind;
    const btn = (id, label, cls = '') => `<button id="${id}" class="cbtn ${cls}">${label}</button>`;
    let html = '';
    if (kind === 'daily') html = `<span class="eyebrow">Day ${data.day} reward</span><h2>+${C.fmt(data.amount)} Parts</h2><p>${data.big ? 'Day 7 bonus: five times the reward!' : 'Day 7 pays five times as much.'}</p>${btn('c-ok', 'Claim')}`;
    if (kind === 'offline') html = `<span class="eyebrow">While you were away</span><h2>+${C.fmt(data.parts)} Parts</h2><p>The gate held for ${hours(data.secs)}${data.secs >= C.OFFLINE_CAP_H * 3600 - 1 ? ', the most it stores' : ''}.</p>${SDK.rewardsOn ? btn('c-ad', 'Watch an ad: double it', 'gold') : ''}${btn('c-ok', 'Collect')}`;
    if (kind === 'pick') html = `<span class="eyebrow">Restore · ${C.fmt(C.restoreCost(P.restored.length))} Parts</span><h2>Choose what rises next</h2><div class="pick">${data.map(id => { const b = C.byId(id); return `<button class="opt" data-id="${id}"><b>${b.name}</b><span>${b.line}</span></button>`; }).join('')}</div>${btn('c-no', 'Not yet', 'quiet')}`;
    if (kind === 'fork') html = `<span class="eyebrow">Gate stage ${data}</span><h2>Choose a path</h2><div class="pick">${C.FORKS[data].map(f => `<button class="opt" data-id="${f.id}"><b>${f.name}</b><span>${f.line}</span></button>`).join('')}</div>`;
    if (kind === 'ascend') html = `<span class="eyebrow">The city is whole</span><h2>Ascend?</h2><p>The city returns to ruins and the waves start again, but Parts and damage are <b>x${C.ASCEND_MULT}</b> for good${P.ascensions ? ` (now x${C.fmt(Math.pow(C.ASCEND_MULT, P.ascensions))})` : ''}. Best wave and combos stay.</p>${btn('c-ok', 'Ascend', 'gold')}${btn('c-no', 'Not yet', 'quiet')}`;
    if (kind === 'save') html = `<span class="eyebrow">Wave ${C.fmt(P.wave)} · boss</span><h2>The gate is breaking!</h2><p>Watch an ad to rebuild the gate and keep fighting this boss.</p>${btn('c-ad', 'Watch an ad: save the gate', 'gold')}${btn('c-no', 'Fall back', 'quiet')}`;
    if (kind === 'settings') html = `<h2>Paused</h2>${['music', 'sfx', 'reduced'].map(k => `<label class="tog"><input type="checkbox" id="s-${k}" ${P.settings[k] ? 'checked' : ''}><span>${{ music: 'Music', sfx: 'Sound effects', reduced: 'Reduced motion' }[k]}</span></label>`).join('')}<p class="small">Best wave ${C.fmt(P.bestWave)} · Ascensions ${P.ascensions} · Combos ${P.combos.length}/3</p>${btn('c-ok', 'Resume')}`;
    body.innerHTML = html;
    const close = () => { card.className = ''; S.card = null; ui(); nextCard(); };
    const on = (id, f) => { const b = body.querySelector('#' + id); if (b) b.onclick = () => { A.unlock(); f(); }; };
    const adHooks = { mute: () => A.set('muted', true), unmute: () => A.set('muted', false) };
    if (kind === 'daily') on('c-ok', () => { P.parts += data.amount; P.daily = { day: data.today, streak: data.day, claimedTotal: P.daily.claimedTotal + data.amount }; A.play('tier_up', 0.5); save(); close(); });
    if (kind === 'offline') {
      on('c-ok', () => { P.parts += data.parts; save(); close(); });
      on('c-ad', () => SDK.rewarded(adHooks).then(ok => { P.parts += data.parts * (ok ? 2 : 1); if (ok) toast('Doubled!'); save(); close(); }));
    }
    if (kind === 'pick' || kind === 'fork') body.querySelectorAll('.opt').forEach(o => (o.onclick = () => {
      A.unlock();
      if (kind === 'pick') { close(); doRestore(o.dataset.id); return; }
      P.forks[data] = o.dataset.id; refresh(); S.gateHp = gateMax();
      toast(`${C.FORKS[data].find(f => f.id === o.dataset.id).name} chosen`); A.play('tier_up', 0.5); save(); close();
    }));
    if (kind === 'pick' || kind === 'ascend') on('c-no', close);
    if (kind === 'ascend') on('c-ok', () => {
      P = C.ascend(P); refresh(); S.enemies = []; S.shots = []; S.spawnQ = []; S.gateHp = 0; A.play('ascension', 0.7);
      S.cheerNext = 'A new dawn. Rebuild!'; startWave(); save(); close();
      toast(`Ascension ${P.ascensions}: Parts and damage x${C.fmt(Math.pow(C.ASCEND_MULT, P.ascensions))}`);
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
  function glow(x, y, r, rgba, a) {
    const g = cx.createRadialGradient(x, y, 0, x, y, r); g.addColorStop(0, rgba + a + ')'); g.addColorStop(1, rgba + '0)');
    cx.fillStyle = g; cx.beginPath(); cx.arc(x, y, r, 0, 7); cx.fill();
  }

  // Towers stand on the painted platforms: a contact shadow on the stone, a slow breath, a kick and a flash when they
  // fire, and their element's light at the crest. Dormant (grey) until their building is restored; a tier taller per stage.
  function drawTowers() {
    const els = elements(), stage = C.stageOf(P.gateLevel), idle = ['fire', 'frost', 'frost', 'storm'], calm = reduced();
    TOWERS.forEach((t, i) => {
      const e = els.length ? els[i % els.length] : null, im = img[(e || idle[i]) + stage]; if (!im) return;
      const breathe = calm ? 0 : Math.sin(S.t * 2 + i) * 0.012, h = CITY.towerH * (1 + (stage - 1) * 0.08) * (1 + breathe - t.recoil * 0.05);
      shadow(t.x + 5, t.y + 2, CITY.platformR * 0.95, CITY.platformR * 0.36, 0.42);
      if (!e) cx.filter = 'grayscale(0.85) brightness(0.8)';
      sprite(im, t.x, t.y + CITY.towerSink, h);
      cx.filter = 'none';
      if (e) {
        cx.save(); cx.globalCompositeOperation = 'lighter';
        glow(t.x, t.y - h * 0.78, 24 + t.flash * 16, GLOW[e], 0.16 + (calm ? 0 : 0.05 * Math.sin(S.t * 3 + i)) + t.flash * 0.4);
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
    if (e.burn > 0 && !reduced() && Math.random() < 0.3) S.parts.push({ x: e.x + (Math.random() - 0.5) * e.size * 0.5, y: e.y - e.size * 0.5, vx: 0, vy: -50, t: 0, life: 0.4, col: COLOR.fire, r: 2.5 });
    const bw = e.boss ? 120 : 54, by = e.y - e.size - 8;
    cx.fillStyle = 'rgba(42,26,34,.85)'; cx.fillRect(e.x - bw / 2 - 2, by - 2, bw + 4, 9);
    cx.fillStyle = e.boss ? '#ff6a3a' : e.armour ? '#c8c2b4' : '#e5533d'; cx.fillRect(e.x - bw / 2, by, bw * Math.max(0, e.hp / e.max), 5);
  }

  function drawShot(s) {
    const col = COLOR[s.el];
    cx.save(); cx.globalCompositeOperation = 'lighter';
    s.trail.forEach(([x, y], i) => { cx.globalAlpha = (i + 1) / s.trail.length * 0.5; cx.fillStyle = col; cx.beginPath(); cx.arc(x, y, 3 + i * 0.7, 0, 7); cx.fill(); });
    cx.globalAlpha = 1; glow(s.x, s.y, 16, GLOW[s.el], 0.7);
    cx.fillStyle = '#fff'; cx.beginPath(); cx.arc(s.x, s.y, 4.5, 0, 7); cx.fill();
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
    cx.setTransform(k, 0, 0, k, 0, 0);
    // ground: the Outer Seal meadow, with the treeline at the very top behind everything that walks
    if (img.meadow) cx.drawImage(img.meadow, 0, 0, W, CITY_Y + 30); else { cx.fillStyle = '#6f9a3c'; cx.fillRect(0, 0, W, CITY_Y + 30); }
    if (img.forest) cx.drawImage(img.forest, (W - img.forest.width) / 2, -8);
    // the city: the ruined painting, restored buildings over it as they come back
    if (cityRuined) cx.drawImage(cityRuined, 0, CITY_Y);
    // the plaza and its fountain come back to life once half the city stands again
    for (const id of P.restored.length >= 5 ? P.restored.concat('plaza') : P.restored) {
      const part = cityRestored[id]; if (!part) continue;
      cx.save(); cx.globalAlpha = S.reveal && S.reveal.b === id ? Math.min(1, S.reveal.t / 0.9) : 1; cx.drawImage(part, 0, CITY_Y); cx.restore();
    }
    if (S.reveal && !reduced()) { const s = CITY.spots[S.reveal.b]; if (s && S.reveal.t < 1.2) { cx.save(); cx.globalCompositeOperation = 'lighter'; glow(sx(s[0]), sy(s[1]), 95, 'rgba(255,220,140,', 0.5 * (1 - S.reveal.t / 1.2)); cx.restore(); } }
    // the field, back to front
    for (const e of S.enemies.slice().sort((a, b) => a.y - b.y)) drawEnemy(e);
    drawGateBar();
    drawTowers();
    for (const s of S.shots) drawShot(s);
    for (const p of S.parts) if (p.dust) { cx.globalAlpha = 0.6 * (1 - p.t / p.life); cx.fillStyle = p.col; cx.beginPath(); cx.arc(p.x, p.y, p.r * (1 + p.t * 2), 0, 7); cx.fill(); }
    cx.globalAlpha = 1;
    cx.save(); cx.globalCompositeOperation = 'lighter';
    for (const p of S.parts) if (!p.dust) { cx.globalAlpha = 1 - p.t / p.life; cx.fillStyle = p.col; cx.beginPath(); cx.arc(p.x, p.y, p.r, 0, 7); cx.fill(); }
    cx.restore();
    cx.textAlign = 'center'; cx.textBaseline = 'alphabetic'; cx.lineJoin = 'round';
    for (const p of S.pops) {
      if (p.arc) {
        const [x0, y0, x1, y1] = p.arc, mx = (x0 + x1) / 2 + (Math.random() - 0.5) * 30, my = (y0 + y1) / 2 + (Math.random() - 0.5) * 30;
        cx.save(); cx.strokeStyle = COLOR.storm; cx.lineWidth = 3; cx.shadowColor = COLOR.storm; cx.shadowBlur = 10;
        cx.beginPath(); cx.moveTo(x0, y0); cx.lineTo(mx, my); cx.lineTo(x1, y1); cx.stroke(); cx.restore(); continue;
      }
      cx.globalAlpha = 1 - p.t / p.life; cx.font = `${p.size}px "HG Display", sans-serif`;
      cx.lineWidth = 6; cx.strokeStyle = '#2a1a22'; cx.strokeText(p.text, p.x, p.y - p.t * 40); cx.fillStyle = p.col; cx.fillText(p.text, p.x, p.y - p.t * 40);
      cx.globalAlpha = 1;
    }
    drawWavePop();
    if (S.reveal && (S.reveal.t += 1 / 60) > 1.4) S.reveal = null;
  }

  // ---------- DOM UI ----------
  const el = id => document.getElementById(id);
  let toastT = 0;
  function toast(text) { const t = el('toast'); t.textContent = text; t.classList.add('show'); toastT = 2.8; }
  function ui() {
    el('wave').textContent = `Wave ${C.fmt(P.wave)}`;
    el('parts').textContent = C.fmt(P.parts);
    el('city').textContent = `City ${P.restored.length}/10`;
    el('combos').textContent = `Combos ${P.combos.length}/3`;
    el('stars').textContent = P.ascensions ? `★ ${P.ascensions}` : '';
    el('stars').hidden = !P.ascensions;
    const fork = C.pendingFork(P), uc = C.upgradeCost(P.gateLevel, B), up = el('up');
    up.querySelector('.label').textContent = fork ? 'Choose path' : 'Upgrade';
    up.querySelector('.cost').textContent = fork ? `Stage ${fork}` : C.fmt(uc);
    up.querySelector('.fill').style.width = fork ? '100%' : Math.min(100, P.parts / uc * 100) + '%';
    up.classList.toggle('ready', !!fork || P.parts >= uc);
    const re = el('re'), asc = C.canAscend(P), n = P.restored.length, rc = asc ? 0 : C.restoreCost(n);
    re.classList.toggle('ascend', asc);
    re.querySelector('.label').textContent = asc ? 'Ascend' : 'Restore';
    re.querySelector('.cost').textContent = asc ? `x${C.ASCEND_MULT} forever` : C.fmt(rc);
    re.querySelector('.fill').style.width = asc ? '100%' : Math.min(100, P.parts / rc * 100) + '%';
    re.classList.toggle('ready', asc || P.parts >= rc);
    for (const b of [up, re]) if (b.classList.contains('ready')) b.querySelector('.wait').textContent = '';
    // pointing hand: first session only, one button at a time, never while a card is open
    let target = null;
    if (P.firstSession && !S.card) {
      if (P.gateLevel === 1 && S.sessionT > 6 && P.parts >= uc) target = 'up';
      else if (n < C.FIXED_ORDER && P.parts >= rc && (P.gateLevel > 1 || n > 0)) target = 're';
      if (n >= C.FIXED_ORDER) { P.firstSession = false; P.daily.day = P.daily.day || C.dayKey(new Date()); save(); }
    }
    el('hand').className = target ? 'on ' + target : '';
  }

  // ---------- loop (never pauses on tab switch; only the player pauses) ----------
  let acc = 0, last = performance.now();
  function advance(now) {
    acc += Math.min(10, (now - last) / 1000); last = now;
    let n = 0; while (acc >= DT && n < 600) { if (!S.paused) step(); acc -= DT; n++; }
    if ((toastT -= n * DT) <= 0) el('toast').classList.remove('show');
  }
  function frame(now) { advance(now); render(); ui(); requestAnimationFrame(frame); }
  setInterval(() => { if (document.hidden) advance(performance.now()); }, 250);
  document.addEventListener('visibilitychange', () => { SDK.gameplay(!document.hidden && !S.paused); if (document.hidden) save(); });
  root.addEventListener('pagehide', save);

  // ---------- stage fit ----------
  function fit() {
    const vw = root.innerWidth, vh = root.innerHeight, f = Math.min(vw / W, vh / H);
    el('stage').style.transform = `translate(${(vw - W * f) / 2}px,${(vh - H * f) / 2}px) scale(${f})`;
    k = Math.max(0.5, Math.min(2, f * (root.devicePixelRatio || 1)));
    cv.width = Math.round(W * k); cv.height = Math.round(H * k);
  }
  root.addEventListener('resize', fit);

  // ---------- boot ----------
  function bind() {
    el('up').addEventListener('click', upgrade); el('re').addEventListener('click', restore);
    el('pause').addEventListener('click', () => {
      A.unlock();
      if (S.card === 'settings') { el('card').querySelector('#c-ok').click(); return; }
      if (S.card) return;
      S.paused = true; el('pause').classList.add('on'); SDK.gameplay(false); showCard('settings');
    });
    root.addEventListener('keydown', e => {
      if (e.repeat) return; const key = e.key.toLowerCase();
      if (S.card) { if (key === 'enter') { const b = el('card').querySelector('#c-ok'); if (b) { e.preventDefault(); b.click(); } } return; }
      if (key === 'u') upgrade(); else if (key === 'r') restore(); else if (key === 'p') el('pause').click();
    });
    root.addEventListener('pointerdown', () => A.unlock(), { once: true });
    if (matchMedia('(hover: hover) and (pointer: fine)').matches) document.documentElement.classList.add('keys');
  }
  function returnCards() {
    // the daily reward is the first thing a returning player sees, then what the gate earned while they were away
    const now = Date.now(), d = C.dailyDue(P, now), off = C.offlineEarnings(P, B, now);
    if (d) cardQueue.push(['daily', d]);
    if (off.parts > 0 && !P.firstSession) cardQueue.push(['offline', off]);
    nextCard();
  }
  fit(); bind(); A.set('music', P.settings.music); A.set('sfx', P.settings.sfx); startWave();
  const T = s => `assets/towers/${s}.webp`;
  Promise.all([loadImg('ruined', 'assets/city/city-ruined.webp'), loadImg('restored', 'assets/city/city-restored.webp'),
    loadImg('meadow', 'assets/ground/meadow.webp'), loadImg('forest', 'assets/kit/forest-edge-top.webp'),
    loadImg('slime', 'assets/enemies/slime-walk.webp'), loadImg('beetle', 'assets/enemies/beetle-walk.webp'), loadImg('golem', 'assets/enemies/golem-walk.webp'),
    ...['fire', 'frost', 'storm'].flatMap(e => [1, 2, 3].map(t => loadImg(e + t, T(`${e}-${t}`))))])
    .then(() => { prepareCity(); el('boot').remove(); SDK.loadingStop(); SDK.gameplay(true); last = performance.now(); returnCards(); requestAnimationFrame(frame); });
})(typeof globalThis !== 'undefined' ? globalThis : this);
