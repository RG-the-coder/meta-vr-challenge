using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Deliberately squeeze both grips close together, then spread to unleash a tower wave.</summary>
    public sealed class TowerWaveGesture
    {
        bool charging, spent;
        float heldSince;
        public bool IsCharging => charging && !spent;
        public void Reset() { charging = spent = false; heldSince = 0; }
        public bool Update(bool allowed, bool grips, Vector3 left, Vector3 right, float now)
        {
            if (!allowed || !grips) { Reset(); return false; }
            if (spent) return false;
            float distance = Vector3.Distance(left, right);
            if (!charging)
            {
                if (distance <= .28f) { charging = true; heldSince = now; }
                return false;
            }
            if (now - heldSince > 2f) { charging = false; spent = true; return false; }
            if (distance < .8f || now - heldSince < .25f) return false;
            spent = true; return true;
        }
    }
}
