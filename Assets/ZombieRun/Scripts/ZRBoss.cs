using System.Collections.Generic;
using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// Boss de quartier : à chaque nouvelle zone, un zombie géant court devant le joueur.
    /// Il se retourne et frappe le sol : une onde toxique fonce dans un couloir (le couloir clignote en rouge avant).
    /// Il faut changer de couloir (ou sauter par-dessus). Après quelques coups, il s'effondre.
    /// </summary>
    public class ZRBoss : MonoBehaviour
    {
        enum Phase { Off, Enter, Fight, Leave }

        class Wave { public Transform t, mark; public int lane; public float rel, warn; public bool launched; }

        ZRAnimated z;
        Transform body;
        Light glow;
        Material waveMat, markMat;
        readonly List<Wave> waves = new List<Wave>();
        readonly Stack<Wave> pool = new Stack<Wave>();
        Phase phase;
        float timer, ahead, x, yaw, nextAttack;
        int attacksLeft, attackIndex, round;
        bool turned;

        const float WaveSpeed = 26f;      // vitesse de l'onde vers le joueur (en plus de la course)
        const float WarnTime = 1.0f;      // le couloir clignote pendant ce temps avant l'onde

        public bool Active => phase != Phase.Off;
        /// <summary>Le boss vient d'être battu (lu une seule fois).</summary>
        public bool JustDefeated { get; private set; }

        public void Build(ZRLibrary lib)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            waveMat = new Material(sh);
            waveMat.SetColor("_BaseColor", new Color(0.35f, 1f, 0.1f));
            waveMat.EnableKeyword("_EMISSION");
            waveMat.SetColor("_EmissionColor", new Color(0.4f, 1f, 0.1f) * 3f);
            markMat = new Material(sh);
            markMat.SetColor("_BaseColor", new Color(0.8f, 0.05f, 0.05f));
            markMat.EnableKeyword("_EMISSION");
            markMat.SetColor("_EmissionColor", new Color(1f, 0.05f, 0.05f) * 2.5f);

            body = new GameObject("Boss").transform;
            body.SetParent(transform, false);
            if (lib.zombies.Length > 0 && lib.zombieAnimator != null)
            {
                int zi = lib.zombies.Length > 1 ? 1 : 0;
                z = ZRAnimated.Create(lib.zombies[zi], lib.zombieAnimator, body, 3.6f, zi < lib.zombieTextures.Length ? lib.zombieTextures[zi] : null);
                z.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            glow = new GameObject("Lueur du boss").AddComponent<Light>();
            glow.transform.SetParent(body, false);
            glow.transform.localPosition = new Vector3(0f, 2.6f, -1.2f);
            glow.type = LightType.Point; glow.range = 9f; glow.intensity = 4f; glow.color = new Color(0.5f, 1f, 0.2f);
            body.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (z != null && body.gameObject.activeSelf) { z.KeepLooping(); z.KeepFeetAbove(0f, Time.deltaTime); }
        }

        /// <summary>Le boss surgit loin devant. round = combien de boss on a déjà croisés (il frappe plus souvent).</summary>
        public void Begin(float playerZ, int round)
        {
            this.round = round;
            phase = Phase.Enter;
            timer = 0f;
            ahead = 46f; x = 0f; yaw = 180f; turned = true;
            attacksLeft = 5 + Mathf.Min(round, 3);   // 5 coups au premier Colosse, jusqu'à 8 ensuite
            attackIndex = 0;
            JustDefeated = false;
            body.gameObject.SetActive(true);
            body.position = new Vector3(0f, 0f, playerZ + ahead);
            z?.Reset();
            z?.Play("Scream", 0.1f, true);
        }

        /// <summary>Retire le boss et ses ondes (nouvelle partie, menu).</summary>
        public void Stop()
        {
            phase = Phase.Off;
            body.gameObject.SetActive(false);
            ClearWaves();
        }

        /// <summary>Seconde chance : les ondes en cours disparaissent, le boss recule un peu.</summary>
        public void ClearWaves()
        {
            foreach (var w in waves) Release(w);
            waves.Clear();
            nextAttack = Mathf.Max(nextAttack, 1.5f);
        }

        /// <summary>Renvoie vrai si une onde touche le joueur.</summary>
        public bool Tick(Vector3 playerPos, float playerY, float dt, bool canHit)
        {
            JustDefeated = false;
            if (phase == Phase.Off) return false;
            timer += dt;
            bool hit = false;

            switch (phase)
            {
                case Phase.Enter:
                    // Il arrive en rugissant, face au joueur, puis se retourne et court
                    ahead = Mathf.MoveTowards(ahead, 17f, 14f * dt);
                    if (timer > 1.6f) { phase = Phase.Fight; turned = false; nextAttack = 1.2f; z?.Play("Run", 0.2f); }
                    break;

                case Phase.Fight:
                    ahead = Mathf.MoveTowards(ahead, 17f, 6f * dt);
                    nextAttack -= dt;
                    if (nextAttack <= 0f && attacksLeft > 0)
                    {
                        attacksLeft--; attackIndex++;
                        int lane = Mathf.Clamp(Mathf.RoundToInt(playerPos.x / ZRWorld.LaneWidth) + 1, 0, 2);
                        Launch(lane);
                        // Plus tard, deux couloirs à la fois : il ne reste qu'un passage
                        if (round >= 1 && attackIndex % 2 == 0) Launch((lane + Random.Range(1, 3)) % 3);
                        turned = true;
                        nextAttack = Mathf.Lerp(2.4f, 1.7f, Mathf.Clamp01(round / 3f));
                    }
                    // Il se retourne pour frapper, puis repart en courant
                    bool anyWarn = false;
                    foreach (var w in waves) if (!w.launched) anyWarn = true;
                    if (turned && !anyWarn && nextAttack < 1.2f) { turned = false; z?.Play("Run", 0.2f); }
                    if (attacksLeft == 0 && waves.Count == 0 && nextAttack < 0.6f) { phase = Phase.Leave; timer = 0f; z?.Play("Scream", 0.1f, true); }
                    break;

                case Phase.Leave:
                    // Il s'effondre sur le côté et disparaît derrière
                    x = Mathf.MoveTowards(x, 7f, 5f * dt);
                    ahead -= (timer < 1f ? 4f : 30f) * dt;
                    if (timer > 0.9f) { turned = true; }
                    if (ahead < -15f) { Stop(); JustDefeated = true; return false; }
                    break;
            }

            yaw = Mathf.MoveTowards(yaw, turned ? 180f : 0f, 540f * dt);
            body.position = new Vector3(x, 0f, playerPos.z + ahead);
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (z != null) z.SetSpeed(1.1f);
            glow.intensity = 3.5f + Mathf.Sin(Time.time * 7f) * 1f;

            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                float laneX = ZRWorld.LaneX(w.lane);
                if (!w.launched)
                {
                    // Le couloir clignote en rouge entre le joueur et le boss
                    w.warn -= dt;
                    float len = Mathf.Max(1f, ahead - 1f);
                    w.mark.gameObject.SetActive(Mathf.Repeat(w.warn, 0.24f) > 0.1f);
                    w.mark.position = new Vector3(laneX, 0.03f, playerPos.z + len * 0.5f);
                    w.mark.localScale = new Vector3(2.1f, 0.04f, len);
                    if (w.warn <= 0f)
                    {
                        w.launched = true; w.rel = ahead - 1.5f;
                        w.mark.gameObject.SetActive(false);
                        w.t.gameObject.SetActive(true);
                        z?.Play("Attack", 0.05f, true);
                    }
                    continue;
                }
                w.rel -= WaveSpeed * dt;
                float h = 0.55f + Mathf.Sin(Time.time * 20f + i) * 0.1f;
                w.t.position = new Vector3(laneX, h * 0.5f, playerPos.z + w.rel);
                w.t.localScale = new Vector3(2.2f, h, 1.2f);
                if (canHit && Mathf.Abs(w.rel) < 0.9f && Mathf.Abs(playerPos.x - laneX) < 1.1f && playerY < 0.75f) hit = true;
                if (w.rel < -4f) { Release(w); waves.RemoveAt(i); }
            }
            return hit;
        }

        void Launch(int lane)
        {
            var w = pool.Count > 0 ? pool.Pop() : NewWave();
            w.lane = lane; w.warn = WarnTime; w.launched = false;
            w.t.gameObject.SetActive(false);
            w.mark.gameObject.SetActive(true);
            waves.Add(w);
        }

        Wave NewWave()
        {
            var t = ZRBuild.Box("Onde toxique", transform, waveMat, Vector3.zero, Vector3.one).transform;
            var mark = ZRBuild.Box("Alerte couloir", transform, markMat, Vector3.zero, Vector3.one).transform;
            return new Wave { t = t, mark = mark };
        }

        void Release(Wave w)
        {
            w.t.gameObject.SetActive(false);
            w.mark.gameObject.SetActive(false);
            pool.Push(w);
        }
    }
}
