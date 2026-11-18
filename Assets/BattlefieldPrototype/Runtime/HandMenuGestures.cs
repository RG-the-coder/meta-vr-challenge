using UnityEngine;

namespace BattlefieldPrototype
{
    // Deliberate presentation, not a pinch or a tracking reacquisition, opens the menu.
    public sealed class PalmMenuGesture
    {
        bool armed = true;
        float started = -1;
        public bool Update(bool tracked, bool open, bool presented, float now)
        {
            if (!tracked) { started = -1; return false; }
            if (!presented) { armed = true; started = -1; return false; }
            if (!open) { started = -1; return false; }
            if (!armed) return false;
            if (started < 0) started = now;
            if (now - started < .45f) return false;
            armed = false; started = -1; return true;
        }
    }

    public sealed class HandPageSwipe
    {
        bool tracking, spent;
        Vector3 origin, previous;
        float began, settled;
        public int Update(bool allowed, Vector3 position, float now)
        {
            if (!allowed || !float.IsFinite(position.sqrMagnitude))
            { tracking = spent = false; return 0; }
            if (!tracking)
            { tracking = true; origin = previous = position; began = settled = now; return 0; }
            if (spent)
            {
                if (Vector3.Distance(previous, position) > .012f) settled = now;
                previous = position;
                if (now - settled > .35f) { spent = false; origin = position; began = now; }
                return 0;
            }
            Vector3 delta = position - origin;
            if (now - began > .8f || Mathf.Abs(delta.y) > .12f || Mathf.Abs(delta.z) > .15f)
            { origin = position; began = now; return 0; }
            if (now - began < .12f || Mathf.Abs(delta.x) < .2f) return 0;
            spent = true; previous = position; settled = now;
            return delta.x < 0 ? 1 : -1;
        }
    }
}
