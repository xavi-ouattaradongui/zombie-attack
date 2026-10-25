using System.Collections.Generic;
using UnityEngine;

namespace ZombieRun
{
    /// <summary>
    /// La horde : plusieurs zombies qui courent derrière le joueur. Ils se rapprochent quand on trébuche,
    /// et l'attrapent au moindre choc.
    /// </summary>
    public class ZRHorde : MonoBehaviour
    {
        class Member { public ZRAnimated z; public Transform t; public Vector2 offset; public float speedVar; }

        readonly List<Member> members = new List<Member>();
        float distance = 4f, target = 4f, stumbleTimer, clock;
        bool catching, menu, intro, introRunning;

        /// <summary>Les zombies sont-ils tout près (après un trébuchement) ?</summary>
        public bool Close => stumbleTimer > 0f;

        public void Build(ZRLibrary lib, int count)
        {
            if (lib.zombies.Length == 0 || lib.zombieAnimator == null) return;
            for (int i = 0; i < count; i++)
            {
                var t = new GameObject("Zombie " + (i + 1)).transform;
                t.SetParent(transform, false);
                int zi = i % lib.zombies.Length;
                var z = ZRAnimated.Create(lib.zombies[zi], lib.zombieAnimator, t, Random.Range(1.7f, 1.9f), zi < lib.zombieTextures.Length ? lib.zombieTextures[zi] : null);
                z.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                float row = i / 3;
                members.Add(new Member
                {
                    z = z, t = t,
                    offset = new Vector2(((i % 3) - 1) * 1.5f + (row > 0 ? 0.75f : 0f) + Random.Range(-0.2f, 0.2f), row * 0.8f + Random.Range(0f, 0.3f)),
                    speedVar = Random.Range(0.9f, 1.15f)
                });
            }
        }

        void LateUpdate()
        {
            foreach (var m in members) { m.z.KeepLooping(); m.z.KeepFeetAbove(0f, Time.deltaTime); }
        }

        /// <summary>Menu : pas de zombies, ils arrivent au moment où on appuie sur JOUER.</summary>
        public void ShowMenu(Vector3 playerPos)
        {
            menu = true; catching = false; intro = false;
            foreach (var m in members) m.t.gameObject.SetActive(false);
        }

        /// <summary>Départ : les zombies surgissent au bout de la rue, derrière le perso, et foncent vers lui.</summary>
        public void StartIntro(Vector3 playerPos, float back = 24f, bool running = true)
        {
            menu = false; catching = false; intro = true; introRunning = running;
            foreach (var m in members)
            {
                m.t.gameObject.SetActive(true);
                m.t.position = new Vector3(playerPos.x + m.offset.x * 1.3f, 0f, playerPos.z - back - m.offset.y * 2.5f);
                m.t.rotation = Quaternion.identity;
                m.z.Reset();
                m.z.Play(running ? "Run" : "Idle", 0f);
                m.z.SetSpeed(m.speedVar);
                m.z.animator.Update(Random.Range(0f, 1f));
            }
        }

        /// <summary>Cinématique : les zombies immobiles entendent un bruit, hurlent et chargent.</summary>
        public void Wake()
        {
            introRunning = true;
            foreach (var m in members) m.z.Play("Scream", 0.1f, true);
            wakeTimer = 0.9f;
        }

        float wakeTimer;

        public void StartChase(float playerZ = 0f)
        {
            menu = false; catching = false;
            float start = 2.4f;
            if (intro && members.Count > 0)
            {
                // On repart de là où les zombies sont arrivés pendant le départ (pas de saut)
                float sum = 0f;
                foreach (var m in members) sum += m.t.position.z + m.offset.y;
                start = Mathf.Max(2.4f, playerZ - sum / members.Count);
            }
            intro = false;
            distance = start; target = 2.4f; stumbleTimer = 3f;
            foreach (var m in members) { m.t.gameObject.SetActive(true); m.z.Reset(); }
        }

