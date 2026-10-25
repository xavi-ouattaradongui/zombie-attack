using UnityEngine;

namespace ZombieRun
{
    /// <summary>Sons générés par code : pièces, saut, glissade, grognements de zombies, ambiance.</summary>
    public class ZRSfx : MonoBehaviour
    {
        AudioSource fx, ambience;
        AudioSource musicA, musicB;   // deux sources pour passer d'une musique à l'autre en fondu
        float volA, volB;

        /// <summary>Volumes réglés dans les Paramètres (0 à 1), sauvegardés.</summary>
        public static float MusicVolume { get => PlayerPrefs.GetFloat("zr_vol_music", 0.8f); set { PlayerPrefs.SetFloat("zr_vol_music", Mathf.Clamp01(value)); PlayerPrefs.Save(); } }
        public static float SfxVolume { get => PlayerPrefs.GetFloat("zr_vol_sfx", 1f); set { PlayerPrefs.SetFloat("zr_vol_sfx", Mathf.Clamp01(value)); PlayerPrefs.Save(); } }
        float musicScale = 0.8f;

        /// <summary>À appeler quand les réglages changent.</summary>
        public void ApplyVolumes() { musicScale = MusicVolume; fx.volume = SfxVolume; }
        AudioClip coin, jump, slide, lane, hit, start, select, growl, scream, wind, staticNoise, siren, power, shieldBreak;

        void Awake()
        {
            fx = gameObject.AddComponent<AudioSource>();
            fx.playOnAwake = false;
            ambience = gameObject.AddComponent<AudioSource>();
            ambience.loop = true;
            ambience.volume = 0.25f;
            coin = Tone("coin", 0.1f, 1300f, 1900f, 0.18f, 0f);
            jump = Tone("jump", 0.18f, 260f, 640f, 0.18f, 0f);
            slide = Tone("slide", 0.25f, 400f, 140f, 0.15f, 0.7f);
            lane = Tone("lane", 0.06f, 500f, 620f, 0.07f, 0.2f);
            hit = Tone("hit", 0.5f, 180f, 40f, 0.5f, 0.8f);
            start = Tone("start", 0.4f, 300f, 900f, 0.18f, 0f);
            select = Tone("select", 0.08f, 700f, 900f, 0.12f, 0f);
            growl = Growl("growl", 1.2f, 70f, 0.35f);
            scream = Growl("scream", 0.9f, 140f, 0.3f);
            staticNoise = Static("static", 0.45f);
            siren = Siren("siren", 4.5f);
            power = Tone("power", 0.45f, 500f, 1500f, 0.22f, 0f);
            shieldBreak = Tone("shield", 0.5f, 1200f, 200f, 0.3f, 0.5f);
            musicA = gameObject.AddComponent<AudioSource>(); musicA.loop = true; musicA.playOnAwake = false; musicA.volume = 0f;
            musicB = gameObject.AddComponent<AudioSource>(); musicB.loop = true; musicB.playOnAwake = false; musicB.volume = 0f;
            ApplyVolumes();
        }

        /// <summary>Change de musique en fondu (même morceau = on ajuste seulement le volume). null = silence.</summary>
        public void Music(AudioClip clip, float volume)
        {
            if (clip != null && musicA.clip == clip) { volA = volume; return; }
            // l'ancienne musique passe en B et s'éteint, la nouvelle démarre en A
            (musicA, musicB) = (musicB, musicA);
            volB = 0f;
            volA = clip != null ? volume : 0f;
            if (clip != null)
            {
                musicA.clip = clip;
                musicA.volume = 0f;
                musicA.Play();
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            musicA.volume = Mathf.MoveTowards(musicA.volume, volA * musicScale, dt * 0.8f);
            musicB.volume = Mathf.MoveTowards(musicB.volume, volB, dt * 0.8f);
            if (musicB.isPlaying && musicB.volume <= 0f) musicB.Stop();
        }

        public void Coin() => fx.PlayOneShot(coin);
        public void Jump() => fx.PlayOneShot(jump);
        public void Slide() => fx.PlayOneShot(slide);
        public void Lane() => fx.PlayOneShot(lane);
        public void Hit() => fx.PlayOneShot(hit);
        public void StartRun() { fx.PlayOneShot(start); fx.PlayOneShot(growl, 0.8f); }
        public void Select() => fx.PlayOneShot(select);
        public void Growl() => fx.PlayOneShot(growl, 0.7f);
        public void Scream() => fx.PlayOneShot(scream, 0.6f);
        public void ScreamFar() => fx.PlayOneShot(scream, 0.25f);
        /// <summary>Grésillement de radio (transmissions, indices).</summary>
        public void Static() => fx.PlayOneShot(staticNoise, 0.5f);
        /// <summary>Sirène lointaine (intro).</summary>
        public void Siren() => fx.PlayOneShot(siren, 0.45f);
        public void PowerUp() => fx.PlayOneShot(power);
        public void ShieldBreak() => fx.PlayOneShot(shieldBreak);

        const int Rate = 44100;

        static AudioClip Tone(string name, float dur, float f0, float f1, float vol, float noise)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var d = new float[n];
            var rng = new System.Random(3);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / Rate;
                float s = Mathf.Sin(ph) * 0.7f + Mathf.Sin(ph * 2f) * 0.2f;
                s = Mathf.Lerp(s, (float)(rng.NextDouble() * 2 - 1), noise);
                d[i] = s * Mathf.Exp(-t * 4f) * (1f - t) * vol;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Growl(string name, float dur, float f, float vol)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var d = new float[n];
            var rng = new System.Random(9);
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float freq = f * (1f + 0.25f * Mathf.Sin(t * 18f) + 0.1f * Mathf.Sin(t * 53f));
                ph += 2f * Mathf.PI * freq / Rate;
                float saw = (ph / Mathf.PI) % 2f - 1f;
                lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2 - 1), 0.08f);
                float env = Mathf.Sin(Mathf.PI * t);
                d[i] = (saw * 0.6f + lp * 1.4f) * env * vol;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Static(string name, float dur)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var d = new float[n];
            var rng = new System.Random(11);
            float hp = 0f, prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float w = (float)(rng.NextDouble() * 2 - 1);
                hp = 0.85f * (hp + w - prev); prev = w;          // bruit aigu, façon radio
                float crackle = rng.NextDouble() < 0.002 ? 1f : 0f;
                float gate = 0.6f + 0.4f * Mathf.Sin(t * 90f);
                d[i] = (hp * 0.5f * gate + crackle) * Mathf.Min(1f, (1f - t) * 4f) * Mathf.Min(1f, t * 30f) * 0.6f;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Siren(string name, float dur)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var d = new float[n];
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n, sec = (float)i / Rate;
                float f = 620f + 260f * Mathf.Sin(sec * Mathf.PI * 0.9f);  // montée / descente lente
                ph += 2f * Mathf.PI * f / Rate;
                float s = Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2f) * 0.15f;
                d[i] = s * Mathf.Sin(Mathf.PI * t) * 0.35f;              // lointaine : fondu entrée / sortie
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip Wind(string name, float dur)
        {
            int n = Mathf.CeilToInt(Rate * dur);
            var d = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                lp = Mathf.Lerp(lp, (float)(rng.NextDouble() * 2 - 1), 0.02f + 0.015f * Mathf.Sin(t * Mathf.PI * 4f));
                float loopFade = Mathf.Min(1f, Mathf.Min(t, 1f - t) * 20f);
                d[i] = lp * 0.6f * loopFade;
            }
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
