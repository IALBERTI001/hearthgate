// Every game script must parse. A trailing // comment on a one-line block once swallowed the closing brackets and
// left the game stuck on its boot screen (Nov 5 2026); the core tests never load game.js, so this catches it.
const fs = require('fs'), path = require('path'), vm = require('vm');
const dir = path.join(__dirname, '..', 'js');
for (const f of fs.readdirSync(dir).filter(f => f.endsWith('.js'))) new vm.Script(fs.readFileSync(path.join(dir, f), 'utf8'), { filename: f });
console.log('syntax test passed');
