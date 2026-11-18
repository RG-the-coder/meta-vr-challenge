using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    public sealed class BattleDefense : MonoBehaviour
    {
        public TowerBattle Battle { get; private set; }
        public BattleTower BuiltNear { get; private set; }
        public DefenseKind Kind { get; private set; }
        public BattleTeam Owner { get; private set; }
        public float Health { get; private set; }
        public bool IsBarricade => DefenseCatalog.IsBarricade(Kind);
        public bool IsActive => !removed && isActiveAndEnabled;
        public BoxCollider Footprint { get; private set; }
        float armedAt, nextTick;
        int roundId;
        bool removed;
        NavMeshObstacle obstacle;
        Material masonry, metal;

        internal void Initialize(TowerBattle battle, DefenseKind kind, BattleTeam owner, BattleTower tower, Material stone, Material iron)
        {
            Battle = battle; Kind = kind; Owner = owner; BuiltNear = tower; masonry = stone; metal = iron;
            Health = DefenseCatalog.Health(kind); roundId = battle.Director.RoundId;
            armedAt = Time.time + 1; nextTick = armedAt;
            Vector3 size = DefenseCatalog.Size(kind);
            Footprint = gameObject.AddComponent<BoxCollider>(); Footprint.size = size; Footprint.center = Vector3.up * size.y * .5f;
            Footprint.isTrigger = !IsBarricade;
            if (IsBarricade)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = size; obstacle.center = Footprint.center;
                obstacle.carving = true; obstacle.carveOnlyStationary = false;
            }
            BuildModel();
        }

        void BuildModel()
        {
            Color timber = new Color(.32f, .19f, .09f), stone = new Color(.47f, .47f, .43f), iron = new Color(.26f, .31f, .32f);
            if (Kind == DefenseKind.TimberBarricade)
            {
                for (int i = 0; i < 11; i++) Part("Timber palisade", PrimitiveType.Cube, new Vector3(-2.4f + i * .48f, 1, 0), new Vector3(.44f, 1.92f, .52f), timber);
                for (int side = -1; side <= 1; side += 2)
                    Part("Cross brace", PrimitiveType.Cube, new Vector3(0, .55f + (side + 1) * .4f, side * .29f), new Vector3(5.3f, .2f, .12f), timber * .8f);
            }
            else if (Kind == DefenseKind.StoneBarricade)
            {
                for (int row = 0; row < 3; row++) for (int column = 0; column < 6; column++)
                    Part("Masonry block", PrimitiveType.Cube, new Vector3(-2.25f + column * .9f, .5f + row, 0),
                        new Vector3(.86f, .96f, 1.08f), stone * (1 - (column + row) % 3 * .05f));
            }
            else if (Kind == DefenseKind.SpikeBarricade)
            {
                Part("Spiked timber core", PrimitiveType.Cube, new Vector3(0, .5f, 0), new Vector3(5.35f, .85f, 1.15f), timber);
                for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 10; i++)
                    Part("Iron stake", PrimitiveType.Cube, new Vector3(-2.35f + i * .52f, .82f, side * .44f), new Vector3(.09f, .95f, .09f), iron,
                        Quaternion.Euler(side * 35, 0, 0));
            }
            else if (Kind == DefenseKind.Caltrops)
            {
                for (int x = 0; x < 4; x++) for (int z = 0; z < 4; z++) for (int arm = 0; arm < 2; arm++)
                    Part("Caltrop iron", PrimitiveType.Cube, new Vector3(-1.2f + x * .8f, .08f, -1.2f + z * .8f),
                        new Vector3(.43f, .055f, .055f), iron, Quaternion.Euler(arm == 0 ? 20 : -20, 45 + arm * 90, 0));
            }
            else
            {
                Part("Mine casing", PrimitiveType.Cylinder, new Vector3(0, .075f, 0), new Vector3(1.35f, .07f, 1.35f), iron);
                Part("Pressure plate", PrimitiveType.Cylinder, new Vector3(0, .15f, 0), new Vector3(.7f, .015f, .7f), new Color(.69f, .29f, .08f));
            }
            Vector3 size = DefenseCatalog.Size(Kind);
            Part("Friendly ownership stripe", PrimitiveType.Cube, new Vector3(0, IsBarricade ? size.y - .15f : .18f, -size.z * .5f - .015f),
                new Vector3(IsBarricade ? 1.1f : .5f, .09f, .035f), new Color(.2f, .78f, .9f));
        }

        void Part(string title, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, Quaternion? rotation = null)
        {
            var piece = GameObject.CreatePrimitive(shape); piece.name = title; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = position; piece.transform.localScale = scale; piece.transform.localRotation = rotation ?? Quaternion.identity;
            var collider = piece.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = piece.GetComponent<Renderer>(); renderer.sharedMaterial = shape == PrimitiveType.Cylinder ? metal : masonry;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        public Vector3 AttackPoint(Vector3 origin) => Footprint != null ? Footprint.ClosestPoint(origin) : transform.position;
        public bool HasClearShot(Vector3 origin)
        {
            Vector3 point = AttackPoint(origin), delta = point - origin;
            if (delta.sqrMagnitude < .0001f) return true;
            foreach (var collider in Physics.OverlapSphere(origin, .05f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.GetComponentInParent<BattleUnit>() == null) return false;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude + .02f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null && hit.collider.GetComponentInParent<BattleDefense>() != this) return false;
            return true;
        }
        public bool TakeDamage(float amount, BattleTeam attacker)
        {
            if (!IsActive || !IsBarricade || Battle == null || !Battle.Director.IsBattleRunning || attacker == Owner || !float.IsFinite(amount) || amount <= 0) return false;
            Health = Mathf.Max(0, Health - amount);
            Battle.Director.GetComponent<BattleEffects>()?.Impact(transform.position + Vector3.up, Vector3.up, new Color(.7f, .58f, .4f));
            if (Health <= 0) Remove();
            return true;
        }

        void Update()
        {
            if (removed || Battle == null) return;
            if (Battle.Director.RoundId != roundId) { Remove(); return; }
            if (!Battle.Director.IsBattleRunning || Time.time < armedAt || Time.time < nextTick) return;
            nextTick = Time.time + .25f;
            if (Kind != DefenseKind.SpikeBarricade && IsBarricade) return;
            foreach (var unit in Battle.Director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Team == Owner || unit.Definition.IsFlying || unit.transform.position.y > .8f) continue;
                float distance = Vector3.ProjectOnPlane(unit.transform.position - transform.position, Vector3.up).magnitude;
                if (Kind == DefenseKind.SpikeBarricade)
                {
                    if (TowerBattle.FootprintDistance(unit.transform.position, transform.position, transform.rotation, DefenseCatalog.Size(Kind) * .5f) <= unit.Definition.BodyRadius + .35f &&
                        HasClearShot(unit.EyePosition)) unit.TakeDamage(2, null, Vector3.zero, 0);
                }
                else if (Kind == DefenseKind.Caltrops)
                {
                    if (distance <= 2.5f && ClearBlast(unit)) unit.ApplySlow(3);
                }
                else if (distance <= 2.2f && ClearBlast(unit))
                {
                    Detonate(); return;
                }
            }
        }
        bool ClearBlast(BattleUnit unit)
        {
            Vector3 from = transform.position + Vector3.up * .25f, delta = unit.EyePosition - from;
            foreach (var hit in Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<BattleUnit>() == null && hit.collider.GetComponentInParent<BattleDefense>() != this) return false;
            return true;
        }
        void Detonate()
        {
            foreach (var unit in Battle.Director.Units)
                if (unit != null && unit.IsAlive && unit.Team != Owner && !unit.Definition.IsFlying && unit.transform.position.y <= .8f &&
                    Vector3.ProjectOnPlane(unit.transform.position - transform.position, Vector3.up).sqrMagnitude <= 16 && ClearBlast(unit))
                    unit.TakeDamage(90, null, unit.transform.position - transform.position, 3);
            Battle.Director.GetComponent<BattleEffects>()?.Pulse(transform.position, 4, new Color(1, .45f, .12f));
            Remove();
        }
        void Remove()
        {
            if (removed) return; removed = true;
            if (Footprint != null) Footprint.enabled = false;
            if (obstacle != null) obstacle.enabled = false;
            Battle?.ForgetDefense(this); BattleDirector.InvalidateSightPhysics();
            gameObject.SetActive(false); Destroy(gameObject);
        }
        void OnDestroy() { Battle?.ForgetDefense(this); }
    }
}
