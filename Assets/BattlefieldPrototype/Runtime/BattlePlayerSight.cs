using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        bool sightRestricted, savedFog;
        float savedFarClip, savedFogStart, savedFogEnd;
        FogMode savedFogMode;
        readonly System.Collections.Generic.Dictionary<BattleUnit, bool> sightCache = new System.Collections.Generic.Dictionary<BattleUnit, bool>();
        int sightFrame = -1;
        int sightGeometryVersion = -1;
        BattleUnit sightOwner;
        Vector3 sightEye, sightForward;
        public bool HasLimitedSight => isActiveAndEnabled && CurrentUnit != null && CurrentUnit.IsAlive && director != null && director.IsBattleRunning;
        public float ViewSightRange => HasLimitedSight ? CurrentUnit.SightRange : float.PositiveInfinity;
        public bool IsWithinViewDistance(BattleUnit other)
        {
            if (other == null || !other.IsAlive) return false;
            if (!HasLimitedSight || other == CurrentUnit) return true;
            return viewCamera != null && (other.AimPosition - viewCamera.transform.position).sqrMagnitude <= ViewSightRange * ViewSightRange;
        }

        public bool CanSeeUnit(BattleUnit other)
        {
            if (other == null || !other.IsAlive) return false;
            if (!HasLimitedSight || other == CurrentUnit) return true;
            if (viewCamera == null) return false;
            Vector3 eye = viewCamera.transform.position, forward = viewCamera.transform.forward;
            if (sightFrame != Time.frameCount || sightGeometryVersion != BattleDirector.SightGeometryVersion || sightOwner != CurrentUnit || (sightEye - eye).sqrMagnitude > .04f ||
                Vector3.Dot(sightForward, forward) < .999f)
            {
                sightFrame = Time.frameCount; sightOwner = CurrentUnit; sightEye = eye; sightForward = forward;
                sightGeometryVersion = BattleDirector.SightGeometryVersion;
                sightCache.Clear();
            }
            if (!sightCache.TryGetValue(other, out bool visible))
            {
                visible = CurrentUnit.CanSeeFrom(other, eye, forward);
                sightCache[other] = visible;
            }
            return visible;
        }

        public bool CanSeeMapPoint(Vector3 position)
        {
            if (!HasLimitedSight || viewCamera == null) return true;
            Vector3 eye = viewCamera.transform.position;
            position.y = eye.y;
            return CurrentUnit.CanSeePositionFrom(position, eye, viewCamera.transform.forward);
        }

        void UpdateSight()
        {
            if (viewCamera == null || director == null) return;
            if (HasLimitedSight)
            {
                if (!sightRestricted)
                {
                    savedFarClip = viewCamera.farClipPlane;
                    savedFog = RenderSettings.fog;
                    savedFogMode = RenderSettings.fogMode;
                    savedFogStart = RenderSettings.fogStartDistance;
                    savedFogEnd = RenderSettings.fogEndDistance;
                    sightRestricted = true;
                }
                viewCamera.farClipPlane = Mathf.Max(viewCamera.nearClipPlane + .1f, ViewSightRange);
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = ViewSightRange * .78f;
                RenderSettings.fogEndDistance = ViewSightRange;
                foreach (var other in director.Units)
                    // The depth buffer handles walls and partial cover. Awareness
                    // must not switch an entire visible model off at its chest point.
                    if (other != null) other.SetObservedByPlayer(IsWithinViewDistance(other));
            }
            else RestoreSight();
        }

        void RestoreSight()
        {
            if (!sightRestricted) { RestoreWorldBodies(); return; }
            if (viewCamera != null) viewCamera.farClipPlane = savedFarClip;
            var scenery = director != null ? director.GetComponent<BattlefieldScenery>() : null;
            // Scenery restores the scene's original lighting when its root disables.
            if (scenery == null || scenery.isActiveAndEnabled)
            {
                RenderSettings.fog = savedFog;
                RenderSettings.fogMode = savedFogMode;
                RenderSettings.fogStartDistance = savedFogStart;
                RenderSettings.fogEndDistance = savedFogEnd;
            }
            sightRestricted = false;
            sightCache.Clear(); sightOwner = null;
            RestoreWorldBodies();
        }
        void RestoreWorldBodies()
        { if (director != null) foreach (var other in director.Units) if (other != null) other.SetObservedByPlayer(true); }
    }
}
