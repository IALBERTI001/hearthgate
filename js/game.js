// Hearthgate hello-world: portrait stage, a fight already running at load, Upgrade and Restore, city counters.
// Design stage 720x1280. CrazyGames' smallest desktop frame (800x450) shows it at 0.35, so every label is at least
// 30 design px (about 10.5 rendered px there). Simulation runs on a fixed 60 Hz step and never pauses on tab switch.
(function (root) {
  'use strict';
  const C = root.HGCore, SDK = root.GameSDK || { gameplay() {}, loadingStop() {}, happytime() {} };
  const W = 720, H = 1280, FIELD_TOP = 96, GATE_Y = 640, CITY_Y = 712, CITY_H = 400, DT = 1 / 60;
  const SAVE_KEY = 'hearthgate.profile.v1';
  const RANGE_Y = 260; // defenders reach enemies below this line, so every enemy is seen walking in from the top edge
  const ELEMENT = { forge: 'fire', frostSpire: 'frost', stormBell: 'storm' };
  const COLOR = { fire: '#ff7a2e', frost: '#8fe3ff', storm: '#c77dff', none: '#f3e2b3' };
  // Where each restorable building sits in the city image (720x480 space), so a restore reveals the restored art there.
  const SPOT = { forge: [441, 84, 92], frostSpire: [96, 262, 98], stormBell: [511, 190, 86], b4: [108, 84, 96], b5: [300, 197, 92], b6: [436, 330, 100] };

  // ---------- state ----------
  let P = load();
  const S = { t: 0, enemies: [], shots: [], pops: [], spawnLeft: 0, spawnT: 0, gateHp: 0, hintStep: 0, paused: false, sessionT: 0, saveT: 0 };
  function load() { try { return C.sanitize(localStorage.getItem(SAVE_KEY)); } catch (e) { return C.defaults(); } }
  function save() { P.lastSeen = Date.now(); try { localStorage.setItem(SAVE_KEY, JSON.stringify(P)); } catch (e) {} }
  root.Hearthgate = { reloadSave() { P = load(); startWave(); render(); }, state: () => ({ P, S }) };

  const gateMax = () => Math.floor(100 * Math.pow(1.18, P.gateLevel - 1));
  const dmg = () => 4 * Math.pow(1.5, P.gateLevel - 1);
  const elements = () => P.restored.map(b => ELEMENT[b]).filter(Boolean);
  const isBoss = w => w % 10 === 0;

  // Wave callout from Warden of Power (Nov 2 2026), carried over Nov 5 2026 at the user's request: a big WAVE N! with one
  // encouraging line under it, special lines on the first wave, boss waves and after the gate falls back.
  const CHEERS = ['Keep it up!', 'Great job!', 'WOOOAHH!', 'Unstoppable!', 'Hold the line!', 'Nice work!', 'You got this!',
    'Crushing it!', 'On fire!', 'Legendary!', 'The city cheers you on!', 'Brick by brick!'];
  let wavePop = null, cheerNext = null;
  function showWavePop(n) {
    const boss = isBoss(n);
    const cheer = cheerNext || (n === 1 ? ['Good luck!', 'How far will you go?'][Math.floor(Math.random() * 2)]
      : boss ? 'Boss incoming. Hold the gate!' : n % 50 === 0 ? 'HALFWAY TO GLORY!' : CHEERS[(n * 7 + 3) % CHEERS.length]);
    cheerNext = null; wavePop = { n, cheer, big: boss, t: 0 };
  }
  function drawWavePop() {
    if (!wavePop) return;
    const age = wavePop.t, L = 2.4; if (age > L) { wavePop = null; return; }
    const calm = matchMedia('(prefers-reduced-motion: reduce)').matches;
    const pop = calm ? 1 : age < 0.22 ? 0.55 + 0.6 * age / 0.22 : age < 0.36 ? 1.15 - (age - 0.22) / 0.14 * 0.15 : 1;
    const fade = age > L - 0.5 ? Math.max(0, (L - age) / 0.5) : 1;
    cx.save(); cx.globalAlpha = fade; cx.translate(W / 2, 236 - (calm ? 0 : age * 6)); cx.scale(pop, pop);
    cx.textAlign = 'center'; cx.textBaseline = 'middle'; cx.lineJoin = 'round';
    const title = 'WAVE ' + C.fmt(wavePop.n) + '!';
    cx.font = (wavePop.big ? 76 : 66) + 'px "HG Display", sans-serif'; cx.lineWidth = 12; cx.strokeStyle = '#24162c'; cx.strokeText(title, 0, 0);
    const g = cx.createLinearGradient(0, -34, 0, 34); g.addColorStop(0, '#fff3c2'); g.addColorStop(0.55, '#ffd36b'); g.addColorStop(1, '#f08a2c');
    cx.fillStyle = g; cx.fillText(title, 0, 0);
    cx.globalAlpha = fade * Math.max(0, Math.min(1, (age - 0.18) / 0.2));
    cx.font = (wavePop.big ? 38 : 34) + 'px "HG Display", sans-serif'; cx.lineWidth = 9; cx.strokeText(wavePop.cheer, 0, 56);
    cx.fillStyle = wavePop.big ? '#ffb3f0' : '#bff3ff'; cx.fillText(wavePop.cheer, 0, 56); cx.restore();
  }

  function startWave() {
    showWavePop(P.wave);
    S.gateHp = S.gateHp > 0 ? Math.min(S.gateHp, gateMax()) : gateMax();
    S.spawnLeft = isBoss(P.wave) ? 1 : 6 + Math.min(14, P.wave * 2);
    S.spawnT = 0.4;
  }
  function spawn() {
    const boss = isBoss(P.wave), row = boss ? 2 : (P.wave > 3 && Math.random() < 0.35 ? 1 : 0);
    const hp = 10 * Math.pow(1.16, P.wave - 1) * (boss ? 40 : row === 1 ? 0.7 : 1);
    S.enemies.push({ x: 90 + Math.random() * 540, y: 20, hp, max: hp, sp: (row === 1 ? 78 : 52) * (boss ? 0.6 : 1), kind: ['slime', 'beetle', 'golem'][row], boss,
      frame: Math.random() * 6, chill: 0, burn: 0, size: boss ? 150 : row === 1 ? 70 : 80 });
  }

  // ---------- simulation ----------
  const DEF = [[180, GATE_Y - 14], [360, GATE_Y - 24], [540, GATE_Y - 14]].map(([x, y], i) => ({ x, y, cd: i * 0.2 }));
  function step() {
    S.t += DT; S.sessionT += DT; if (wavePop) wavePop.t += DT;
    if (S.spawnLeft > 0 && (S.spawnT -= DT) <= 0) { spawn(); S.spawnLeft--; S.spawnT = Math.max(0.35, 1.1 - P.wave * 0.03); }
    const els = elements();
    for (const d of DEF) {
      if ((d.cd -= DT) > 0) continue;
      let best = null; for (const e of S.enemies) if (e.y > RANGE_Y && (!best || e.y > best.y)) best = e;
      if (!best) continue;
      d.cd = 0.55; d.n = (d.n || 0) + 1;
      const el = els.length ? els[(d.n + DEF.indexOf(d)) % els.length] : 'none';
      S.shots.push({ x: d.x, y: d.y - 40, tx: best, el, sp: 900 });
    }
    for (const s of S.shots) {
      const dx = s.tx.x - s.x, dy = s.tx.y - s.y, L = Math.hypot(dx, dy);
      if (L < 18 || s.tx.hp <= 0) { s.done = true; if (s.tx.hp > 0) hit(s.tx, s.el); continue; }
      s.x += dx / L * s.sp * DT; s.y += dy / L * s.sp * DT;
    }
    S.shots = S.shots.filter(s => !s.done);
    for (const e of S.enemies) {
      if (e.burn > 0) { e.burn -= DT; e.hp -= dmg() * 0.6 * DT; }
      e.chill = Math.max(0, e.chill - DT);
      e.frame += DT * 8;
      if (e.y < GATE_Y - 30) e.y += e.sp * (e.chill > 0 ? 0.45 : 1) * DT;
      else { S.gateHp -= (e.boss ? 14 : 4) * DT; }
      if (e.hp <= 0 && !e.dead) kill(e);
    }
    S.enemies = S.enemies.filter(e => !e.dead);
    for (const p of S.pops) p.t += DT; S.pops = S.pops.filter(p => p.t < p.life);
    if (S.gateHp <= 0) fallBack();
    else if (S.spawnLeft === 0 && S.enemies.length === 0) {
      if (isBoss(P.wave)) { pop(W / 2, 360, 'BOSS DOWN!', '#ffd56b', 1.6, 52); SDK.happytime(); }
      P.wave++; P.bestWave = Math.max(P.bestWave, P.wave); startWave();
    }
    if ((S.saveT += DT) > 5) { S.saveT = 0; save(); }
  }

  function hit(e, el) {
    let d = dmg();
    if (el === 'fire') { if (e.chill > 0) { d *= 3; combo('shatter', e, 'SHATTER!', COLOR.frost); } e.burn = 2; }
    if (el === 'frost') e.chill = 2.2;
    if (el === 'storm') {
      const jumps = e.chill > 0 ? 4 : 2; if (e.chill > 0) combo('frostStorm', e, 'SUPERCHAIN!', COLOR.storm);
      if (e.burn > 0) combo('fireStorm', e, 'FIRESTORM!', COLOR.fire);
      S.enemies.filter(o => o !== e && o.hp > 0).sort((a, b) => Math.hypot(a.x - e.x, a.y - e.y) - Math.hypot(b.x - e.x, b.y - e.y))
        .slice(0, jumps).forEach(o => { o.hp -= d * 0.6; if (e.burn > 0) o.burn = 2; S.pops.push({ arc: [e.x, e.y, o.x, o.y], t: 0, life: 0.18 }); });
    }
    e.hp -= d;
    pop(e.x + (Math.random() - 0.5) * 30, e.y - 40, C.fmt(d), COLOR[el], 0.7, 30);
  }
  function combo(id, e, word, col) {
    pop(e.x, e.y - 80, word, col, 1.1, 44);
    if (!P.combos.includes(id)) { P.combos.push(id); toast(`New combo: ${word.replace('!', '')}  ${P.combos.length}/3`); save(); }
  }
  function kill(e) {
    e.dead = true; const gain = Math.ceil((1 + P.wave / 3) * (e.boss ? 25 : 1) * Math.pow(1.5, P.ascensions));
    P.parts += gain; pop(e.x, e.y - 10, '+' + C.fmt(gain), '#ffd56b', 0.9, 32);
  }
  function fallBack() {
    const to = Math.max(1, P.wave - 3);
    toast(`The gate fell back to wave ${to}. Nothing lost.`); cheerNext = 'Regroup. You\'ve got this!';
    P.wave = to; S.enemies = []; S.shots = []; S.gateHp = gateMax(); startWave(); save();
  }
  function pop(x, y, text, col, life, size) { S.pops.push({ x, y, text, col, t: 0, life, size }); }

  // ---------- actions ----------
  function upgrade() {
    const c = C.upgradeCost(P.gateLevel);
    if (P.parts < c) return refuse('up', c);
    P.parts -= c; P.gateLevel++; S.gateHp = gateMax();
    toast(`Damage ${C.fmt(dmg() / 1.5)} -> ${C.fmt(dmg())}`); if (S.hintStep === 0) S.hintStep = 1; save(); ui();
  }
  function restore() {
    const n = P.restored.length; if (n >= C.BUILDINGS.length) return;
    const c = C.restoreCost(n);
    if (P.parts < c) return refuse('re', c);
    P.parts -= c; const b = C.BUILDINGS[n]; P.restored.push(b);
    const name = { forge: 'Forge restored: defenders gain Fire', frostSpire: 'Frost Spire restored: shots Slow', stormBell: 'Storm Bell restored: chain lightning' }[b] || 'Building restored';
    toast(name); S.reveal = { b, t: 0 }; SDK.happytime(); save(); ui();
  }
  function refuse(id, cost) {
    const b = el(id); b.classList.remove('shake'); void b.offsetWidth; b.classList.add('shake');
    const rate = Math.max(0.5, 1 + P.wave / 3) * 1.4;
    const secs = Math.ceil((cost - P.parts) / rate);
    b.querySelector('.wait').textContent = `~${secs}s`;
  }

  // ---------- rendering ----------
  const cv = document.getElementById('field'), cx = cv.getContext('2d');
  const img = {}; let k = 1;
  function loadImg(name, src) { return new Promise(ok => { const i = new Image(); i.onload = () => { img[name] = i; ok(); }; i.onerror = ok; i.src = src; }); }

  function render() {
    cx.setTransform(k, 0, 0, k, 0, 0);
    // battlefield: wilderness above the gate
    const g = cx.createLinearGradient(0, 0, 0, GATE_Y);
    g.addColorStop(0, '#2f5a2a'); g.addColorStop(1, '#6f9a3c'); cx.fillStyle = g; cx.fillRect(0, 0, W, GATE_Y);
    // city behind the gate
    if (img.ruined) {
      cx.drawImage(img.ruined, 0, 40, 720, 400, 0, CITY_Y, W, CITY_H);
      for (const b of P.restored) {
        const s = SPOT[b]; if (!s || !img.restored) continue;
        const a = S.reveal && S.reveal.b === b ? Math.min(1, S.reveal.t / 0.8) : 1;
        cx.save(); cx.globalAlpha = a; cx.beginPath(); cx.ellipse(s[0], CITY_Y + s[1] - 40, s[2], s[2] * 0.72, 0, 0, 7); cx.clip();
        cx.drawImage(img.restored, 0, 40, 720, 400, 0, CITY_Y, W, CITY_H); cx.restore();
      }
    } else { cx.fillStyle = '#6b6158'; cx.fillRect(0, CITY_Y, W, CITY_H); }
    // gate wall
    cx.fillStyle = '#7d7368'; cx.fillRect(0, GATE_Y, W, 72); cx.fillStyle = '#5a5048'; for (let x = 0; x < W; x += 48) cx.fillRect(x, GATE_Y - 18, 30, 20);
    cx.fillStyle = '#3a2f2a'; cx.fillRect(310, GATE_Y + 8, 100, 64);
    const hp = Math.max(0, S.gateHp / gateMax()); cx.fillStyle = '#2a1a22'; cx.fillRect(200, GATE_Y + 50, 320, 12); cx.fillStyle = hp > 0.35 ? '#7bd15a' : '#e5533d'; cx.fillRect(200, GATE_Y + 50, 320 * hp, 12);
    // defenders
    const els = elements(), towers = ['fire', 'frost', 'storm'];
    DEF.forEach((d, i) => {
      const e = els.length ? els[i % els.length] : null, t = img[(e || towers[i]) + '1'];
      if (!t) return;
      const h = 96, w = t.width / t.height * h;
      if (!e) cx.filter = 'grayscale(1) brightness(.85)';
      cx.drawImage(t, d.x - w / 2, d.y - h + 20, w, h); cx.filter = 'none';
    });
    // enemies
    for (const e of S.enemies) {
      // one asset, one sheet: each enemy draws only from its own 6-frame walk sheet
      const sh = img[e.kind];
      if (sh) { const cw = sh.width / 6, ch = sh.height, dw = e.size * 1.2, dh = dw * ch / cw;
        cx.drawImage(sh, (Math.floor(e.frame) % 6) * cw, 0, cw, ch, e.x - dw / 2, e.y - dh, dw, dh); }
      if (e.chill > 0) { cx.fillStyle = 'rgba(143,227,255,.35)'; cx.beginPath(); cx.arc(e.x, e.y - e.size / 2, e.size / 2, 0, 7); cx.fill(); }
      if (e.burn > 0) { cx.fillStyle = 'rgba(255,122,46,.25)'; cx.beginPath(); cx.arc(e.x, e.y - e.size / 2, e.size / 2.4, 0, 7); cx.fill(); }
      cx.fillStyle = '#2a1a22'; cx.fillRect(e.x - 30, e.y - e.size - 10, 60, 7); cx.fillStyle = '#e5533d'; cx.fillRect(e.x - 30, e.y - e.size - 10, 60 * Math.max(0, e.hp / e.max), 7);
    }
    for (const s of S.shots) { cx.fillStyle = COLOR[s.el]; cx.beginPath(); cx.arc(s.x, s.y, 9, 0, 7); cx.fill(); }
    cx.textAlign = 'center';
    for (const p of S.pops) {
      if (p.arc) { cx.strokeStyle = COLOR.storm; cx.lineWidth = 4; cx.beginPath(); cx.moveTo(p.arc[0], p.arc[1] - 40); cx.lineTo(p.arc[2], p.arc[3] - 40); cx.stroke(); continue; }
      cx.globalAlpha = 1 - p.t / p.life; cx.font = `${p.size}px "HG Display", sans-serif`;
      cx.lineWidth = 6; cx.strokeStyle = '#2a1a22'; cx.strokeText(p.text, p.x, p.y - p.t * 40); cx.fillStyle = p.col; cx.fillText(p.text, p.x, p.y - p.t * 40);
      cx.globalAlpha = 1;
    }
    drawWavePop();
    if (S.reveal && (S.reveal.t += 1 / 60) > 1) S.reveal = null;
  }

  // ---------- DOM UI ----------
  const el = id => document.getElementById(id);
  let toastT = 0;
  function toast(text) { const t = el('toast'); t.textContent = text; t.classList.add('show'); toastT = 2.6; }
  function ui() {
    el('wave').textContent = `Wave ${P.wave}`;
    el('parts').textContent = C.fmt(P.parts);
    el('city').textContent = `City ${P.restored.length}/10`;
    el('combos').textContent = `Combos ${P.combos.length}/3`;
    const uc = C.upgradeCost(P.gateLevel), n = P.restored.length, rc = n < 10 ? C.restoreCost(n) : 0;
    el('up').querySelector('.cost').textContent = C.fmt(uc);
    el('up').querySelector('.fill').style.width = Math.min(100, P.parts / uc * 100) + '%';
    el('up').classList.toggle('ready', P.parts >= uc);
    el('re').querySelector('.cost').textContent = n < 10 ? C.fmt(rc) : 'Done';
    el('re').querySelector('.fill').style.width = n < 10 ? Math.min(100, P.parts / rc * 100) + '%' : '100%';
    el('re').classList.toggle('ready', n < 10 && P.parts >= rc);
    for (const id of ['up', 're']) if (el(id).classList.contains('ready')) el(id).querySelector('.wait').textContent = '';
    // pointing hand: first session only, one button at a time
    const hand = el('hand'); let target = null;
    if (P.firstSession) {
      if (P.gateLevel === 1 && S.sessionT > 6 && P.parts >= uc) target = 'up';
      else if (P.restored.length < C.FIXED_ORDER && P.parts >= rc && (P.gateLevel > 1 || P.restored.length > 0)) target = 're';
      if (P.restored.length >= C.FIXED_ORDER) P.firstSession = false;
    }
    hand.className = target ? 'on ' + target : '';
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
    const st = el('stage'); st.style.transform = `translate(${(vw - W * f) / 2}px,${(vh - H * f) / 2}px) scale(${f})`;
    k = Math.max(0.5, Math.min(2, f * (root.devicePixelRatio || 1)));
    cv.width = Math.round(W * k); cv.height = Math.round(H * k);
    document.documentElement.style.setProperty('--fit', f);
  }
  root.addEventListener('resize', fit);

  // ---------- boot ----------
  function bind() {
    el('up').addEventListener('click', upgrade); el('re').addEventListener('click', restore);
    el('pause').addEventListener('click', () => { S.paused = !S.paused; el('pause').classList.toggle('on', S.paused); SDK.gameplay(!S.paused); });
    root.addEventListener('keydown', e => { if (e.repeat) return; const key = e.key.toLowerCase(); if (key === 'u') upgrade(); else if (key === 'r') restore(); else if (key === 'p') el('pause').click(); });
    if (matchMedia('(hover: hover) and (pointer: fine)').matches) document.documentElement.classList.add('keys');
  }
  fit(); bind(); startWave();
  Promise.all([loadImg('ruined', 'assets/city/hub-ruined.webp'), loadImg('restored', 'assets/city/hub-restored.webp'), loadImg('slime', 'assets/enemies/slime-walk.webp'), loadImg('beetle', 'assets/enemies/beetle-walk.webp'), loadImg('golem', 'assets/enemies/golem-walk.webp'),
    loadImg('fire1', 'assets/towers/fire-1.webp'), loadImg('frost1', 'assets/towers/frost-1.webp'), loadImg('storm1', 'assets/towers/storm-1.webp')])
    .then(() => { el('boot').remove(); SDK.loadingStop(); SDK.gameplay(true); last = performance.now(); requestAnimationFrame(frame); });
})(typeof globalThis !== 'undefined' ? globalThis : this);
