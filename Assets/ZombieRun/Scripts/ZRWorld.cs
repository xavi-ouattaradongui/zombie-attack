using System.Collections.Generic;
using UnityEngine;

namespace ZombieRun
{
    public enum Kind { Jump, Wall, Slide, Oncoming }

    /// <summary>
    /// La ville infinie : route, trottoirs, immeubles Kenney, lampadaires, voitures abandonnées,
    /// zombies sur les côtés, obstacles et pièces. Tout est réutilisé (pool) pour rester fluide.
    /// </summary>
    public class ZRWorld : MonoBehaviour
    {
        public const float LaneWidth = 2.5f;
        public const float RoadHalf = 4f;
        public const float SidewalkWidth = 4.5f;
        public const float ChunkLength = 40f;
        const int ChunkCount = 7;
        const float SpawnAhead = 190f;
        const float OncomingSpeed = 9f;

        class Obstacle
        {
            public Kind kind;
            public GameObject go;          // modèle Kenney (géré par le pool) ou objet maison
            public bool pooled;
            public Vector3 center, size;   // boîte de collision (centre relatif à root)
            public Transform root;
            public float z;
            public List<GameObject> extras;
            public int variant;            // modèle de barrière à glisser
            public bool walkable;          // petite voiture : on peut sauter dessus et courir sur le toit
            public float top;              // hauteur du toit
            public ZRAnimated zombie;      // zombie qui court vers nous (au lieu d'une voiture)
            public float speed = OncomingSpeed;
        }

        class Coin { public GameObject go; public Vector3 pos; public bool taken; }

        class Chunk
        {
            public Transform root;
            public float z;
            public readonly List<GameObject> items = new List<GameObject>();
            public Light lamp;
            public float flickerSeed;
            public int biome;
            public GameObject industrial, port;                 // décors fabriqués en code (zone industrielle, port)
            public readonly List<Transform> warehouses = new List<Transform>();
            public readonly List<Transform> chimneys = new List<Transform>();
            public readonly List<Renderer> containers = new List<Renderer>();
            public Transform crane;
        }

        class Ambient
        {
            public ZRAnimated z;
            public bool active, screamed;
            public float worldZ;
        }

        ZRLibrary lib;
        ZRMaterials mats;
        ZRPool pool;
        Transform ground;
        readonly List<Chunk> chunks = new List<Chunk>();
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        readonly List<Coin> coins = new List<Coin>();
        readonly Stack<GameObject> coinPool = new Stack<GameObject>();
        readonly Dictionary<int, Stack<GameObject>> gatePools = new Dictionary<int, Stack<GameObject>>();
        const int GateVariants = 4;
        readonly List<Ambient> ambients = new List<Ambient>();
        Mesh coinMesh;
        Material coinMat;
        float nextRowZ, clock, playerZ;

        public static float LaneX(int lane) => (lane - 1) * LaneWidth;

        // ========================= Quartiers (le décor change avec la distance) =========================

        public const float BiomeLength = 1300f;
        public static readonly string[] BiomeNames = { "CENTRE-VILLE", "QUARTIER DES SAULES", "ZONE INDUSTRIELLE NORD", "PORT EST" };
        public static readonly string[] BiomeLines = { "Les rues sont mortes. Cours.", "Pavillons vides, jardins piétinés.", "Brouillard toxique : ne respire pas trop.", "Les grilles du port. La horde est partout." };

        /// <summary>Quartier à une distance donnée : centre-ville, banlieue, zone industrielle, port, puis on recommence.</summary>
        public static int BiomeAt(float z) => z < 0f ? 0 : Mathf.FloorToInt(z / BiomeLength) % BiomeNames.Length;

        // Ambiance de chaque quartier : brouillard, lumière ambiante, couleur des lampadaires
        static readonly Color[] FogTint = { new Color(0.2f, 0.24f, 0.27f), new Color(0.2f, 0.17f, 0.3f), new Color(0.2f, 0.3f, 0.14f), new Color(0.32f, 0.12f, 0.1f) };
        static readonly float[] FogDensity = { 0.009f, 0.0105f, 0.014f, 0.011f };
        static readonly Color[] SkyTint = { new Color(0.42f, 0.48f, 0.6f), new Color(0.45f, 0.4f, 0.62f), new Color(0.38f, 0.5f, 0.3f), new Color(0.6f, 0.32f, 0.28f) };
        static readonly Color[] LampTint = { new Color(1f, 0.78f, 0.5f), new Color(1f, 0.85f, 0.65f), new Color(1f, 0.6f, 0.25f), new Color(1f, 0.25f, 0.18f) };
        Camera atmosphereCam;
        float atmosphereBlend = -1f;

