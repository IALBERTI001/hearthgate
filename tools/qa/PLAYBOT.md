# Blind-player bot (Nov 15 2026)

Owner: "You have to take the perspective of a player too ... make a player bot that runs through and stumbles without knowing how the game works."

1. Bring the WebGL build into the workspace (tar it on the PC, stage it, extract to ~/hgweb).
2. Serve it: `python3 -m http.server 8900 --bind 127.0.0.1` (run in the background).
3. Start the browser: `node tools/qa/playbot-server.cjs` (Chromium with SwiftShader, 1280x720). HTTP commands on :8901:
   /shot?name=, /click?x=&y=, /hold?x=&y=&ms=[&tx=&ty=], /key?k=, /wait?ms=, /logs?n=, /fresh (clears the save), /reload.
4. Send in an agent that knows nothing: it may only look at screenshots and use those commands; no source, no docs, no /eval.
   It plays ~45 actions from a fresh save and reports what it thought the game was, every stumble, polish problems, the
   top fixes and two scores (newcomer can play without help; HUD looks professional).
5. Fix everything it found, rebuild, re-run with a fresh agent. Stop only when both scores are 8+.

Notes: the browser renders a few frames a second, so game time runs slow and short pop-ups can be missed; judge feel in the
real browser too. Background processes die after 2 hours: restart them.
