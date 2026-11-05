// The city painting's layout, measured on art/masters/city-north-gate-restored.png (1536x1024, Nov 5 2026).
// All values are source pixels; the game shows the crop at exactly half scale (1440x960 -> 720x480).
// Pinned by tests/core.test.cjs: the gate sits on the screen's centre line and the platforms sit inside the wall.
(function (root) {
  'use strict';
  root.HG_CITY = {
    band: [720, 480],
    crop: [40, 0, 1440, 960],
    focus: [767, 493],                 // the plaza fountain, under the gate
    gate: [767, 100],                  // the gatehouse arch
    wallTop: 64,                       // where enemies press against the wall
    gateBar: 236,                      // the gate's health bar, on the road just inside the arch
    platforms: [[220, 133], [460, 133], [1073, 133], [1313, 133]],
    platformR: 30,                     // stage px
    towerH: 82,                        // stage px, tier 1
    towerSink: 12,                     // tower base sits this far below the platform's centre
    spots: {                           // building centre x, y and reveal radii rx, ry
      frostSpire: [407, 287, 150, 116], forge: [1087, 313, 150, 116], stormBell: [1347, 433, 138, 122],
      bakery: [187, 460, 150, 110], market: [427, 600, 150, 110], tavern: [1120, 593, 150, 116],
      granary: [213, 773, 150, 122], plaza: [767, 470, 200, 150], library: [587, 800, 152, 122], cottages: [953, 800, 152, 116], watchtower: [1333, 767, 138, 130]
    }
  };
})(typeof globalThis !== 'undefined' ? globalThis : this);
