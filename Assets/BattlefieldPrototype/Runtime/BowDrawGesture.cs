using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed class BowDrawGesture
    {
        bool needsRelease;
        public const float RestDistance = .12f;
        public const float FullDraw = .4f;
        public const float MinimumPull = .025f;
        public const float NockReach = .35f;
        float startingDistance;
        public bool Drawing { get; private set; }
        public float Pull { get; private set; }
        public float StringDistance { get; private set; } = RestDistance;
        public static bool CanNock(Vector3 bow, Vector3 stringHand) => float.IsFinite(bow.sqrMagnitude) &&
            float.IsFinite(stringHand.sqrMagnitude) && (stringHand - bow).sqrMagnitude <= NockReach * NockReach;
        public void Cancel() { Drawing = false; Pull = 0; StringDistance = RestDistance; needsRelease = true; }
        public bool Update(bool allowed, bool held, Vector3 bow, Vector3 stringHand, out float power)
            => Update(allowed, held, bow, stringHand, Vector3.forward, out power);

        public bool Update(bool allowed, bool held, Vector3 bow, Vector3 stringHand, Vector3 forward, out float power)
        {
            power = 0;
            if (!allowed || !float.IsFinite(bow.sqrMagnitude) || !float.IsFinite(stringHand.sqrMagnitude) ||
                !float.IsFinite(forward.sqrMagnitude) || forward.sqrMagnitude < .001f) { Cancel(); return false; }
            if (needsRelease) { if (!held) needsRelease = false; return false; }
            float distance = Vector3.Distance(bow, stringHand);
            // Grip orientation differs between controllers and optical palms. Use
            // the hands themselves for drawing and aiming, including tilted wrists.
            if (held && !Drawing && CanNock(bow, stringHand))
            { Drawing = true; startingDistance = distance; }
            bool fire = false;
            if (Drawing)
            {
                if (distance > 1.2f)
                { Cancel(); return false; }
                if (held)
                {
                    StringDistance = distance;
                    Pull = Mathf.Clamp(distance - startingDistance, 0, .55f);
                }
                if (!held)
                {
                    fire = Pull >= MinimumPull;
                    power = Mathf.Lerp(.2f, 1, Mathf.Clamp01(Pull / FullDraw));
                    Drawing = false; Pull = 0;
                }
            }
            return fire;
        }
    }
}
