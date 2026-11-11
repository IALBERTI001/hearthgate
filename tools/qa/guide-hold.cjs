// Owner, Nov 10 2026: "the player should acknowledge the text of the game before continuing... did not get to read it because
// it was too fast" and "Restore is too vague". Pins: the Magister's line stays and the fight holds until the player taps; a
// tap moves to the next line; the Rebuild button names the building it brings back. Usage: node guide-hold.cjs <url?qa>
const { chromium } = require('playwright');
(async () => { const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 390, height: 844 } });
  const fail = []; p.on('pageerror', e => fail.push('page error ' + e.message));
  await p.goto(process.argv[2]); await p.waitForFunction(() => !document.getElementById('boot'));
  await p.waitForFunction(() => { const g = document.getElementById('guide'); return g && g.classList.contains('wait'); }, null, { timeout: 15000 });
  const read = () => p.evaluate(() => ({ t: Hearthgate.state().S.t, text: document.querySelector('#guide p').textContent, re: document.querySelector('#re .label').textContent }));
  const a = await read(); await p.waitForTimeout(4000); const b2 = await read();
  if (b2.text !== a.text) fail.push('the line moved on by itself');
  if (b2.t !== a.t) fail.push(`the fight ran while he talked (${a.t} -> ${b2.t})`);
  if (!/^Rebuild \w/.test(a.re)) fail.push(`Rebuild button does not name the building: "${a.re}"`);
  await p.click('#guide'); await p.waitForTimeout(300); const c = await read();
  if (c.text === a.text) fail.push('a tap did not move to the next line');
  console.log(fail.length ? 'GUIDE HOLD FAIL: ' + fail.join('; ') : 'guide holds until read; Rebuild names the building'); await b.close(); process.exit(fail.length ? 1 : 0);
})();
