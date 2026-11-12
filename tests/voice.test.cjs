// every recorded line the game lists is shipped, and every line of the Magister's script has its recording (owner, Nov 12 2026)
const fs = require('fs'), path = require('path');
const src = fs.readFileSync(path.join(__dirname, '../js/game.js'), 'utf8');
const listed = [...src.match(/const VOICE = new Set\(\[([\s\S]*?)\]\)/)[1].matchAll(/'([\w-]+)'/g)].map(m => m[1]);
const g = src.slice(src.indexOf('const GUIDE = {'), src.indexOf('const VOICE'));
const lines = []; for (const m of g.matchAll(/^\s{4}(\w+): \[([\s\S]*?)\]\],?$/gm)) { const n = (m[2].match(/\['(?:smile|worry|proud)'/g) || []).length; for (let i = 1; i <= n; i++) lines.push(`${m[1]}-${i}`); }
const missing = listed.filter(n => !fs.existsSync(path.join(__dirname, `../assets/voice/${n}.mp3`)));
const unrecorded = lines.filter(n => !listed.includes(n));
if (missing.length || unrecorded.length || lines.length < 18) { console.error('voice FAIL', { missing, unrecorded, lines: lines.length }); process.exit(1); }
console.log(`voice ok: ${lines.length} lines, all recorded and shipped`);
