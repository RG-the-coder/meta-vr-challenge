using UnityEngine;
using UnityEngine.Rendering;

namespace BattlefieldPrototype
{
    public readonly struct BattleFlare
    {
        public readonly BattleTeam Team;
        public readonly Vector3 Position;
        public readonly int Sequence;
        public readonly float LaunchedAt;
        public BattleFlare(BattleTeam team, Vector3 position, int sequence, float time)
        { Team = team; Position = position; Sequence = sequence; LaunchedAt = time; }
    }

    /// <summary>One signal per team per match. Only the signal position is public.</summary>
    public sealed class BattleFlares : MonoBehaviour
    {
        readonly BattleFlare[] signals = new BattleFlare[2];
        readonly bool[] spent = new bool[2];
        readonly LineRenderer[] beams = new LineRenderer[2], rings = new LineRenderer[2];
        BattleDirector director;
        Material material;
        int sequence;
        float nextEnemyDecision;
        public bool HasCharge(BattleTeam team) => !spent[(int)team];
        public bool TryGetFlare(BattleTeam team, out BattleFlare signal)
        { signal = signals[(int)team]; return signal.Sequence > 0; }
        public bool TryGetLatest(out BattleFlare signal)
        { signal = signals[0].Sequence > signals[1].Sequence ? signals[0] : signals[1]; return signal.Sequence > 0; }

        public void Initialize(BattleDirector owner)
        {
            if (director == owner) return;
            if (director != null) director.RoundReset -= ResetMatch;
            director = owner;
            if (director != null) director.RoundReset += ResetMatch;
            ResetMatch();
        }

        public bool TryUsePlayerFlare(PossessionController player)
        {
            if (director == null || player == null || player != director.GetComponent<PossessionController>()) return false;
            var unit = player.CurrentUnit;
            if (unit == null || !unit.IsAlive || unit.Director != director || unit.Team != BattleTeam.Blue) return false;
            return Launch(BattleTeam.Blue, unit.transform.position);
        }

        bool Launch(BattleTeam team, Vector3 position)
        {
            if (director == null || !director.IsBattleRunning || spent[(int)team] ||
                float.IsNaN(position.sqrMagnitude) || float.IsInfinity(position.sqrMagnitude)) return false;
            spent[(int)team] = true;
            signals[(int)team] = new BattleFlare(team, position, ++sequence, director.BattleDuration);
            CreateBeacon(signals[(int)team]);
            return true;
        }

        void ResetMatch()
        {
            sequence = 0; nextEnemyDecision = 12;
            for (int team = 0; team < 2; team++)
            {
                spent[team] = false; signals[team] = default;
                if (beams[team] != null) beams[team].gameObject.SetActive(false);
                if (rings[team] != null) rings[team].gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (director == null) return;
            bool active = director.IsBattleRunning;
            for (int team = 0; team < 2; team++)
            {
                if (beams[team] == null) continue;
                beams[team].gameObject.SetActive(active && signals[team].Sequence > 0);
                rings[team].gameObject.SetActive(active && signals[team].Sequence > 0);
                if (!active) continue;
                float age = director.BattleDuration - signals[team].LaunchedAt;
                beams[team].widthMultiplier = .11f + .025f * Mathf.Sin(age * 4);
                rings[team].widthMultiplier = .06f + .015f * Mathf.Sin(age * 4);
            }
            if (!active || !HasCharge(BattleTeam.Red) || director.BattleDuration < nextEnemyDecision) return;
            nextEnemyDecision = director.BattleDuration + 2;
            TryEnemySignal();
        }

        bool TryEnemySignal()
        {
            if (director == null || !director.IsBattleRunning || !HasCharge(BattleTeam.Red) || director.BattleDuration < 12) return false;
            if (TryGetLatest(out var latest) && director.BattleDuration - latest.LaunchedAt < 18) return false;
            int living = 0, idle = 0, rallying = 0;
            Vector3 center = Vector3.zero;
            foreach (var unit in director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Red) continue;
                living++;
                var ai = unit.GetComponent<BattleUnitAI>();
                if (unit.IsPossessed || ai != null && (ai.HasEnemyMemory || ai.State == BattleTacticalState.Fighting || ai.State == BattleTacticalState.Pursuing)) continue;
                idle++; center += unit.transform.position;
                if (ai != null && ai.State == BattleTacticalState.Rallying) rallying++;
            }
            if (idle < Mathf.Max(1, Mathf.CeilToInt(living * .6f)) || rallying > Mathf.Max(0, living / 3)) return false;
            center /= idle;
            BattleUnit launcher = null;
            float best = float.PositiveInfinity;
            foreach (var unit in director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Red || unit.IsPossessed || unit.HealthFraction < .35f) continue;
                var ai = unit.GetComponent<BattleUnitAI>();
                if (ai != null && ai.HasEnemyMemory || director.FindPriorityEnemy(unit) != null) continue;
                float score = (unit.transform.position - center).sqrMagnitude + (1 - unit.HealthFraction) * 100;
                if (score < best) { best = score; launcher = unit; }
            }
            // Only red positions, red observations, and elapsed time inform this choice.
            if (launcher != null && latest.Sequence > 0 && (launcher.transform.position - latest.Position).sqrMagnitude < 144) return false;
            return launcher != null && Launch(BattleTeam.Red, launcher.transform.position);
        }

        void CreateBeacon(BattleFlare flare)
        {
            if (material == null)
            {
                var shader = Resources.Load<Shader>("BattlefieldEffects");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null) material = new Material(shader) { hideFlags = HideFlags.DontSave };
            }
            int team = (int)flare.Team;
            Color tint = team == 0 ? new Color(.2f, .9f, 1) : new Color(1, .38f, .08f);
            if (beams[team] == null)
            {
                beams[team] = CreateLine(flare.Team + " signal beam", material);
                rings[team] = CreateLine(flare.Team + " signal ring", material);
            }
            var beam = beams[team]; beam.gameObject.SetActive(true); beam.positionCount = 3;
            beam.SetPosition(0, flare.Position + Vector3.up * .2f);
            beam.SetPosition(1, flare.Position + Vector3.up * 12);
            beam.SetPosition(2, flare.Position + Vector3.up * 20);
            beam.startColor = tint; beam.endColor = tint * new Color(1, 1, 1, .15f); beam.widthMultiplier = .12f;
            var ring = rings[team]; ring.gameObject.SetActive(true); ring.positionCount = 49;
            ring.startColor = ring.endColor = tint; ring.widthMultiplier = .06f;
            for (int i = 0; i < 49; i++)
            {
                float angle = i * Mathf.PI / 24;
                ring.SetPosition(i, flare.Position + new Vector3(Mathf.Cos(angle) * 2, .08f, Mathf.Sin(angle) * 2));
            }
        }

        LineRenderer CreateLine(string label, Material shared)
        {
            var obj = new GameObject(label, typeof(LineRenderer)); obj.transform.SetParent(transform, false);
            var line = obj.GetComponent<LineRenderer>(); line.sharedMaterial = shared; line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            line.numCapVertices = 3; line.numCornerVertices = 3;
            return line;
        }
        void OnDestroy()
        {
            if (director != null) director.RoundReset -= ResetMatch;
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
