// Every model the game shows comes from Blender (tools/blender/hg_kit.py exports them to Assets/Art/Models). The editor
// setup (HearthgateSetup) fills this set from those files. If a model is missing, a plain stand-in is built so the game
// still runs, and it says so in the console: stand-ins never ship (game-art-briefs: no code-drawn art presented as final).
using System.Collections.Generic;
using UnityEngine;

namespace Hearthgate.Game
{
    public static class Art
    {
        public static ArtSet Set;
        static readonly Dictionary<(Color, bool), Material> mats = new Dictionary<(Color, bool), Material>();
        static readonly HashSet<string> warned = new HashSet<string>();

        public static Material Mat(Color c, bool glow = false)
        {
            if (mats.TryGetValue((c, glow), out var m)) return m;   // shared: one material per colour, never one per shot
            // start from the material assets the editor setup made (they keep the URP shader and its emission variant in the
            // build); Shader.Find alone returns a stripped or missing shader in a WebGL player
            var tpl = Set != null ? (glow ? Set.glow : Set.lit) : null;
            m = tpl != null ? new Material(tpl) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.0f); }   // at 2.2 every shot bloomed to white (fresh-eyes review 4: "every shot is a white streak")
            mats[(c, glow)] = m;
            return m;
        }

        // an unlit colour (the particle template): shots and their trails keep their element's hue instead of the lit glow
        // material's light plus emission, which bloomed every shot to a white streak (owner, Nov 14 2026, browser check)
        static readonly Dictionary<Color, Material> flats = new Dictionary<Color, Material>();
        public static Material Flat(Color c)
        {
            if (flats.TryGetValue(c, out var m)) return m;
            m = Set != null && Set.particle != null ? new Material(Set.particle) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); m.color = c;
            return flats[c] = m;
        }

        // an instance of the model, or a labelled stand-in made of primitives
        public static GameObject Spawn(GameObject prefab, string what, Vector3 at, Transform parent = null)
        {
            GameObject g;
            if (prefab != null) g = Object.Instantiate(prefab, at, prefab.transform.rotation, parent);
            else
            {
                if (warned.Add(what)) Debug.LogWarning($"[Hearthgate] no Blender model for '{what}' yet: using a stand-in");
                g = StandIn(what); g.transform.SetParent(parent, false); g.transform.position = at;
            }
            g.name = what;
            foreach (var c in g.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            return g;
        }

        // a built-in shape with a renderer and no collider (see ArtSet.sphereMesh)
        public static GameObject Shape(PrimitiveType t, Material m)
        {
            var g = new GameObject(t.ToString());
            g.AddComponent<MeshFilter>().sharedMesh = Set != null ? Set.MeshOf(t) : null;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        static GameObject Prim(PrimitiveType t, Transform p, Vector3 pos, Vector3 scale, Color c)
        {
            var g = Shape(t, Mat(c)); g.transform.SetParent(p, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; return g;
        }

        static GameObject StandIn(string what)
        {
            var root = new GameObject(what); var t = root.transform;
            switch (what)
            {
                case "slime": Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.5f, 0), new Vector3(1.2f, 1f, 1.2f), new Color(0.45f, 0.85f, 0.4f)); break;
                case "beetle": Prim(PrimitiveType.Capsule, t, new Vector3(0, 0.45f, 0), new Vector3(0.8f, 0.45f, 1.2f), new Color(0.35f, 0.3f, 0.6f)); break;
                case "golem": Prim(PrimitiveType.Cube, t, new Vector3(0, 0.6f, 0), new Vector3(1.1f, 1.2f, 0.9f), new Color(0.55f, 0.5f, 0.45f)); break;
                case "gate": Prim(PrimitiveType.Cube, t, new Vector3(0, 2.5f, 0), new Vector3(4f, 5f, 2f), Field.STONE); break;
                case "wall": Prim(PrimitiveType.Cube, t, new Vector3(0, 1.4f, 0), new Vector3(2 * Field.RX, 2.8f, 1.2f), Field.STONE * 0.9f); break;
                case "towerBase": Prim(PrimitiveType.Cylinder, t, new Vector3(0, 1.8f, 0), new Vector3(1.6f, 1.8f, 1.6f), Field.STONE); break;
                case "ground": Prim(PrimitiveType.Plane, t, new Vector3(0, 0, 10), new Vector3(8, 1, 8), new Color(0.45f, 0.7f, 0.3f)); break;
                case "road": Prim(PrimitiveType.Cube, t, new Vector3(0, 0.01f, 20), new Vector3(5, 0.02f, 40), new Color(0.75f, 0.65f, 0.5f)); break;
                case "ruin": Prim(PrimitiveType.Cube, t, new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 2f), new Color(0.5f, 0.45f, 0.4f)); break;
                case "fountain": Prim(PrimitiveType.Cylinder, t, new Vector3(0, 0.3f, 0), new Vector3(3f, 0.3f, 3f), Field.STONE); break;
                case "villager": Prim(PrimitiveType.Capsule, t, new Vector3(0, 0.45f, 0), new Vector3(0.35f, 0.45f, 0.35f), new Color(0.8f, 0.5f, 0.3f)); break;
                default:
                    if (what.StartsWith("top-")) Prim(PrimitiveType.Sphere, t, new Vector3(0, 0.6f, 0), Vector3.one * 0.9f, Field.Of(what.Substring(4)));
                    else Prim(PrimitiveType.Cube, t, new Vector3(0, 1.3f, 0), new Vector3(2.6f, 2.6f, 2.2f), new Color(0.9f, 0.75f, 0.55f));
                    break;
            }
            return root;
        }
    }
}
