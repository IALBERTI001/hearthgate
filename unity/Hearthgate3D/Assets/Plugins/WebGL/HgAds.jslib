// The rewarded ad for restoring the fallen town (GameLoop.Fall). It asks the CrazyGames SDK v3 only when the page has it
// and has ads on (window.hgAdsOn, set by the Full Launch page; Basic Launch has ads off, so the button never shows there).
// The result comes back to the game object 'Game' as AdDone("1") when watched, AdDone("0") on any error: no reward then.
mergeInto(LibraryManager.library, {
  HG_AdReady: function () {
    try { var s = window.CrazyGames && window.CrazyGames.SDK; return (window.hgAdsOn && s && s.ad && s.ad.requestAd) ? 1 : 0; }
    catch (e) { return 0; }
  },
  HG_AdRewarded: function () {
    var done = function (ok) { try { window.hgUnity && window.hgUnity.SendMessage('Game', 'AdDone', ok ? '1' : '0'); } catch (e) {} };
    try {
      window.CrazyGames.SDK.ad.requestAd('rewarded', {
        adStarted: function () {},
        adFinished: function () { done(true); },
        adError: function () { done(false); }
      });
    } catch (e) { done(false); }
  }
});
