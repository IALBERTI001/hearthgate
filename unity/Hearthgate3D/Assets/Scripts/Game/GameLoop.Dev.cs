// Test hooks, compiled into development builds only (HG_DEV=1): the browser calls
// unityInstance.SendMessage("Game", "Dev", "shop") to reach late systems on a throwaway profile. Never in a release build.
#if DEVELOPMENT_BUILD
using System.Linq;
using Hearthgate.Core;
using UnityEngine;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public void Dev(string cmd)
        {
            var a = cmd.Split(' ');
            switch (a[0])
            {
                case "res": P.res += double.Parse(a[1]); break;
                // a fresh-player review without losing the test profile: keep a copy, play fresh, put it back (reload after each)
                case "backup": Save(); PlayerPrefs.SetString(SAVE_KEY + "_bak", PlayerPrefs.GetString(SAVE_KEY, "")); PlayerPrefs.Save(); Debug.Log("[dev] save backed up"); break;
                case "restore": if (PlayerPrefs.HasKey(SAVE_KEY + "_bak")) { PlayerPrefs.SetString(SAVE_KEY, PlayerPrefs.GetString(SAVE_KEY + "_bak")); PlayerPrefs.Save(); noSave = true; Debug.Log("[dev] save restored; reload the page"); } break;
                case "fresh": PlayerPrefs.DeleteKey(SAVE_KEY); PlayerPrefs.Save(); noSave = true; Debug.Log("[dev] save wiped; reload the page"); break;   // a new player's first minutes, on the test profile only
                case "shards": P.shards += int.Parse(a[1]); break;
                case "wave": P.wave = P.runWave = int.Parse(a[1]); break;
                case "asc": P.ascensions = int.Parse(a[1]); Refresh(); for (int i = 0; i < HG.BUILDINGS.Length; i++) RefreshPlot(i); RefreshTowers(); break;
                case "fly": flyT = 0; Debug.Log("[dev] butterfly model: " + (art != null && art.butterfly ? "yes" : "NO")); break;
                case "flies": foreach (var f in meadow) Debug.Log($"[dev] fly {f.kind.id} at {f.tr.position} screen {Camera.main.WorldToScreenPoint(f.tr.position)} scale {f.tr.localScale} rot {f.tr.eulerAngles} t {f.t}"); Debug.Log($"[dev] meadow {meadow.Count} flyT {flyT}"); break;
                case "goto": { int bi = System.Array.IndexOf(HG.BUILDING_IDS, a[1]); if (!Wizard.I.On) Wizard.I.Toggle(); Wizard.I.Teleport(Field.Door(bi, a.Length > 2 ? float.Parse(a[2]) : 1.2f)); break; }
                case "fall": gateHp = 0; GateFell(); break;
                case "puffs": Debug.Log($"[dev] puff model {(art && art.puff ? "yes" : "NO")}, heap {(art && art.rubbleHeap ? "yes" : "NO")}, puffs {puffs.Count}" + (puffs.Count > 0 ? $" first {puffs[0].t.position} scale {puffs[0].t.lossyScale} age {puffs[0].age}" : "")); break;
                case "puffs2": foreach (var pf in puffs) if (pf.age > 1.5f && pf.age < 3f) { var rr = pf.t.GetComponentInChildren<Renderer>(); Debug.Log($"[dev] unit {puffUnit} size {pf.size} life {pf.life} age {pf.age} puff at {pf.t.position} screen {Camera.main.WorldToScreenPoint(pf.t.position)} lossy {pf.t.lossyScale} bounds {(rr ? rr.bounds.size.ToString() : "none")} visible {(rr ? rr.isVisible : false)} enabled {(rr ? rr.enabled : false)} active {pf.t.gameObject.activeInHierarchy} mat {(rr ? rr.sharedMaterial.name + " " + rr.sharedMaterial.color : "")}"); break; } break;
                case "siege":   // enemies that live at the walls, and walls that hold: to watch the attack at the gate
                    gateHp = 1e30;
                    foreach (var kind in new[] { "slime", "armoured", "boss" }.Where(HG.ROLES.ContainsKey)) for (int k = 0; k < 4; k++)
                    { var en = new Enemy(kind, P.wave, k, 0, SpawnZ(k) * 0.01f, unitsRoot); en.hp = en.max = 1e30; enemies.Add(en); }
                    break;
                case "rise": if (P.fallen) StartOver(); break;
                case "pause": Time.timeScale = a[1] == "1" ? 0 : 1; break;
                case "shop": foreach (var id in HG.BUILDING_IDS.Where(id => !P.restored.Contains(id)).ToArray()) { P.res += RestoreCost; Restore(id); } break;
                case "spawn": if (!HG.ROLES.ContainsKey(a[1])) { Debug.Log("[dev] no such kind: " + a[1]); break; }
                    for (int k = 0; k < (a.Length > 2 ? int.Parse(a[2]) : 1); k++)
                    { var en = new Enemy(a[1], P.wave, k % 4, 0, SpawnZ(k % 4) * (a.Length > 3 ? float.Parse(a[3]) : 1f), unitsRoot); EventSpawn(en); enemies.Add(en); }
                    break;
            }
            Save(); Debug.Log("[dev] " + cmd);
        }
    }
}
#endif
