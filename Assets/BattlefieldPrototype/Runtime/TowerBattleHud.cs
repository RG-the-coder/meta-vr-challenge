using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BattlefieldPrototype
{
    /// <summary>One live command interface for desktop pointers and tracked controller rays.</summary>
    [DefaultExecutionOrder(50)]
    public sealed partial class TowerBattleHud : MonoBehaviour
    {
        TowerBattle battle;
        PossessionController player;
        Canvas canvas;
        CanvasScaler scaler;
        GraphicRaycaster raycaster;
        Material overlay, pointerMaterial;
        RectTransform live, modal, commandPanel, rosterPanel, summitPanel;
        Text economy, mode, towerStats, notice, dossier, pageText, mapTitle, modalTitle, modalCopy, resultStats;
        Text controls, controlsPage, summitInfo, mapLegend;
        Button mount, build, travel, possess, newMatch, home;
        readonly Button[] recruits = new Button[8];
        readonly Button[] towerButtons = new Button[7];
        int towerPage;
        Text towerNetworkTitle;
        Button previousTowers, nextTowers;
        readonly Button[] summitPowers = new Button[3];
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly Color background = new Color(.025f, .052f, .063f, .96f);
        GameObject eventSystem;
        GameObject pressed, hovered;
        bool pointerDown, world, requirePointerRelease, requireHandRelease = true, mousePointer;
        int page, controlsIndex, width, height, shownRound;
        float nextTextAt, nextMapAt;
        PointerEventData pointer;
        TowerMinimapGraphic map;
        LineRenderer deploymentRing, pointerLine, recruitmentPreview;
        Transform bow;
        Transform bowArrow;
        LineRenderer bowString;
        readonly Transform[] bowLimbs = new Transform[16];
        Material bowMaterial;
        RectTransform reticle;
        Text target;
        float bowRecoil;
        Image blink;
        BattleUiPointerGraphic pointerDot;
        bool menuAnchorReady;
        BattlePhase layoutPhase;
        public bool PointerOverUI { get; private set; }
        public bool ControllerOverUI { get; private set; }
        public Canvas Canvas => canvas;
        public TowerMinimapGraphic Minimap => map;

        public void Initialize(TowerBattle siege, PossessionController controller)
        {
            battle = siege; player = controller; overlay = BattleUiStyle.OverlayMaterial();
            var obj = new GameObject("Skyhold command interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = obj.GetComponent<Canvas>(); canvas.sortingOrder = 500;
            scaler = obj.GetComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; scaler.dynamicPixelsPerUnit = 4;
            raycaster = obj.GetComponent<GraphicRaycaster>();
            // Both input paths use the same explicit press/release dispatch, avoiding duplicate purchases.
            raycaster.enabled = false;
            if (EventSystem.current == null)
            {
                eventSystem = new GameObject("Tower HUD input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
            live = new GameObject("Live command HUD", typeof(RectTransform)).GetComponent<RectTransform>(); live.SetParent(canvas.transform, false); Stretch(live);
            var top = Panel(live, "Economy", 18, 16, 760, 82);
            Label(top, "Brand", "WARDENS AND RAIDERS  /  WAR", 16, 16, 10, 720, 22, BattleUiStyle.Accent);
            economy = Label(top, "Treasury", "", 23, 16, 36, 730, 34, Color.white);
            commandPanel = Panel(live, "Command station", 18, 112, 408, 242);
            mode = Label(commandPanel, "Control mode", "", 20, 16, 12, 376, 30, BattleUiStyle.Accent);
            towerStats = Label(commandPanel, "Bound tower", "", 16, 16, 48, 376, 58, Color.white);
            mount = Button(commandPanel, "Mount", "DISMOUNT [SPACE]", 16, 119, 182, 36, () => { battle.ToggleMount(); Refresh(); });
            possess = Button(commandPanel, "Possess", "CHARACTERS [P]", 210, 119, 182, 36, () => ToggleCharacters());
            travel = Button(commandPanel, "Travel", "BIND TO SELECTED", 16, 167, 182, 34, () => battle.Teleport(battle.SelectedTower));
            build = Button(commandPanel, "Raise", "RAISE TOWER [U]", 210, 167, 182, 34, () => battle.SelectedTower?.TryBuild(BattleTeam.Blue));
            foreach (var button in new[] { mount, possess, travel, build }) button.GetComponentInChildren<Text>().fontSize = 14;
            Label(commandPanel, "Risk", "Your tether stays at risk.", 12, 210, 211, 182, 22, BattleUiStyle.Muted);
            var detail = Panel(live, "Character dossier", 18, 370, 408, 304);
            dossier = Label(detail, "Dossier", "", 17, 16, 12, 376, 280, Color.white);
            var advice = Panel(live, "Command advice", 448, 112, 650, 230);
            notice = Label(advice, "Command feedback", "", 17, 14, 10, 622, 65, BattleUiStyle.Accent);
            controlsPage = Label(advice, "Controls page", "", 16, 14, 87, 490, 28, BattleUiStyle.Accent);
            Button(advice, "Previous controls", "<", 518, 83, 50, 32, () => ChangeControls(-1));
            Button(advice, "Next controls", ">", 586, 83, 50, 32, () => ChangeControls(1));
            controls = Label(advice, "Movement hints", "", 20, 14, 124, 622, 96, Color.white);
            summitPanel = Panel(live, "Summit powers", 18, 370, 408, 338);
            Label(summitPanel, "Summit heading", "BOW & SUMMIT SPELLS", 16, 14, 12, 256, 28, BattleUiStyle.Accent);
            Button(summitPanel, "Select bow", "BOW / DRAW", 284, 10, 110, 30, () => { battle.ChooseSummitPower(null); Refresh(); }).GetComponentInChildren<Text>().fontSize = 14;
            for (int i = 0; i < summitPowers.Length; i++)
            {
                int slot = i;
                summitPowers[i] = Button(summitPanel, "Summit power " + i, "", 14 + i * 130, 54, 120, 70,
                    () => { battle.ChooseSummitPower(slot); if (world && battle.SelectedSummitPower == slot) CloseCommandMenu(); else Refresh(); });
                summitPowers[i].GetComponentInChildren<Text>().fontSize = 13;
            }
            summitInfo = Label(summitPanel, "Summit instructions", "", 14, 14, 134, 380, 112, Color.white);
            CreateTowerPulseControls();
            CreateDefensePanel();
            var minimapPanel = Panel(live, "Height-based minimap", 0, 16, 304, 338);
            AnchorRight(minimapPanel, 18, 16);
            mapTitle = Label(minimapPanel, "Map heading", "", 16, 14, 10, 276, 26, BattleUiStyle.Accent);
            var mapObj = new GameObject("Tower scouting map", typeof(RectTransform), typeof(TowerMinimapGraphic)); mapObj.transform.SetParent(minimapPanel, false);
            map = mapObj.GetComponent<TowerMinimapGraphic>(); map.Initialize(battle, player); map.material = overlay;
            BattleUiStyle.Place(map.rectTransform, 14, 44, 276, 250);
            mapLegend = Label(minimapPanel, "Map legend", "GOLD: bound tower / DOTS: living troops\nSelect a tower or plan a squad route.", 12, 14, 298, 276, 34, BattleUiStyle.Muted);
            var towerPanel = Panel(live, "Tower network", 0, 370, 304, 298); AnchorRight(towerPanel, 18, 370);
            towerNetworkTitle = Label(towerPanel, "Network heading", "TOWER NETWORK", 13, 12, 8, 194, 22, BattleUiStyle.Accent);
            previousTowers = Button(towerPanel,"Previous towers","<",214,6,34,26,()=>ChangeTowerPage(-1));
            nextTowers = Button(towerPanel,"Next towers",">",254,6,34,26,()=>ChangeTowerPage(1));
            previousTowers.GetComponentInChildren<Text>().fontSize = nextTowers.GetComponentInChildren<Text>().fontSize = 16;
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int index = i;
                towerButtons[i] = Button(towerPanel, "Tower " + i, "", 12, 38 + i * 35, 280, 30,
                    () => { int selected = towerPage * towerButtons.Length + index; if (selected < battle.Towers.Count) battle.SelectedTower = battle.Towers[selected]; });
                towerButtons[i].GetComponentInChildren<Text>().fontSize = 13;
            }
            rosterPanel = Panel(live, "Live recruitment", 18, 0, 1080, 190); AnchorBottom(rosterPanel, 18, 18);
            Label(rosterPanel, "Recruitment heading", "LIVE RECRUITMENT  /  DISMOUNT TO PLACE", 16, 16, 10, 760, 26, BattleUiStyle.Accent);
            pageText = Label(rosterPanel, "Roster page", "", 15, 768, 10, 160, 26, BattleUiStyle.Muted);
            Button(rosterPanel, "Previous page", "<", 936, 8, 56, 30, () => { page = (page + PageCount - 1) % PageCount; Refresh(); });
            Button(rosterPanel, "Next page", ">", 1002, 8, 56, 30, () => { page = (page + 1) % PageCount; Refresh(); });
            for (int i = 0; i < recruits.Length; i++)
            {
                int index = i;
                recruits[i] = Button(rosterPanel, "Recruit " + i, "", 16 + i % 4 * 262, 48 + i / 4 * 60, 196, 51, () =>
                {
                    int selected = page * 8 + index;
                    if (selected < rosterEntries.Count) BeginRecruitment(rosterEntries[selected].Role);
                    Refresh();
                });
                recruits[i].GetComponentInChildren<Text>().fontSize = 15;
            }
            Label(rosterPanel, "Live notice", "Combat and income continue while you recruit. Units retain their original classes, weapons and powers.", 13, 16, 170, 1048, 18, BattleUiStyle.Muted);
            reticle = new GameObject("Weapon reticle", typeof(RectTransform)).GetComponent<RectTransform>(); reticle.SetParent(live, false);
            reticle.anchorMin = reticle.anchorMax = reticle.pivot = Vector2.one * .5f; reticle.sizeDelta = Vector2.one * 20;
            var cross = BattleUiStyle.Box(reticle, "Horizontal", Color.white, overlay).rectTransform;
            cross.anchorMin = cross.anchorMax = cross.pivot = Vector2.one * .5f; cross.sizeDelta = new Vector2(16, 2);
            cross = BattleUiStyle.Box(reticle, "Vertical", Color.white, overlay).rectTransform;
            cross.anchorMin = cross.anchorMax = cross.pivot = Vector2.one * .5f; cross.sizeDelta = new Vector2(2, 16);
            target = Label(live, "Aimed character", "", 16, 480, 470, 550, 30, Color.white);
            var backdrop = BattleUiStyle.Box(canvas.transform, "Home and outcome veil", new Color(.012f, .025f, .035f, .91f), overlay);
            modal = backdrop.rectTransform; Stretch(modal); backdrop.raycastTarget = true;
            var card = Panel(modal, "Skyhold briefing", 0, 0, 1000, 630);
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f; card.anchoredPosition = Vector2.zero;
            Label(card, "Eyebrow", "W A R   /   H A N D - C O N T R O L L E D   T O W E R   W A R F A R E", 18, 40, 28, 920, 30, BattleUiStyle.Accent);
            modalTitle = Label(card, "Title", "WARDENS AND RAIDERS", 46, 36, 76, 928, 85, Color.white);
            modalCopy = Label(card, "Briefing", "", 22, 40, 180, 920, 245, Color.white);
            resultStats = Label(card, "Briefing detail", "", 17, 40, 435, 920, 70, BattleUiStyle.Muted);
            newMatch = Button(card, "Begin siege", "BEGIN THE SIEGE", 40, 537, 446, 54, () => battle.NewGame());
            home = Button(card, "Return home", "RETURN HOME", 510, 537, 450, 54, () => battle.GoHome());
            CreateMapSelection(card);
            blink = BattleUiStyle.Box(canvas.transform, "Comfort teleport blink", Color.black, overlay); Stretch(blink.rectTransform); blink.raycastTarget = false;
            deploymentRing = MakeLine("Deployment radius", new Color(.2f, .85f, 1, .8f), .12f);
            recruitmentPreview = MakeLine("Character spawn preview", new Color(.25f, 1, .55f), .07f);
            recruitmentPreview.loop = true; recruitmentPreview.positionCount = 64;
            pointerLine = MakeLine("Controller command ray", new Color(.8f, .9f, 1, .7f), .006f);
            CreateDefenseVisuals();
            CreateTroopOrderControls();
            CreateInterfaceTools(card);
            CreateVrHudTabs();
            CreateEasyCommander();
            CreateHandControls();
            pointerDot = new GameObject("UI pointing dot", typeof(RectTransform), typeof(BattleUiPointerGraphic)).GetComponent<BattleUiPointerGraphic>();
            pointerMaterial = new Material(overlay) { name = "Hand cursor overlay", renderQueue = 3600 };
            pointerLine.sharedMaterial = pointerMaterial; pointerLine.sortingOrder = 610;
            pointerDot.transform.SetParent(canvas.transform, false); pointerDot.material = pointerMaterial; pointerDot.raycastTarget = false;
            var cursorCanvas = pointerDot.gameObject.AddComponent<Canvas>(); cursorCanvas.overrideSorting = true; cursorCanvas.sortingOrder = 610;
            pointerDot.rectTransform.sizeDelta = Vector2.one * 28; pointerDot.gameObject.SetActive(false);
            CreateMenuAccess(); CreateSimpleInterface();
            CreateBow(); CreateBowGuide(); player.ShotFired += RecoilBow; Layout(); Refresh();
        }
        int PageCount => Mathf.Max(1, Mathf.CeilToInt(rosterEntries.Count / 8f));
        void ChangeTowerPage(int direction)
        {
            int count = Mathf.Max(1,Mathf.CeilToInt(battle.Towers.Count/(float)towerButtons.Length));
            towerPage = (towerPage + direction + count) % count; Refresh();
        }
        void ResetHudRound()
        {
            if (shownRound == battle.Director.RoundId) return;
            shownRound = battle.Director.RoundId; page = controlsIndex = towerPage = vrHudPage = 0;
            charactersOpen = guideOpen = false; vrHudVisible = true; vrHudGesture.Reset(); CancelPointer();
            RequestVrHudAnchor();
        }
        void ChangeControls(int direction)
        {
            controlsIndex = (controlsIndex + direction + 4) % 4;
            RefreshControls();
        }
        void RefreshControls()
        {
            string heading;
            switch (controlsIndex)
            {
                case 0:
                    heading = "MOVEMENT";
                    controls.text = world
                        ? "Left stick: move   /   Right stick: turn\nRight stick up / down: rise / descend\nX: dismount / remount"
                        : "WASD: move   /   Hold RMB: look\nF / C: rise / descend\nSpace: dismount / remount   /   Esc: free cursor";
                    if (player.CurrentUnit != null)
                        controls.text = world
                            ? "Left stick: move   /   Right stick: turn\nHead tracking: look\nLeft grip: return to your tower"
                            : "WASD: move   /   Mouse: look\nEsc: free cursor   /   Q: return to your tower";
                    break;
                case 1:
                    heading = "GROUND WORKS";
                    controls.text = world
                        ? "Dismount; descend near ground to build.\nDefenses: clear ground in the gold zones.\nChoose a card, then point + trigger to place.\nRotate: HUD button. Combat and income continue."
                        : "Space: dismount; stay near ground to build.\nDefenses: clear ground in the gold zones.\nChoose a card; LMB on ground or minimap to place.\nY: rotate defenses. Combat and income continue.";
                    break;
                case 2:
                    heading = "TOWERS";
                    controls.text = "Select a tower on the minimap or in Tower Network.\n" + (world
                        ? "Raise / Rebuild: spend gold on the selected tower.\nBind to Selected: switch to your owned tower."
                        : "U: raise / rebuild the selected tower.\nT: bind to your next standing tower.") +
                        "\nBinding changes require standing on your summit.";
                    break;
                default:
                    heading = "COMBAT";
                    controls.text = world
                        ? "Right trigger: attack   /   B: possess\nA: next ally   /   Left grip: return\nPowers: R grip, L trigger, R / L stick clicks\nY: characters / Menu: controller guide"
                        : "LMB: attack   /   E: possess   /   Q: return\nTab: next ally   /   1-4 (or G/H/J/K): powers\nZ: advance   /   X: hold   /   V: guard\nR: new siege";
                    if (player.CurrentUnit == null && battle.IsMounted)
                        controls.text = world
                            ? "Left hand: bow / Right trigger: pull + release\nR grip: Repulse / L trigger: Lightning (30g)\nR stick click: Restoration (25g)\nL stick click: Tower Wave / upgrades via HUD"
                            : "Hold / release LMB: bow / RMB: look\n1 / G: Repulse / 2 / H: Lightning (30g)\n3 / J: Restoration (25g) / 4 / K: Tower Wave\nWave upgrades: Shield Break, then Aftershock.";
                    break;
            }
            controlsPage.text = $"CONTROLS  {controlsIndex + 1} / 4   /   {heading}";
        }
        RectTransform Panel(Transform parent, string title, float x, float y, float w, float h)
        {
            var image = BattleUiStyle.Box(parent, title, background, overlay); image.raycastTarget = true;
            BattleUiStyle.Place(image.rectTransform, x, y, w, h); BattleUiStyle.Frame(image.rectTransform, overlay); return image.rectTransform;
        }
        Text Label(Transform parent, string title, string copy, int fontSize, float x, float y, float w, float h, Color color)
        {
            var label = BattleUiStyle.Label(parent, title, copy, fontSize, overlay); label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            BattleUiStyle.Place(label.rectTransform, x, y, w, h); return label;
        }
        Button Button(Transform parent, string title, string copy, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var button = BattleUiStyle.Button(parent, title, copy, overlay, action); BattleUiStyle.Place((RectTransform)button.transform, x, y, w, h);
            return button;
        }
        static void AnchorRight(RectTransform rect, float x, float y)
        { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one; rect.anchoredPosition = new Vector2(-x, -y); }
        static void AnchorBottom(RectTransform rect, float x, float y)
        { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); }
        static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = Vector2.one * .5f; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        LineRenderer MakeLine(string title, Color tint, float lineWidth)
        {
            var line = new GameObject(title, typeof(LineRenderer)).GetComponent<LineRenderer>(); line.transform.SetParent(transform, false);
            line.sharedMaterial = overlay; line.startColor = line.endColor = tint; line.startWidth = line.endWidth = lineWidth;
            line.useWorldSpace = true; line.enabled = false; return line;
        }
        void CreateBow()
        {
            bow = new GameObject("Summit longbow").transform; bow.SetParent(transform, false);
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (shader == null) shader = Shader.Find("Standard");
            bowMaterial = battle.Terrain.PrimitiveMaterial != null ? new Material(battle.Terrain.PrimitiveMaterial) : new Material(shader);
            Color wood = new Color(.38f, .23f, .1f), gold = new Color(.75f, .58f, .27f);
            Vector3 previous = BowLimbPoint(0, 0);
            for (int i = 1; i <= 16; i++)
            {
                Vector3 next = BowLimbPoint(i, 0);
                bowLimbs[i - 1] = BowRod(previous, next, .018f, i > 6 && i < 11 ? gold : wood); previous = next;
            }
            bowString = new GameObject("Drawable bow string", typeof(LineRenderer)).GetComponent<LineRenderer>();
            bowString.transform.SetParent(bow, false); bowString.useWorldSpace = false; bowString.sharedMaterial = overlay;
            bowString.startColor = bowString.endColor = new Color(.95f, .9f, .7f);
            bowString.startWidth = bowString.endWidth = .003f; bowString.positionCount = 3;
            bowArrow = new GameObject("Nocked arrow").transform; bowArrow.SetParent(bow, false);
            BowRod(new Vector3(0, 0, -.12f), new Vector3(0, 0, .75f), .005f, wood, bowArrow);
            BowRod(new Vector3(0, 0, .68f), new Vector3(0, 0, .78f), .012f, new Color(.65f, .7f, .72f), bowArrow);
            bow.gameObject.SetActive(false);
        }
        Transform BowRod(Vector3 from, Vector3 to, float radius, Color tint, Transform parent = null)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rod.name = "Longbow detail"; rod.transform.SetParent(parent != null ? parent : bow, false);
            rod.transform.localPosition = (from + to) * .5f; rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            rod.transform.localScale = new Vector3(radius * 2, (to - from).magnitude * .5f, radius * 2);
            var collider = rod.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = rod.GetComponent<Renderer>(); renderer.sharedMaterial = bowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", tint); properties.SetColor("_Color", tint); renderer.SetPropertyBlock(properties);
            return rod.transform;
        }
        static Vector3 BowLimbPoint(int index, float tension)
        {
            float y = -.46f + .92f * index / 16;
            float edge = Mathf.Abs(y) / .46f;
            return new Vector3(0, y, -BowDrawGesture.RestDistance + .12f * Mathf.Cos(edge * Mathf.PI * .5f) - tension * .1f * edge * edge);
        }
        void RecoilBow() => bowRecoil = 1;
        void Layout()
        {
            // The headset must keep a stereo canvas even when the mouse is used
            // in the editor mirror. Input ownership must not change rendering mode.
            bool wasWorld = world;
            bool showingResult = layoutPhase != battle.Director.Phase &&
                (battle.Director.Phase == BattlePhase.Victory || battle.Director.Phase == BattlePhase.Defeat);
            world = player.IsXrDisplayActive;
            layoutPhase = battle.Director.Phase;
            width = Screen.width; height = Screen.height;
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(null, true);
            canvas.worldCamera = player.ViewCamera; canvas.renderMode = world ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            // The editor/XR display size can differ from the visible Game viewport.
            // Fit to its actual pixels rather than CanvasScaler's rendering display.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = world ? 1 : Mathf.Max(.01f, Mathf.Min(width / 1440f, height / 900f));
            if (world)
            {
                rect.sizeDelta = new Vector2(1440, 900); rect.localScale = Vector3.one * .0016f;
                rect.pivot = Vector2.one * .5f;
                if (!menuAnchorReady || !wasWorld || showingResult) RequestVrHudAnchor();
            }
            LayoutVrViewEffects();
            // Overlay root transforms are driven by Unity. Resetting their scale
            // here breaks the cached canvas scale after a Game view resize.
            LayoutGuide();
            ApplyVrHudLayout();
        }
        void Update()
        {
            if (battle == null || canvas == null) return;
            if (world != player.IsXrDisplayActive || layoutPhase != battle.Director.Phase || width != Screen.width || height != Screen.height) Layout();
            BattleUiStyle.RestoreOverlayScale(canvas);
            ResetHudRound();
            InterfaceHotkeys();
            UpdateVrHudGesture();
            ProcessInput();
            if (Time.unscaledTime >= nextTextAt) { nextTextAt = Time.unscaledTime + .08f; Refresh(); }
        }
        void LateUpdate()
        {
            if (canvas == null) return;
            UpdateHandControls();
            var tint = blink.color; tint.a = player.BlinkOpacity; blink.color = tint;
            if (map.isActiveAndEnabled && Time.unscaledTime >= nextMapAt)
            { nextMapAt = Time.unscaledTime + .1f; map.SetVerticesDirty(); }
            bowRecoil = Mathf.MoveTowards(bowRecoil, 0, Time.deltaTime * 5);
            bool running = battle.Director.IsBattleRunning;
            deploymentRing.enabled = running && battle.CanDeploy && battle.Recruitment.HasValue && !battle.SelectedDefense.HasValue;
            if (deploymentRing.enabled)
            {
                deploymentRing.loop = true; deploymentRing.positionCount = 96;
                for (int i = 0; i < 96; i++)
                { float a = i * Mathf.PI * 2 / 96; deploymentRing.SetPosition(i, battle.BoundTower.transform.position + new Vector3(Mathf.Sin(a) * battle.DeploymentRadius, .14f, Mathf.Cos(a) * battle.DeploymentRadius)); }
            }
            UpdateDefenseVisuals();
            UpdateRecruitmentPreview();
            UpdateTroopRouteVisuals();
            bool useBow = !BlocksGameplayInput && !HandMenuVisible && battle.CanUseSummitActions && !battle.SelectedSummitPower.HasValue && !battle.SelectedDefense.HasValue && !battle.IsPlanningRoute;
            bow.gameObject.SetActive(useBow);
            if (useBow) UpdateBowPresentation();
            UpdateBowGuide();
            bool showReticle = !(player.PhysicalHandBow && battle.CanUseSummitActions) && !BlocksGameplayInput && running && (battle.IsPlanningRoute || battle.CanUseSummitActions || player.CurrentUnit != null) && !PointerOverUI && !ControllerOverUI;
            reticle.gameObject.SetActive(showReticle);
            if (showReticle && RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)reticle.parent, player.AimScreenPosition,
                world ? player.ViewCamera : null, out var local)) reticle.anchoredPosition = local;
        }
        public void Refresh()
        {
            ResetHudRound();
            bool running = battle.Director.IsBattleRunning;
            live.gameObject.SetActive(running); modal.gameObject.SetActive(!running);
            if (!running)
            {
                if (battle.Director.Phase == BattlePhase.Victory || battle.Director.Phase == BattlePhase.Defeat)
                { guideOpen = charactersOpen = false; vrHudVisible = true; }
                bool atHome = battle.Director.Phase == BattlePhase.Home;
                bool won = battle.Director.Phase == BattlePhase.Victory;
                RefreshMapSelection(atHome);
                modalTitle.text = atHome ? "WARDENS AND RAIDERS" : won ? "VICTORY" : "DEFEAT";
                modalTitle.fontSize = atHome ? 66 : 46;
                modalCopy.text = atHome ?
                    "Summon allies using gold.\nGuards, archers and healers. Summon repeatedly with no soldier limit.\nCapture their tower to win." :
                    (won ? "The enemy's bound tower has fallen or been captured." : "Your bound tower has fallen or been captured.");
                resultStats.text = atHome ? "MENU beside your left hand opens controls. Pinch to select.\nBow: pull with both hands, follow the curved guide, release." :
                    $"SIEGE TIME {battle.Director.BattleDuration:0}s / ALLIES {battle.Director.AliveBlue} / ENEMIES {battle.Director.AliveRed}\nNEW SIEGE resets both squads.";
                newMatch.GetComponentInChildren<Text>().text = atHome ? "BEGIN THE SIEGE" : "NEW SIEGE";
                home.gameObject.SetActive(!atHome);
                RefreshInterfaceTools();
                ApplyVrHudLayout();
                return;
            }
            RefreshControls();
            var bound = battle.BoundTower; var selected = battle.SelectedTower;
            towerPage = Mathf.Clamp(towerPage,0,Mathf.Max(0,Mathf.CeilToInt(battle.Towers.Count/(float)towerButtons.Length)-1));
            rosterPanel.gameObject.SetActive(battle.CanDeploy && !battle.IsPlanningRoute);
            RefreshDefenseControls();
            RefreshTroopOrderControls();
            summitPanel.gameObject.SetActive(battle.CanUseSummitActions);
            travel.interactable = battle.CanUseSummitActions && selected != null && selected != bound && selected.Owner == BattleTeam.Blue && !selected.IsBroken;
            for (int i = 0; i < summitPowers.Length; i++)
            {
                float remaining = battle.SummitPowerRemaining(i); int cost = TowerBattle.SummitPowerCost(i);
                string binding = player.IsUsingOpticalHands ? "SELECT" : world ? (i == 0 ? "R GRIP" : i == 1 ? "L TRIGGER" : "R STICK") : (i + 1).ToString();
                summitPowers[i].GetComponentInChildren<Text>().text = TowerBattle.SummitPowerName(i).ToUpperInvariant() + " [" + binding + "]\n" +
                    (cost == 0 ? "FREE" : cost + "g / CAST") + " / " + (remaining > 0 ? $"{remaining:0.0}s" : "READY");
                summitPowers[i].interactable = battle.CanUseSummitActions && remaining <= 0 && battle.BlueGold >= cost;
                summitPowers[i].image.color = battle.SelectedSummitPower == i ? new Color(.15f, .39f, .43f) : new Color(.08f, .13f, .15f);
            }
            RefreshTowerPulseControls();
            economy.text = $"{battle.BlueGold:0} GOLD   <color=#86BAC5>+{battle.Income(BattleTeam.Blue):0.##}/s</color>     ALLY {battle.Director.AliveBlue}   ENEMY {battle.Director.AliveRed}";
            var unit = player.CurrentUnit;
            target.text = player.AimedUnit != null ? player.AimedUnit.Definition.DisplayName + (player.AimedUnit.Team == BattleTeam.Blue ? " / " + (world ? "B" : "E") + " to possess" : battle.CanUseSummitActions ? " / REPEL OR STRIKE" : " / ENEMY") : "";
            mode.text = battle.IsPlanningRoute ? "PLAN TROOP ROUTE / CLICK GROUND" : unit != null ? "POSSESSING / " + unit.Definition.DisplayName.ToUpperInvariant() : battle.IsMounted ? "SUMMIT / " +
                (battle.CanUseSummitActions ? battle.SelectedSummitPower.HasValue ? TowerBattle.SummitPowerName(battle.SelectedSummitPower.Value).ToUpperInvariant() : "BOW READY" : "POWERS LOCKED") : "DISMOUNTED / COMMAND VIEW";
            towerStats.text = $"BOUND: {bound.Title}  /  {bound.Height:0.#}m  /  {bound.Health:0} HP\nDEPLOY {battle.DeploymentRadius:0}m    SCOUT {battle.ScoutRadius:0}m";
            mount.GetComponentInChildren<Text>().text = unit != null ? (world ? "RETURN / L GRIP" : "RETURN TO TOWER [Q]") :
                (battle.IsMounted ? "DISMOUNT" : "REMOUNT") + (world ? " [X]" : " [SPACE]");
            if (player.IsUsingOpticalHands) mount.GetComponentInChildren<Text>().text = unit != null ? "RETURN TO TOWER" : battle.IsMounted ? "DISMOUNT" : "REMOUNT";
            possess.GetComponentInChildren<Text>().text = world ? "CHARACTERS [Y]" : "CHARACTERS [P]";
            if (player.IsUsingOpticalHands) possess.GetComponentInChildren<Text>().text = "CHARACTERS";
            build.GetComponentInChildren<Text>().text = selected == null ? "SELECT A TOWER" : (selected.IsBroken ? "REBUILD " : "RAISE +3m ") + selected.BuildCost + (player.IsUsingOpticalHands ? "g" : "g [U]");
            build.interactable = selected != null && (selected.IsBroken || selected.Owner == BattleTeam.Blue) && selected.Height < BattleTower.MaximumHeight && battle.BlueGold >= selected.BuildCost;
            possess.interactable = !player.IsSwitchingUnit;
            mount.interactable = !player.IsSwitchingUnit;
            notice.text = battle.Message;
            notice.color = BattleUiStyle.Accent;
            var flying = unit != null ? unit.Definition : battle.Recruitment.HasValue ? battle.Definition(battle.Recruitment.Value) : null;
            if (flying != null && flying.IsFlying && selected != null && selected.Owner != BattleTeam.Blue && !selected.IsBroken)
                notice.text = selected.CanBeRaidedBy(flying)
                    ? $"RAID IN REACH: {selected.Title} ({selected.Height:0.#}m).\nHold its summit for 8s, uninterrupted; capture costs 35 gold."
                    : $"TOWER TOO HIGH: {selected.Height:0.#}m / {flying.DisplayName} ceiling {flying.MaximumFlightHeight:0}m.\nDamage it first, or recruit a higher-reaching flyer.";
            if (selected != null && selected.CapturingTeam == BattleTeam.Blue && selected.CaptureProgress >= 1)
                notice.text = $"RAID WAITING FOR GOLD: {selected.Title}.\nKeep the summit secure; capture needs 35 gold.";
            if (bound.CapturingTeam == BattleTeam.Red && bound.CaptureProgress > 0)
            {
                notice.text = $"BOUND TOWER UNDER RAID: {bound.CaptureProgress:P0}\nHit the raider or contest the summit! About {(1 - bound.CaptureProgress) * BattleTower.CaptureSeconds:0.0}s remain.";
                notice.color = BattleUiStyle.Enemy;
            }
            mapTitle.text = battle.IsPlanningRoute ? "PLAN ROUTE / ADD POINTS" : map.IsBuildingMap ? "GROUND WORKS / ALL OWNED ZONES" :
                map.IsRoutingMap ? "ROUTE / FULL MAP" : $"SCOUTING  /  {bound.Height:0.#}m TOWER  /  {battle.ScoutRadius:0}m";
            if (world) mapTitle.text = battle.IsPlanningRoute ? "PLAN ROUTE" : map.IsBuildingMap ? "BUILD ZONES" :
                map.IsRoutingMap ? "TROOP ROUTE" : $"SCOUT / {bound.Height:0.#}m / {battle.ScoutRadius:0}m";
            mapLegend.text = battle.IsPlanningRoute ? "Click ground: add point / GO ROUTE: send\nGOLD: active route / WHITE: route plan" : map.IsBuildingMap ? "GOLD: build zone  /  CYAN: barricade\nORANGE: trap  /  Click ground: build" :
                map.IsRoutingMap ? "GOLD: troop route / Z: resume advancing\nTroops follow each waypoint, then siege." : "GOLD: bound tower / DOTS: living troops\nSelect a tower or plan a squad route.";
            BuildRosterEntries();
            page = Mathf.Clamp(page, 0, PageCount - 1);
            pageText.text = $"ROSTER {page + 1} / {PageCount}";
            for (int i = 0; i < recruits.Length; i++)
            {
                int index = page * 8 + i; bool exists = index < rosterEntries.Count; recruits[i].gameObject.SetActive(exists); if (!exists) continue;
                var definition = rosterEntries[index];
                recruits[i].GetComponentInChildren<Text>().text = definition.DisplayName + (definition.IsFlying ? $" / AIR {definition.MaximumFlightHeight:0}m" : "") + $"\n{definition.DeploymentCost}g  /  {definition.MaxHealth:0} HP";
                recruits[i].interactable = !player.IsSwitchingUnit && battle.BlueGold >= definition.DeploymentCost;
                recruits[i].image.color = battle.Recruitment == definition.Role ? new Color(.15f, .39f, .43f) : new Color(.08f, .13f, .15f);
            }
            for (int i = 0; i < towerButtons.Length; i++)
            {
                int index = towerPage * towerButtons.Length + i;
                towerButtons[i].gameObject.SetActive(index < battle.Towers.Count);
                if (index >= battle.Towers.Count) continue;
                var tower = battle.Towers[index]; string owner = tower.Owner == BattleTeam.Blue ? "ALLY" : tower.Owner == BattleTeam.Red ? "ENEMY" : "RUIN";
                string marker = tower == bound ? "* " : tower == battle.EnemyBoundTower ? "! " : "";
                towerButtons[i].GetComponentInChildren<Text>().text = marker + tower.Title + $" / {owner} / {tower.Height:0.#}m" + (tower.CaptureProgress > 0 ? $" {tower.CaptureProgress:P0}" : "");
                towerButtons[i].image.color = tower == selected ? new Color(.28f, .25f, .13f) : new Color(.08f, .13f, .15f);
                towerButtons[i].GetComponentInChildren<Text>().color = tower.Owner == BattleTeam.Blue ? BattleUiStyle.Friendly : tower.Owner == BattleTeam.Red ? BattleUiStyle.Enemy : BattleUiStyle.Muted;
            }
            int towerPages = Mathf.Max(1,Mathf.CeilToInt(battle.Towers.Count/(float)towerButtons.Length));
            towerNetworkTitle.text = $"TOWERS {towerPage+1}/{towerPages} / {battle.Towers.Count} TOTAL";
            previousTowers.interactable = nextTowers.interactable = towerPages > 1;
            if (battle.IsPlanningRoute)
                dossier.text = "STEER YOUR TROOPS\n\nClick walkable ground or the minimap to plan up to 8 waypoints. GO ROUTE sends all autonomous friendly troops along that path.\n\nThey fight nearby enemies without chasing off route, then resume the siege after the last point.\n\nN: plan / cancel. Enter: send. Backspace: undo. No gold cost.";
            else if (unit != null)
            {
                dossier.text = $"{unit.Definition.DisplayName.ToUpperInvariant()}  /  {unit.Health:0} HP  /  {unit.Shield:0} SHIELD\n{BattleClassCatalog.Attack(unit.Definition.Role)}  /  LMB\n";
                for (int i = 0; i < unit.Definition.AbilityCount; i++)
                    dossier.text += "\n" + BattleClassCatalog.Binding(i, world) + "  " + BattleClassCatalog.Ability(unit.Definition.Role, i).Name + (unit.GetAbilityCooldownRemaining(i) > 0 ? $"  {unit.GetAbilityCooldownRemaining(i):0.0}s" : "  READY");
                if (unit.Definition.IsFlying) dossier.text += $"\n\nALT {unit.transform.position.y:0.#}m / MAX {unit.Definition.MaximumFlightHeight:0}m\n" + (world ? "Right stick up/down: climb / descend." : "F / C: climb / descend.") + "\nRaid towers at or below your ceiling: 8s / 35g.";
            }
            else if (battle.SelectedDefense.HasValue)
            {
                var kind = battle.SelectedDefense.Value;
                dossier.text = DefenseCatalog.Name(kind).ToUpperInvariant() + $" / {DefenseCatalog.Cost(kind)} GOLD\n\n" + DefenseCatalog.Description(kind) +
                    "\n\nBuild around any owned standing tower, in its larger gold zone.\n\nGround command only. Y / Rotate turns the footprint 45 degrees. Traps arm after 1s and ignore allies and flyers.";
            }
            else if (battle.Recruitment.HasValue)
            {
                var definition = battle.Definition(battle.Recruitment.Value);
                dossier.text = $"{definition.DisplayName.ToUpperInvariant()} / {definition.DeploymentCost} GOLD\n" + (definition.IsFlying ? $"FLIGHT CEILING {definition.MaximumFlightHeight:0}m / RAID 8s + 35g" : BattleClassCatalog.Specialty(definition.Role)) + $"\n\n{BattleClassCatalog.Description(definition.Role)}\n\nLMB on clear ground or the minimap to recruit.";
            }
            else dossier.text = "THE RULES OF THE SIEGE\n\nSecure a broken foundation with troops, then rebuild for 35 gold.\n\nFlyers raid reachable summits: hold for 8s and pay 35 gold. Throw them away with Repulse to interrupt without damage.\n\nBow, throw, paid spells and binding changes require your standing tower's summit. Select an owned tower, then Bind or T.";
            RefreshInterfaceTools();
            ApplyVrHudLayout();
            RefreshEasyCommander();
        }
        void ProcessInput()
        {
            PointerOverUI = ControllerOverUI = false; pointerLine.enabled = false; pointerDot.gameObject.SetActive(false);
            if (ProcessMenuAccess()) return;
            if (world && player.WantsPhysicalBowInput)
            { requirePointerRelease = requireHandRelease = true; CancelPointer(); return; }
            if (world && battle.Director.IsBattleRunning && !vrHudVisible && !guideOpen && !charactersOpen)
            { requirePointerRelease = requireHandRelease = true; CancelPointer(); return; }
            bool handsOwnPointer = world && player.OpticalInputOwnsPointer;
            if (handsOwnPointer && mousePointer) { CancelPointer(); mousePointer = false; }
            var mouse = handsOwnPointer ? null : Mouse.current;
            bool mouseAvailable = mouse != null && !player.IsMouseLookCaptured;
            bool mouseActivity = mouseAvailable && (mouse.delta.ReadValue().sqrMagnitude > .01f || mouse.leftButton.wasPressedThisFrame);
            bool tracked = world && !handsOwnPointer && player.HasTrackedAim;
            Ray handRay = default; bool pinching = false;
            bool opticalTracked = world && !tracked && player.TryOpticalHandPointer(out handRay, out pinching);
            if (!opticalTracked || !pinching) handSelectionLatched = false;
            if (world && !tracked) requirePointerRelease = true;
            if (!opticalTracked) requireHandRelease = true;
            if (requirePointerRelease && tracked && !player.RightTriggerHeld) requirePointerRelease = false;
            if (requireHandRelease && opticalTracked && !pinching) requireHandRelease = false;
            bool useMouse = !handsOwnPointer && (!world || (!pointerDown && mouseActivity && (!opticalTracked || mouse.leftButton.isPressed)) || (mousePointer && pointerDown) || (!tracked && !opticalTracked));
            if (!pointerDown && tracked && player.RightTriggerHeld && !requirePointerRelease) useMouse = false;
            if (!pointerDown && opticalTracked && pinching && !requireHandRelease) useMouse = false;
            if (useMouse != mousePointer) { CancelPointer(); mousePointer = useMouse; }
            if (useMouse && mouseAvailable)
            {
                Vector2 position = mouse.position.ReadValue();
                if (!world) PointerOverUI = Dispatch(position, mouse.leftButton.isPressed);
                else
                {
                    var camera = player.ViewCamera;
                    var eye = camera.stereoEnabled ? Camera.MonoOrStereoscopicEye.Left : Camera.MonoOrStereoscopicEye.Mono;
                    var mouseRay = camera.ViewportPointToRay(new Vector3(position.x / Mathf.Max(1, Screen.width), position.y / Mathf.Max(1, Screen.height), 0), eye);
                    if (TryHudPoint(mouseRay, out var mousePoint)) PointerOverUI = DispatchWorld(mousePoint, mouse.leftButton.isPressed);
                    else CancelPointer();
                }
            }
            else if (tracked && !requirePointerRelease && TryControllerHudPoint(out var ray, out var point))
            {
                pointerLine.enabled = true; pointerLine.positionCount = 2;
                pointerLine.SetPosition(0, ray.origin); pointerLine.SetPosition(1, point);
                ControllerOverUI = DispatchWorld(point, player.RightTriggerHeld);
            }
            else if (opticalTracked)
            {
                if (TryHudPoint(handRay, out var handPoint))
                {
                    DrawPointerLine(handRay, handPoint);
                    ControllerOverUI = DispatchWorldPointer(handPoint, pinching && !requireHandRelease, true);
                }
                else
                {
                    if (pinching) requireHandRelease = true;
                    CancelPointer();
                    if (TryHudCursorPoint(handRay, out var edge))
                    { DrawPointerLine(handRay, edge); ShowPointerDot(edge, false); pointerDot.color = new Color(1, .8f, .2f); }
                }
            }
            else
            {
                if (tracked && player.RightTriggerHeld) requirePointerRelease = true;
                if (opticalTracked && pinching) requireHandRelease = true;
                CancelPointer();
            }
        }
        bool TryControllerHudPoint(out Ray ray, out Vector3 point)
        {
            ray = new Ray(player.RightHandAim.position, player.RightHandAim.forward);
            return TryHudPoint(ray, out point);
        }
        bool TryHudPoint(Ray ray, out Vector3 point)
        {
            point = default;
            var plane = new Plane(canvas.transform.forward, canvas.transform.position);
            if (!plane.Raycast(ray, out float distance) || distance <= 0 || distance > 10) return false;
            point = ray.GetPoint(distance);
            var rect = (RectTransform)canvas.transform;
            return rect.rect.Contains((Vector2)rect.InverseTransformPoint(point));
        }
        bool TryHudCursorPoint(Ray ray, out Vector3 point)
        {
            point = default;
            var plane = new Plane(canvas.transform.forward, canvas.transform.position);
            if (!plane.Raycast(ray, out float distance) || distance <= 0) return false;
            var rect = (RectTransform)canvas.transform;
            Vector3 local = rect.InverseTransformPoint(ray.GetPoint(distance));
            local.x = Mathf.Clamp(local.x, rect.rect.xMin + 20, rect.rect.xMax - 20);
            local.y = Mathf.Clamp(local.y, rect.rect.yMin + 20, rect.rect.yMax - 20);
            local.z = 0; point = rect.TransformPoint(local); return true;
        }
        void DrawPointerLine(Ray ray, Vector3 point)
        {
            pointerLine.enabled = true; pointerLine.positionCount = 2;
            pointerLine.SetPosition(0, ray.origin); pointerLine.SetPosition(1, point);
        }
        void ShowWorldHandPointer(Ray ray, bool pinching)
        {
            Vector3 point = Physics.Raycast(ray, out var hit, 100, ~0, QueryTriggerInteraction.Ignore) ? hit.point : ray.GetPoint(3);
            if (battle.SelectedDefense.HasValue && battle.TryDefenseAimPoint(ray, out var ground)) point = ground;
            DrawPointerLine(ray, point); ShowPointerDot(point, pinching);
            pointerDot.transform.rotation = player.ViewCamera.transform.rotation;
            pointerDot.transform.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(point, player.ViewCamera.transform.position) / 1.65f, .3f, 60);
        }
        [BeforeRenderOrder(210)]
        void UpdateControllerRay()
        {
            FinalizeVrHudAnchor(player != null && player.HasTrackedHeadPose);
            UpdateMenuAccess();
            if (bow != null && bow.gameObject.activeInHierarchy) UpdateBowPresentation();
            if (pointerLine == null || !pointerLine.enabled || !world) return;
            if (menuAccessPointer)
            {
                Ray accessRay = default;
                bool tracked = player.OpticalInputOwnsPointer ? player.TryOpticalHandPointer(out accessRay, out _, false) : player.HasTrackedAim;
                if (!player.OpticalInputOwnsPointer && tracked) accessRay = new Ray(player.RightHandAim.position, player.RightHandAim.forward);
                if (tracked && TryMenuAccessPoint(accessRay, out var accessPoint))
                { DrawPointerLine(accessRay, accessPoint); ShowPointerDot(accessPoint, pointerDown); pointerDot.transform.rotation = menuAccessCanvas.transform.rotation; }
                return;
            }
            // Tracking is refreshed after LateUpdate; keep the visible laser on
            // the same final pose as the hands without dispatching another click.
            if (!player.OpticalInputOwnsPointer && player.HasTrackedAim && TryControllerHudPoint(out var ray, out var point))
            { pointerLine.SetPosition(0, ray.origin); pointerLine.SetPosition(1, point); if (ControllerOverUI) ShowPointerDot(point, pointerDown); return; }
            if (HandMenuVisible && player.TryOpticalHandPointer(out var handRay, out _, false) && TryHudCursorPoint(handRay, out var handPoint))
            { DrawPointerLine(handRay, handPoint); ShowPointerDot(handPoint, pointerDown); return; }
            if (!HandMenuVisible && ShowHandWorldPointer && player.TryOpticalAim(out var worldRay, out bool pinching))
            { ShowWorldHandPointer(worldRay, pinching); return; }
            // Keep a visible aiming ray when a tracked hand points outside the menu.
            if (player.TryOpticalHandPointer(out var outsideRay, out _, false))
            { DrawPointerLine(outsideRay, outsideRay.GetPoint(3)); pointerDot.gameObject.SetActive(false); return; }
            pointerLine.enabled = false;
        }
        void UpdateBowPresentation()
        {
            if (player.TryBowPose(out var pose))
            {
                if (bow.parent != transform) bow.SetParent(transform, false);
                bow.SetPositionAndRotation(pose.position, pose.rotation);
            }
            else
            {
                if (bow.parent != player.ViewCamera.transform) bow.SetParent(player.ViewCamera.transform, false);
                bow.localPosition = new Vector3(.3f, -.17f, .56f) - Vector3.forward * (bowRecoil * .08f);
                bow.localRotation = Quaternion.Euler(bowRecoil * 6, -3, -8);
            }
            bow.localScale = Vector3.one;
            float pull = player.BowPull;
            float tension = Mathf.Clamp01(pull / BowDrawGesture.FullDraw);
            bowString.startColor = bowString.endColor = Color.Lerp(new Color(.95f, .9f, .7f), new Color(1, .65f, .15f), tension);
            for (int i = 0; i < bowLimbs.Length; i++)
            {
                Vector3 from = BowLimbPoint(i, tension), to = BowLimbPoint(i + 1, tension);
                bowLimbs[i].localPosition = (from + to) * .5f;
                bowLimbs[i].localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
                bowLimbs[i].localScale = new Vector3(.036f, (to - from).magnitude * .5f, .036f);
            }
            float stringDistance = player.BowStringDistance;
            bowString.SetPosition(0, BowLimbPoint(0, tension)); bowString.SetPosition(1, new Vector3(0, 0, -stringDistance));
            bowString.SetPosition(2, BowLimbPoint(16, tension)); bowArrow.localPosition = Vector3.back * (stringDistance - BowDrawGesture.RestDistance);
        }
        bool Dispatch(Vector2 position, bool held)
        {
            if (EventSystem.current == null) return false;
            if (pointer == null) pointer = new BattleUiPointerEventData(EventSystem.current);
            ((BattleUiPointerEventData)pointer).HasWorldPosition = false;
            pointer.position = position; pointer.button = PointerEventData.InputButton.Left;
            hits.Clear(); raycaster.Raycast(pointer, hits);
            GameObject target = hits.Count > 0 ? hits[0].gameObject : null;
            pointer.pointerCurrentRaycast = hits.Count > 0 ? hits[0] : default;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)canvas.transform, position,
                world ? player.ViewCamera : null, out var cursorPoint) &&
                ((RectTransform)canvas.transform).rect.Contains((Vector2)canvas.transform.InverseTransformPoint(cursorPoint))) ShowPointerDot(cursorPoint, held);
            return DispatchTarget(target, held);
        }
        bool handSelectionLatched;
        bool DispatchWorld(Vector3 point, bool held) => DispatchWorldPointer(point, held, false);
        bool DispatchWorldPointer(Vector3 point, bool held, bool selectOnPress, Canvas hitCanvas = null)
        {
            if (hitCanvas == null) hitCanvas = canvas;
            if (EventSystem.current == null) return false;
            if (pointer == null) pointer = new BattleUiPointerEventData(EventSystem.current);
            var worldPointer = (BattleUiPointerEventData)pointer;
            worldPointer.HasWorldPosition = true; worldPointer.WorldPosition = point;
            pointer.position = player.ViewCamera.WorldToScreenPoint(point);
            pointer.button = PointerEventData.InputButton.Left;
            Graphic best = null;
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(hitCanvas);
            for (int i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                if (!graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.canvasRenderer.cull || graphic.depth < 0) continue;
                // Test in panel coordinates, independent of the left/right eye projection.
                if (!graphic.rectTransform.rect.Contains((Vector2)graphic.rectTransform.InverseTransformPoint(point))) continue;
                if (!graphic.Raycast(pointer.position, player.ViewCamera)) continue;
                if (best == null || graphic.depth > best.depth) best = graphic;
            }
            pointer.pointerCurrentRaycast = best == null ? default : new RaycastResult {
                gameObject = best.gameObject, module = raycaster, worldPosition = point,
                worldNormal = -hitCanvas.transform.forward, depth = best.depth, screenPosition = pointer.position
            };
            ShowPointerDot(point, held);
            return DispatchTarget(best != null ? best.gameObject : null, held, selectOnPress);
        }
        void ShowPointerDot(Vector3 point, bool held)
        {
            pointerDot.gameObject.SetActive(true); pointerDot.transform.SetAsLastSibling();
            // The overlay shader and last-sibling order keep the cursor above buttons.
            // Keep it on the canvas plane so camera-space previews cannot clip it.
            pointerDot.transform.position = point;
            pointerDot.transform.rotation = canvas.transform.rotation; pointerDot.transform.localScale = Vector3.one;
            pointerDot.color = held ? new Color(.2f, 1, .65f) : new Color(.25f, 1, 1);
        }
        bool DispatchTarget(GameObject target, bool held, bool selectOnPress = false)
        {
            if (target != hovered)
            {
                if (hovered != null) ExecuteEvents.ExecuteHierarchy(hovered, pointer, ExecuteEvents.pointerExitHandler);
                hovered = target; pointer.pointerEnter = target;
                if (target != null) ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerEnterHandler);
                if (world && target != null && target.GetComponentInParent<Button>() is Button button && button.IsInteractable())
                    player.PulseRight(.06f, .018f);
            }
            if (held && !pointerDown && !(selectOnPress && handSelectionLatched))
            {
                if (target != null && target.transform.IsChildOf(canvas.transform)) PreserveSelectedMenuAnchor();
                pointerDown = true;
                pointer.pressPosition = pointer.position; pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                pressed = target != null ? ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler) : null;
                if (pressed == null && target != null) pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                pointer.pointerPress = pressed;
                if (selectOnPress)
                {
                    handSelectionLatched = true;
                    // An optical pinch is a single selection, not a mouse drag.
                    // Clear the press before callbacks that may close/rebuild the menu.
                    var selected = pressed; pressed = null; pointer.pointerPress = null;
                    if (selected != null)
                    {
                        ExecuteEvents.Execute(selected, pointer, ExecuteEvents.pointerUpHandler);
                        ExecuteEvents.Execute(selected, pointer, ExecuteEvents.pointerClickHandler);
                    }
                    return target != null;
                }
            }
            else if (!held && pointerDown)
            {
                pointerDown = false;
                var released = pressed; pressed = null; pointer.pointerPress = null;
                if (released != null)
                {
                    ExecuteEvents.Execute(released, pointer, ExecuteEvents.pointerUpHandler);
                    if (target != null && released == ExecuteEvents.GetEventHandler<IPointerClickHandler>(target))
                        ExecuteEvents.Execute(released, pointer, ExecuteEvents.pointerClickHandler);
                }
            }
            pointerDown = held; return target != null;
        }
        void CancelPointer()
        {
            if (pointerDot != null) pointerDot.gameObject.SetActive(false);
            if (pointer != null && pressed != null) ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            if (pointer != null && hovered != null) ExecuteEvents.ExecuteHierarchy(hovered, pointer, ExecuteEvents.pointerExitHandler);
            if (pointer != null) { pointer.pointerPress = null; pointer.pointerEnter = null; }
            pressed = hovered = null; pointerDown = false;
        }
        void OnDisable()
        {
            vrHudGesture.Reset();
            Application.onBeforeRender -= UpdateControllerRay;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (vrViewEffects != null) vrViewEffects.gameObject.SetActive(false);
            if (menuAccessCanvas != null) menuAccessCanvas.gameObject.SetActive(false);
            if (bow != null) bow.gameObject.SetActive(false);
            HideBowGuide();
            if (deploymentRing != null) deploymentRing.enabled = false;
            if (recruitmentPreview != null) recruitmentPreview.enabled = false;
            if (pointerLine != null) pointerLine.enabled = false;
            foreach (var line in defenseRings) if (line != null) line.enabled = false;
            if (defensePreview != null) defensePreview.enabled = false;
            if (troopRouteLine != null) troopRouteLine.enabled = false;
            foreach (var line in troopRouteMarkers) if (line != null) line.enabled = false;
            CancelPointer();
        }
        void OnEnable()
        {
            Application.onBeforeRender += UpdateControllerRay;
            if (canvas != null) canvas.gameObject.SetActive(true);
            if (vrViewEffects != null) vrViewEffects.gameObject.SetActive(world);
            RequestVrHudAnchor();
        }
        void OnDestroy()
        {
            if (player != null) { player.ShotFired -= RecoilBow; player.PossessionChanged -= RememberCharacter; }
            if (canvas != null) Destroy(canvas.gameObject); if (bow != null) Destroy(bow.gameObject);
            if (vrViewEffects != null) Destroy(vrViewEffects.gameObject);
            if (menuAccessCanvas != null) Destroy(menuAccessCanvas.gameObject);
            if (bowMaterial != null) Destroy(bowMaterial);
            if (pointerMaterial != null) Destroy(pointerMaterial);
            if (overlay != null) Destroy(overlay); if (eventSystem != null) Destroy(eventSystem);
        }
    }
}
