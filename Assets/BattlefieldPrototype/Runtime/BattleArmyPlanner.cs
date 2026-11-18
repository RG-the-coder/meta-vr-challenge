using System.Collections.Generic;
using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Random exact-budget armies with enough slots reserved to finish the remainder.</summary>
    public static class BattleArmyPlanner
    {
        public static bool TryBuild(IReadOnlyList<BattleUnitDefinition> definitions, int power, int maxUnits,
            out List<BattleUnitDefinition> roster)
            => TryBuildInternal(definitions, power, maxUnits, -1, out roster);

        public static bool TryBuildStrategic(IReadOnlyList<BattleUnitDefinition> definitions, int power, int clearSlots,
            int doctrine, out List<BattleUnitDefinition> roster)
            => TryBuildInternal(definitions, power, clearSlots, Mathf.Clamp(doctrine, 0, 3), out roster);

        public static string DoctrineName(int doctrine) => doctrine == 1 ? "Ranged company" : doctrine == 2 ? "Flanking pack"
            : doctrine == 3 ? "Arcane battery" : "Shield wall";

        static readonly float[,] weights = {
            { 8, 3, 4, 2, 5, 3, 2, .08f, 1, 1, 2, 6, 3, 1, 1, 5, 7, 2, 2, 3, 5, 1, 5, 5, 2, 2, 4, 2, 3 },
            { 4, 9, 4, 5, 1, 4, 2, .08f, 3, 1, 5, 3, 1, 2, 1, 2, 3, 1, 4, 4, 3, 9, 1, 3, 5, 4, 3, 6, 2 },
            { 2, 4, 3, 9, 1, 2, 2, .08f, 2, 8, 1, 1, 1, 2, 8, 2, 2, 9, 2, 3, 3, 3, 6, 2, 9, 5, 2, 3, 2 },
            { 4, 2, 5, 1, 2, 4, 9, .15f, 1, 1, 5, 4, 2, 8, 1, 4, 2, 1, 8, 7, 4, 2, 2, 6, 2, 7, 7, 9, 6 }
        };

        static float DoctrineWeight(int doctrine, UnitRole role)
            => (int)role >= 0 && (int)role < weights.GetLength(1) ? weights[doctrine, (int)role] : 1;

        static bool TryBuildInternal(IReadOnlyList<BattleUnitDefinition> definitions, int power, int maxUnits, int doctrine,
            out List<BattleUnitDefinition> roster)
        {
            roster = new List<BattleUnitDefinition>();
            if (power < 0 || maxUnits < 0 || definitions == null) return false;
            var minimumUnits = new int[power + 1];
            for (int amount = 1; amount <= power; amount++)
            {
                minimumUnits[amount] = power + 1;
                foreach (var definition in definitions)
                {
                    int cost = definition != null ? definition.DeploymentCost : 0;
                    if (cost > 0 && cost <= amount)
                        minimumUnits[amount] = Mathf.Min(minimumUnits[amount], minimumUnits[amount - cost] + 1);
                }
            }
            if (minimumUnits[power] > maxUnits) return false;
            var choices = new List<BattleUnitDefinition>();
            int remaining = power;
            while (remaining > 0)
            {
                choices.Clear();
                foreach (var definition in definitions)
                {
                    int cost = definition != null ? definition.DeploymentCost : 0;
                    if (cost > 0 && cost <= remaining && minimumUnits[remaining - cost] <= maxUnits - roster.Count - 1)
                        choices.Add(definition);
                }
                // Feasibility above guarantees at least one choice at every step.
                var selected = choices[Random.Range(0, choices.Count)];
                if (doctrine >= 0)
                {
                    float total = 0;
                    foreach (var choice in choices) total += DoctrineWeight(doctrine, choice.Role);
                    float roll = Random.value * total;
                    foreach (var choice in choices)
                    {
                        roll -= DoctrineWeight(doctrine, choice.Role);
                        if (roll <= 0) { selected = choice; break; }
                    }
                }
                roster.Add(selected);
                remaining -= selected.DeploymentCost;
            }
            return true;
        }
    }
}
