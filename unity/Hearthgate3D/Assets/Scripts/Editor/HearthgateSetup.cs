// One-step setup, so the project needs no hand-made scene and can run in batch mode:
//   Unity.exe -batchmode -quit -projectPath <this folder> -executeMethod Hearthgate.EditorTools.HearthgateSetup.SetupAll -logFile setup.log
//   Unity.exe -batchmode -quit -projectPath <this folder> -executeMethod Hearthgate.EditorTools.HearthgateSetup.BuildWebGL -logFile build.log
// SetupAll: URP on, the Blender models (Assets/Art/Models/*.fbx) wired into the art set, the scene built and listed, the
// WebGL settings CrazyGames asks for (crazygames-sdk-starter: small download, no fullscreen button, data caching).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Hearthgate.Game;

namespace Hearthgate.EditorTools
{
    public static class HearthgateSetup
    {
        const string SCENE = "Assets/Scenes/Main.unity", ART = "Assets/Art/HearthgateArt.asset", MODELS = "Assets/Art/Models";

        [MenuItem("Hearthgate/Set up everything")]
        public static void SetupAll() { SetupRendering(); var art = WireArt(); BuildScene(art); SetupWebGL(); AssetDatabase.SaveAssets(); VerifyArt(); Debug.Log("[Hearthgate] setup done"); }

        [MenuItem("Hearthgate/Rendering (URP)")]
        public static void SetupRendering()
        {
            Directory.CreateDirectory("Assets/Settings");
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/HearthgateURP.asset");
            if (rp == null)
            {
                var data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, "Assets/Settings/HearthgateRenderer.asset");
                rp = UniversalRenderPipelineAsset.Create(data);
                AssetDatabase.CreateAsset(rp, "Assets/Settings/HearthgateURP.asset");
            }
            rp.supportsHDR = false; rp.msaaSampleCount = 4; rp.shadowDistance = 90f; rp.renderScale = 1f;
            GraphicsSettings.defaultRenderPipeline = rp;
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = rp; }
            EditorUtility.SetDirty(rp);
        }

        // every model exported by tools/blender/hg_kit.py is named for its slot (gate.fbx, top_fire.fbx, building_forge.fbx ...)
        [MenuItem("Hearthgate/Wire Blender models")]
        static void WireArtMenu() => WireArt();
        public static ArtSet WireArt()
        {
            Directory.CreateDirectory(MODELS);
            AssetDatabase.Refresh();
            var art = AssetDatabase.LoadAssetAtPath<ArtSet>(ART);
            // an asset whose script link broke loads as null: replace it instead of failing on "already exists"
            if (art == null) { AssetDatabase.DeleteAsset(ART); art = ScriptableObject.CreateInstance<ArtSet>(); AssetDatabase.CreateAsset(art, ART); }
            art.lit = MatAsset("Lit", "Universal Render Pipeline/Lit", false);
            art.glow = MatAsset("Glow", "Universal Render Pipeline/Lit", true);
            art.particle = MatAsset("Particle", "Universal Render Pipeline/Particles/Unlit", false);
            GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS}/{n}.fbx");
            art.ground = M("ground"); art.road = M("road"); art.gate = M("gate"); art.wall = M("wall"); art.towerBase = M("tower_base");
            art.topStone = M("top_stone"); art.topFire = M("top_fire"); art.topFrost = M("top_frost"); art.topStorm = M("top_storm");
            art.slime = M("slime"); art.beetle = M("beetle"); art.golem = M("golem"); art.ruin = M("ruin"); art.villager = M("villager"); art.fountain = M("fountain");
            art.buildings = Hearthgate.Core.HG.BUILDING_IDS.Select(id => M("building_" + id)).ToArray();
            EditorUtility.SetDirty(art);
            int have = new[] { art.ground, art.road, art.gate, art.wall, art.towerBase, art.topStone, art.topFire, art.topFrost, art.topStorm, art.slime, art.beetle, art.golem, art.ruin, art.fountain }.Count(x => x) + art.buildings.Count(x => x);
            Debug.Log($"[Hearthgate] art set: {have} of 24 models found in {MODELS}");
            return art;
        }

        // material templates the game copies at run time; being assets referenced by the art set, the build keeps their
        // shaders and the emission variant (Shader.Find in a WebGL player finds nothing that no asset uses)
        static Material MatAsset(string n, string shader, bool emission)
        {
            string path = $"Assets/Art/Mat_{n}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.white); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            EditorUtility.SetDirty(m);
            return m;
        }

        // the check that would have caught the Nov 13 2026 bug: reload the art set from disk and the scene's reference to it
        public static void VerifyArt()
        {
            AssetDatabase.SaveAssets(); Resources.UnloadUnusedAssets();
            var art = AssetDatabase.LoadAssetAtPath<ArtSet>(ART);
            var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            var boot = Object.FindFirstObjectByType<Boot>();
            string bad = art == null ? "the art set asset has no script (is ArtSet in ArtSet.cs?)"
                : boot == null || boot.art != art ? "the scene's Boot does not reference the art set"
                : art.gate == null || art.slime == null || art.lit == null ? "the art set is missing models or materials" : null;
            if (bad == null) { Debug.Log("[Hearthgate] art check passed"); return; }
            Debug.LogError("[Hearthgate] art check FAILED: " + bad);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        public static void BuildScene(ArtSet art)
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
            cam.AddComponent<UniversalAdditionalCameraData>();
            var boot = new GameObject("Boot").AddComponent<Boot>(); boot.art = art;
            EditorSceneManager.SaveScene(scene, SCENE);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE, true) };
        }
        [MenuItem("Hearthgate/Build the scene")]
        static void BuildSceneMenu() => BuildScene(WireArt());

        [MenuItem("Hearthgate/WebGL settings")]
        public static void SetupWebGL()
        {
            PlayerSettings.companyName = "Hearthgate Studio"; PlayerSettings.productName = "Hearthgate";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;      // works on hosts that do not send Content-Encoding
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.defaultWebScreenWidth = 1280; PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.template = "PROJECT:Hearthgate";   // full-window canvas, no Unity frame or fullscreen button
        }

        [MenuItem("Hearthgate/Build WebGL")]
        public static void BuildWebGL()
        {
            SetupAll();
            var opts = new BuildPlayerOptions { scenes = new[] { SCENE }, locationPathName = "Builds/WebGL", target = BuildTarget.WebGL, options = BuildOptions.None };
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Hearthgate] WebGL build: {r.summary.result}, {r.summary.totalSize / 1048576f:0.0} MB, {r.summary.totalErrors} errors");
            if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
