# Hearthgate

Hold the gate, and the city rebuilds itself behind you. An idle defense game on one portrait screen, built for CrazyGames first.

- Play locally: `python3 -m http.server` in this folder, then open http://localhost:8000
- Tests: `node tests/core.test.cjs`
- New-player timing run: `node tools/qa/new-player-run.cjs http://localhost:8000/ <outdir> 390 844 75`
- CrazyGames upload zip: `python3 tools/build_crazygames.py` (fails over 20 MB)

Art, fonts and the SDK layer are adapted from Warden of Power. Fonts: Lilita One and Nunito (OFL, licences in assets/fonts).
