// Hearthgate core: compact numbers, the save profile and its sanitizer. Pure logic, no DOM, so Node tests can load it.
(function (root) {
  'use strict';

  // 999, 1K, 1.04K, 12.5K, 3.2M ... no commas, never capped.
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

  // The ten buildings of city layer 1. The first three are restored in this order (approved fix 1, Nov 5 2026).
  const BUILDINGS = ['forge', 'frostSpire', 'stormBell', 'b4', 'b5', 'b6', 'b7', 'b8', 'b9', 'b10'];
  const FIXED_ORDER = 3;

  function defaults() {
    return { v: 1, parts: 0, wave: 1, bestWave: 1, gateLevel: 1, restored: [], combos: [], ascensions: 0,
      lastSeen: 0, daily: { day: '', streak: 0, claimedTotal: 0 }, firstSession: true };
  }

  const num = (v, lo, hi, d) => (typeof v === 'number' && isFinite(v)) ? Math.min(hi, Math.max(lo, v)) : d;

  // Fill missing fields, clamp numbers, drop unknown keys and unknown buildings. Runs on every load.
  function sanitize(raw) {
    const d = defaults(); let p = raw;
    if (typeof p === 'string') { try { p = JSON.parse(p); } catch (e) { p = null; } }
    if (!p || typeof p !== 'object') return d;
    const out = defaults();
    out.parts = num(p.parts, 0, 1e300, 0);
    out.wave = Math.floor(num(p.wave, 1, 1e9, 1));
    out.bestWave = Math.max(out.wave, Math.floor(num(p.bestWave, 1, 1e9, 1)));
    out.gateLevel = Math.floor(num(p.gateLevel, 1, 1e6, 1));
    out.ascensions = Math.floor(num(p.ascensions, 0, 1e6, 0));
    out.lastSeen = num(p.lastSeen, 0, 1e15, 0);
    out.firstSession = p.firstSession !== false;
    out.restored = Array.isArray(p.restored) ? [...new Set(p.restored.filter(b => BUILDINGS.includes(b)))] : [];
    out.combos = Array.isArray(p.combos) ? [...new Set(p.combos.filter(c => ['shatter', 'frostStorm', 'fireStorm'].includes(c)))] : [];
    if (p.daily && typeof p.daily === 'object') {
      out.daily.day = typeof p.daily.day === 'string' ? p.daily.day.slice(0, 10) : '';
      out.daily.streak = Math.floor(num(p.daily.streak, 0, 1e6, 0));
      out.daily.claimedTotal = Math.floor(num(p.daily.claimedTotal, 0, 1e9, 0));
    }
    void d; return out;
  }

  // Upgrade and restore costs. Placeholder curves; set in balancing.
  const upgradeCost = lvl => Math.floor(10 * Math.pow(1.35, lvl - 1));
  const restoreCost = n => n < FIXED_ORDER ? [12, 30, 70][n] : Math.floor(70 * Math.pow(1.8, n - 2));

  // The city painting (Warden's hub art, 1536x1024) is composed off to the left for Warden's hub screen. Hearthgate crops it
  // around its focal point, the central plaza and the city gate below it, so the city sits on the screen's centre line
  // under the gate (user, Nov 5 2026: "why is the city always off center?"). Spots are in source pixels.
  const CITY = { band: [720, 440], crop: [0, 30, 1280, 782], focus: [620, 430],
    spots: { forge: [940, 180, 196], frostSpire: [205, 560, 209], stormBell: [1090, 405, 183], b4: [230, 180, 205], b5: [640, 420, 196], b6: [930, 705, 213] } };
  const cityToBand = (sx, sy) => [(sx - CITY.crop[0]) * CITY.band[0] / CITY.crop[2], (sy - CITY.crop[1]) * CITY.band[1] / CITY.crop[3]];

  root.HGCore = { CITY, cityToBand, fmt, BUILDINGS, FIXED_ORDER, defaults, sanitize, upgradeCost, restoreCost };
  if (typeof module !== 'undefined') module.exports = root.HGCore;
})(typeof globalThis !== 'undefined' ? globalThis : this);
