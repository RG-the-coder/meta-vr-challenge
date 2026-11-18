using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        LineRenderer bowTrajectory, bowImpact;
        Canvas bowPowerCanvas;
        Text bowPowerText;
        readonly Vector3[] trajectoryPoints = new Vector3[161];
        readonly RaycastHit[] trajectoryHits = new RaycastHit[64];

        void CreateBowGuide()
        {
            bowTrajectory = MakeLine("Bow trajectory", new Color(1, .82f, .3f), .012f);
            bowImpact = MakeLine("Bow landing marker", new Color(1, .82f, .3f), .025f);
            bowImpact.loop = true; bowImpact.positionCount = 32;
            bowPowerCanvas = new GameObject("Bow draw feedback", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            bowPowerCanvas.transform.SetParent(transform, false);
            bowPowerCanvas.renderMode = RenderMode.WorldSpace; bowPowerCanvas.sortingOrder = 550;
            ((RectTransform)bowPowerCanvas.transform).sizeDelta = new Vector2(280, 70);
            bowPowerCanvas.transform.localScale = Vector3.one * .0011f;
            var back = BattleUiStyle.Box(bowPowerCanvas.transform, "Background", new Color(.02f, .04f, .05f, .95f), overlay);
            Stretch(back.rectTransform);
            bowPowerText = BattleUiStyle.Label(bowPowerCanvas.transform, "Power", "", 24, overlay, TextAnchor.MiddleCenter);
            Stretch(bowPowerText.rectTransform); HideBowGuide();
        }

        void HideBowGuide()
        {
            if (bowTrajectory != null) bowTrajectory.enabled = false;
            if (bowImpact != null) bowImpact.enabled = false;
            if (bowPowerCanvas != null) bowPowerCanvas.gameObject.SetActive(false);
        }

        void UpdateBowGuide()
        {
            HideBowGuide();
            if (bowTrajectory == null || HandMenuVisible || !battle.CanUseSummitActions ||
                !player.TryBowTrajectory(out var ray, out float power)) return;
            Vector3 velocity = ray.direction * Mathf.Lerp(45, 110, power);
            Vector3 point = ray.origin; float travelled = 0;
            trajectoryPoints[0] = point; int count = 1; bool hitSurface = false;
            const float step = .03f;
            for (int i = 1; i < trajectoryPoints.Length; i++)
            {
                // Match the projectile's gravity and swept collision, including
                // the arrow radius, instead of drawing a misleading straight ray.
                Vector3 delta = velocity * step + Vector3.down * (.5f * 9.81f * step * step);
                velocity += Vector3.down * (9.81f * step);
                float distance = Mathf.Min(delta.magnitude, 230 - travelled);
                int hits = Physics.SphereCastNonAlloc(point, .1f, delta.normalized, trajectoryHits, distance, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity; Vector3 impact = default;
                for (int h = 0; h < hits; h++)
                {
                    var hit = trajectoryHits[h]; var unit = hit.collider.GetComponentInParent<BattleUnit>();
                    if (unit != null && (!unit.IsAlive || unit.Team == BattleTeam.Blue)) continue;
                    if (hit.distance < nearest) { nearest = hit.distance; impact = hit.point; }
                }
                if (nearest < float.PositiveInfinity) { point = impact; hitSurface = true; }
                else point += delta.normalized * distance;
                trajectoryPoints[count++] = point; travelled += distance;
                if (hitSurface || travelled >= 230) break;
            }
            bowTrajectory.positionCount = count; bowTrajectory.enabled = true;
            for (int i = 0; i < count; i++) bowTrajectory.SetPosition(i, trajectoryPoints[i]);
            Color color = power >= .99f ? new Color(.3f, 1, .65f) : new Color(1, .82f, .3f);
            bowTrajectory.startColor = bowTrajectory.endColor = color;
            bowImpact.enabled = hitSurface; bowImpact.startColor = bowImpact.endColor = color;
            var eye = player.ViewCamera.transform;
            float viewDistance = Vector3.Distance(eye.position, point);
            // Keep distant parts legible in a headset; a constant centimetre-wide
            // line disappears into subpixel dots across the battlefield.
            bowTrajectory.startWidth = .012f;
            bowTrajectory.endWidth = Mathf.Clamp(viewDistance * .0015f, .018f, .3f);
            bowImpact.startWidth = bowImpact.endWidth = Mathf.Clamp(viewDistance * .0008f, .018f, .16f);
            float radius = Mathf.Clamp(viewDistance * .008f, .08f, 1);
            for (int i = 0; i < 32; i++)
            { float angle = i * Mathf.PI * 2 / 32; bowImpact.SetPosition(i, point + (eye.right * Mathf.Cos(angle) + eye.up * Mathf.Sin(angle)) * radius); }
            bowPowerCanvas.gameObject.SetActive(true); bowPowerCanvas.worldCamera = player.ViewCamera;
            Vector3 labelPosition = ray.origin + Vector3.up * .2f;
            bowPowerCanvas.transform.SetPositionAndRotation(labelPosition, Quaternion.LookRotation(labelPosition - eye.position, Vector3.up));
            bowPowerText.color = color;
            bowPowerText.text = player.BowPull < BowDrawGesture.MinimumPull ? "PULL BACK TO AIM" : $"{Mathf.RoundToInt(power * 100)}% POWER\nRELEASE TO SHOOT";
        }
    }
}
