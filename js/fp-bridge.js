// Freeplay layer: replaces sdk.js in the freeplays.site build. Same GameSDK interface the game calls (gameplay,
// loading, happytime, rewarded), with no ads. Progress saves through Freeplay's cloud saves when the player is signed
// in (fp.save/fp.load), and in this browser otherwise. Inert if the Freeplay SDK is missing. Adapted from Warden's fp-bridge.js.
(function (root) {
  'use strict';
  const KEYS = ['hearthgate.profile.v1', 'hearthgate.settings.v1'];
  function score(raw) {
    try { const p = JSON.parse(raw) || {}; return (p.ascensions || 0) * 1e9 + (p.restored ? p.restored.length : 0) * 1e6 + (p.bestWave || 0) * 1e3 + (p.gateLevel || 0); }
    catch (e) { return -1; }
  }
  const GameSDK = {
    ready: false, fp: null, mirror: false, _playing: null, _t: null, rewardsOn: false,
    async init() {
      try {
        if (!root.Freeplay || !root.Freeplay.init) return false;
        this.fp = (await root.Freeplay.init()) || root.Freeplay; await this.sync();
        if (this._playing != null) { const p = this._playing; this._playing = null; this.gameplay(p); }
      } catch (e) { this.fp = null; }
      return !!this.fp;
    },
    snapshot() { const o = {}; for (const k of KEYS) { try { const v = localStorage.getItem(k); if (v != null) o[k] = v; } catch (e) {} } return o; },
    async sync() {
      const fp = this.fp; if (!fp || !fp.load) return;
      let cloud = null; try { cloud = await fp.load(); } catch (e) {}
      try {
        const local = localStorage.getItem(KEYS[0]);
        if (cloud && typeof cloud === 'object' && cloud[KEYS[0]] && (!local || score(cloud[KEYS[0]]) > score(local))) {
          for (const k of KEYS) if (typeof cloud[k] === 'string') orig.call(localStorage, k, cloud[k]);
          if (root.Hearthgate && root.Hearthgate.reloadSave) root.Hearthgate.reloadSave();
        }
      } catch (e) {}
      this.mirror = true; this.push();
    },
    push() { if (!this.fp || !this.fp.save || !this.mirror) return; clearTimeout(this._t); this._t = setTimeout(() => { try { this.fp.save(this.snapshot()); } catch (e) {} }, 1500); },
    gameplay(on) { on = !!on; if (!this.fp) { this._playing = on; return; } if (this._playing === on) return; this._playing = on; try { on ? this.fp.gameplayStart() : this.fp.gameplayStop(); } catch (e) {} },
    loadingStart() {}, loadingStop() {}, happytime() {}, reportCompletion() {},
    rewarded() { return Promise.resolve(false); }
  };
  const orig = Storage.prototype.setItem;
  try { Storage.prototype.setItem = function (k, v) { orig.call(this, k, v); if (this === root.localStorage && /^hearthgate\./.test(k)) GameSDK.push(); }; } catch (e) {}
  root.GameSDK = GameSDK;
  const go = () => GameSDK.init(); if (root.Freeplay) go(); else root.addEventListener('load', go);
})(typeof globalThis !== 'undefined' ? globalThis : this);
