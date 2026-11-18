using System;
using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Sweeps the rendered head against scenery without moving any battle unit.</summary>
    internal sealed class CameraCollisionSolver : IDisposable
    {
        private const float Skin = 0.015f;
        private const int MaxIterations = 8;
        private RaycastHit[] hits = new RaycastHit[32];
        private Collider[] overlaps = new Collider[32];
        private readonly SphereCollider probe;
        private bool hasPosition;
        private Vector3 lastPosition;

        public CameraCollisionSolver()
        {
            var obj = new GameObject("Camera collision probe") { hideFlags = HideFlags.HideAndDontSave };
            probe = obj.AddComponent<SphereCollider>();
            probe.isTrigger = true;
            probe.enabled = false;
        }

        public Vector3 Resolve(Vector3 desired, float radius, Transform rig, bool teleport)
        {
            probe.radius = radius;
            Vector3 position = ResolveOverlaps(hasPosition && !teleport ? lastPosition : desired, radius, rig);
            if (hasPosition && !teleport)
                position = SweepAndSlide(position, desired - position, radius, rig);
            hasPosition = true;
            lastPosition = position;
            return position;
        }

        public Vector3 ConstrainMovement(Vector3 safeEye, Vector3 movement, float radius, Transform rig)
        {
            probe.radius = radius;
            Vector3 start = ResolveOverlaps(safeEye, radius, rig);
            return SweepAndSlide(start, movement, radius, rig) - start;
        }

        private Vector3 SweepAndSlide(Vector3 position, Vector3 remaining, float radius, Transform rig)
        {
            for (int iteration = 0; iteration < MaxIterations && remaining.sqrMagnitude > 0.000001f; iteration++)
            {
                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                if (!FindObstacle(position, radius, direction, distance + Skin, rig, out var hit))
                {
                    position += remaining;
                    break;
                }
                float travel = Mathf.Clamp(hit.distance - Skin, 0f, distance);
                position += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, hit.normal);
            }
            return ResolveOverlaps(position, radius, rig);
        }

        private bool FindObstacle(Vector3 position, float radius, Vector3 direction, float distance,
            Transform rig, out RaycastHit closest)
        {
            int count;
            while (true)
            {
                count = Physics.SphereCastNonAlloc(position, radius, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            closest = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!IsWorldSolid(hit.collider, rig) || hit.distance >= nearest) continue;
                closest = hit;
                nearest = hit.distance;
            }
            return nearest < float.PositiveInfinity;
        }

        private Vector3 ResolveOverlaps(Vector3 position, float radius, Transform rig)
        {
            // ComputePenetration requires enabled colliders. The probe is a trigger,
            // excluded by the queries, and is disabled again before gameplay resumes.
            probe.enabled = true;
            try
            {
                for (int iteration = 0; iteration < MaxIterations; iteration++)
                {
                    int count;
                    while (true)
                    {
                        count = Physics.OverlapSphereNonAlloc(position, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
                        if (count < overlaps.Length) break;
                        Array.Resize(ref overlaps, overlaps.Length * 2);
                    }
                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    {
                        var other = overlaps[i];
                        if (!IsWorldSolid(other, rig)) continue;
                        if (!Physics.ComputePenetration(probe, position, Quaternion.identity,
                            other, other.transform.position, other.transform.rotation, out var normal, out float depth)) continue;
                        position += normal * (depth + Skin);
                        moved = true;
                    }
                    if (!moved) break;
                }
                return position;
            }
            finally
            {
                probe.enabled = false;
            }
        }

        private static bool IsWorldSolid(Collider collider, Transform rig)
        {
            return collider != null && collider.enabled && !collider.isTrigger &&
                (rig == null || !collider.transform.IsChildOf(rig)) &&
                collider.GetComponentInParent<BattleUnit>() == null;
        }

        public void Dispose()
        {
            if (probe == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(probe.gameObject);
            else UnityEngine.Object.DestroyImmediate(probe.gameObject);
        }
    }
}
