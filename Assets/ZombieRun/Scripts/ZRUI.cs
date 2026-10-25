using UnityEngine;
using UnityEngine.UI;

namespace ZombieRun
{
    public enum UIButton { None, Play, Retry, Menu, Left, Right, Info, CloseInfo, Card, Missions, Settings, Panel, Journal, Skip, Back, PrevChapter, NextChapter, ReplayIntro, ReplayEnding, Shop, Pause, Resume, Home, ChanceYes, ChanceNo }

    /// <summary>
    /// Interface de Zombie Attack :
    /// écran d'accueil (fond + logo), menu (choix du perso + bouton JOUER), score en course,
    /// écran de fin (RÉESSAYER / MENU). Les boutons sont testés à la main (pas besoin d'EventSystem).
    /// </summary>
    public class ZRUI : MonoBehaviour
    {
        Font font, titleFont, symbolFont;   // texte courant (Lilita One), titres (Bangers), symboles ◄ ► (police intégrée)
        RectTransform root;
        GameObject splash, menu, hud, over, arrowL, arrowR;
        RectTransform splashLogo, menuLogo, playBtn, retryBtn, menuBtn, arrowLRect, arrowRRect, journalBtn, infoBtn, cardsRow;
        Text coinTotal, missionBadgeText;
        RectTransform overTitle, overMissionsBtn, overBox;
        RectTransform missionsBtn, settingsBtn, missionBadge;
        Text storyInfo;
        Image redFlash, fade;
        Text best, charName, charDesc, splashTap, score, coinsText, warning, overScore, overRecord, overInfo;
        int lastScore = -1, lastCoins = -1;
        float flash, fadeAlpha, splashTime, menuTime, overTime;
        Sprite rounded;
        Image loadFill;

        /// <summary>Durée de l'écran d'accueil avant d'arriver sur le menu.</summary>
        public const float SplashDuration = 2.8f;

        static readonly Color Blood = new Color(0.85f, 0.08f, 0.06f);
        static readonly Color Toxic = new Color(0.55f, 1f, 0.25f);
        static readonly Color Pale = new Color(0.9f, 0.92f, 0.85f);
        static readonly Color Ink = new Color(0f, 0f, 0f);
        static readonly Color Purple = new Color(0.22f, 0.05f, 0.3f);
        static readonly Color ButtonGreen = new Color(0.45f, 0.9f, 0.12f);
        static readonly Color ButtonPurple = new Color(0.45f, 0.18f, 0.62f);

        public void Build(Sprite splashSprite, Sprite logoSprite, Font title = null, Font body = null)
        {
            symbolFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = body != null ? body : symbolFont;
            titleFont = title != null ? title : font;
            rounded = RoundedSprite();
            var cg = new GameObject("Canvas", typeof(RectTransform));
            cg.transform.SetParent(transform, false);
            var canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cg.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cg.transform;

            var top = new Vector2(0.5f, 1f);
            var bottom = new Vector2(0.5f, 0f);
            var center = new Vector2(0.5f, 0.5f);

            // Flash rouge quand on trébuche
            redFlash = FullImage("Flash", root, new Color(0.6f, 0f, 0f, 0f));

            // ---------- Écran d'accueil ----------
            splash = Panel("Accueil", new Color(0f, 0f, 0f, 1f));
            if (splashSprite != null)
            {
                var bg = FullImage("Fond", splash.transform, Color.white);
                bg.sprite = splashSprite;
                var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = splashSprite.rect.width / splashSprite.rect.height;
            }
            FullImage("Assombrir", splash.transform, new Color(0f, 0f, 0f, 0.25f));
            splashLogo = Logo(splash.transform, logoSprite, center, new Vector2(0f, 120f), 980f);
            if (logoSprite == null) splashLogo = T(Label(splash.transform, "ZOMBIE ATTACK", 170, Toxic, center, new Vector2(0f, 120f), new Vector2(1100f, 200f), Purple), -3f).rectTransform;
            // Barre de chargement (puis on arrive tout seul sur le menu)
            var barBack = BarImage(splash.transform, "Barre", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, 220f), new Vector2(700f, 44f));
            var barBorder = barBack.gameObject.AddComponent<Outline>();
            barBorder.effectColor = Purple; barBorder.effectDistance = new Vector2(5f, -5f);
            loadFill = BarImage(barBack.transform, "Remplissage", ButtonGreen, Vector2.zero, Vector2.zero);
            var frt = loadFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f); frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = new Vector2(6f, 6f); frt.offsetMax = new Vector2(6f, -6f);
            splashTap = Label(splash.transform, "CHARGEMENT...", 44, Color.white, bottom, new Vector2(0f, 290f), new Vector2(1000f, 70f), Ink);

            // ---------- Menu (barre du haut, perso au centre, vignettes, JOUER, aide) ----------
            menu = Panel("Menu", null);
            menuLogo = null;
            // Barre du haut : pièces gagnées (à gauche), record (à droite)
            var coinPill = Pill(menu.transform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(360f, 104f));
            PillArt(coinPill);
            if (A("Coin") != null) { coinPill.anchoredPosition = new Vector2(70f, -40f); CoinIcon(coinPill, new Vector2(-340f, -52f), 104f); }
            else CoinIcon(coinPill, new Vector2(-300f, -52f), 78f);
            coinTotal = InPill(T(Label(coinPill, "0", 62, Color.white, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Ink, A("Coin") != null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft)), A("Coin") != null ? 70f : 118f, 20f);
            var bestPill = Pill(menu.transform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(360f, 104f));
            PillArt(bestPill);
            InPill(T(Label(bestPill, "RECORD", 38, Toxic, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Ink, TextAnchor.MiddleLeft)), 28f, 28f);
            best = InPill(T(Label(bestPill, "0", 62, new Color(1f, 0.82f, 0.25f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Ink, TextAnchor.MiddleRight)), 28f, 28f);
            // Flèches vertes de part et d'autre du perso
            arrowL = Chevron(menu.transform, true, new Vector2(-400f, 230f)).gameObject;
            arrowR = Chevron(menu.transform, false, new Vector2(400f, 230f)).gameObject;
            arrowLRect = (RectTransform)arrowL.transform;
            arrowRRect = (RectTransform)arrowR.transform;
            charName = T(Label(menu.transform, "", 92, Color.white, bottom, new Vector2(0f, 940f), new Vector2(900f, 110f), Purple), -2f);
            charDesc = Label(menu.transform, "", 30, Pale, bottom, new Vector2(0f, 885f), new Vector2(1000f, 44f), Ink);
            cardsRow = new GameObject("Vignettes", typeof(RectTransform)).GetComponent<RectTransform>();
            cardsRow.SetParent(menu.transform, false);
            cardsRow.anchorMin = cardsRow.anchorMax = bottom; cardsRow.anchoredPosition = new Vector2(0f, 735f); cardsRow.sizeDelta = new Vector2(1000f, 220f);
            playBtn = Button(menu.transform, "JOUER", ButtonGreen, bottom, new Vector2(0f, 470f), new Vector2(640f, 190f), 118);
            missionsBtn = Tile(menu.transform, "!", "MISSIONS", bottom, new Vector2(-345f, 190f), new Vector2(320f, 220f));
            infoBtn = Tile(menu.transform, "?", "BOUTIQUE", bottom, new Vector2(0f, 190f), new Vector2(320f, 220f));
            settingsBtn = Tile(menu.transform, "gear", "PARAMÈTRES", bottom, new Vector2(345f, 190f), new Vector2(320f, 220f));
            missionBadge = Child(missionsBtn, "Pastille", Blood, Vector2.zero, Vector2.zero).rectTransform;
            missionBadge.anchorMin = missionBadge.anchorMax = new Vector2(1f, 1f); missionBadge.sizeDelta = new Vector2(56f, 56f); missionBadge.anchoredPosition = new Vector2(-14f, -14f);
            missionBadgeText = T(Label(missionBadge, "3", 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(56f, 56f), Ink));
            journalBtn = Button(menu.transform, "JOURNAL", ButtonPurple, bottom, new Vector2(0f, 60f), new Vector2(440f, 100f), 56);
            storyInfo = Label(menu.transform, "", 34, Toxic, top, new Vector2(0f, -170f), new Vector2(1000f, 50f), Ink);
            BuildHelp();
            BuildSettings();
            BuildMissions();
            BuildShop();

            // ---------- En course ----------
            hud = Panel("HUD", null);
            score = T(Label(hud.transform, "0", 110, Color.white, new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(700f, 130f), Purple, TextAnchor.UpperLeft));
            coinsText = T(Label(hud.transform, "0", 80, new Color(1f, 0.82f, 0.25f), new Vector2(1f, 1f), new Vector2(-140f, -60f), new Vector2(600f, 110f), Purple, TextAnchor.UpperRight));
            CoinIcon(hud.transform, new Vector2(-70f, -108f), 64f);
            warning = T(Label(hud.transform, "LES ZOMBIES SONT LÀ !", 76, Blood, top, new Vector2(0f, -220f), new Vector2(1100f, 100f), Ink));
            BuildPause(center, bottom);

            // ---------- Fin de partie (GAME OVER) ----------
            over = Panel("Fin", new Color(0.02f, 0f, 0.04f, 0.28f));
            overTitle = SlimeTitle(over.transform, new Vector2(0f, -110f));
            // Encadré « TU AS PÉRI » + score
            var box = overBox = Pill(over.transform, new Vector2(0.5f, 0f), new Vector2(0f, 680f), new Vector2(760f, 230f));
            box.pivot = new Vector2(0.5f, 0f); box.anchoredPosition = new Vector2(0f, 660f);
            Skull(box, new Vector2(120f, 115f));
            T(Label(box, "TU AS PÉRI", 58, Color.white, new Vector2(0f, 1f), new Vector2(240f, -26f), new Vector2(500f, 70f), Ink, TextAnchor.UpperLeft));
            Label(box, "SCORE", 34, new Color(0.65f, 0.65f, 0.7f), new Vector2(0f, 1f), new Vector2(242f, -98f), new Vector2(300f, 44f), Ink, TextAnchor.UpperLeft);
            overScore = T(Label(box, "", 92, Toxic, new Vector2(0f, 1f), new Vector2(240f, -132f), new Vector2(500f, 100f), Ink, TextAnchor.UpperLeft));
            overRecord = T(Label(box, "NOUVEAU RECORD !", 46, new Color(1f, 0.82f, 0.25f), new Vector2(1f, 1f), new Vector2(40f, 40f), new Vector2(460f, 60f), Purple), 6f);
            ((RectTransform)overRecord.transform).pivot = new Vector2(1f, 0.5f);
            overInfo = Label(over.transform, "", 40, Pale, bottom, new Vector2(0f, 615f), new Vector2(1000f, 56f), Ink);
            retryBtn = Button(over.transform, "REJOUER", ButtonGreen, bottom, new Vector2(0f, 440f), new Vector2(700f, 190f), 110);
            Drips(retryBtn, new[] { -260f, -120f, 150f, 270f }, ButtonGreen, -95f);
            menuBtn = DarkButton(over.transform, "ACCUEIL", new Vector2(-235f, 200f));
            overMissionsBtn = DarkButton(over.transform, "BOUTIQUE", new Vector2(235f, 200f));

            BuildStory(top, bottom, center, logoSprite);

            // Fondu au noir (toujours au-dessus)
            fade = FullImage("Fondu", root, new Color(0f, 0f, 0f, 0f));
        }

