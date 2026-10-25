using UnityEditor;
using UnityEngine;

namespace ZombieRun.EditorTools
{
    /// <summary>Outils de test de l'histoire (menu Zombie Run > Histoire).</summary>
    public static class ZRStoryTools
    {
        [MenuItem("Zombie Run/Histoire/Réinitialiser la progression")]
        static void ResetStory()
        {
            ZRStory.ResetAll();
            Debug.Log("[Zombie Attack] Histoire remise à zéro : l'intro se rejouera au prochain JOUER.");
        }

        [MenuItem("Zombie Run/Histoire/Aller au chapitre 5 (16 indices)")]
        static void Chapter5()
        {
            ZRStory.UnlockFirst(16);
            ZRStory.Secret = 0;
            Debug.Log("[Zombie Attack] 16 indices trouvés : chapitre 5 ouvert.");
        }

        [MenuItem("Zombie Run/Histoire/Tous les indices (fin secrète prête)")]
        static void AllClues()
        {
            ZRStory.UnlockFirst(ZRStory.Clues.Length);
            ZRStory.Secret = 0;
            Debug.Log("[Zombie Attack] 20 indices trouvés. En course, dépasse " + ZRStory.SecretDistance + " m (ou maintiens K) pour l'enregistrement final.");
        }

        [MenuItem("Zombie Run/Histoire/Afficher la progression")]
        static void Show()
        {
            Debug.Log($"[Zombie Attack] Indices : {ZRStory.FoundCount}/{ZRStory.Clues.Length} · chapitre en cours : {ZRStory.CurrentChapter + 1} · intro vue : {ZRStory.IntroSeen} · fin secrète : {ZRStory.Secret}");
        }
    }
}
