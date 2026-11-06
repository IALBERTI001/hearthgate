// Hearthgate core: numbers, the save profile, buildings, upgrades, waves, idle rewards and Ascension.
// Pure logic, no DOM, so Node tests can load it. Every balance number lives here.
(function (root) {
  'use strict';

  // ---------- numbers: 999, 1K, 1.04K, 12.5K, 3.2M ... no commas, never capped ----------
  const UNITS = ['', 'K', 'M', 'B', 'T', 'Qa', 'Qi', 'Sx', 'Sp', 'Oc', 'No', 'Dc'];
  function fmt(n) {
    if (!isFinite(n)) return '0';
    const neg = n < 0; n = Math.abs(n);
    if (n < 1000) return (neg ? '-' : '') + String(Math.floor(n));
    let i = 0; while (n >= 1000 && i < 700) { n /= 1000; i++; }
    if (n >= 999.5) { n /= 1000; i++; }
    const s = n >= 100 ? n.toFixed(0) : n >= 10 ? n.toFixed(1) : n.toFixed(2);
    return (neg ? '-' : '') + s.replace(/\.0+$/, '').replace(/(\.\d*?)0+$/, '$1') + unit(i);
  }
  // Past Dc the suffixes run aa, ab ... zz, so numbers keep growing without a cap.
  function unit(i) {
    if (i < UNITS.length) return UNITS[i];
    const j = i - UNITS.length, a = 'abcdefghijklmnopqrstuvwxyz';
    return a[Math.floor(j / 26) % 26] + a[j % 26];
  }

  // ---------- buildings: city layer 1 ----------
  // The first three are restored in this order so SHATTER lands in the first minute (approved fix 1, Nov 5 2026);
  // after them, Restore offers a pick of two. Each building visibly changes the fight or the economy.
  const BUILDINGS = [
    { id: 'forge', name: 'Forge', element: 'fire', line: 'Towers gain Fire. Hits burn.' },
    { id: 'frostSpire', name: 'Frost Spire', element: 'frost', line: 'Towers gain Frost. Hits slow.' },
    { id: 'stormBell', name: 'Storm Bell', element: 'storm', line: 'Towers gain Storm. Hits chain.' },
    { id: 'bakery', name: 'Bakery', bonus: { res: 0.15 }, line: '+15% Resonance from every enemy.' },
    { id: 'market', name: 'Market', bonus: { upgradeDiscount: 0.10 }, line: 'Upgrades cost 10% less.' },
    { id: 'tavern', name: 'Tavern', bonus: { fireRate: 0.10 }, line: 'Towers fire 10% faster.' },
    { id: 'granary', name: 'Granary', bonus: { offline: 0.5 }, line: '+50% Resonance while you are away.' },
    { id: 'library', name: 'Library', bonus: { combo: 0.25 }, line: 'Combos hit 25% harder.' },
    { id: 'cottages', name: 'Cottages', bonus: { gateHp: 0.25 }, line: 'The gate has 25% more health.' },
    { id: 'watchtower', name: 'Watchtower', bonus: { damage: 0.15 }, line: 'Towers hit 15% harder.' }
  ];
  const BUILDING_IDS = BUILDINGS.map(b => b.id);
  const byId = id => BUILDINGS.find(b => b.id === id);
  const FIXED_ORDER = 3;
  const COMBOS = ['shatter', 'frostStorm', 'fireStorm'];

  // Restore choice: the first three in fixed order; after that two unrestored buildings, the same two until one is
  // picked (seeded by how many are restored and the Ascension count, so reloading never rerolls the offer).
  function restoreOffer(p) {
    const left = BUILDING_IDS.filter(id => !p.restored.includes(id));
    if (!left.length) return [];
    if (p.restored.length < FIXED_ORDER) return [BUILDING_IDS[p.restored.length]];
    if (left.length === 1) return left;
    const seed = (p.restored.length * 7 + p.ascensions * 13 + 3) % left.length;
    return [left[seed], left[(seed + 1 + (p.ascensions % (left.length - 1))) % left.length]].filter((v, i, a) => a.indexOf(v) === i);
  }

  // ---------- gate upgrades and forks ----------
  // The gate climbs levels; stages at levels 10 and 25 grow the towers a tier and each offers a fork (approved fix 2).
  const STAGES = [1, 15, 40];
  const FORKS = {
    2: [{ id: 'ballistae', name: 'Heavy Bolts', line: '+40% tower damage', bonus: { damage: 0.40 } },
        { id: 'crews', name: 'Swift Crews', line: '+35% fire rate', bonus: { fireRate: 0.35 } }],
    3: [{ id: 'focus', name: 'Elemental Focus', line: 'Combos hit 50% harder', bonus: { combo: 0.50 } },
        { id: 'ironGate', name: 'Iron Gate', line: 'Double gate health, slow repair', bonus: { gateHp: 1.0, regen: 0.02 } }]
  };
  const stageOf = lvl => lvl >= STAGES[2] ? 3 : lvl >= STAGES[1] ? 2 : 1;
  // A fork is waiting when the gate has reached a stage whose fork has not been chosen yet.
  const pendingFork = p => [2, 3].find(s => stageOf(p.gateLevel) >= s && !p.forks[s]) || null;

  // Every bonus from buildings, forks and Ascension, summed.
  function bonuses(p) {
    const b = { res: 0, upgradeDiscount: 0, fireRate: 0, offline: 0, combo: 0, gateHp: 0, damage: 0, regen: 0 };
    const add = o => { for (const k in o) b[k] += o[k]; };
    for (const id of p.restored) { const d = byId(id); if (d && d.bonus) add(d.bonus); }
    for (const s of [2, 3]) { const f = p.forks[s] && FORKS[s].find(x => x.id === p.forks[s]); if (f) add(f.bonus); }
    b.ascend = Math.pow(ASCEND_MULT, p.ascensions);
    return b;
  }

  const upgradeCost = (lvl, b) => Math.floor(5 * Math.pow(1.27, lvl - 1) * (1 - (b ? b.upgradeDiscount : 0)));
  const restoreCost = n => n < FIXED_ORDER ? [30, 80, 220][n] : Math.floor(600 * Math.pow(2.7, n - 3));
  const towerDamage = (p, b) => 3 * Math.pow(1.235, p.gateLevel - 1) * (1 + b.damage) * b.ascend;
  const fireInterval = b => 0.55 / (1 + b.fireRate);
  const gateMax = (p, b) => Math.floor(100 * Math.pow(1.11, p.gateLevel - 1) * (1 + b.gateHp));
  const killRes = (wave, boss, b) => Math.ceil(4 * Math.pow(1.12, wave - 1) * (boss ? 25 : 1) * (1 + b.res) * b.ascend);

  // ---------- the city makes Resonance (Nov 5 2026, the owner's idea) ----------
  // Every restored building hums out Resonance each second and can be levelled up by tapping it. Its base output follows
  // the order it was restored in (later restores cost more, so they make more); levels 10, 25, 50 and 100 each double it.
  // Kills stay the main income: the city is tuned so the fight pays more than the town until late in a run (pacing bot).
  const MILESTONES = [10, 25, 50, 100];
  const levelOf = (p, id) => p.restored.includes(id) ? (p.levels[id] || 1) : 0;
  function buildingRate(p, id, b, lvl) {
    const idx = p.restored.indexOf(id); if (idx < 0) return 0;
    const L = lvl || levelOf(p, id), dbl = MILESTONES.filter(m => L >= m).length;
    return CITY_BASE * Math.pow(2.7, idx) * L * Math.pow(2, dbl) * b.ascend;
  }
  const cityRate = (p, b) => p.restored.reduce((s, id) => s + buildingRate(p, id, b), 0);
  const levelCost = (p, id, b) => { const idx = p.restored.indexOf(id); return idx < 0 ? Infinity
    : Math.floor(restoreCost(idx) * LEVEL_COST * Math.pow(1.15, levelOf(p, id) - 1) * (1 - b.upgradeDiscount)); };
  const nextMilestone = L => MILESTONES.find(m => m > L) || null;

  // ---------- waves and enemy roles (approved fix 3) ----------
  // slime: basic. runner (beetle): fast, Frost slows it. armoured (golem): Fire burns through the armour.
  // swarm (small slimes): many and weak, Storm chains through them. A boss every 10 waves.
  // Sizes in stage px: every regular enemy is shorter than a tier-1 tower (82 px); only the boss towers over the wall
  // (owner's correction, Nov 5 2026: "regular mobs towering over a small fence seems a stretch").
  const ROLES = {
    slime: { sheet: 'slime', hp: 1, speed: 46, size: 66, label: 'Slimes' },
    runner: { sheet: 'beetle', hp: 0.7, speed: 84, size: 60, label: 'Runners', weak: 'frost' },
    armoured: { sheet: 'golem', hp: 2.6, speed: 36, size: 76, label: 'Armoured', weak: 'fire', armour: 0.5 },
    swarm: { sheet: 'slime', hp: 0.35, speed: 54, size: 44, label: 'Swarm', weak: 'storm' },
    boss: { sheet: 'golem', hp: 90, speed: 28, size: 160, label: 'Boss', armour: 0.3 }
  };
  const isBoss = w => w % 10 === 0;
  // The wave's make-up as [role, count] pairs. New roles arrive one at a time: runners at wave 3, armoured at 6, swarm at 12.
  function waveMix(w) {
    if (isBoss(w)) return [['boss', 1], ['slime', Math.min(8, 2 + Math.floor(w / 10))]];
    const n = 6 + Math.min(14, w * 2);
    const mix = [];
    const runners = w >= 3 ? Math.floor(n * (w % 3 === 0 ? 0.5 : 0.25)) : 0;
    const armoured = w >= 6 ? Math.max(1, Math.floor(n * (w % 4 === 2 ? 0.3 : 0.12))) : 0;
    const swarm = w >= 12 && w % 5 === 2 ? n : 0;
    const slimes = Math.max(0, n - runners - armoured);
    if (slimes) mix.push(['slime', slimes]);
    if (runners) mix.push(['runner', runners]);
    if (armoured) mix.push(['armoured', armoured]);
    if (swarm) mix.push(['swarm', swarm]);
    return mix;
  }
  // How hard an enemy at the gate hits it, per second; grows with the wave like the gate's health grows with its level.
  const enemyBite = (w, role) => (role === 'boss' ? 16 : role === 'armoured' ? 7 : role === 'swarm' ? 2 : 4) * Math.pow(1.12, w - 1);
  const enemyHp = (w, role) => 10 * Math.pow(1.235, w - 1) * ROLES[role].hp;

  // ---------- idle: offline earnings, daily reward ----------
  const OFFLINE_CAP_H = 8, CITY_BASE = 0.09, LEVEL_COST = 0.4;
  // Resonance per second from the fight: about what the current wave pays when fought live.
  const fightRate = (p, b) => 4 * Math.pow(1.12, p.wave - 1) * 1.2 * (1 + b.res) * b.ascend;
  // While the player is away the gate keeps fighting and the city keeps humming; the Granary adds to both.
  const offlineRate = (p, b) => (fightRate(p, b) + cityRate(p, b)) * (1 + b.offline);
  function offlineEarnings(p, b, now) {
    if (!p.lastSeen || now <= p.lastSeen) return { secs: 0, res: 0, fight: 0, city: 0 };
    const secs = Math.min(OFFLINE_CAP_H * 3600, (now - p.lastSeen) / 1000);
    if (secs < 60) return { secs, res: 0, fight: 0, city: 0 };
    const fight = Math.floor(secs * fightRate(p, b) * (1 + b.offline)), city = Math.floor(secs * cityRate(p, b) * (1 + b.offline));
    return { secs, res: fight + city, fight, city };
  }
  // Daily reward: one tap on the first screen of the day; a missed day pauses the streak (never resets it);
  // day 7 of each week pays 5x. Days use the player's local midnight; a clock moved back never pays twice.
  const dayKey = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  function dailyDue(p, now) {
    const today = dayKey(new Date(now));
    if (p.firstSession || !p.daily.day) return null;           // the first session never shows it
    if (today <= p.daily.day) return null;                    // already claimed today, or the clock went back
    const day = p.daily.streak + 1, big = day % 7 === 0;
    // worth about 20 minutes of the gate's income at the best wave reached, so it is felt at once
    const amount = Math.floor(4 * Math.pow(1.12, p.bestWave - 1) * 1.2 * 1200 * (big ? 5 : 1) * Math.pow(ASCEND_MULT, p.ascensions));
    return { today, day, big, amount };
  }

  // ---------- Ascension ----------
  // At 10/10 the player can Ascend: the city returns to ruins, the gate and waves restart, and Resonance and damage are
  // multiplied by 1.5 for good, stacking with each Ascension. Combos found and the best wave stay.
  const ASCEND_MULT = 1.5;
  const canAscend = p => p.restored.length >= BUILDINGS.length;
  function ascend(p) {
    const q = defaults();
    q.ascensions = p.ascensions + 1; q.bestWave = p.bestWave; q.combos = p.combos.slice();
    q.daily = Object.assign({}, p.daily); q.firstSession = false; q.lastSeen = p.lastSeen;
    q.settings = Object.assign({}, p.settings); q.grown = p.grown;
    return q;
  }

  // ---------- save profile ----------
  function defaults() {
    return { v: 3, res: 0, wave: 1, bestWave: 1, gateLevel: 1, restored: [], levels: {}, grown: false, combos: [], forks: {}, ascensions: 0,
      lastSeen: 0, daily: { day: '', streak: 0, claimedTotal: 0 }, firstSession: true,
      settings: { music: true, sfx: true, reduced: false } };
  }
  const num = (v, lo, hi, d) => (typeof v === 'number' && isFinite(v)) ? Math.min(hi, Math.max(lo, v)) : d;
  const LEGACY = { b4: 'bakery', b5: 'market', b6: 'tavern', b7: 'granary', b8: 'library', b9: 'cottages', b10: 'watchtower' };

  // Fill missing fields, clamp numbers, drop unknown keys and buildings, migrate v1 saves. Runs on every load.
  function sanitize(raw) {
    let p = raw;
    if (typeof p === 'string') { try { p = JSON.parse(p); } catch (e) { p = null; } }
    const out = defaults();
    if (!p || typeof p !== 'object') return out;
    out.res = num(p.res !== undefined ? p.res : p.parts, 0, 1e300, 0);   // v2 saves called it Parts
    out.wave = Math.floor(num(p.wave, 1, 1e9, 1));
    out.bestWave = Math.max(out.wave, Math.floor(num(p.bestWave, 1, 1e9, 1)));
    out.gateLevel = Math.floor(num(p.gateLevel, 1, 1e6, 1));
    out.ascensions = Math.floor(num(p.ascensions, 0, 1e6, 0));
    out.lastSeen = num(p.lastSeen, 0, 1e15, 0);
    out.firstSession = p.firstSession !== false;
    const rest = Array.isArray(p.restored) ? p.restored.map(b => LEGACY[b] || b) : [];
    out.restored = [...new Set(rest.filter(b => BUILDING_IDS.includes(b)))];
    if (p.levels && typeof p.levels === 'object') for (const id of out.restored) {
      const L = Math.floor(num(p.levels[id], 1, 1e5, 1)); if (L > 1) out.levels[id] = L;
    }
    out.grown = p.grown === true;
    out.combos = Array.isArray(p.combos) ? [...new Set(p.combos.filter(c => COMBOS.includes(c)))] : [];
    if (p.forks && typeof p.forks === 'object') for (const s of [2, 3]) {
      const f = p.forks[s]; if (typeof f === 'string' && FORKS[s].some(x => x.id === f) && stageOf(out.gateLevel) >= s) out.forks[s] = f;
    }
    if (p.daily && typeof p.daily === 'object') {
      out.daily.day = typeof p.daily.day === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(p.daily.day) ? p.daily.day : '';
      out.daily.streak = Math.floor(num(p.daily.streak, 0, 1e6, 0));
      out.daily.claimedTotal = Math.floor(num(p.daily.claimedTotal, 0, 1e15, 0));
    }
    if (p.settings && typeof p.settings === 'object') for (const k of ['music', 'sfx', 'reduced']) {
      if (typeof p.settings[k] === 'boolean') out.settings[k] = p.settings[k];
    }
    return out;
  }

  // ---------- the city painting ----------
  // Crop of the city painting into the 720x440 band, its focal point (the gatehouse and plaza on the centre line),
  // and each building's spot for its restore reveal, in source pixels. Filled in from the painting by tools/city_layout.py.
  const CITY = root.HG_CITY || { band: [720, 440], crop: [0, 30, 1280, 782], focus: [620, 430], platforms: [], spots: {} };
  const cityToBand = (sx, sy) => [(sx - CITY.crop[0]) * CITY.band[0] / CITY.crop[2], (sy - CITY.crop[1]) * CITY.band[1] / CITY.crop[3]];

  root.HGCore = { fmt, BUILDINGS, BUILDING_IDS, byId, FIXED_ORDER, COMBOS, restoreOffer, STAGES, FORKS, stageOf, pendingFork,
    bonuses, upgradeCost, restoreCost, towerDamage, fireInterval, gateMax, killRes,
    MILESTONES, levelOf, buildingRate, cityRate, levelCost, nextMilestone, fightRate, ROLES, isBoss, waveMix, enemyHp, enemyBite,
    OFFLINE_CAP_H, offlineRate, offlineEarnings, dayKey, dailyDue, ASCEND_MULT, canAscend, ascend, defaults, sanitize,
    CITY, cityToBand };
  if (typeof module !== 'undefined') module.exports = root.HGCore;
})(typeof globalThis !== 'undefined' ? globalThis : this);
