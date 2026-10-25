using System.Collections.Generic;
using UnityEngine;

namespace ZombieRun
{
    /// <summary>Le coureur : 3 couloirs, saut, glissade, trébuchement. 5 personnages Mixamo au choix.</summary>
    public class ZRPlayer : MonoBehaviour
    {
        const float StandHeight = 1.8f;
        const float SlideHeight = 0.85f;
        const float HalfWidth = 0.4f;
        const float JumpVelocity = 11.5f;
        const float Gravity = -30f;
        const float LaneSpeed = 15f;
        const float SlideTime = 0.75f;

        class Slot { public string name, description; public ZRAnimated model; }

        readonly List<Slot> slots = new List<Slot>();
        Transform visual;
        int lane = 1, prevLane = 1;
        float x, y, vy, slideTimer, yaw, tilt, knockback, stumbleShake;
        bool grounded = true;

        public int Index { get; private set; }
        public int Count => slots.Count;
        public string CharacterName => Count > 0 ? slots[Index].name : "";
        public string CharacterDescription => Count > 0 ? slots[Index].description : "";
        public bool Alive { get; private set; } = true;
        public bool FacingCamera { get; set; }
        /// <summary>Animation imposée (départ : « LookBack », puis « Run » sur place). null = normal.</summary>
        public string ForcedState { get; set; }
        /// <summary>Hauteur du sol sous le joueur (toit de voiture), donnée par le monde à chaque image.</summary>
        public float GroundHeight { get; set; }
        public float Y => y;
        /// <summary>Super saut : multiplie la force du saut (1 = normal).</summary>
        public float JumpBoost { get; set; } = 1f;
        /// <summary>Nombre d'atterrissages sur un toit de voiture (missions).</summary>
        public int RoofLandings { get; private set; }
        public float Z => transform.position.z;
        public int Lane => lane;
        float Height => slideTimer > 0f ? SlideHeight : StandHeight;
        ZRAnimated Model => Count > 0 ? slots[Index].model : null;

        public void Build(ZRLibrary lib)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            foreach (var r in lib.runners)
            {
                if (r == null || !r.IsValid) continue;
                var holder = new GameObject(r.displayName).transform;
                holder.SetParent(visual, false);
                var m = ZRAnimated.Create(r.model, lib.runnerAnimator, holder, r.height, r.texture);
                m.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                slots.Add(new Slot { name = r.displayName, description = r.description, model = m });
            }
            SetCharacter(0);
        }

        public void SetCharacter(int i)
        {
            if (Count == 0) return;
            Index = (i % Count + Count) % Count;
            for (int k = 0; k < slots.Count; k++) slots[k].model.root.parent.gameObject.SetActive(k == Index);
            Model.Reset();
        }

        public void ResetPlayer()
        {
            lane = prevLane = 1;
            GroundHeight = 0f;
            x = y = vy = slideTimer = tilt = knockback = stumbleShake = 0f;
            grounded = true;
            Alive = true;
            transform.position = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            Model?.Reset();
        }

        public bool MoveLeft() { if (!Alive || lane <= 0) return false; prevLane = lane; lane--; return true; }
        public bool MoveRight() { if (!Alive || lane >= 2) return false; prevLane = lane; lane++; return true; }

        /// <summary>Annule un changement de couloir (on a heurté le côté d'une voiture).</summary>
        public void BounceBack() { lane = prevLane; stumbleShake = 0.5f; }

        public bool Jump()
        {
            if (!Alive || !grounded) return false;
            vy = JumpVelocity * JumpBoost; grounded = false; slideTimer = 0f;
            return true;
        }

        public bool Slide()
        {
            if (!Alive) return false;
            if (!grounded) vy = Mathf.Min(vy, -22f);
            slideTimer = SlideTime;
            return true;
        }

        public void Tick(float speed, float dt, bool running)
        {
            float prevX = x;
            if (Alive)
            {
                x = Mathf.MoveTowards(x, ZRWorld.LaneX(lane), LaneSpeed * dt);
                if (slideTimer > 0f) slideTimer -= dt;
            }
            float ground = GroundHeight;
            // On quitte le toit d'une voiture (fin du toit ou changement de couloir) : on retombe
            if (grounded && y > ground + 0.05f) { grounded = false; vy = 0f; }
            if (!grounded)
            {
                vy += Gravity * dt; y += vy * dt;
                // Atterrissage sur la route ou sur un toit (petite marge pour les sauts un peu courts)
                if (vy <= 0f && y <= ground && y >= ground - 0.45f) { y = ground; vy = 0f; grounded = true; if (ground > 0.5f) RoofLandings++; }
                else if (y <= 0f) { y = 0f; vy = 0f; grounded = true; }
            }
            float z = transform.position.z;
            if (Alive && running) z += speed * dt;
            if (!Alive && knockback > 0f) { z -= knockback * dt; knockback = Mathf.MoveTowards(knockback, 0f, 14f * dt); }
            transform.position = new Vector3(x, y, z);

            float vx = (x - prevX) / Mathf.Max(dt, 0.0001f);
            // Le perso regarde toujours vers la route : dans le menu, c'est la caméra qui se place devant lui.
            yaw = Mathf.Lerp(yaw, 0f, Mathf.Clamp01(8f * dt));
            tilt = Mathf.Lerp(tilt, -vx * 0.6f, Mathf.Clamp01(12f * dt));
            float shake = stumbleShake > 0f ? Mathf.Sin(Time.time * 60f) * 6f * stumbleShake : 0f;
            stumbleShake = Mathf.MoveTowards(stumbleShake, 0f, dt);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(0f, yaw + shake, tilt), Mathf.Clamp01(20f * dt));

            var m = Model;
            if (m == null) return;
            if (!Alive) m.Play("Fall", 0.08f);
            else if (ForcedState != null) { m.Play(ForcedState, 0.25f); m.SetSpeed(1f); return; }
            else if (!running) m.Play("Idle");
            else if (slideTimer > 0f) m.Play("Slide", 0.1f);
            else if (!grounded) m.Play("Jump", 0.08f);
            else m.Play("Run");
            m.SetSpeed(Alive && running ? Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(12f, 30f, speed)) : 1f);
        }

        void LateUpdate()
        {
            // Après l'animation : les pieds ne doivent jamais passer sous la route.
            Model?.KeepLooping();
            Model?.KeepFeetAbove(transform.position.y, Time.deltaTime);
        }

        public Bounds GetBounds()
        {
            float bottom = y + 0.1f, top = y + Height;
            return new Bounds(new Vector3(x, (bottom + top) * 0.5f, Z), new Vector3(HalfWidth * 2f, top - bottom, 0.8f));
        }

        /// <summary>Seconde chance : le perso se relève et repart.</summary>
        public void Revive()
        {
            Alive = true;
            knockback = 0f; slideTimer = 0f; vy = 0f;
            y = 0f; grounded = true;
            Model?.Reset();
        }

        public void Hit()
        {
            Alive = false;
            slideTimer = 0f;
            knockback = 7f;
        }
    }
}
