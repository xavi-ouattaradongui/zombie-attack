using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// Scénario de Zombie Attack : La Dernière Course (données) + progression sauvegardée.
    /// Généré depuis le document de scénario : 5 chapitres, 20 indices, radios d'ambiance, intro, fin secrète.
    /// </summary>
    public static class ZRStory
    {
        public enum ClueType { Radio, Note, Rapport, Audio, Dossier, Alerte, Temoignage }
        /// <summary>Forme de l'objet sur la route : 0 radio, 1 document, 2 cassette, 3 dossier rouge, 4 enregistrement final.</summary>
        public struct Clue { public int chapter; public ClueType type; public string title, speaker, text; public int style; }
        public struct Line { public string speaker, text; public Line(string s, string t) { speaker = s; text = t; } }
        public struct Chapter { public string name, goal; public int gate; public Line[] ambient; }

        public static readonly string[] TypeNames = { "MESSAGE RADIO", "NOTE", "RAPPORT D'INCIDENT", "ENREGISTREMENT AUDIO", "DOSSIER PATIENT ZÉRO", "ALERTE OFFICIELLE", "TÉMOIGNAGE" };
        public const int CluesPerChapter = 4;
        public const float SecretDistance = 1500;

        public static readonly Chapter[] Chapters =
        {
            new Chapter { name = "LES RUES ABANDONNÉES", goal = "Survivre. Trouver d'autres survivants.", gate = 250, ambient = new[] { new Line("Préfecture", "Ceci est un message de la préfecture de Valmonde. Restez chez vous. N'approchez pas des personnes blessées."), new Line("Nora", "…quelqu'un ? Ici Nora, sur le 88.6. Si tu cours, surtout ne t'arrête pas."), new Line("Voix inconnue", "Si quelqu'un nous entend, répondez. Le centre d'évacuation est toujours opérationnel… enfin, c'est ce qu'on nous a dit.") } },
            new Chapter { name = "LE MÉTRO INFESTÉ", goal = "Passer sous la ville. Comprendre ce qu'on nous cache.", gate = 400, ambient = new[] { new Line("Nora", "Delmas t'envoie dans la ligne 4 ? Bizarre. Elle est fermée depuis trois jours, « pour travaux »."), new Line("Voix inconnue", "N'approchez pas du secteur scientifique. Je répète : ne vous approchez pas du laboratoire."), new Line("Nora", "Les vitres des rames sont couvertes de traces de mains… à l'intérieur. Qui ferme une rame de l'extérieur ?") } },
            new Chapter { name = "LE QUARTIER INDUSTRIEL", goal = "Traverser la zone contaminée. Suivre la piste de l'Institut.", gate = 600, ambient = new[] { new Line("Nora", "Le brouillard vert au-dessus des docks, ce n'est pas de la pollution. Respire le moins possible."), new Line("Préfecture", "Zone industrielle nord : confinement sanitaire de niveau 4. Tout contrevenant sera… (coupure)"), new Line("Nora", "Ceux qui courent le plus vite ont tous été mordus il y a plus de deux jours. Le virus les change.") } },
            new Chapter { name = "LE LABORATOIRE SECRET", goal = "Entrer dans le Secteur 9. Trouver le patient zéro.", gate = 800, ambient = new[] { new Line("Nora", "Tu vas vraiment à l'Institut ? Si tu croises le Dr Valen, demande-lui pour mon frère. Il travaillait là-bas."), new Line("Voix inconnue", "Sujet 0, retournez en confinement. Sujet 0, répondez. (message en boucle)"), new Line("Nora", "Les mutants autour de l'Institut ne ressemblent pas aux autres. Leur peau… on dirait qu'elle a fondu.") } },
            new Chapter { name = "LA ZONE D'ÉVACUATION", goal = "Atteindre le Port Est. Survivre à la grande horde.", gate = 1000, ambient = new[] { new Line("Capitaine Delmas", "À tous les survivants : le Port Est est encerclé. Dernier départ cette nuit. Ensuite, nous fermons."), new Line("Nora", "Je vois le port. Il y en a des milliers devant les grilles. Passe par les côtés, pas par l'entrée principale."), new Line("Voix inconnue", "Si vous trouvez cet enregistrement, ne cherchez pas seulement à fuir. Découvrez ce qui s'est réellement passé.") } },
        };

        public static readonly Clue[] Clues =
        {
            new Clue { chapter = 0, type = ClueType.Radio, style = 0, title = "Premier contact", speaker = "Nora", text = "Je te vois depuis le toit de la tour Bellune. Tu cours vite, c'est bien. Ils sont attirés par le bruit, pas par la lumière." },
            new Clue { chapter = 0, type = ClueType.Alerte, style = 1, title = "Message d'urgence n°3", speaker = "Préfecture", text = "Un centre d'évacuation est ouvert au Port Est. Rejoignez-le par vos propres moyens. Les ponts du centre-ville sont fermés." },
            new Clue { chapter = 0, type = ClueType.Temoignage, style = 1, title = "Mot sur une porte", speaker = "Lou", text = "Lou, infirmière à Saint-Aubin. Nous sommes six dans la pharmacie. Après une morsure : fièvre, une heure de silence, puis ils se relèvent." },
            new Clue { chapter = 0, type = ClueType.Radio, style = 0, title = "Port Est", speaker = "Capitaine Delmas", text = "Ici le capitaine Delmas, Port Est. Un bateau part chaque nuit à deux heures. Contournez le centre par la ligne 4 du métro. Les tunnels sont… praticables." },
            new Clue { chapter = 1, type = ClueType.Temoignage, style = 1, title = "Carnet du conducteur", speaker = "Samir", text = "On m'a ordonné d'arrêter la rame entre deux stations et de bloquer les portes. Il restait des passagers. « Quarantaine, deux heures. » Ça fait trois jours." },
            new Clue { chapter = 1, type = ClueType.Alerte, style = 1, title = "Ordre de fermeture", speaker = "Préfecture", text = "Stations Hauterive, Mirabel et Quai-Noir : fermeture définitive à 23 h. Aucune évacuation des occupants. Copie transmise à l'Institut Noctis." },
            new Clue { chapter = 1, type = ClueType.Note, style = 1, title = "Graffiti sur le quai", speaker = "Voix inconnue", text = "ILS SAVAIENT. LE PREMIER MALADE EST ARRIVÉ À SAINT-AUBIN SIX JOURS AVANT L'ALERTE." },
            new Clue { chapter = 1, type = ClueType.Radio, style = 0, title = "Fréquence coupée", speaker = "Voix inconnue", text = "Ils nous ont menti. Le premier cas a été signalé plusieurs jours avant l'alerte officielle. Cherchez le Secteur 9… (grésillements)" },
            new Clue { chapter = 2, type = ClueType.Note, style = 1, title = "Bon de livraison", speaker = "Institut Noctis", text = "Destinataire : Institut Noctis, Secteur 9. Contenu : 40 doses, souche L-0. Mention : Projet LAZARE, confidentiel. Livré quatorze jours avant l'alerte." },
            new Clue { chapter = 2, type = ClueType.Note, style = 1, title = "Note de service", speaker = "Directeur Kessler", text = "Aucune information sur LAZARE ne doit sortir du Secteur 9. Le client attend des résultats, pas des états d'âme. — Direction" },
            new Clue { chapter = 2, type = ClueType.Audio, style = 2, title = "Journal du Dr Valen, jour 1", speaker = "Dr Valen", text = "LAZARE devait réparer les tissus morts. Le sujet 0 a reçu la première injection ce matin. Ses plaies se referment à vue d'œil. C'est… extraordinaire." },
            new Clue { chapter = 2, type = ClueType.Rapport, style = 1, title = "Incident n°117", speaker = "Institut Noctis", text = "Deux techniciens mordus pendant un transfert. Fièvre, perte de conscience, réveil agressif après 58 minutes. Le Dr Valen demande l'arrêt du programme. Refusé." },
            new Clue { chapter = 3, type = ClueType.Audio, style = 2, title = "Journal du Dr Valen, jour 3", speaker = "Dr Valen", text = "Le cœur du sujet 0 s'est arrêté pendant six heures. Puis il s'est réveillé. Il ne parle plus, mais il me suit des yeux à travers la vitre." },
            new Clue { chapter = 3, type = ClueType.Dossier, style = 3, title = "Dossier LAZARE-0", speaker = "Institut Noctis", text = "Sujet 0. Homme, 34 ans, volontaire rémunéré. Température : 29 °C. Pouls : 4 battements par minute. Agressivité extrême. Décision : poursuivre l'observation." },
            new Clue { chapter = 3, type = ClueType.Note, style = 1, title = "Courriel du Dr Valen", speaker = "Dr Valen", text = "Si LAZARE sort de ce bâtiment, aucune quarantaine ne l'arrêtera. Je refuse de signer le rapport de sécurité. Je garde une copie de tout." },
            new Clue { chapter = 3, type = ClueType.Rapport, style = 1, title = "La nuit de la panne", speaker = "Institut Noctis", text = "Panne générale du Secteur 9 pendant 11 minutes. Porte du confinement B ouverte. Cellule du sujet 0 : vide. Deux gardes disparus. Ne pas prévenir les autorités." },
            new Clue { chapter = 4, type = ClueType.Radio, style = 0, title = "Canal privé", speaker = "Capitaine Delmas", text = "Oui, monsieur. Les échantillons de l'Institut sont à bord. Les civils passeront après. Non, toujours aucune trace du sujet 0." },
            new Clue { chapter = 4, type = ClueType.Temoignage, style = 1, title = "Le badge", speaker = "Nora", text = "J'ai retrouvé le badge de mon frère. Secteur 9, agent de sécurité. Il était de garde la nuit de la panne. Je crois savoir ce qu'il est devenu." },
            new Clue { chapter = 4, type = ClueType.Note, style = 1, title = "Contrat LAZARE, page 2", speaker = "Institut Noctis", text = "Objectif du client : une souche capable de relever un soldat tombé… ou de rendre une zone inhabitable. Les deux usages sont acceptés." },
            new Clue { chapter = 4, type = ClueType.Alerte, style = 1, title = "Dernier message", speaker = "Préfecture", text = "Valmonde est déclarée zone perdue. Les communications seront coupées à minuit. Bonne chance à tous." },
        };

        public static readonly Line[] Ending =
        {
            new Line("Dr Valen", "Si quelqu'un écoute ceci, c'est que je suis morte. Ou pire."),
            new Line("Dr Valen", "Le sujet 0 ne s'est pas échappé. Quelqu'un a ouvert sa porte."),
            new Line("Dr Valen", "Kessler savait. Le client savait. LAZARE n'était pas un accident : c'était un essai."),
            new Line("Dr Valen", "Le sujet 0 est toujours vivant. Il marche vers le nord. Il ne s'arrêtera pas."),
            new Line("Voix inconnue", "La ville est perdue. Le monde aussi. Mais tant que tu cours, il reste une chance. Si tu veux connaître la vérité, retourne là où tout a commencé."),
        };

        // ================= Sauvegarde (PlayerPrefs) =================
        const string KClues = "zr_story_clues", KIntro = "zr_story_intro", KSecret = "zr_story_secret", KAmbient = "zr_story_ambient", KCard = "zr_story_card";

        public static int Found => PlayerPrefs.GetInt(KClues, 0);
        public static bool IsFound(int i) => (Found & (1 << i)) != 0;
        public static int FoundCount { get { int n = 0, f = Found; while (f != 0) { n += f & 1; f >>= 1; } return n; } }
        public static bool AllFound => FoundCount >= Clues.Length;

        public static bool IntroSeen { get => PlayerPrefs.GetInt(KIntro, 0) == 1; set { PlayerPrefs.SetInt(KIntro, value ? 1 : 0); PlayerPrefs.Save(); } }
        /// <summary>0 = rien, 1 = enregistrement final ramassé (séquence à jouer), 2 = fin secrète vue.</summary>
        public static int Secret { get => PlayerPrefs.GetInt(KSecret, 0); set { PlayerPrefs.SetInt(KSecret, value); PlayerPrefs.Save(); } }
        /// <summary>Dernier chapitre dont le titre a été montré en début de course.</summary>
        public static int CardShown { get => PlayerPrefs.GetInt(KCard, -1); set { PlayerPrefs.SetInt(KCard, value); PlayerPrefs.Save(); } }

        public static void MarkFound(int i) { PlayerPrefs.SetInt(KClues, Found | (1 << i)); PlayerPrefs.Save(); }

        public static int ChapterFoundCount(int ch)
        {
            int n = 0;
            for (int k = 0; k < CluesPerChapter; k++) if (IsFound(ch * CluesPerChapter + k)) n++;
            return n;
        }

        public static bool ChapterComplete(int ch) => ChapterFoundCount(ch) >= CluesPerChapter;

        /// <summary>Un chapitre est ouvert quand tous les précédents sont terminés.</summary>
        public static bool ChapterUnlocked(int ch) => ch == 0 || ChapterComplete(ch - 1);

        /// <summary>Chapitre en cours : le premier qui n'est pas terminé (-1 si tout est trouvé).</summary>
        public static int CurrentChapter
        {
            get
            {
                for (int c = 0; c < Chapters.Length; c++) if (!ChapterComplete(c)) return c;
                return -1;
            }
        }

        /// <summary>Prochain indice à trouver, dans l'ordre (-1 si tout est trouvé).</summary>
        public static int NextClue
        {
            get
            {
                for (int i = 0; i < Clues.Length; i++) if (!IsFound(i)) return i;
                return -1;
            }
        }

        /// <summary>Transmission d'ambiance suivante d'un chapitre (tourne sans se répéter).</summary>
        public static Line NextAmbient(int ch)
        {
            int k = PlayerPrefs.GetInt(KAmbient + ch, 0);
            PlayerPrefs.SetInt(KAmbient + ch, k + 1);
            var a = Chapters[ch].ambient;
            return a[k % a.Length];
        }

        public static void ResetAll()
        {
            foreach (var k in new[] { KClues, KIntro, KSecret, KCard }) PlayerPrefs.DeleteKey(k);
            for (int c = 0; c < Chapters.Length; c++) PlayerPrefs.DeleteKey(KAmbient + c);
            PlayerPrefs.Save();
        }

        /// <summary>Outil de test : marque comme trouvés les n premiers indices.</summary>
        public static void UnlockFirst(int n)
        {
            int f = 0;
            for (int i = 0; i < Mathf.Min(n, Clues.Length); i++) f |= 1 << i;
            PlayerPrefs.SetInt(KClues, f);
            PlayerPrefs.SetInt(KIntro, 1);
            PlayerPrefs.Save();
        }
    }
}