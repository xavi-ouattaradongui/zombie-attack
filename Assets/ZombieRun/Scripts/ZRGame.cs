using UnityEngine;

namespace ZombieRun
{
    public enum GameState { Splash, Menu, Intro, Playing, GameOver, Cinematic, Ending, Journal, SecondChance }

    /// <summary>
    /// Chef d'orchestre de Zombie Attack. Écrans : accueil → menu → départ (le perso regarde derrière lui,
    /// les zombies arrivent) → course → fin (RÉESSAYER / MENU). Règles (comme Subway Surfers) :
    /// - se cogner contre une voiture = attrapé ;
    /// - trébucher (petit obstacle, ou côté d'une voiture) = les zombies reviennent au contact ;
    /// - trébucher une 2e fois pendant qu'ils sont tout près = attrapé.
    /// </summary>
    public class ZRGame : MonoBehaviour
    {
        const string BestKey = "zr_best";
        const string CoinsKey = "zr_coins_total";
        const string CharKey = "zr_character_v3";
        /// <summary>Histoire (intro, indices, radios, journal, fin secrète) : désactivée. Mettre true pour la remettre.</summary>
        public static readonly bool StoryEnabled = false;
        const float IntroTurn = 2.1f, IntroLength = 2.9f;
        const float StartSpeed = 13f, MaxSpeed = 30f, DistanceToMax = 2500f;

        [Tooltip("Rempli automatiquement par le menu Zombie Run.")]
        public ZRLibrary library = new ZRLibrary();
        public ZRMaterials materials = new ZRMaterials();
        public Camera mainCamera;
        [Range(2, 8)] public int hordeSize = 5;

        ZRWorld world;
        ZRPlayer player;
        ZRHorde horde;
        ZRCameraRig rig;
        ZRUI ui;
        ZRSfx sfx;
        ZRNarrative story;

        // Super objets
        // Durées des super objets : améliorées dans la boutique (ZRUpgrades)
        readonly float[] powerMax = { 10f, 30f, 10f, 12f };
        float magnet, jumpBoost, doubleCoins, shield, shieldGrace, nextPowerAt;
        ZRBoss boss;
        int bossRound, chancesUsed;
        float bossDelay = -1f, reviveGrace, chanceTimer, resumeTimer = -1f;
        bool paused, chanceShown;
        const float ChanceTime = 5f;
        /// <summary>Prix de la seconde chance : 25, puis 50, 100, 200… dans la même partie.</summary>
        int ChanceCost => 25 << Mathf.Min(chancesUsed, 20);
        int Wallet => PlayerPrefs.GetInt(CoinsKey, 0) + coins;
        Transform shieldFx;
        bool cineReplay;
        int journalChapter;
        float[] endingTimes;

        GameState state;
        float distance, speed, stateTime, overDelay, invulnerable, slowdown, growlTimer;
        int coins, best, lastScore, currentZone, lastRoofLandings;
        bool lastRecord;

        int Score => Mathf.FloorToInt(distance) + coins * 10;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = false;
            if (!materials.IsComplete) materials.Fill();
            if (mainCamera == null) mainCamera = Camera.main;

            world = new GameObject("Ville").AddComponent<ZRWorld>();
            world.Init(library, materials);
            player = new GameObject("Joueur").AddComponent<ZRPlayer>();
            player.Build(library);
            player.SetCharacter(PlayerPrefs.GetInt(CharKey, 0));
            horde = new GameObject("Horde").AddComponent<ZRHorde>();
            horde.Build(library, hordeSize);
            boss = new GameObject("Boss de quartier").AddComponent<ZRBoss>();
            boss.Build(library);

            rig = mainCamera.GetComponent<ZRCameraRig>();
            if (rig == null) rig = mainCamera.gameObject.AddComponent<ZRCameraRig>();
            rig.Init(player.transform, mainCamera);

            ui = new GameObject("UI").AddComponent<ZRUI>();
            ui.SetArt(library.art);
            ui.Build(library.splash, library.logo, library.titleFont, library.bodyFont);
            ui.SetOverArt(library.overTitle, library.overPanel, library.overRetry);
            ui.SetMenuArt(library.playButton, library.arrowLeft, library.arrowRight);
            sfx = gameObject.AddComponent<ZRSfx>();
            story = new ZRNarrative(world, ui, sfx, mainCamera);
            best = PlayerPrefs.GetInt(BestKey, 0);
            EnterSplash();
            StartCoroutine(CapturePortraits());
        }

