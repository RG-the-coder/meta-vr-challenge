using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>One toggle per deliberate, held hands-together gesture.</summary>
    public sealed class TwoHandHudGesture
    {
        const float CloseDistance = .24f;
        const float ReleaseDistance = .38f;
        const float HoldSeconds = .45f;
        float closeSince = -1;
        bool spent;

        public void Reset() { closeSince = -1; spent = false; }

        public bool Update(bool allowed, Vector3 left, Vector3 right, float now)
        {
            if (!allowed) { closeSince = -1; spent = true; return false; }
            float distance = Vector3.Distance(left, right);
            if (distance >= ReleaseDistance) { Reset(); return false; }
            if (spent) return false;
            if (distance > CloseDistance) { closeSince = -1; return false; }
            if (closeSince < 0) { closeSince = now; return false; }
            if (now - closeSince < HoldSeconds) return false;
            spent = true;
            return true;
        }
    }
}
