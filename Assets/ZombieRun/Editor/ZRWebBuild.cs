using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieRun.EditorTools
{
    /// <summary>
    /// Version web (WebGL) : construit le jeu dans le dossier « docs » du projet.
    /// GitHub Pages publie ce dossier : après chaque construction, il suffit d'envoyer (push) sur GitHub.
    /// </summary>
    public static class ZRWebBuild
    {
        const string Scene = "Assets/ZombieRun/ZombieRun.unity";

        [MenuItem("Zombie Run/Construire la version web (dossier docs)", priority = 100)]
        public static void Build() => Run(false);

        [MenuItem("Zombie Run/Construire et tester la version web", priority = 101)]
        public static void BuildAndTest() => Run(true);

        static void Run(bool test)
        {
            if (!File.Exists(Scene))
            {
                EditorUtility.DisplayDialog("Zombie Attack", "La scène n'existe pas encore : fais d'abord « Zombie Run → Créer la scène Zombie Run ».", "OK");
                return;
            }
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Réglages de la version web
            PlayerSettings.companyName = "Dongui";
            PlayerSettings.productName = "Zombie Attack";
            PlayerSettings.WebGL.template = "PROJECT:ZombieAttack";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;   // GitHub Pages n'envoie pas les bons en-têtes : le jeu décompresse lui-même
            PlayerSettings.WebGL.dataCaching = true;              // 2e visite : chargement instantané
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.runInBackground = false;

            // Textures des persos en version légère
            foreach (var dir in new[] { "Assets/ZombieRun/Characters/Runners/Textures", "Assets/ZombieRun/Characters/Zombies/Textures" })
                if (AssetDatabase.IsValidFolder(dir)) AssetDatabase.ImportAsset(dir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs"));
            if (Directory.Exists(Path.Combine(output, "Build"))) Directory.Delete(Path.Combine(output, "Build"), true);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = output,
                target = BuildTarget.WebGL,
                options = test ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("Zombie Attack", "La construction a échoué. Regarde la Console (erreurs en rouge).", "OK");
                return;
            }
            File.WriteAllText(Path.Combine(output, ".nojekyll"), "");   // GitHub Pages : publier les fichiers tels quels
            float mb = report.summary.totalSize / (1024f * 1024f);
            Debug.Log($"[Zombie Attack] Version web prête dans {output} ({mb:0.0} Mo).");
            if (!test)
                EditorUtility.DisplayDialog("Zombie Attack",
                    $"Version web prête ({mb:0.0} Mo) dans le dossier « docs ».\n\nPour la mettre en ligne : GitHub Desktop → Commit → Push.\nLe site se met à jour tout seul en 1 à 2 minutes.", "Super");
        }
    }
}
