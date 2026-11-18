using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed class BattleProjectile : MonoBehaviour
    {
        private const float Radius = 0.1f;
        private BattleDirector director;
        private BattleUnit source;
        private BattleTeam team;
        private Vector3 direction;
        private float damage;
        private float speed;
        private float remainingDistance;
        private bool spawnChecked;
        private int roundId;
        private Renderer projectileRenderer;
        private float distanceTravelled;
        private float gravity;
        private Vector3 velocity;

        public Vector3 Direction => direction;
        public Vector3 SpawnOrigin { get; private set; }

        internal static void SpawnSummitArrow(BattleDirector battle, Vector3 origin, Vector3 direction, float power, Color color)
        {
            if (battle == null || direction.sqrMagnitude < .001f) return;
            power = Mathf.Clamp(power, .2f, 1);
            SpawnInternal(battle, null, BattleTeam.Blue, origin, direction, TowerBattle.SummitBowDamage * power,
                Mathf.Lerp(45, 110, power), 230, color, 9.81f);
        }

        public static void Spawn(BattleDirector battle, BattleUnit owner, Vector3 origin,
            Vector3 aimDirection, float hitDamage, float travelSpeed, float range, Color color)
        {
            if (battle == null || owner == null || aimDirection.sqrMagnitude < 0.001f) return;
            SpawnInternal(battle, owner, owner.Team, origin, aimDirection, hitDamage, travelSpeed, range, color);
        }

        public static void SpawnForTeam(BattleDirector battle, BattleTeam team, Vector3 origin,
            Vector3 direction, float damage, float speed, float range, Color color)
        {
            if (battle == null || direction.sqrMagnitude < .001f || !float.IsFinite(direction.sqrMagnitude)) return;
            SpawnInternal(battle, null, team, origin, direction, damage, speed, range, color);
        }

        static void SpawnInternal(BattleDirector battle, BattleUnit owner, BattleTeam team, Vector3 origin,
            Vector3 aimDirection, float hitDamage, float travelSpeed, float range, Color color,
            float gravity = 0)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = owner != null ? owner.Definition.DisplayName + " Projectile" : "Summit bow arrow";
            visual.transform.position = origin;
            visual.transform.localScale = new Vector3(.065f, .065f, .25f);
            visual.transform.rotation = Quaternion.LookRotation(aimDirection.normalized);
            visual.transform.SetParent(battle.transform, true);
            var collider = visual.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = visual.GetComponent<Renderer>();
            var ownerVisual = owner != null && owner.VisualRoot != null ? owner.VisualRoot.GetComponentInChildren<Renderer>(true) : null;
            if (ownerVisual != null) renderer.sharedMaterial = ownerVisual.sharedMaterial;
            else if (battle.GetComponent<BattlefieldBootstrap>()?.PrimitiveMaterial != null)
                renderer.sharedMaterial = battle.GetComponent<BattlefieldBootstrap>().PrimitiveMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Keep the physical trajectory exactly on the aim ray. Only hide its
            // initial visual while it is inside the possessed camera's near plane.
            renderer.enabled = owner != null && !owner.IsPossessed;

            var projectile = visual.AddComponent<BattleProjectile>();
            projectile.director = battle;
            projectile.source = owner;
            projectile.team = team;
            projectile.direction = aimDirection.normalized;
            projectile.damage = hitDamage;
            projectile.speed = Mathf.Max(0.1f, travelSpeed);
            projectile.gravity = gravity;
            projectile.velocity = projectile.direction * projectile.speed;
            projectile.remainingDistance = Mathf.Max(0.1f, range);
            projectile.roundId = battle.RoundId;
            projectile.projectileRenderer = renderer;
            projectile.SpawnOrigin = origin;
        }

        private void Update()
        {
            if (director == null || !director.IsBattleRunning || director.RoundId != roundId)
            {
                Destroy(gameObject);
                return;
            }

            // SphereCast omits colliders already overlapping its origin.
            if (!spawnChecked)
            {
                spawnChecked = true;
                BattleUnit overlappingVictim = null;
                bool blockedByWorld = false;
                foreach (var collider in Physics.OverlapSphere(transform.position, Radius, ~0, QueryTriggerInteraction.Ignore))
                {
                    var unit = collider.GetComponentInParent<BattleUnit>();
                    if (unit != null && (unit == source || !unit.IsAlive || unit.Team == team)) continue;
                    if (unit == null)
                    {
                        var tower = collider.GetComponentInParent<BattleTower>();
                        if (tower != null) tower.TakeDamage(damage, team);
                        var defense = collider.GetComponentInParent<BattleDefense>();
                        if (defense != null) defense.TakeDamage(damage, team);
                        blockedByWorld = true; break;
                    }
                    overlappingVictim = unit;
                }
                // Overlap ordering is unspecified. A wall touching the spawn sphere
                // must block the shot even if an enemy overlaps the other side.
                if (blockedByWorld || overlappingVictim != null)
                {
                    if (!blockedByWorld) overlappingVictim.TakeDamage(damage, source, direction);
                    else director.GetComponent<BattleEffects>()?.Impact(transform.position, -direction, new Color(.67f, .60f, .46f));
                    Destroy(gameObject);
                    return;
                }
            }

            float dt = Time.deltaTime;
            Vector3 displacement = velocity * dt + Vector3.down * (.5f * gravity * dt * dt);
            velocity += Vector3.down * (gravity * dt);
            float step = Mathf.Min(displacement.magnitude, remainingDistance);
            if (step <= 0f) return;
            direction = displacement.normalized;
            if (gravity > 0) transform.rotation = Quaternion.LookRotation(velocity);
            // Sweeping the entire traveled segment avoids tunneling even at low frame rates.
            var hits = Physics.SphereCastAll(transform.position, Radius, direction, step, ~0, QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            BattleUnit victim = null;
            BattleTower victimTower = null;
            BattleDefense victimDefense = null;
            Vector3 impactPoint = Vector3.zero, impactNormal = Vector3.up;
            foreach (var hit in hits)
            {
                var unit = hit.collider.GetComponentInParent<BattleUnit>();
                if (unit != null && (unit == source || !unit.IsAlive || unit.Team == team)) continue;
                if (hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                victim = unit;
                victimTower = hit.collider.GetComponentInParent<BattleTower>();
                victimDefense = hit.collider.GetComponentInParent<BattleDefense>();
                impactPoint = hit.point;
                impactNormal = hit.normal;
            }

            if (!float.IsPositiveInfinity(nearestDistance))
            {
                transform.position += direction * nearestDistance;
                if (victim != null) victim.TakeDamage(damage, source, direction);
                else if (victimTower != null) victimTower.TakeDamage(damage, team);
                else if (victimDefense != null) victimDefense.TakeDamage(damage, team);
                else director.GetComponent<BattleEffects>()?.Impact(impactPoint, impactNormal, new Color(.67f, .60f, .46f));
                Destroy(gameObject);
                return;
            }

            transform.position += direction * step;
            distanceTravelled += step;
            if (projectileRenderer != null && distanceTravelled >= 0.35f) projectileRenderer.enabled = true;
            remainingDistance -= step;
            if (remainingDistance <= 0.001f) Destroy(gameObject);
        }
    }
}
