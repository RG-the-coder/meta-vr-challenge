using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    internal static class BattleUiStyle
    {
        internal static readonly Color Ink = new Color(.035f, .044f, .052f, .94f);
        internal static readonly Color Panel = new Color(.065f, .079f, .089f, .97f);
        internal static readonly Color Edge = new Color(.23f, .27f, .28f);
        internal static readonly Color Muted = new Color(.58f, .63f, .63f);
        internal static readonly Color Accent = new Color(.78f, .64f, .39f);
        internal static readonly Color Text = new Color(.89f, .89f, .83f);
        internal static readonly Color Friendly = new Color(.32f, .68f, .78f);
        internal static readonly Color Enemy = new Color(.78f, .34f, .28f);

        internal static RectTransform Emblem(Transform parent, string name, UnitRole role, Color color, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(BattleUiEmblem));
            go.transform.SetParent(parent, false);
            var emblem = go.GetComponent<BattleUiEmblem>();
            emblem.Role = role;
            emblem.color = color;
            emblem.material = material;
            emblem.raycastTarget = false;
            return emblem.rectTransform;
        }

        internal static void Frame(RectTransform parent, Material material, Color? tint = null)
        {
            Color c = tint ?? Edge;
            for (int i = 0; i < 4; i++)
            {
                var edge = Box(parent, "Frame " + i, c, material).rectTransform;
                edge.anchorMin = i < 2 ? new Vector2(0, i) : new Vector2(i - 2, 0);
                edge.anchorMax = i < 2 ? new Vector2(1, i) : new Vector2(i - 2, 1);
                edge.pivot = Vector2.one * .5f;
                edge.sizeDelta = i < 2 ? new Vector2(0, 1) : new Vector2(1, 0);
                edge.anchoredPosition = Vector2.zero;
            }
        }

        internal static Material OverlayMaterial()
        {
            var shader = Resources.Load<Shader>("BattlefieldOverlay");
            return shader != null ? new Material(shader) { name = "Battlefield overlay UI", enableInstancing = true } : null;
        }

        internal static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        internal static void RestoreOverlayScale(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
            // Recover an existing Play session whose transform was reset by an
            // earlier layout or script reload while CanvasScaler kept its cache.
            Vector3 fitted = Vector3.one * canvas.scaleFactor;
            if (canvas.transform.localScale != fitted) canvas.transform.localScale = fitted;
        }

        internal static Image Box(Transform parent, string name, Color color, Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.material = material;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(Transform parent, string name, string value, int size, Material material,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = Text;
            text.material = material;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(Transform parent, string name, string text, Material material, UnityAction action)
        {
            var box = Box(parent, name, new Color(.12f, .15f, .16f, .98f), material);
            Frame(box.rectTransform, material);
            box.raycastTarget = true;
            var button = box.gameObject.AddComponent<Button>();
            button.targetGraphic = box;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1.32f, 1.25f, 1.1f),
                pressedColor = new Color(.7f, .78f, .8f), selectedColor = Color.white,
                disabledColor = new Color(.48f, .48f, .48f, .65f), colorMultiplier = 1, fadeDuration = .12f };
            button.onClick.AddListener(action);
            var label = Label(box.transform, "Label", text, 20, material, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(5, 2);
            label.rectTransform.offsetMax = new Vector2(-5, -2);
            return button;
        }
    }
}
