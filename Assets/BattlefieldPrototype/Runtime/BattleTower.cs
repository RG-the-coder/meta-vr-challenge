using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    /// <summary>A tower's remaining height is its health, income, reach and camera ceiling.</summary>
    [DefaultExecutionOrder(-60)]
    public sealed partial class BattleTower : MonoBehaviour
    {
        public const float StoreyHeight = 3f;
        public const float MaximumHeight = 36f;
        public const float HealthPerMetre = 220f;
        public const int CaptureCost = 35;
        public const float CaptureSeconds = 8f;
        public const float Radius = 3.2f;
        public string Title { get; private set; }
        public BattleTeam? Owner { get; private set; }
        public float Height { get; private set; }
        public bool IsBroken => Height <= .001f;
        public float Health => Height * HealthPerMetre;
        public float DeploymentRadius => 12f + Height * 2.25f;
        public float ScoutRadius => 28f + Height * 4f;
        public float Income => IsBroken || !Owner.HasValue ? 0 : .09f * (1f + Height / StoreyHeight);
        public int BuildCost => IsBroken ? CaptureCost : 18 + Mathf.CeilToInt(Height / StoreyHeight) * 8;
        public float CaptureProgress { get; private set; }
        public BattleTeam? CapturingTeam { get; private set; }
        public Vector3 Summit => transform.position + Vector3.up * (Height + 1.7f);
        public TowerBattle Battle { get; private set; }
        public bool CanBeRaidedBy(BattleUnitDefinition definition)
            => !IsBroken && definition != null && definition.IsFlying && transform.position.y + Height <= definition.MaximumFlightHeight + .001f;
        Transform shaft, crown, flag, beacon;
        Renderer shaftRenderer, crownRenderer, flagRenderer;
        MaterialPropertyBlock properties;
        BattleUnit captor;
        float captorDamageAt;
        bool heightRefreshPending, hasTint;
        float renderedHeight = -1, nextImpactAt;
        BattleTeam? renderedOwner;
        internal int GeometryRefreshCount { get; private set; }

        internal void Initialize(TowerBattle battle, string title, BattleTeam? owner, float height, Material masonry, Material metal)
        {
            Battle = battle; Title = title; Owner = owner; Height = Mathf.Clamp(height, 0, MaximumHeight);
            properties = new MaterialPropertyBlock();
            var plinth = Piece("Octagonal foundation", PrimitiveType.Cylinder, transform, new Vector3(0, .22f, 0), new Vector3(7.6f, .22f, 7.6f), masonry, true);
            shaft = Piece("Height / structural health", PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, masonry, true).transform;
            shaftRenderer = shaft.GetComponent<Renderer>();
            crown = new GameObject("Summit battlements").transform; crown.SetParent(transform, false);
            var platform = Piece("Crown platform", PrimitiveType.Cylinder, crown, Vector3.zero, new Vector3(7.3f, .22f, 7.3f), metal, false);
            crownRenderer = platform.GetComponent<Renderer>();
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI / 6;
                var merlon = Piece("Crenellation", PrimitiveType.Cube, crown, new Vector3(Mathf.Sin(angle) * 3.25f, .55f, Mathf.Cos(angle) * 3.25f), new Vector3(.72f, .8f, .65f), masonry, false);
                merlon.transform.localRotation = Quaternion.Euler(0, i * 30, 0);
            }
            flag = Piece("Banner", PrimitiveType.Cube, crown, new Vector3(-1.7f, 2.3f, -1.7f), new Vector3(1.7f, .85f, .07f), metal, false).transform;
            flagRenderer = flag.GetComponent<Renderer>();
            Piece("Banner mast", PrimitiveType.Cylinder, crown, new Vector3(-2.6f, 1.4f, -1.7f), new Vector3(.09f, 1.4f, .09f), metal, false);
            beacon = Piece("Bound commander beacon", PrimitiveType.Sphere, crown, new Vector3(0, 4, 0), Vector3.one * .8f, metal, false).transform;
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.radius = 3.9f; obstacle.height = 2;
            obstacle.center = Vector3.up;
            obstacle.carving = true; obstacle.carveOnlyStationary = false;
            Refresh();
        }

        static GameObject Piece(string title, PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Material material, bool solid)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = title; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) { var collider = obj.GetComponent<Collider>(); collider.enabled = false; Destroy(collider); }
            return obj;
        }

        public bool TakeDamage(float amount, BattleTeam attacker)
        {
            if (Battle == null || !Battle.Director.IsBattleRunning || IsBroken || Owner == attacker ||
                !float.IsFinite(amount) || amount <= 0) return false;
            float previousHeight = Height;
            Height = Mathf.Max(0, Height - amount / HealthPerMetre);
            Battle.GetComponent<TowerBattleHud>()?.MoveMenuWithRaisedTower(this, Height - previousHeight);
            ResetCapture(); heightRefreshPending = true;
            if (Time.time >= nextImpactAt)
            {
                nextImpactAt = Time.time + .08f;
                Battle.Director.GetComponent<BattleEffects>()?.Impact(AttackPoint(transform.position + Vector3.forward * 10 + Vector3.up), Vector3.forward, new Color(1, .65f, .23f));
            }
            if (IsBroken)
            {
                Battle.Report(Title + " has fallen.");
                Battle.OnTowerDestroyed(this);
            }
            return true;
        }

        public bool TryBuild(BattleTeam team)
        {
            if (Battle == null || !Battle.Director.IsBattleRunning) return false;
            bool rebuilding = IsBroken;
            if (!rebuilding && Owner != team) return Battle.Fail("Break this tower first, or send a flyer to its summit.");
            if (Height >= MaximumHeight - .01f) return Battle.Fail("This tower has reached its maximum height.");
            if (rebuilding && (!Battle.HasTroopsNear(this, team) || Battle.HasTroopsNear(this, team == BattleTeam.Blue ? BattleTeam.Red : BattleTeam.Blue)))
                return Battle.Fail("Secure the foundation with your troops before rebuilding.");
            int cost = BuildCost;
            if (!Battle.TrySpend(team, cost)) return Battle.Fail("Not enough gold to build this tower.");
            var previous = Owner;
            float previousHeight = Height;
            Owner = team; Height = rebuilding ? StoreyHeight : Mathf.Min(MaximumHeight, Height + StoreyHeight);
            ResetCapture(); Refresh();
            if (previous != team || rebuilding) Battle.OnTowerCaptured(this, previous);
            Battle.Report(Title + (rebuilding ? " rebuilt." : " raised by 3 metres."));
            Battle.GetComponent<TowerBattleHud>()?.MoveMenuWithRaisedTower(this, Height - previousHeight);
            return true;
        }

        public Vector3 AttackPoint(Vector3 from)
        {
            Vector3 delta = Vector3.ProjectOnPlane(from - transform.position, Vector3.up).normalized;
            if (delta.sqrMagnitude < .01f) delta = Vector3.forward;
            return transform.position + delta * (Radius - .12f) + Vector3.up * Mathf.Clamp(from.y - transform.position.y, .65f, Mathf.Max(.65f, Height - .2f));
        }

        internal void TickCapture(float deltaTime)
        {
            if (IsBroken) { ResetCapture(); return; }
            BattleUnit candidate = null; bool contested = false;
            foreach (var unit in Battle.Director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.IsHandControlled || !CanBeRaidedBy(unit.Definition) ||
                    Vector3.ProjectOnPlane(unit.transform.position - transform.position, Vector3.up).sqrMagnitude > 36 ||
                    Mathf.Abs(unit.transform.position.y - Height - transform.position.y) > 1.5f ||
                    unit.transform.position.y > unit.Definition.MaximumFlightHeight + .1f) continue;
                if (Owner == unit.Team) { contested = true; continue; }
                if (candidate != null && candidate.Team != unit.Team) { contested = true; break; }
                if (candidate == null || unit == captor) candidate = unit;
            }
            if (candidate == null || contested) { ResetCapture(); return; }
            if (candidate != captor || candidate.LastDamagedAt > captorDamageAt)
            {
                captor = candidate; captorDamageAt = candidate.LastDamagedAt; CaptureProgress = 0;
            }
            CapturingTeam = candidate.Team;
            // A damaged raider must establish a fresh foothold at the summit.
            if (Time.time - candidate.LastDamagedAt < .5f) { CaptureProgress = 0; return; }
            CaptureProgress = Mathf.Min(1, CaptureProgress + Mathf.Max(0, deltaTime) / CaptureSeconds);
            if (CaptureProgress < 1 || !Battle.TrySpend(candidate.Team, CaptureCost)) return;
            var previous = Owner; Owner = candidate.Team;
            ResetCapture(); Refresh(); Battle.OnTowerCaptured(this, previous);
            Battle.Report(Title + " captured from the air with its height intact.");
        }

        void ResetCapture() { captor = null; CapturingTeam = null; CaptureProgress = 0; }
        void LateUpdate() { if (heightRefreshPending) Refresh(); }
        internal void Refresh()
        {
            if (shaft == null) return;
            heightRefreshPending = false;
            if (!Mathf.Approximately(renderedHeight, Height))
            {
                renderedHeight = Height; GeometryRefreshCount++;
                shaft.localPosition = Vector3.up * Mathf.Max(.3f, Height * .5f);
                shaft.localScale = new Vector3(Radius * 2, Mathf.Max(.3f, Height * .5f), Radius * 2);
                crown.localPosition = Vector3.up * Mathf.Max(.5f, Height);
                crown.gameObject.SetActive(!IsBroken);
                BattleDirector.InvalidateSightPhysics();
            }
            if (!hasTint || renderedOwner != Owner)
            {
                hasTint = true; renderedOwner = Owner;
                Color tint = !Owner.HasValue ? new Color(.54f, .51f, .43f) : Owner == BattleTeam.Blue ? new Color(.15f, .48f, .6f) : new Color(.63f, .23f, .16f);
                SetTint(shaftRenderer, Color.Lerp(tint, new Color(.42f, .42f, .36f), .45f));
                SetTint(crownRenderer, tint); SetTint(flagRenderer, tint * 1.7f);
            }
            beacon.gameObject.SetActive(Battle != null && (Battle.BoundTower == this || Battle.EnemyBoundTower == this));
        }
        void SetTint(Renderer renderer, Color tint)
        { properties.Clear(); properties.SetColor("_BaseColor", tint); properties.SetColor("_Color", tint); renderer.SetPropertyBlock(properties); }
    }
}