        /// <summary>Prend en photo le visage de chaque personnage pour les vignettes du menu.</summary>
        System.Collections.IEnumerator CapturePortraits()
        {
            yield return null; // une image : les animations sont en place
            int keep = player.Index;
            var rt = new RenderTexture(256, 256, 24);
            var go = new GameObject("Appareil photo portraits");
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.15f, 0.32f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 4f;
            cam.targetTexture = rt;
            var lightGo = new GameObject("Lumière portrait");
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.range = 4f; l.intensity = 3f; l.color = new Color(1f, 0.95f, 0.9f);
            var shots = new Texture[player.Count];
            for (int i = 0; i < player.Count; i++)
            {
                player.SetCharacter(i);
                float h = i < library.runners.Length ? library.runners[i].height : 1.5f;
                var p = player.transform.position;
                var head = p + Vector3.up * (h * 0.86f);
                go.transform.position = head + new Vector3(0.15f, 0.02f, 1.15f);
                go.transform.rotation = Quaternion.LookRotation(head + Vector3.down * 0.06f - go.transform.position);
                lightGo.transform.position = head + new Vector3(-0.4f, 0.4f, 0.9f);
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(256, 256, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                shots[i] = tex;
            }
            player.SetCharacter(keep);
            Destroy(go); Destroy(lightGo); rt.Release(); Destroy(rt);
            ui.SetPortraits(shots);
            if (state == GameState.Menu) ui.SetCharacter(player.CharacterName, player.CharacterDescription, player.Count, player.Index);
        }

        void ResetRun()
        {
            world.ResetWorld();
            player.ResetPlayer();
            distance = 0f; coins = 0; speed = StartSpeed; currentZone = 0;
            lastRoofLandings = player != null ? player.RoofLandings : 0;
            magnet = jumpBoost = doubleCoins = shield = shieldGrace = 0f;
            nextPowerAt = Random.Range(160f, 260f);
            if (player != null) player.JumpBoost = 1f;
            if (shieldFx != null) shieldFx.gameObject.SetActive(false);
            stateTime = 0f; overDelay = -1f; invulnerable = 0f; slowdown = 0f;
            growlTimer = 4f;
            if (boss != null) boss.Stop();
            bossRound = 0; bossDelay = -1f; chancesUsed = 0; reviveGrace = 0f;
            paused = false; resumeTimer = -1f; Time.timeScale = 1f;
        }

        void EnterSplash()
        {
            ResetRun();
            state = GameState.Splash;
            player.FacingCamera = true;
            horde.ShowMenu(player.transform.position);
            ui.ShowSplash();
            sfx.Music(library.menuMusic, 0.55f);
        }

        void EnterMenu()
        {
            ResetRun();
            state = GameState.Menu;
            player.FacingCamera = true;
            player.ForcedState = null;
            horde.ShowMenu(player.transform.position);
            story.ResetPalette();
            ui.ClearRadio();
            ui.ShowHelp(false);
            ui.ShowMenu(best, PlayerPrefs.GetInt(CoinsKey, 0), player.CharacterName, player.CharacterDescription, player.Count, player.Index);
            ui.ShowSettings(false); ui.ShowMissions(false);
            ui.SetMissionBadge(ZRMissions.Completed - PlayerPrefs.GetInt("zr_m_seen", 0));
            ui.FadeIn();
            sfx.Music(library.menuMusic, 0.55f);
        }

        /// <summary>JOUER : le perso regarde derrière lui, les zombies arrivent en courant, puis la course démarre.</summary>
        void StartIntro()
        {
            state = GameState.Intro;
            stateTime = 0f;
            ui.HideAll();
            player.ForcedState = "LookBack";
            horde.StartIntro(player.transform.position);
            sfx.Music(library.runMusic, 0.6f);
            sfx.Select();
            sfx.Growl();
        }

        void StartRun()
        {
            state = GameState.Playing;
            stateTime = 0f;
            player.FacingCamera = false;
            player.ForcedState = null;
            horde.StartChase(player.Z);
            ui.ShowHud();
            ui.ShowPauseButton(true);
            sfx.StartRun();
            sfx.Music(library.runMusic, 0.6f);
            if (StoryEnabled) story.BeginRun();
        }

        void ChangeCharacter(int step)
        {
            if (player.Count < 2) return;
            player.SetCharacter(player.Index + step);
            PlayerPrefs.SetInt(CharKey, player.Index);
            PlayerPrefs.Save();
            ui.SetCharacter(player.CharacterName, player.CharacterDescription, player.Count, player.Index);
            sfx.Select();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var input = ZRInput.Read();
            if (state == GameState.Playing && (paused || resumeTimer >= 0f)) { UpdatePause(input); return; }
            stateTime += dt;
            switch (state)
            {
                case GameState.Splash: UpdateSplash(input, dt); break;
                case GameState.Menu: UpdateMenu(input, dt); break;
                case GameState.Intro: UpdateIntro(dt); break;
                case GameState.Playing: UpdatePlaying(input, dt); break;
                case GameState.GameOver: UpdateOver(input, dt); break;
                case GameState.Cinematic: UpdateCinematic(input, dt); break;
                case GameState.Ending: UpdateEnding(input, dt); break;
                case GameState.Journal: UpdateJournal(input, dt); break;
                case GameState.SecondChance: UpdateChance(input, dt); break;
            }
            horde.Tick(player.transform.position, speed, dt);
            world.UpdateAtmosphere(player.Z, dt);
            bool front = state == GameState.Splash || state == GameState.Menu || state == GameState.Journal
                || (state == GameState.GameOver && stateTime > 0.9f)
                || (state == GameState.Intro && stateTime < IntroTurn) || (state == GameState.Cinematic && stateTime < CineTurn);
            rig.Tick(Mathf.InverseLerp(StartSpeed, MaxSpeed, speed), dt, front);
            ShowcaseLights(front && state != GameState.GameOver, dt);
        }

        Light keyLight, fillLight;

        /// <summary>Éclairage « vitrine » : quand la caméra est de face (menu), le perso est bien éclairé.</summary>
        void ShowcaseLights(bool on, float dt)
        {
            if (keyLight == null)
            {
                keyLight = new GameObject("Lumière menu (face)").AddComponent<Light>();
                keyLight.type = LightType.Point; keyLight.range = 6f; keyLight.color = new Color(1f, 0.93f, 0.85f);
                fillLight = new GameObject("Lumière menu (contre-jour)").AddComponent<Light>();
                fillLight.type = LightType.Point; fillLight.range = 5f; fillLight.color = new Color(0.55f, 1f, 0.45f);
            }
            var p = player.transform.position;
            keyLight.transform.position = p + new Vector3(0.6f, 1.9f, 2.2f);
            fillLight.transform.position = p + new Vector3(-1.2f, 1.6f, -0.8f);
            float k = Mathf.MoveTowards(keyLight.intensity / 4.5f, on ? 1f : 0f, dt * 2f);
            keyLight.intensity = 4.5f * k; fillLight.intensity = 2.5f * k;
            keyLight.enabled = fillLight.enabled = k > 0.001f;
        }

        static bool EnterKey => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

        void UpdateSplash(Swipe input, float dt)
        {
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            // Pas besoin de toucher : quand le chargement est fini, on arrive sur le menu
            if (stateTime >= ZRUI.SplashDuration) EnterMenu();
        }

        void UpdateMenu(Swipe input, float dt)
        {
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            if (stateTime < 0.3f) return;
            if (ui.PanelOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { ui.ShowSettings(false); ui.ShowMissions(false); ui.ShowShop(false); return; }
                if (input == Swipe.Tap && ui.ButtonAt(ZRInput.TapPosition) == UIButton.Panel) PanelButton(ui.TappedId);
                return;
            }
            if (ui.HelpOpen)
            {
                if (input == Swipe.Tap || EnterKey || Input.GetKeyDown(KeyCode.Escape)) { ui.ShowHelp(false); sfx.Select(); }
                return;
            }
            if (EnterKey) { ui.Press(UIButton.Play); Play(); return; }
            switch (input)
            {
                case Swipe.Left: ChangeCharacter(-1); break;
                case Swipe.Right: ChangeCharacter(1); break;
                case Swipe.Up: ui.Press(UIButton.Play); Play(); break;
                case Swipe.Tap:
                    switch (ui.ButtonAt(ZRInput.TapPosition))
                    {
                        case UIButton.Play: ui.Press(UIButton.Play); Play(); break;
                        case UIButton.Journal when StoryEnabled: ui.Press(UIButton.Journal); sfx.Select(); OpenJournal(Mathf.Max(0, ZRStory.CurrentChapter)); break;
                        case UIButton.Info: ui.Press(UIButton.Info); sfx.Select(); ui.ShowHelp(true); break;
                        case UIButton.Shop: ui.Press(UIButton.Shop); sfx.Select(); ui.ShowShop(true, Wallet); break;
                        case UIButton.Missions:
                            ui.Press(UIButton.Missions); sfx.Select();
                            PlayerPrefs.SetInt("zr_m_seen", ZRMissions.Completed); ui.SetMissionBadge(0);
                            ui.ShowMissions(true); break;
                        case UIButton.Settings: ui.Press(UIButton.Settings); sfx.Select(); ui.ShowSettings(true); break;
                        case UIButton.Card: if (ui.TappedCard < player.Count && ui.TappedCard != player.Index) ChangeCharacter(ui.TappedCard - player.Index); break;
                        case UIButton.Left: ChangeCharacter(-1); break;
                        case UIButton.Right: ChangeCharacter(1); break;
                    }
                    break;
            }
        }

        void UpdateIntro(float dt)
        {
            if (stateTime > 0.7f && stateTime - dt <= 0.7f) sfx.Scream();
            // Le perso se retourne vers la route et se met à courir sur place pendant que la caméra passe derrière lui
            if (stateTime > IntroTurn && player.ForcedState == "LookBack") { player.ForcedState = "Run"; player.FacingCamera = false; }
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            if (stateTime >= IntroLength) StartRun();
        }

        void UpdatePlaying(Swipe input, float dt)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || (input == Swipe.Tap && ui.ButtonAt(ZRInput.TapPosition) == UIButton.Pause))
            {
                ui.Press(UIButton.Pause); Pause(); return;
            }
            switch (input)
            {
                case Swipe.Left:
                    if (player.MoveLeft())
                    {
                        if (world.LaneBlocked(player.Lane, player.Z, 0.6f, player.Y)) SideScrape(); else sfx.Lane();
                    }
                    break;
                case Swipe.Right:
                    if (player.MoveRight())
                    {
                        if (world.LaneBlocked(player.Lane, player.Z, 0.6f, player.Y)) SideScrape(); else sfx.Lane();
                    }
                    break;
                case Swipe.Up: if (player.Jump()) { sfx.Jump(); Mission(ZRMissions.Stat.Jumps, 1); } break;
                case Swipe.Down: if (player.Slide()) { sfx.Slide(); Mission(ZRMissions.Stat.Slides, 1); } break;
            }

