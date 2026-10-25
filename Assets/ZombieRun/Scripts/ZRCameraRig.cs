using UnityEngine;

namespace ZombieRun
{
    /// <summary>Caméra : de face dans le menu, elle tourne autour du perso au départ, puis reste derrière lui pendant la course.</summary>
    public class ZRCameraRig : MonoBehaviour
    {
        Transform target;
        Camera cam;
        float shake, blend = 1f;

        /// <summary>Plans de cinématique : la caméra va où on lui dit (blend = menu, pour repartir de face).</summary>
        public bool cinematic;
        public Vector3 cinePos, cineLook;

        public void Init(Transform t, Camera c) { target = t; cam = c; }

        /// <summary>Revient tout de suite derrière le perso (RÉESSAYER depuis l'écran de fin, vu de face).</summary>
        public void SnapBehind() { blend = 0f; }
        public void Shake(float a) => shake = Mathf.Max(shake, a);

        public void Tick(float speed01, float dt, bool menu)
        {
            if (target == null || cam == null) return;
            if (cinematic)
            {
                blend = 1f;
                transform.position = Vector3.Lerp(transform.position, cinePos, 1f - Mathf.Exp(-4f * dt));
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(cineLook - transform.position), 1f - Mathf.Exp(-5f * dt));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, cam.aspect < 1f ? 70f : 55f, Mathf.Clamp01(3f * dt));
                if (shake > 0f) { transform.position += Random.insideUnitSphere * shake; shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f); }
                return;
            }
            blend = Mathf.MoveTowards(blend, menu ? 1f : 0f, dt * 1.25f);
            float b = blend * blend * (3f - 2f * blend);
            bool portrait = cam.aspect < 1f;
            var tp = target.position;
            var basePos = new Vector3(tp.x, 0f, tp.z);

            // Course (b = 0) : derrière le perso, angle façon Subway Surfers.
            // Menu (b = 1) : devant lui, de face, la rue (et les zombies) derrière lui.
            // Entre les deux, la caméra tourne autour du perso.
            var runRel = new Vector3(-0.5f * tp.x, 4f + tp.y * 0.3f, -5.85f);
            var runLookRel = new Vector3(-0.4f * tp.x, tp.y * 0.2f, 9.8f);
            // Menu : perso bien au centre et en grand, entre la barre du haut et les vignettes
            var menuRel = portrait ? new Vector3(0f, 1.0f, -3.4f) : new Vector3(0.6f, 1.3f, -4.2f);
            var menuLookRel = portrait ? new Vector3(0f, 0f, 0.73f) : new Vector3(0f, 0.7f, 1.5f);
            var turn = Quaternion.Euler(0f, 180f * b, 0f);
            var desired = basePos + turn * Vector3.Lerp(runRel, menuRel, b);
            var look = basePos + turn * Vector3.Lerp(runLookRel, menuLookRel, b);

            var p = transform.position;
            float k = blend > 0f && blend < 1f ? 1f : 0f; // pendant le virage, on suit exactement l'orbite
            p.x = Mathf.Lerp(p.x, desired.x, k > 0f ? 1f : Mathf.Clamp01(8f * dt));
            p.y = Mathf.Lerp(p.y, desired.y, k > 0f ? 1f : Mathf.Clamp01(6f * dt));
            p.z = desired.z;
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(look - p);
            if (shake > 0f)
            {
                transform.position += Random.insideUnitSphere * shake;
                shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
            }
            float runFov = (portrait ? 58f : 50f) + speed01 * 6f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Mathf.Lerp(runFov, portrait ? 68f : 52f, b), Mathf.Clamp01(4f * dt));
        }
    }
}
