// Magister Bram, the town's wizard, guides the first steps (the 2D game's GUIDE, js/game.js): a short line at the moment
// it matters, in the owner's own recorded voice (assets/voice, Nov 12 2026), with the one button to press glowing. Each
// block is said once (Profile.told). Tap the card to skip. The card is the game's one button kit (Hud.Action).
using System.Collections.Generic;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public static class Guide
    {
        // block id -> lines: text, the action to press ("up", "re", "walk" or null), the voice file
        static readonly Dictionary<string, (string text, string press, string voice)[]> LINES = new Dictionary<string, (string, string, string)[]>
        {
            { "intro", new[] { ("Defend the city!", (string)null, "intro-1"), ("Tap Upgrade towers!", "up", "intro-3") } },
            { "restore", new[] { ("Tap Rebuild Forge!", "re", "restore-3") } },
            { "forge", new[] { ("Fire towers!", (string)null, "forge-1") } },
            { "frostSpire", new[] { ("Freeze, then burn: SHATTER!", (string)null, "frostSpire-2") } },
            { "stormBell", new[] { ("Lightning chains!", (string)null, "stormBell-1") } },
            { "boss", new[] { ("A great beast! Hold the walls!", (string)null, "boss-1") } },
            { "fell", new[] { ("Nothing is lost. Push again!", (string)null, "fell-1") } },
            { "whole", new[] { ("The city stands! Wave {aw} to Ascend", (string)null, "whole-1") } },
            { "ascend", new[] { ("Ascend: better than ever!", (string)null, "ascend-1"), ("Tap Ascend!", "re", "ascend-2") } },
            { "gate", new[] { ("At the walls! Upgrade!", "up", "gate-1") } },
            { "surge", new[] { ("Tap the fountain: double Resonance!", (string)null, "surge-1") } },
        };
        static readonly Queue<(string text, string press, string voice)> q = new Queue<(string, string, string)>();
        public static string Pressing;   // the action the current line points at (Hud makes it glow)
        static float t;
        public static bool Busy => q.Count > 0 || t > 0;

        public static void Say(string id, string aw = null)
        {
            var P = GameLoop.I ? GameLoop.I.P : null; if (P == null || !LINES.ContainsKey(id) || P.told.Contains("g_" + id)) return;
            P.told.Add("g_" + id);
            foreach (var l in LINES[id]) q.Enqueue((aw == null ? l.text : l.text.Replace("{aw}", aw), l.press, l.voice));
        }
        // called by Hud every frame: shows the next line when the last one is done
        public static void Tick(float dt, System.Action<string> show, System.Action hide)
        {
            if (t > 0) { t -= dt; if (t <= 0) { Pressing = null; if (q.Count == 0) hide(); } return; }
            if (q.Count == 0) return;
            var l = q.Dequeue(); Pressing = l.press; show(l.text); Sound.Voice(l.voice);
            t = Mathf.Max(2.8f, 0.9f + l.text.Length * 0.07f);
        }
        public static void Skip() { t = Mathf.Min(t, 0.01f); }
    }
}
