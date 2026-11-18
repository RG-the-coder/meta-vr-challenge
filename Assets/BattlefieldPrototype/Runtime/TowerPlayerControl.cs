using UnityEngine;
using UnityEngine.InputSystem;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BattlefieldPrototype
{
    public sealed partial class PossessionController
    {
        bool towerPrimaryHeld, towerPowerPressConsumed;
        BattleTower viewHeightTower;
        float viewTowerHeight;
        internal bool IsInGroundCommandView => !switchPending && CurrentUnit == null && viewCamera != null &&
            viewCamera.transform.position.y >= 1.2f && viewCamera.transform.position.y <= 3.1f;
        internal bool IsAtTowerSummit(BattleTower tower) => !switchPending && CurrentUnit == null && viewCamera != null && tower != null &&
            Mathf.Abs(viewCamera.transform.position.y - tower.Summit.y) <= (IsUsingXR ? 1.5f : .65f) &&
            Vector3.ProjectOnPlane(viewCamera.transform.position - tower.transform.position, Vector3.up).sqrMagnitude <= BattleTower.Radius * BattleTower.Radius;
        public void EnterTowerView(BattleTower tower, bool mounted)
        {
            if (tower == null) return;
            ReleaseAllHandUnits(); bowDraw.Cancel(); desktopBowHeld = false;
            Vector3 point = mounted ? tower.Summit + Vector3.forward * 2.6f : tower.transform.position + Vector3.forward * 9 + Vector3.up * 2.4f;
            SetMenuView(point, Quaternion.Euler(mounted ? 18 : 16, 0, 0));
            GetComponent<TowerBattleHud>()?.ReanchorAfterTravel();
            viewHeightTower = tower; viewTowerHeight = tower.Height;
            menuViewApplied = menuPitchedCamera = false;
            mouseLookSuspended = true;
            towerPrimaryHeld = true; towerPowerPressConsumed = true;
        }
        void ApplyTowerViewLimits()
        {
            var towers = director != null ? director.Towers : null;
            if (towers == null || !director.IsBattleRunning || CurrentUnit != null || towers.BoundTower == null) return;
            Vector3 eye = viewCamera.transform.position, center = towers.BoundTower.transform.position;
            Vector3 flat = Vector3.ProjectOnPlane(eye - center, Vector3.up);
            flat.x = Mathf.Clamp(center.x + flat.x, -towers.Terrain.ArenaSize.x * .5f + 1, towers.Terrain.ArenaSize.x * .5f - 1) - center.x;
            flat.z = Mathf.Clamp(center.z + flat.z, -towers.Terrain.ArenaSize.y * .5f + 1, towers.Terrain.ArenaSize.y * .5f - 1) - center.z;
            if (towers.IsMounted) flat = Vector3.ClampMagnitude(flat, BattleTower.Radius - .2f);
            float ceiling = towers.BoundTower.Summit.y;
            // Preserve roomscale head bob/crouching. Replacing eye height every
            // tracking update makes world objects appear attached to the head.
            float y = towers.IsMounted ? IsUsingXR && viewHeightTower == towers.BoundTower
                ? eye.y + towers.BoundTower.Height - viewTowerHeight : ceiling
                : Mathf.Clamp(eye.y, 1.7f, Mathf.Max(1.7f, ceiling));
            rigRoot.position += center + flat + Vector3.up * (y - center.y) - eye;
            // The tower damage/build path translates the panel by the same height change.
            viewHeightTower = towers.BoundTower; viewTowerHeight = towers.BoundTower.Height;
        }
        void HandleTowerPrimary()
        {
            var siege = director.Towers;
            if (!director.IsBattleRunning) return;
            if (siege.SelectedDefense.HasValue)
            {
                if (towerPrimaryHeld) return;
                towerPrimaryHeld = true;
                if (siege.TryDefenseAimPoint(AimRay, out var placement))
                    siege.TryPlaceDefense(siege.SelectedDefense.Value, placement, siege.DefenseRotation, out _);
                return;
            }
            if (siege.IsMounted)
            {
                if (towerPowerPressConsumed) return;
                if (siege.SelectedSummitPower.HasValue)
                {
                    if (towerPrimaryHeld) return;
                    towerPrimaryHeld = towerPowerPressConsumed = true;
                    if (siege.TryUseSummitPower(siege.SelectedSummitPower.Value, AimRay)) PulseRight(.55f, .12f); return;
                }
                // Bow shots are released by the draw interaction, never by holding attack.
                return;
            }
            // Deployment is one purchase per press, including a held XR trigger.
            if (towerPrimaryHeld) return;
            towerPrimaryHeld = true;
            if (siege.SelectedDefense.HasValue)
            {
                if (Physics.Raycast(AimRay, out var ground, 300, ~0, QueryTriggerInteraction.Ignore))
                    siege.TryPlaceDefense(siege.SelectedDefense.Value, ground.point, siege.DefenseRotation, out _);
                return;
            }
            if (!siege.Recruitment.HasValue) return;
            if (Physics.Raycast(AimRay, out var hit, 300, ~0, QueryTriggerInteraction.Ignore))
                siege.TryDeploy(siege.Recruitment.Value, hit.point, out _);
        }
        void UpdateTowerPrimaryLatch()
        {
            bool desktop = !OpticalInputOwnsPointer && Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool controller = IsUsingXR && RightTriggerHeld;
            bool optical = IsUsingXR && OpticalPinchHeld(false);
            if (!desktop && !controller && !optical) towerPrimaryHeld = towerPowerPressConsumed = false;
        }

        void HandleTowerPowers(bool primary, bool secondary, int extraMask)
        {
            if (director == null || director.Towers == null || switchPending || CurrentUnit != null) return;
            if (primary && director.Towers.TryUseSummitPower(0, AimRay)) PulseRight(.45f, .12f);
            if (secondary && director.Towers.TryUseSummitPower(1, AimRay)) PulseBoth(.65f, .16f);
            if ((extraMask & (1 << 2)) != 0 && director.Towers.TryUseSummitPower(2, AimRay)) PulseBoth(.3f, .18f);
            if ((extraMask & (1 << 3)) != 0 && director.Towers.TryTowerPulse()) PulseBoth(.85f, .22f);
        }
    }
}
