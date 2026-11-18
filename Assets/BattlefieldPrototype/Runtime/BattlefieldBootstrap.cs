using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    /// <summary>Creates the battlefield, articulated class models and deployment flow without prefab wiring.</summary>
    public sealed partial class BattlefieldBootstrap : MonoBehaviour
    {
        [Header("Live tower warfare")]
        public bool TowerDefenseMode = true;
        [Header("Unit balance assets")]
        public BattleUnitDefinition Vanguard;
        public BattleUnitDefinition Ranger;
        public BattleUnitDefinition Medic;
        public BattleUnitDefinition Skirmisher;
        public BattleUnitDefinition Brute;
        public BattleUnitDefinition Engineer;
        public BattleUnitDefinition Stormcaller;
        public BattleUnitDefinition[] AdditionalCharacters;
        [Header("Each army")]
        [Tooltip("Reroll composition and deployment every round. Counts below set army size and role weights.")]
        public bool RandomizeArmies = true;
        [Range(1, 12)] public int VanguardsPerTeam = 4;
        [Range(0, 12)] public int RangersPerTeam = 4;
        [Range(0, 6)] public int MedicsPerTeam = 2;
        [Range(0, 8)] public int SkirmishersPerTeam = 2;
        [Range(0, 8)] public int BrutesPerTeam = 2;
        [Range(0, 8)] public int EngineersPerTeam = 1;
        [Range(0, 8)] public int StormcallersPerTeam = 1;
        [Header("Arena")]
        public Vector2 ArenaSize = new Vector2(160, 192);
        public bool RandomizeBattlefield = true;
        public BattleMapKind MapKind = BattleMapKind.RuinedVillage;
        public BattleMapKind CurrentMapKind { get; private set; }
        public string MapName => TowerDefenseMode ? TowerMapCatalog.Name(TowerMap) : BattleMapNames.Name(CurrentMapKind);
        public IReadOnlyList<Bounds> CoverFootprints => coverFootprints;
        [Tooltip("A referenced URP material keeps the primitive shader included in device builds.")]
        public Material PrimitiveMaterial;
        [Header("Optional existing Meta / XR rig")]
        public Transform ExistingRigRoot;
        public Camera ExistingHeadCamera;
        public Transform ExistingRightHandAim;
        public Transform ExistingLeftHandGrip;
        [Header("Army deployment")]
        [Range(10, 5000)] public int ArmyPowerBudget = 600;
        public bool RandomizePowerBudget = true;
        [Range(50, 5000)] public int MinimumArmyPower = 400;
        [Range(50, 5000)] public int MaximumArmyPower = 1000;

        readonly List<Material> materials = new List<Material>();
        readonly List<Texture2D> characterTextures = new List<Texture2D>();
        readonly List<BattleUnitDefinition> ownedDefinitions = new List<BattleUnitDefinition>();
        BattleDirector director;
        Transform unitRoot;
        Material blue, red, ground, stone, trim, healer, armor, leather, visor, storm;
        BattlefieldScenery scenery;
        Transform arenaRoot;
        readonly List<Bounds> coverFootprints = new List<Bounds>();
        Material foliage, water, frost, acid;
        Mesh canopyMesh;
        bool hasStarted;
        bool battlefieldPreviewReady, previewRandomized;
        readonly List<BattleUnitDefinition> additionalDefinitions = new List<BattleUnitDefinition>();

        void Start()
        {
            if (hasStarted) return;
            hasStarted = true;
            ArenaSize = new Vector2(Mathf.Max(TowerDefenseMode ? 160 : 28, ArenaSize.x), Mathf.Max(TowerDefenseMode ? 192 : 36, ArenaSize.y));
            Vanguard = EnsureDefinition(Vanguard, UnitRole.Vanguard);
            Ranger = EnsureDefinition(Ranger, UnitRole.Ranger);
            Medic = EnsureDefinition(Medic, UnitRole.Medic);
            Skirmisher = EnsureDefinition(Skirmisher, UnitRole.Skirmisher);
            Brute = EnsureDefinition(Brute, UnitRole.Brute);
            Engineer = EnsureDefinition(Engineer, UnitRole.Engineer);
            Stormcaller = EnsureDefinition(Stormcaller, UnitRole.Stormcaller);
            ArmyPowerBudget = BattleDeployment.NormalizeBudget(ArmyPowerBudget);
            for (int role = (int)UnitRole.Wisp; role <= (int)UnitRole.Warden; role++)
            {
                BattleUnitDefinition supplied = null;
                if (AdditionalCharacters != null) foreach (var candidate in AdditionalCharacters)
                    if (candidate != null && (int)candidate.Role == role) { supplied = candidate; break; }
                additionalDefinitions.Add(EnsureDefinition(supplied, (UnitRole)role));
            }
            if (TowerDefenseMode)
                for (int role = (int)UnitRole.Skywing; role <= (int)UnitRole.Crownwing; role++)
                {
                    BattleUnitDefinition supplied = null;
                    if (AdditionalCharacters != null) foreach (var candidate in AdditionalCharacters)
                        if (candidate != null && (int)candidate.Role == role) { supplied = candidate; break; }
                    additionalDefinitions.Add(EnsureDefinition(supplied, (UnitRole)role));
                }
            blue = MakeMaterial(new Color(.12f, .30f, .36f), .06f, .18f);
            red = MakeMaterial(new Color(.48f, .14f, .10f), .06f, .18f);
            ground = MakeMaterial(Color.white);
            stone = MakeMaterial(Color.white);
            trim = MakeMaterial(new Color(.58f, .44f, .25f), .72f, .38f);
            healer = MakeMaterial(new Color(.24f, .76f, .49f), .1f, .3f);
            armor = MakeMaterial(new Color(.28f, .32f, .35f), .78f, .44f);
            leather = MakeMaterial(new Color(.12f, .10f, .077f), .02f, .22f);
            visor = MakeMaterial(new Color(.70f, .59f, .36f), .25f, .58f);
            storm = MakeMaterial(new Color(.48f, .31f, .72f), .2f, .5f);
            frost = MakeMaterial(new Color(.29f,.74f,.92f),.15f,.6f);
            acid = MakeMaterial(new Color(.55f,.80f,.13f),.05f,.55f);
            foliage = MakeMaterial(new Color(.32f, .57f, .21f), 0, .22f);
            water = MakeMaterial(new Color(.22f, .65f, .78f), .05f, .48f);
            DressCharacterMaterials();
            scenery = gameObject.AddComponent<BattlefieldScenery>();
            scenery.Initialize(ground, stone, ArenaSize);
            CreateArena(MapKind);
            director = gameObject.AddComponent<BattleDirector>();
            gameObject.AddComponent<BattleEffects>().Initialize(director);
            gameObject.AddComponent<BattleAudio>().Initialize(director);
            gameObject.AddComponent<BattleOrders>().Initialize(director);
            director.Initialize(SpawnRound);
            var deployment = gameObject.AddComponent<BattleDeployment>();
            deployment.Initialize(director, this, ArmyPowerBudget);
            if (!TowerDefenseMode) deployment.GoHome();
            PossessionController player = gameObject.AddComponent<PossessionController>();
            if (ExistingRigRoot != null && ExistingHeadCamera != null)
                player.AssignRig(ExistingRigRoot, ExistingHeadCamera, ExistingRightHandAim, ExistingLeftHandGrip);
            player.Initialize(director);
            gameObject.AddComponent<BattleFirstPerson>().Initialize(this, player);
            if (TowerDefenseMode)
            {
                var towers = gameObject.AddComponent<TowerBattle>();
                towers.Initialize(this, director, player);
                gameObject.AddComponent<TowerBattleHud>().Initialize(towers, player);
                towers.GoHome();
            }
            else gameObject.AddComponent<BattleHud>().Initialize(director, player);
        }

        BattleUnitDefinition EnsureDefinition(BattleUnitDefinition definition, UnitRole role)
        {
            if (definition != null) return definition;
            definition = BattleUnitDefinition.CreateDefault(role);
            ownedDefinitions.Add(definition);
            return definition;
        }

        Material MakeMaterial(Color color, float metallic = 0, float smoothness = .12f)
        {
            Material material;
            if (PrimitiveMaterial != null) material = new Material(PrimitiveMaterial);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                material = new Material(shader);
            }
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            materials.Add(material);
            return material;
        }

        void DressCharacterMaterials()
        {
            blue.name = "Deep teal wool"; red.name = "Oxide red wool";
            armor.name = "Tempered iron"; leather.name = "Worn brown hide";
            trim.name = "Antique brass"; visor.name = "Amber eye slit";
            healer.name = "Jade focus"; storm.name = "Amethyst focus";
            Texture2D cloth = CharacterSurface("Woven regiment cloth", true);
            Texture2D metal = CharacterSurface("Brushed and weathered metal", false);
            blue.mainTexture = cloth; red.mainTexture = cloth;
            armor.mainTexture = metal; trim.mainTexture = metal; leather.mainTexture = cloth;
            foreach (Material glow in new[] { visor, healer, storm, frost, acid })
            {
                if (!glow.HasProperty("_EmissionColor")) continue;
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", glow.color * (glow == visor ? .45f : .65f));
            }
        }

        Texture2D CharacterSurface(string label, bool woven)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = label, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear,
                anisoLevel = 2, hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float grain = Mathf.PerlinNoise(x * .31f + 17.2f, y * .31f + 9.7f);
                    float wear = Mathf.PerlinNoise(x * .07f + 3.1f, y * .07f + 13.4f);
                    float detail = woven ? ((x % 4 < 2) == (y % 4 < 2) ? .07f : -.04f) :
                        Mathf.PerlinNoise(x * .9f, y * .08f) * .09f;
                    float value = Mathf.Clamp01(.69f + grain * .18f + wear * .10f + detail);
                    pixels[y * size + x] = new Color(value, value, value, 1);
                }
            texture.SetPixels(pixels); texture.Apply(true, true);
            characterTextures.Add(texture);
            return texture;
        }

        internal void PrepareBattlefield()
        {
            // Starting from Home must use the map the player just inspected.
            if (battlefieldPreviewReady && MapKind == CurrentMapKind && previewRandomized == RandomizeBattlefield)
            {
                battlefieldPreviewReady = false;
                return;
            }
            battlefieldPreviewReady = false;
            var next = RandomizeBattlefield ? (BattleMapKind)(((int)CurrentMapKind + Random.Range(1, BattleMapNames.Count)) % BattleMapNames.Count) : MapKind;
            if (arenaRoot == null || next != CurrentMapKind) CreateArena(next);
        }

        internal void PrepareHomeBattlefield()
        {
            var next = RandomizeBattlefield ? (BattleMapKind)(((int)CurrentMapKind + Random.Range(1, BattleMapNames.Count)) % BattleMapNames.Count) : MapKind;
            PreviewBattlefield(next);
        }

        internal void PreviewBattlefield(BattleMapKind kind)
        {
            MapKind = kind;
            if (arenaRoot == null || kind != CurrentMapKind) CreateArena(kind);
            previewRandomized = RandomizeBattlefield;
            battlefieldPreviewReady = true;
        }

        void CreateArena(BattleMapKind kind)
        {
            ReleaseMapMeshes();
            if (arenaRoot != null)
            {
                var previousSurface=arenaRoot.GetComponent<NavMeshSurface>();
                arenaRoot.gameObject.SetActive(false);
                if(previousSurface!=null && previousSurface.navMeshData!=null) Destroy(previousSurface.navMeshData);
                Destroy(arenaRoot.gameObject); scenery.ClearDressing();
            }
            coverFootprints.Clear(); movingObstacles.Clear(); towerSightBlockers.Clear();
            CurrentMapKind = kind;
            Transform arena = new GameObject("Arena - runtime NavMesh").transform;
            arenaRoot = arena;
            arena.SetParent(transform, false);
            Shape("Ground", PrimitiveType.Cube, arena, new Vector3(0, -0.25f, 0),
                new Vector3(ArenaSize.x, 0.5f, ArenaSize.y), ground, true);
            float halfX = ArenaSize.x * 0.5f;
            float halfZ = ArenaSize.y * 0.5f;
            bool enclosed=!TowerDefenseMode && BattleMapNames.IsEnclosed(kind) && ArenaSize.x>=60 && ArenaSize.y>=70;
            float perimeter=enclosed?10:2;
            Shape("West boundary", PrimitiveType.Cube, arena, new Vector3(-halfX, perimeter/2, 0), new Vector3(1, perimeter, ArenaSize.y), stone, true);
            Shape("East boundary", PrimitiveType.Cube, arena, new Vector3(halfX, perimeter/2, 0), new Vector3(1, perimeter, ArenaSize.y), stone, true);
            Shape("North boundary", PrimitiveType.Cube, arena, new Vector3(0, perimeter/2, halfZ), new Vector3(ArenaSize.x, perimeter, 1), stone, true);
            Shape("South boundary", PrimitiveType.Cube, arena, new Vector3(0, perimeter/2, -halfZ), new Vector3(ArenaSize.x, perimeter, 1), stone, true);
            if (TowerDefenseMode) { BuildTowerTerrain(); builtTowerMap = TowerMap; hasTowerArena = true; }
            else if(enclosed) BuildComplexMap();
            else
            {
                Cover("West cover", new Vector3(-8, 1, 0), new Vector3(3, 2, 4));
                Cover("East cover", new Vector3(8, 1, 0), new Vector3(3, 2, 4));
                Cover("North cover", new Vector3(-3.5f, .9f, 5), new Vector3(2, 1.8f, 2));
                Cover("South cover", new Vector3(3.5f, .9f, -5), new Vector3(2, 1.8f, 2));
                BuildMapFeatures();
            }
            Shape("Blue deployment stripe", PrimitiveType.Cube, arena, new Vector3(0, 0.012f, -halfZ + 5), new Vector3(ArenaSize.x - 2, 0.02f, 0.3f), blue, false);
            Shape("Red deployment stripe", PrimitiveType.Cube, arena, new Vector3(0, 0.012f, halfZ - 5), new Vector3(ArenaSize.x - 2, 0.02f, 0.3f), red, false);
            // Surface only collects terrain siblings; neither armies nor the player enter this bake.
            NavMeshSurface surface = arena.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            foreach(var moving in movingObstacles) moving.ActivateNavigation();
            if (surface.navMeshData == null)
                Debug.LogError("Prototype NavMesh could not be built. Check that AI Navigation is enabled.", this);
            scenery.DressArena(arena, ArenaSize, ground, stone, armor);
        }

        void Cover(string name, Vector3 p, Vector3 size)
        {
            Shape(name, PrimitiveType.Cube, arenaRoot, p, size, stone, true);
            coverFootprints.Add(new Bounds(p, size));
        }

        void BuildMapFeatures()
        {
            if (ArenaSize.x < 60 || ArenaSize.y < 70) return;
            if (CurrentMapKind == BattleMapKind.RuinedVillage)
            {
                for (int x = -1; x <= 1; x++) for (int z = -2; z <= 2; z++)
                {
                    if (x == 0 && z == 0) continue;
                    Vector3 p = new Vector3(x * ArenaSize.x * .27f, 1.8f, z * ArenaSize.y * .17f);
                    Cover("Village wall cover", p, new Vector3(8, 3.6f, 1.1f));
                    Cover("Village return cover", p + new Vector3(-3.5f, -.45f, 2.6f), new Vector3(1.1f, 2.7f, 5.2f));
                }
            }
            else if (CurrentMapKind == BattleMapKind.RiverCrossing)
            {
                Shape("Shallow river", PrimitiveType.Cube, arenaRoot, new Vector3(0, .015f, 0), new Vector3(ArenaSize.x - 2, .025f, 10), water, false);
                // Broken banks leave three broad causeways; all lanes remain connected.
                float gap = 9, spacing = ArenaSize.x * .29f;
                var edges = new[] { -ArenaSize.x * .5f + 1, -spacing - gap*.5f, -spacing + gap*.5f,
                    -gap*.5f, gap*.5f, spacing - gap*.5f, spacing + gap*.5f, ArenaSize.x*.5f - 1 };
                for (int i = 0; i < edges.Length; i += 2) for (int side = -1; side <= 1; side += 2)
                    Cover("River bank cover", new Vector3((edges[i]+edges[i+1])*.5f, .65f, side*5.5f), new Vector3(edges[i+1]-edges[i], 1.3f, 1.2f));
                for (int i = -1; i <= 1; i++)
                    Shape("Stone causeway", PrimitiveType.Cube, arenaRoot, new Vector3(i*spacing,.035f,0), new Vector3(gap,.06f,12), stone, true);
            }
            else
            {
                if(canopyMesh==null)
                {
                    var sample=GameObject.CreatePrimitive(PrimitiveType.Sphere); sample.SetActive(false);
                    canopyMesh=Instantiate(sample.GetComponent<MeshFilter>().sharedMesh); canopyMesh.name="Irregular oak crown"; Destroy(sample);
                    var vertices=canopyMesh.vertices;
                    for(int v=0;v<vertices.Length;v++)
                    {
                        Vector3 p=vertices[v];
                        float variation=Mathf.PerlinNoise(p.x*8+p.z*3+17,p.y*9-p.z*4+5);
                        vertices[v]=p*(.94f+variation*.10f);
                    }
                    canopyMesh.vertices=vertices; canopyMesh.RecalculateNormals(); canopyMesh.RecalculateTangents(); canopyMesh.RecalculateBounds();
                }
                for (int i = 0; i < 58; i++)
                {
                    float x = Mathf.Sin(i * 8.13f) * ArenaSize.x * .41f, z = Mathf.Cos(i * 3.71f) * ArenaSize.y * .41f;
                    if (Mathf.Abs(x) < 12 && Mathf.Abs(z) < 14) continue;
                    float height = 4.5f + (i % 4) * .6f;
                    Shape("Ancient oak trunk", PrimitiveType.Cylinder, arenaRoot, new Vector3(x, height*.5f,z), new Vector3(.95f,height*.5f,.95f), leather, true);
                    coverFootprints.Add(new Bounds(new Vector3(x, 0,z), new Vector3(1.2f,height,1.2f)));
                    for (int crown = 0; crown < 3; crown++)
                        Shape("Oak canopy", PrimitiveType.Sphere, arenaRoot, new Vector3(x+Mathf.Sin(crown*3+i)*1.5f,height+crown*.6f,z+Mathf.Cos(crown*3+i)*1.5f), new Vector3(5.6f,3.2f,5.4f), foliage, false).GetComponent<MeshFilter>().sharedMesh=canopyMesh;
                }
                for (int i = -2; i <= 2; i++)
                    Cover("Forest ruin cover", new Vector3(i*13,1.05f,(i%2==0?1:-1)*23), new Vector3(4,2.1f,2));
            }
        }

        void SpawnRound()
        {
            ClearArmies();
            // Both sides share the same newly rolled roster to keep the match fair;
            // role ordering and clear deployment positions are shuffled independently.
            List<BattleUnitDefinition> roster = BuildRoundRoster();
            List<Vector3> bluePositions = BuildDeploymentPositions(BattleTeam.Blue);
            List<Vector3> redPositions = BuildDeploymentPositions(BattleTeam.Red);
            int count = Mathf.Min(roster.Count, Mathf.Min(bluePositions.Count, redPositions.Count));
            if (count < roster.Count)
            {
                Debug.LogWarning($"Only {count} clear deployment slots per army are available; both armies were reduced equally.", this);
                roster.RemoveRange(count, roster.Count - count);
            }
            SpawnArmy(BattleTeam.Blue, roster, bluePositions);
            SpawnArmy(BattleTeam.Red, roster, redPositions);
        }

        internal void ClearArmies()
        {
            if (unitRoot != null)
            {
                foreach (var unit in unitRoot.GetComponentsInChildren<BattleUnit>(true))
                    director.UnregisterUnit(unit);
                unitRoot.gameObject.SetActive(false);
                Destroy(unitRoot.gameObject);
            }
            unitRoot = new GameObject("Armies").transform;
            unitRoot.SetParent(transform, false);
            Physics.SyncTransforms();
        }

        internal BattleUnitDefinition[] GetDefinitions()
        {
            var definitions = new List<BattleUnitDefinition> { Vanguard, Ranger, Medic, Skirmisher, Brute, Engineer, Stormcaller };
            definitions.AddRange(additionalDefinitions);
            return definitions.ToArray();
        }

        void OnValidate() { ArmyPowerBudget = BattleDeployment.NormalizeBudget(ArmyPowerBudget); }

        List<BattleUnitDefinition> BuildRoundRoster()
        {
            var definitions = GetDefinitions();
            var originalWeights = new[] { Mathf.Clamp(VanguardsPerTeam, 1, 12), Mathf.Clamp(RangersPerTeam, 0, 12),
                Mathf.Clamp(MedicsPerTeam, 0, 6), Mathf.Clamp(SkirmishersPerTeam, 0, 8), Mathf.Clamp(BrutesPerTeam, 0, 8),
                Mathf.Clamp(EngineersPerTeam, 0, 8), Mathf.Clamp(StormcallersPerTeam, 0, 8) };
            var weights = new int[definitions.Length];
            for (int role = 0; role < weights.Length; role++) weights[role] = role < originalWeights.Length ? originalWeights[role] : 1;
            int total = 0;
            foreach (int weight in weights) total += weight;
            var roster = new List<BattleUnitDefinition>(total);
            if (!RandomizeArmies)
            {
                for (int role = 0; role < definitions.Length; role++)
                    for (int i = 0; i < weights[role]; i++) roster.Add(definitions[role]);
                return roster;
            }

            // Keep at least one of each enabled class when army size allows, then
            // vary the remaining slots using the inspector counts as relative weights.
            for (int role = 0; role < definitions.Length; role++)
                if (weights[role] > 0) roster.Add(definitions[role]);
            while (roster.Count < total)
            {
                int roll = Random.Range(0, total);
                for (int role = 0; role < definitions.Length; role++)
                {
                    roll -= weights[role];
                    if (roll >= 0) continue;
                    roster.Add(definitions[role]);
                    break;
                }
            }
            return roster;
        }

        internal List<Vector3> BuildDeploymentPositions(BattleTeam team)
        {
            float edgeX = ArenaSize.x * 0.5f - 2.1f;
            float rear = ArenaSize.y * 0.5f - 2.2f;
            const float spacing = 2.7f;
            var candidates = new List<Vector3>();
            for (float z = -rear; z <= rear; z += spacing)
            {
                for (float x = -edgeX; x <= edgeX; x += spacing)
                {
                    Vector3 position = new Vector3(x, 0f, z);
                    if (RandomizeArmies)
                        position += new Vector3(Random.Range(-0.15f, 0.15f), 0f, Random.Range(-0.15f, 0.15f));
                    if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
                    position = hit.position;
                    // NavMesh sampling can move toward an edge. Recheck the actual
                    // controller-sized capsule and separation after sampling.
                    if (Mathf.Abs(position.x) > edgeX || Mathf.Abs(position.z) > rear) continue;
                    if (Physics.CheckCapsule(position + Vector3.up * .82f, position + Vector3.up * 2.18f,
                        .76f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    candidates.Add(position);
                }
            }
            if (RandomizeArmies) Shuffle(candidates);
            return candidates;
        }

        void SpawnArmy(BattleTeam team, List<BattleUnitDefinition> roundRoster, List<Vector3> positions)
        {
            var roster = new List<BattleUnitDefinition>(roundRoster);
            if (RandomizeArmies) Shuffle(roster);
            for (int i = 0; i < roster.Count; i++) CreateUnit(roster[i], team, positions[i], i + 1);
        }

        static void Shuffle<T>(List<T> values)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int other = Random.Range(0, i + 1);
                T value = values[i];
                values[i] = values[other];
                values[other] = value;
            }
        }

        internal BattleUnit CreateUnit(BattleUnitDefinition definition, BattleTeam team, Vector3 position, int index)
        {
            GameObject root = new GameObject($"{team} {definition.DisplayName} {index}");
            root.transform.SetParent(unitRoot, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0, team == BattleTeam.Blue ? 0 : 180, 0);
            CharacterController motor = root.AddComponent<CharacterController>();
            motor.height = 1.8f;
            motor.radius = 0.42f;
            motor.center = new Vector3(0, 0.9f, 0);
            motor.stepOffset = 0.25f;
            motor.skinWidth = 0.04f;
            motor.minMoveDistance = 0;
            var model = BattleCharacterModel.Create(root.transform, definition.Role, definition.BodyScale, CharacterPalette(team));
            Transform visual = model.transform;
            BattleUnit unit = root.AddComponent<BattleUnit>();
            unit.VisualRoot = visual;
            unit.SetVisuals(visual.GetComponentsInChildren<Renderer>());
            unit.Configure(director, definition, team);
            root.AddComponent<BattleUnitPresentation>().Initialize(unit, model);
            root.AddComponent<BattleUnitAI>().Initialize(unit, director);
            return unit;
        }

        Material[] CharacterPalette(BattleTeam team)
        {
            return new[] { team == BattleTeam.Blue ? blue : red, armor, leather, trim, visor, healer, storm, frost, acid };
        }

        internal BattleCharacterModel CreatePresentationModel(UnitRole role,BattleTeam team,Transform parent)
            => BattleCharacterModel.Create(parent,role,1,CharacterPalette(team));

        /// <summary>A complete character for the creator's studio view, without motors, hitboxes or AI.</summary>
        internal Transform CreateCharacterPreview(UnitRole role, Transform parent)
        {
            float scale = 1;
            foreach (var definition in GetDefinitions())
                if (definition != null && definition.Role == role) { scale = definition.BodyScale; break; }
            return BattleCharacterModel.Create(parent, role, scale, CharacterPalette(BattleTeam.Blue)).transform;
        }
        GameObject Shape(string label, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool keepCollider)
        {
            GameObject shape = GameObject.CreatePrimitive(type);
            shape.name = label;
            shape.transform.SetParent(parent, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                Collider collider = shape.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
            }
            return shape;
        }

        void OnDestroy()
        {
            ReleaseMapMeshes();
            if(arenaRoot!=null)
            {
                var surface=arenaRoot.GetComponent<NavMeshSurface>();
                if(surface!=null && surface.navMeshData!=null) Destroy(surface.navMeshData);
            }
            for (int i = 0; i < materials.Count; i++) if (materials[i] != null) Destroy(materials[i]);
            for (int i = 0; i < characterTextures.Count; i++) if (characterTextures[i] != null) Destroy(characterTextures[i]);
            for (int i = 0; i < ownedDefinitions.Count; i++) if (ownedDefinitions[i] != null) Destroy(ownedDefinitions[i]);
            if(canopyMesh!=null) Destroy(canopyMesh);
            if(gableRoof!=null) Destroy(gableRoof);
            if(cragMesh!=null) Destroy(cragMesh);
        }
    }
}

