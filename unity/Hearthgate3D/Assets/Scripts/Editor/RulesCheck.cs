// Proves the 3D game keeps the 2D game's numbers: values computed by js/core.js (node, Nov 13 2026) checked against the
// C# port. Menu: Hearthgate/Check rules against the 2D game; batch: -executeMethod Hearthgate.EditorTools.RulesCheck.Run
using System;
using UnityEditor;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.EditorTools
{
    public static class RulesCheck
    {
        static readonly (string k, double a, double v, string s)[] GOLD =
        {
            ("UpgradeCost", 1.0, 5.0, null),
            ("TowerDamage", 1.0, 3.0, null),
            ("GateMax", 1.0, 100.0, null),
            ("UpgradeCost", 7.0, 20.0, null),
            ("TowerDamage", 7.0, 10.644444460487303, null),
            ("GateMax", 7.0, 187.0, null),
            ("UpgradeCost", 15.0, 141.0, null),
            ("TowerDamage", 15.0, 57.6047983999765, null),
            ("GateMax", 15.0, 431.0, null),
            ("UpgradeCost", 40.0, 55887.0, null),
            ("TowerDamage", 40.0, 11275.416702553232, null),
            ("GateMax", 40.0, 5855.0, null),
            ("KillRes", 1.0, 4.0, null),
            ("EnemyHp", 1.0, 10.0, null),
            ("WaveCount", 1.0, 8.0, null),
            ("KillRes", 5.0, 7.0, null),
            ("EnemyHp", 5.0, 23.26311300625001, null),
            ("WaveCount", 5.0, 16.0, null),
            ("KillRes", 10.0, 12.0, null),
            ("EnemyHp", 10.0, 66.83479470258246, null),
            ("WaveCount", 10.0, 4.0, null),
            ("KillRes", 23.0, 49.0, null),
            ("EnemyHp", 23.0, 1039.137618069117, null),
            ("WaveCount", 23.0, 20.0, null),
            ("RestoreCost", 0.0, 30.0, null),
            ("RestoreCost", 1.0, 80.0, null),
            ("RestoreCost", 2.0, 220.0, null),
            ("RestoreCost", 3.0, 600.0, null),
            ("RestoreCost", 6.0, 11809.0, null),
            ("CityRate", 4.0, 4.83057, null),
            ("Fmt", 0.0, double.NaN, "0"),
            ("Fmt", 999.0, double.NaN, "999"),
            ("Fmt", 1000.0, double.NaN, "1K"),
            ("Fmt", 1040.0, double.NaN, "1.04K"),
            ("Fmt", 12500.0, double.NaN, "12.5K"),
            ("Fmt", 3200000.0, double.NaN, "3.2M"),
            ("Fmt", 1e+40, double.NaN, "10ab"),
        };

        [MenuItem("Hearthgate/Check rules against the 2D game")]
        public static void Run()
        {
            int bad = 0;
            foreach (var (k, a, v, s) in GOLD)
            {
                var p = new Profile(); int n = (int)a; double got; string gs = null;
                if (k == "CityRate") { p.restored.AddRange(new[] { "forge", "frostSpire", "stormBell", "bakery" }); p.SetLevel("forge", 12); }
                else p.gateLevel = Math.Max(1, k == "UpgradeCost" || k == "TowerDamage" || k == "GateMax" ? n : 1);
                var b = HG.Bonuses(p);
                switch (k)
                {
                    case "UpgradeCost": got = HG.UpgradeCost(n, b); break;
                    case "TowerDamage": got = HG.TowerDamage(p, b); break;
                    case "GateMax": got = HG.GateMax(p, b); break;
                    case "KillRes": got = HG.KillRes(n, false, b); break;
                    case "EnemyHp": got = HG.EnemyHp(n, "slime"); break;
                    case "WaveCount": got = 0; foreach (var (_, c) in HG.WaveMix(n)) got += c; break;
                    case "RestoreCost": got = HG.RestoreCost(n); break;
                    case "CityRate": got = HG.CityRate(p, b); break;
                    default: got = double.NaN; gs = HG.Fmt(a); break;
                }
                bool ok = s != null ? gs == s : Math.Abs(got - v) <= 1e-6 * Math.Max(1, Math.Abs(v));
                if (!ok) { bad++; Debug.LogError($"[Hearthgate] rules differ: {k}({a}) 2D={(s ?? v.ToString())} 3D={(gs ?? got.ToString())}"); }
            }
            Debug.Log(bad == 0 ? $"[Hearthgate] rules match the 2D game ({GOLD.Length} checks)" : $"[Hearthgate] {bad} of {GOLD.Length} rule checks differ");
            if (Application.isBatchMode && bad > 0) EditorApplication.Exit(1);
        }
    }
}
