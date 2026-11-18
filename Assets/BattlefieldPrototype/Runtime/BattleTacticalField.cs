using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    // Enemy knowledge belongs to its observer. Only patrol coverage is shared.
    // Fixed storage and lazy decay keep this cheap for large armies.
    public sealed class BattleTacticalField
    {
        const int Cells = 20;
        readonly Dictionary<BattleUnit, float[]> contacts = new Dictionary<BattleUnit, float[]>();
        readonly float[,] visited = new float[2, Cells * Cells];
        readonly BattleDirector director;
        public BattleTacticalField(BattleDirector owner) { director = owner; Clear(); }
        public void Clear()
        {
            contacts.Clear();
            for (int team = 0; team < 2; team++) for (int i = 0; i < Cells * Cells; i++)
                visited[team, i] = float.NegativeInfinity;
        }
        public Vector2 MapSize
        {
            get { var map = director.GetComponent<BattlefieldBootstrap>(); return map != null ? map.ArenaSize : new Vector2(160, 192); }
        }
        int Cell(Vector3 position)
        {
            Vector2 size = MapSize;
            Vector3 p = position - director.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x / size.x + .5f) * Cells), 0, Cells - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z / size.y + .5f) * Cells), 0, Cells - 1);
            return z * Cells + x;
        }
        public void RecordContact(BattleUnit observer, BattleUnit target)
        {
            if (observer == null || target == null || observer.Team == target.Team || !observer.CanSee(target)) return;
            if (!contacts.TryGetValue(observer, out var contact))
            {
                contact = new float[Cells * Cells];
                for (int i = 0; i < contact.Length; i++) contact[i] = float.NegativeInfinity;
                contacts.Add(observer, contact);
            }
            contact[Cell(target.transform.position)] = Time.time;
        }
        public void Forget(BattleUnit observer) { if (observer != null) contacts.Remove(observer); }
        public void Visit(BattleTeam team, Vector3 position) => visited[(int)team, Cell(position)] = Time.time;
        public float Heat(BattleUnit observer, Vector3 position)
        {
            if (observer == null || !contacts.TryGetValue(observer, out var contact)) return 0;
            int cell = Cell(position), x = cell % Cells, z = cell / Cells;
            float heat = 0;
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                if (x + dx < 0 || x + dx >= Cells || z + dz < 0 || z + dz >= Cells) continue;
                float age = Time.time - contact[(z + dz) * Cells + x + dx];
                heat = Mathf.Max(heat, Mathf.Clamp01(1 - age / 14f) / (1 + dx * dx + dz * dz));
            }
            return heat;
        }
        public float RecentVisit(BattleTeam team, Vector3 position) => Mathf.Clamp01(1 - (Time.time - visited[(int)team, Cell(position)]) / 22f);
        public Vector3 ClampToMap(Vector3 position, float margin = 3)
        {
            Vector2 half = MapSize * .5f - Vector2.one * margin;
            Vector3 center = director.transform.position;
            position.x = Mathf.Clamp(position.x, center.x - half.x, center.x + half.x);
            position.z = Mathf.Clamp(position.z, center.z - half.y, center.z + half.y);
            return position;
        }
        public static int HealingSlot(UnitRole role) => role == UnitRole.Medic || role == UnitRole.Engineer ? 0 :
            role == UnitRole.Alchemist || role == UnitRole.Paladin ? 1 : role == UnitRole.Warden ? 2 : -1;
        public static bool IsProtector(UnitRole role) => role == UnitRole.Vanguard || role == UnitRole.Sentinel ||
            role == UnitRole.Paladin || role == UnitRole.Warden || role == UnitRole.Halberdier;
    }
}