        // ================= Écrans =================

        public void ShowSplash()
        {
            SetPanels(splash);
            splashTime = 0f;
        }

        public void ShowMenu(int bestScore, int totalCoins, string name, string desc, int count, int index)
        {
            SetPanels(menu);
            menuTime = 0f;
            best.text = bestScore.ToString();
            coinTotal.text = totalCoins.ToString();
            int found = ZRGame.StoryEnabled ? ZRStory.FoundCount : 0;
            storyInfo.text = found > 0 ? "INDICES : " + found + " / " + ZRStory.Clues.Length : "";
            journalBtn.gameObject.SetActive(ZRGame.StoryEnabled);
            SetCharacter(name, desc, count, index);
        }

        public void SetCharacter(string name, string desc, int count, int index)
        {
            charName.text = name;
            charDesc.text = desc;
            arrowL.SetActive(count > 1);
            arrowR.SetActive(count > 1);
            BuildCards(count, index);
        }

        /// <summary>Pendant l'animation de départ : on ne montre rien, juste la scène.</summary>
        public void HideAll() => SetPanels(null);

        public void ShowHud()
        {
            SetPanels(hud);
            lastScore = lastCoins = -1;
            SetScore(0, 0, false);
        }

        public void SetScore(int s, int c, bool zombiesClose)
        {
            if (s != lastScore) { score.text = s.ToString(); lastScore = s; }
            if (c != lastCoins) { coinsText.text = c.ToString(); lastCoins = c; }
            warning.gameObject.SetActive(zombiesClose);
        }

        public void Flash() => flash = 0.6f;

        /// <summary>Petit passage par le noir (changement d'écran).</summary>
        public void FadeIn() => fadeAlpha = 1f;

        public void ShowOver(int s, int c, int bestScore, bool record)
        {
            SetPanels(over);
            overTime = 0f;
            overScore.text = s.ToString("N0").Replace(',', ' ');
            overInfo.text = "+" + c + " pièces      ·      RECORD : " + bestScore.ToString("N0").Replace(',', ' ');
            overRecord.gameObject.SetActive(record);
            overTitle.localScale = Vector3.zero;
        }

        void SetPanels(GameObject show)
        {
            if (journal != null) journal.SetActive(show == journal);
            splash.SetActive(show == splash);
            menu.SetActive(show == menu);
            hud.SetActive(show == hud);
            over.SetActive(show == over);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (chancePanel != null) chancePanel.SetActive(false);
            if (countText != null) countText.gameObject.SetActive(false);
        }

        /// <summary>Quel bouton se trouve sous ce point de l'écran (en pixels) ?</summary>
        public UIButton ButtonAt(Vector2 screen)
        {
            if (cinema.activeSelf && skipBtn.gameObject.activeSelf && Inside(skipBtn, screen, 1.3f)) return UIButton.Skip;
            if (help.activeSelf) return UIButton.CloseInfo;   // l'aide se ferme d'une touche
            if (pausePanel.activeSelf)
            {
                if (Inside(pauseResume, screen, 1.1f)) return UIButton.Resume;
                if (Inside(pauseHome, screen, 1.1f)) return UIButton.Home;
                return UIButton.None;
            }
            if (chancePanel.activeSelf)
            {
                if (Inside(chanceYes, screen, 1.1f)) return UIButton.ChanceYes;
                if (Inside(chanceNo, screen, 1.1f)) return UIButton.ChanceNo;
                return UIButton.None;
            }
            if (PanelOpen)
            {
                foreach (var h in hot)
                    if (h.rt.gameObject.activeInHierarchy && Inside(h.rt, screen, 1.08f)) { TappedId = h.id; return UIButton.Panel; }
                return UIButton.None;
            }
            if (journal.activeSelf)
            {
                if (Inside(backBtn, screen, 1.1f)) return UIButton.Back;
                if (Inside(prevBtn, screen, 1.3f)) return UIButton.PrevChapter;
                if (Inside(nextBtn, screen, 1.3f)) return UIButton.NextChapter;
                if (Inside(replayIntroBtn, screen, 1.1f)) return UIButton.ReplayIntro;
                if (Inside(replayEndBtn, screen, 1.1f)) return UIButton.ReplayEnding;
                return UIButton.None;
            }
            if (menu.activeSelf)
            {
                if (Inside(journalBtn, screen, 1.1f)) return UIButton.Journal;
                if (Inside(infoBtn, screen, 1.05f)) return UIButton.Shop;
                if (Inside(missionsBtn, screen, 1.05f)) return UIButton.Missions;
                if (Inside(settingsBtn, screen, 1.05f)) return UIButton.Settings;
                for (int i = 0; i < cards.Count; i++)
                    if (Inside(cards[i], screen, 1.05f)) { TappedCard = i; return UIButton.Card; }
                if (Inside(playBtn, screen, 1.15f)) return UIButton.Play;
                if (arrowL.activeSelf && (Inside(arrowLRect, screen, 1.2f) || screen.x < Screen.width * 0.2f)) return UIButton.Left;
                if (arrowR.activeSelf && (Inside(arrowRRect, screen, 1.2f) || screen.x > Screen.width * 0.8f)) return UIButton.Right;
            }
            if (hud.activeSelf && Inside(pauseBtn, screen, 1.3f)) return UIButton.Pause;
            if (over.activeSelf)
            {
                if (Inside(retryBtn, screen, 1.1f)) return UIButton.Retry;
                if (Inside(menuBtn, screen, 1.1f)) return UIButton.Menu;
                if (Inside(overMissionsBtn, screen, 1.1f)) return UIButton.Shop;
            }
            return UIButton.None;
        }

        public void Press(UIButton b)
        {
            var rt = b == UIButton.Shop ? (over.activeSelf ? overMissionsBtn : infoBtn) : b == UIButton.Pause ? pauseBtn : b == UIButton.Resume ? pauseResume : b == UIButton.Home ? pauseHome : b == UIButton.ChanceYes ? chanceYes : b == UIButton.ChanceNo ? chanceNo :
                b == UIButton.Missions ? missionsBtn : b == UIButton.Settings ? settingsBtn : b == UIButton.Info ? infoBtn : b == UIButton.Play ? playBtn : b == UIButton.Retry ? retryBtn : b == UIButton.Menu ? menuBtn : b == UIButton.Journal ? journalBtn : b == UIButton.Back ? backBtn : null;
            if (rt != null) rt.localScale = Vector3.one * 0.88f;
        }

