using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BattlefieldPrototype
{
    /// <summary>A mouse/controller-accessible tactical board for authoring the blue army.</summary>
    [DefaultExecutionOrder(210)]
    public sealed class BattleDeploymentMenu : MonoBehaviour
    {
        private BattleDirector director;
        private BattleDeployment deployment;
        private PossessionController player;
        private Canvas canvas;
        private CanvasScaler scaler;
        private GraphicRaycaster raycaster;
        private Material material;
        private RectTransform home, editor, board, markerRoot, detailsPanel, resultsPanel;
        private Text resultTitle, resultSubtitle, resultStats;
        private Image resultAccent;
        private bool detailsOpen;
        private float layoutWidth, layoutHeight;
        private int classPage, powerPage;
        private const int ClassesPerPage = 8;
        private readonly List<UnitRole> displayedRoles = new List<UnitRole>();
        private Text budgetText, messageText, selectedText;
        private Text classNameText, classInfoText, readinessText;
        private Image budgetFill;
        private BattleCharacterPreview editorPreview;
        private BattleUiEmblem selectedEmblem;
        private Button fightButton, replaceButton, removeButton, addButton, deselectButton;
        private readonly List<Button> roleButtons = new List<Button>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly Dictionary<BattleUnit, RectTransform> markers = new Dictionary<BattleUnit, RectTransform>();
        private readonly List<BattleUnit> staleMarkers = new List<BattleUnit>();
        private Image controllerCursor;
        private UnitRole chosenRole = UnitRole.Vanguard;
        private bool placementActive = true;
        private RectTransform mapContent;
        private float mapZoom = 1;
        private Vector2 mapCenter;
        private int presetSlot;
        private InputField presetName;
        private readonly string[] presetDrafts=new string[BattleDeployment.PresetSlots];
        private Vector2 arenaSize;
        private bool lastXR, lastWorldSpace, startHeld, usingDesktopPointer = true;
        private bool pointerDown;
        private GameObject hoveredObject, pressedObject;
        private PointerEventData pointerData;
        private InputAction mouseClick;
        private Mouse clickMouse;
        private bool mousePressPending, mouseReleasePending;
        private Vector2 mousePressPosition, mouseReleasePosition;
        private int lastWidth, lastHeight;
        private float nextRefresh;
        private string actionMessage = "Choose a class, then click in the battlefield.";

        public void Initialize(BattleDirector battle, PossessionController controller)
        {
            director = battle;
            deployment = battle.GetComponent<BattleDeployment>();
            player = controller;
            arenaSize = deployment.ArenaSize;
            material = BattleUiStyle.OverlayMaterial();
            var go = new GameObject("Battle Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.sortingOrder = 600;
            scaler = go.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3;
            raycaster = go.GetComponent<GraphicRaycaster>();
            // Route this modal menu directly from mouse/controller input. An XR
            // scene's active UI module may omit mouse bindings or consume them.
            // Unregister only this raycaster from that module to avoid double clicks;
            // its Raycast method remains available for our shared hit-testing path.
            raycaster.enabled = false;
            Color menuBackground = BattleUiStyle.Ink; menuBackground.a = 1;
            home = BattleUiStyle.Box(canvas.transform, "Home", menuBackground, material).rectTransform;
            editor = BattleUiStyle.Box(canvas.transform, "Deployment", menuBackground, material).rectTransform;
            // These are modal screens: clicks on empty space cannot reach gameplay.
            home.GetComponent<Image>().raycastTarget = true;
            editor.GetComponent<Image>().raycastTarget = true;
            resultsPanel = BattleUiStyle.Box(canvas.transform, "Battle Results", new Color(.018f, .028f, .05f, .94f), material).rectTransform;
            resultsPanel.GetComponent<Image>().raycastTarget = true;
            controllerCursor = BattleUiStyle.Box(canvas.transform, "Controller Cursor", Color.white, material);
            controllerCursor.rectTransform.sizeDelta = new Vector2(9, 9);
            controllerCursor.rectTransform.pivot = new Vector2(.5f, .5f);
            controllerCursor.gameObject.SetActive(false);
            director.PhaseChanged += OnPhaseChanged;
            deployment.Changed += Refresh;
            EnsureMouseInput();
            RebuildLayout();
            OnPhaseChanged(director.Phase);
        }

        private void OnPhaseChanged(BattlePhase phase)
        {
            CancelPointer();
            mousePressPending = mouseReleasePending = false;
            bool menu = phase != BattlePhase.Fighting;
            bool result = phase == BattlePhase.Victory || phase == BattlePhase.Defeat;
            if (result)
            {
                usingDesktopPointer = !player.IsUsingXR || player.IsDesktopInputActive;
                if (lastWorldSpace != (player.IsUsingXR && !usingDesktopPointer)) RebuildLayout();
            }
            detailsOpen = false;
            if (detailsPanel != null) detailsPanel.gameObject.SetActive(false);
            if (phase == BattlePhase.Setup)
            {
                arenaSize = deployment.ArenaSize;
                mapZoom = 1; mapCenter = Vector2.zero;
                RebuildLayout();
            }
            canvas.gameObject.SetActive(menu);
            home.gameObject.SetActive(phase == BattlePhase.Home);
            editor.gameObject.SetActive(phase == BattlePhase.Setup);
            resultsPanel.gameObject.SetActive(result);
            if (result)
            {
                player.OpenMenuCursor();
                UpdateResult();
            }
            else if (menu)
            {
                placementActive = phase == BattlePhase.Setup;
                actionMessage = "Choose a class, then click in the battlefield.";
                player.SetMenuView(new Vector3(0, 30, -13), Quaternion.Euler(65, 0, 0));
                Refresh();
            }
            else if (phase == BattlePhase.Fighting && player.CurrentUnit == null) player.CycleUnit();
        }

        private void LateUpdate()
        {
            if (canvas == null || player == null) return;
            if (lastXR != player.IsUsingXR && !Application.isEditor) usingDesktopPointer = !player.IsUsingXR;
            if (lastXR != player.IsUsingXR || lastWidth != Screen.width || lastHeight != Screen.height) RebuildLayout();
            BattleUiStyle.RestoreOverlayScale(canvas);
            if (!canvas.gameObject.activeSelf) return;
            // Simulator and integrating-scene scripts may have captured the mouse.
            // Army editing always uses a free, visible cursor.
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + .1f;
                Refresh();
            }
            var keyboard = Keyboard.current;
            bool namingArmy=presetName!=null && presetName.gameObject.activeInHierarchy && presetName.isFocused;
            if (keyboard != null && !namingArmy)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.xKey.wasPressedThisFrame) ConfirmScreen();
                if ((director.Phase == BattlePhase.Victory || director.Phase == BattlePhase.Defeat) && keyboard.rKey.wasPressedThisFrame) director.NewGame();
                if (director.Phase == BattlePhase.Setup)
                {
                    if (!detailsOpen && (keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)) RemoveSelected();
                    if (keyboard.escapeKey.wasPressedThisFrame) { if (detailsOpen) CloseDetails(); else Deselect(); }
                }
            }
            bool start = lastXR && player.TouchInput.Left.Primary;
            if (start && !startHeld && !namingArmy) ConfirmScreen();
            startHeld = start;
            UpdateMenuPointer();
            UpdateMapNavigation();
        }

        private void ConfirmScreen()
        {
            if (director.Phase == BattlePhase.Home) director.NewGame();
            else if (director.Phase == BattlePhase.Victory || director.Phase == BattlePhase.Defeat) director.NewGame();
            else if (detailsOpen) CloseDetails();
            else if (deployment.CanStart) director.BeginBattle();
        }

        private void RebuildLayout()
        {
            CancelPointer();
            lastXR = player.IsUsingXR;
            lastWorldSpace = lastXR && !usingDesktopPointer;
            lastWidth = Mathf.Max(1, Screen.width);
            lastHeight = Mathf.Max(1, Screen.height);
            var rect = (RectTransform)canvas.transform;
            rect.SetParent(lastWorldSpace ? player.ViewCamera.transform : null, false);
            canvas.worldCamera = player.ViewCamera;
            canvas.renderMode = lastWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = !lastWorldSpace;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            float scale = lastWorldSpace ? 1 : Mathf.Max(.1f, Mathf.Min(lastWidth / 1100f, lastHeight / 760f));
            scaler.scaleFactor = scale;
            float width = lastWorldSpace ? 1000 : lastWidth / scale;
            float height = lastWorldSpace ? 760 : lastHeight / scale;
            layoutWidth = width;
            layoutHeight = height;
            if (lastWorldSpace)
            {
                rect.sizeDelta = new Vector2(width, height);
                rect.localScale = Vector3.one * .00165f;
                rect.localPosition = new Vector3(0, 0, 1.5f);
                rect.localRotation = Quaternion.identity;
            }
            Stretch(home);
            Stretch(editor);
            Stretch(resultsPanel);
            ClearChildren(home);
            ClearChildren(editor);
            ClearChildren(resultsPanel);
            markers.Clear();
            roleButtons.Clear();
            displayedRoles.Clear();
            BuildHome(width, height);
            BuildEditor(width, height);
            BuildResults(width, height);
            controllerCursor.transform.SetAsLastSibling();
            Refresh();
        }

        private void BuildHome(float width, float height)
        {
            AddContours(home);
            float margin = 54, leftWidth = width * .48f;
            float top = Mathf.Max(24, (height - 660) * .5f);
            var crest = BattleUiStyle.Emblem(home, "War council crest", UnitRole.Paladin, BattleUiStyle.Accent, material);
            BattleUiStyle.Place(crest, margin, top, 40, 48);
            Label(home, "Eyebrow", "B A T T L E F I E L D", margin + 56, top + 8, leftWidth, 36, 25).color = BattleUiStyle.Accent;
            Label(home, "Edition", "TACTICAL WARFARE  /  FIRST-PERSON COMMAND", margin, top + 91, leftWidth, 30, 16).color = BattleUiStyle.Muted;
            var title = Label(home, "Title", "YOUR ARMY.\nYOUR COMMAND.", margin, top + 135, leftWidth + 12, 152, 51);
            title.fontStyle = FontStyle.Bold;
            Label(home, "Subtitle", "Assemble a force. Shape the battlefield.\nLead the charge through your soldiers' eyes.", margin, top + 304, leftWidth - 12, 72, 22).color = BattleUiStyle.Muted;
            var start = Button(home, "New Game", "FORGE YOUR ARMY    >", margin, top + 414, leftWidth - 60, 62, () => director.NewGame());
            Primary(start);
            var configuration = director.GetComponent<BattlefieldBootstrap>();
            string power = configuration.RandomizePowerBudget ? configuration.MinimumArmyPower + "–" + configuration.MaximumArmyPower : deployment.Budget.ToString();
            Label(home, "How to play", $"{deployment.Definitions.Count:00} CLASSES    /    {power} POWER PER SIDE", margin, top + 506, leftWidth, 30, 17).color = BattleUiStyle.Accent;
            Label(home, "Input", "ENTER TO BEGIN    /    VR: POINT + TRIGGER", margin, top + 551, leftWidth, 28, 16).color = BattleUiStyle.Muted;
            Label(home, "Steps", "01  RECRUIT    /    02  DEPLOY    /    03  COMMAND", margin, top + 604, leftWidth, 28, 14).color = BattleUiStyle.Muted;
            float mapX = width * .57f, mapWidth = width * .36f;
            Label(home, "Preview heading", "NEXT BATTLEFIELD   /   " + ((int)configuration.CurrentMapKind + 1).ToString("00") + " OF " + BattleMapNames.Count,
                mapX, top + 6, mapWidth, 28, 16).color = BattleUiStyle.Accent;
            var preview = Panel(home, "Battlefield preview", mapX, top + 46, mapWidth, 420);
            var graphicObject = new GameObject("Course minimap", typeof(RectTransform), typeof(BattleMinimapGraphic));
            graphicObject.transform.SetParent(preview, false);
            var graphic = graphicObject.GetComponent<BattleMinimapGraphic>();
            graphic.Director = director; graphic.Terrain = configuration; graphic.material = material; graphic.raycastTarget = false;
            BattleUiStyle.Place(graphic.rectTransform, 12, 12, mapWidth - 24, 396);
            Label(preview, "Compass", "N", mapWidth - 32, 15, 22, 24, 16, TextAnchor.MiddleCenter).color = BattleUiStyle.Text;
            Button(home, "Map Previous", "<", mapX, top + 480, 38, 38, () => ChooseMap(-1));
            Label(home, "Map Choice", configuration.MapName, mapX + 44, top + 480, mapWidth - 88, 38, 18, TextAnchor.MiddleCenter).color = BattleUiStyle.Accent;
            Button(home, "Map Next", ">", mapX + mapWidth - 38, top + 480, 38, 38, () => ChooseMap(1));
            Label(home, "Map Description", BattleMapNames.Description(configuration.CurrentMapKind), mapX, top + 529, mapWidth, 60, 15).color = BattleUiStyle.Muted;
            Button(home, "Map Rotation", configuration.RandomizeBattlefield ? "ROTATION: ON" : "ROTATION: OFF", mapX, top + 601, (mapWidth - 10) * .55f, 38, () =>
            {
                configuration.RandomizeBattlefield = !configuration.RandomizeBattlefield;
                configuration.PreviewBattlefield(configuration.CurrentMapKind);
                RebuildLayout();
            }).GetComponentInChildren<Text>().fontSize = 15;
            Button(home, "Shuffle Map", "SHUFFLE", mapX + (mapWidth - 10) * .55f + 10, top + 601, (mapWidth - 10) * .45f, 38, () =>
            {
                configuration.RandomizeBattlefield = true;
                configuration.PrepareHomeBattlefield();
                RebuildLayout();
            }).GetComponentInChildren<Text>().fontSize = 15;
        }

        void ChooseMap(int direction)
        {
            var terrain=director.GetComponent<BattlefieldBootstrap>();
            terrain.RandomizeBattlefield=false;
            terrain.MapKind=(BattleMapKind)(((int)terrain.MapKind+direction+BattleMapNames.Count)%BattleMapNames.Count);
            terrain.PreviewBattlefield(terrain.MapKind);
            RebuildLayout();
        }

        private void BuildEditor(float width, float height)
        {
            AddContours(editor);
            Label(editor, "Eyebrow", "WAR COUNCIL  /  DEPLOYMENT", 24, 17, width * .5f, 24, 15).color = BattleUiStyle.Accent;
            Label(editor, "Title", "ARMY FORGE", 22, 44, 380, 48, 36).fontStyle = FontStyle.Bold;
            budgetText = Label(editor, "Power", "", width * .47f, 26, width * .53f - 24, 32, 19, TextAnchor.MiddleRight);
            var budgetTrack = Panel(editor, "Power allocation", width * .57f, 68, width * .43f - 24, 7);
            budgetFill = BattleUiStyle.Box(budgetTrack, "Spent power", BattleUiStyle.Accent, material);
            BattleUiStyle.Place(budgetFill.rectTransform, 0, 0, 0, 7);
            float sideWidth = width < 1040 ? 224 : 248;
            float sideX = width - sideWidth - 24;
            float centerX = sideWidth + 44, centerWidth = sideX - centerX - 20;
            float bodyHeight = height - 262;
            var roster = Panel(editor, "Recruitment panel", 24, 112, sideWidth, bodyHeight);
            Label(roster, "Roster heading", "01   RECRUIT", 14, 12, sideWidth - 28, 28, 19).color = BattleUiStyle.Accent;
            int pages = Mathf.Max(1, Mathf.CeilToInt(deployment.Definitions.Count / (float)ClassesPerPage));
            classPage = Mathf.Clamp(classPage, 0, pages - 1);
            Label(roster, "Chooser", "CLASSES   " + (classPage + 1) + " / " + pages, 14, 49, sideWidth - 100, 25, 15).color = BattleUiStyle.Muted;
            Button(roster, "Class Previous", "<", sideWidth - 78, 46, 28, 28, () => ChangeClassPage(-1)).interactable = classPage > 0;
            Button(roster, "Class Next", ">", sideWidth - 44, 46, 28, 28, () => ChangeClassPage(1)).interactable = classPage < pages - 1;
            int index = 0;
            float cellWidth = (sideWidth - 34) / 2;
            float cellHeight = Mathf.Clamp((bodyHeight - 210) / 4, 60, 88);
            for (int roleIndex = classPage * ClassesPerPage; roleIndex < Mathf.Min(deployment.Definitions.Count, (classPage + 1) * ClassesPerPage); roleIndex++)
            {
                var definition = deployment.Definitions[roleIndex];
                UnitRole role = definition.Role;
                var button = Button(roster, "Choose " + role, "", 12 + index % 2 * (cellWidth + 10), 90 + index / 2 * (cellHeight + 8), cellWidth, cellHeight, () => Choose(role));
                var emblem = BattleUiStyle.Emblem(button.transform, "Class heraldry", role, BattleUiStyle.Accent, material);
                BattleUiStyle.Place(emblem, (cellWidth - 28) * .5f, 7, 28, 28);
                Label(button.transform, "Class name", definition.DisplayName, 2, 34, cellWidth - 4, 20, 15, TextAnchor.MiddleCenter);
                Label(button.transform, "Cost", definition.DeploymentCost + " PWR", 2, cellHeight - 19, cellWidth - 4, 17, 13, TextAnchor.MiddleCenter).color = BattleUiStyle.Muted;
                roleButtons.Add(button); displayedRoles.Add(role); index++;
            }
            BuildPresetControls(roster, sideWidth, bodyHeight);
            Label(editor, "Map heading", director.GetComponent<BattlefieldBootstrap>().MapName, centerX, 119, centerWidth - 72, 28, 17).color = BattleUiStyle.Accent;
            Button(editor, "Zoom Out", "−", centerX + centerWidth - 64, 117, 28, 28, () => ZoomMap(.8f));
            Button(editor, "Zoom In", "+", centerX + centerWidth - 30, 117, 28, 28, () => ZoomMap(1.25f));
            float mapHeight = Mathf.Min(bodyHeight - 106, centerWidth * arenaSize.y / arenaSize.x);
            float mapWidth = mapHeight * arenaSize.x / arenaSize.y;
            board = Panel(editor, "Deployment Board", centerX + (centerWidth - mapWidth) * .5f, 154, mapWidth, mapHeight);
            board.GetComponent<Image>().color = new Color(.09f, .125f, .13f);
            board.GetComponent<Image>().raycastTarget = true;
            board.gameObject.AddComponent<RectMask2D>();
            board.gameObject.AddComponent<DeploymentBoardInput>().Initialize(OnBoardClick);
            mapContent = new GameObject("Map content", typeof(RectTransform)).GetComponent<RectTransform>();
            mapContent.SetParent(board, false); Stretch(mapContent); mapContent.pivot = Vector2.one*.5f;
            DrawWorldRect("Playable ground", deployment.DeploymentBounds.center, deployment.DeploymentBounds.size, new Color(.12f,.28f,.26f,.3f));
            AddContours(mapContent);
            Color cover = new Color(.39f, .43f, .39f);
            foreach (var footprint in director.GetComponent<BattlefieldBootstrap>().CoverFootprints)
                DrawWorldRect("Terrain cover", footprint.center, footprint.size, cover);
            foreach(var gate in director.GetComponent<BattlefieldBootstrap>().MovingObstacles)
                DrawWorldRect("Moving gate (cycles during battle)",gate.ClosedBounds.center,gate.ClosedBounds.size,new Color(.85f,.52f,.18f,.85f));
            Label(board, "Deployment Label", "DEPLOY ON ANY CLEAR GROUND", 4, mapHeight - 26, mapWidth - 8, 23, 12, TextAnchor.MiddleCenter).color = BattleUiStyle.Friendly;
            Label(board, "Compass", "N", mapWidth - 29, 36, 22, 24, 16, TextAnchor.MiddleCenter).color = BattleUiStyle.Muted;
            markerRoot = new GameObject("Units", typeof(RectTransform)).GetComponent<RectTransform>();
            markerRoot.SetParent(mapContent, false);
            Stretch(markerRoot);
            UpdateMapTransform();
            for (int direction=0;direction<4;direction++)
            {
                int d=direction;
                Button(editor,"Pan "+direction,new[]{"<",">","^","v"}[direction],centerX+direction*27,164+mapHeight,23,23,
                    ()=>PanMap(d==0?Vector2.left:d==1?Vector2.right:d==2?Vector2.up:Vector2.down)).GetComponentInChildren<Text>().fontSize=14;
            }
            Label(editor, "Map legend", "SCROLL: ZOOM / MMB: PAN", centerX+112, 164 + mapHeight, centerWidth-112, 24, 11, TextAnchor.MiddleCenter).color = BattleUiStyle.Muted;
            Button(editor, "Reset Map", "RESET VIEW", centerX, 195 + mapHeight, centerWidth, 27, () =>
            {
                mapZoom = 1; mapCenter = Vector2.zero;
                UpdateMapTransform(); RefreshMarkers();
            }).GetComponentInChildren<Text>().fontSize = 13;
            var dossier = Panel(editor, "Selected class dossier", sideX, 112, sideWidth, bodyHeight);
            Label(dossier, "Dossier heading", "UNIT DOSSIER", 14, 12, sideWidth - 100, 25, 16).color = BattleUiStyle.Accent;
            Button(dossier, "Info", "INFO", sideWidth - 76, 8, 62, 32, OpenDetails).GetComponentInChildren<Text>().fontSize = 16;
            float portraitHeight = Mathf.Max(170, bodyHeight - 206);
            editorPreview = CreatePortrait(dossier, chosenRole, 2, 48, sideWidth - 4, portraitHeight);
            var insignia = BattleUiStyle.Emblem(dossier, "Selected heraldry", chosenRole, BattleUiStyle.Accent, material);
            BattleUiStyle.Place(insignia, 14, portraitHeight + 60, 28, 30); selectedEmblem = insignia.GetComponent<BattleUiEmblem>();
            classNameText = Label(dossier, "Selected class", "", 50, portraitHeight + 59, sideWidth - 58, 32, 22);
            classInfoText = Label(dossier, "Selected specialty", "", 14, portraitHeight + 98, sideWidth - 28, 49, 15);
            selectedText = Label(dossier, "Selection", "", 14, bodyHeight - 54, sideWidth - 28, 43, 14);
            float actionWidth = (width - 72) / 4;
            addButton = Button(editor, "Add New", "+  ADD NEW", 24, height - 134, actionWidth, 35, BeginPlacement);
            deselectButton = Button(editor, "Deselect", "DESELECT [ESC]", 32 + actionWidth, height - 134, actionWidth, 35, Deselect);
            removeButton = Button(editor, "Remove", "REMOVE UNIT", 40 + 2 * actionWidth, height - 134, actionWidth, 35, RemoveSelected);
            replaceButton = Button(editor, "Replace", "REPLACE SELECTED", 48 + 3 * actionWidth, height - 134, actionWidth, 35, ReplaceSelected);
            messageText = Label(editor, "Message", "", 24, height - 91, width - 48, 26, 16);
            Button(editor, "Home", "HOME", 24, height - 54, 86, 38, () => director.ReturnHome());
            Button(editor, "Randomize", "AUTO-FILL", 118, height - 54, 122, 38, () =>
            {
                deployment.MirrorEnemyArmy(); placementActive = false; actionMessage = deployment.LastMessage; Refresh();
            });
            Button(editor, "Clear", "CLEAR", 248, height - 54, 86, 38, () =>
            {
                deployment.Clear(); placementActive = true; actionMessage = "Army cleared. Choose a class or click clear ground to deploy."; Refresh();
            });
            readinessText = Label(editor, "Readiness", "", 350, height - 50, width - 650, 30, 14, TextAnchor.MiddleRight);
            fightButton = Button(editor, "Start Battle", "MARCH TO BATTLE   >", width - 280, height - 54, 256, 38, () => director.BeginBattle());
            Primary(fightButton);
            detailsPanel = BattleUiStyle.Box(editor, "Character Details", new Color(.035f, .044f, .052f, 1f), material).rectTransform;
            Stretch(detailsPanel);
            detailsPanel.GetComponent<Image>().raycastTarget = true;
            if (detailsOpen) BuildDetails();
            detailsPanel.gameObject.SetActive(detailsOpen);
        }

        private void Choose(UnitRole role)
        {
            chosenRole = role;
            placementActive = true;
            powerPage = 0;
            actionMessage = deployment.SelectedUnit == null ? "Click in the battlefield to place a " + role + "."
                : "Click the map to move your unit, or replace it with a " + role + ".";
            Refresh();
        }

        private void OpenDetails()
        {
            if (director.Phase != BattlePhase.Setup || detailsPanel == null) return;
            powerPage = 0;
            detailsOpen = true;
            BuildDetails();
            detailsPanel.gameObject.SetActive(true);
        }

        private void ChangeClassPage(int direction) { classPage += direction; RebuildLayout(); }

        private void BuildDetails()
        {
            if (detailsPanel == null) return;
            ClearChildren(detailsPanel);
            BattleUnitDefinition definition = null;
            foreach (var candidate in deployment.Definitions) if (candidate.Role == chosenRole) definition = candidate;
            if (definition == null) return;
            AddContours(detailsPanel);
            float portraitWidth = layoutWidth * .34f;
            float x = portraitWidth + 56, width = layoutWidth - x - 24;
            Color accent = BattleUiStyle.Accent;
            Label(detailsPanel, "Dossier eyebrow", "WAR COUNCIL  /  UNIT DOSSIER", 24, 20, layoutWidth - 48, 24, 16).color = accent;
            var line = BattleUiStyle.Box(detailsPanel, "Class Accent", accent, material);
            BattleUiStyle.Place(line.rectTransform, 24, 59, layoutWidth - 48, 1);
            var portrait = Panel(detailsPanel, "Character portrait", 24, 84, portraitWidth, layoutHeight - 180);
            CreatePortrait(portrait, chosenRole, 2, 2, portraitWidth - 4, layoutHeight - 184);
            var insignia = BattleUiStyle.Emblem(portrait, "Class seal", chosenRole, accent, material);
            BattleUiStyle.Place(insignia, 18, 18, 50, 56);
            Label(detailsPanel, "Class Name", definition.DisplayName.ToUpperInvariant(), x, 84, width, 52, 38).fontStyle = FontStyle.Bold;
            Label(detailsPanel, "Specialty", BattleClassCatalog.Specialty(chosenRole) + $"  /  {definition.BodyHeight:0.0}m", x, 139, width, 29, 18).color = accent;
            Label(detailsPanel, "Description", BattleClassCatalog.Description(chosenRole), x, 180, width, 68, 19).color = BattleUiStyle.Muted;
            Label(detailsPanel, "Stats", $"{definition.MaxHealth:0} HP     /     {definition.ArmorFraction:P0} ARMOR     /     {definition.CombatMoveSpeed:0.0} m/s     /     {definition.DeploymentCost} POWER",
                x, 250, width, 30, 18);
            var attack = Panel(detailsPanel, "Primary weapon", x, 293, width, 80);
            Label(attack, "Attack", $"PRIMARY  /  {BattleClassCatalog.Attack(chosenRole)}\n{definition.Damage:0} damage   /   {definition.AttackRange:0.#}m range   /   {definition.CombatAttackCooldown:0.##}s per attack",
                14, 10, width - 28, 62, 18);
            powerPage = Mathf.Clamp(powerPage, 0, Mathf.Max(0, (definition.AbilityCount - 1) / 2));
            for (int slot = powerPage * 2; slot < Mathf.Min(definition.AbilityCount, powerPage * 2 + 2); slot++)
            {
                var ability = BattleClassCatalog.Ability(chosenRole, slot);
                float y = 389 + slot % 2 * 101;
                var card = Panel(detailsPanel, "Ability " + slot, x, y, width, 93);
                Label(card, "Name", BattleClassCatalog.Binding(slot, lastWorldSpace) + "  " + ability.Name + $"    {definition.GetAbilityCooldown(slot):0.#}s", 12, 6, width - 24, 28, 21).color = accent;
                Label(card, "Description", ability.Description, 12, 38, width - 24, 49, 18).color = BattleUiStyle.Muted;
            }
            if (definition.AbilityCount == 0)
                Label(detailsPanel, "No Powers", "PRIMARY ATTACK SPECIALIST\nThis character relies on its weapon. Use its low cost to fill out your army.", x, 411, width, 106, 22).color = BattleUiStyle.Muted;
            if (definition.AbilityCount > 2)
            {
                Button(detailsPanel, "Power Previous", "<", x, layoutHeight - 128, 44, 30, () => { powerPage--; BuildDetails(); }).interactable = powerPage > 0;
                Label(detailsPanel, "Power Page", $"Powers {powerPage * 2 + 1}-{Mathf.Min(definition.AbilityCount, powerPage * 2 + 2)} of {definition.AbilityCount}",
                    x + 52, layoutHeight - 128, width - 104, 30, 18, TextAnchor.MiddleCenter).color = BattleUiStyle.Muted;
                Button(detailsPanel, "Power Next", ">", layoutWidth - 68, layoutHeight - 128, 44, 30, () => { powerPage++; BuildDetails(); }).interactable = powerPage == 0;
            }
            float buttonWidth = (layoutWidth - 44) / 2;
            Button(detailsPanel, "Back", "Back to army", 18, layoutHeight - 54, buttonWidth, 40, CloseDetails);
            Primary(Button(detailsPanel, "Use Class", "Choose " + definition.DisplayName, 26 + buttonWidth, layoutHeight - 54, buttonWidth, 40,
                () => { placementActive = true; CloseDetails(); }));
        }

        private void CloseDetails()
        {
            detailsOpen = false;
            if (detailsPanel != null) detailsPanel.gameObject.SetActive(false);
            actionMessage = deployment.SelectedUnit != null ? "Click Replace selected to change to " + chosenRole + "."
                : placementActive ? "Click clear battlefield ground to place a " + chosenRole + "."
                : "Nothing selected. Choose a class or Add new to deploy.";
            Refresh();
        }

        private void BuildResults(float width, float height)
        {
            AddContours(resultsPanel);
            float cardWidth = Mathf.Min(720, width - 36);
            float x = (width - cardWidth) / 2;
            float y = Mathf.Max(22, (height - 440) / 2);
            resultAccent = BattleUiStyle.Box(resultsPanel, "Accent", Color.white, material);
            BattleUiStyle.Place(resultAccent.rectTransform, x, y, cardWidth, 5);
            Label(resultsPanel, "Eyebrow", "BATTLE COMPLETE", x, y + 22, cardWidth, 30, 21, TextAnchor.MiddleCenter);
            resultTitle = Label(resultsPanel, "Title", "", x, y + 68, cardWidth, 85, 60, TextAnchor.MiddleCenter);
            resultSubtitle = Label(resultsPanel, "Subtitle", "", x + 14, y + 159, cardWidth - 28, 58, 24, TextAnchor.UpperCenter);
            var stats = BattleUiStyle.Box(resultsPanel, "Summary", BattleUiStyle.Panel, material);
            BattleUiStyle.Frame(stats.rectTransform, material);
            BattleUiStyle.Place(stats.rectTransform, x, y + 232, cardWidth, 76);
            resultStats = Label(stats.transform, "Stats", "", 12, 8, cardWidth - 24, 60, 22, TextAnchor.MiddleCenter);
            float bw = (cardWidth - 12) / 2;
            Primary(Button(resultsPanel, "New Army", "New army [R]", x, y + 340, bw, 54, () => director.NewGame()));
            Button(resultsPanel, "Home", "Home", x + bw + 12, y + 340, bw, 54, () => director.ReturnHome());
            Label(resultsPanel, "Hint", "Enter: build your next army", x, y + 407, cardWidth, 28, 18, TextAnchor.MiddleCenter);
            UpdateResult();
        }

        private void UpdateResult()
        {
            if (resultTitle == null) return;
            bool won = director.Phase == BattlePhase.Victory;
            Color accent = won ? new Color(.95f, .78f, .34f) : new Color(1f, .39f, .36f);
            resultTitle.text = won ? "VICTORY" : "DEFEAT";
            resultTitle.color = resultAccent.color = accent;
            resultSubtitle.text = won ? "Your army controls the battlefield.\nPrepare a new formation for the next fight."
                : "Your army has fallen.\nRethink your formation and try a different combination.";
            int seconds = Mathf.FloorToInt(director.BattleDuration);
            resultStats.text = $"BLUE SURVIVORS   {director.AliveBlue}     |     RED REMAINING   {director.AliveRed}\nBATTLE TIME   {seconds / 60:00}:{seconds % 60:00}";
        }

        private void Deselect()
        {
            placementActive = false;
            deployment.SelectUnit(null);
            actionMessage = "Deselected. Choose a class to deploy, or click a unit to edit it.";
            Refresh();
        }

        private void BeginPlacement()
        {
            placementActive = true;
            deployment.SelectUnit(null);
            actionMessage = "Click in the battlefield to add a " + chosenRole + ".";
            Refresh();
        }

        private void RemoveSelected()
        {
            if (deployment.SelectedUnit == null) return;
            deployment.Remove(deployment.SelectedUnit);
            actionMessage = "Unit removed. Its Power has been refunded.";
            Refresh();
        }

        private void ReplaceSelected()
        {
            if (deployment.SelectedUnit == null) return;
            deployment.TryReplace(deployment.SelectedUnit, chosenRole);
            actionMessage = deployment.LastMessage;
            Refresh();
        }

        private void OnBoardClick(PointerEventData data)
        {
            if (director.Phase != BattlePhase.Setup) return;
            Camera eventCamera = lastWorldSpace ? player.ViewCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapContent, data.position, eventCamera, out Vector2 local)) return;
            Vector2 size = mapContent.rect.size;
            Vector3 point = new Vector3(local.x / size.x * arenaSize.x, 0, local.y / size.y * arenaSize.y);
            // Map content uses a centered pivot and carries the zoom/pan transform.
            BattleUnit nearest = null;
            float nearestPixels = 13f * 13f;
            foreach (var unit in director.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Blue) continue;
                Vector2 pixelOffset = new Vector2((unit.transform.position.x - point.x) / arenaSize.x * size.x,
                    (unit.transform.position.z - point.z) / arenaSize.y * size.y);
                pixelOffset *= mapZoom;
                if (pixelOffset.sqrMagnitude >= nearestPixels) continue;
                nearest = unit;
                nearestPixels = pixelOffset.sqrMagnitude;
            }
            if (nearest != null)
            {
                if (nearest == deployment.SelectedUnit) { Deselect(); return; }
                placementActive = false;
                chosenRole = nearest.Definition.Role;
                deployment.SelectUnit(nearest);
                actionMessage = "Selected " + nearest.Definition.DisplayName + ". Click ground to move, or click this unit again to deselect.";
            }
            else
            {
                if (deployment.SelectedUnit != null)
                {
                    deployment.TryMove(deployment.SelectedUnit, point);
                    string moveMessage = deployment.LastMessage;
                    placementActive = false;
                    actionMessage = moveMessage;
                    Refresh();
                    return;
                }
                if (!placementActive)
                {
                    actionMessage = "Nothing selected. Choose a class or Add new to deploy.";
                    Refresh();
                    return;
                }
                if (deployment.TryPlace(chosenRole, point))
                {
                    // Stay in add mode so successive clicks can build a formation.
                    string placedMessage = deployment.LastMessage;
                    deployment.SelectUnit(null);
                    actionMessage = placedMessage;
                    Refresh();
                    return;
                }
                actionMessage = deployment.LastMessage;
            }
            Refresh();
        }

        private void Refresh()
        {
            if (deployment == null || budgetText == null) return;
            budgetText.text = "POWER  " + deployment.Spent + " / " + deployment.Budget + "     ENEMY  " + deployment.EnemySpent + " / " + deployment.Budget
                + "     UNITS  " + deployment.Count;
            if (budgetFill != null)
                budgetFill.rectTransform.sizeDelta = new Vector2(((RectTransform)budgetFill.transform.parent).rect.width * deployment.Spent / Mathf.Max(1, deployment.Budget), 7);
            var selected = deployment.SelectedUnit;
            selectedText.text = selected != null ? "SELECTED  /  " + selected.Definition.DisplayName + "\nMove on map or replace below."
                : placementActive ? "ADDING  /  " + chosenRole + "\nClick clear battlefield ground to deploy."
                : "NOTHING SELECTED\nChoose a class or click a unit.";
            selectedText.color = selected != null ? BattleUiStyle.Accent : BattleUiStyle.Muted;
            if (classNameText != null)
            {
                foreach (var definition in deployment.Definitions)
                    if (definition.Role == chosenRole)
                    {
                        classNameText.text = definition.DisplayName;
                        classInfoText.text = BattleClassCatalog.Specialty(chosenRole) + $"\n{definition.MaxHealth:0} HP   /   {definition.DeploymentCost} POWER";
                        classInfoText.color = BattleUiStyle.Muted;
                        break;
                    }
                editorPreview.SetRole(chosenRole);
                if (selectedEmblem.Role != chosenRole) { selectedEmblem.Role = chosenRole; selectedEmblem.SetVerticesDirty(); }
                readinessText.text = deployment.CanStart ? "FORMATION READY" : "DEPLOY YOUR FIRST UNIT";
                readinessText.color = deployment.CanStart ? BattleUiStyle.Accent : BattleUiStyle.Muted;
            }
            removeButton.interactable = selected != null;
            addButton.interactable = deployment.IsEditing;
            deselectButton.interactable = deployment.IsEditing && (selected != null || placementActive);
            replaceButton.interactable = selected != null && selected.Definition.Role != chosenRole &&
                deployment.Remaining + selected.Definition.DeploymentCost >= BattleDeployment.GetCost(chosenRole);
            fightButton.interactable = deployment.CanStart;
            messageText.text = actionMessage;
            for (int i = 0; i < roleButtons.Count; i++)
            {
                roleButtons[i].image.color = placementActive && displayedRoles[i] == chosenRole ? new Color(.28f, .25f, .18f) : new Color(.09f, .115f, .125f);
                bool affordable = BattleDeployment.GetCost(displayedRoles[i]) <= deployment.Remaining + (selected != null ? selected.Definition.DeploymentCost : 0);
                roleButtons[i].transform.Find("Cost").GetComponent<Text>().color = affordable ? BattleUiStyle.Muted : BattleUiStyle.Enemy;
            }
            RefreshMarkers();
            UpdateResult();
        }

        private void RefreshMarkers()
        {
            if (markerRoot == null) return;
            staleMarkers.Clear();
            foreach (var pair in markers)
                if (pair.Key == null || !pair.Key.IsAlive || !pair.Key.gameObject.activeInHierarchy) staleMarkers.Add(pair.Key);
            foreach (var stale in staleMarkers)
            {
                if (markers[stale] != null) Destroy(markers[stale].gameObject);
                markers.Remove(stale);
            }
            foreach (var unit in director.Units)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (!markers.TryGetValue(unit, out RectTransform marker))
                {
                    marker = BattleUiStyle.Box(markerRoot, unit.name, Color.white, material).rectTransform;
                    var icon = BattleUiStyle.Emblem(marker, "Role", unit.Definition.Role, BattleUiStyle.Text, material);
                    Stretch(icon); icon.offsetMin = new Vector2(4, 4); icon.offsetMax = new Vector2(-4, -4);
                    BattleUiStyle.Frame(marker, material, unit.Team == BattleTeam.Blue ? BattleUiStyle.Friendly : BattleUiStyle.Enemy);
                    markers.Add(unit, marker);
                }
                bool selected = unit == deployment.SelectedUnit;
                marker.GetComponent<Image>().color = selected ? new Color(.55f, .43f, .2f) : unit.Team == BattleTeam.Blue
                    ? new Color(.13f, .34f, .4f) : new Color(.42f, .16f, .13f);
                marker.anchorMin = marker.anchorMax = new Vector2(unit.transform.position.x / arenaSize.x + .5f,
                    unit.transform.position.z / arenaSize.y + .5f);
                marker.pivot = Vector2.one * .5f;
                marker.anchoredPosition = Vector2.zero;
                marker.sizeDelta = Vector2.one * (selected ? 24 : mapZoom>1.2f?22:deployment.Count+director.AliveRed>100?14:18) / mapZoom;
            }
        }

        private void UpdateMenuPointer()
        {
            controllerCursor.gameObject.SetActive(false);
            // XR session focus can differ from the Editor Game view's mouse focus.
            // Input System already controls which mouse events reach the player.
            var mouse = (mousePressPending || mouseReleasePending) && clickMouse != null ? clickMouse : Mouse.current;
            bool mouseUsed = mouse != null && (mouse.delta.ReadValue().sqrMagnitude > .01f ||
                mouse.leftButton.isPressed || mousePressPending || mouseReleasePending);
            bool pressed = lastXR && player.HasTrackedAim && player.RightTriggerHeld;
            // Mouse input wins when the simulator mirrors it into a trigger.
            if (!lastXR || mouseUsed) usingDesktopPointer = true;
            else if (pressed) usingDesktopPointer = false;
            if (lastWorldSpace != (lastXR && !usingDesktopPointer))
            {
                RebuildLayout();
                Canvas.ForceUpdateCanvases();
                return; // Dispatch retained mouse events after the new graphics are ready.
            }
            if (usingDesktopPointer)
            {
                if (mouse == null) { CancelPointer(); return; }
                Vector2 position = mouse.position.ReadValue();
                // Input-action callbacks retain both edges when an entire click
                // happens between player frames, including after XR pose updates.
                bool press = mousePressPending;
                bool release = mouseReleasePending;
                Vector2 pressPosition = mousePressPosition;
                Vector2 releasePosition = mouseReleasePosition;
                mousePressPending = mouseReleasePending = false;
                if (press && !pointerDown) ProcessPointer(pressPosition, true);
                ProcessPointer(release ? releasePosition : position, release ? false : mouse.leftButton.isPressed);
                return;
            }
            player.UseTrackedMenuAim();
            Ray ray = player.AimRay;
            var plane = new Plane(canvas.transform.forward, canvas.transform.position);
            if (!plane.Raycast(ray, out float distance) || distance < 0) { CancelPointer(); return; }
            Vector3 point = ray.GetPoint(distance);
            var canvasRect = (RectTransform)canvas.transform;
            Vector3 local = canvasRect.InverseTransformPoint(point);
            if (!canvasRect.rect.Contains(local)) { CancelPointer(); return; }
            controllerCursor.gameObject.SetActive(true);
            controllerCursor.transform.position = point;
            ProcessPointer(player.ViewCamera.WorldToScreenPoint(point), pressed);
        }

        private void ProcessPointer(Vector2 screenPosition, bool isPressed)
        {
            if (pointerData == null) pointerData = new PointerEventData(EventSystem.current);
            pointerData.position = screenPosition;
            pointerData.button = PointerEventData.InputButton.Left;
            uiHits.Clear();
            raycaster.Raycast(pointerData, uiHits);
            GameObject target = uiHits.Count > 0 ? uiHits[0].gameObject : null;
            pointerData.pointerCurrentRaycast = uiHits.Count > 0 ? uiHits[0] : default;
            if (target != hoveredObject)
            {
                if (hoveredObject != null) ExecuteEvents.ExecuteHierarchy(hoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
                hoveredObject = target;
                pointerData.pointerEnter = target;
                if (target != null) ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerEnterHandler);
            }
            if (isPressed && !pointerDown)
            {
                if(EventSystem.current!=null && (target==null || target.GetComponentInParent<InputField>()==null) &&
                    EventSystem.current.currentSelectedGameObject!=null && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>()!=null)
                    EventSystem.current.SetSelectedGameObject(null);
                pointerData.pressPosition = screenPosition;
                pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;
                pressedObject = ExecuteEvents.ExecuteHierarchy(target, pointerData, ExecuteEvents.pointerDownHandler);
                if (pressedObject == null) pressedObject = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                pointerData.pointerPress = pressedObject;
            }
            else if (!isPressed && pointerDown)
            {
                GameObject released = pressedObject;
                pressedObject = null;
                pointerData.pointerPress = null;
                pointerDown = false;
                if (released != null)
                {
                    ExecuteEvents.Execute(released, pointerData, ExecuteEvents.pointerUpHandler);
                    if (released == ExecuteEvents.GetEventHandler<IPointerClickHandler>(target))
                        ExecuteEvents.Execute(released, pointerData, ExecuteEvents.pointerClickHandler);
                }
                return;
            }
            pointerDown = isPressed;
        }

        private void CancelPointer()
        {
            if (pointerData != null)
            {
                if (pressedObject != null) ExecuteEvents.Execute(pressedObject, pointerData, ExecuteEvents.pointerUpHandler);
                if (hoveredObject != null) ExecuteEvents.ExecuteHierarchy(hoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
            }
            pressedObject = hoveredObject = null;
            pointerData = null;
            pointerDown = false;
        }

        private void OnMousePressed(InputAction.CallbackContext context)
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy || !(context.control.device is Mouse mouse)) return;
            clickMouse = mouse;
            mousePressPosition = mouse.position.ReadValue();
            mousePressPending = true;
        }

        private void OnMouseReleased(InputAction.CallbackContext context)
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy || !(context.control.device is Mouse mouse)) return;
            clickMouse = mouse;
            mouseReleasePosition = mouse.position.ReadValue();
            mouseReleasePending = true;
        }

        private void EnsureMouseInput()
        {
            if (mouseClick == null)
            {
                mouseClick = new InputAction("Army builder click", InputActionType.Button, "<Mouse>/leftButton");
                mouseClick.performed += OnMousePressed;
                mouseClick.canceled += OnMouseReleased;
            }
            if (isActiveAndEnabled) mouseClick.Enable();
        }

        private void DrawWorldRect(string name, Vector3 center, Vector3 size, Color color)
        {
            var rect = BattleUiStyle.Box(mapContent, name, color, material).rectTransform;
            rect.anchorMin = new Vector2((center.x - size.x * .5f) / arenaSize.x + .5f, (center.z - size.z * .5f) / arenaSize.y + .5f);
            rect.anchorMax = new Vector2((center.x + size.x * .5f) / arenaSize.x + .5f, (center.z + size.z * .5f) / arenaSize.y + .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void BuildPresetControls(RectTransform parent, float width, float height)
        {
            float y=height-86;
            Button(parent,"Previous Army Slot","<",12,y,26,25,()=>ChangePresetSlot(-1));
            Button(parent,"Next Army Slot",">",width-38,y,26,25,()=>ChangePresetSlot(1));
            var field=BattleUiStyle.Box(parent,"Army name",new Color(.10f,.13f,.14f),material);
            field.raycastTarget=true;
            BattleUiStyle.Place(field.rectTransform,44,y,width-88,25);
            presetName=field.gameObject.AddComponent<InputField>();
            var text=BattleUiStyle.Label(field.transform,"Name","",15,material,TextAnchor.MiddleLeft);
            Stretch(text.rectTransform); text.rectTransform.offsetMin=new Vector2(5,0); text.rectTransform.offsetMax=new Vector2(-5,0);
            presetName.textComponent=text; presetName.characterLimit=32; presetName.caretColor=BattleUiStyle.Accent;
            presetName.text=presetDrafts[presetSlot] ?? deployment.GetPresetName(presetSlot) ?? "Army "+(presetSlot+1);
            presetName.onValueChanged.AddListener(value=>presetDrafts[presetSlot]=value);
            Button(parent,"Save Army","SAVE "+(presetSlot+1),12,y+34,(width-32)/2,28,()=>
            { deployment.SavePreset(presetSlot,presetName.text); actionMessage=deployment.LastMessage; Refresh(); });
            Button(parent,"Load Army","LOAD",20+(width-32)/2,y+34,(width-32)/2,28,()=>
            {
                bool loaded=deployment.LoadPreset(presetSlot);
                actionMessage=deployment.LastMessage;
                if (loaded) { placementActive=false; presetName.text=deployment.GetPresetName(presetSlot) ?? "Army "+(presetSlot+1); }
                Refresh();
            });
        }

        private void ChangePresetSlot(int direction)
        {
            presetSlot=(presetSlot+direction+BattleDeployment.PresetSlots)%BattleDeployment.PresetSlots;
            presetName.text=presetDrafts[presetSlot] ?? deployment.GetPresetName(presetSlot) ?? "Army "+(presetSlot+1);
            presetName.transform.parent.Find("Save Army/Label").GetComponent<Text>().text="SAVE "+(presetSlot+1);
        }

        private void ZoomMap(float factor)
        {
            mapZoom=Mathf.Clamp(mapZoom*factor,1,6);
            if (deployment.SelectedUnit!=null) mapCenter=new Vector2(deployment.SelectedUnit.transform.position.x,deployment.SelectedUnit.transform.position.z);
            UpdateMapTransform(); RefreshMarkers();
        }

        private void PanMap(Vector2 direction)
        { mapCenter+=Vector2.Scale(direction,arenaSize)/(mapZoom*5); UpdateMapTransform(); }

        private void UpdateMapTransform()
        {
            if (mapContent==null) return;
            Vector2 limit=arenaSize*.5f*(1-1/mapZoom);
            mapCenter=new Vector2(Mathf.Clamp(mapCenter.x,-limit.x,limit.x),Mathf.Clamp(mapCenter.y,-limit.y,limit.y));
            mapContent.localScale=Vector3.one*mapZoom;
            mapContent.anchoredPosition=new Vector2(-mapCenter.x/arenaSize.x*mapContent.rect.width*mapZoom,
                -mapCenter.y/arenaSize.y*mapContent.rect.height*mapZoom);
        }

        private void UpdateMapNavigation()
        {
            if (director.Phase!=BattlePhase.Setup || detailsOpen || mapContent==null || Mouse.current==null) return;
            var mouse=Mouse.current;
            if (!RectTransformUtility.RectangleContainsScreenPoint(board,mouse.position.ReadValue(),lastWorldSpace?player.ViewCamera:null)) return;
            float scroll=mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll)>.1f) ZoomMap(scroll>0?1.25f:.8f);
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta=mouse.delta.ReadValue()/Mathf.Max(.1f,scaler.scaleFactor);
                mapCenter-=new Vector2(delta.x/board.rect.width*arenaSize.x,delta.y/board.rect.height*arenaSize.y)/mapZoom;
                UpdateMapTransform();
            }
        }

        private Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var text = BattleUiStyle.Label(parent, name, value, size, material, alignment);
            BattleUiStyle.Place(text.rectTransform, x, y, w, h);
            return text;
        }

        private Button Button(Transform parent, string name, string value, float x, float y, float w, float h, UnityEngine.Events.UnityAction action)
        {
            var button = BattleUiStyle.Button(parent, name, value, material, action);
            BattleUiStyle.Place((RectTransform)button.transform, x, y, w, h);
            return button;
        }

        private RectTransform Panel(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = BattleUiStyle.Box(parent, name, BattleUiStyle.Panel, material).rectTransform;
            BattleUiStyle.Place(rect, x, y, width, height);
            BattleUiStyle.Frame(rect, material);
            return rect;
        }

        private static void Primary(Button button)
        {
            button.image.color = BattleUiStyle.Accent;
            var text = button.transform.Find("Label").GetComponent<Text>();
            text.color = new Color(.07f, .08f, .075f);
            text.fontStyle = FontStyle.Bold;
        }

        private void AddContours(RectTransform parent)
        {
            var obj = new GameObject("Topographic engraving", typeof(RectTransform), typeof(BattleMapGraphic));
            obj.transform.SetParent(parent, false);
            var graphic = obj.GetComponent<BattleMapGraphic>();
            graphic.material = material;
            graphic.GridOnly = parent == mapContent;
            graphic.color = new Color(1, 1, 1, parent == mapContent ? 1 : .16f);
            graphic.raycastTarget = false;
            Stretch(graphic.rectTransform);
        }

        private BattleCharacterPreview CreatePortrait(Transform parent, UnitRole role, float x, float y, float width, float height)
        {
            var obj = new GameObject("Model preview", typeof(RectTransform), typeof(RawImage), typeof(BattleCharacterPreview));
            obj.transform.SetParent(parent, false);
            BattleUiStyle.Place((RectTransform)obj.transform, x, y, width, height);
            var portrait = obj.GetComponent<BattleCharacterPreview>();
            portrait.Initialize(director.GetComponent<BattlefieldBootstrap>(), role, material);
            return portrait;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            while (parent.childCount > 0)
            {
                var child = parent.GetChild(parent.childCount - 1);
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void OnDisable() { mouseClick?.Disable(); CancelPointer(); mousePressPending = mouseReleasePending = false; if (canvas != null) canvas.gameObject.SetActive(false); }
        private void OnEnable()
        {
            EnsureMouseInput();
            if (canvas == null) return;
            // Runtime UnityEvent listeners do not survive a script-domain reload.
            // Rebuild live controls as well as the input binding on re-enable.
            RebuildLayout();
            OnPhaseChanged(director.Phase);
        }
        private void OnDestroy()
        {
            if (director != null) director.PhaseChanged -= OnPhaseChanged;
            if (deployment != null) deployment.Changed -= Refresh;
            if (canvas != null) Destroy(canvas.gameObject);
            if (material != null) Destroy(material);
            mouseClick?.Dispose();
        }
    }

    public sealed class DeploymentBoardInput : MonoBehaviour, IPointerClickHandler
    {
        private System.Action<PointerEventData> clicked;
        public void Initialize(System.Action<PointerEventData> action) => clicked = action;
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) clicked?.Invoke(eventData);
        }
    }
}
