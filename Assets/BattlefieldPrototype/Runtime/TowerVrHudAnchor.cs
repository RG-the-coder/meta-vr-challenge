using UnityEngine;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        bool hudAnchorPending, hudAnchorWaitingForTracking;
        Canvas vrViewEffects;
        internal Vector3 HandPointerOrigin { get; private set; }
        internal Vector3 HandPointerShoulder => HandPointerOrigin - Vector3.up * .25f + canvas.transform.right * .18f;
        internal bool HandMenuVisible => world && (!battle.Director.IsBattleRunning || vrHudVisible || guideOpen || charactersOpen);
        internal bool MenuNeedsRecall => menuAnchorReady && player != null && player.ViewCamera != null &&
            (Vector3.Distance(player.ViewCamera.transform.position, canvas.transform.position) > 3.5f ||
             Vector3.Angle(player.ViewCamera.transform.forward, canvas.transform.position - player.ViewCamera.transform.position) > 65);

        void AnchorHiddenMenu()
        {
            // Navigation changes content in place. Only summoning a hidden menu
            // (or explicit travel/startup paths) places it at the current head pose.
            if (!menuAnchorReady || !HandMenuVisible) RequestVrHudAnchor();
        }

        void PreserveSelectedMenuAnchor()
        {
            if (!world || !menuAnchorReady || !HandMenuVisible) return;
            // Once a user selects the displayed panel, its provisional position
            // is accepted. Late head tracking must not move it during the click.
            hudAnchorPending = hudAnchorWaitingForTracking = false;
        }

        internal void ReanchorAfterTravel() => RequestVrHudAnchor();

        internal void MoveMenuWithRaisedTower(BattleTower tower, float rise)
        {
            if (!world || canvas == null || Mathf.Abs(rise) < .000001f || !battle.IsMounted ||
                battle.BoundTower != tower || player.CurrentUnit != null) return;
            // Follow changes to the platform height, including damage, without
            // following head turns or recentering when a button is selected.
            Vector3 offset = Vector3.up * rise;
            canvas.transform.position += offset;
            HandPointerOrigin += offset;
            if (menuAccessPlaced) menuAccessFallback += offset;
            player.TranslateHandPointer(offset);
        }

        void RequestVrHudAnchor()
        {
            if (!world || canvas == null || player == null || player.ViewCamera == null) return;
            AnchorVrHudAtEye();
            hudAnchorPending = true;
            hudAnchorWaitingForTracking = !player.HasTrackedHeadPose;
        }

        void AnchorVrHudAtEye()
        {
            var view = player.ViewCamera.transform;
            HandPointerOrigin = view.position;
            player.ResetHandPointer();
            var facing = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            if (facing.sqrMagnitude < .1f) facing = Vector3.forward;
            // Center the panel just above the actual eyes, independent of head pitch.
            canvas.transform.SetPositionAndRotation(view.position + facing * 1.65f + Vector3.up * .08f,
                Quaternion.LookRotation(facing, Vector3.up));
            menuAnchorReady = true;
            CancelPointer();
        }

        void FinalizeVrHudAnchor(bool headTracked)
        {
            if (!world || canvas == null) return;
            if (!hudAnchorPending && !(hudAnchorWaitingForTracking && headTracked)) return;
            // The before-render callback runs after the camera's final tracking pose.
            // Retry once when initial tracking arrives; later head motion never moves the panel.
            AnchorVrHudAtEye();
            hudAnchorPending = false;
            if (headTracked) hudAnchorWaitingForTracking = false;
        }

        void LayoutVrViewEffects()
        {
            if (world)
            {
                if (vrViewEffects == null)
                {
                    vrViewEffects = new GameObject("VR aim and comfort effects", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
                    vrViewEffects.renderMode = RenderMode.WorldSpace; vrViewEffects.sortingOrder = 501;
                }
                var rect = (RectTransform)vrViewEffects.transform;
                rect.SetParent(player.ViewCamera.transform, false);
                rect.localPosition = new Vector3(0, 0, 1.65f); rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * .0016f; rect.sizeDelta = new Vector2(4000, 4000);
                vrViewEffects.worldCamera = player.ViewCamera;
                reticle.SetParent(rect, false); blink.rectTransform.SetParent(rect, false);
            }
            else
            {
                reticle.SetParent(live, false); blink.rectTransform.SetParent(canvas.transform, false);
            }
            reticle.anchorMin = reticle.anchorMax = reticle.pivot = Vector2.one * .5f;
            reticle.sizeDelta = Vector2.one * 20; reticle.localScale = Vector3.one;
            Stretch(blink.rectTransform); blink.rectTransform.localScale = Vector3.one;
            if (vrViewEffects != null) vrViewEffects.gameObject.SetActive(world && isActiveAndEnabled);
        }
    }
}
