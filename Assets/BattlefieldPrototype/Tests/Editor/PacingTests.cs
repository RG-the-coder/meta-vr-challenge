using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BattlefieldPrototype.Tests
{
    public sealed partial class TowerBattleTests
    {
        [UnityTest] public IEnumerator PacingSampleGood() => SamplePacing(0, 173);
        [UnityTest] public IEnumerator PacingSampleDecent() => SamplePacing(1, 173);
        [UnityTest] public IEnumerator PacingSamplePoor() => SamplePacing(2, 173);
        [UnityTest] public IEnumerator PacingSampleGoodAlternate() => SamplePacing(0, 911);
        [UnityTest] public IEnumerator PacingSampleDecentAlternate() => SamplePacing(1, 911);
        [UnityTest] public IEnumerator PacingSamplePoorAlternate() => SamplePacing(2, 911);
        IEnumerator SamplePacing(int skill, int seed)
        {
            Random.InitState(seed); battle.NewGame(); player.enabled = false; hud.enabled = false;
            foreach (var camera in root.GetComponentsInChildren<Camera>()) camera.enabled = false;
            float oldMaximum = Time.maximumDeltaTime; Time.maximumDeltaTime = .1f; Time.timeScale = 12;
            float nextBuy = skill == 2 ? 30 : 0, nextShot = 0;
            UnitRole[][] plans = {
                new[] { UnitRole.Ranger, UnitRole.Vanguard, UnitRole.Medic, UnitRole.Bombard, UnitRole.Stormwing },
                new[] { UnitRole.Vanguard, UnitRole.Ranger, UnitRole.Medic, UnitRole.Vanguard, UnitRole.Sentinel },
                new[] { UnitRole.Vanguard, UnitRole.Vanguard, UnitRole.Militia, UnitRole.Medic, UnitRole.Brute }
            };
            int purchases = 0; float wallStart = Time.realtimeSinceStartup;
            try
            {
                while (director.IsBattleRunning && director.BattleDuration < 660 && Time.realtimeSinceStartup - wallStart < 180)
                {
                    float now = director.BattleDuration;
                    if (now >= nextBuy)
                    {
                        nextBuy = now + (skill == 0 ? 2 : skill == 1 ? 9 : 24);
                        if (battle.BlueGold >= 35 + battle.Definition(plans[skill][purchases % plans[skill].Length]).DeploymentCost &&
                            battle.TryQuickRecruit(plans[skill][purchases % plans[skill].Length], out _)) purchases++;
                    }
                    if (now >= nextShot)
                    {
                        nextShot = now + (skill == 0 ? 1.5f : skill == 1 ? 4 : 12);
                        BattleUnit closest = null; float range = skill == 2 ? 22 : skill == 1 ? 40 : 65;
                        foreach (var unit in director.Units)
                        {
                            if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Red) continue;
                            float distance = Vector3.Distance(player.ViewCamera.transform.position, unit.AimPosition);
                            if (distance < range) { closest = unit; range = distance; }
                        }
                        if (closest != null) battle.TryFireEasyBow(new Ray(player.ViewCamera.transform.position, closest.AimPosition - player.ViewCamera.transform.position));
                    }
                    yield return null;
                }
                string line = $"skill={skill} seed={seed} map={battle.Map} seconds={director.BattleDuration:0.0} outcome={director.Phase} bought={purchases} blue={director.AliveBlue} red={director.AliveRed} gold={battle.BlueGold:0.0} redGold={battle.RedGold:0.0} towers={battle.OwnedCount(BattleTeam.Blue)}/{battle.OwnedCount(BattleTeam.Red)} wall={Time.realtimeSinceStartup-wallStart:0.0}";
                TestContext.WriteLine(line);
                string folder = System.Environment.GetEnvironmentVariable("BATTLEFIELD_PACING_DIR");
                if (!string.IsNullOrEmpty(folder)) { Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "Pacing.txt"), line + "\n"); }
                Assert.That(director.BattleDuration, Is.GreaterThan(30));
            }
            finally { Time.timeScale = 1; Time.maximumDeltaTime = oldMaximum; }
        }
    }
}
