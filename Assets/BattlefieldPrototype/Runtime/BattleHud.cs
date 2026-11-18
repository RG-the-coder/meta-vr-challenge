using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    /// <summary>Screen-resolution desktop HUD and a larger, high-density headset HUD.</summary>
    [DefaultExecutionOrder(200)]
    [AddComponentMenu("Battlefield Prototype/Battle HUD")]
    public sealed class BattleHud : MonoBehaviour
    {
        private const float PanelWidth = 1000f;
        private BattleDirector director;
        private PossessionController player;
        private Canvas canvas;
        private CanvasScaler scaler;
        private GraphicRaycaster raycaster;
        private RectTransform panel, statusPanel, healthBackground, buttonRow;
        private Image statusPanelImage;
        private Text statusText, controlsText, targetText, arsenalTitle;
        private Text matchText, healthText;
        private Image classAccent;
        private RectTransform classEmblem;
        private UnitRole emblemRole;
        private readonly RectTransform[] healthSegments = new RectTransform[9];
        private readonly RectTransform[] abilityCards = new RectTransform[BattleClassCatalog.MaxAbilities];
        private readonly Text[] abilityNames = new Text[BattleClassCatalog.MaxAbilities], abilityHints = new Text[BattleClassCatalog.MaxAbilities], abilityTimers = new Text[BattleClassCatalog.MaxAbilities];
        private readonly Image[] abilityProgress = new Image[BattleClassCatalog.MaxAbilities];
        private Text noPowers;
        private BattleUnit shownUnit;
        private RectTransform crosshair;
        private RectTransform healthFill;
        private Image healthImage;
        private Button startButton, nextButton, spectatorButton;
        private readonly Button[] buttons = new Button[4];
        private GameObject ownedEventSystem;
        private LineRenderer pointer;
        private Material pointerMaterial;
        private Material overlayMaterial;
        private Image blink;
        private float recoilUntil;
        private BattleDeploymentMenu deploymentMenu;
        public BattleTacticalHud TacticalHud { get; private set; }
        private Font font;
        private float nextTextUpdate;
        private bool lastXR, lastDesktopInput, lastMouseCaptured;
        private int lastScreenWidth, lastScreenHeight;
        private float contentWidth = PanelWidth - 44f;

        public void Initialize(BattleDirector battleDirector, PossessionController controller)
        {
            director = battleDirector;
            player = controller;
            if (player != null && player.ViewCamera != null) CreateHud();
        }

        private void Start()
        {
            if (director == null) director = FindAnyObjectByType<BattleDirector>();
            if (player == null) player = FindAnyObjectByType<PossessionController>();
            if (player != null && player.ViewCamera != null) CreateHud();
        }

        private void OnEnable()
        {
            if (canvas != null) canvas.gameObject.SetActive(true);
            if (deploymentMenu != null) deploymentMenu.enabled = true;
            Application.onBeforeRender += UpdatePointerBeforeRender;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdatePointerBeforeRender;
            // The desktop overlay is a scene root, so follow this component's lifetime.
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (pointer != null) pointer.enabled = false;
            if (deploymentMenu != null) deploymentMenu.enabled = false;
        }

        private void CreateHud()
        {
            if (canvas != null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            overlayMaterial = BattleUiStyle.OverlayMaterial();
            var canvasObject = new GameObject("Battle HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(player.ViewCamera.transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.sortingOrder = 500;
            scaler = canvasObject.GetComponent<CanvasScaler>();
            raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            // World-space glyphs are rasterized at three times their layout size.
            // The desktop overlay uses display resolution, independent of URP render scale.
            scaler.dynamicPixelsPerUnit = 3f;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            panel = CreateImage("Panel", canvas.transform, HudPanelColor(),
                Vector2.zero, new Vector2(PanelWidth, 258f)).rectTransform;
            panel.GetComponent<Image>().raycastTarget = true;
            BattleUiStyle.Frame(panel, overlayMaterial);
            statusPanelImage = CreateImage("Status Panel", canvas.transform, Color.clear, Vector2.zero, new Vector2(PanelWidth, 108f));
            statusPanelImage.raycastTarget = true;
            statusPanel = statusPanelImage.rectTransform;
            BattleUiStyle.Frame(statusPanel, overlayMaterial);
            statusText = CreateText("Status", statusPanel, new Vector2(22f, -12f), new Vector2(contentWidth, 76f), 26, TextAnchor.UpperLeft);
            statusText.fontStyle = FontStyle.Bold;
            matchText = CreateText("Battle Summary", statusPanel, Vector2.zero, Vector2.zero, 20, TextAnchor.UpperLeft);
            matchText.color = BattleUiStyle.Muted;
            healthText = CreateText("Health Value", statusPanel, Vector2.zero, Vector2.zero, 22, TextAnchor.UpperLeft);
            classAccent = CreateImage("Class Accent", statusPanel, BattleUiStyle.Accent, Vector2.zero, Vector2.zero);
            healthBackground = CreateImage("Health Background", statusPanel, new Color(.13f, .16f, .16f, 1f),
                new Vector2(22f, -88f), new Vector2(contentWidth, 10f)).rectTransform;
            healthImage = CreateImage("Health", healthBackground, new Color(.40f, .70f, .55f, 1f),
                Vector2.zero, new Vector2(contentWidth, 10f));
            healthFill = healthImage.rectTransform;
            for (int i = 0; i < healthSegments.Length; i++)
                healthSegments[i] = CreateImage("Vitality Segment " + i, healthBackground, HudPanelColor(), Vector2.zero, Vector2.zero).rectTransform;
            arsenalTitle = CreateText("Arsenal Heading", panel, Vector2.zero, Vector2.zero, 15, TextAnchor.UpperLeft);
            arsenalTitle.color = BattleUiStyle.Accent;
            arsenalTitle.fontStyle = FontStyle.Bold;
            controlsText = CreateText("Controls", panel, new Vector2(22f, -108f), new Vector2(contentWidth, 86f), 22, TextAnchor.UpperLeft);
            controlsText.color = Color.Lerp(BattleUiStyle.Text, BattleUiStyle.Muted, .2f);
            noPowers = CreateText("No Powers", panel, Vector2.zero, Vector2.zero, 22, TextAnchor.UpperLeft);
            noPowers.text = "Standard arms";
            noPowers.color = BattleUiStyle.Muted;
            for (int slot = 0; slot < BattleClassCatalog.MaxAbilities; slot++)
            {
                var card = CreateImage("Ability " + slot, panel, new Color(.08f, .10f, .105f, .76f), Vector2.zero, Vector2.zero);
                abilityCards[slot] = card.rectTransform;
                BattleUiStyle.Frame(card.rectTransform, overlayMaterial, new Color(.25f, .28f, .28f, .65f));
                abilityNames[slot] = CreateText("Name", card.transform, Vector2.zero, Vector2.zero, 22, TextAnchor.UpperLeft);
                abilityNames[slot].fontStyle = FontStyle.Bold;
                abilityHints[slot] = CreateText("Effect", card.transform, Vector2.zero, Vector2.zero, 20, TextAnchor.UpperLeft);
                abilityHints[slot].color = BattleUiStyle.Muted;
                abilityTimers[slot] = CreateText("Cooldown", card.transform, Vector2.zero, Vector2.zero, 20, TextAnchor.UpperRight);
                abilityTimers[slot].fontStyle = FontStyle.Bold;
                CreateImage("Track", card.transform, new Color(.17f, .20f, .20f), Vector2.zero, Vector2.zero);
                abilityProgress[slot] = CreateImage("Progress", card.transform, BattleUiStyle.Accent, Vector2.zero, Vector2.zero);
            }
            targetText = CreateText("Possession Target", canvas.transform, Vector2.zero, new Vector2(PanelWidth, 48f), 28, TextAnchor.MiddleCenter);
            var reticle = new GameObject("Aim", typeof(RectTransform));
            reticle.transform.SetParent(canvas.transform, false);
            crosshair = (RectTransform)reticle.transform;
            crosshair.sizeDelta = new Vector2(20f, 20f);
            CreateReticleStroke("Horizontal Outline", new Vector2(20f, 4f), Color.black);
            CreateReticleStroke("Vertical Outline", new Vector2(4f, 20f), Color.black);
            CreateReticleStroke("Horizontal", new Vector2(18f, 2f), BattleUiStyle.Text);
            CreateReticleStroke("Vertical", new Vector2(2f, 18f), BattleUiStyle.Text);

            var row = new GameObject("Desktop Buttons", typeof(RectTransform));
            row.transform.SetParent(panel, false);
            buttonRow = (RectTransform)row.transform;
            Place(buttonRow, new Vector2(22f, -200f), new Vector2(contentWidth, 44f));
            float buttonWidth = (contentWidth - 36f) / 4f;
            startButton = CreateButton("Start [Space]", 0, buttonWidth, () => director.BeginBattle());
            CreateButton("Restart [R]", 1, buttonWidth, () => director.RestartBattle());
            nextButton = CreateButton("Next Ally [Tab]", 2, buttonWidth, () => player.CycleUnit());
            spectatorButton = CreateButton("Spectator [Q]", 3, buttonWidth, () => player.ReleaseControl());
            // Reuse the integrating scene's EventSystem; clean up only one we create.
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
            {
                ownedEventSystem = new GameObject("Battle HUD Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.transform.SetParent(transform, false);
                ownedEventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
                // InputSystemUIInputModule assigns its default actions in OnEnable.
            }

            var pointerObject = new GameObject("Right Hand Aim Pointer", typeof(LineRenderer));
            pointerObject.transform.SetParent(transform, false);
            pointer = pointerObject.GetComponent<LineRenderer>();
            pointer.positionCount = 2;
            pointer.useWorldSpace = true;
            pointer.startWidth = 0.0025f;
            pointer.endWidth = 0.0015f;
            pointer.numCapVertices = 4;
            pointer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pointer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                pointerMaterial = new Material(shader);
                pointer.sharedMaterial = pointerMaterial;
            }
            pointer.enabled = false;
            blink = CreateImage("Switch Blink", canvas.transform, Color.clear, Vector2.zero, Vector2.zero);
            var blinkCanvas = blink.gameObject.AddComponent<Canvas>();
            blinkCanvas.overrideSorting = true;
            blinkCanvas.sortingOrder = 2000;
            player.ShotFired += OnShotFired;
            TacticalHud=canvas.gameObject.AddComponent<BattleTacticalHud>();
            TacticalHud.Initialize(director,player,canvas,overlayMaterial);
            if (director.GetComponent<BattleDeployment>() != null)
            {
                deploymentMenu = gameObject.AddComponent<BattleDeploymentMenu>();
                deploymentMenu.Initialize(director, player);
            }
            UpdateLayout();
            UpdateControls();
            if (director != null) UpdateStatus(player.CurrentUnit);
        }

        private Button CreateButton(string label, int index, float width, UnityAction action)
        {
            var image = CreateImage(label, buttonRow, index == 0 ? new Color(.31f, .27f, .18f, .96f) : new Color(.12f, .15f, .16f, .96f),
                new Vector2(index * (width + 12f), 0f), new Vector2(width, 44f));
            image.raycastTarget = true;
            BattleUiStyle.Frame(image.rectTransform, overlayMaterial, index == 0 ? BattleUiStyle.Accent : BattleUiStyle.Edge);
            var button = image.gameObject.AddComponent<Button>();
            buttons[index] = button;
            button.targetGraphic = image;
            // Space and Tab are gameplay bindings; avoid also submitting/navigating UI.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.18f, 1.13f, 1.02f);
            colors.pressedColor = new Color(.72f, .75f, .73f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.5f, .52f, .52f, .55f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.onClick.AddListener(action);
            var text = CreateText("Label", image.transform, Vector2.zero, new Vector2(width, 44f), 22, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            text.text = label;
            return button;
        }

        private static Color HudPanelColor()
        {
            var color = BattleUiStyle.Ink;
            color.a = .70f;
            return color;
        }

        private static Color ClassTint(UnitRole role) => Color.Lerp(BattleClassCatalog.Color(role), BattleUiStyle.Accent, .35f);

        private void CreateReticleStroke(string label, Vector2 size, Color color)
        {
            var stroke = CreateImage(label, crosshair, color, Vector2.zero, size).rectTransform;
            stroke.anchorMin = stroke.anchorMax = stroke.pivot = new Vector2(0.5f, 0.5f);
            stroke.anchoredPosition = Vector2.zero;
        }

        private Image CreateImage(string label, Transform parent, Color color, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.material = overlayMaterial;
            image.raycastTarget = false;
            Place(image.rectTransform, position, size);
            return image;
        }

        private Text CreateText(string label, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = font;
            text.material = overlayMaterial;
            text.fontSize = fontSize;
            text.color = BattleUiStyle.Text;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = false;
            text.raycastTarget = false;
            Place(text.rectTransform, position, size);
            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void UpdateLayout()
        {
            lastXR = player.IsXrDisplayActive;
            shownUnit = player.CurrentUnit;
            lastScreenWidth = Mathf.Max(1, Screen.width);
            lastScreenHeight = Mathf.Max(1, Screen.height);
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(lastXR ? player.ViewCamera.transform : null, false);
            canvas.worldCamera = player.ViewCamera;
            canvas.renderMode = lastXR ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = !lastXR;
            scaler.uiScaleMode = lastXR ? CanvasScaler.ScaleMode.ConstantPixelSize : CanvasScaler.ScaleMode.ScaleWithScreenSize;
            float scale = Mathf.Max(.01f, Mathf.Min(lastScreenWidth / 1280f, lastScreenHeight / 720f));
            float width;
            if (lastXR)
            {
                rect.sizeDelta = new Vector2(1200, 900);
                rect.localScale = Vector3.one * .0014f;
                rect.localPosition = new Vector3(0, 0, 1.5f);
                rect.localRotation = Quaternion.identity;
                width = 520;
            }
            else
            {
                scaler.referenceResolution = new Vector2(lastScreenWidth, lastScreenHeight) / scale;
                width = Mathf.Min(520, Mathf.Max(260, lastScreenWidth * .46f / scale - 12));
            }
            contentWidth = width - 24;
            statusPanel.SetParent(canvas.transform, false);
            statusPanel.anchorMin = statusPanel.anchorMax = statusPanel.pivot = new Vector2(0, 1);
            statusPanel.anchoredPosition = new Vector2(12, -12);
            statusPanel.sizeDelta = new Vector2(width, 124);
            statusPanelImage.color = HudPanelColor();
            Place(classAccent.rectTransform, Vector2.zero, new Vector2(width, 3));
            Place(matchText.rectTransform, new Vector2(12, -10), new Vector2(contentWidth, 25));
            bool possessed = shownUnit != null && shownUnit.IsAlive;
            UnitRole role = possessed ? shownUnit.Definition.Role : UnitRole.Vanguard;
            if (classEmblem == null || emblemRole != role)
            {
                if (classEmblem != null) Destroy(classEmblem.gameObject);
                emblemRole = role;
                classEmblem = BattleUiStyle.Emblem(statusPanel, "Class Insignia", role, ClassTint(role), overlayMaterial);
            }
            classEmblem.gameObject.SetActive(possessed);
            Place(classEmblem, new Vector2(12, -40), new Vector2(30, 30));
            Place(statusText.rectTransform, new Vector2(possessed ? 52 : 12, -37), new Vector2(contentWidth - (possessed ? 40 : 0), 38));
            statusText.fontSize = lastXR ? 30 : 26;
            Place(healthText.rectTransform, new Vector2(12, -76), new Vector2(contentWidth, 26));
            Place(healthBackground, new Vector2(12, -107), new Vector2(contentWidth, 9));
            for (int i = 0; i < healthSegments.Length; i++)
                Place(healthSegments[i], new Vector2(contentWidth * (i + 1) / 10f - 1, 0), new Vector2(2, 9));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(12, -144);
            int count = possessed ? shownUnit.Definition.AbilityCount : 0;
            bool compact = !lastXR && count > 2;
            float step = lastXR ? 78 : compact ? 54 : 70;
            Place(arsenalTitle.rectTransform, new Vector2(12, -10), new Vector2(contentWidth, 24));
            arsenalTitle.fontSize = lastXR ? 18 : 15;
            arsenalTitle.text = possessed ? "COMBAT ARTS" : "FIELD COMMAND";
            float controlsY = count > 0 ? 36 + count * step : possessed ? 74 : 36;
            noPowers.gameObject.SetActive(possessed && count == 0);
            Place(noPowers.rectTransform, new Vector2(12, -36), new Vector2(contentWidth, 30));
            float controlsHeight = lastXR ? (count > 2 ? 136 : 100) : 84;
            float buttonsY = controlsY + controlsHeight + 8;
            panel.sizeDelta = new Vector2(width, lastXR ? controlsY + controlsHeight + 8 : buttonsY + 84);
            for (int slot = 0; slot < BattleClassCatalog.MaxAbilities; slot++)
            {
                abilityCards[slot].gameObject.SetActive(slot < count);
                float y = 34 + slot * step;
                float height = lastXR ? 70 : compact ? 50 : 62;
                Place(abilityCards[slot], new Vector2(12, -y), new Vector2(contentWidth, height));
                Place(abilityNames[slot].rectTransform, new Vector2(9, -3), new Vector2(contentWidth - 18, 29));
                abilityNames[slot].fontSize = lastXR ? 26 : 22;
                Place(abilityHints[slot].rectTransform, new Vector2(9, compact ? -26 : -32), new Vector2(contentWidth - 86, compact ? 22 : 26));
                abilityHints[slot].fontSize = lastXR ? 22 : compact ? 18 : 20;
                Place(abilityTimers[slot].rectTransform, new Vector2(contentWidth - 80, compact ? -26 : -32), new Vector2(71, 26));
                Place(abilityCards[slot].Find("Track").GetComponent<RectTransform>(), new Vector2(9, -height + 3), new Vector2(contentWidth - 18, 3));
                Place(abilityProgress[slot].rectTransform, new Vector2(9, -height + 3), new Vector2(contentWidth - 18, 3));
            }
            Place(controlsText.rectTransform, new Vector2(12, -controlsY), new Vector2(contentWidth, controlsHeight));
            controlsText.fontSize = lastXR ? 26 : 22;
            buttonRow.gameObject.SetActive(!lastXR);
            Place(buttonRow, new Vector2(12, -buttonsY), new Vector2(contentWidth, 84));
            float buttonWidth = (contentWidth - 8) / 2;
            string[] labels = { "Begin Battle", "Army Creator", "Next Ally", "Spectate" };
            for (int i = 0; i < buttons.Length; i++)
            {
                Place((RectTransform)buttons[i].transform, new Vector2(i % 2 * (buttonWidth + 8), -(i / 2) * 44), new Vector2(buttonWidth, 40));
                var label = buttons[i].GetComponentInChildren<Text>();
                label.rectTransform.sizeDelta = new Vector2(buttonWidth, 40);
                label.fontSize = buttonWidth < 160 ? 18 : 20;
                label.text = labels[i];
            }
            crosshair.anchorMin = crosshair.anchorMax = crosshair.pivot = Vector2.one * .5f;
            crosshair.anchoredPosition = Vector2.zero;
            targetText.rectTransform.anchorMin = targetText.rectTransform.anchorMax = Vector2.one * .5f;
            targetText.rectTransform.pivot = Vector2.one * .5f;
            targetText.rectTransform.sizeDelta = new Vector2(520, 48);
            blink.rectTransform.anchorMin = Vector2.zero;
            blink.rectTransform.anchorMax = Vector2.one;
            blink.rectTransform.pivot = Vector2.one * .5f;
            blink.rectTransform.anchoredPosition = Vector2.zero;
            blink.rectTransform.sizeDelta = lastXR ? Vector2.one * 10000 : Vector2.zero;
            UpdateControls();
            nextTextUpdate = 0;
            TacticalHud?.RebuildLayout(lastXR);
        }

        private void LateUpdate()
        {
            if (player == null || director == null) return;
            if (canvas == null) { CreateHud(); if (canvas == null) return; }
            bool world = player.IsXrDisplayActive;
            if (lastXR != world || shownUnit != player.CurrentUnit || lastScreenWidth != Screen.width || lastScreenHeight != Screen.height) UpdateLayout();
            BattleUiStyle.RestoreOverlayScale(canvas);
            if (lastDesktopInput != player.IsDesktopInputActive || lastMouseCaptured != player.IsMouseLookCaptured) UpdateControls();
            raycaster.enabled = !lastXR && !player.IsMouseLookCaptured;
            bool inMenu = player.IsMenuActive;
            panel.gameObject.SetActive(!inMenu);
            statusPanel.gameObject.SetActive(!inMenu);
            targetText.gameObject.SetActive(!inMenu);
            crosshair.gameObject.SetActive(!inMenu && player.IsAimingFromHead);
            blink.color = new Color(0, 0, 0, player.BlinkOpacity);
            crosshair.localScale = Vector3.one * (1 + Mathf.Clamp01((recoilUntil - Time.unscaledTime) / .12f) * .45f);
            UpdatePointer();
            var unit = player.CurrentUnit;
            float fraction = unit != null && unit.IsAlive ? unit.HealthFraction : 0;
            healthFill.sizeDelta = new Vector2(contentWidth * fraction, 9);
            healthImage.color = Color.Lerp(new Color(.84f, .34f, .27f), new Color(.40f, .70f, .55f), fraction);
            if (unit != null && unit.IsAlive)
            {
                var accent = ClassTint(unit.Definition.Role);
                classAccent.color = accent;
                statusText.color = BattleUiStyle.Text;
                for (int slot = 0; slot < unit.Definition.AbilityCount; slot++)
                {
                    var ability = BattleClassCatalog.Ability(unit.Definition.Role, slot);
                    float remaining = unit.GetAbilityCooldownRemaining(slot);
                    float ready = 1 - Mathf.Clamp01(remaining / unit.Definition.GetAbilityCooldown(slot));
                    string binding = lastXR ? BattleClassCatalog.Binding(slot, true) : slot == 0 ? "G" : slot == 1 ? "H" : slot == 2 ? "J" : "K";
                    abilityNames[slot].text = binding + "  /  " + ability.Name;
                    abilityNames[slot].color = remaining <= 0 ? BattleUiStyle.Text : BattleUiStyle.Muted;
                    abilityHints[slot].text = ability.Hint;
                    abilityTimers[slot].text = !director.IsBattleRunning ? "WAIT" : remaining > 0 ? remaining.ToString("0.0") + "s" : "READY";
                    abilityTimers[slot].color = remaining <= 0 ? accent : BattleUiStyle.Muted;
                    abilityProgress[slot].color = accent;
                    abilityProgress[slot].rectTransform.sizeDelta = new Vector2((contentWidth - 18) * ready, 3);
                }
            }
            else
            {
                classAccent.color = BattleUiStyle.Accent;
                statusText.color = BattleUiStyle.Text;
            }
            startButton.interactable = director.Phase == BattlePhase.Setup;
            nextButton.interactable = director.AliveBlue > 0 || player.AllowEnemyPossession && director.AliveRed > 0;
            spectatorButton.interactable = unit != null;
            if (Time.unscaledTime < nextTextUpdate) return;
            nextTextUpdate = Time.unscaledTime + .1f;
            UpdateStatus(unit);
        }

        private void UpdatePointer()
        {
            pointer.enabled = false;
            if (player.IsMenuActive) return;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,
                    player.AimScreenPosition, null, out Vector2 local);
                crosshair.anchoredPosition = local;
                targetText.rectTransform.anchoredPosition = local + new Vector2(0, 58);
            }
            if (player.IsAimingFromHead && canvas.renderMode == RenderMode.WorldSpace)
            {
                // Also honor an assigned camera's asymmetric/lens-shifted projection.
                var hudPlane = new Plane(canvas.transform.forward, canvas.transform.position);
                if (hudPlane.Raycast(player.AimRay, out float distance))
                {
                    crosshair.position = player.AimRay.GetPoint(distance);
                    targetText.rectTransform.anchoredPosition = crosshair.anchoredPosition + new Vector2(0f, 58f);
                }
            }
            if (!player.IsUsingXR || player.IsAimingFromHead || !player.HasTrackedAim) return;
            var camera = player.ViewCamera.transform;
            Vector3 start = player.AimRay.origin;
            Vector3 end = player.AimPoint;
            float startDepth = Vector3.Dot(start - camera.position, camera.forward);
            float endDepth = Vector3.Dot(end - camera.position, camera.forward);
            // A view-facing ribbon starting at/behind an eye becomes a giant triangle
            // under perspective projection. Clip the visible segment ahead of both eyes.
            float near = Mathf.Max(0.15f, player.ViewCamera.nearClipPlane + 0.08f);
            if (startDepth <= near && endDepth <= near) return;
            if (startDepth < near)
                start = Vector3.Lerp(start, end, (near - startDepth) / (endDepth - startDepth));
            else if (endDepth < near)
                end = Vector3.Lerp(start, end, (startDepth - near) / (startDepth - endDepth));
            if ((end - start).sqrMagnitude < 0.0001f) return;
            pointer.enabled = true;
            pointer.SetPosition(0, start);
            pointer.SetPosition(1, end);
            var color = player.AimedUnit != null ? new Color(.49f, .80f, .64f) : BattleUiStyle.Accent;
            pointer.startColor = color;
            pointer.endColor = color;
            if (pointerMaterial != null)
            {
                if (pointerMaterial.HasProperty("_BaseColor")) pointerMaterial.SetColor("_BaseColor", color);
                if (pointerMaterial.HasProperty("_Color")) pointerMaterial.SetColor("_Color", color);
            }
        }

        [BeforeRenderOrder(200)]
        private void UpdatePointerBeforeRender()
        {
            if (pointer != null && player != null && player.ViewCamera != null) UpdatePointer();
        }

        private void UpdateStatus(BattleUnit unit)
        {
            int seconds = Mathf.FloorToInt(director.BattleDuration);
            matchText.text = $"BLUE {director.AliveBlue}  :  RED {director.AliveRed}   |   {seconds / 60:00}:{seconds % 60:00}";
            statusText.text = unit != null && unit.IsAlive ? unit.Definition.DisplayName.ToUpperInvariant() : "SPECTATOR";
            healthText.text = unit != null && unit.IsAlive
                ? $"HP {Mathf.CeilToInt(unit.Health)} / {Mathf.CeilToInt(unit.MaxHealth)}" + (unit.Shield > 0 ? $"   +{Mathf.CeilToInt(unit.Shield)} shield" : "")
                : "Tab / A to take command";
            var target = player.AimedUnit;
            targetText.text = target != null ? (player.IsDesktopInputActive ? "E: possess " : "B: possess ") + target.Definition.DisplayName : "";
            targetText.color = new Color(.57f, .86f, .70f);
        }

        private void UpdateControls()
        {
            lastDesktopInput = player.IsDesktopInputActive;
            lastMouseCaptured = player.IsMouseLookCaptured;
            string attack = player.CurrentUnit != null && player.CurrentUnit.IsMelee ? "strike" : "fire";
            controlsText.text = lastDesktopInput || !lastXR
                ? "WASD move | Mouse look\nLMB " + attack + " | Tab ally\nE/Q control | Esc cursor"
                : "L stick move | R stick turn\nR trigger: " + attack + "\nA ally | B possess";
            if (lastXR && !lastDesktopInput && player.CurrentUnit != null && player.CurrentUnit.Definition.AbilityCount > 2)
                controlsText.text += "\nStick clicks: powers 3 / 4";
            if (player.CurrentUnit == null)
                controlsText.text = lastDesktopInput || !lastXR ? "WASD / F / C: fly\nTab ally | E possess\nR army | Esc cursor" : "Left stick: fly\nA ally | B possess\nY: new army";
        }

        private void OnDestroy()
        {
            if (player != null) player.ShotFired -= OnShotFired;
            if (canvas != null) Destroy(canvas.gameObject);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
            if (pointer != null) Destroy(pointer.gameObject);
            if (pointerMaterial != null) Destroy(pointerMaterial);
            if (overlayMaterial != null) Destroy(overlayMaterial);
        }

        private void OnShotFired() => recoilUntil = Time.unscaledTime + 0.12f;
    }
}