            float difficulty = Mathf.Clamp01(distance / DistanceToMax);
            float baseSpeed = Mathf.Lerp(StartSpeed, MaxSpeed, difficulty);
            slowdown = Mathf.MoveTowards(slowdown, 0f, dt * 0.5f);
            speed = baseSpeed * (1f - slowdown * 0.35f);
            if (invulnerable > 0f) invulnerable -= dt;

            float z0 = player.Z;
            player.GroundHeight = world.GroundAt(player.transform.position.x, player.Z);
            player.Tick(speed, dt, true);
            float dz = player.Z - z0;
            distance += dz;
            world.Tick(player.Z, difficulty, dt);

            var b = player.GetBounds();
            if (StoryEnabled) story.Tick(distance, player.Z, b.center);
            TickPowerUps(dt, b.center);
            int zone = ZRWorld.BiomeAt(player.Z);
            if (zone != currentZone)
            {
                currentZone = zone;
                ui.ChapterCard("NOUVELLE ZONE", ZRWorld.BiomeNames[zone], ZRWorld.BiomeLines[zone]);
                sfx.Growl();
                bossDelay = 2.5f; // le boss du quartier arrive juste après
            }
            UpdateBoss(dt);
            if (state != GameState.Playing) return;
            int got = world.CollectCoins(b.center, magnet > 0f ? 1.6f : 1.1f);
            if (got > 0) { int gain = got * (doubleCoins > 0f ? 2 : 1); coins += gain; sfx.Coin(); Mission(ZRMissions.Stat.Coins, gain); }
            Mission(ZRMissions.Stat.RunDistance, Mathf.FloorToInt(distance));
            if (player.RoofLandings != lastRoofLandings) { Mission(ZRMissions.Stat.RoofLands, player.RoofLandings - lastRoofLandings); lastRoofLandings = player.RoofLandings; }
            ui.SetScore(Score, coins, horde.Close && stateTime > 3f);

