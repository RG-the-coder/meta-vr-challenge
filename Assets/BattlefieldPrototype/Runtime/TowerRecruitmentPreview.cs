using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        float nextRecruitmentPreviewAt;
        Vector3 recruitmentPreviewPoint;
        Vector3 recruitmentPreviewCandidate;
        bool recruitmentPreviewValid;
        UnitRole? previewRole;

        void UpdateRecruitmentPreview()
        {
            recruitmentPreview.enabled = false;
            if (!battle.CanDeploy || !battle.Recruitment.HasValue || BlocksGameplayInput ||
                battle.SelectedDefense.HasValue || battle.IsPlanningRoute || !TryRecruitmentPreviewPoint(out var candidate))
            { nextRecruitmentPreviewAt = 0; return; }
            var definition = battle.Definition(battle.Recruitment.Value);
            if (definition == null) return;
            // Track moved targets immediately, and reuse near-identical placement checks briefly.
            if (Time.unscaledTime >= nextRecruitmentPreviewAt || previewRole != battle.Recruitment ||
                (candidate - recruitmentPreviewCandidate).sqrMagnitude > .01f)
            {
                nextRecruitmentPreviewAt = Time.unscaledTime + .08f; previewRole = battle.Recruitment;
                recruitmentPreviewCandidate = candidate;
                recruitmentPreviewValid = battle.TryPlacement(battle.BoundTower, definition, candidate, out var position, out _);
                recruitmentPreviewPoint = recruitmentPreviewValid ? position : candidate;
            }
            bool valid = recruitmentPreviewValid && battle.BlueGold >= definition.DeploymentCost;
            Color tint = valid ? new Color(.25f, 1, .55f) : new Color(1, .28f, .18f);
            recruitmentPreview.startColor = recruitmentPreview.endColor = tint;
            recruitmentPreview.enabled = true;
            float radius = Mathf.Max(.7f, definition.BodyRadius + .25f);
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                recruitmentPreview.SetPosition(i, recruitmentPreviewPoint + new Vector3(Mathf.Sin(angle) * radius, .12f, Mathf.Cos(angle) * radius));
            }
        }

        bool TryRecruitmentPreviewPoint(out Vector3 point)
        {
            point = default;
            if (PointerOverUI || ControllerOverUI)
            {
                if (hovered == null || hovered.GetComponentInParent<TowerMinimapGraphic>() != map || pointer == null) return false;
                Vector2 local;
                if (pointer is BattleUiPointerEventData worldPointer && worldPointer.HasWorldPosition)
                    local = map.rectTransform.InverseTransformPoint(worldPointer.WorldPosition);
                else if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(map.rectTransform, pointer.position,
                    world ? player.ViewCamera : null, out local)) return false;
                point = map.Unproject(local); return true;
            }
            if (!Physics.Raycast(player.AimRay, out var hit, 300, ~0, QueryTriggerInteraction.Ignore)) return false;
            point = hit.point; return true;
        }
    }
}
