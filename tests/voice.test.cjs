// every recorded line the game lists is shipped, every caption names its recording, and nothing unused ships
// (owner recorded them Nov 12 2026; captions over the action since Nov 13 2026)
const fs = require('fs'), path = require('path');
const src = fs.readFileSync(path.join(__dirname, '../js/game.js'), 'utf8');
const listed = [...src.match(/const VOICE = new Set\(\[([\s\S]*?)\]\)/)[1].matchAll(/'([\w-]+)'/g)].map(m => m[1]);
const g = src.slice(src.indexOf('const GUIDE = {'), src.indexOf('const VOICE'));
const used = [...g.matchAll(/\['(?:smile|worry|proud)',[^\]]*?'([\w]+-\d)'\]/g)].map(m => m[1]);
const lines = (g.match(/\['(?:smile|worry|proud)'/g) || []).length;
const missing = listed.filter(n => !fs.existsSync(path.join(__dirname, `../assets/voice/${n}.mp3`)));
const unrecorded = used.filter(n => !listed.includes(n));
const unused = fs.readdirSync(path.join(__dirname, '../assets/voice')).map(f => f.replace('.mp3', '')).filter(n => !listed.includes(n));
if (missing.length || unrecorded.length || unused.length || used.length !== lines || lines < 12) { console.error('voice FAIL', { missing, unrecorded, unused, used: used.length, lines }); process.exit(1); }
console.log(`voice ok: ${lines} captions, each with its recording, all shipped, none unused`);
