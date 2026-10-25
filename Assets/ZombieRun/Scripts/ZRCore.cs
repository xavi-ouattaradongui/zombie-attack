using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ZombieRun
{
    public enum Swipe { None, Left, Right, Up, Down, Tap }

    /// <summary>Clavier (QWERTY + AZERTY), souris et swipe tactile.</summary>
    public static class ZRInput
    {
        static Vector2 start;
        static bool tracking;
        public static Vector2 TapPosition { get; private set; }

        public static Swipe Read()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q)) return Swipe.Left;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return Swipe.Right;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space)) return Swipe.Up;
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) return Swipe.Down;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TapPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                return Swipe.Tap;
            }

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        start = t.position; tracking = true; break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (tracking && (t.position - start).magnitude > Threshold()) { tracking = false; return Direction(t.position - start); }
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (tracking)
                        {
                            tracking = false;
                            var d = t.position - start;
                            if (d.magnitude > Threshold()) return Direction(d);
                            TapPosition = t.position;
                            return Swipe.Tap;
                        }
                        break;
                }
                return Swipe.None;
            }

            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) { start = mouse; tracking = true; }
            else if (tracking && Input.GetMouseButton(0))
            {
                if ((mouse - start).magnitude > Threshold()) { tracking = false; return Direction(mouse - start); }
            }
            else if (tracking && Input.GetMouseButtonUp(0))
            {
                tracking = false;
                var d = mouse - start;
                if (d.magnitude > Threshold()) return Direction(d);
                TapPosition = mouse;
                return Swipe.Tap;
            }
            return Swipe.None;
        }

        /// <summary>Sensibilité du doigt (Paramètres) : 0 faible, 1 normale, 2 forte.</summary>
        public static int Sensitivity { get => PlayerPrefs.GetInt("zr_sensi", 1); set { PlayerPrefs.SetInt("zr_sensi", Mathf.Clamp(value, 0, 2)); PlayerPrefs.Save(); } }
        static readonly float[] SensiFactor = { 1.45f, 1f, 0.65f };

        static float Threshold() => Mathf.Max(20f, (Screen.dpi > 0f ? Screen.dpi : 160f) * 0.22f * SensiFactor[Sensitivity]);

        static Swipe Direction(Vector2 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0f ? Swipe.Right : Swipe.Left;
            return d.y > 0f ? Swipe.Up : Swipe.Down;
        }
    }

    /// <summary>Comment un modèle Kenney doit être mis à l'échelle et orienté.</summary>
    public enum Fit { Width, Height, Length }

    /// <summary>
    /// Réserve d'objets réutilisables. Chaque modèle est mesuré une fois, puis mis à la bonne taille
    /// (les packs Kenney n'ont pas tous la même échelle), posé au sol et centré.
    /// </summary>
    public class ZRPool
    {
        struct Info
        {
            public float scale;
            public Vector3 offset;   // décalage local (après échelle) pour centrer en X/Z et poser au sol
            public Vector3 size;     // taille finale en mètres
            public bool turn;        // tourné de 90° pour que le côté long soit en travers
        }

        readonly Transform root, hidden;
        readonly Dictionary<(GameObject, Fit, float, bool), Info> infos = new Dictionary<(GameObject, Fit, float, bool), Info>();
        readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> owner = new Dictionary<GameObject, GameObject>();

        public ZRPool(Transform parent)
        {
            root = new GameObject("Pool").transform;
            root.SetParent(parent, false);
            hidden = new GameObject("Hidden").transform;
            hidden.SetParent(root, false);
            hidden.gameObject.SetActive(false);
        }

        /// <summary>Taille finale (mètres) d'un modèle avec ces réglages.</summary>
        public Vector3 SizeOf(GameObject prefab, Fit fit, float target, bool crosswise = false) => GetInfo(prefab, fit, target, crosswise).size;

        Info GetInfo(GameObject prefab, Fit fit, float target, bool crosswise)
        {
            var key = (prefab, fit, target, crosswise);
            if (infos.TryGetValue(key, out var info)) return info;

            var probe = Object.Instantiate(prefab);
            probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            probe.transform.localScale = Vector3.one;
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var r in probe.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(probe);
            if (first) b = new Bounds(Vector3.zero, Vector3.one);

            bool turn = crosswise && b.size.z > b.size.x;
            float w = turn ? b.size.z : b.size.x;
            float l = turn ? b.size.x : b.size.z;
            float measured = fit == Fit.Width ? w : fit == Fit.Height ? b.size.y : l;
            float s = target / Mathf.Max(measured, 0.0001f);
            var c = b.center;
            var offset = new Vector3(-c.x, -b.min.y, -c.z) * s;
            if (turn) offset = new Vector3(offset.z, offset.y, -offset.x); // suit la rotation de 90°
            info = new Info
            {
                scale = s,
                offset = offset,
                size = new Vector3(w * s, b.size.y * s, l * s),
                turn = turn
            };
            infos[key] = info;
            return info;
        }

        public GameObject Spawn(GameObject prefab, Vector3 pos, float yaw, Fit fit, float target, bool crosswise = false, Transform parent = null)
        {
            var info = GetInfo(prefab, fit, target, crosswise);
            if (!free.TryGetValue(prefab, out var stack)) { stack = new Stack<GameObject>(); free[prefab] = stack; }
            GameObject go = stack.Count > 0 ? stack.Pop() : null;
            if (go == null)
            {
                go = Object.Instantiate(prefab);
                owner[go] = prefab;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off; // ombres coupées sur le décor : beaucoup plus fluide
                    r.receiveShadows = true;
                }
            }
            go.transform.SetParent(parent != null ? parent : root, false);
            var rot = Quaternion.Euler(0f, yaw + (info.turn ? 90f : 0f), 0f);
            go.transform.localScale = Vector3.one * info.scale;
            go.transform.SetPositionAndRotation(pos + Quaternion.Euler(0f, yaw, 0f) * info.offset, rot);
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            if (!owner.TryGetValue(go, out var prefab)) { Object.Destroy(go); return; }
            go.SetActive(false);
            go.transform.SetParent(hidden, false);
            free[prefab].Push(go);
        }
    }

    /// <summary>Quelques matériaux créés par code (route de secours, rayures de danger, lumières...).</summary>
    [Serializable]
    public class ZRMaterials
    {
        public Material asphalt, sidewalk, hazard, darkMetal, headlight, taillight, lampGlow, fire, blood, sirenRed, sirenBlue;

        public bool IsComplete => asphalt && sidewalk && hazard && darkMetal && headlight && taillight && lampGlow && fire && blood && sirenRed && sirenBlue;

        public void Fill(Action<Object, string> save = null)
        {
            var asphaltTex = Noise("ZRAsphalt", new Color(0.13f, 0.13f, 0.14f), 0.25f);
            var sidewalkTex = Tiles("ZRSidewalk");
            var hazardTex = Stripes("ZRHazard");
            save?.Invoke(asphaltTex, asphaltTex.name);
            save?.Invoke(sidewalkTex, sidewalkTex.name);
            save?.Invoke(hazardTex, hazardTex.name);

            asphalt = Lit("ZR Asphalt", Color.white, asphaltTex, new Vector2(1f, 8f), 0.35f);
            sidewalk = Lit("ZR Sidewalk", new Color(0.55f, 0.55f, 0.56f), sidewalkTex, new Vector2(2f, 16f), 0.15f);
            hazard = Lit("ZR Hazard", Color.white, hazardTex, new Vector2(3f, 1f), 0.4f);
            darkMetal = Lit("ZR Dark Metal", new Color(0.12f, 0.12f, 0.13f), null, Vector2.one, 0.6f, 0.7f);
            headlight = Lit("ZR Headlight", Color.white, null, Vector2.one, 0.9f, 0f, new Color(1f, 0.95f, 0.8f), 4f);
            taillight = Lit("ZR Taillight", new Color(0.5f, 0f, 0f), null, Vector2.one, 0.9f, 0f, new Color(1f, 0.05f, 0.05f), 3f);
            lampGlow = Lit("ZR Lamp Glow", new Color(1f, 0.85f, 0.6f), null, Vector2.one, 0.5f, 0f, new Color(1f, 0.75f, 0.4f), 3f);
            fire = Lit("ZR Fire", new Color(1f, 0.45f, 0.05f), null, Vector2.one, 0.2f, 0f, new Color(1f, 0.4f, 0.05f), 6f);
            blood = Lit("ZR Stain", new Color(0.18f, 0.02f, 0.02f), null, Vector2.one, 0.7f);
            sirenRed = Lit("ZR Siren Red", new Color(0.6f, 0f, 0f), null, Vector2.one, 0.8f, 0f, new Color(1f, 0f, 0f), 6f);
            sirenBlue = Lit("ZR Siren Blue", new Color(0f, 0f, 0.6f), null, Vector2.one, 0.8f, 0f, new Color(0.1f, 0.3f, 1f), 6f);
            if (save != null)
                foreach (var m in new[] { asphalt, sidewalk, hazard, darkMetal, headlight, taillight, lampGlow, fire, blood, sirenRed, sirenBlue })
                    save(m, m.name);
        }

        static Material Lit(string name, Color color, Texture2D tex, Vector2 tiling, float smooth, float metal = 0f, Color emission = default, float intensity = 0f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Standard")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
                m.SetTextureScale("_BaseMap", tiling); m.SetTextureScale("_MainTex", tiling);
            }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (intensity > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static Texture2D NewTex(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, true)
        { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };

        static Texture2D Noise(string name, Color baseColor, float amount)
        {
            const int s = 256;
            var t = NewTex(name, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s;
                    float n = 1f - amount * 0.5f + Hash(x, y, 1) * amount * 0.6f + Hash(x / 16, y / 16, 2) * amount * 0.4f;
                    var c = baseColor * n;
                    // lignes de séparation des voies (pointillés)
                    if ((Mathf.Abs(u - 1f / 3f) < 0.01f || Mathf.Abs(u - 2f / 3f) < 0.01f) && (y / 32) % 2 == 0) c = new Color(0.75f, 0.72f, 0.6f);
                    // fissures
                    if (Hash(x / 3, y / 3, 7) > 0.995f) c *= 0.4f;
                    c.a = 1f;
                    px[y * s + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        static Texture2D Tiles(string name)
        {
            const int s = 128;
            var t = NewTex(name, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool joint = x % 32 < 2 || y % 32 < 2;
                    float n = joint ? 0.55f : 0.85f + Hash(x, y, 3) * 0.15f;
                    px[y * s + x] = new Color(n, n, n, 1f);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        static Texture2D Stripes(string name)
        {
            const int s = 64;
            var t = NewTex(name, s);
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    px[y * s + x] = ((x + y) / 16) % 2 == 0 ? new Color(1f, 0.75f, 0.05f) : new Color(0.08f, 0.08f, 0.08f);
            t.SetPixels(px); t.Apply(true);
            return t;
        }
    }

    public static class ZRBuild
    {
        static Mesh cube;
        public static Mesh Cube
        {
            get
            {
                if (cube != null) return cube;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = go.GetComponent<MeshFilter>().sharedMesh;
                if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
                return cube;
            }
        }

        public static GameObject Box(string name, Transform parent, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = Cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
