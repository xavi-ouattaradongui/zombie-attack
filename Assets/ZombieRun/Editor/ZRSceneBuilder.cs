using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieRun.EditorTools
{
    /// <summary>Menu « Zombie Run - Créer la scène » : range les modèles, crée les animations et une scène prête à jouer.</summary>
    public static class ZRSceneBuilder
    {
        const string Root = "Assets/ZombieRun";
        const string Generated = Root + "/Generated";
        const string ScenePath = Root + "/ZombieRun.unity";
        const string Env = Root + "/Environment";

        [MenuItem("Zombie Run/Créer la scène Zombie Run")]
        [MenuItem("Window/Zombie Run - Créer la scène", false, 1)]
        [MenuItem("Assets/Zombie Run - Créer la scène", false, 1)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Zombie Run", "Arrête d'abord le jeu (bouton Play) puis relance ce menu.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(Generated)) AssetDatabase.CreateFolder(Root, "Generated");

            // Réimporte les modèles avec les bons réglages
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Characters", Env }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            ExtractCharacterTextures();
            foreach (var f in System.IO.Directory.GetFiles(Root + "/UI", "*.png"))
                AssetDatabase.ImportAsset(f.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);

            var mats = new ZRMaterials();
            mats.Fill(Save);
            var lib = BuildLibrary();
            var profile = PostFx();
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var night = new Color(0.16f, 0.2f, 0.24f);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.2f, 0.24f, 0.27f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = night;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 55f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.transform.position = new Vector3(-1.1f, 1.6f, 5.6f);

            var moon = new GameObject("Lune").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.78f, 0.84f, 1f);
            moon.intensity = 1.25f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.6f;
            moon.transform.rotation = Quaternion.Euler(40f, 30f, 0f);

            var vol = new GameObject("Post FX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;

            var game = new GameObject("ZombieRunGame").AddComponent<ZRGame>();
            game.library = lib;
            game.materials = mats;
            game.mainCamera = cam;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).Select(s => new EditorBuildSettingsScene(s.path, false)).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();

            string report = $"{(lib.logo != null ? "logo OK, " : "pas de logo, ")}{lib.runners.Length} coureurs, {lib.zombies.Length} zombies, {lib.cars.Length + lib.bigVehicles.Length} véhicules, {lib.shops.Length + lib.towers.Length} immeubles.";
            Debug.Log("[Zombie Run] Scène créée : " + report);
            EditorUtility.DisplayDialog("Zombie Run", "La scène est prête !\n" + report + "\nAppuie sur Play.", "OK");
        }

        /// <summary>
        /// Sort les textures cachées dans les fichiers Mixamo (couleurs des persos) pour que Unity les retrouve.
        /// </summary>
        static void ExtractCharacterTextures()
        {
            var models = AssetDatabase.FindAssets("t:Model", new[] { Root + "/Characters" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.Contains("@")).ToList();
            foreach (var path in models)
            {
                string dir = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                string texRoot = dir + "/Textures";
                if (!AssetDatabase.IsValidFolder(texRoot)) AssetDatabase.CreateFolder(dir, "Textures");
                string texDir = texRoot + "/" + name;
                if (!AssetDatabase.IsValidFolder(texDir)) AssetDatabase.CreateFolder(texRoot, name);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                if (AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }).Length == 0) imp.ExtractTextures(texDir);
            }
            AssetDatabase.Refresh();
            foreach (var path in models) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>La texture de couleur principale d'un perso (fichier « diffuse »).</summary>
        static Texture2D Diffuse(string modelPath)
        {
            string dir = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/') + "/Textures/" + System.IO.Path.GetFileNameWithoutExtension(modelPath);
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            string pick = paths.FirstOrDefault(p => p.ToLower().Contains("diffuse") && !p.ToLower().Contains("hair"))
                       ?? paths.FirstOrDefault(p => p.ToLower().Contains("diffuse"))
                       ?? paths.FirstOrDefault(p => !p.ToLower().Contains("normal") && !p.ToLower().Contains("spec") && !p.ToLower().Contains("gloss") && !p.ToLower().Contains("anim"));
            return pick != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(pick) : null;
        }

        static void Save(Object o, string name)
        {
            string path = Generated + "/" + name + (o is Material ? ".mat" : ".asset");
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(o, path);
        }

        static GameObject M(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        static GameObject[] Many(string folder, params string[] names) =>
            names.Select(n => M($"{Env}/{folder}/FBX/{n}.fbx")).Where(g => g != null).ToArray();

        static ZRLibrary BuildLibrary()
        {
            var lib = new ZRLibrary();
            lib.splash = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/Splash.png");
            lib.logo = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/Logo.png");
            lib.overTitle = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/GameOverTitle.png");
            lib.overPanel = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/ScorePanel.png");
            lib.overRetry = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/RetryButton.png");
            lib.playButton = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/PlayButton.png");
            var art = new System.Collections.Generic.List<Sprite>();
            foreach (var f in System.IO.Directory.GetFiles(Root + "/UI", "*.png"))
            {
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace('\\', '/'));
                if (sp != null) art.Add(sp);
            }
            lib.art = art.ToArray();
            lib.arrowLeft = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/ArrowLeft.png");
            lib.arrowRight = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/UI/ArrowRight.png");
            lib.titleFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/Bangers-Regular.ttf");
            lib.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/UI/Fonts/LilitaOne-Regular.ttf");
            // Musique choisie (Pixabay) : jouée pendant la course ; sinon la musique générée
            var chosen = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/Zombie.mp3");
            lib.runMusic = chosen != null ? chosen : AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/RunMusic.wav");
            lib.menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/MenuMusic.wav"); // musique de l'accueil et du menu
            if (lib.splash == null || lib.logo == null) Debug.LogWarning("[Zombie Run] Images d'accueil manquantes dans " + Root + "/UI");
            string runners = Root + "/Characters/Runners";
            var cast = new (string file, string name, string desc, float h)[]
            {
                ("Timmy", "TIMMY", "Pas encore mordu... et il compte bien le rester !", 1.5f),
                ("Doozy", "DOOZY", "Il a semé plus de zombies qu'il n'a de baskets.", 1.5f),
            };
            lib.runners = cast.Select(c => new ZRCharacterAsset { model = M($"{runners}/{c.file}.fbx"), texture = Diffuse($"{runners}/{c.file}.fbx"), displayName = c.name, description = c.desc, height = c.h })
                              .Where(r => r.model != null).ToArray();
            lib.runnerAnimator = Controller(runners + "/Anim", "Runner", Generated + "/Runner.controller",
                new (string, float, string)[] { ("Idle", 0f, null), ("Run", 0f, null), ("Jump", 0.75f, null), ("Slide", 0.9f, null), ("Fall", 0f, null), ("LookBack", 0f, null) });

            string zombies = Root + "/Characters/Zombies";
            var zNames = new[] { "Mremireh", "CopZombie", "ZombieGirl", "ScoutZombie" }.Where(n => M($"{zombies}/{n}.fbx") != null).ToArray();
            lib.zombies = zNames.Select(n => M($"{zombies}/{n}.fbx")).ToArray();
            lib.zombieTextures = zNames.Select(n => Diffuse($"{zombies}/{n}.fbx")).ToArray();
            lib.zombieAnimator = Controller(zombies + "/Anim", "Zombie", Generated + "/Zombie.controller",
                new (string, float, string)[] { ("Idle", 0f, null), ("Run", 0f, null), ("Scream", 0f, "Idle"), ("Attack", 0f, null) });

            lib.road = M($"{Env}/Roads/FBX/road-straight.fbx");
            lib.shops = Many("Commercial", "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g", "building-h", "building-i", "building-j", "building-k", "building-l", "building-m", "building-n");
            lib.towers = Many("Commercial", "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e");
            lib.houses = Many("Suburban", "building-type-a", "building-type-c", "building-type-e", "building-type-g", "building-type-j", "building-type-m");
            lib.trees = Many("Suburban", "tree-large", "tree-small");
            lib.lamps = Many("Roads", "light-square", "light-curved");
            lib.pole = M($"{Env}/Roads/FBX/electricity-pole.fbx");
            lib.trafficLight = M($"{Env}/Roads/FBX/traffic-light.fbx");
            lib.fence = M($"{Env}/Suburban/FBX/fence-1x4.fbx");
            lib.dumpster = M($"{Env}/Roads/FBX/dumpster.fbx");
            lib.signs = Many("Roads", "road-sign-warning", "road-sign-stop", "construction-light");
            lib.cars = Many("Cars", "sedan", "taxi", "suv", "van", "hatchback-sports", "sedan-sports");
            lib.bigVehicles = Many("Cars", "truck", "ambulance", "firetruck", "garbage-truck", "delivery");
            lib.police = M($"{Env}/Cars/FBX/police.fbx");
            lib.barrier = M($"{Env}/Roads/FBX/construction-barrier.fbx");
            lib.box = M($"{Env}/Cars/FBX/box.fbx");
            lib.tire = M($"{Env}/Cars/FBX/debris-tire.fbx");
            lib.cone = M($"{Env}/Cars/FBX/cone.fbx");
            lib.constructionLight = M($"{Env}/Roads/FBX/construction-light.fbx");
            lib.door = M($"{Env}/Cars/FBX/debris-door.fbx");
            return lib;
        }

        /// <summary>Crée un Animator Controller avec un état par animation (fichiers « Prefix@Etat.fbx »).</summary>
        static AnimatorController Controller(string folder, string prefix, string path, (string state, float seconds, string then)[] states)
        {
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var made = new Dictionary<string, AnimatorState>();
            foreach (var (state, seconds, _) in states)
            {
                AnimationClip clip = null;
                // Course : « Standard Run » de Mixamo (Runner@Run.fbx) ; le sprint ne sert qu'en secours.
                foreach (var file in state == "Run" ? new[] { state, "Sprint" } : new[] { state })
                {
                    clip = AssetDatabase.LoadAllAssetsAtPath($"{folder}/{prefix}@{file}.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip != null) break;
                }
                var st = sm.AddState(state);
                st.motion = clip;
                if (clip != null && seconds > 0f) st.speed = Mathf.Max(0.5f, clip.length / seconds);
                if (clip == null) Debug.LogWarning($"[Zombie Run] Animation manquante : {prefix}@{state}");
                made[state] = st;
                if (state == "Idle") sm.defaultState = st;
            }
            foreach (var (state, _, then) in states)
            {
                if (then == null || !made.ContainsKey(then)) continue;
                var tr = made[state].AddTransition(made[then]);
                tr.hasExitTime = true;
                tr.exitTime = 0.95f;
                tr.duration = 0.2f;
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static VolumeProfile PostFx()
        {
            string path = Generated + "/ZombiePostFX.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.Add<Bloom>(true); bloom.intensity.Override(0.9f); bloom.threshold.Override(0.95f); bloom.scatter.Override(0.7f);
            var color = p.Add<ColorAdjustments>(true); color.saturation.Override(-8f); color.contrast.Override(10f); color.postExposure.Override(0.45f); color.colorFilter.Override(new Color(0.85f, 1f, 0.92f));
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.5f);
            var grain = p.Add<FilmGrain>(true); grain.intensity.Override(0.15f);
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            return p;
        }
    }
}
