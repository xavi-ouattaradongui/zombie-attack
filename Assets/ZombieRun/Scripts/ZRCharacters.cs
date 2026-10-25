using System;
using UnityEngine;

namespace ZombieRun
{
    [Serializable]
    public class ZRCharacterAsset
    {
        public string displayName;
        [TextArea] public string description;
        public GameObject model;
        public Texture2D texture;   // couleurs du perso (secours si le matériau importé n'a pas sa texture)
        public float height = 1.7f;
        public bool IsValid => model != null;
    }

    /// <summary>Tous les modèles du jeu, rangés par le menu Zombie Run (rien à remplir à la main).</summary>
    [Serializable]
    public class ZRLibrary
    {
        [Header("Écrans")]
        public Sprite splash;
        public Sprite logo;
        [Header("Écran GAME OVER (découpés de la maquette)")]
        public Sprite overTitle, overPanel, overRetry;
        [Header("Menu (découpés de la maquette)")]
        public Sprite playButton, arrowLeft, arrowRight;
        [Tooltip("Toutes les images découpées des maquettes (tuiles, vignettes, pièce...)")]
        public Sprite[] art;
        public Font titleFont, bodyFont;
        public AudioClip runMusic, menuMusic;

        [Header("Personnages")]
        public ZRCharacterAsset[] runners = new ZRCharacterAsset[0];
        public RuntimeAnimatorController runnerAnimator;
        public GameObject[] zombies = new GameObject[0];
        public Texture2D[] zombieTextures = new Texture2D[0];
        public RuntimeAnimatorController zombieAnimator;

        [Header("Route et décor")]
        public GameObject road;
        public GameObject[] shops = new GameObject[0];
        public GameObject[] towers = new GameObject[0];
        public GameObject[] houses = new GameObject[0];
        public GameObject[] trees = new GameObject[0];
        public GameObject[] lamps = new GameObject[0];
        public GameObject pole, trafficLight, fence, dumpster;
        public GameObject[] signs = new GameObject[0];

        [Header("Obstacles")]
        public GameObject[] cars = new GameObject[0];
        public GameObject[] bigVehicles = new GameObject[0];
        public GameObject police;
        public GameObject barrier, box, tire, cone, constructionLight, door;

        public bool HasEnvironment => road != null && shops.Length > 0 && cars.Length > 0;
    }

    /// <summary>Un personnage Mixamo animé. Les noms d'états doivent exister dans son Animator Controller.</summary>
    public class ZRAnimated
    {
        public Transform root;
        public Animator animator;
        string current;
        Transform[] toes;
        float baseY, extraY, sole;

        public static ZRAnimated Create(GameObject model, RuntimeAnimatorController controller, Transform parent, float height, Texture2D texture = null)
        {
            var go = UnityEngine.Object.Instantiate(model, parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (texture != null) ApplyTexture(renderers, texture);
            foreach (var r in renderers)
                if (r is SkinnedMeshRenderer smr0) smr0.updateWhenOffscreen = true;

            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.Update(0f); // pose de départ (attente) pour mesurer le vrai corps

            // Mesure du corps : on utilise les os Mixamo (haut du crâne « HeadTop_End » et orteils), c'est le plus fiable.
            float minY = float.MaxValue, maxY = float.MinValue;
            Transform headTop = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (n.EndsWith("HeadTop_End")) headTop = t;
                if (n.Contains("Toe") || n.Contains("Foot")) minY = Mathf.Min(minY, t.position.y);
            }
            if (headTop != null && minY < float.MaxValue && headTop.position.y > minY + 0.01f)
            {
                maxY = headTop.position.y;
                minY -= (maxY - minY) * 0.02f; // les os des orteils sont juste au-dessus de la semelle
            }
            else
            {
                minY = float.MaxValue;
            var baked = new Mesh();
            foreach (var r in renderers)
            {
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    smr.BakeMesh(baked, true);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    var verts = baked.vertices;
                    for (int i = 0; i < verts.Length; i += 3)
                    {
                        float y = m.MultiplyPoint3x4(verts[i]).y;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                else if (r is MeshRenderer)
                {
                    minY = Mathf.Min(minY, r.bounds.min.y);
                    maxY = Mathf.Max(maxY, r.bounds.max.y);
                }
            }
            UnityEngine.Object.Destroy(baked);
            }
            if (maxY > minY + 0.01f)
            {
                float k = height / (maxY - minY);
                go.transform.localScale = Vector3.one * k;
                float feet = (minY - parent.position.y) * k;
                go.transform.localPosition = new Vector3(0f, -feet, 0f);
            }
            var toeList = new System.Collections.Generic.List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("Toe")) toeList.Add(t);
            return new ZRAnimated
            {
                root = go.transform, animator = anim, toes = toeList.ToArray(),
                baseY = go.transform.localPosition.y, sole = height * 0.02f
            };
        }

        /// <summary>
        /// À appeler après l'animation (LateUpdate) : remonte le perso si ses pieds passent sous le sol
        /// (certaines animations Mixamo, comme le saut ou la glissade, descendent trop bas).
        /// </summary>
        public void KeepFeetAbove(float groundY, float dt)
        {
            if (toes == null || toes.Length == 0 || !root.gameObject.activeInHierarchy) return;
            float min = float.MaxValue;
            foreach (var t in toes) if (t.position.y < min) min = t.position.y;
            float below = groundY - (min - sole);          // > 0 : les pieds sont dans le sol
            float target = Mathf.Max(0f, extraY + below);  // on ne fait que remonter, jamais enfoncer
            extraY = target > extraY ? target : Mathf.MoveTowards(extraY, target, dt * 3f);
            var p = root.localPosition;
            p.y = baseY + extraY;
            root.localPosition = p;
        }

        /// <summary>Remet la texture de couleur sur les matériaux qui l'ont perdue à l'import.</summary>
        static void ApplyTexture(Renderer[] renderers, Texture2D texture)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    bool hasTex = m != null && ((m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) || (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null));
                    if (hasTex) continue;
                    var nm = new Material(shader != null ? shader : Shader.Find("Standard")) { name = (m != null ? m.name : "Mat") + " (couleur)" };
                    nm.SetTexture("_BaseMap", texture);
                    nm.SetTexture("_MainTex", texture);
                    nm.SetColor("_BaseColor", Color.white);
                    nm.SetFloat("_Smoothness", 0.3f);
                    mats[i] = nm;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public void Play(string state, float fade = 0.15f, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (state == current && !restart) return;
            current = state;
            animator.CrossFadeInFixedTime(state, fade, 0, 0f);
        }

        static readonly int[] LoopHashes =
        {
            Animator.StringToHash("Base Layer.Run"), Animator.StringToHash("Base Layer.Idle"), Animator.StringToHash("Base Layer.Attack")
        };

        /// <summary>
        /// Sécurité : si une animation qui doit tourner en boucle (course, attente, attaque) arrive au bout,
        /// on la relance sans coupure, même si le fichier n'a pas été importé en mode boucle.
        /// </summary>
        public void KeepLooping()
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.IsInTransition(0)) return;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.loop || info.normalizedTime < 0.97f) return;
            foreach (var h in LoopHashes)
                if (info.fullPathHash == h) { animator.Play(h, 0, info.normalizedTime % 1f); return; }
        }

        public void SetSpeed(float s) { if (animator != null) animator.speed = s; }
        public void Reset() => current = null;
        public void SetActive(bool on) => root.gameObject.SetActive(on);
    }
}
