using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// Fait vivre le scénario pendant les courses, sans toucher aux règles du jeu :
    /// radios d'ambiance, indices posés sur la route, chapitres débloqués, ambiance lumineuse du chapitre,
    /// enregistrement final de la fin secrète.
    /// </summary>
    public class ZRNarrative
    {
        readonly ZRWorld world;
        readonly ZRUI ui;
        readonly ZRSfx sfx;

        int runChapter;
        float nextClueAt, nextAmbientAt;
        int cluesThisRun, ambientThisRun;
        int activeClue = -1;           // indice posé sur la route (-1 aucun, -2 enregistrement final)
        bool finalTriedThisRun;
        static bool cardShownThisSession;


        /// <summary>Vrai si l'enregistrement final vient d'être ramassé pendant cette course.</summary>
        public bool SecretJustFound { get; private set; }

        public ZRNarrative(ZRWorld world, ZRUI ui, ZRSfx sfx, Camera cam)
        {
            this.world = world; this.ui = ui; this.sfx = sfx;
        }

        /// <summary>Début d'une course : chapitre, ambiance, titre du chapitre si besoin.</summary>
        public void BeginRun()
        {
            int current = ZRStory.CurrentChapter;
            runChapter = current >= 0 ? current : Random.Range(0, ZRStory.Chapters.Length);

            if (current >= 0 && (!cardShownThisSession || ZRStory.CardShown != current))
            {
                var c = ZRStory.Chapters[current];
                ui.ChapterCard("CHAPITRE " + (current + 1), c.name, c.goal);
                ZRStory.CardShown = current;
                cardShownThisSession = true;
            }

            nextClueAt = ZRStory.Chapters[runChapter].gate + Random.Range(0f, 250f);
            nextAmbientAt = 120f;
            cluesThisRun = 0;
            ambientThisRun = 0;
            activeClue = -1;
            finalTriedThisRun = false;
            SecretJustFound = false;
        }

        /// <summary>À appeler à chaque image de course.</summary>
        public void Tick(float distance, float playerZ, Vector3 playerCenter)
        {
#if UNITY_EDITOR
            // Test dans l'éditeur : touche K = faire apparaître le prochain indice tout de suite
            if (Input.GetKeyDown(KeyCode.K)) { nextClueAt = distance; cluesThisRun = 0; finalTriedThisRun = false; }
            bool forceFinal = Input.GetKey(KeyCode.K);
#else
            bool forceFinal = false;
#endif
            // Radios d'ambiance : à 120 m, puis à 900 m
            if (ambientThisRun < 2 && distance >= nextAmbientAt)
            {
                var line = ZRStory.NextAmbient(runChapter);
                ui.Radio(line.speaker, "", line.text);
                sfx.Static();
                ambientThisRun++;
                nextAmbientAt = 900f;
            }

            if (activeClue == -1)
            {
                int next = ZRStory.NextClue;
                if (next >= 0)
                {
                    if (cluesThisRun < 2 && distance >= nextClueAt && world.PlaceClue(playerZ + 70f, ZRStory.Clues[next].style))
                    {
                        activeClue = next;
                        sfx.Static();
                    }
                }
                else if (ZRStory.Secret == 0 && !finalTriedThisRun && (distance >= ZRStory.SecretDistance || forceFinal))
                {
                    if (world.PlaceClue(playerZ + 70f, 4)) { activeClue = -2; finalTriedThisRun = true; sfx.Static(); }
                }
                return;
            }

            if (world.CollectClue(playerCenter, 1.3f)) { Collected(distance); return; }
            if (world.ConsumeClueMissed() || !world.ClueActive)
            {
                // Raté : il reviendra à la course suivante
                activeClue = -1;
                cluesThisRun = 2;
            }
        }

        void Collected(float distance)
        {
            sfx.Static();
            sfx.Select();
            if (activeClue == -2)
            {
                ZRStory.Secret = 1;
                SecretJustFound = true;
                ui.Radio("ENREGISTREMENT FINAL", "Laboratoire Noctis · Secteur 9", "Enregistrement final récupéré. Survis jusqu'au bout pour l'écouter.");
                activeClue = -1;
                return;
            }

            int i = activeClue;
            var clue = ZRStory.Clues[i];
            ZRStory.MarkFound(i);
            ui.Radio(clue.speaker + "  ·  " + ZRStory.TypeNames[(int)clue.type], clue.title, clue.text);
            ui.ClueToast("INDICE " + ZRStory.FoundCount + " / " + ZRStory.Clues.Length);

            if (ZRStory.ChapterComplete(clue.chapter))
            {
                int nextCh = clue.chapter + 1;
                if (nextCh < ZRStory.Chapters.Length)
                    ui.Radio("JOURNAL", "CHAPITRE " + (nextCh + 1) + " DÉBLOQUÉ", ZRStory.Chapters[nextCh].name + " — " + ZRStory.Chapters[nextCh].goal);
                else
                    ui.Radio("JOURNAL", "TOUS LES INDICES SONT TROUVÉS", "Un dernier enregistrement attend quelque part après " + (int)ZRStory.SecretDistance + " m…");
            }
            cluesThisRun++;
            nextClueAt = distance + 700f;
            activeClue = -1;
        }

        public void ConsumeSecret() => SecretJustFound = false;

        /// <summary>L'ambiance lumineuse suit maintenant le quartier traversé (voir ZRWorld.UpdateAtmosphere).</summary>
        public void ResetPalette() { }

    }
}
