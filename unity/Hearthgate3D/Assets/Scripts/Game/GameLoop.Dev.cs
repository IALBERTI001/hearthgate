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
                case "shards": P.shards += int.Parse(a[1]); break;
                case "wave": P.wave = int.Parse(a[1]); break;
                case "shop": foreach (var id in HG.BUILDING_IDS.Where(id => !P.restored.Contains(id)).ToArray()) { P.res += RestoreCost; Restore(id); } break;
                case "spawn":
                    for (int k = 0; k < (a.Length > 2 ? int.Parse(a[2]) : 1); k++)
                    { var en = new Enemy(a[1], P.wave, k % 4, 0, 0.05f, unitsRoot); EventSpawn(en); enemies.Add(en); }
                    break;
            }
            Save(); Debug.Log("[dev] " + cmd);
        }
    }
}
#endif
