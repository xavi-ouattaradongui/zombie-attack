using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// Boutique : on dépense ses pièces pour que les super objets durent plus longtemps.
    /// 5 niveaux par pouvoir, chaque niveau ajoute +20 % de durée.
    /// </summary>
    public static class ZRUpgrades
    {
        public const int MaxLevel = 5;
        public static readonly int[] Costs = { 100, 250, 500, 1000, 2000 };
        public static readonly float[] BaseTimes = { 10f, 30f, 10f, 12f }; // aimant, bouclier, super saut, pièces x2
        public static readonly string[] Descriptions =
        {
            "Attire les pièces autour de toi",
            "Encaisse un choc à ta place",
            "Tu sautes beaucoup plus haut",
            "Chaque pièce compte double",
        };

        const string Key = "zr_up_";

        public static int Level(int power) => Mathf.Clamp(PlayerPrefs.GetInt(Key + power, 0), 0, MaxLevel);

        /// <summary>Durée du pouvoir (en secondes) pour un niveau donné.</summary>
        public static float Duration(int power, int level) => BaseTimes[power] * (1f + 0.2f * level);
        public static float Duration(int power) => Duration(power, Level(power));

        /// <summary>Prix du prochain niveau (0 si déjà au maximum).</summary>
        public static int NextCost(int power) { int l = Level(power); return l >= MaxLevel ? 0 : Costs[l]; }

        /// <summary>Achète le niveau suivant si on a assez de pièces. Renvoie vrai si c'est fait.</summary>
        public static bool Buy(int power, string coinsKey)
        {
            int cost = NextCost(power);
            int wallet = PlayerPrefs.GetInt(coinsKey, 0);
            if (cost <= 0 || wallet < cost) return false;
            PlayerPrefs.SetInt(coinsKey, wallet - cost);
            PlayerPrefs.SetInt(Key + power, Level(power) + 1);
            PlayerPrefs.Save();
            return true;
        }
    }
}
