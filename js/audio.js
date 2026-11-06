// Hearthgate sound: Warden of Power's effects, a calm playlist made in code, and Warden's engine theme for boss waves. Music plays by default at low volume (studio rule)
// and only starts after the player's first tap (platform rule). Each effect plays at most every 70 ms so a busy wave
// never turns into noise. Muted while an ad plays.
(function (root) {
  'use strict';
  const SFX = ['fire_launch_0', 'ice_launch_0', 'lightning_launch_0', 'fire_contact_0', 'ice_contact_0', 'lightning_contact_0',
    'shatter_combo', 'freeze_combo', 'conduction_combo', 'tier_up', 'golem_death', 'ascension',
    'buy', 'feat', 'surge', 'claim', 'boss_horn', 'level_up'];   // the last six are made in code (tools/make_sfx.py)
  const st = { music: true, sfx: true, muted: false, unlocked: false, boss: false, hidden: false };
  let ctx = null, gain = null, buffers = {}, last = {};
  // ---------- music (Nov 6 2026 rules, game-audio-assets): a calm 8-bit playlist at a steady 72 to 88 BPM, shuffled with
  // no song twice in a row and 30 to 60 s of quiet town ambience between songs; boss waves switch to the battle theme,
  // so the player hears that a test is coming, and a short breath of ambience follows before the calm songs return.
  const SONGS = ['hearthfire', 'lantern-walk', 'starlit-wall', 'rebuilding'], VOL = 0.16, FADE = 1.2;
  let songEl = null, ambEl = null, bossEl = null, lastSong = '', gapT = null, mode = 'idle';
  function fadeTo(el, v, secs, then) {
    if (!el) return; clearInterval(el._f); const from = el.volume, steps = Math.max(1, Math.round(secs * 20)); let k = 0;
    el._f = setInterval(() => { k++; el.volume = Math.max(0, Math.min(1, from + (v - from) * k / steps)); if (k >= steps) { clearInterval(el._f); if (then) then(); } }, 50);
  }
  const audible = () => st.music && !st.muted && !st.hidden;
  function stopEl(el) { if (el) fadeTo(el, 0, FADE, () => el.pause()); }
  function nextSong() {
    clearTimeout(gapT); if (st.boss) return;
    const pool = SONGS.filter(n => n !== lastSong), n = lastSong ? pool[Math.floor(Math.random() * pool.length)] : SONGS[0]; lastSong = n;
    stopEl(ambEl); mode = 'song';
    songEl = new Audio(`assets/music/${n}.mp3`); songEl.volume = 0; songEl.onended = () => gap(30 + Math.random() * 30);
    if (audible()) songEl.play().then(() => fadeTo(songEl, VOL, FADE)).catch(() => {});
  }
  function gap(secs) {
    clearTimeout(gapT); if (st.boss) return; mode = 'gap';
    if (!ambEl) { ambEl = new Audio('assets/ambience/town.mp3'); ambEl.loop = true; }
    ambEl.volume = 0; if (audible()) ambEl.play().then(() => fadeTo(ambEl, VOL, 2)).catch(() => {});
    gapT = setTimeout(nextSong, secs * 1000);
  }
  function boss(on) {
    if (st.boss === on) return; st.boss = on; if (!st.unlocked) return;
    if (on) {
      clearTimeout(gapT); stopEl(songEl); stopEl(ambEl); mode = 'boss';
      if (!bossEl) { bossEl = new Audio('assets/music/battle.mp3'); bossEl.loop = true; }
      bossEl.currentTime = 0; bossEl.volume = 0; if (audible()) bossEl.play().then(() => fadeTo(bossEl, VOL, 0.6)).catch(() => {});
    } else { stopEl(bossEl); gap(10); }   // a breath of quiet after the fight, then the calm songs again
  }
  function unlock() {
    if (st.unlocked) return; st.unlocked = true;
    try {
      ctx = new (root.AudioContext || root.webkitAudioContext)(); gain = ctx.createGain(); gain.gain.value = 0.7; gain.connect(ctx.destination);
      for (const n of SFX) fetch(`assets/sfx/${n}.mp3`).then(r => r.arrayBuffer()).then(b => ctx.decodeAudioData(b)).then(buf => { buffers[n] = buf; }).catch(() => {});
    } catch (e) { ctx = null; }
    if (st.boss) { st.boss = false; boss(true); } else nextSong();
  }
  function applyMusic() {
    const el = mode === 'boss' ? bossEl : mode === 'gap' ? ambEl : songEl; if (!el) return;
    if (audible()) { el.play().then(() => fadeTo(el, VOL, 0.5)).catch(() => {}); } else { clearInterval(el._f); el.pause(); }
  }
  root.document && document.addEventListener('visibilitychange', () => { st.hidden = document.hidden; applyMusic(); });
  function play(name, vol = 0.4) {
    if (!ctx || !st.sfx || st.muted || !buffers[name]) return;
    const now = ctx.currentTime; if (last[name] && now - last[name] < 0.07) return; last[name] = now;
    const s = ctx.createBufferSource(), g = ctx.createGain(); g.gain.value = vol; s.buffer = buffers[name];
    s.playbackRate.value = 0.94 + Math.random() * 0.12; s.connect(g); g.connect(gain); s.start();
  }
  function set(k, v) { st[k] = v; if (k === 'music' || k === 'muted') applyMusic(); if (ctx && k === 'muted') gain.gain.value = v ? 0 : 0.7; }
  root.HGAudio = { unlock, play, set, boss, state: () => ({ mode, lastSong, boss: st.boss }) };
})(typeof globalThis !== 'undefined' ? globalThis : this);
