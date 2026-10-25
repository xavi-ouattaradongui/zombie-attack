using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// Missions : 3 défis actifs à la fois, de plus en plus durs. Une mission réussie rapporte des pièces
    /// et laisse sa place à la suivante. Tout est sauvegardé (PlayerPrefs).
    /// </summary>
    public static class ZRMissions
    {
        public enum Stat { Coins, Jumps, Slides, Powers, RoofLands, Shields, Runs, RunDistance, RunScore }

        public struct Def
        {
            public string text; public Stat stat; public int goal, reward;
            /// <summary>Vrai = à réussir en une seule partie (meilleure partie) ; faux = cumulé sur toutes les parties.</summary>
            public bool PerRun => stat == Stat.RunDistance || stat == Stat.RunScore;
        }

        static readonly Def[] Pool =
        {
            D("Ramasse {0} pièces", Stat.Coins, 100, 100),
            D("Saute {0} fois", Stat.Jumps, 15, 100),
            D("Cours {0} m en une partie", Stat.RunDistance, 500, 150),
            D("Glisse {0} fois", Stat.Slides, 15, 100),
            D("Atteins le Quartier des Saules ({0} m)", Stat.RunDistance, 1300, 300),
            D("Ramasse {0} super objets", Stat.Powers, 3, 150),
            D("Saute sur {0} voitures", Stat.RoofLands, 10, 200),
            D("Ramasse {0} pièces", Stat.Coins, 500, 250),
            D("Fais un score de {0}", Stat.RunScore, 2000, 250),
            D("Joue {0} parties", Stat.Runs, 10, 200),
            D("Casse {0} boucliers", Stat.Shields, 3, 250),
            D("Atteins la Zone industrielle ({0} m)", Stat.RunDistance, 1200, 400),
            D("Saute {0} fois", Stat.Jumps, 100, 300),
            D("Ramasse {0} super objets", Stat.Powers, 10, 400),
            D("Atteins le Port Est ({0} m)", Stat.RunDistance, 3900, 800),
            D("Ramasse {0} pièces", Stat.Coins, 2000, 600),
            D("Fais un score de {0}", Stat.RunScore, 10000, 800),
        };

        static Def D(string t, Stat s, int g, int r) => new Def { text = t, stat = s, goal = g, reward = r };

        public const int Slots = 3;
        const string KSlot = "zr_m_slot", KProg = "zr_m_prog", KNext = "zr_m_next", KDone = "zr_m_done";

        static int[] slot, prog;
        static bool loaded;

        /// <summary>Mission n° i de la liste. Après la fin de la liste, on recommence avec des objectifs plus grands.</summary>
        public static Def Get(int index)
        {
            var d = Pool[index % Pool.Length];
            int round = index / Pool.Length;
            if (round > 0) { d.goal *= round + 1; d.reward = d.reward * (round + 2) / 2; }
            return d;
        }

        public static string Text(int index) { var d = Get(index); return string.Format(d.text, d.goal.ToString("N0").Replace(',', ' ')); }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            slot = new int[Slots]; prog = new int[Slots];
            int next = PlayerPrefs.GetInt(KNext, Slots);
            for (int i = 0; i < Slots; i++)
            {
                slot[i] = PlayerPrefs.GetInt(KSlot + i, i);
                prog[i] = PlayerPrefs.GetInt(KProg + i, 0);
            }
            PlayerPrefs.SetInt(KNext, next);
        }

        public static void Save()
        {
            Load();
            for (int i = 0; i < Slots; i++) { PlayerPrefs.SetInt(KSlot + i, slot[i]); PlayerPrefs.SetInt(KProg + i, prog[i]); }
            PlayerPrefs.Save();
        }

        public static int SlotMission(int i) { Load(); return slot[i]; }
        public static int SlotProgress(int i) { Load(); return Mathf.Min(prog[i], Get(slot[i]).goal); }
        public static int Completed => PlayerPrefs.GetInt(KDone, 0);

        /// <summary>
        /// Ajoute une valeur (cumulé) ou propose un record de partie (par partie).
        /// Renvoie la récompense gagnée (0 si aucune mission terminée) et le texte de la mission réussie.
        /// </summary>
        public static int Report(Stat stat, int value, out string done)
        {
            Load();
            done = null;
            int reward = 0;
            for (int i = 0; i < Slots; i++)
            {
                var d = Get(slot[i]);
                if (d.stat != stat) continue;
                prog[i] = d.PerRun ? Mathf.Max(prog[i], value) : prog[i] + value;
                if (prog[i] < d.goal) continue;
                // Mission réussie : récompense, puis la suivante prend la place
                reward += d.reward;
                done = Text(slot[i]);
                int next = PlayerPrefs.GetInt(KNext, Slots);
                slot[i] = next;
                prog[i] = 0;
                PlayerPrefs.SetInt(KNext, next + 1);
                PlayerPrefs.SetInt(KDone, Completed + 1);
                Save();
            }
            return reward;
        }
    }
}
