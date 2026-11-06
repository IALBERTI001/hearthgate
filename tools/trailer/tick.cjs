// Deterministic frames for captures (Nov 6 2026). Playwright's clock faked requestAnimationFrame but the game measured
// time with the real performance.now(), so each screenshot's real duration ran the game about nine times too fast: waves,
// bosses and banners flew past. Here animation frames are queued and run by hand with a made-up time, 1/30 s apart.
const SHIM = () => {
  let q = [], now = 0;
  window.requestAnimationFrame = f => { q.push(f); return q.length; };
  window.__tick = ms => { if (!now) now = performance.now(); const steps = Math.max(1, Math.round(ms / (1000 / 30)));
    for (let i = 0; i < steps; i++) { now += ms / steps; const run = q; q = []; run.forEach(f => f(now)); } };
};
const tick = (p, ms) => p.evaluate(ms => window.__tick(ms), ms);
module.exports = { SHIM, tick };