        /// <summary>Le joueur a trébuché : la horde revient au contact. Renvoie vrai si c'est le 2e trébuchement (attrapé).</summary>
        public bool Stumble()
        {
            bool caught = stumbleTimer > 0f;
            stumbleTimer = 6f;
            distance = Mathf.Min(distance, 3f);
            return caught;
        }

        /// <summary>Seconde chance : la horde lâche le joueur et repart derrière lui, à bonne distance.</summary>
        public void Release()
        {
            catching = false;
            distance = 9f; target = 22f; stumbleTimer = 0f;
            foreach (var m in members) m.z.Reset();
        }

        public void Catch(Vector3 playerPos)
        {
            catching = true;
            foreach (var m in members)
            {
                if (!m.t.gameObject.activeSelf || m.t.position.z < playerPos.z - 7f)
                    m.t.position = new Vector3(playerPos.x + m.offset.x, 0f, playerPos.z - 6f - m.offset.y);
                m.t.gameObject.SetActive(true);
                m.z.Reset();
                m.z.SetSpeed(1f);
            }
        }

        public void Tick(Vector3 playerPos, float speed, float dt)
        {
            clock += dt;
            if (intro)
            {
                if (!introRunning) { foreach (var m in members) m.z.Play("Idle", 0.2f); return; }
                if (wakeTimer > 0f) { wakeTimer -= dt; return; } // le temps du hurlement
                foreach (var m in members)
                {
                    var p = m.t.position;
                    float stopZ = playerPos.z - 6.5f - m.offset.y;
                    p.z = Mathf.MoveTowards(p.z, stopZ, 7f * m.speedVar * dt);
                    p.x = Mathf.Lerp(p.x, playerPos.x * 0.6f + m.offset.x, Mathf.Clamp01(1.5f * dt));
                    m.t.position = p;
                    m.z.Play("Run", 0.2f);
                }
                return;
            }

            if (menu)
            {
                foreach (var m in members)
                {
                    if (!m.t.gameObject.activeSelf) continue; // menu : zombies cachés
                    m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.LookRotation(playerPos - m.t.position + Vector3.up * 0.01f), dt * 2f);
                    if (Random.value < dt * 0.15f) m.z.Play("Scream", 0.2f, true);
                }
                return;
            }

            if (catching)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    var goal = playerPos + new Vector3(m.offset.x * 0.6f, 0f, -1.1f - m.offset.y * 0.6f);
                    var p = Vector3.MoveTowards(m.t.position, new Vector3(goal.x, 0f, goal.z), 9f * dt);
                    bool arrived = (p - new Vector3(goal.x, 0f, goal.z)).sqrMagnitude < 0.05f;
                    m.t.position = p;
                    var look = playerPos - p; look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) m.t.rotation = Quaternion.LookRotation(look);
                    m.z.Play(arrived ? "Attack" : "Run", 0.15f);
                }
                return;
            }

            if (stumbleTimer > 0f) stumbleTimer -= dt;
            target = stumbleTimer > 0f ? 2.4f : 22f;
            distance = Mathf.MoveTowards(distance, target, (target > distance ? 2.5f : 8f) * dt);

            foreach (var m in members)
            {
                float wobble = Mathf.Sin(clock * 2f * m.speedVar + m.offset.x) * 0.4f;
                var goal = new Vector3(playerPos.x * 0.6f + m.offset.x + wobble, 0f, playerPos.z - distance - m.offset.y);
                var p = m.t.position;
                p.x = Mathf.Lerp(p.x, goal.x, Mathf.Clamp01(3f * dt));
                p.z = goal.z;
                p.y = 0f;
                m.t.position = p;
                m.t.rotation = Quaternion.Slerp(m.t.rotation, Quaternion.identity, dt * 6f);
                bool visible = distance < 20f;
                if (m.t.gameObject.activeSelf != visible) { m.t.gameObject.SetActive(visible); m.z.Reset(); }
                if (!visible) continue;
                m.z.Play("Run", 0.2f);
                m.z.SetSpeed(Mathf.Lerp(0.95f, 1.4f, Mathf.InverseLerp(12f, 30f, speed)) * m.speedVar);
            }
        }
    }
}