            growlTimer -= dt;
            if (growlTimer <= 0f) { sfx.Growl(); growlTimer = horde.Close ? 1.5f : Random.Range(5f, 9f); }

            var swept = new Bounds(b.center - new Vector3(0f, 0f, dz * 0.5f), b.size + new Vector3(0f, 0f, dz));
            if (reviveGrace > 0f) reviveGrace -= dt;
            if (shieldGrace > 0f || reviveGrace > 0f) { if (shieldGrace > 0f) shieldGrace -= dt; }
            else if (world.Hits(swept, out var kind) && !(kind == Kind.Slide && invulnerable > 0f))
            {
                if (UseShield()) return;
                // Tout ce qu'on touche fait attraper, sauf les obstacles à glisser (on trébuche seulement).
                if (kind != Kind.Slide) Caught();
                else Stumble();
            }
        }

        void SideScrape()
        {
            if (UseShield()) { player.BounceBack(); return; }
            player.BounceBack();
            Stumble();
        }

        void Stumble()
        {
            invulnerable = 0.8f;
            slowdown = 1f;
            sfx.Hit();
            rig.Shake(0.25f);
            ui.Flash();
            if (horde.Stumble()) Caught();
            else sfx.Scream();
        }

        void Caught()
        {
            if (state != GameState.Playing) return;
            player.Hit();
            sfx.Music(library.runMusic, 0.18f);
            horde.Catch(player.transform.position);
            rig.Shake(0.5f);
            sfx.Hit();
            sfx.Growl();
            ui.Flash();
            ui.ShowPauseButton(false);
            stateTime = 0f;
            // Seconde chance si on a de quoi payer (25, puis 50, 100, 200…)
            if (Wallet >= ChanceCost) { state = GameState.SecondChance; chanceTimer = ChanceTime; chanceShown = false; }
            else FinishRun(1.6f);
        }

        /// <summary>La partie est vraiment finie : missions, puis écran GAME OVER.</summary>
        void FinishRun(float delay)
        {
            state = GameState.GameOver;
            stateTime = 0f;
            overDelay = delay;
            boss.Stop();
            world.SpawnPaused = false;
            Mission(ZRMissions.Stat.Runs, 1);
            Mission(ZRMissions.Stat.RunScore, Score);
            ZRMissions.Save();
        }

        void UpdateChance(Swipe input, float dt)
        {
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            if (stateTime < 0.8f) return;
            if (!chanceShown) { chanceShown = true; ui.ShowChance(true, ChanceCost, Wallet); }
            chanceTimer -= dt;
            ui.SetChanceTimer(chanceTimer / ChanceTime);
            var b = input == Swipe.Tap ? ui.ButtonAt(ZRInput.TapPosition) : UIButton.None;
            if (b == UIButton.ChanceYes || EnterKey || input == Swipe.Up) { ui.Press(UIButton.ChanceYes); Revive(); return; }
            if (b == UIButton.ChanceNo || chanceTimer <= 0f || Input.GetKeyDown(KeyCode.Escape) || input == Swipe.Down)
            {
                if (b == UIButton.ChanceNo) ui.Press(UIButton.ChanceNo);
                sfx.Select();
                ui.ShowChance(false);
                FinishRun(0.5f);
            }
        }

        /// <summary>Seconde chance payée : le perso se relève, la route devant est dégagée, 2,5 s d'invincibilité.</summary>
        void Revive()
        {
            int cost = ChanceCost;
            int bank = PlayerPrefs.GetInt(CoinsKey, 0);
            int fromBank = Mathf.Min(bank, cost);
            PlayerPrefs.SetInt(CoinsKey, bank - fromBank);
            PlayerPrefs.Save();
            coins = Mathf.Max(0, coins - (cost - fromBank));
            chancesUsed++;
            player.Revive();
            horde.Release();
            world.ClearAhead(player.Z - 3f, player.Z + 45f);
            boss.ClearWaves();
            reviveGrace = 2.5f;
            state = GameState.Playing;
            stateTime = 3.5f;
            ui.ShowChance(false);
            ui.ShowHud();
            ui.ShowPauseButton(true);
            ui.PowerToast("C'EST REPARTI !", new Color(0.62f, 1f, 0.12f));
            sfx.Music(library.runMusic, 0.6f);
            sfx.PowerUp();
        }

        // ================= Boss de quartier =================

        void UpdateBoss(float dt)
        {
            if (bossDelay > 0f)
            {
                bossDelay -= dt;
                if (bossDelay <= 0f)
                {
                    world.ClearAhead(player.Z + 4f, player.Z + 500f);
                    world.SpawnPaused = true;
                    boss.Begin(player.Z, bossRound++);
                    ui.PowerToast("LE COLOSSE ARRIVE !", new Color(1f, 0.25f, 0.2f));
                    sfx.Siren(); sfx.Scream();
                    rig.Shake(0.35f);
                }
            }
            if (!boss.Active && !boss.JustDefeated) return;
            bool hit = boss.Tick(player.transform.position, player.Y, dt, reviveGrace <= 0f && shieldGrace <= 0f);
            if (boss.JustDefeated)
            {
                world.SpawnPaused = false;
                coins += 50;
                ui.PowerToast("COLOSSE ESQUIVÉ ! +50", new Color(1f, 0.82f, 0.25f));
                sfx.PowerUp();
                return;
            }
            if (hit && !UseShield()) Caught();
        }

        // ================= Pause =================

        void Pause()
        {
            if (paused) return;
            paused = true;
            resumeTimer = -1f;
            Time.timeScale = 0f;
            sfx.Select();
            ui.ShowPause(true);
        }

        void UpdatePause(Swipe input)
        {
            if (resumeTimer >= 0f)
            {
                // Compte à rebours 3, 2, 1 avant de reprendre
                resumeTimer -= Time.unscaledDeltaTime;
                ui.SetCountdown(Mathf.CeilToInt(resumeTimer), 1f - Mathf.Repeat(resumeTimer, 1f));
                if (resumeTimer <= 0f) { resumeTimer = -1f; ui.SetCountdown(0, 0f); Time.timeScale = 1f; }
                return;
            }
            var b = input == Swipe.Tap ? ui.ButtonAt(ZRInput.TapPosition) : UIButton.None;
            if (b == UIButton.Resume || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || EnterKey)
            {
                ui.Press(UIButton.Resume); sfx.Select();
                paused = false; ui.ShowPause(false);
                resumeTimer = 3f;
            }
            else if (b == UIButton.Home)
            {
                ui.Press(UIButton.Home); sfx.Select();
                // On quitte la partie : les pièces ramassées sont gardées
                PlayerPrefs.SetInt(CoinsKey, PlayerPrefs.GetInt(CoinsKey, 0) + coins);
                PlayerPrefs.Save();
                paused = false; Time.timeScale = 1f;
                EnterMenu();
            }
        }

        // Téléphone verrouillé, appel, autre appli : pause automatique
        void OnApplicationPause(bool p) { if (p && state == GameState.Playing && !paused && player != null && player.Alive) Pause(); }
        void OnApplicationFocus(bool f) { if (!f && state == GameState.Playing && !paused && player != null && player.Alive) Pause(); }

        void UpdateOver(Swipe input, float dt)
        {
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            if (overDelay > 0f)
            {
                overDelay -= dt;
                if (overDelay <= 0f)
                {
                    int s = Score;
                    bool record = s > best;
                    if (record) { best = s; PlayerPrefs.SetInt(BestKey, best); }
                    PlayerPrefs.SetInt(CoinsKey, PlayerPrefs.GetInt(CoinsKey, 0) + coins);
                    PlayerPrefs.Save();
                    lastScore = s; lastRecord = record;
                    if (StoryEnabled && ZRStory.Secret == 1) StartEnding(false);   // l'enregistrement final a été ramassé : fin secrète
                    else ui.ShowOver(s, coins, best, record);
                }
                return;
            }
            if (stateTime < 2.2f) return;
            if (ui.PanelOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { ui.ShowMissions(false); ui.ShowShop(false); return; }
                if (input == Swipe.Tap && ui.ButtonAt(ZRInput.TapPosition) == UIButton.Panel) PanelButton(ui.TappedId);
                return;
            }
            var b = input == Swipe.Tap ? ui.ButtonAt(ZRInput.TapPosition) : UIButton.None;
            if (b == UIButton.Shop)
            {
                ui.Press(UIButton.Shop); sfx.Select();
                ui.ShowShop(true, PlayerPrefs.GetInt(CoinsKey, 0));
                return;
            }
            if (b == UIButton.Menu || input == Swipe.Down || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.M))
            {
                ui.Press(UIButton.Menu); sfx.Select(); EnterMenu();
            }
            else if (b == UIButton.Retry || input == Swipe.Up || EnterKey)
            {
                ui.Press(UIButton.Retry); ResetRun(); rig.SnapBehind(); StartRun();
            }
        }
    
        // ================= Missions et Paramètres =================

        /// <summary>Fait avancer les missions ; une mission réussie donne ses pièces tout de suite.</summary>
        void Mission(ZRMissions.Stat stat, int value)
        {
            int reward = ZRMissions.Report(stat, value, out var done);
            if (reward <= 0) return;
            PlayerPrefs.SetInt(CoinsKey, PlayerPrefs.GetInt(CoinsKey, 0) + reward);
            PlayerPrefs.Save();
            ui.PowerToast("MISSION RÉUSSIE ! +" + reward, new Color(1f, 0.82f, 0.25f));
            sfx.PowerUp();
        }

        void PanelButton(string id)
        {
            ui.PressHot(id);
            sfx.Select();
            switch (id)
            {
                case "close": ui.ShowSettings(false); ui.ShowMissions(false); ui.ShowShop(false); return;
                case "help": ui.ShowSettings(false); ui.ShowHelp(true); return;
                case "buy0": case "buy1": case "buy2": case "buy3":
                    int pw = id[3] - '0';
                    if (ZRUpgrades.Buy(pw, CoinsKey)) { sfx.PowerUp(); ui.PowerToast(ZRWorld.PowerNames[pw] + " AMÉLIORÉ !", ZRWorld.PowerColors[pw]); }
                    else sfx.Hit();
                    int wallet = PlayerPrefs.GetInt(CoinsKey, 0);
                    ui.ShowShop(true, wallet); ui.SetWallet(wallet);
                    return;
                case "music-": ZRSfx.MusicVolume -= 0.1f; break;
                case "music+": ZRSfx.MusicVolume += 0.1f; break;
                case "sfx-": ZRSfx.SfxVolume -= 0.1f; break;
                case "sfx+": ZRSfx.SfxVolume += 0.1f; break;
                case "sensi-": ZRInput.Sensitivity -= 1; break;
                case "sensi+": ZRInput.Sensitivity += 1; break;
            }
            sfx.ApplyVolumes();
            ui.ShowSettings(true); // met à jour les valeurs affichées
        }

        // ================= Super objets =================

        void TickPowerUps(float dt, Vector3 center)
        {
            // Apparition : un super objet tous les 280 à 420 m
            if (!world.PowerActive && distance >= nextPowerAt)
            {
                float r = Random.value;
                int type = r < 0.3f ? 0 : r < 0.55f ? 1 : r < 0.75f ? 2 : 3;
                if (world.PlacePowerUp(player.Z + 65f, type)) nextPowerAt = distance + Random.Range(280f, 420f);
            }
            int got = world.CollectPowerUp(center, 1.3f);
            if (got >= 0)
            {
                powerMax[got] = ZRUpgrades.Duration(got);
                switch ((ZRWorld.Power)got)
                {
                    case ZRWorld.Power.Magnet: magnet = powerMax[got]; break;
                    case ZRWorld.Power.Shield: shield = powerMax[got]; break;
                    case ZRWorld.Power.Jump: jumpBoost = powerMax[got]; break;
                    default: doubleCoins = powerMax[got]; break;
                }
                ui.PowerToast(ZRWorld.PowerNames[got], ZRWorld.PowerColors[got]);
                sfx.PowerUp();
                Mission(ZRMissions.Stat.Powers, 1);
            }
            magnet = Mathf.Max(0f, magnet - dt);
            jumpBoost = Mathf.Max(0f, jumpBoost - dt);
            doubleCoins = Mathf.Max(0f, doubleCoins - dt);
            shield = Mathf.Max(0f, shield - dt);
            player.JumpBoost = jumpBoost > 0f ? 1.3f : 1f;
            if (magnet > 0f) world.AttractCoins(center, 12f, dt);
            ShieldVisual(dt);
            ui.SetPowers(magnet / powerMax[0], shield / powerMax[1], jumpBoost / powerMax[2], doubleCoins / powerMax[3]);
        }

        /// <summary>Le bouclier encaisse un choc : on traverse l'obstacle sans être attrapé.</summary>
        bool UseShield()
        {
            if (shield <= 0f) return false;
            shield = 0f;
            shieldGrace = 1.2f;
            sfx.ShieldBreak();
            Mission(ZRMissions.Stat.Shields, 1);
            rig.Shake(0.2f);
            ui.PowerToast("BOUCLIER BRISÉ !", ZRWorld.PowerColors[1]);
            return true;
        }

        void ShieldVisual(float dt)
        {
            bool on = shield > 0f || shieldGrace > 0f;
            if (shieldFx == null)
            {
                if (!on) return;
                shieldFx = new GameObject("Bouclier").transform;
                shieldFx.SetParent(player.transform, false);
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(sh != null ? sh : Shader.Find("Standard"));
                mat.SetColor("_BaseColor", ZRWorld.PowerColors[1] * 0.5f);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", ZRWorld.PowerColors[1] * 4f);
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    ZRBuild.Box("Éclat", shieldFx, mat, new Vector3(Mathf.Cos(a) * 0.85f, 0.4f + i * 0.35f, Mathf.Sin(a) * 0.85f), new Vector3(0.14f, 0.14f, 0.14f), new Vector3(45f, 45f, 0f));
                }
                var l = new GameObject("Lueur").AddComponent<Light>();
                l.transform.SetParent(shieldFx, false); l.transform.localPosition = Vector3.up * 1.2f;
                l.type = LightType.Point; l.range = 5f; l.intensity = 2.5f; l.color = ZRWorld.PowerColors[1];
            }
            shieldFx.gameObject.SetActive(on);
            if (on) shieldFx.localRotation = Quaternion.Euler(0f, Time.time * 220f, 0f);
        }

        // ================= Histoire : intro, fin secrète, journal =================

        const float CineTurn = 22.5f, CineEnd = 24f;

        /// <summary>JOUER : la première fois, l'intro de l'histoire ; ensuite, le départ court habituel.</summary>
        void Play()
        {
            if (StoryEnabled && !ZRStory.IntroSeen) StartCinematic(false);
            else StartIntro();
        }

        void StartCinematic(bool replay)
        {
            ResetRun();
            cineReplay = replay;
            state = GameState.Cinematic;
            stateTime = 0f;
            player.ForcedState = null;
            horde.ShowMenu(player.transform.position);
            story.ResetPalette();
            ui.CinemaShow(true);
            ui.CinemaBlack(1f, 0f);
            ui.CinemaCenter("VALMONDE\nTROIS JOURS APRÈS L'ALERTE", 96, true);
            rig.cinematic = true;
            rig.cinePos = new Vector3(2f, 18f, 70f);
            rig.cineLook = new Vector3(0f, 1f, 0f);
            rig.transform.position = rig.cinePos;
            sfx.Music(library.menuMusic, 0.35f);
            sfx.Siren();
        }

        static bool Crossed(float t, float dt, float at) => t >= at && t - dt < at;

        void UpdateCinematic(Swipe input, float dt)
        {
            float t = stateTime;
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);

            if ((input == Swipe.Tap && ui.ButtonAt(ZRInput.TapPosition) == UIButton.Skip) || Input.GetKeyDown(KeyCode.Escape))
            {
                sfx.Select();
                rig.cinematic = false;
                ui.CinemaShow(false);
                if (cineReplay) { OpenJournal(journalChapter); return; }
                ZRStory.IntroSeen = true;
                ResetRun();
                StartIntro();
                return;
            }

            if (Crossed(t, dt, 1.6f)) sfx.ScreamFar();
            if (Crossed(t, dt, 4.5f))
            {
                ui.CinemaCenter("");
                ui.CinemaBlack(0f, 0.9f);
                ui.CinemaSubtitle("PRÉFECTURE : « …la situation est hors de contrôle. Restez chez vous. Je répète… »");
                sfx.Static();
            }
            if (t >= 4.5f && t < 11f)
            {
                // Plan de drone : la caméra survole la rue vide puis descend vers le survivant
                float k = Mathf.SmoothStep(0f, 1f, (t - 4.5f) / 6.5f);
                rig.cinePos = Vector3.Lerp(new Vector3(2f, 18f, 70f), new Vector3(1.5f, 6f, 15f), k);
                rig.cineLook = Vector3.Lerp(new Vector3(0f, 0f, 10f), new Vector3(0f, 1.2f, 0f), k);
            }
            if (Crossed(t, dt, 8f)) sfx.ScreamFar();
            if (Crossed(t, dt, 11f))
            {
                ui.CinemaSubtitle("");
                ui.CinemaBlack(1f, 3f);
                ui.CinemaCenter("Tu te réveilles. Le bâtiment est vide.\nDehors, plus un bruit.", 52);
            }
            if (Crossed(t, dt, 14f))
            {
                ui.CinemaCenter("");
                ui.CinemaBlack(0f, 1.2f);
                rig.cinematic = false;               // vue de face du survivant
                horde.StartIntro(player.transform.position, 30f, false);
            }
            if (Crossed(t, dt, 18f))
            {
                ui.CinemaCenter("CLANG !", 170, true);
                sfx.Music(library.runMusic, 0.6f);
                sfx.Hit();
                rig.Shake(0.3f);
                player.ForcedState = "LookBack";
                horde.Wake();
                sfx.Scream();
                sfx.Growl();
            }
            if (Crossed(t, dt, 19.3f)) ui.CinemaCenter("");
            if (Crossed(t, dt, 21f))
            {
                ui.CinemaSubtitle("NORA : « Toi, en bas ! Ne reste pas planté là… COURS ! »");
                sfx.Static();
            }
            if (Crossed(t, dt, CineTurn)) { player.ForcedState = "Run"; player.FacingCamera = false; }
            if (t >= CineEnd)
            {
                ui.CinemaShow(false);
                if (cineReplay) { OpenJournal(journalChapter); return; }
                ZRStory.IntroSeen = true;
                StartRun();
            }
        }

        void StartEnding(bool replay)
        {
            cineReplay = replay;
            state = GameState.Ending;
            stateTime = 0f;
            sfx.Music(null, 0f);
            ui.CinemaShow(true);
            ui.CinemaBlack(1f, replay ? 0f : 0.8f);
            var lines = ZRStory.Ending;
            endingTimes = new float[lines.Length + 2];
            float t = 2f;
            for (int i = 0; i < lines.Length; i++)
            {
                endingTimes[i] = t;
                t += 1.5f + (lines[i].speaker.Length + lines[i].text.Length) / 38f + 1.8f;
            }
            endingTimes[lines.Length] = t;          // logo + TO BE CONTINUED
            endingTimes[lines.Length + 1] = t + 5f; // fin
        }

        void UpdateEnding(Swipe input, float dt)
        {
            float t = stateTime;
            var lines = ZRStory.Ending;
            bool skip = (input == Swipe.Tap && ui.ButtonAt(ZRInput.TapPosition) == UIButton.Skip) || Input.GetKeyDown(KeyCode.Escape);
            for (int i = 0; i < lines.Length; i++)
                if (Crossed(t, dt, endingTimes[i]))
                {
                    ui.CinemaTyped(lines[i].speaker.ToUpperInvariant() + "\n\n« " + lines[i].text + " »", 44);
                    sfx.Static();
                }
            if (Crossed(t, dt, endingTimes[lines.Length])) { ui.CinemaEndCard(true); sfx.Growl(); }
            if (skip || t >= endingTimes[lines.Length + 1])
            {
                ui.CinemaEndCard(false);
                ui.CinemaShow(false);
                if (cineReplay) { OpenJournal(journalChapter); return; }
                ZRStory.Secret = 2;
                state = GameState.GameOver;
                stateTime = 1.2f;
                ui.ShowOver(lastScore, coins, best, lastRecord);
            }
        }

        void OpenJournal(int chapter)
        {
            state = GameState.Journal;
            stateTime = 0f;
            journalChapter = Mathf.Clamp(chapter, 0, ZRStory.Chapters.Length - 1);
            rig.cinematic = false;
            ResetRun();
            player.ForcedState = null;
            horde.ShowMenu(player.transform.position);
            story.ResetPalette();
            ui.ShowJournal(journalChapter);
        }

        void UpdateJournal(Swipe input, float dt)
        {
            player.Tick(0f, dt, false);
            world.Tick(player.Z, 0f, dt);
            if (stateTime < 0.2f) return;
            var b = input == Swipe.Tap ? ui.ButtonAt(ZRInput.TapPosition) : UIButton.None;
            if (b == UIButton.Back || input == Swipe.Down || Input.GetKeyDown(KeyCode.Escape)) { ui.Press(UIButton.Back); sfx.Select(); EnterMenu(); return; }
            int step = (b == UIButton.PrevChapter || input == Swipe.Right) ? -1 : (b == UIButton.NextChapter || input == Swipe.Left) ? 1 : 0;
            if (step != 0)
            {
                int c = Mathf.Clamp(journalChapter + step, 0, ZRStory.Chapters.Length - 1);
                if (c != journalChapter) { journalChapter = c; sfx.Select(); ui.ShowJournal(c); }
                return;
            }
            if (b == UIButton.ReplayIntro) { sfx.Select(); StartCinematic(true); return; }
            if (b == UIButton.ReplayEnding) { sfx.Select(); StartEnding(true); }
        }
}
}
