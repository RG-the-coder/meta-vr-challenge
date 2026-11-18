using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    [Serializable] public sealed class BattleArmyPreset
    {
        public int version = 1;
        public string name = "Army";
        public List<BattlePresetUnit> units = new List<BattlePresetUnit>();
    }
    [Serializable] public struct BattlePresetUnit
    {
        public UnitRole role;
        public float x, z, yaw;
    }

    public sealed partial class BattleDeployment
    {
        public const int PresetSlots = 3;
        public string PresetPath(int slot) => Path.Combine(Application.persistentDataPath, "BattlefieldArmies", "army-" + Mathf.Clamp(slot, 0, PresetSlots-1) + ".json");

        public string GetPresetName(int slot)
        {
            try { return File.Exists(PresetPath(slot)) ? JsonUtility.FromJson<BattleArmyPreset>(File.ReadAllText(PresetPath(slot)))?.name : null; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException) { return null; }
        }

        public BattleArmyPreset CapturePreset(string name)
        {
            var preset = new BattleArmyPreset { name = string.IsNullOrWhiteSpace(name) ? "Army" : name.Trim() };
            if (preset.name.Length > 32) preset.name = preset.name.Substring(0,32);
            foreach (var unit in director.Units)
                if (IsBlueUnit(unit)) preset.units.Add(new BattlePresetUnit { role=unit.Definition.Role,
                    x=unit.transform.position.x/ArenaSize.x+.5f, z=unit.transform.position.z/ArenaSize.y+.5f, yaw=unit.transform.eulerAngles.y });
            return preset;
        }

        public bool SavePreset(int slot, string name)
        {
            if (!CanEdit()) return false;
            if (Count == 0) return Fail("Place at least one unit before saving an army.");
            try
            {
                string path = PresetPath(slot);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(CapturePreset(name), true));
                if (File.Exists(path)) File.Copy(temporary, path, true); else File.Move(temporary, path);
                if (File.Exists(temporary)) File.Delete(temporary);
                LastMessage = "Army saved to slot " + (slot+1) + ".";
                Changed?.Invoke();
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { return Fail("Could not save the army: " + error.Message); }
        }

        public bool LoadPreset(int slot)
        {
            if (!CanEdit()) return false;
            try
            {
                if (!File.Exists(PresetPath(slot))) return Fail("This army slot is empty. Save a formation first.");
                return TryApplyPreset(JsonUtility.FromJson<BattleArmyPreset>(File.ReadAllText(PresetPath(slot))));
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            { return Fail("Could not load the army: " + error.Message); }
        }

        public bool TryApplyPreset(BattleArmyPreset preset) => TryApplyPreset(preset, false);

        bool TryApplyPreset(BattleArmyPreset preset, bool mirrorEnemy)
        {
            if (!CanEdit()) return false;
            if (preset == null || preset.version != 1 || preset.units == null || preset.units.Count == 0 || preset.units.Count > 5000)
                return Fail("This saved army is empty or uses an unsupported format.");
            int cost = 0;
            foreach (var entry in preset.units)
            {
                if (FindDefinition(entry.role) == null || !float.IsFinite(entry.x) || !float.IsFinite(entry.z) || !float.IsFinite(entry.yaw)
                    || entry.x < 0 || entry.x > 1 || entry.z < 0 || entry.z > 1) return Fail("The saved army contains an invalid unit or position.");
                cost += GetCost(entry.role);
            }
            if (cost > Budget) return Fail($"Saved army needs {cost} Power; this battle allows {Budget}. Your current army was kept.");
            Physics.SyncTransforms();
            var positions = new List<Vector3>();
            List<Vector3> fallback = null;
            for (int index = 0; index < preset.units.Count; index++)
            {
                var entry = preset.units[index];
                var definition = FindDefinition(entry.role);
                Vector3 desired = new Vector3((entry.x-.5f)*ArenaSize.x,0,(entry.z-.5f)*ArenaSize.y);
                bool found = false;
                Vector3 snapped = default;
                for (int attempt = 0; attempt < 65 && !found; attempt++)
                {
                    float radius = attempt == 0 ? 0 : 2*(1+(attempt-1)/16);
                    float angle = (attempt-1)%16*Mathf.PI/8;
                    Vector3 point = desired + new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                    found = TryStageFormationPosition(point, definition, preset, positions, out snapped);
                }
                // Asymmetric architecture can occupy an entire mirrored formation. Search
                // the remaining map rather than dropping units or rerolling their classes.
                if (!found && mirrorEnemy)
                {
                    if (fallback == null)
                    {
                        fallback = new List<Vector3>();
                        Bounds bounds = DeploymentBounds;
                        for (float z = bounds.min.z; z <= bounds.max.z; z += 2)
                            for (float x = bounds.min.x; x <= bounds.max.x; x += 2)
                                fallback.Add(new Vector3(x, 0, z));
                    }
                    fallback.Sort((a, b) => (a-desired).sqrMagnitude.CompareTo((b-desired).sqrMagnitude));
                    foreach (var point in fallback)
                        if (TryStageFormationPosition(point, definition, preset, positions, out snapped)) { found=true; break; }
                }
                if (!found) return Fail("This formation cannot fit the current terrain and enemy positions. Your army was kept.");
                positions.Add(snapped);
            }
            ClearBlueUnits();
            for (int i=0;i<preset.units.Count;i++)
            {
                var unit=bootstrap.CreateUnit(FindDefinition(preset.units[i].role),BattleTeam.Blue,positions[i],++nextUnitId);
                unit.transform.rotation=Quaternion.Euler(0,preset.units[i].yaw,0);
            }
            Physics.SyncTransforms();
            LastMessage = mirrorEnemy
                ? $"Enemy army copied: {Count} units, {cost} / {Budget} Power. Select any character to edit."
                : $"Loaded {preset.name}: {cost} / {Budget} Power. Terrain adjustments applied where needed.";
            Changed?.Invoke();
            return true;
        }

        bool TryStageFormationPosition(Vector3 point, BattleUnitDefinition definition, BattleArmyPreset preset,
            List<Vector3> positions, out Vector3 snapped)
        {
            snapped = default;
            if (!InDeploymentZone(point) || !NavMesh.SamplePosition(point, out var hit, .65f, NavMesh.AllAreas)
                || !InDeploymentZone(hit.position) || Mathf.Abs(hit.position.y) > .5f) return false;
            point = hit.position;
            foreach (var unit in director.Units)
                if (unit != null && unit.IsAlive && unit.Team != BattleTeam.Blue && (unit.transform.position-point).sqrMagnitude <
                    Mathf.Pow(Mathf.Max(MinimumSeparation,unit.Definition.BodyRadius+definition.BodyRadius+.15f),2)) return false;
            for (int previous = 0; previous < positions.Count; previous++)
            {
                float separation = Mathf.Max(MinimumSeparation, definition.BodyRadius+FindDefinition(preset.units[previous].role).BodyRadius+.15f);
                if ((positions[previous]-point).sqrMagnitude < separation*separation) return false;
            }
            float r = definition.BodyRadius;
            foreach (var obstacle in Physics.OverlapCapsule(point+Vector3.up*(r+.08f),
                point+Vector3.up*Mathf.Max(r+.08f,definition.BodyHeight-r),r+.04f,~0,QueryTriggerInteraction.Ignore))
            {
                var unit = obstacle.GetComponentInParent<BattleUnit>();
                if (unit == null || unit.Team != BattleTeam.Blue) return false;
            }
            snapped = point;
            return true;
        }
    }
}