        static bool Inside(RectTransform rt, Vector2 screen, float grow)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // en Screen Space Overlay, c'est déjà en pixels écran
            var c = (corners[0] + corners[2]) * 0.5f;
            var half = (corners[2] - corners[0]) * 0.5f * grow;
            return Mathf.Abs(screen.x - c.x) <= half.x && Mathf.Abs(screen.y - c.y) <= half.y;
        }

        void Update()
        {
            if (splash == null) return; // interface pas encore construite (ou scripts rechargés pendant le jeu)
            float dt = Time.unscaledDeltaTime;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);

            if (splash.activeSelf)
            {
                splashTime += dt;
                // Le logo arrive en rebondissant, puis respire doucement
                float t = Mathf.Clamp01(splashTime / 0.7f);
                float pop = t < 1f ? BackOut(t) : 1f + 0.03f * Mathf.Sin((splashTime - 0.7f) * 2.5f);
                if (splashLogo != null)
                {
                    splashLogo.localScale = Vector3.one * pop;
                    splashLogo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(splashTime * 1.7f) * 1.5f);
                }
                Fade(splashTap, 0.6f + 0.4f * pulse);
                if (loadFill != null)
                {
                    float w = ((RectTransform)loadFill.transform.parent).rect.width - 12f;
                    float prog = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(splashTime / (SplashDuration - 0.3f)));
                    loadFill.rectTransform.sizeDelta = new Vector2(w * prog, loadFill.rectTransform.sizeDelta.y);
                }
            }

            if (menu.activeSelf)
            {
                menuTime += dt;
                if (menuLogo != null) menuLogo.localScale = Vector3.one * (1f + 0.025f * Mathf.Sin(menuTime * 2.2f));
                float s = 1f + 0.05f * Mathf.Sin(menuTime * 5f);
                playBtn.localScale = Vector3.Lerp(playBtn.localScale, Vector3.one * s, Mathf.Clamp01(12f * dt));
            }

            if (over.activeSelf)
            {
                overTime += dt;
                float t = Mathf.Clamp01(overTime / 0.45f);
                float k = BackOut(t);
                retryBtn.localScale = Vector3.Lerp(retryBtn.localScale, Vector3.one * k * (1f + 0.04f * Mathf.Sin(overTime * 5f)), Mathf.Clamp01(15f * dt));
                menuBtn.localScale = Vector3.Lerp(menuBtn.localScale, Vector3.one * k, Mathf.Clamp01(15f * dt));
                overMissionsBtn.localScale = Vector3.Lerp(overMissionsBtn.localScale, Vector3.one * k, Mathf.Clamp01(15f * dt));
                // Le titre tombe en rebondissant, puis tremble un peu
                float tt = Mathf.Clamp01(overTime / 0.6f);
                overTitle.localScale = Vector3.one * (tt < 1f ? BackOut(tt) : 1f + 0.02f * Mathf.Sin(overTime * 3f));
                overTitle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(overTime * 1.6f) * 1.2f - 2f);
            }

            if (warning != null && warning.gameObject.activeSelf) Fade(warning, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f));
            UpdateStory(dt);
            flash = Mathf.MoveTowards(flash, 0f, dt);
            redFlash.color = new Color(0.6f, 0f, 0f, flash * 0.6f);
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, 0f, dt * 2.2f);
            fade.color = new Color(0f, 0f, 0f, fadeAlpha);
        }

        // ================= Histoire : radio, titres de chapitre, cinématiques, journal =================

        GameObject radio, card, cinema, journal, toast;
        Text radioWho, radioTitle, radioBody, cardNum, cardName, cardGoal, cineCenter, cineSub, toastText;
        Text jTitle, jProgress, jChapter, jGoal;
        readonly Text[] jEntries = new Text[ZRStory.CluesPerChapter];
        RectTransform skipBtn, backBtn, prevBtn, nextBtn, replayIntroBtn, replayEndBtn, cineLogo;
        Image cineBlack;
        CanvasGroup radioGroup, cardGroup, toastGroup;
        Text cineEndText;
        readonly System.Collections.Generic.Queue<string[]> radioQueue = new System.Collections.Generic.Queue<string[]>();
        string radioFull = "";
        float radioTime = -1f, radioDuration, cardTime = -1f, toastTime = -1f, blackTarget, blackSpeed = 1f, cineTypeTime = -1f;
        string cineTyped = "";

        void BuildStory(Vector2 top, Vector2 bottom, Vector2 center, Sprite logoSprite)
        {
            // --- Bandeau radio (pendant la course) ---
            radio = new GameObject("Radio", typeof(RectTransform));
            radio.transform.SetParent(root, false);
            var rrt = (RectTransform)radio.transform;
            rrt.anchorMin = rrt.anchorMax = top; rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -320f); rrt.sizeDelta = new Vector2(1000f, 300f);
            radioGroup = radio.AddComponent<CanvasGroup>();
            radioGroup.blocksRaycasts = false;
            Child(rrt, "Fond", new Color(0.02f, 0.03f, 0.02f, 0.78f), Vector2.zero, Vector2.zero);
            var edge = Child(rrt, "Bord", new Color(0.55f, 1f, 0.25f, 0.9f), Vector2.zero, Vector2.zero);
            var ert = edge.rectTransform; ert.anchorMin = new Vector2(0f, 0f); ert.anchorMax = new Vector2(0f, 1f); ert.pivot = new Vector2(0f, 0.5f);
            ert.offsetMin = new Vector2(0f, 10f); ert.offsetMax = new Vector2(14f, -10f);
            radioWho = T(Label(radio.transform, "", 38, Toxic, new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(920f, 48f), Ink, TextAnchor.UpperLeft));
            radioTitle = T(Label(radio.transform, "", 52, Color.white, new Vector2(0f, 1f), new Vector2(40f, -64f), new Vector2(920f, 58f), Ink, TextAnchor.UpperLeft));
            radioBody = Label(radio.transform, "", 34, Pale, new Vector2(0f, 1f), new Vector2(40f, -122f), new Vector2(920f, 170f), Ink, TextAnchor.UpperLeft);
            radioBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            radio.SetActive(false);

            // --- Petit message « INDICE 3 / 20 » ---
            toast = new GameObject("Toast", typeof(RectTransform));
            toast.transform.SetParent(root, false);
            Stretch((RectTransform)toast.transform);
            toastGroup = toast.AddComponent<CanvasGroup>(); toastGroup.blocksRaycasts = false;
            toastText = T(Label(toast.transform, "", 92, Toxic, center, new Vector2(0f, 260f), new Vector2(1000f, 110f), Purple), -3f);
            toast.SetActive(false);

            // --- Titre de chapitre (début de course) ---
            card = new GameObject("Chapitre", typeof(RectTransform));
            card.transform.SetParent(root, false);
            Stretch((RectTransform)card.transform);
            cardGroup = card.AddComponent<CanvasGroup>(); cardGroup.blocksRaycasts = false;
            cardNum = T(Label(card.transform, "", 44, Toxic, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(1000f, 60f), Ink));
            cardName = T(Label(card.transform, "", 74, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -360f), new Vector2(1060f, 95f), Purple), -2f);
            cardGoal = Label(card.transform, "", 32, Pale, new Vector2(0.5f, 1f), new Vector2(0f, -425f), new Vector2(1000f, 50f), Ink);
            card.SetActive(false);

            // --- Cinématiques : bandes noires, noir complet, textes, bouton PASSER ---
            cinema = new GameObject("Cinema", typeof(RectTransform));
            cinema.transform.SetParent(root, false);
            Stretch((RectTransform)cinema.transform);
            cineBlack = FullImage("Noir", cinema.transform, new Color(0f, 0f, 0f, 1f));
            BarEdge(cinema.transform, true); BarEdge(cinema.transform, false);
            cineLogo = Logo(cinema.transform, logoSprite, center, new Vector2(0f, 160f), 820f);
            if (cineLogo != null) cineLogo.gameObject.SetActive(false);
            cineEndText = T(Label(cinema.transform, "TO BE CONTINUED...", 96, Color.white, center, new Vector2(0f, -330f), new Vector2(1000f, 120f), Purple), -3f);
            cineEndText.gameObject.SetActive(false);
            cineCenter = Label(cinema.transform, "", 58, Color.white, center, new Vector2(0f, 60f), new Vector2(960f, 600f), Ink);
            cineCenter.horizontalOverflow = HorizontalWrapMode.Wrap;
            cineSub = Label(cinema.transform, "", 40, Color.white, bottom, new Vector2(0f, 300f), new Vector2(980f, 200f), Ink);
            cineSub.horizontalOverflow = HorizontalWrapMode.Wrap;
            skipBtn = Button(cinema.transform, "PASSER »", ButtonPurple, new Vector2(1f, 1f), new Vector2(-190f, -110f), new Vector2(300f, 100f), 46);
            cinema.SetActive(false);

            // --- Journal ---
            journal = Panel("Journal", new Color(0.03f, 0.02f, 0.05f, 0.94f));
            jTitle = T(Label(journal.transform, "JOURNAL DE SURVIE", 104, Toxic, top, new Vector2(0f, -70f), new Vector2(1000f, 120f), Purple), -2f);
            jProgress = Label(journal.transform, "", 40, Pale, top, new Vector2(0f, -170f), new Vector2(1000f, 60f), Ink);
            prevBtn = Sym(Label(journal.transform, "◄", 110, Color.white, top, new Vector2(-460f, -265f), new Vector2(130f, 140f), Ink)).rectTransform;
            nextBtn = Sym(Label(journal.transform, "►", 110, Color.white, top, new Vector2(460f, -265f), new Vector2(130f, 140f), Ink)).rectTransform;
            jChapter = T(Label(journal.transform, "", 60, Color.white, top, new Vector2(0f, -250f), new Vector2(780f, 80f), Blood));
            jGoal = Label(journal.transform, "", 34, Pale, top, new Vector2(0f, -310f), new Vector2(900f, 50f), Ink);
            for (int i = 0; i < jEntries.Length; i++)
            {
                var e = Label(journal.transform, "", 31, Pale, top, new Vector2(0f, -380f - i * 285f), new Vector2(960f, 270f), Ink, TextAnchor.UpperLeft);
                e.horizontalOverflow = HorizontalWrapMode.Wrap;
                e.supportRichText = true;
                ((RectTransform)e.transform).pivot = new Vector2(0.5f, 1f);
                jEntries[i] = e;
            }
            replayIntroBtn = Button(journal.transform, "REVOIR L'INTRO", ButtonPurple, bottom, new Vector2(-260f, 200f), new Vector2(460f, 100f), 40);
            replayEndBtn = Button(journal.transform, "REVOIR LA FIN", ButtonPurple, bottom, new Vector2(260f, 200f), new Vector2(460f, 100f), 40);
            backBtn = Button(journal.transform, "RETOUR", ButtonGreen, bottom, new Vector2(0f, 75f), new Vector2(440f, 110f), 60);
        }

        void BarEdge(Transform parent, bool upper)
        {
            var go = new GameObject(upper ? "BandeHaut" : "BandeBas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, upper ? 1f : 0f); rt.anchorMax = new Vector2(1f, upper ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, upper ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 190f); rt.anchoredPosition = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = Color.black; img.raycastTarget = false;
        }

        /// <summary>Transmission radio en bandeau, sans arrêter la course (file d'attente).</summary>
        public void Radio(string who, string title, string body)
        {
            radioQueue.Enqueue(new[] { who, title, body });
            if (radioTime < 0f) NextRadio();
        }

        void NextRadio()
        {
            if (radioQueue.Count == 0) { radioTime = -1f; radio.SetActive(false); return; }
            var r = radioQueue.Dequeue();
            radioWho.text = r[0].ToUpperInvariant();
            radioTitle.text = r[1];
            radioTitle.gameObject.SetActive(r[1].Length > 0);
            ((RectTransform)radioBody.transform).anchoredPosition = new Vector2(40f, r[1].Length > 0 ? -122f : -70f);
            radioFull = "« " + r[2] + " »";
            radioBody.text = "";
            radioDuration = 3f + radioFull.Length * 0.045f;
            radioTime = 0f;
            radio.SetActive(true);
        }

        public void ClearRadio() { radioQueue.Clear(); radioTime = -1f; if (radio != null) radio.SetActive(false); }

        public void ClueToast(string text) { toastText.text = text; toastText.color = Toxic; toastTime = 0f; toast.SetActive(true); }

        public void ChapterCard(string num, string name, string goal)
        {
            cardNum.text = num; cardName.text = name; cardGoal.text = goal;
            cardTime = 0f; card.SetActive(true);
        }

        // --- Cinématiques ---
        public void CinemaShow(bool on)
        {
            cinema.SetActive(on);
            if (on) { SetPanels(null); cineCenter.text = ""; cineSub.text = ""; cineTypeTime = -1f; if (cineLogo != null) cineLogo.gameObject.SetActive(false); cineEndText.gameObject.SetActive(false); ClearRadio(); }
        }

        /// <summary>Fondu du noir complet : alpha visé et vitesse (par seconde).</summary>
        public void CinemaBlack(float target, float speed)
        {
            blackTarget = target; blackSpeed = speed;
            if (speed <= 0f) cineBlack.color = new Color(0f, 0f, 0f, target);
        }

        public void CinemaCenter(string text, int size = 58, bool title = false)
        {
            cineCenter.font = title ? titleFont : font;
            cineCenter.fontSize = size; cineCenter.text = text; cineTypeTime = -1f;
        }
        public void CinemaSubtitle(string text) => cineSub.text = text;

        /// <summary>Texte qui s'écrit lettre par lettre au centre (fin secrète).</summary>
        public void CinemaTyped(string text, int size = 46) { cineCenter.font = font; cineCenter.fontSize = size; cineTyped = text; cineTypeTime = 0f; cineCenter.text = ""; }

        public void CinemaEndCard(bool on)
        {
            cineCenter.text = ""; cineSub.text = ""; cineTypeTime = -1f;
            if (cineLogo != null) cineLogo.gameObject.SetActive(on);
            cineEndText.gameObject.SetActive(on);
        }

        // --- Journal ---
        public void ShowJournal(int chapter)
        {
            SetPanels(journal);
            int found = ZRStory.FoundCount;
            jProgress.text = "Indices trouvés : " + found + " / " + ZRStory.Clues.Length;
            var c = ZRStory.Chapters[chapter];
            bool open = ZRStory.ChapterUnlocked(chapter);
            jChapter.text = "CHAPITRE " + (chapter + 1) + (open ? " — " + c.name : " — ???");
            jGoal.text = open ? c.goal : "Termine le chapitre " + chapter + " pour le débloquer.";
            for (int k = 0; k < jEntries.Length; k++)
            {
                int i = chapter * ZRStory.CluesPerChapter + k;
                var clue = ZRStory.Clues[i];
                if (ZRStory.IsFound(i))
                    jEntries[k].text = "<color=#8CFF40><b>" + ZRStory.TypeNames[(int)clue.type] + "</b> · " + clue.speaker + "</color>\n<b><color=#FFFFFF>" + clue.title + "</color></b>\n" + clue.text;
                else
                    jEntries[k].text = "<color=#777777><b>INDICE " + (k + 1) + " — ???</b>\nPas encore découvert. Cours plus loin pour le trouver.</color>";
            }
            prevBtn.gameObject.SetActive(chapter > 0);
            nextBtn.gameObject.SetActive(chapter < ZRStory.Chapters.Length - 1);
            replayIntroBtn.gameObject.SetActive(ZRStory.IntroSeen);
            replayEndBtn.gameObject.SetActive(ZRStory.Secret >= 2);
        }

        void UpdateStory(float dt)
        {
            journalBtn.localScale = Vector3.Lerp(journalBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            infoBtn.localScale = Vector3.Lerp(infoBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            missionsBtn.localScale = Vector3.Lerp(missionsBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            settingsBtn.localScale = Vector3.Lerp(settingsBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            foreach (var h in hot) h.rt.localScale = Vector3.Lerp(h.rt.localScale, Vector3.one, Mathf.Clamp01(12f * dt));
            for (int i = 0; i < cards.Count; i++)
            {
                float target = i == selectedCard ? 1.06f + 0.02f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
                cards[i].localScale = Vector3.Lerp(cards[i].localScale, Vector3.one * target, Mathf.Clamp01(10f * dt));
            }
            if (arrowL != null && arrowL.activeSelf)
            {
                float k = Mathf.Sin(Time.unscaledTime * 4f) * 10f;
                arrowLRect.anchoredPosition = new Vector2(-400f - k, 230f);
                arrowRRect.anchoredPosition = new Vector2(400f + k, 230f);
            }
            backBtn.localScale = Vector3.Lerp(backBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            if (radioTime >= 0f)
            {
                radioTime += dt;
                int n = Mathf.Min(radioFull.Length, Mathf.FloorToInt(radioTime * 55f));
                if (radioBody.text.Length != n) radioBody.text = radioFull.Substring(0, n);
                float a = Mathf.Clamp01(radioTime * 5f) * Mathf.Clamp01((radioDuration - radioTime) * 3f);
                radioGroup.alpha = a;
                if (radioTime > radioDuration) NextRadio();
            }
            if (cardTime >= 0f)
            {
                cardTime += dt;
                cardGroup.alpha = Mathf.Clamp01(cardTime * 2.5f) * Mathf.Clamp01((3.6f - cardTime) * 2f);
                card.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Clamp01(1f - cardTime));
                if (cardTime > 3.6f) { cardTime = -1f; card.SetActive(false); }
            }
            if (toastTime >= 0f)
            {
                toastTime += dt;
                toastGroup.alpha = Mathf.Clamp01((2f - toastTime) * 2f);
                toast.transform.localScale = Vector3.one * (toastTime < 0.4f ? BackOut(toastTime / 0.4f) : 1f);
                if (toastTime > 2f) { toastTime = -1f; toast.SetActive(false); }
            }
            if (cinema.activeSelf)
            {
                var c = cineBlack.color;
                if (blackSpeed > 0f) c.a = Mathf.MoveTowards(c.a, blackTarget, dt * blackSpeed);
                cineBlack.color = c;
                if (cineTypeTime >= 0f)
                {
                    cineTypeTime += dt;
                    int n = Mathf.Min(cineTyped.Length, Mathf.FloorToInt(cineTypeTime * 38f));
                    if (cineCenter.text.Length != n) cineCenter.text = cineTyped.Substring(0, n);
                }
                skipBtn.localScale = Vector3.Lerp(skipBtn.localScale, Vector3.one, Mathf.Clamp01(10f * dt));
            }
        }

        /// <summary>Style « titre » : police Bangers, contour épais et ombre portée pour l'effet relief.</summary>
        Text T(Text t, float angle = 0f)
        {
            t.font = titleFont; t.fontStyle = titleFont == symbolFont ? FontStyle.Bold : FontStyle.Normal;
            var o = t.GetComponent<Outline>();
            if (o != null) o.effectDistance = new Vector2(5f, -5f);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sh.effectDistance = new Vector2(0f, -9f);
            if (angle != 0f) t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return t;
        }

        /// <summary>Petite pièce dorée dessinée en code (icône du compteur).</summary>
        void CoinIcon(Transform parent, Vector2 pos, float size)
        {
            var go = new GameObject("Icône pièce", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(size, size);
            if (A("Coin") != null)
            {
                var ci = go.AddComponent<Image>(); ci.sprite = A("Coin"); ci.preserveAspect = true; ci.raycastTarget = false;
                rt.sizeDelta = new Vector2(size * 1.2f, size * 1.2f);
                return;
            }
            var outer = go.AddComponent<Image>(); outer.sprite = rounded; outer.type = Image.Type.Sliced; outer.color = new Color(0.75f, 0.45f, 0.05f); outer.raycastTarget = false;
            var inner = Child(rt, "Or", new Color(1f, 0.82f, 0.25f), Vector2.zero, new Vector2(-14f, -14f));
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.7f); sh.effectDistance = new Vector2(0f, -6f);
        }

        /// <summary>Flèches et symboles : police intégrée (les polices de titre n'ont pas ces signes).</summary>
        Text Sym(Text t) { t.font = symbolFont; t.fontStyle = FontStyle.Bold; return t; }

        // ================= Menu : vignettes, flèches, tuiles, aide =================

        readonly System.Collections.Generic.List<RectTransform> cards = new System.Collections.Generic.List<RectTransform>();
        Texture[] portraits;
        int selectedCard = -1, cardCount = -1;
        GameObject help;

        /// <summary>Index de la vignette touchée (après un UIButton.Card).</summary>
        public int TappedCard { get; private set; }
        public bool HelpOpen => help != null && help.activeSelf;

        /// <summary>Portraits des personnages (pris en photo au lancement du jeu).</summary>
        public void SetPortraits(Texture[] p) { portraits = p; cardCount = -1; }

        RectTransform Pill(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Pastille", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            Child(rt, "Bord", new Color(0.35f, 0.25f, 0.5f, 0.9f), Vector2.zero, new Vector2(8f, 8f));
            Child(rt, "Fond", new Color(0.04f, 0.03f, 0.08f, 0.82f), Vector2.zero, Vector2.zero);
            return rt;
        }

        /// <summary>Chevron vert (deux barres arrondies en V).</summary>
        RectTransform Chevron(Transform parent, bool left, Vector2 pos)
        {
            var go = new GameObject(left ? "Flèche gauche" : "Flèche droite", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(150f, 220f);
            float s = left ? 1f : -1f;
            for (int k = 0; k < 2; k++)
            {
                float sign = k == 0 ? 1f : -1f;
                var bar = new GameObject("Barre", typeof(RectTransform));
                bar.transform.SetParent(rt, false);
                var b = (RectTransform)bar.transform;
                b.sizeDelta = new Vector2(150f, 50f);
                b.anchoredPosition = new Vector2(0f, sign * 46f);
                b.localRotation = Quaternion.Euler(0f, 0f, sign * s * 45f);
                var shadow = Child(b, "Ombre", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, -10f), new Vector2(14f, 14f));
                Child(b, "Bord", new Color(0.1f, 0.25f, 0.05f), Vector2.zero, new Vector2(14f, 14f));
                Child(b, "Vert", ButtonGreen, Vector2.zero, Vector2.zero);
                Child(b, "Reflet", new Color(1f, 1f, 1f, 0.3f), new Vector2(0f, 9f), new Vector2(-30f, -30f));
            }
            return rt;
        }

        /// <summary>Tuile sombre avec une grosse icône et un libellé (comme BOUTIQUE / MISSIONS).</summary>
        RectTransform Tile(Transform parent, string icon, string label, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Tuile " + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var full = A(label == "MISSIONS" ? "TileMissions" : label == "PARAMÈTRES" ? "TileSettings" : label == "BOUTIQUE" ? "TileShop" : "-");
            if (full != null) { ArtImage(rt, full); return rt; }
            if (A("TileBlank") != null)
            {
                SlicedArt(rt, A("TileBlank"));
                T(Label(rt, icon, 130, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(size.x, 140f), Ink));
                Label(rt, label, 50, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(size.x, 60f), Ink).GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
                return rt;
            }
            Child(rt, "Ombre", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, -12f), Vector2.zero);
            Child(rt, "Bord", new Color(0.3f, 0.3f, 0.36f), Vector2.zero, new Vector2(10f, 10f));
            Child(rt, "Fond", new Color(0.1f, 0.11f, 0.15f, 0.96f), Vector2.zero, Vector2.zero);
            var disc = Child(rt, "Pastille", Toxic, new Vector2(0f, 0f), Vector2.zero);
            var drt = disc.rectTransform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 1f); drt.sizeDelta = new Vector2(78f, 78f); drt.anchoredPosition = new Vector2(0f, -52f);
            if (icon == "gear") Gear(drt);
            else T(Label(drt, icon, 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(78f, 78f), Purple));
            T(Label(rt, label, label.Length > 9 ? 42 : 50, Color.white, new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(size.x, 60f), Ink));
            return rt;
        }

        void BuildCards(int count, int index)
        {
            selectedCard = index;
            int total = Mathf.Max(4, count);
            if (cardCount != total || cards.Count == 0)
            {
                foreach (var c in cards) Destroy(c.gameObject);
                cards.Clear();
                cardCount = total;
                for (int i = 0; i < total; i++) cards.Add(Card(i, i < count));
            }
            for (int i = 0; i < cards.Count; i++)
            {
                bool sel = i == index;
                cards[i].Find("Halo").gameObject.SetActive(sel);
                var face = cards[i].Find("Masque/Portrait");
                if (face != null) face.GetComponent<RawImage>().color = sel ? Color.white : new Color(0.55f, 0.55f, 0.6f);
            }
        }

        RectTransform Card(int i, bool unlocked)
        {
            var go = new GameObject("Vignette " + (i + 1), typeof(RectTransform));
            go.transform.SetParent(cardsRow, false);
            var rt = (RectTransform)go.transform;
            float w = 200f, gap = 32f;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, w);
            rt.anchoredPosition = new Vector2((i - (cardCount - 1) * 0.5f) * (w + gap), 0f);
            if (A("CardFrame") != null && A("CardSelected") != null)
            {
                rt.sizeDelta = new Vector2(w, w * 0.955f);
                var sel = ArtImage(rt, A("CardSelected")); sel.name = "Halo";
                sel.anchorMin = sel.anchorMax = new Vector2(0.5f, 0.5f);
                sel.sizeDelta = new Vector2(w * 460f / 386f, w * 0.955f * 424f / 346f);
                sel.gameObject.SetActive(false);
                var mask = Child(rt, "Masque", new Color(0.13f, 0.11f, 0.2f), Vector2.zero, new Vector2(-24f, -24f));
                if (!unlocked)
                {
                    // Joueur en attente : un gros point d'interrogation dans le cadre
                    mask.color = new Color(0.07f, 0.08f, 0.13f);
                    T(Label(mask.transform, "?", 150, new Color(0.55f, 0.57f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(w, 180f), Ink));
                    ArtImage(rt, A("CardFrame")).name = "Cadre";
                    sel.SetAsLastSibling();
                    return rt;
                }
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (portraits != null && i < portraits.Length && portraits[i] != null)
                {
                    var f = new GameObject("Portrait", typeof(RectTransform));
                    f.transform.SetParent(mask.transform, false);
                    Stretch((RectTransform)f.transform);
                    var raw = f.AddComponent<RawImage>(); raw.texture = portraits[i]; raw.raycastTarget = false;
                }
                ArtImage(rt, A("CardFrame")).name = "Cadre";
                sel.SetAsLastSibling();
                return rt;
            }
            var halo = Child(rt, "Halo", Toxic, Vector2.zero, new Vector2(26f, 26f));
            halo.gameObject.SetActive(false);
            Child(rt, "Bord", new Color(0.25f, 0.25f, 0.3f), Vector2.zero, new Vector2(8f, 8f));
            var bg = Child(rt, "Masque", new Color(0.13f, 0.11f, 0.2f), Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (unlocked && portraits != null && i < portraits.Length && portraits[i] != null)
            {
                var face = new GameObject("Portrait", typeof(RectTransform));
                face.transform.SetParent(bg.transform, false);
                Stretch((RectTransform)face.transform);
                var raw = face.AddComponent<RawImage>(); raw.texture = portraits[i]; raw.raycastTarget = false;
            }
            else if (!unlocked)
            {
                T(Label(bg.transform, "?", 110, new Color(0.45f, 0.45f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(w, 130f), Ink));
                Label(bg.transform, "BIENTÔT", 30, new Color(0.6f, 0.6f, 0.65f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(w, 40f), Ink);
            }
            return rt;
        }

        // ---------- Aide « Comment jouer » ----------
        void BuildHelp()
        {
            help = new GameObject("Aide", typeof(RectTransform));
            help.transform.SetParent(root, false);
            Stretch((RectTransform)help.transform);
            FullImage("Voile", help.transform, new Color(0f, 0f, 0f, 0.72f));
            var cardGo = new GameObject("Carte", typeof(RectTransform));
            cardGo.transform.SetParent(help.transform, false);
            var c = (RectTransform)cardGo.transform;
            c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.sizeDelta = new Vector2(980f, 1480f);
            Child(c, "Bord", Toxic, Vector2.zero, new Vector2(12f, 12f));
            Child(c, "Fond", new Color(0.07f, 0.05f, 0.11f, 0.98f), Vector2.zero, Vector2.zero);
            var top = new Vector2(0.5f, 1f);
            T(Label(c, "COMMENT JOUER", 104, Toxic, top, new Vector2(0f, -40f), new Vector2(940f, 120f), Purple), -2f);

            float y = -200f;
            HelpRow(c, "◄ ►", "Glisse à gauche ou à droite pour changer de voie.", ref y);
            HelpRow(c, "▲", "Glisse vers le haut pour sauter… même sur les petites voitures !", ref y);
            HelpRow(c, "▼", "Glisse vers le bas pour passer sous les barrières.", ref y);
            HelpRow(c, "●", "Ramasse un maximum de pièces.", ref y, new Color(1f, 0.82f, 0.25f));

            T(Label(c, "SUPER OBJETS", 60, Color.white, top, new Vector2(0f, y - 10f), new Vector2(940f, 70f), Purple));
            y -= 90f;
            string[] what = { "attire les pièces vers toi", "encaisse un choc à ta place", "tu sautes beaucoup plus haut", "chaque pièce compte double" };
            for (int i = 0; i < 4; i++)
            {
                var dot = Child(c, "Bonus", ZRWorld.PowerColors[i], Vector2.zero, Vector2.zero);
                var d = dot.rectTransform; d.anchorMin = d.anchorMax = new Vector2(0f, 1f); d.pivot = new Vector2(0f, 1f);
                d.sizeDelta = new Vector2(300f, 66f); d.anchoredPosition = new Vector2(50f, y);
                T(Label(d, ZRWorld.PowerNames[i], 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(300f, 60f), Ink));
                var t = Label(c, what[i], 38, Pale, new Vector2(0f, 1f), new Vector2(380f, y - 12f), new Vector2(560f, 50f), Ink, TextAnchor.UpperLeft);
                y -= 84f;
            }
            y -= 20f;
            var warn = Label(c, "Touche une voiture ou un obstacle : la horde t'attrape ! Les barrières à glisser te font seulement trébucher.", 38, new Color(1f, 0.45f, 0.4f), top, new Vector2(0f, y), new Vector2(880f, 120f), Ink);
            warn.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)warn.transform).pivot = new Vector2(0.5f, 1f);
            Label(c, "Clavier : flèches ou ZQSD · Espace pour sauter", 30, new Color(0.7f, 0.7f, 0.75f), new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(900f, 40f), Ink);
            Button(c, "COMPRIS !", ButtonGreen, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 140f), 80);
            help.SetActive(false);
        }

        void HelpRow(RectTransform c, string key, string text, ref float y, Color? keyColor = null)
        {
            var k = Child(c, "Touche", new Color(0.18f, 0.16f, 0.26f), Vector2.zero, Vector2.zero);
            var kr = k.rectTransform; kr.anchorMin = kr.anchorMax = new Vector2(0f, 1f); kr.pivot = new Vector2(0f, 1f);
            kr.sizeDelta = new Vector2(170f, 110f); kr.anchoredPosition = new Vector2(50f, y);
            Sym(Label(kr, key, 60, keyColor ?? Toxic, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 110f), Ink));
            var t = Label(c, text, 40, Color.white, new Vector2(0f, 1f), new Vector2(250f, y - 8f), new Vector2(680f, 100f), Ink, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)t.transform).pivot = new Vector2(0f, 1f);
            y -= 135f;
        }

        public void ShowHelp(bool on) { help.SetActive(on); if (on) help.transform.SetAsLastSibling(); fade.transform.SetAsLastSibling(); }

        readonly System.Collections.Generic.Dictionary<string, Sprite> art = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Images découpées des maquettes (à donner AVANT Build).</summary>
        public void SetArt(Sprite[] sprites)
        {
            art.Clear();
            if (sprites == null) return;
            foreach (var sp in sprites) if (sp != null) art[sp.name] = sp;
        }

        Sprite A(string name) { Sprite sp; return art.TryGetValue(name, out sp) ? sp : null; }

        /// <summary>Image étirable (9-slice) : les bords arrondis gardent leur forme quelle que soit la taille.</summary>
        static RectTransform SlicedArt(RectTransform parent, Sprite sp)
        {
            var rt = ArtImage(parent, sp);
            var img = rt.GetComponent<Image>();
            img.preserveAspect = false;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = sp.rect.height / Mathf.Max(1f, parent.sizeDelta.y);
            rt.SetAsFirstSibling();
            return rt;
        }

        void PillArt(RectTransform pill)
        {
            var sp = A("PillBody");
            if (sp == null) return;
            foreach (Transform c in pill) c.gameObject.SetActive(false);
            SlicedArt(pill, sp);
        }

        /// <summary>Texte qui remplit la hauteur de son cadre : le chiffre est centré verticalement dans la pastille.</summary>
        static Text InPill(Text t, float left, float right)
        {
            var rt = (RectTransform)t.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, 0f); rt.offsetMax = new Vector2(-right, 4f);
            return t;
        }

        /// <summary>JOUER et les flèches du menu : images découpées de la maquette.</summary>
        public void SetMenuArt(Sprite play, Sprite left, Sprite right)
        {
            if (play != null)
            {
                foreach (Transform c in playBtn) c.gameObject.SetActive(false);
                playBtn.sizeDelta = new Vector2(660f, 660f * play.rect.height / play.rect.width);
                ArtImage(playBtn, play);
            }
            ArrowArt(arrowLRect, left);
            ArrowArt(arrowRRect, right);
        }

        static void ArrowArt(RectTransform rt, Sprite sp)
        {
            if (sp == null || rt == null) return;
            foreach (Transform c in rt) c.gameObject.SetActive(false);
            foreach (var g in rt.GetComponents<Graphic>()) g.enabled = false;
            rt.sizeDelta = new Vector2(130f, 130f * sp.rect.height / sp.rect.width);
            ArtImage(rt, sp);
        }

        /// <summary>Remplace le titre, l'encadré et REJOUER dessinés en code par les images découpées de la maquette.</summary>
        public void SetOverArt(Sprite title, Sprite panel, Sprite retry)
        {
            if (title != null)
            {
                foreach (Transform c in overTitle) c.gameObject.SetActive(false);
                overTitle.sizeDelta = new Vector2(840f, 840f * title.rect.height / title.rect.width);
                overTitle.anchoredPosition = new Vector2(0f, -70f);
                ArtImage(overTitle, title);
            }
            if (panel != null)
            {
                foreach (Transform c in overBox)
                    if (c != overScore.transform && c != overRecord.transform) c.gameObject.SetActive(false);
                float w = 860f, h = w * panel.rect.height / panel.rect.width;
                overBox.sizeDelta = new Vector2(w, h);
                ArtImage(overBox, panel).SetAsFirstSibling();
                // Le score s'écrit à la place du « 2450 » de l'image
                var sr = (RectTransform)overScore.transform;
                sr.anchorMin = sr.anchorMax = new Vector2(0f, 0f); sr.pivot = new Vector2(0f, 0.5f);
                sr.anchoredPosition = new Vector2(w * 0.368f, h * 0.235f); sr.sizeDelta = new Vector2(w * 0.6f, h * 0.36f);
                overScore.fontSize = Mathf.RoundToInt(h * 0.3f);
                overScore.alignment = TextAnchor.MiddleLeft;
                overScore.color = new Color(0.55f, 1f, 0.1f);
            }
            if (retry != null)
            {
                foreach (Transform c in retryBtn) c.gameObject.SetActive(false);
                retryBtn.sizeDelta = new Vector2(780f, 780f * retry.rect.height / retry.rect.width);
                retryBtn.anchoredPosition = new Vector2(0f, 425f);
                ArtImage(retryBtn, retry);
            }
        }

        static RectTransform ArtImage(RectTransform parent, Sprite sprite)
        {
            var go = new GameObject("Image " + sprite.name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
            return rt;
        }

        // ================= Écran GAME OVER : titre gluant, tête de mort, boutons =================

        static readonly Color Slime = new Color(0.62f, 1f, 0.12f);

        /// <summary>« GAME OVER » en lettres vertes gluantes avec des gouttes qui coulent.</summary>
        RectTransform SlimeTitle(Transform parent, Vector2 pos)
        {
            var go = new GameObject("Titre GAME OVER", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(1000f, 520f);
            Drips(rt, new[] { -330f, -170f, 30f, 190f, 330f }, Slime, -235f);
            Drips(rt, new[] { -260f, -60f, 110f, 270f }, Slime, -470f);
            SlimeWord(rt, "GAME", new Vector2(0f, -120f));
            SlimeWord(rt, "OVER", new Vector2(0f, -355f));
            return rt;
        }

        void SlimeWord(RectTransform parent, string word, Vector2 pos)
        {
            var t = T(Label(parent, word, 250, Slime, new Vector2(0.5f, 1f), pos, new Vector2(1000f, 260f), new Color(0.2f, 0.03f, 0.3f)));
            var o = t.GetComponent<Outline>(); o.effectDistance = new Vector2(9f, -9f); o.effectColor = new Color(0.2f, 0.03f, 0.3f, 1f);
            var o2 = t.gameObject.AddComponent<Outline>(); o2.effectDistance = new Vector2(-7f, 7f); o2.effectColor = new Color(0.2f, 0.03f, 0.3f, 1f);
            t.GetComponent<Shadow>().effectDistance = new Vector2(0f, -16f);
            ((RectTransform)t.transform).pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Gouttes vertes qui pendent (rectangles arrondis + goutte ronde).</summary>
        void Drips(RectTransform parent, float[] xs, Color col, float y)
        {
            for (int i = 0; i < xs.Length; i++)
            {
                float len = 50f + (i * 37 % 5) * 18f, w = 26f + (i % 2) * 8f;
                var d = Child(parent, "Goutte", col, Vector2.zero, Vector2.zero).rectTransform;
                d.anchorMin = d.anchorMax = new Vector2(0.5f, parent.pivot.y >= 1f ? 1f : 0.5f); d.pivot = new Vector2(0.5f, 1f);
                d.sizeDelta = new Vector2(w, len); d.anchoredPosition = new Vector2(xs[i], y);
                var drop = Child(parent, "Goutte", col, Vector2.zero, Vector2.zero).rectTransform;
                drop.anchorMin = drop.anchorMax = d.anchorMin; drop.pivot = new Vector2(0.5f, 1f);
                drop.sizeDelta = new Vector2(w + 6f, w + 6f); drop.anchoredPosition = new Vector2(xs[i], y - len + 8f);
            }
        }

        /// <summary>Tête de mort dessinée en code (crâne, yeux en X, mâchoire).</summary>
        void Skull(RectTransform parent, Vector2 center)
        {
            var c = Child(parent, "Crâne", new Color(0.9f, 0.9f, 0.88f), Vector2.zero, Vector2.zero).rectTransform;
            c.anchorMin = c.anchorMax = new Vector2(0f, 0f); c.sizeDelta = new Vector2(140f, 128f); c.anchoredPosition = center + new Vector2(0f, 12f);
            var jaw = Child(parent, "Mâchoire", new Color(0.9f, 0.9f, 0.88f), Vector2.zero, Vector2.zero).rectTransform;
            jaw.anchorMin = jaw.anchorMax = new Vector2(0f, 0f); jaw.sizeDelta = new Vector2(84f, 54f); jaw.anchoredPosition = center + new Vector2(0f, -52f);
            var dark = new Color(0.12f, 0.1f, 0.14f);
            T(Label(c, "X  X", 54, dark, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(140f, 70f), new Color(0.9f, 0.9f, 0.88f)));
            Label(jaw, "| | |", 34, dark, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(84f, 50f), new Color(0.9f, 0.9f, 0.88f));
        }

        /// <summary>Bouton sombre (ACCUEIL, MISSIONS) comme sur la maquette.</summary>
        RectTransform DarkButton(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject("Bouton " + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(420f, 130f);
            if (text == "ACCUEIL" && A("TileHome") != null)
            {
                rt.sizeDelta = new Vector2(430f, 430f * A("TileHome").rect.height / A("TileHome").rect.width);
                ArtImage(rt, A("TileHome"));
                return rt;
            }
            if (text == "BOUTIQUE" && A("WideShop") != null)
            {
                rt.sizeDelta = new Vector2(430f, 430f * A("WideShop").rect.height / A("WideShop").rect.width);
                ArtImage(rt, A("WideShop"));
                return rt;
            }
            if (A("WideBlank") != null)
            {
                rt.sizeDelta = new Vector2(430f, 430f * 238f / 622f);
                SlicedArt(rt, A("WideBlank"));
                bool icon = text == "MISSIONS" && A("IconMissions") != null;
                if (icon)
                {
                    var ic = ArtImage(rt, A("IconMissions"));
                    ic.anchorMin = ic.anchorMax = new Vector2(0f, 0.5f); ic.sizeDelta = new Vector2(92f, 102f); ic.anchoredPosition = new Vector2(112f, 0f);
                }
                Label(rt, text, 50, Color.white, new Vector2(0.5f, 0.5f), new Vector2(icon ? 58f : 0f, 0f), new Vector2(380f, 70f), Ink).GetComponent<Outline>().effectDistance = new Vector2(3f, -3f);
                return rt;
            }
            Child(rt, "Ombre", new Color(0f, 0f, 0f, 0.6f), new Vector2(0f, -12f), Vector2.zero);
            Child(rt, "Bord", new Color(0.32f, 0.32f, 0.38f), Vector2.zero, new Vector2(10f, 10f));
            Child(rt, "Fond", new Color(0.1f, 0.11f, 0.15f, 0.97f), Vector2.zero, Vector2.zero);
            T(Label(rt, text, 56, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(420f, 80f), Ink));
            return rt;
        }

        // ================= Pause et seconde chance =================

        GameObject pausePanel, chancePanel;
        RectTransform pauseBtn, pauseResume, pauseHome, chanceYes, chanceNo;
        Text countText, chanceCost, chanceWallet;
        Image chanceBar;

        void BuildPause(Vector2 center, Vector2 bottom)
        {
            // Bouton pause en haut à droite, sous les pièces
            var go = new GameObject("Bouton pause", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            pauseBtn = (RectTransform)go.transform;
            pauseBtn.anchorMin = pauseBtn.anchorMax = new Vector2(1f, 1f); pauseBtn.pivot = new Vector2(0.5f, 0.5f);
            pauseBtn.sizeDelta = new Vector2(130f, 130f); pauseBtn.anchoredPosition = new Vector2(-95f, -250f);
            if (A("TileBlank") != null) SlicedArt(pauseBtn, A("TileBlank"));
            else { Child(pauseBtn, "Bord", new Color(0.3f, 0.3f, 0.36f), Vector2.zero, new Vector2(10f, 10f)); Child(pauseBtn, "Fond", new Color(0.1f, 0.11f, 0.15f, 0.96f), Vector2.zero, Vector2.zero); }
            for (int i = 0; i < 2; i++)
            {
                var bar = Child(pauseBtn, "Barre", Color.white, Vector2.zero, Vector2.zero).rectTransform;
                bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f); bar.sizeDelta = new Vector2(22f, 62f); bar.anchoredPosition = new Vector2(i == 0 ? -17f : 17f, 0f);
            }
            countText = T(Label(hud.transform, "", 320, Slime, center, new Vector2(0f, 120f), new Vector2(600f, 360f), new Color(0.2f, 0.03f, 0.3f)));
            countText.gameObject.SetActive(false);

            // Écran de pause
            pausePanel = Panel("Pause", new Color(0.02f, 0f, 0.05f, 0.7f));
            SlimeWord((RectTransform)pausePanel.transform, "PAUSE", new Vector2(0f, -560f));
            pauseResume = Button(pausePanel.transform, "REPRENDRE", ButtonGreen, center, new Vector2(0f, -40f), new Vector2(700f, 190f), 100);
            pauseHome = DarkButton(pausePanel.transform, "ACCUEIL", new Vector2(0f, 560f));

            // Seconde chance
            chancePanel = Panel("Seconde chance", new Color(0.02f, 0f, 0.05f, 0.62f));
            T(Label(chancePanel.transform, "SECONDE", 190, Slime, center, new Vector2(0f, 520f), new Vector2(1000f, 200f), new Color(0.2f, 0.03f, 0.3f)), -3f);
            T(Label(chancePanel.transform, "CHANCE ?", 190, Slime, center, new Vector2(0f, 340f), new Vector2(1000f, 200f), new Color(0.2f, 0.03f, 0.3f)), -3f);
            var back = Child((RectTransform)chancePanel.transform, "Temps", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero).rectTransform;
            back.anchorMin = back.anchorMax = center; back.sizeDelta = new Vector2(700f, 34f); back.anchoredPosition = new Vector2(0f, 190f);
            chanceBar = Child(back, "Jauge", Slime, Vector2.zero, new Vector2(-8f, -8f));
            chanceBar.type = Image.Type.Filled; chanceBar.fillMethod = Image.FillMethod.Horizontal; chanceBar.sprite = null;
            chanceYes = Button(chancePanel.transform, "CONTINUER", ButtonGreen, center, new Vector2(0f, 0f), new Vector2(720f, 200f), 104);
            var costRow = new GameObject("Prix", typeof(RectTransform));
            costRow.transform.SetParent(chancePanel.transform, false);
            var cr = (RectTransform)costRow.transform; cr.anchorMin = cr.anchorMax = center; cr.sizeDelta = new Vector2(400f, 100f); cr.anchoredPosition = new Vector2(0f, -170f);
            CoinIcon(cr, new Vector2(-300f, -50f), 80f);
            chanceCost = T(Label(cr, "", 84, new Color(1f, 0.82f, 0.25f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(260f, 100f), Ink, TextAnchor.MiddleLeft));
            ((RectTransform)chanceCost.transform).pivot = new Vector2(0f, 0.5f);
            chanceWallet = Label(chancePanel.transform, "", 40, Pale, center, new Vector2(0f, -260f), new Vector2(900f, 60f), Ink);
            chanceNo = DarkButton(chancePanel.transform, "NON MERCI", new Vector2(0f, 330f));
        }

        public bool PauseOpen => pausePanel != null && pausePanel.activeSelf;
        public void ShowPause(bool on) { pausePanel.SetActive(on); if (on) { pausePanel.transform.SetAsLastSibling(); pauseResume.localScale = Vector3.one; pauseHome.localScale = Vector3.one; } }
        public void ShowPauseButton(bool on) => pauseBtn.gameObject.SetActive(on);

        /// <summary>Compte à rebours de reprise (3, 2, 1). 0 = caché.</summary>
        public void SetCountdown(int n, float t)
        {
            countText.gameObject.SetActive(n > 0);
            if (n <= 0) return;
            countText.text = n.ToString();
            countText.transform.localScale = Vector3.one * (1.4f - 0.4f * Mathf.Clamp01(t * 3f));
        }

        public void ShowChance(bool on, int cost = 0, int wallet = 0)
        {
            chancePanel.SetActive(on);
            if (!on) return;
            chancePanel.transform.SetAsLastSibling();
            chanceYes.localScale = chanceNo.localScale = Vector3.one;
            chanceCost.text = cost.ToString();
            chanceWallet.text = "Tu as " + wallet + " pièces";
        }

        public void SetChanceTimer(float k)
        {
            chanceBar.fillAmount = Mathf.Clamp01(k);
            chanceYes.localScale = Vector3.Lerp(chanceYes.localScale, Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f)), 0.3f);
        }

        // ================= Boutique : améliorer les super objets =================

        GameObject shopPanel;
        Text shopWallet;
        readonly Text[] shopTime = new Text[4], shopPrice = new Text[4];
        readonly Image[][] shopPips = new Image[4][];
        readonly RectTransform[] shopBuy = new RectTransform[4];
        static readonly string[] PowerGlyph = { "A", "B", "S", "x2" };

        void BuildShop()
        {
            var c = Modal("Boutique", "BOUTIQUE", 1560f, out shopPanel);
            var w = new GameObject("Porte-monnaie", typeof(RectTransform));
            w.transform.SetParent(c, false);
            var wr = (RectTransform)w.transform; wr.anchorMin = wr.anchorMax = new Vector2(0.5f, 1f); wr.sizeDelta = new Vector2(400f, 80f); wr.anchoredPosition = new Vector2(0f, -195f);
            CoinIcon(wr, new Vector2(-330f, -40f), 64f);
            shopWallet = T(Label(wr, "", 64, new Color(1f, 0.82f, 0.25f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(300f, 80f), Ink, TextAnchor.MiddleLeft));
            ((RectTransform)shopWallet.transform).pivot = new Vector2(0f, 0.5f);
            Label(c, "Tes super objets durent plus longtemps !", 34, Pale, new Vector2(0.5f, 1f), new Vector2(0f, -258f), new Vector2(900f, 50f), Ink);
            for (int i = 0; i < 4; i++)
            {
                float y = -300f - i * 255f;
                var box = Child(c, "Pouvoir", new Color(0.14f, 0.12f, 0.2f), Vector2.zero, Vector2.zero).rectTransform;
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
                box.sizeDelta = new Vector2(900f, 230f); box.anchoredPosition = new Vector2(0f, y);
                // Pastille de couleur du pouvoir
                var disc = Child(box, "Icône", ZRWorld.PowerColors[i], Vector2.zero, Vector2.zero).rectTransform;
                disc.anchorMin = disc.anchorMax = new Vector2(0f, 0.5f); disc.sizeDelta = new Vector2(120f, 120f); disc.anchoredPosition = new Vector2(90f, 10f);
                T(Label(disc, PowerGlyph[i], 70, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(120f, 120f), Ink));
                var name = T(Label(box, ZRWorld.PowerNames[i], 54, Color.white, new Vector2(0f, 1f), new Vector2(175f, -20f), new Vector2(420f, 64f), Ink, TextAnchor.UpperLeft));
                ((RectTransform)name.transform).pivot = new Vector2(0f, 1f);
                var desc = Label(box, ZRUpgrades.Descriptions[i], 30, Pale, new Vector2(0f, 1f), new Vector2(175f, -84f), new Vector2(460f, 44f), Ink, TextAnchor.UpperLeft);
                ((RectTransform)desc.transform).pivot = new Vector2(0f, 1f);
                // Niveaux (5 cases)
                shopPips[i] = new Image[ZRUpgrades.MaxLevel];
                for (int k = 0; k < ZRUpgrades.MaxLevel; k++)
                {
                    var pip = Child(box, "Niveau", new Color(0f, 0f, 0f, 0.5f), Vector2.zero, Vector2.zero);
                    var prt = pip.rectTransform; prt.anchorMin = prt.anchorMax = new Vector2(0f, 0f); prt.pivot = new Vector2(0f, 0f);
                    prt.sizeDelta = new Vector2(54f, 30f); prt.anchoredPosition = new Vector2(175f + k * 62f, 30f);
                    shopPips[i][k] = pip;
                }
                shopTime[i] = Label(box, "", 32, Color.white, new Vector2(0f, 0f), new Vector2(495f, 30f), new Vector2(200f, 32f), Ink, TextAnchor.LowerLeft);
                ((RectTransform)shopTime[i].transform).pivot = new Vector2(0f, 0f);
                // Bouton d'achat
                shopBuy[i] = Hotspot(Button(box, "", ButtonGreen, new Vector2(1f, 0.5f), new Vector2(-120f, 0f), new Vector2(200f, 150f), 60), "buy" + i);
                CoinIcon(shopBuy[i], new Vector2(-152f, -75f), 46f);
                shopPrice[i] = T(Label(shopBuy[i], "", 52, Color.white, new Vector2(0.5f, 0.5f), new Vector2(25f, 2f), new Vector2(150f, 70f), Ink));
            }
            Hotspot(Button(c, "RETOUR", ButtonGreen, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(480f, 140f), 80), "close");
        }

        public void ShowShop(bool on, int wallet = 0)
        {
            shopPanel.SetActive(on);
            if (!on) return;
            shopPanel.transform.SetAsLastSibling(); fade.transform.SetAsLastSibling();
            shopWallet.text = wallet.ToString();
            for (int i = 0; i < 4; i++)
            {
                int lvl = ZRUpgrades.Level(i);
                for (int k = 0; k < ZRUpgrades.MaxLevel; k++) shopPips[i][k].color = k < lvl ? ZRWorld.PowerColors[i] : new Color(0f, 0f, 0f, 0.5f);
                float now = ZRUpgrades.Duration(i, lvl);
                int cost = ZRUpgrades.NextCost(i);
                shopTime[i].text = cost > 0 ? Mathf.RoundToInt(now) + " s → " + Mathf.RoundToInt(ZRUpgrades.Duration(i, lvl + 1)) + " s" : Mathf.RoundToInt(now) + " s";
                bool afford = cost > 0 && wallet >= cost;
                shopPrice[i].text = cost > 0 ? cost.ToString() : "MAX";
                foreach (var img in shopBuy[i].GetComponentsInChildren<Image>())
                    if (img.name == "Fond") img.color = cost <= 0 ? new Color(0.35f, 0.35f, 0.4f) : afford ? ButtonGreen : new Color(0.4f, 0.4f, 0.45f);
                shopBuy[i].Find("Icône pièce")?.gameObject.SetActive(cost > 0);
                ((RectTransform)shopPrice[i].transform).anchoredPosition = new Vector2(cost > 0 ? 25f : 0f, 2f);
            }
        }

        /// <summary>Met à jour les pièces affichées dans le menu (après un achat).</summary>
        public void SetWallet(int coins) { if (coinTotal != null) coinTotal.text = coins.ToString(); }

        // ================= Paramètres et Missions =================

        struct Hot { public RectTransform rt; public string id; }
        readonly System.Collections.Generic.List<Hot> hot = new System.Collections.Generic.List<Hot>();
        GameObject settings, missionsPanel;
        Text musicValue, sfxValue, sensiValue, missionsDone;
        readonly Text[] mText = new Text[ZRMissions.Slots], mProg = new Text[ZRMissions.Slots], mReward = new Text[ZRMissions.Slots];
        readonly Image[] mBar = new Image[ZRMissions.Slots];

        /// <summary>Identifiant du bouton touché dans un panneau (après UIButton.Panel).</summary>
        public string TappedId { get; private set; }
        public bool PanelOpen => (settings != null && settings.activeSelf) || (missionsPanel != null && missionsPanel.activeSelf) || (shopPanel != null && shopPanel.activeSelf);

        RectTransform Hotspot(RectTransform rt, string id) { hot.Add(new Hot { rt = rt, id = id }); return rt; }
        public void PressHot(string id) { foreach (var h in hot) if (h.id == id) h.rt.localScale = Vector3.one * 0.88f; }

        /// <summary>Roue dentée dessinée en code (icône Paramètres).</summary>
        void Gear(RectTransform parent)
        {
            for (int i = 0; i < 8; i++)
            {
                var tooth = Child(parent, "Dent", Color.white, Vector2.zero, Vector2.zero).rectTransform;
                tooth.anchorMin = tooth.anchorMax = new Vector2(0.5f, 0.5f);
                tooth.sizeDelta = new Vector2(16f, 62f);
                tooth.localRotation = Quaternion.Euler(0f, 0f, i * 22.5f);
            }
            var disc = Child(parent, "Disque", Color.white, Vector2.zero, Vector2.zero).rectTransform;
            disc.anchorMin = disc.anchorMax = new Vector2(0.5f, 0.5f); disc.sizeDelta = new Vector2(46f, 46f);
            var hole = Child(parent, "Trou", Toxic, Vector2.zero, Vector2.zero).rectTransform;
            hole.anchorMin = hole.anchorMax = new Vector2(0.5f, 0.5f); hole.sizeDelta = new Vector2(20f, 20f);
        }

        RectTransform Modal(string name, string title, float height, out GameObject root)
        {
            root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(this.root, false);
            Stretch((RectTransform)root.transform);
            FullImage("Voile", root.transform, new Color(0f, 0f, 0f, 0.72f));
            var cardGo = new GameObject("Carte", typeof(RectTransform));
            cardGo.transform.SetParent(root.transform, false);
            var c = (RectTransform)cardGo.transform;
            c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.sizeDelta = new Vector2(980f, height);
            Child(c, "Bord", Toxic, Vector2.zero, new Vector2(12f, 12f));
            Child(c, "Fond", new Color(0.07f, 0.05f, 0.11f, 0.98f), Vector2.zero, Vector2.zero);
            T(Label(c, title, 104, Toxic, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(940f, 120f), Purple), -2f);
            root.SetActive(false);
            return c;
        }

        RectTransform Small(RectTransform parent, string text, Vector2 anchor, Vector2 pos, string id, Color color)
        {
            var b = Button(parent, text, color, anchor, pos, new Vector2(120f, 110f), 80);
            return Hotspot(b, id);
        }

        void SettingRow(RectTransform c, string title, float y, string id, out Text value)
        {
            T(Label(c, title, 52, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(900f, 64f), Purple));
            Small(c, "-", new Vector2(0.5f, 1f), new Vector2(-280f, y - 130f), id + "-", ButtonPurple);
            Small(c, "+", new Vector2(0.5f, 1f), new Vector2(280f, y - 130f), id + "+", ButtonGreen);
            value = T(Label(c, "", 64, new Color(1f, 0.82f, 0.25f), new Vector2(0.5f, 1f), new Vector2(0f, y - 100f), new Vector2(400f, 80f), Ink));
        }

        void BuildSettings()
        {
            var c = Modal("Paramètres", "PARAMÈTRES", 1360f, out settings);
            Hotspot(Button(c, "COMMENT JOUER ?", ButtonPurple, new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(640f, 130f), 64), "help");
            SettingRow(c, "MUSIQUE", -200f, "music", out musicValue);
            SettingRow(c, "EFFETS SONORES", -450f, "sfx", out sfxValue);
            SettingRow(c, "SENSIBILITÉ DU DOIGT", -700f, "sensi", out sensiValue);
            Hotspot(Button(c, "RETOUR", ButtonGreen, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(480f, 140f), 80), "close");
        }

        static readonly string[] SensiNames = { "FAIBLE", "NORMALE", "FORTE" };

        public void ShowSettings(bool on)
        {
            settings.SetActive(on);
            if (!on) return;
            settings.transform.SetAsLastSibling(); fade.transform.SetAsLastSibling();
            musicValue.text = Mathf.RoundToInt(ZRSfx.MusicVolume * 100f) + " %";
            sfxValue.text = Mathf.RoundToInt(ZRSfx.SfxVolume * 100f) + " %";
            sensiValue.text = SensiNames[ZRInput.Sensitivity];
        }

        void BuildMissions()
        {
            var c = Modal("Missions", "MISSIONS", 1300f, out missionsPanel);
            missionsDone = Label(c, "", 36, Pale, new Vector2(0.5f, 1f), new Vector2(0f, -165f), new Vector2(900f, 50f), Ink);
            for (int i = 0; i < ZRMissions.Slots; i++)
            {
                float y = -240f - i * 260f;
                var box = Child(c, "Mission", new Color(0.14f, 0.12f, 0.2f), Vector2.zero, Vector2.zero).rectTransform;
                box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f); box.pivot = new Vector2(0.5f, 1f);
                box.sizeDelta = new Vector2(900f, 230f); box.anchoredPosition = new Vector2(0f, y);
                mText[i] = Label(box, "", 44, Color.white, new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(620f, 110f), Ink, TextAnchor.UpperLeft);
                mText[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                ((RectTransform)mText[i].transform).pivot = new Vector2(0f, 1f);
                // Récompense
                CoinIcon(box, new Vector2(-190f, -60f), 60f);
                mReward[i] = T(Label(box, "", 56, new Color(1f, 0.82f, 0.25f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(140f, 64f), Ink, TextAnchor.UpperRight));
                ((RectTransform)mReward[i].transform).pivot = new Vector2(1f, 1f);
                // Barre de progression
                var back = Child(box, "Barre", new Color(0f, 0f, 0f, 0.55f), Vector2.zero, Vector2.zero).rectTransform;
                back.anchorMin = back.anchorMax = new Vector2(0f, 0f); back.pivot = new Vector2(0f, 0f);
                back.sizeDelta = new Vector2(640f, 40f); back.anchoredPosition = new Vector2(30f, 30f);
                var fill = Child(back, "Jauge", ButtonGreen, Vector2.zero, new Vector2(-8f, -8f));
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.sprite = null;
                mBar[i] = fill;
                mProg[i] = T(Label(box, "", 40, Color.white, new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(200f, 50f), Ink, TextAnchor.LowerRight));
                ((RectTransform)mProg[i].transform).pivot = new Vector2(1f, 0f);
            }
            Hotspot(Button(c, "RETOUR", ButtonGreen, new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(480f, 140f), 80), "close");
        }

        public void ShowMissions(bool on)
        {
            missionsPanel.SetActive(on);
            if (!on) return;
            missionsPanel.transform.SetAsLastSibling(); fade.transform.SetAsLastSibling();
            missionsDone.text = "Missions accomplies : " + ZRMissions.Completed;
            for (int i = 0; i < ZRMissions.Slots; i++)
            {
                int m = ZRMissions.SlotMission(i);
                var d = ZRMissions.Get(m);
                int p = ZRMissions.SlotProgress(i);
                mText[i].text = ZRMissions.Text(m) + (d.PerRun ? "\n<size=30><color=#B0B0B8>en une seule partie</color></size>" : "");
                mReward[i].text = "+" + d.reward;
                mBar[i].fillAmount = d.goal > 0 ? (float)p / d.goal : 0f;
                mProg[i].text = p + " / " + d.goal;
            }
        }

        /// <summary>Petit chiffre rouge sur la tuile Missions (missions en cours).</summary>
        public void SetMissionBadge(int n) { missionBadge.gameObject.SetActive(n > 0); missionBadgeText.text = n.ToString(); }

        // ================= Super objets : jauges à l'écran =================

        readonly RectTransform[] powerRows = new RectTransform[4];
        readonly Image[] powerFill = new Image[4];

        void BuildPowers()
        {
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Bonus " + ZRWorld.PowerNames[i], typeof(RectTransform));
                go.transform.SetParent(hud.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(330f, 74f);
                Child(rt, "Fond", new Color(0f, 0f, 0f, 0.6f), Vector2.zero, Vector2.zero);
                var fill = Child(rt, "Jauge", ZRWorld.PowerColors[i], Vector2.zero, Vector2.zero);
                var frt = fill.rectTransform;
                frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(0f, 0f);
                frt.offsetMin = new Vector2(10f, 8f); frt.offsetMax = new Vector2(-10f, 20f);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.sprite = null;
                Label(rt, ZRWorld.PowerNames[i], 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(330f, 60f), Ink);
                powerRows[i] = rt; powerFill[i] = fill;
                go.SetActive(false);
            }
        }

        /// <summary>Temps restant de chaque bonus (0 à 1) : aimant, bouclier, super saut, pièces x2.</summary>
        public void SetPowers(float magnet, float shield, float jump, float dbl)
        {
            if (powerRows[0] == null) BuildPowers();
            float[] v = { magnet, shield, jump, dbl };
            float y = -200f;
            for (int i = 0; i < 4; i++)
            {
                bool on = v[i] > 0f;
                if (powerRows[i].gameObject.activeSelf != on) powerRows[i].gameObject.SetActive(on);
                if (!on) continue;
                powerRows[i].anchoredPosition = new Vector2(40f, y);
                powerFill[i].fillAmount = v[i];
                y -= 86f;
            }
        }

        public void PowerToast(string text, Color color)
        {
            toastText.text = text;
            toastText.color = color;
            toastTime = 0f;
            toast.SetActive(true);
        }

        static float BackOut(float t)
        {
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        static void Fade(Text t, float a) { if (t == null) return; var c = t.color; c.a = a; t.color = c; }

        // ================= Construction =================

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        GameObject Panel(string name, Color? dim)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            Stretch((RectTransform)go.transform);
            if (dim.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = dim.Value;
                img.raycastTarget = false;
            }
            go.SetActive(false);
            return go;
        }

        static Image FullImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Image BarImage(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform Logo(Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, float width)
        {
            if (sprite == null) return null;
            var go = new GameObject("Logo", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, width * sprite.rect.height / sprite.rect.width);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(0f, -12f);
            return rt;
        }

        RectTransform Button(Transform parent, string text, Color color, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            var go = new GameObject("Bouton " + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Ombre portée + bord sombre + fond coloré + reflet
            var shadow = Child(rt, "Ombre", new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, -16f), Vector2.zero);
            var border = Child(rt, "Bord", Purple, Vector2.zero, new Vector2(18f, 18f));
            var fill = Child(rt, "Fond", color, Vector2.zero, Vector2.zero);
            var shine = Child(rt, "Reflet", new Color(1f, 1f, 1f, 0.28f), Vector2.zero, Vector2.zero);
            var srt = shine.rectTransform;
            srt.anchorMin = new Vector2(0.06f, 0.55f); srt.anchorMax = new Vector2(0.94f, 0.9f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            shadow.transform.SetAsFirstSibling();

            var label = T(Label(rt, text, (int)(fontSize * 1.12f), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), size, Purple));
            return rt;
        }

        Image Child(RectTransform parent, string name, Color color, Vector2 offset, Vector2 grow)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = offset - grow * 0.5f; rt.offsetMax = offset + grow * 0.5f;
            var img = go.AddComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Rectangle aux coins arrondis, fabriqué en code (pas besoin d'image).</summary>
        static Sprite RoundedSprite()
        {
            const int size = 128, radius = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 pos, Vector2 box, Color outline, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = font; t.text = text; t.fontSize = size; t.fontStyle = font == symbolFont ? FontStyle.Bold : FontStyle.Normal;
            t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = go.AddComponent<Outline>();
            o.effectColor = new Color(outline.r, outline.g, outline.b, 0.85f);
            o.effectDistance = new Vector2(4f, -4f);
            return t;
        }
    }
}
