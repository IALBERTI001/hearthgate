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
            // HDR so the glows bloom; soft shadows over the whole field (the camera sees about 80 m deep), two cascades
            rp.supportsHDR = true; rp.msaaSampleCount = 4; rp.shadowDistance = 85f; rp.renderScale = 1f; rp.shadowCascadeCount = 2;
            var so = new SerializedObject(rp);
            void Set(string prop, System.Action<SerializedProperty> f) { var sp = so.FindProperty(prop); if (sp != null) f(sp); else Debug.LogWarning("[Hearthgate] URP asset has no " + prop); }
            Set("m_MainLightShadowmapResolution", sp => sp.intValue = 2048);
            Set("m_SoftShadowsSupported", sp => sp.boolValue = true);
            Set("m_MainLightShadowsSupported", sp => sp.boolValue = true);
            so.ApplyModifiedPropertiesWithoutUndo();
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
            Mesh Builtin(PrimitiveType t) { var g = GameObject.CreatePrimitive(t); var m = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g); return m; }
            art.sphereMesh = Builtin(PrimitiveType.Sphere); art.capsuleMesh = Builtin(PrimitiveType.Capsule); art.cubeMesh = Builtin(PrimitiveType.Cube);
            art.cylinderMesh = Builtin(PrimitiveType.Cylinder); art.planeMesh = Builtin(PrimitiveType.Plane);
            GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS}/{n}.fbx");
            art.ground = M("ground"); art.road = M("road"); art.gate = M("gate"); art.wall = M("wall"); art.towerBase = M("tower_base");
            art.topStone = M("top_stone"); art.topFire = M("top_fire"); art.topFrost = M("top_frost"); art.topStorm = M("top_storm"); art.topEarth = M("top_earth");
            art.slime = M("slime"); art.beetle = M("beetle"); art.golem = M("golem"); art.ruin = M("ruin"); art.villager = M("villager"); art.fountain = M("fountain");
            art.buildings = Hearthgate.Core.HG.BUILDING_IDS.Select(id => M("building_" + id)).ToArray();
            art.ruins = Hearthgate.Core.HG.BUILDING_IDS.Select(id => M("ruin_" + id)).ToArray(); art.marker = M("marker"); art.wizard = M("wizard"); art.villagers = new[] { M("villager_a"), M("villager_b"), M("villager_c") }.Where(x => x).ToArray();
            art.capLooks = new[] { "top_fire_obsidian", "top_fire_ember", "top_frost_night", "top_frost_aurora", "top_storm_void", "top_storm_steel" }.Select(M).Where(x => x).ToArray();
            art.variants = new[] { "elite", "shielded", "sprinter", "splitter", "mender", "ironhide", "bulwark", "phantom", "thief", "bomber", "totem" }.Select(M).Where(x => x).ToArray();
            EditorUtility.SetDirty(art);
            int have = new[] { art.ground, art.road, art.gate, art.wall, art.towerBase, art.topStone, art.topFire, art.topFrost, art.topStorm, art.slime, art.beetle, art.golem, art.ruin, art.fountain, art.wizard }.Count(x => x) + art.buildings.Count(x => x);
            Debug.Log($"[Hearthgate] art set: {have} of 25 models found in {MODELS}");
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
                : art.gate == null || art.slime == null || art.lit == null || art.sphereMesh == null ? "the art set is missing models, materials or meshes" : null;
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
            var camData = cam.GetComponent<UniversalAdditionalCameraData>(); camData.renderPostProcessing = true; camData.renderShadows = true;
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = PostProfile();
            var boot = new GameObject("Boot").AddComponent<Boot>(); boot.art = art;
            EditorSceneManager.SaveScene(scene, SCENE);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SCENE, true) };
        }
        // the look: neutral tonemapping, a little contrast and saturation, bloom on the glows, a soft vignette
        const string POST = "Assets/Settings/HearthgatePost.asset";
        public static VolumeProfile PostProfile()
        {
            Directory.CreateDirectory("Assets/Settings");
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(POST);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, POST); }
            T Get<T>() where T : VolumeComponent
            {
                if (p.TryGet<T>(out var c)) return c;
                c = p.Add<T>(true); c.name = typeof(T).Name; AssetDatabase.AddObjectToAsset(c, p); return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>(); ca.postExposure.Override(-0.15f); ca.contrast.Override(16f); ca.saturation.Override(4f);
            var bl = Get<Bloom>(); bl.threshold.Override(1.0f); bl.intensity.Override(0.85f); bl.scatter.Override(0.65f);
            var vg = Get<Vignette>(); vg.intensity.Override(0.32f); vg.smoothness.Override(0.5f); vg.color.Override(new Color(0.12f, 0.08f, 0.1f));
            EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
            return p;
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
            PlayerSettings.WebGL.nameFilesAsHashes = true;   // a new build gets new file names: a browser that cached the last build's engine
                                                             // with this build's data crashed with a stack overflow (Nov 14 2026)
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
            PlayerSettings.bundleVersion = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");   // the build stamp in Settings (crazygames-qa)
            LayoutCheck.Run();   // fails the batch build on an overlap, a hidden plot, the HUD over the world, or the view cropping
            var opts = new BuildPlayerOptions { scenes = new[] { SCENE }, locationPathName = "Builds/WebGL", target = BuildTarget.WebGL, options = System.Environment.GetEnvironmentVariable("HG_DEV") == "1" ? BuildOptions.Development : BuildOptions.None };   // HG_DEV=1: named stack traces
            var r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[Hearthgate] WebGL build: {r.summary.result}, {r.summary.totalSize / 1048576f:0.0} MB, {r.summary.totalErrors} errors");
            if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
