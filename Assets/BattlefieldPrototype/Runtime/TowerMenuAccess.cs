using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    public sealed partial class TowerBattleHud
    {
        Canvas menuAccessCanvas;
        Button menuAccessButton;
        bool menuAccessPointer, menuAccessPlaced;
        Vector3 menuAccessFallback;

        void CreateMenuAccess()
        {
            menuAccessCanvas = new GameObject("Wrist menu", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            menuAccessCanvas.renderMode = RenderMode.WorldSpace;
            menuAccessCanvas.sortingOrder = 605;
            var rect = (RectTransform)menuAccessCanvas.transform;
            rect.sizeDelta = new Vector2(200, 84); rect.localScale = Vector3.one * .0012f;
            menuAccessButton = BattleUiStyle.Button(rect, "Open menu", "MENU", overlay, OpenCommandMenu);
            Stretch((RectTransform)menuAccessButton.transform);
            menuAccessButton.GetComponentInChildren<Text>().fontSize = 32;
            menuAccessButton.image.color = new Color(.06f, .32f, .38f, 1);
            menuAccessCanvas.gameObject.SetActive(false);
        }

        void OpenCommandMenu()
        {
            if (!battle.Director.IsBattleRunning || player.IsSwitchingUnit) return;
            if (HandMenuVisible && !MenuNeedsRecall) return;
            guideOpen = charactersOpen = false; vrHudVisible = true; vrHudPage = battle.SelectedDefense.HasValue ? 4 : 0;
            player.OpenMenuCursor(); RequestVrHudAnchor();
            requireHandRelease = requirePointerRelease = true;
            CancelPointer(); Refresh();
        }

        void CloseCommandMenu()
        {
            if (!battle.Director.IsBattleRunning) return;
            guideOpen = charactersOpen = false; vrHudVisible = false;
            requireHandRelease = requirePointerRelease = true;
            CancelPointer(); Refresh();
        }

        void UpdateMenuAccess()
        {
            if (menuAccessCanvas == null) return;
            bool visible = world && battle.Director.IsBattleRunning && !player.IsDrawingBow && !player.IsSwitchingUnit &&
                (!HandMenuVisible || MenuNeedsRecall);
            menuAccessCanvas.gameObject.SetActive(visible);
            if (!visible) return;
            var eye = player.ViewCamera.transform;
            menuAccessCanvas.worldCamera = player.ViewCamera;
            Vector3 position;
            if (player.TryOpticalHandPalm(true, out var palm) && Vector3.Distance(eye.position, palm) > .25f &&
                Vector3.Angle(eye.forward, palm + Vector3.up * .14f - eye.position) < 40)
                position = palm + Vector3.up * .14f;
            else
            {
                // A missing left hand still leaves a usable button. Reposition only
                // after a large turn/travel, never for small head movements.
                if (!menuAccessPlaced || Vector3.Distance(eye.position, menuAccessFallback) > 1.8f ||
                    Vector3.Angle(eye.forward, menuAccessFallback - eye.position) > 40)
                {
                    menuAccessFallback = eye.position + eye.forward * .85f - eye.right * .24f - Vector3.up * .25f;
                    menuAccessPlaced = true;
                }
                position = menuAccessFallback;
            }
            menuAccessCanvas.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye.position, Vector3.up));
        }

        bool TryMenuAccessPoint(Ray ray, out Vector3 point)
        {
            point = default;
            if (menuAccessCanvas == null || !menuAccessCanvas.gameObject.activeInHierarchy) return false;
            var rect = (RectTransform)menuAccessCanvas.transform;
            if (!new Plane(rect.forward, rect.position).Raycast(ray, out float distance) || distance <= 0 || distance > 3) return false;
            point = ray.GetPoint(distance);
            return rect.rect.Contains((Vector2)rect.InverseTransformPoint(point));
        }

        bool ProcessMenuAccess()
        {
            menuAccessPointer = false; UpdateMenuAccess();
            if (!world || menuAccessCanvas == null || !menuAccessCanvas.gameObject.activeInHierarchy) return false;
            bool optical = player.OpticalInputOwnsPointer;
            Ray ray; bool held;
            if (optical)
            {
                if (!player.TryOpticalHandPointer(out ray, out held)) return false;
                if (!held) { requireHandRelease = false; handSelectionLatched = false; }
            }
            else
            {
                if (!player.HasTrackedAim) return false;
                ray = new Ray(player.RightHandAim.position, player.RightHandAim.forward); held = player.RightTriggerHeld;
                if (!held) requirePointerRelease = false;
            }
            if (!TryMenuAccessPoint(ray, out var point)) return false;
            menuAccessPointer = true; ControllerOverUI = true;
            bool blocked = optical ? requireHandRelease : requirePointerRelease;
            DrawPointerLine(ray, point);
            // This canvas contains one button. Dispatch its known geometric hit
            // directly so a newly shown launcher works before canvas batching.
            if (pointer == null) pointer = new BattleUiPointerEventData(EventSystem.current);
            ((BattleUiPointerEventData)pointer).HasWorldPosition = true;
            ((BattleUiPointerEventData)pointer).WorldPosition = point;
            pointer.position = player.ViewCamera.WorldToScreenPoint(point);
            pointer.button = PointerEventData.InputButton.Left;
            ShowPointerDot(point, held && !blocked);
            DispatchTarget(menuAccessButton.gameObject, held && !blocked, optical);
            pointerDot.transform.rotation = menuAccessCanvas.transform.rotation;
            return true;
        }
    }
}
