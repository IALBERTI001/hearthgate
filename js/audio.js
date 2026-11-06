// Hearthgate sound: Warden of Power's effects and its engine theme. Music plays by default at low volume (studio rule)
// and only starts after the player's first tap (platform rule). Each effect plays at most every 70 ms so a busy wave
// never turns into noise. Muted while an ad plays.
(function (root) {
  'use strict';
  const SFX = ['fire_launch_0', 'ice_launch_0', 'lightning_launch_0', 'fire_contact_0', 'ice_contact_0', 'lightning_contact_0',
    'shatter_combo', 'freeze_combo', 'conduction_combo', 'tier_up', 'golem_death', 'ascension',
    'buy', 'feat', 'surge', 'claim', 'boss_horn', 'level_up'];   // the last six are made in code (tools/make_sfx.py)
  const st = { music: true, sfx: true, muted: false, unlocked: false };
  let ctx = null, gain = null, buffers = {}, last = {}, track = null;
  function unlock() {
    if (st.unlocked) return; st.unlocked = true;
    try {
      ctx = new (root.AudioContext || root.webkitAudioContext)(); gain = ctx.createGain(); gain.gain.value = 0.7; gain.connect(ctx.destination);
      for (const n of SFX) fetch(`assets/sfx/${n}.mp3`).then(r => r.arrayBuffer()).then(b => ctx.decodeAudioData(b)).then(buf => { buffers[n] = buf; }).catch(() => {});
    } catch (e) { ctx = null; }
    track = new Audio('assets/music/battle.mp3'); track.loop = true; track.volume = 0.16; track.preload = 'auto';
    applyMusic();
  }
  function applyMusic() { if (!track) return; if (st.music && !st.muted) track.play().catch(() => {}); else track.pause(); }
  function play(name, vol = 0.4) {
    if (!ctx || !st.sfx || st.muted || !buffers[name]) return;
    const now = ctx.currentTime; if (last[name] && now - last[name] < 0.07) return; last[name] = now;
    const s = ctx.createBufferSource(), g = ctx.createGain(); g.gain.value = vol; s.buffer = buffers[name];
    s.playbackRate.value = 0.94 + Math.random() * 0.12; s.connect(g); g.connect(gain); s.start();
  }
  function set(k, v) { st[k] = v; if (k === 'music' || k === 'muted') applyMusic(); if (ctx && k === 'muted') gain.gain.value = v ? 0 : 0.7; }
  root.HGAudio = { unlock, play, set };
})(typeof globalThis !== 'undefined' ? globalThis : this);
