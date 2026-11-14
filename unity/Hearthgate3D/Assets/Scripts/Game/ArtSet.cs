// The art set: every model the game shows, made in Blender (tools/blender/hg_kit.py) and wired by the editor setup
// (HearthgateSetup.WireArt). It lives in its own file because Unity links a ScriptableObject asset to its script by file
// name: inside Art.cs the asset lost its script and every model fell back to a stand-in (Nov 13 2026).
using UnityEngine;

namespace Hearthgate.Game
{
    [CreateAssetMenu(menuName = "Hearthgate/Art Set")]
    public class ArtSet : ScriptableObject
    {
        public GameObject ground, road, gate, wall, towerBase, topStone, topFire, topFrost, topStorm;
        public GameObject slime, beetle, golem, ruin, villager, fountain;
        public GameObject[] buildings = new GameObject[10];   // in the order of HG.BUILDINGS
        public Material lit, glow, particle;                   // material templates, so the build keeps their shaders

        public GameObject Enemy(string model) => model == "beetle" ? beetle : model == "golem" ? golem : slime;
        public GameObject Top(string el) => el == "fire" ? topFire : el == "frost" ? topFrost : el == "storm" ? topStorm : topStone;
    }

}
