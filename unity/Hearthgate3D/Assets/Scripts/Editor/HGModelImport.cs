// How the Blender models come into Unity (Nov 13 2026). The kit names its materials HG_<palette name>[_glow]; this sets
// what the FBX cannot carry reliably: painted surfaces are matte, '_gloss' ones (slimes, water, crystal cores) are shiny,
// '_glow' ones (fire, frost, storm, lit windows) emit their own colour so the bloom picks them up. Models import without
// cameras, lights, animation or colliders, with compressed meshes for the WebGL download.
using UnityEditor;
using UnityEngine;

namespace Hearthgate.EditorTools
{
    public class HGModelImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.Contains("Assets/Art/Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false; mi.importLights = false; mi.importAnimation = false; mi.addCollider = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium; mi.isReadable = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        void OnPostprocessMaterial(Material m)
        {
            if (!assetPath.Contains("Assets/Art/Models/")) return;
            string n = m.name;
            bool gloss = n.Contains("gloss"), glow = n.EndsWith("_glow");
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", gloss ? 0.78f : 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", n.Contains("gold") || n.Contains("iron") ? 0.35f : 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 1f);
            if (glow)
            {
                var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                m.EnableKeyword("_EMISSION");
                // gentle: the bloom does the rest; at 2.4 fire and frost blew out to white and the elements stopped reading
                // fire is held lower still: bright orange drifts to yellow in the tonemapper and the element stops reading
                m.SetColor("_EmissionColor", c * (n.Contains("window") ? 1.0f : n.Contains("fire") ? 0.9f : 1.3f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
        }
    }
}
