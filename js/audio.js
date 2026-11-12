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
      loadVoices();
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
  // ---------- the Magister's voice (owner, Nov 7 2026: "a villager mayor or magician popping out... with a voice thing...
  // like we did for the karate bunny dialogue just better"). Karate Bunnies played one square blip per vowel group; here
  // each syllable is a short voiced sound: a warm triangle tone through two band-pass formants picked from its vowel, an
  // octave shimmer, a speaking melody that falls through the sentence and lifts on a question, and soft attack and release.
  const FORMANT = { a: [750, 1200], e: [480, 1900], i: [320, 2300], o: [500, 900], u: [360, 800], y: [320, 2100] };
  function speak(sylls, base = 132) {
    if (!ctx || !st.sfx || st.muted) return [];
    const t0 = ctx.currentTime + 0.03, nodes = [];
    for (const s of sylls) {
      const t = t0 + s.at, f0 = base * s.tone * (0.96 + Math.random() * 0.08), [f1, f2] = FORMANT[s.v] || FORMANT.a, len = s.len;
      const o = ctx.createOscillator(), o2 = ctx.createOscillator(), g = ctx.createGain(), b1 = ctx.createBiquadFilter(), b2 = ctx.createBiquadFilter(), mix = ctx.createGain();
      o.type = 'triangle'; o.frequency.setValueAtTime(f0, t); o.frequency.linearRampToValueAtTime(f0 * (s.rise ? 1.18 : 0.94), t + len);
      o2.type = 'sine'; o2.frequency.setValueAtTime(f0 * 2, t); const g2 = ctx.createGain(); g2.gain.value = 0.35;
      b1.type = 'bandpass'; b1.frequency.value = f1; b1.Q.value = 5; b2.type = 'bandpass'; b2.frequency.value = f2; b2.Q.value = 7;
      mix.gain.value = 1;
      g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.8, t + 0.018); g.gain.setValueAtTime(0.8, t + len * 0.55); g.gain.exponentialRampToValueAtTime(0.0001, t + len);
      o.connect(b1); o.connect(b2); o2.connect(g2); g2.connect(b1); b1.connect(mix); b2.connect(mix); mix.connect(g); g.connect(gain);
      o.start(t); o2.start(t); o.stop(t + len + 0.03); o2.stop(t + len + 0.03); nodes.push(o, o2);
    }
    return nodes;
  }
  // a soft wooden knock for villagers hammering (no sample: a short band-passed noise tap over a low thump)
  function knock(vol = 0.25) {
    if (!ctx || !st.sfx || st.muted) return; const t = ctx.currentTime, len = 0.07;
    const nb = ctx.createBuffer(1, Math.ceil(ctx.sampleRate * len), ctx.sampleRate), d = nb.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / d.length, 3);
    const n = ctx.createBufferSource(), bp = ctx.createBiquadFilter(), g = ctx.createGain(), o = ctx.createOscillator(), og = ctx.createGain();
    n.buffer = nb; bp.type = 'bandpass'; bp.frequency.value = 900 + Math.random() * 500; bp.Q.value = 3; g.gain.value = vol * 1.6;
    o.type = 'sine'; o.frequency.setValueAtTime(190 + Math.random() * 40, t); o.frequency.exponentialRampToValueAtTime(90, t + len);
    og.gain.setValueAtTime(vol, t); og.gain.exponentialRampToValueAtTime(0.0001, t + len);
    n.connect(bp); bp.connect(g); g.connect(gain); o.connect(og); og.connect(gain); n.start(t); o.start(t); o.stop(t + len + 0.02);
  }
  // recorded voice lines (owner, Nov 10 2026: "i will do the voice-lines"): assets/voice/<name>.mp3, decoded once audio unlocks
  const voices = {}; let voiceList = [];
  function loadVoices(names) { voiceList = names || voiceList; if (!ctx) return;
    for (const n of voiceList) if (!voices[n]) { voices[n] = 'loading'; fetch(`assets/voice/${n}.mp3`).then(r => r.arrayBuffer()).then(b => ctx.decodeAudioData(b)).then(buf => { voices[n] = buf; }).catch(() => { delete voices[n]; }); } }
  const voiceDur = n => (voices[n] && voices[n].duration) || 0;
  const voiceOn = () => !!ctx && st.sfx && !st.muted;
  function voice(n, vol = 0.6) {   // files are levelled to -19 LUFS; 0.6 sits him about 6 dB over the effects
    if (!ctx || !st.sfx || st.muted || !voiceDur(n)) return [];
    const s = ctx.createBufferSource(), g = ctx.createGain(); g.gain.value = vol; s.buffer = voices[n]; s.connect(g); g.connect(gain); s.start();
    const el = mode === 'boss' ? bossEl : mode === 'gap' ? ambEl : songEl;   // the music steps back while he speaks
    if (el && audible()) { fadeTo(el, VOL * 0.4, 0.25); s.onended = () => { if (audible()) fadeTo(el, VOL, 0.8); }; }
    return [s];
  }
  function hush(nodes) { for (const n of nodes || []) { try { n.stop(); } catch (e) { /* already stopped */ } } }
  function set(k, v) { st[k] = v; if (k === 'music' || k === 'muted') applyMusic(); if (ctx && k === 'muted') gain.gain.value = v ? 0 : 0.7; }
  root.HGAudio = { unlock, play, set, boss, speak, hush, knock, voice, voiceDur, voiceOn, loadVoices, state: () => ({ mode, lastSong, boss: st.boss, muted: !!st.muted }) };
})(typeof globalThis !== 'undefined' ? globalThis : this);