        /// <summary>Fait glisser l'ambiance vers celle du quartier où se trouve le joueur.</summary>
        public void UpdateAtmosphere(float pz, float dt, bool instant = false)
        {
            if (atmosphereCam == null) atmosphereCam = Camera.main;
            // Fondu sur les 60 derniers mètres avant le changement de quartier
            float local = Mathf.Repeat(Mathf.Max(0f, pz), BiomeLength);
            int b = BiomeAt(pz), nb = (b + 1) % BiomeNames.Length;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BiomeLength - 60f, BiomeLength, local));
            var fog = Color.Lerp(FogTint[b], FogTint[nb], k);
            var sky = Color.Lerp(SkyTint[b], SkyTint[nb], k);
            float dens = Mathf.Lerp(FogDensity[b], FogDensity[nb], k);
            float a = instant ? 1f : Mathf.Clamp01(dt * 2f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fog, a);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, dens, a);
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, sky, a);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, sky * 0.7f, a);
            if (atmosphereCam != null) atmosphereCam.backgroundColor = Color.Lerp(atmosphereCam.backgroundColor, fog * 0.8f, a);
        }

        public void Init(ZRLibrary library, ZRMaterials materials)
        {
            lib = library;
            mats = materials;
            pool = new ZRPool(transform);

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(g.GetComponent<Collider>());
            g.name = "Sol";
            g.transform.SetParent(transform, false);
            g.transform.localScale = new Vector3(40f, 1f, 60f);
            g.transform.position = new Vector3(0f, -0.05f, 0f);
            g.GetComponent<Renderer>().sharedMaterial = mats.darkMetal;
            ground = g.transform;

            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coinMesh = c.GetComponent<MeshFilter>().sharedMesh;
            Destroy(c);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            coinMat = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "ZR Coin" };
            coinMat.SetColor("_BaseColor", new Color(1f, 0.78f, 0.2f));
            coinMat.SetFloat("_Metallic", 0.9f);
            coinMat.SetFloat("_Smoothness", 0.8f);
            coinMat.EnableKeyword("_EMISSION");
            coinMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.1f) * 1.2f);

            for (int i = 0; i < ChunkCount; i++) chunks.Add(BuildChunk(i));

            if (lib.zombies.Length > 0 && lib.zombieAnimator != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var holder = new GameObject("Zombie de rue " + i).transform;
                    holder.SetParent(transform, false);
                    int zi = i % lib.zombies.Length;
                    var z = ZRAnimated.Create(lib.zombies[zi], lib.zombieAnimator, holder, Random.Range(1.65f, 1.85f), zi < lib.zombieTextures.Length ? lib.zombieTextures[zi] : null);
                    holder.gameObject.SetActive(false);
                    ambients.Add(new Ambient { z = z });
                }
                for (int i = 0; i < 5; i++)
                {
                    var holder = new GameObject("Zombie qui fonce " + i).transform;
                    holder.SetParent(transform, false);
                    int zi = (i + 1) % lib.zombies.Length;
                    var z = ZRAnimated.Create(lib.zombies[zi], lib.zombieAnimator, holder, Random.Range(1.7f, 1.9f), zi < lib.zombieTextures.Length ? lib.zombieTextures[zi] : null);
                    z.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    holder.gameObject.SetActive(false);
                    runnerPool.Push(z);
                }
            }
        }

        public void ResetWorld()
        {
            RemoveClue();
            RemovePower();
            foreach (var o in obstacles) ReleaseObstacle(o);
            obstacles.Clear();
            foreach (var c in coins) { c.go.SetActive(false); coinPool.Push(c.go); }
            coins.Clear();
            foreach (var a in ambients) { a.active = false; a.z.root.parent.gameObject.SetActive(false); }
            for (int i = 0; i < chunks.Count; i++) PlaceChunk(chunks[i], (i - 1) * ChunkLength);
            nextRowZ = 45f;
            clock = 0f;
            SpawnPaused = false;
            DoubleCoins = false;
        }

        public void Tick(float pz, float difficulty, float dt)
        {
            clock += dt;
            playerZ = pz;

            float maxZ = float.MinValue;
            foreach (var c in chunks) if (c.z > maxZ) maxZ = c.z;
            foreach (var c in chunks)
                if (c.z + ChunkLength < pz - 25f) { maxZ += ChunkLength; PlaceChunk(c, maxZ); }

            if (SpawnPaused) nextRowZ = Mathf.Max(nextRowZ, pz + SpawnAhead);
            while (nextRowZ < pz + SpawnAhead) nextRowZ += SpawnRow(nextRowZ, difficulty);

            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                if (o.kind == Kind.Oncoming && Mathf.Abs(o.z - pz) < 80f)
                {
                    o.z -= o.speed * dt;
                    var p = o.root.position; p.z = o.z; o.root.position = p;
                    if (o.zombie != null) { o.zombie.KeepLooping(); o.zombie.KeepFeetAbove(0f, dt); }
                }
                if (o.z < pz - 15f) { ReleaseObstacle(o); obstacles.RemoveAt(i); }
            }

            float spin = clock * 180f;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                if (c.pos.z < pz - 10f) { c.go.SetActive(false); coinPool.Push(c.go); coins.RemoveAt(i); continue; }
                if (!c.taken)
                {
                    c.go.transform.rotation = Quaternion.Euler(90f, spin + c.pos.z * 10f, 0f);
                    var twin = c.go.transform.GetChild(0).gameObject;
                    if (twin.activeSelf != DoubleCoins) twin.SetActive(DoubleCoins);
                }
            }

            // Lampadaires qui grésillent
            foreach (var c in chunks)
            {
                if (c.lamp == null) continue;
                float f = Mathf.PerlinNoise(clock * 6f, c.flickerSeed);
                c.lamp.intensity = f < 0.2f ? 1f : 4.5f;
            }

            // Indice de l'histoire : il tourne, flotte, et disparaît si on le rate
            if (clueGo != null)
            {
                if (clueZ < pz - 8f) { RemoveClue(); clueMissed = true; }
                else
                {
                    clueModel.localRotation = Quaternion.Euler(0f, clock * 90f, 0f);
                    clueModel.localPosition = new Vector3(0f, 1.15f + Mathf.Sin(clock * 3f) * 0.12f, 0f);
                    if (clueLight != null) clueLight.intensity = 3f + Mathf.Sin(clock * 6f) * 1.2f;
                }
            }

            // Super objet : il tourne, et disparaît si on le rate
            if (powerGo != null)
            {
                if (powerZ < pz - 8f) RemovePower();
                else
                {
                    powerModel.localRotation = Quaternion.Euler(0f, clock * 120f, 0f);
                    powerModel.localPosition = new Vector3(0f, 1.2f + Mathf.Sin(clock * 4f) * 0.12f, 0f);
                }
            }

            // Zombies sur les trottoirs : ils hurlent quand on passe
            foreach (var a in ambients)
            {
                if (!a.active) continue;
                float d = a.worldZ - pz;
                if (d < -12f) { a.active = false; a.z.root.parent.gameObject.SetActive(false); continue; }
                if (!a.screamed && d < 18f) { a.screamed = true; a.z.Play("Scream", 0.2f); }
            }

            ground.position = new Vector3(0f, -0.05f, pz + 150f);
        }

        /// <summary>Bonus PIÈCES x2 actif : chaque pièce de la route apparaît en double.</summary>
        public bool DoubleCoins { get; set; }

        /// <summary>Plus d'obstacles pendant un moment (combat de boss).</summary>
        public bool SpawnPaused { get; set; }

        /// <summary>Retire les obstacles entre deux distances (seconde chance, arrivée du boss).</summary>
        public void ClearAhead(float fromZ, float toZ)
        {
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                if (o.z < fromZ || o.z > toZ) continue;
                ReleaseObstacle(o); obstacles.RemoveAt(i);
            }
        }

        public bool Hits(Bounds player, out Kind kind)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (Mathf.Abs(o.z - player.center.z) > o.size.z * 0.5f + 2f) continue;
                if (o.walkable && player.min.y >= o.top - 0.35f) continue; // au-dessus du toit : pas de choc
                var b = new Bounds(o.root.position + o.center, o.size);
                if (b.Intersects(player)) { kind = o.kind; return true; }
            }
            kind = Kind.Jump;
            return false;
        }

        /// <summary>Hauteur du sol sous le joueur : 0 sur la route, ou le toit d'une petite voiture.</summary>
        public float GroundAt(float x, float z)
        {
            float g = 0f;
            foreach (var o in obstacles)
            {
                if (!o.walkable) continue;
                var c = o.root.position + o.center;
                if (Mathf.Abs(x - c.x) < o.size.x * 0.5f + 0.15f && Mathf.Abs(z - c.z) < o.size.z * 0.5f + 0.3f && o.top > g) g = o.top;
            }
            return g;
        }

        /// <summary>Le couloir visé est-il bloqué sur le côté (voiture à côté de nous) ?</summary>
        public bool LaneBlocked(int lane, float z, float halfLength, float y = 0f)
        {
            float x = LaneX(lane);
            foreach (var o in obstacles)
            {
                if (o.kind != Kind.Wall && o.kind != Kind.Oncoming) continue;
                if (o.walkable && y >= o.top - 0.35f) continue;
                var p = o.root.position + o.center;
                if (Mathf.Abs(p.x - x) < 1f && Mathf.Abs(p.z - z) < o.size.z * 0.5f + halfLength) return true;
            }
            return false;
        }

        public int CollectCoins(Vector3 center, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            foreach (var c in coins)
            {
                if (c.taken || Mathf.Abs(c.pos.z - center.z) > radius) continue;
                if ((c.pos - center).sqrMagnitude < r2) { c.taken = true; c.go.SetActive(false); n++; }
            }
            return n;
        }

        // ========================= Obstacles =========================

        float SpawnRow(float z, float difficulty)
        {
            float spacing = Mathf.Lerp(26f, 15f, difficulty) + Random.Range(-2f, 3f);

            // Voiture qui fonce vers nous (plus tard dans la partie) ou zombie qui court vers nous (dès le début)
            if (difficulty > 0.04f && Random.value < 0.1f + 0.14f * difficulty)
            {
                int lane = Random.Range(0, 3);
                bool zombie = runnerPool.Count > 0 && (difficulty <= 0.15f || Random.value < 0.5f);
                if (zombie) SpawnZombieRunner(lane, z + 25f);
                else SpawnOncoming(lane, z + 30f);
                return spacing;
            }

            var kinds = new Kind?[3];
            int a = Random.Range(0, 3);
            kinds[a] = RandomKind();
            if (Random.value < 0.35f + 0.4f * difficulty)
            {
                int b = (a + Random.Range(1, 3)) % 3;
                kinds[b] = RandomKind();
            }
            // Toujours laisser au moins un passage possible
            if (kinds[0] == Kind.Wall && kinds[1] == Kind.Wall && kinds[2] == Kind.Wall) kinds[Random.Range(0, 3)] = Kind.Jump;

            for (int lane = 0; lane < 3; lane++)
                if (kinds[lane].HasValue) SpawnObstacle(kinds[lane].Value, lane, z);

            if (Random.value < 0.75f)
            {
                int lane = Random.Range(0, 3);
                float x = LaneX(lane);
                if (kinds[lane] == Kind.Jump)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float dz = -4.5f + i * 1.5f, t = dz / 4.5f;
                        AddCoin(new Vector3(x, 1f + 1.5f * (1f - t * t), z + dz));
                    }
                }
                else if (kinds[lane] == Kind.Wall)
                {
                    for (int i = 0; i < 5; i++) AddCoin(new Vector3(x, 1f, z + 5f + i * 1.6f));
                }
                else
                {
                    for (int i = 0; i < 5; i++) AddCoin(new Vector3(x, 1f, z - 7f + i * 1.6f));
                }
            }
            return spacing;
        }

        static Kind RandomKind()
        {
            float r = Random.value;
            if (r < 0.42f) return Kind.Wall;
            if (r < 0.70f) return Kind.Jump;
            return Kind.Slide;
        }

        void SpawnObstacle(Kind kind, int lane, float z)
        {
            float x = LaneX(lane);
            switch (kind)
            {
                case Kind.Wall:
                {
                    bool big = lib.bigVehicles.Length > 0 && Random.value < 0.35f;
                    bool cop = !big && lib.police != null && Random.value < 0.15f;
                    var prefab = cop ? lib.police : big ? Pick(lib.bigVehicles) : Pick(lib.cars);
                    if (prefab == null) return;
                    float yaw = (Random.value < 0.5f ? 0f : 180f) + Random.Range(-8f, 8f);
                    var size = pool.SizeOf(prefab, Fit.Width, 2.05f);
                    var go = pool.Spawn(prefab, new Vector3(x, 0f, z), yaw, Fit.Width, 2.05f);
                    // Les petites voitures (pas les camions) : boîte à la hauteur du toit, on peut monter dessus
                    bool walkable = !big && size.y < 2f;
                    float h = walkable ? Mathf.Max(size.y * 0.95f, 0.9f) : 3f;
                    var o = new Obstacle
                    {
                        kind = kind, go = go, pooled = true, root = go.transform, z = z,
                        center = new Vector3(0f, h * 0.5f, 0f) + (new Vector3(x, 0f, z) - go.transform.position),
                        size = new Vector3(1.9f, h, size.z * 0.95f),
                        walkable = walkable, top = h
                    };
                    if (cop) o.extras = Siren(go.transform, size);
                    obstacles.Add(o);
                    break;
                }
                case Kind.Jump:
                {
                    float r = Random.value;
                    GameObject prefab = r < 0.6f ? lib.barrier : r < 0.8f ? lib.dumpster : lib.box;
                    if (prefab == null) prefab = lib.barrier;
                    float width = prefab == lib.box ? 1.3f : 2.2f;
                    var size = pool.SizeOf(prefab, Fit.Width, width, true);
                    float h = Mathf.Min(size.y, 1.25f);
                    var go = pool.Spawn(prefab, new Vector3(x, 0f, z), 0f, Fit.Width, width, true);
                    if (size.y > 1.25f) go.transform.localScale *= 1.25f / size.y; // jamais trop haut pour être sauté
                    obstacles.Add(new Obstacle
                    {
                        kind = kind, go = go, pooled = true, root = go.transform, z = z,
                        center = new Vector3(0f, h * 0.5f, 0f) + (new Vector3(x, 0f, z) - go.transform.position),
                        size = new Vector3(Mathf.Min(size.x, 2.1f), h, Mathf.Max(size.z, 0.5f))
                    });
                    break;
                }
                case Kind.Slide:
                {
                    int v = Random.Range(0, GateVariants);
                    if (!gatePools.TryGetValue(v, out var gp)) gatePools[v] = gp = new Stack<GameObject>();
                    var go = gp.Count > 0 ? gp.Pop() : BuildGate(v);
                    go.SetActive(true);
                    go.transform.position = new Vector3(x, 0f, z);
                    obstacles.Add(new Obstacle
                    {
                        kind = kind, go = go, pooled = false, root = go.transform, z = z, variant = v,
                        center = new Vector3(0f, 2.1f, 0f), size = new Vector3(2.1f, 1.4f, v == 1 ? 1.4f : 0.5f)
                    });
                    break;
                }
            }
        }

        void SpawnOncoming(int lane, float z)
        {
            var prefab = Pick(lib.cars);
            if (prefab == null) return;
            float x = LaneX(lane);
            var size = pool.SizeOf(prefab, Fit.Width, 2.05f);
            var holder = new GameObject("Voiture folle").transform;
            holder.SetParent(transform, false);
            holder.position = new Vector3(x, 0f, z);
            var go = pool.Spawn(prefab, holder.position, 180f, Fit.Width, 2.05f, false, holder);
            var extras = new List<GameObject> { go };
            // Phares allumés face au joueur
            float front = -size.z * 0.5f;
            extras.Add(ZRBuild.Box("Phare", holder, mats.headlight, new Vector3(-0.6f, 0.75f, front), new Vector3(0.35f, 0.18f, 0.05f)));
            extras.Add(ZRBuild.Box("Phare", holder, mats.headlight, new Vector3(0.6f, 0.75f, front), new Vector3(0.35f, 0.18f, 0.05f)));
            var lightGo = new GameObject("Phares");
            lightGo.transform.SetParent(holder, false);
            lightGo.transform.localPosition = new Vector3(0f, 1f, front - 0.3f);
            lightGo.transform.localRotation = Quaternion.Euler(8f, 180f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot; l.range = 30f; l.spotAngle = 60f; l.intensity = 6f; l.color = new Color(1f, 0.95f, 0.8f);
            extras.Add(lightGo);
            obstacles.Add(new Obstacle
            {
                kind = Kind.Oncoming, go = holder.gameObject, pooled = false, root = holder, z = z,
                center = new Vector3(0f, 1.5f, 0f), size = new Vector3(1.9f, 3f, size.z * 0.95f), extras = extras
            });
        }

        List<GameObject> Siren(Transform car, Vector3 size)
        {
            var list = new List<GameObject>();
            var holder = new GameObject("Gyrophare");
            holder.transform.SetParent(car, true);
            holder.transform.position = car.position + Vector3.up * (size.y + 0.05f);
            holder.transform.rotation = car.rotation;
            float inv = 1f / car.lossyScale.x;
            ZRBuild.Box("Rouge", holder.transform, mats.sirenRed, new Vector3(-0.3f, 0f, 0f) * inv, new Vector3(0.45f, 0.15f, 0.25f) * inv);
            ZRBuild.Box("Bleu", holder.transform, mats.sirenBlue, new Vector3(0.3f, 0f, 0f) * inv, new Vector3(0.45f, 0.15f, 0.25f) * inv);
            list.Add(holder);
            return list;
        }

        /// <summary>
        /// Obstacles sous lesquels on glisse. Tout est au-dessus de 1,5 m : en glissant on passe dessous.
        /// 0 = barrière jaune, 1 = échafaudage de chantier, 2 = poteau électrique tombé, 3 = banderole « zone infectée ».
        /// </summary>
        GameObject BuildGate(int variant)
        {
            var g = new GameObject("Obstacle à glisser");
            g.transform.SetParent(transform, false);
            var t = g.transform;
            switch (variant)
            {
                default: // Barrière jaune et noire
                    ZRBuild.Box("Barre", t, mats.hazard, new Vector3(0f, 1.65f, 0f), new Vector3(2.4f, 0.35f, 0.18f));
                    ZRBuild.Box("Barre2", t, mats.hazard, new Vector3(0f, 2.35f, 0f), new Vector3(2.4f, 0.35f, 0.18f));
                    ZRBuild.Box("PoteauG", t, mats.darkMetal, new Vector3(-1.25f, 1.4f, 0f), new Vector3(0.14f, 2.8f, 0.14f));
                    ZRBuild.Box("PoteauD", t, mats.darkMetal, new Vector3(1.25f, 1.4f, 0f), new Vector3(0.14f, 2.8f, 0.14f));
                    ZRBuild.Box("LampeG", t, mats.taillight, new Vector3(-1.25f, 2.85f, 0f), new Vector3(0.2f, 0.12f, 0.2f));
                    ZRBuild.Box("LampeD", t, mats.taillight, new Vector3(1.25f, 2.85f, 0f), new Vector3(0.2f, 0.12f, 0.2f));
                    break;

                case 1: // Échafaudage de chantier
                    for (int sx = -1; sx <= 1; sx += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                            ZRBuild.Box("Tube", t, mats.darkMetal, new Vector3(sx * 1.2f, 1.5f, sz * 0.6f), new Vector3(0.1f, 3f, 0.1f));
                    ZRBuild.Box("Plancher", t, mats.sidewalk, new Vector3(0f, 1.75f, 0f), new Vector3(2.5f, 0.12f, 1.4f));
                    ZRBuild.Box("Plinthe", t, mats.hazard, new Vector3(0f, 1.95f, -0.68f), new Vector3(2.5f, 0.28f, 0.05f));
                    ZRBuild.Box("Garde-corps", t, mats.darkMetal, new Vector3(0f, 2.75f, -0.62f), new Vector3(2.5f, 0.07f, 0.07f));
                    ZRBuild.Box("Garde-corps2", t, mats.darkMetal, new Vector3(0f, 2.75f, 0.62f), new Vector3(2.5f, 0.07f, 0.07f));
                    ZRBuild.Box("Diagonale", t, mats.darkMetal, new Vector3(0f, 2.35f, 0.6f), new Vector3(2.6f, 0.06f, 0.06f), new Vector3(0f, 0f, 18f));
                    ZRBuild.Box("Gyro", t, mats.lampGlow, new Vector3(1.1f, 2.95f, -0.6f), new Vector3(0.18f, 0.18f, 0.18f));
                    break;

                case 2: // Poteau électrique tombé sur deux blocs de béton
                    ZRBuild.Box("BlocG", t, mats.sidewalk, new Vector3(-1.2f, 0.85f, 0f), new Vector3(0.45f, 1.7f, 0.7f));
                    ZRBuild.Box("BlocD", t, mats.sidewalk, new Vector3(1.2f, 0.95f, 0f), new Vector3(0.45f, 1.9f, 0.7f));
                    ZRBuild.Box("Poteau", t, mats.darkMetal, new Vector3(0f, 1.98f, 0f), new Vector3(3.4f, 0.32f, 0.32f), new Vector3(0f, 12f, 3.5f));
                    ZRBuild.Box("Traverse", t, mats.darkMetal, new Vector3(1.45f, 2.45f, 0.1f), new Vector3(0.12f, 0.9f, 0.12f), new Vector3(0f, 0f, 8f));
                    ZRBuild.Box("Fil", t, mats.darkMetal, new Vector3(-0.3f, 2.5f, 0.25f), new Vector3(2.8f, 0.03f, 0.03f), new Vector3(0f, 0f, -6f));
                    ZRBuild.Box("Etincelle", t, mats.fire, new Vector3(1.45f, 2.92f, 0.1f), new Vector3(0.16f, 0.16f, 0.16f));
                    ZRBuild.Box("Ruban", t, mats.hazard, new Vector3(0f, 1.62f, -0.36f), new Vector3(2.3f, 0.08f, 0.02f));
                    break;

                case 3: // Banderole « zone infectée » entre deux poteaux
                    ZRBuild.Box("PoteauG", t, mats.darkMetal, new Vector3(-1.25f, 1.5f, 0f), new Vector3(0.12f, 3f, 0.12f));
                    ZRBuild.Box("PoteauD", t, mats.darkMetal, new Vector3(1.25f, 1.5f, 0f), new Vector3(0.12f, 3f, 0.12f));
                    ZRBuild.Box("Banderole", t, mats.sirenRed, new Vector3(0f, 2.15f, 0f), new Vector3(2.4f, 1.1f, 0.05f));
                    ZRBuild.Box("BandeHaut", t, mats.hazard, new Vector3(0f, 2.75f, 0f), new Vector3(2.4f, 0.12f, 0.07f));
                    ZRBuild.Box("BandeBas", t, mats.hazard, new Vector3(0f, 1.6f, 0f), new Vector3(2.4f, 0.12f, 0.07f));
                    ZRBuild.Box("Croix1", t, mats.blood, new Vector3(0f, 2.15f, -0.04f), new Vector3(0.9f, 0.14f, 0.02f), new Vector3(0f, 0f, 35f));
                    ZRBuild.Box("Croix2", t, mats.blood, new Vector3(0f, 2.15f, -0.04f), new Vector3(0.9f, 0.14f, 0.02f), new Vector3(0f, 0f, -35f));
                    ZRBuild.Box("Gyro", t, mats.sirenBlue, new Vector3(-1.25f, 3.05f, 0f), new Vector3(0.2f, 0.12f, 0.2f));
                    break;
            }
            return g;
        }

        // ---------- Zombies qui foncent vers nous ----------

        readonly Stack<ZRAnimated> runnerPool = new Stack<ZRAnimated>();

        void SpawnZombieRunner(int lane, float z)
        {
            var zb = runnerPool.Pop();
            var holder = zb.root.parent;
            holder.gameObject.SetActive(true);
            holder.position = new Vector3(LaneX(lane) + Random.Range(-0.25f, 0.25f), 0f, z);
            holder.rotation = Quaternion.Euler(0f, 180f, 0f);   // face au joueur
            zb.Reset();
            zb.Play("Run", 0f);
            zb.SetSpeed(Random.Range(1.05f, 1.25f));
            zb.animator.Update(Random.Range(0f, 1f));
            obstacles.Add(new Obstacle
            {
                kind = Kind.Oncoming, go = holder.gameObject, pooled = false, root = holder, z = z, zombie = zb,
                speed = Random.Range(5.5f, 7.5f),
                // Assez bas pour sauter par-dessus de justesse
                center = new Vector3(0f, 0.75f, 0f), size = new Vector3(1f, 1.5f, 0.8f)
            });
        }

        void ReleaseObstacle(Obstacle o)
        {
            if (o.zombie != null) { o.go.SetActive(false); runnerPool.Push(o.zombie); return; }
            if (o.kind == Kind.Oncoming)
            {
                if (o.extras != null && o.extras.Count > 0) pool.Release(o.extras[0]); // la voiture retourne au pool
                Destroy(o.go);
                return;
            }
            if (o.extras != null) foreach (var e in o.extras) Destroy(e);
            if (o.kind == Kind.Slide) { o.go.SetActive(false); gatePools[o.variant].Push(o.go); }
            else if (o.pooled) pool.Release(o.go);
        }

        // ========================= Indices de l'histoire =========================

        GameObject clueGo;
        Transform clueModel;
        Light clueLight;
        float clueZ;
        bool clueMissed;
        Material clueGreen, clueRed;

        public bool ClueActive => clueGo != null;

        /// <summary>Vrai une seule fois quand le joueur est passé à côté de l'indice.</summary>
        public bool ConsumeClueMissed() { bool m = clueMissed; clueMissed = false; return m; }

        /// <summary>Pose un indice dans une voie libre autour de z. Style : 0 radio, 1 document, 2 cassette, 3 dossier, 4 enregistrement final.</summary>
        public bool PlaceClue(float z, int style)
        {
            if (clueGo != null) return false;
            int pick = FreeLane(z);
            if (pick < 0) return false;
            for (int i = coins.Count - 1; i >= 0; i--)
                if (Mathf.Abs(coins[i].pos.z - z) < 3f && Mathf.Abs(coins[i].pos.x - LaneX(pick)) < 1f) { coins[i].taken = true; coins[i].go.SetActive(false); }
            BuildClue(new Vector3(LaneX(pick), 0f, z), style);
            clueZ = z;
            clueMissed = false;
            return true;
        }

        /// <summary>Une voie sans obstacle autour de z (et sans voiture qui arrive), ou -1.</summary>
        int FreeLane(float z)
        {
            var free = new List<int>();
            for (int lane = 0; lane < 3; lane++)
            {
                float x = LaneX(lane);
                bool ok = !(powerGo != null && Mathf.Abs(powerZ - z) < 6f && Mathf.Abs(powerGo.transform.position.x - x) < 1f)
                       && !(clueGo != null && Mathf.Abs(clueZ - z) < 6f && Mathf.Abs(clueGo.transform.position.x - x) < 1f);
                if (ok)
                    foreach (var o in obstacles)
                    {
                        var c = o.root.position + o.center;
                        if (Mathf.Abs(c.x - x) > 1.2f) continue;
                        if (o.kind == Kind.Oncoming && c.z > playerZ) { ok = false; break; }
                        if (Mathf.Abs(c.z - z) < o.size.z * 0.5f + 5f) { ok = false; break; }
                    }
                if (ok) free.Add(lane);
            }
            return free.Count == 0 ? -1 : free[Random.Range(0, free.Count)];
        }

        // ========================= Super objets =========================

        public enum Power { Magnet, Shield, Jump, Double }
        public static readonly string[] PowerNames = { "AIMANT", "BOUCLIER", "SUPER SAUT", "PIÈCES x2" };
        public static readonly Color[] PowerColors = { new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.6f, 1f), new Color(0.45f, 1f, 0.3f), new Color(1f, 0.8f, 0.2f) };

        GameObject powerGo;
        Transform powerModel;
        float powerZ;
        int powerType;
        Material[] powerGlow;

        public bool PowerActive => powerGo != null;

        public bool PlacePowerUp(float z, int type)
        {
            if (powerGo != null) return false;
            int lane = FreeLane(z);
            if (lane < 0) return false;
            for (int i = coins.Count - 1; i >= 0; i--)
                if (Mathf.Abs(coins[i].pos.z - z) < 2.5f && Mathf.Abs(coins[i].pos.x - LaneX(lane)) < 1f) { coins[i].taken = true; coins[i].go.SetActive(false); }
            if (powerGlow == null)
            {
                powerGlow = new Material[PowerColors.Length];
                for (int i = 0; i < powerGlow.Length; i++) powerGlow[i] = Glow(PowerColors[i], 4f);
            }
            powerType = type;
            powerZ = z;
            powerGo = new GameObject("Super objet " + PowerNames[type]);
            powerGo.transform.SetParent(transform, false);
            powerGo.transform.position = new Vector3(LaneX(lane), 0f, z);
            var t = powerGo.transform;
            var g = powerGlow[type];
            ZRBuild.Box("Halo", t, g, new Vector3(0f, 0.02f, 0f), new Vector3(1.2f, 0.03f, 1.2f));
            var lg = new GameObject("Lumière"); lg.transform.SetParent(t, false); lg.transform.localPosition = Vector3.up * 1.4f;
            var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 7f; l.intensity = 3f; l.color = PowerColors[type];

            powerModel = new GameObject("Objet").transform;
            powerModel.SetParent(t, false);
            powerModel.localPosition = Vector3.up * 1.2f;
            var m = powerModel;
            switch ((Power)type)
            {
                case Power.Magnet: // aimant en U
                    ZRBuild.Box("Gauche", m, g, new Vector3(-0.22f, 0.05f, 0f), new Vector3(0.18f, 0.55f, 0.2f));
                    ZRBuild.Box("Droite", m, g, new Vector3(0.22f, 0.05f, 0f), new Vector3(0.18f, 0.55f, 0.2f));
                    ZRBuild.Box("Bas", m, g, new Vector3(0f, -0.25f, 0f), new Vector3(0.62f, 0.18f, 0.2f));
                    ZRBuild.Box("PointeG", m, mats.headlight, new Vector3(-0.22f, 0.38f, 0f), new Vector3(0.19f, 0.12f, 0.21f));
                    ZRBuild.Box("PointeD", m, mats.headlight, new Vector3(0.22f, 0.38f, 0f), new Vector3(0.19f, 0.12f, 0.21f));
                    break;
                case Power.Shield: // écusson
                    ZRBuild.Box("Ecu", m, g, Vector3.zero, new Vector3(0.6f, 0.6f, 0.1f), new Vector3(0f, 0f, 45f));
                    ZRBuild.Box("Bord", m, mats.headlight, new Vector3(0f, 0f, -0.04f), new Vector3(0.3f, 0.3f, 0.06f), new Vector3(0f, 0f, 45f));
                    break;
                case Power.Jump: // basket
                    ZRBuild.Box("Semelle", m, mats.headlight, new Vector3(0f, -0.18f, 0f), new Vector3(0.3f, 0.1f, 0.7f));
                    ZRBuild.Box("Dessus", m, g, new Vector3(0f, 0f, -0.08f), new Vector3(0.3f, 0.28f, 0.5f));
                    ZRBuild.Box("Cheville", m, g, new Vector3(0f, 0.2f, 0.12f), new Vector3(0.3f, 0.3f, 0.22f));
                    break;
                default: // deux grosses pièces
                    var c1 = new GameObject("Pièce"); c1.transform.SetParent(m, false);
                    c1.transform.localScale = new Vector3(0.75f, 0.06f, 0.75f); c1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); c1.transform.localPosition = new Vector3(-0.12f, 0.08f, 0f);
                    c1.AddComponent<MeshFilter>().sharedMesh = coinMesh; c1.AddComponent<MeshRenderer>().sharedMaterial = coinMat;
                    var c2 = Instantiate(c1, m); c2.transform.localPosition = new Vector3(0.14f, -0.08f, -0.1f);
                    break;
            }
            return true;
        }

        /// <summary>Renvoie le type du super objet ramassé, ou -1.</summary>
        public int CollectPowerUp(Vector3 center, float radius)
        {
            if (powerGo == null) return -1;
            var p = powerGo.transform.position;
            if (Mathf.Abs(p.z - center.z) > radius || Mathf.Abs(p.x - center.x) > radius) return -1;
            RemovePower();
            return powerType;
        }

        void RemovePower()
        {
            if (powerGo != null) Destroy(powerGo);
            powerGo = null; powerModel = null;
        }

        /// <summary>Aimant : les pièces proches volent vers le joueur.</summary>
        public void AttractCoins(Vector3 center, float range, float dt)
        {
            foreach (var c in coins)
            {
                if (c.taken) continue;
                var d = c.pos - center;
                if (d.z < -2f || d.z > range || Mathf.Abs(d.x) > 7f) continue;
                c.pos = Vector3.MoveTowards(c.pos, center, 28f * dt);
                c.go.transform.position = c.pos;
            }
        }

        public bool CollectClue(Vector3 center, float radius)
        {
            if (clueGo == null) return false;
            var p = clueGo.transform.position + Vector3.up * 1.1f;
            if (Mathf.Abs(p.z - center.z) > radius || Mathf.Abs(p.x - center.x) > radius) return false;
            RemoveClue();
            return true;
        }

        void RemoveClue()
        {
            if (clueGo != null) Destroy(clueGo);
            clueGo = null; clueModel = null; clueLight = null;
        }

        Material Glow(Color c, float intensity)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "ZR Clue Glow" };
            m.SetColor("_BaseColor", c * 0.6f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * intensity);
            return m;
        }

        void BuildClue(Vector3 pos, int style)
        {
            if (clueGreen == null) { clueGreen = Glow(new Color(0.45f, 1f, 0.3f), 4f); clueRed = Glow(new Color(1f, 0.12f, 0.08f), 5f); }
            bool final = style == 4;
            var glow = final ? clueRed : clueGreen;
            clueGo = new GameObject(final ? "Enregistrement final" : "Indice");
            clueGo.transform.SetParent(transform, false);
            clueGo.transform.position = pos;
            var t = clueGo.transform;

            // Faisceau visible de loin + halo au sol + lumière
            ZRBuild.Box("Faisceau", t, glow, new Vector3(0f, 4f, 0f), new Vector3(0.08f, 8f, 0.08f));
            ZRBuild.Box("Halo", t, glow, new Vector3(0f, 0.02f, 0f), new Vector3(1.3f, 0.03f, 1.3f));
            var lg = new GameObject("Lumière");
            lg.transform.SetParent(t, false);
            lg.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            clueLight = lg.AddComponent<Light>();
            clueLight.type = LightType.Point; clueLight.range = 9f; clueLight.intensity = 3f;
            clueLight.color = final ? new Color(1f, 0.2f, 0.15f) : new Color(0.5f, 1f, 0.4f);

            clueModel = new GameObject("Objet").transform;
            clueModel.SetParent(t, false);
            clueModel.localPosition = new Vector3(0f, 1.15f, 0f);
            var m = clueModel;
            switch (style)
            {
                case 0: // Radio : boîtier, antenne, haut-parleur, voyant
                    ZRBuild.Box("Boîtier", m, mats.darkMetal, Vector3.zero, new Vector3(0.62f, 0.4f, 0.22f));
                    ZRBuild.Box("Grille", m, mats.sidewalk, new Vector3(-0.12f, 0f, -0.115f), new Vector3(0.3f, 0.28f, 0.02f));
                    ZRBuild.Box("Voyant", m, glow, new Vector3(0.18f, 0.08f, -0.115f), new Vector3(0.08f, 0.08f, 0.02f));
                    ZRBuild.Box("Antenne", m, mats.darkMetal, new Vector3(0.22f, 0.45f, 0f), new Vector3(0.025f, 0.55f, 0.025f), new Vector3(0f, 0f, -12f));
                    break;
                case 2: // Cassette audio
                    ZRBuild.Box("Cassette", m, mats.darkMetal, Vector3.zero, new Vector3(0.62f, 0.4f, 0.08f));
                    ZRBuild.Box("Étiquette", m, mats.headlight, new Vector3(0f, 0.1f, -0.045f), new Vector3(0.5f, 0.1f, 0.01f));
                    ZRBuild.Box("BobineG", m, mats.sidewalk, new Vector3(-0.14f, -0.06f, -0.045f), new Vector3(0.1f, 0.1f, 0.01f));
                    ZRBuild.Box("BobineD", m, mats.sidewalk, new Vector3(0.14f, -0.06f, -0.045f), new Vector3(0.1f, 0.1f, 0.01f));
                    break;
                case 3: // Dossier du patient zéro
                case 4: // Enregistrement final (plus grand)
                    float k = style == 4 ? 1.3f : 1f;
                    ZRBuild.Box("Chemise", m, mats.sirenRed, Vector3.zero, new Vector3(0.5f, 0.66f, 0.05f) * k);
                    ZRBuild.Box("Feuilles", m, mats.headlight, new Vector3(0.03f, 0.03f, -0.03f) * k, new Vector3(0.44f, 0.6f, 0.02f) * k);
                    ZRBuild.Box("Tampon", m, mats.blood, new Vector3(0.05f, 0.18f, -0.045f) * k, new Vector3(0.25f, 0.08f, 0.01f) * k);
                    break;
                default: // Document : feuille blanche et tampon rouge
                    ZRBuild.Box("Feuille", m, mats.headlight, Vector3.zero, new Vector3(0.5f, 0.66f, 0.02f));
                    ZRBuild.Box("Lignes", m, mats.darkMetal, new Vector3(0f, -0.05f, -0.015f), new Vector3(0.36f, 0.3f, 0.005f));
                    ZRBuild.Box("Tampon", m, mats.taillight, new Vector3(0.1f, 0.22f, -0.015f), new Vector3(0.18f, 0.08f, 0.005f));
                    break;
            }
        }

        void AddCoin(Vector3 pos)
        {
            GameObject go;
            if (coinPool.Count > 0) go = coinPool.Pop();
            else
            {
                go = new GameObject("Pièce");
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
                go.AddComponent<MeshFilter>().sharedMesh = coinMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = coinMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Deuxième pièce collée à la première : visible seulement avec le bonus PIÈCES x2
                var twin = new GameObject("Pièce double");
                twin.transform.SetParent(go.transform, false);
                twin.transform.localPosition = new Vector3(0.62f, -1.2f, 0.35f);
                twin.AddComponent<MeshFilter>().sharedMesh = coinMesh;
                var tr = twin.AddComponent<MeshRenderer>();
                tr.sharedMaterial = coinMat;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            go.transform.GetChild(0).gameObject.SetActive(DoubleCoins);
            go.SetActive(true);
            go.transform.position = pos;
            coins.Add(new Coin { go = go, pos = pos });
        }

        static GameObject Pick(GameObject[] arr) => arr != null && arr.Length > 0 ? arr[Random.Range(0, arr.Length)] : null;

        // ========================= Décor =========================

        Chunk BuildChunk(int index)
        {
            var root = new GameObject("Rue " + index).transform;
            root.SetParent(transform, false);
            float mid = ChunkLength * 0.5f;
            var road = ZRBuild.Box("Route", root, mats.asphalt, new Vector3(0f, -0.05f, mid), new Vector3(RoadHalf * 2f, 0.1f, ChunkLength));
            road.transform.localScale = new Vector3(RoadHalf * 2f, 0.1f, ChunkLength);
            for (int s = -1; s <= 1; s += 2)
            {
                ZRBuild.Box("Trottoir", root, mats.sidewalk, new Vector3(s * (RoadHalf + SidewalkWidth * 0.5f), 0.1f, mid), new Vector3(SidewalkWidth, 0.2f, ChunkLength));
                ZRBuild.Box("Bordure", root, mats.darkMetal, new Vector3(s * (RoadHalf + 0.1f), 0.12f, mid), new Vector3(0.2f, 0.24f, ChunkLength));
            }

            var c = new Chunk { root = root, flickerSeed = Random.value * 100f };
            BuildIndustrial(c, root);
            BuildPort(c, root);
            var lampGo = new GameObject("Lumière lampadaire");
            lampGo.transform.SetParent(root, false);
            var l = lampGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 22f; l.intensity = 4.5f; l.color = new Color(1f, 0.78f, 0.5f);
            l.shadows = LightShadows.None;
            c.lamp = l;
            return c;
        }

        void PlaceCityBlock(Chunk c, int side, float z, float buildingEdge)
        {
            float zz = 0f;
            while (zz < ChunkLength - 2f)
            {
                bool tower = lib.towers.Length > 0 && Random.value < 0.3f;
                var prefab = tower ? Pick(lib.towers) : Pick(lib.shops);
                float width = tower ? Random.Range(13f, 16f) : Random.Range(9f, 13f);
                var size = pool.SizeOf(prefab, Fit.Width, width);
                if (zz + size.x > ChunkLength + 3f) break;
                float yaw = side < 0 ? 90f : -90f;
                var pos = new Vector3(side * (buildingEdge + size.z * 0.5f), 0f, z + zz + size.x * 0.5f);
                c.items.Add(pool.Spawn(prefab, pos, yaw, Fit.Width, width));
                zz += size.x + Random.Range(0.3f, 2.5f);
            }
        }

        /// <summary>Banlieue : maisons en retrait, clôtures le long du trottoir, arbres dans les jardins.</summary>
        void PlaceSuburbBlock(Chunk c, int side, float z, float buildingEdge)
        {
            var houses = lib.houses.Length > 0 ? lib.houses : lib.shops;
            float zz = Random.Range(0f, 3f);
            while (zz < ChunkLength - 4f)
            {
                var prefab = Pick(houses);
                float width = Random.Range(9f, 11.5f);
                var size = pool.SizeOf(prefab, Fit.Width, width);
                if (zz + size.x > ChunkLength + 2f) break;
                var pos = new Vector3(side * (buildingEdge + 4f + size.z * 0.5f), 0f, z + zz + size.x * 0.5f);
                c.items.Add(pool.Spawn(prefab, pos, side < 0 ? 90f : -90f, Fit.Width, width));
                if (lib.trees.Length > 0 && Random.value < 0.7f)
                    c.items.Add(pool.Spawn(Pick(lib.trees), new Vector3(side * (buildingEdge + 1.8f), 0f, z + zz + Random.Range(1f, size.x)), Random.Range(0f, 360f), Fit.Height, Random.Range(5f, 8f)));
                zz += size.x + Random.Range(3f, 6f);
            }
            // Clôture du jardin, avec des trous (les zombies sont passés)
            if (lib.fence)
                for (float f = 2f; f < ChunkLength; f += 4.2f)
                    if (Random.value < 0.8f)
                        c.items.Add(pool.Spawn(lib.fence, new Vector3(side * (buildingEdge + 0.4f), 0f, z + f), 90f, Fit.Width, 4f));
        }

        Material[] containerMats;
        Material warehouseMat, roofMat;

        Material Paint(Color col, float smooth = 0.35f)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(sh != null ? sh : Shader.Find("Standard")) { name = "ZR Paint" };
            m.SetColor("_BaseColor", col);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        void EnsurePaints()
        {
            if (containerMats != null) return;
            containerMats = new[] { Paint(new Color(0.55f, 0.12f, 0.08f)), Paint(new Color(0.1f, 0.25f, 0.5f)), Paint(new Color(0.75f, 0.42f, 0.08f)), Paint(new Color(0.15f, 0.4f, 0.22f)), Paint(new Color(0.45f, 0.45f, 0.47f)) };
            warehouseMat = Paint(new Color(0.32f, 0.34f, 0.33f), 0.2f);
            roofMat = Paint(new Color(0.18f, 0.19f, 0.2f), 0.3f);
        }

        /// <summary>Zone industrielle : entrepôts, cheminées en feu (fabriqués en code, réutilisés).</summary>
        void BuildIndustrial(Chunk c, Transform root)
        {
            EnsurePaints();
            var set = new GameObject("Zone industrielle").transform;
            set.SetParent(root, false);
            float edge = RoadHalf + SidewalkWidth + 0.5f;
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                {
                    var w = new GameObject("Entrepôt").transform;
                    w.SetParent(set, false);
                    w.localPosition = new Vector3(side * (edge + 7f), 0f, 10f + k * 20f);
                    ZRBuild.Box("Murs", w, warehouseMat, new Vector3(0f, 0.5f, 0f), new Vector3(14f, 1f, 18f));
                    ZRBuild.Box("Toit", w, roofMat, new Vector3(0f, 1.02f, 0f), new Vector3(14.6f, 0.06f, 18.6f));
                    ZRBuild.Box("Bande", w, mats.hazard, new Vector3(-side * 7.02f, 0.9f, 0f), new Vector3(0.05f, 0.08f, 18f));
                    ZRBuild.Box("Porte", w, mats.darkMetal, new Vector3(-side * 7.03f, 0.25f, 0f), new Vector3(0.05f, 0.5f, 6f));
                    ZRBuild.Box("Fenêtres", w, mats.lampGlow, new Vector3(-side * 7.03f, 0.75f, 0f), new Vector3(0.04f, 0.05f, 14f));
                    c.warehouses.Add(w);
                }
            for (int side = -1; side <= 1; side += 2)
            {
                var ch = new GameObject("Cheminée").transform;
                ch.SetParent(set, false);
                ch.localPosition = new Vector3(side * (edge + 13f), 0f, 20f);
                ZRBuild.Box("Fût", ch, mats.darkMetal, new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1f, 1.6f));
                ZRBuild.Box("Bague", ch, mats.hazard, new Vector3(0f, 0.85f, 0f), new Vector3(1.75f, 0.03f, 1.75f));
                ZRBuild.Box("Feu", ch, mats.fire, new Vector3(0f, 1.02f, 0f), new Vector3(1.2f, 0.05f, 1.2f));
                c.chimneys.Add(ch);
            }
            c.industrial = set.gameObject;
            set.gameObject.SetActive(false);
        }

        void PlaceIndustrial(Chunk c)
        {
            foreach (var w in c.warehouses) w.localScale = new Vector3(1f, Random.Range(6f, 10f), 1f);
            foreach (var ch in c.chimneys) { ch.gameObject.SetActive(Random.value < 0.5f); ch.localScale = new Vector3(1f, Random.Range(18f, 26f), 1f); }
        }

        /// <summary>Port : piles de conteneurs colorés et une grue (fabriqués en code, réutilisés).</summary>
        void BuildPort(Chunk c, Transform root)
        {
            EnsurePaints();
            var set = new GameObject("Port").transform;
            set.SetParent(root, false);
            float edge = RoadHalf + SidewalkWidth + 0.8f;
            for (int side = -1; side <= 1; side += 2)
                for (int stack = 0; stack < 3; stack++)
                    for (int lvl = 0; lvl < 3; lvl++)
                    {
                        var box = ZRBuild.Box("Conteneur", set, containerMats[0], new Vector3(side * (edge + 1.4f + (stack % 2) * 2.7f), 1.3f + lvl * 2.65f, 6f + stack * 13f), new Vector3(2.5f, 2.6f, 12f));
                        c.containers.Add(box.GetComponent<Renderer>());
                    }
            var crane = new GameObject("Grue").transform;
            crane.SetParent(set, false);
            ZRBuild.Box("PiedA", crane, mats.hazard, new Vector3(-4f, 11f, 0f), new Vector3(0.8f, 22f, 0.8f));
            ZRBuild.Box("PiedB", crane, mats.hazard, new Vector3(4f, 11f, 0f), new Vector3(0.8f, 22f, 0.8f));
            ZRBuild.Box("Flèche", crane, mats.darkMetal, new Vector3(0f, 22.5f, 0f), new Vector3(30f, 1.2f, 1.4f));
            ZRBuild.Box("Feu", crane, mats.sirenRed, new Vector3(15f, 23.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f));
            c.crane = crane;
            c.port = set.gameObject;
            set.gameObject.SetActive(false);
        }

        void PlacePort(Chunk c)
        {
            for (int i = 0; i < c.containers.Count; i++)
            {
                var r = c.containers[i];
                int lvl = i % 3;
                // piles de hauteur variable : on cache les étages du haut au hasard
                r.gameObject.SetActive(lvl == 0 || Random.value < (lvl == 1 ? 0.75f : 0.4f));
                r.sharedMaterial = containerMats[Random.Range(0, containerMats.Length)];
            }
            bool crane = Random.value < 0.35f;
            c.crane.gameObject.SetActive(crane);
            if (crane) c.crane.localPosition = new Vector3((Random.value < 0.5f ? -1f : 1f) * 20f, 0f, Random.Range(10f, 30f));
        }

        void PlaceChunk(Chunk c, float z)
        {
            foreach (var go in c.items) pool.Release(go);
            c.items.Clear();
            c.z = z;
            c.root.position = new Vector3(0f, 0f, z);
            if (!lib.HasEnvironment) return;

            int biome = BiomeAt(z);
            c.biome = biome;
            c.industrial.SetActive(biome == 2);
            c.port.SetActive(biome == 3);
            if (biome == 2) PlaceIndustrial(c);
            if (biome == 3) PlacePort(c);

            float buildingEdge = RoadHalf + SidewalkWidth + 0.3f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (biome == 0) PlaceCityBlock(c, side, z, buildingEdge);
                else if (biome == 1) PlaceSuburbBlock(c, side, z, buildingEdge);

                // Lampadaires (moins nombreux en banlieue)
                if (lib.lamps.Length > 0)
                    for (int k = 0; k < (biome == 1 ? 1 : 2); k++)
                    {
                        float lz = z + 8f + k * 20f + (biome == 1 && side > 0 ? 18f : 0f);
                        c.items.Add(pool.Spawn(Pick(lib.lamps), new Vector3(side * (RoadHalf + 0.7f), 0.2f, lz), side < 0 ? -90f : 90f, Fit.Height, 6.5f));
                    }

                // Voitures abandonnées garées sur le trottoir (voitures de police au port)
                if (lib.cars.Length > 0 && Random.value < (biome == 3 ? 0.4f : 0.6f))
                {
                    float cz = z + Random.Range(5f, 35f);
                    var car = biome == 3 && lib.police != null && Random.value < 0.6f ? lib.police : Pick(lib.cars);
                    c.items.Add(pool.Spawn(car, new Vector3(side * (RoadHalf + 2f), 0.2f, cz), Random.Range(-35f, 35f) + (Random.value < 0.5f ? 0f : 180f), Fit.Width, 2.05f));
                }

                // Petits détails du quartier
                for (int k = 0; k < 3; k++)
                {
                    float r = Random.value;
                    var pos = new Vector3(side * (RoadHalf + Random.Range(1.2f, SidewalkWidth - 0.5f)), 0.2f, z + Random.Range(1f, 39f));
                    switch (biome)
                    {
                        case 1: // banlieue : arbres, poubelles, panneaux
                            if (r < 0.55f && lib.trees.Length > 0) c.items.Add(pool.Spawn(Pick(lib.trees), pos, Random.Range(0f, 360f), Fit.Height, Random.Range(4.5f, 7f)));
                            else if (r < 0.75f && lib.dumpster) c.items.Add(pool.Spawn(lib.dumpster, pos, Random.Range(0f, 360f), Fit.Width, 1.6f, true));
                            else if (lib.signs.Length > 0) c.items.Add(pool.Spawn(Pick(lib.signs), pos, side < 0 ? 90f : -90f, Fit.Height, 2.6f));
                            break;
                        case 2: // zone industrielle : chantier, cônes, pneus, lampes de chantier
                        case 3: // port : barrières, cônes, gyrophares de chantier
                            if (r < 0.3f && lib.barrier) c.items.Add(pool.Spawn(lib.barrier, pos, side < 0 ? 90f : -90f, Fit.Width, 2f, true));
                            else if (r < 0.55f && lib.cone) c.items.Add(pool.Spawn(lib.cone, pos, Random.Range(0f, 360f), Fit.Height, 0.7f));
                            else if (r < 0.75f && lib.tire) c.items.Add(pool.Spawn(lib.tire, pos, Random.Range(0f, 360f), Fit.Height, 0.7f));
                            else if (lib.constructionLight) c.items.Add(pool.Spawn(lib.constructionLight, pos, Random.Range(0f, 360f), Fit.Height, 1.4f));
                            break;
                        default: // centre-ville
                            if (r < 0.25f && lib.dumpster) c.items.Add(pool.Spawn(lib.dumpster, pos, Random.Range(0f, 360f), Fit.Width, 1.8f, true));
                            else if (r < 0.5f && lib.cone) c.items.Add(pool.Spawn(lib.cone, pos, Random.Range(0f, 360f), Fit.Height, 0.7f));
                            else if (r < 0.65f && lib.tire) c.items.Add(pool.Spawn(lib.tire, pos, Random.Range(0f, 360f), Fit.Height, 0.7f));
                            else if (r < 0.85f && lib.trees.Length > 0) c.items.Add(pool.Spawn(Pick(lib.trees), pos, Random.Range(0f, 360f), Fit.Height, Random.Range(5f, 7f)));
                            else if (lib.signs.Length > 0) c.items.Add(pool.Spawn(Pick(lib.signs), pos, side < 0 ? 90f : -90f, Fit.Height, 2.6f));
                            break;
                    }
                }
                if (lib.pole && biome <= 1 && Random.value < 0.4f)
                    c.items.Add(pool.Spawn(lib.pole, new Vector3(side * (RoadHalf + SidewalkWidth - 0.4f), 0.2f, z + Random.Range(2f, 38f)), 0f, Fit.Height, 8f));
            }

            // Lumière du lampadaire (une par rue, pour rester léger)
            int ls = Random.value < 0.5f ? -1 : 1;
            c.lamp.transform.position = new Vector3(ls * (RoadHalf - 1.5f), 6f, z + 8f);
            c.lamp.color = LampTint[biome];
            c.lamp.enabled = z > -10f;

            // Zombies sur le trottoir
            foreach (var a in ambients)
            {
                if (a.active || z < 60f || Random.value > 0.45f) continue;
                int side = Random.value < 0.5f ? -1 : 1;
                a.active = true;
                a.screamed = false;
                a.worldZ = z + Random.Range(5f, 35f);
                var h = a.z.root.parent;
                h.gameObject.SetActive(true);
                h.position = new Vector3(side * (RoadHalf + Random.Range(1.5f, 3.5f)), 0.2f, a.worldZ);
                h.rotation = Quaternion.Euler(0f, side < 0 ? 120f : -120f, 0f);
                a.z.Reset();
                a.z.Play("Idle", 0f);
                a.z.animator.Update(Random.Range(0f, 2f));
                break;
            }
        }
    }
}
