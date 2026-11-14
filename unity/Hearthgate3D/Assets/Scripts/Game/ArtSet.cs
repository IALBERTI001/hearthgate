// The art set: every model the game shows, made in Blender (tools/blender/hg_kit.py) and wired by the editor setup
// (HearthgateSetup.WireArt). It lives in its own file because Unity links a ScriptableObject asset to its script by file
// name: inside Art.cs the asset lost its script and every model fell back to a stand-in (Nov 13 2026).
using UnityEngine;

namespace Hearthgate.Game
{
    [CreateAssetMenu(menuName = "Hearthgate/Art Set")]
    public class ArtSet : ScriptableObject
    {
        public GameObject ground, road, gate, wall, towerBase, topStone, topFire, topFrost, topStorm, topEarth;
        public GameObject slime, beetle, golem, ruin, villager, fountain;
        public GameObject[] buildings = new GameObject[10];   // in the order of HG.BUILDINGS
        public GameObject[] ruins = new GameObject[10];       // each plot's own burnt shell, same order
        public GameObject marker;                             // where to rebuild next
        public GameObject wizard;                             // the town walk's wizard
        public GameObject[] villagers = new GameObject[0];   // the townsfolk, three outfits (hg_front.villager)
        public GameObject Ruin(int i) => ruins != null && i < ruins.Length && ruins[i] ? ruins[i] : ruin;
        public Material lit, glow, particle;                   // material templates, so the build keeps their shaders
        // Unity's built-in shape meshes, without the colliders CreatePrimitive adds: the WebGL build strips the physics
        // engine code, so CreatePrimitive logged an error for every shot (Nov 13 2026)
        public Mesh sphereMesh, capsuleMesh, cubeMesh, cylinderMesh, planeMesh;
        public Mesh MeshOf(PrimitiveType t) => t == PrimitiveType.Sphere ? sphereMesh : t == PrimitiveType.Capsule ? capsuleMesh
            : t == PrimitiveType.Cylinder ? cylinderMesh : t == PrimitiveType.Plane ? planeMesh : cubeMesh;

        public GameObject[] capLooks = new GameObject[0];        // the Shop's tower caps (hg_front.cap_look), by name
        public GameObject[] variants = new GameObject[0];        // the enemy variants (hg_front.variant), found by name
        public GameObject Enemy(string model)
        {
            foreach (var v in variants) if (v && v.name == model) return v;
            return model == "beetle" || model == "elite" || model == "shielded" || model == "sprinter" || model == "phantom" || model == "thief" ? beetle : model == "golem" || model == "ironhide" || model == "bulwark" || model == "totem" ? golem : slime;
        }
        public GameObject Top(string el) => el == "fire" ? topFire : el == "frost" ? topFrost : el == "storm" ? topStorm : el == "earth" && topEarth ? topEarth : topStone;
    }

}
