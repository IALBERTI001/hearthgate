// Hearthgate CrazyGames layer: SDK init, loading and gameplay events, rewarded ads, saves mirrored to the Data module.
// Inert everywhere else (local play, ad blockers, other portals): no SDK is loaded, no ad is shown, saves stay local.
// Adapted from Warden of Power's game/ads.js (Oct 2026).
(function (root) {
  'use strict';
  const SDK_URL = 'https://sdk.crazygames.com/crazygames-sdk-v3.js';
  const SAVE_PREFIX = /^hearthgate\./;
  const SAVE_KEYS = ['hearthgate.profile.v1', 'hearthgate.settings.v1'];

  function onCrazyGames() {
    try {
      const anc = root.location.ancestorOrigins;
      const hosts = [root.location.hostname, document.referrer, ...(anc ? Array.from(anc) : [])].join(' ');
      return /crazygames\./i.test(hosts);
    } catch (e) { return false; }
  }
  const wanted = !!root.CrazyGames || onCrazyGames() || /[?&]useLocalSdk=true/.test((root.location && root.location.search) || '');
  if (wanted) try { document.documentElement.classList.add('on-crazygames'); } catch (e) {}

  // Higher score = more progressed. Used to pick the cloud or the local copy when both exist.
  function progressScore(raw) {
    try {
      const p = JSON.parse(raw) || {};
      return (p.ascensions || 0) * 1e9 + (p.restored ? p.restored.length : 0) * 1e6 + (p.bestWave || 0) * 1e3 + (p.gateLevel || 0);
    } catch (e) { return -1; }
  }

  const GameSDK = {
    // Rewarded ads stay off in Basic Launch (CrazyGames shows none there). The Full Launch build sets <html data-rewards="on">.
    rewardsOn: (() => { try { return document.documentElement.dataset.rewards === 'on' || /[?&]rewards=on(&|$)/.test(location.search); } catch (e) { return false; } })(),
    ready: false, busy: false, sdk: null, mirror: false, _playing: null, _loading: false, _happyAt: 0,

    async init() {
      if (!wanted) return false;
      try {
        if (!root.CrazyGames) await new Promise((ok, fail) => { const s = document.createElement('script'); s.src = SDK_URL; s.onload = ok; s.onerror = fail; document.head.appendChild(s); });
        const S = root.CrazyGames.SDK; await S.init(); this.sdk = S;
        this.ready = S.environment === 'crazygames' || S.environment === 'local';
      } catch (e) { this.ready = false; }
      if (this.ready) {
        if (this._loading) this.loadingStop(true);
        await this.syncSaves();
        if (this._playing != null) { const p = this._playing; this._playing = null; this.gameplay(p); }
      }
      return this.ready;
    },

    async syncSaves() {
      const S = this.sdk; if (!S || !S.data) return;
      let changed = false;
      try {
        const cloud = S.data.getItem(SAVE_KEYS[0]), local = localStorage.getItem(SAVE_KEYS[0]);
        const cloudWins = cloud && (!local || progressScore(cloud) > progressScore(local));
        for (const k of SAVE_KEYS) {
          const c = S.data.getItem(k), l = localStorage.getItem(k);
          if (cloudWins && c != null && c !== l) { origSet.call(localStorage, k, c); changed = true; }
          else if (!cloudWins && l != null && c !== l) S.data.setItem(k, l);
        }
      } catch (e) {}
      this.mirror = true;
      if (changed && root.Hearthgate && root.Hearthgate.reloadSave) root.Hearthgate.reloadSave();
    },

    // gameplayStart only while the player can actually play; stop on pause, ads and leaving.
    gameplay(on) {
      on = !!on;
      if (!this.ready) { this._playing = on; return; }
      if (this._playing === on) return;
      this._playing = on;
      try { on ? this.sdk.game.gameplayStart() : this.sdk.game.gameplayStop(); } catch (e) {}
    },
    loadingStart() { this._loading = true; if (this.ready) try { this.sdk.game.loadingStart(); } catch (e) {} },
    loadingStop(force) { if (!this._loading && !force) return; this._loading = false; if (this.ready) try { this.sdk.game.loadingStop(); } catch (e) {} },
    // Big moments (boss down, city restored), at most once a minute.
    happytime() { if (!this.ready || Date.now() - this._happyAt < 60000) return; this._happyAt = Date.now(); try { this.sdk.game.happytime(); } catch (e) {} },

    // Resolves true only when the video finished. Never rewards on error, no fill or ad blocker.
    rewarded(hooks = {}) {
      if (!this.ready || !this.rewardsOn || this.busy) return Promise.resolve(false);
      this.busy = true; const was = this._playing; this.gameplay(false);
      return new Promise(done => {
        let settled = false;
        const end = ok => { if (settled) return; settled = true; this.busy = false; try { hooks.unmute && hooks.unmute(); } catch (e) {} if (was) this.gameplay(true); done(ok); };
        try { this.sdk.ad.requestAd('rewarded', { adStarted: () => { try { hooks.mute && hooks.mute(); } catch (e) {} }, adFinished: () => end(true), adError: () => end(false) }); }
        catch (e) { end(false); }
      });
    }
  };

  let origSet = null, origRemove = null;
  try {
    origSet = Storage.prototype.setItem; origRemove = Storage.prototype.removeItem;
    if (wanted) {
      Storage.prototype.setItem = function (k, v) { origSet.call(this, k, v); if (this === root.localStorage && GameSDK.mirror && SAVE_PREFIX.test(k)) try { GameSDK.sdk.data.setItem(k, String(v)); } catch (e) {} };
      Storage.prototype.removeItem = function (k) { origRemove.call(this, k); if (this === root.localStorage && GameSDK.mirror && SAVE_PREFIX.test(k)) try { GameSDK.sdk.data.removeItem(k); } catch (e) {} };
    }
  } catch (e) {}

  root.GameSDK = GameSDK;
  if (wanted) { GameSDK.loadingStart(); GameSDK.init(); }
})(typeof globalThis !== 'undefined' ? globalThis : this);
