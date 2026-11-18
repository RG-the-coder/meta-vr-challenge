using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BattlefieldPrototype
{
    /// <summary>Local real-time tower economy. Both sides buy, build and capture under the same rules.</summary>
    public sealed partial class TowerBattle : MonoBehaviour
    {
        public BattlefieldBootstrap Terrain { get; private set; }
        public BattleDirector Director { get; private set; }
        public PossessionController Player { get; private set; }
        public IReadOnlyList<BattleTower> Towers => towers;
        public IReadOnlyList<BattleUnitDefinition> Definitions => definitions;
        public BattleTower BoundTower { get; private set; }
        public BattleTower EnemyBoundTower { get; private set; }
        public BattleTower SelectedTower { get; set; }
        public bool IsMounted { get; private set; } = true;
        public bool CanDeploy => Director != null && Director.IsBattleRunning && Player.CurrentUnit == null && !IsMounted &&
            BoundTower != null && BoundTower.Owner == BattleTeam.Blue && !BoundTower.IsBroken;
        public string Message { get; private set; } = "Raise your towers. Defend your tether.";
        public float BlueGold => wallets[0];
        public float RedGold => wallets[1];
        public const int StartingGold = 60;
        public const float MaximumIncome = 1.8f;
        public float ScoutRadius => BoundTower != null ? BoundTower.ScoutRadius : 0;
        public float DeploymentRadius => BoundTower != null && !BoundTower.IsBroken ? BoundTower.DeploymentRadius : 0;
        readonly float[] wallets = new float[2];
        readonly List<BattleTower> towers = new List<BattleTower>();
        BattleUnitDefinition[] definitions;
        Transform towerRoot;
        Material masonry, metal;
        float enemyRecruitAt, enemyBuildAt, enemyTravelAt;
        int nextId;
        public UnitRole? Recruitment { get; private set; }

        internal void Initialize(BattlefieldBootstrap terrain, BattleDirector director, PossessionController player)
        {
            Terrain = terrain; Director = director; Player = player; definitions = terrain.GetDefinitions();
            Director.Towers = this;
            var shader = terrain.PrimitiveMaterial != null ? terrain.PrimitiveMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            masonry = new Material(shader) { color = new Color(.49f, .46f, .39f), name = "Tower limestone" };
            metal = new Material(shader) { color = new Color(.52f, .41f, .25f), name = "Tower heraldry" };
        }

        public float Gold(BattleTeam team) => wallets[(int)team];
        public float Income(BattleTeam team)
        { float income = 0; foreach (var tower in towers) if (tower.Owner == team) income += tower.Income; return Mathf.Min(MaximumIncome, income); }
        public int OwnedCount(BattleTeam team)
        { int count = 0; foreach (var tower in towers) if (tower.Owner == team && !tower.IsBroken) count++; return count; }
        public bool TrySpend(BattleTeam team, int amount)
        {
            if (amount < 0 || wallets[(int)team] + .0001f < amount) return false;
            wallets[(int)team] = Mathf.Max(0, wallets[(int)team] - amount); return true;
        }
        public void Report(string message) => Message = message;
        public bool Fail(string message) { Message = message; return false; }

        void ClearTowers()
        {
            CancelRoutePlanning();
            ClearDefenses();
            BoundTower = EnemyBoundTower = SelectedTower = null; towers.Clear();
            if (towerRoot != null) { towerRoot.gameObject.SetActive(false); Destroy(towerRoot.gameObject); }
            towerRoot = new GameObject("Live tower network").transform; towerRoot.SetParent(transform, false);
        }

        public void GoHome()
        {
            Director.ResetRound(BattlePhase.Home, () => { Terrain.ClearArmies(); ClearTowers(); Terrain.EnsureTowerArena(); CreateTowers(); });
            Player.SetMenuView(new Vector3(0, 48, -Terrain.ArenaSize.y * .47f), Quaternion.Euler(36, 0, 0));
            Recruitment = null; Report("Raise your towers. Defend your tether.");
        }

        public void NewGame()
        {
            wallets[0] = wallets[1] = StartingGold; nextId = 0; Recruitment = null; IsMounted = true;
            ResetSummitPowers();
            enemyRecruitAt = 8; enemyBuildAt = 75; enemyTravelAt = 30;
            Director.ResetRound(BattlePhase.Fighting, () =>
            {
                Terrain.ClearArmies(); ClearTowers(); ResetSquadSummoning(); Terrain.EnsureTowerArena(); CreateTowers();
            });
            Player.EnterTowerView(BoundTower, true);
            Report("Open SUMMON. Spend gold to summon allies, with no living-soldier limit. Your towers collect more gold automatically.");
        }

        void CreateTowers()
        {
            foreach (var spawn in TowerMapCatalog.Spawns(Map)) AddTower(spawn.Title,spawn.Position,spawn.Owner,spawn.Height);
            BoundTower = SelectedTower = towers[0]; EnemyBoundTower = towers[1];
            foreach (var tower in towers) tower.Refresh();
            Physics.SyncTransforms();
        }
        void AddTower(string title, Vector2 point, BattleTeam? owner, float height)
        {
            var obj = new GameObject(title); obj.transform.SetParent(towerRoot, false);
            obj.transform.position = new Vector3(point.x * Terrain.ArenaSize.x, 0, point.y * Terrain.ArenaSize.y);
            var tower = obj.AddComponent<BattleTower>(); tower.Initialize(this, title, owner, height, masonry, metal); towers.Add(tower);
        }

        public bool Teleport(BattleTower tower)
        {
            if (!CanUseSummitActions) return Fail("Return to the top of your standing tower before changing your binding.");
            if (!Director.IsBattleRunning || tower == null || !towers.Contains(tower) || tower.IsBroken || tower.Owner != BattleTeam.Blue)
                return Fail("Teleport to one of your standing towers.");
            CancelRoutePlanning();
            BoundTower = SelectedTower = tower; IsMounted = true; Recruitment = null; SelectedSummitPower = null; SelectedDefense = null;
            Player.EnterTowerView(tower, true);
            foreach (var item in towers) item.Refresh();
            Report("Bound to " + tower.Title + ". Capture of this tower ends the battle."); return true;
        }
        public void TeleportNext()
        {
            int start = towers.IndexOf(BoundTower);
            for (int offset = 1; offset <= towers.Count; offset++)
            {
                var tower = towers[(start + offset + towers.Count) % towers.Count];
                if (tower.Owner == BattleTeam.Blue && !tower.IsBroken) { Teleport(tower); return; }
            }
        }
        public void ToggleMount()
        {
            if (!Director.IsBattleRunning || BoundTower == null) return;
            if (Player.CurrentUnit != null) { ReturnToTower(); return; }
            CancelRoutePlanning();
            IsMounted = !IsMounted; Recruitment = null; SelectedSummitPower = null; SelectedDefense = null; Player.EnterTowerView(BoundTower, IsMounted);
            Report(IsMounted ? "Summit ready: bow, Repulse [1], Lightning [2], Restoration [3], Tower Wave [4]." : "Ground command: recruit inside cyan rings, or choose barricades and traps for the larger gold build zones.");
        }
        internal bool BindToUnit(BattleUnit unit)
        {
            if (!Director.IsBattleRunning || unit == null || !unit.IsAlive || unit.Team != BattleTeam.Blue || unit.SpawnTower == null ||
                unit.SpawnTower.Owner != BattleTeam.Blue) return false;
            if (unit.SpawnTower != BoundTower && !CanUseSummitActions)
                return Fail("Return to your summit before possessing a character from a different tower.");
            CancelRoutePlanning();
            BoundTower = unit.SpawnTower; SelectedTower = BoundTower; IsMounted = false; Recruitment = null; SelectedSummitPower = null; SelectedDefense = null;
            foreach (var item in towers) item.Refresh(); return true;
        }
        public void ReturnToTower()
        {
            if (!Director.IsBattleRunning || BoundTower == null) return;
            CancelRoutePlanning();
            IsMounted = true; Recruitment = null; SelectedSummitPower = null; SelectedDefense = null; Player.EnterTowerView(BoundTower, true);
        }
        public void ChooseRecruit(UnitRole? role)
        {
            Recruitment = null;
            if (role.HasValue) Report("Use SUMMON to buy an ally. Towers collect gold automatically.");
        }

        public BattleUnitDefinition Definition(UnitRole role)
        { foreach (var item in definitions) if (item.Role == role) return item; return null; }

        public bool TryDeploy(UnitRole role, Vector3 point, out BattleUnit unit)
        {
            unit = null;
            if (!CanDeploy) return Fail("Use the SUMMON cards to buy your preset allies from your tower.");
            if (!TryRecruit(BattleTeam.Blue, BoundTower, role, point, out unit)) return false;
            Report(unit.Definition.DisplayName + " deployed from " + BoundTower.Title + ". Recruitment remains selected."); return true;
        }
        internal bool TryRecruit(BattleTeam team, BattleTower tower, UnitRole role, Vector3 point, out BattleUnit unit)
        {
            unit = null;
            return Fail("Use the SUMMON cards to recruit allies. Cards can be used repeatedly whenever you have enough gold.");
        }

        public bool TryPlacement(BattleTower tower, BattleUnitDefinition definition, Vector3 point, out Vector3 position, out string reason)
        {
            position = point; reason = "Choose clear ground inside your bound tower's deployment ring.";
            if (tower == null || tower.IsBroken || definition == null || !float.IsFinite(point.sqrMagnitude)) return false;
            point.y = 0;
            if (Vector3.ProjectOnPlane(point - tower.transform.position, Vector3.up).sqrMagnitude > tower.DeploymentRadius * tower.DeploymentRadius ||
                Mathf.Abs(point.x) > Terrain.ArenaSize.x * .5f - 2 || Mathf.Abs(point.z) > Terrain.ArenaSize.y * .5f - 2) return false;
            if (!NavMesh.SamplePosition(point, out var hit, .65f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y) > .5f) return false;
            position = hit.position;
            if (Vector3.ProjectOnPlane(position - tower.transform.position, Vector3.up).sqrMagnitude > tower.DeploymentRadius * tower.DeploymentRadius) return false;
            float radius = definition.BodyRadius;
            foreach (var other in towers)
                if (Vector3.ProjectOnPlane(position - other.transform.position, Vector3.up).sqrMagnitude < 27) return false;
            foreach (var other in Director.Units)
            {
                if (other == null || !other.IsAlive) continue;
                float spacing = Mathf.Max(1.3f, radius + other.Definition.BodyRadius + .15f);
                if (Vector3.ProjectOnPlane(other.transform.position - position, Vector3.up).sqrMagnitude < spacing * spacing) return false;
            }
            Physics.SyncTransforms();
            if (Physics.OverlapCapsule(position + Vector3.up * (radius + .09f), position + Vector3.up * Mathf.Max(radius + .09f, definition.BodyHeight - radius), radius + .04f, ~0, QueryTriggerInteraction.Ignore).Length > 0) return false;
            reason = ""; return true;
        }

        public bool HasTroopsNear(BattleTower tower, BattleTeam team)
        {
            foreach (var unit in Director.Units)
                if (unit != null && unit.IsAlive && unit.Team == team &&
                    Vector3.ProjectOnPlane(unit.transform.position - tower.transform.position, Vector3.up).sqrMagnitude < 100 &&
                    unit.transform.position.y < 5) return true;
            return false;
        }
        internal void OnTowerCaptured(BattleTower tower, BattleTeam? previous)
        {
            if (tower == BoundTower && tower.Owner != BattleTeam.Blue) Director.EndTowerBattle(false);
            else if (tower == EnemyBoundTower && tower.Owner == BattleTeam.Blue) Director.EndTowerBattle(true);
        }
        internal void OnTowerDestroyed(BattleTower tower)
        {
            if (tower == BoundTower) Director.EndTowerBattle(false);
            else if (tower == EnemyBoundTower) Director.EndTowerBattle(true);
        }
        public bool IsScouted(Vector3 point)
        {
            if (BoundTower == null || !float.IsFinite(point.sqrMagnitude) ||
                Vector3.ProjectOnPlane(point - BoundTower.transform.position, Vector3.up).sqrMagnitude > ScoutRadius * ScoutRadius) return false;
            Vector3 delta = point - BoundTower.Summit; float distance = delta.magnitude;
            if (distance < .01f) return true;
            var ray = new Ray(BoundTower.Summit, delta / distance);
            foreach (var wall in Terrain.TowerSightBlockers)
                if (wall.IntersectRay(ray, out float hit) && hit < distance - .1f) return false;
            return true;
        }
        public BattleTower Objective(BattleUnit unit)
        {
            BattleTower best = null; float score = float.PositiveInfinity;
            foreach (var tower in towers)
            {
                if (tower.Owner == unit.Team && !tower.IsBroken) continue;
                // Do not park a squad at a ruin it cannot rebuild. Keep attacking
                // until its economy can afford the capture.
                if (tower.IsBroken && Gold(unit.Team) < BattleTower.CaptureCost) continue;
                float candidate = Vector3.ProjectOnPlane(tower.transform.position - unit.transform.position, Vector3.up).magnitude;
                if (tower.IsBroken) candidate *= .7f;
                else if (unit.Definition.IsFlying && !tower.CanBeRaidedBy(unit.Definition)) candidate *= 1.65f;
                if (tower == (unit.Team == BattleTeam.Blue ? EnemyBoundTower : BoundTower)) candidate *= .85f;
                if (candidate < score) { best = tower; score = candidate; }
            }
            return best;
        }
        void Update()
        {
            if (Director == null || !Director.IsBattleRunning) return;
            wallets[0] += Income(BattleTeam.Blue) * Time.deltaTime; wallets[1] += Income(BattleTeam.Red) * Time.deltaTime;
            foreach (var tower in towers) { tower.TickCapture(Time.deltaTime); if (!Director.IsBattleRunning) return; }

            // Reinforcements remain available; capturing a tower ends the match.
            float clock = Director.BattleDuration;
            if (clock >= enemyRecruitAt)
            {
                enemyRecruitAt = clock + 8;
                TryEnemyReinforcement();
            }
            if (clock >= enemyTravelAt)
            {
                enemyTravelAt = clock + 12;
                if (EnemyBoundTower == null || EnemyBoundTower.Height < 3)
                {
                    BattleTower safest = null;
                    foreach (var tower in towers) if (tower.Owner == BattleTeam.Red && !tower.IsBroken && (safest == null || tower.Height > safest.Height)) safest = tower;
                    if (safest != null) { EnemyBoundTower = safest; foreach (var tower in towers) tower.Refresh(); }
                }
            }
            if (clock >= enemyBuildAt)
            {
                enemyBuildAt = clock + 30;
                BattleTower choice = null;
                foreach (var tower in towers)
                    if (tower.Owner == BattleTeam.Red && !tower.IsBroken && tower.Height < 21 && Gold(BattleTeam.Red) > tower.BuildCost + 50 &&
                        (choice == null || tower.Height < choice.Height)) choice = tower;
                if (choice != null) choice.TryBuild(BattleTeam.Red);
            }

        }
        void OnDestroy()
        {
            if (Director != null && Director.Towers == this) Director.Towers = null;
            if (masonry != null) Destroy(masonry); if (metal != null) Destroy(metal);
        }
    }

    public sealed partial class BattlefieldBootstrap
    {
        void BuildTowerTerrain()
        {
            if (TowerMap != TowerMapKind.OpenMarch) { BuildAlternateTowerTerrain(); return; }
            // Open, connected lanes keep foundations reachable and summit flight readable.
            for (int side = -1; side <= 1; side += 2)
                for (int row = -1; row <= 1; row += 2)
                {
                    Cover("Broken curtain wall", new Vector3(side * ArenaSize.x * .13f, 1.5f, row * ArenaSize.y * .21f), new Vector3(8, 3, 2));
                    Cover("Roadside barricade", new Vector3(side * ArenaSize.x * .38f, 1, row * ArenaSize.y * .29f), new Vector3(4, 2, 6));
                }
        }
    }
}
