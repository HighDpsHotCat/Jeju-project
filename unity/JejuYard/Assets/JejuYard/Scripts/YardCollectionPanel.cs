using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Jeju.Yard
{
    [DisallowMultipleComponent]
    public sealed class YardCollectionPanel : MonoBehaviour
    {
        public sealed class Entry
        {
            public string id, name;
            public string firstMetDate, firstMetLocation;
            public Texture preview;
            public bool placed, supported = true;
        }

        static readonly IReadOnlyList<Entry> Empty = Array.Empty<Entry>();
        static YardCollectionPanel active;
        static int closedFrame = -10;
        public static bool BlocksYardInput => (active != null && active.IsOpen) || Time.frameCount == closedFrame;
        public bool IsOpen { get; private set; }
        public bool IsDetailOpen => selectedId != null;
        public bool IsResidentOpen { get; private set; }
        public event Action Opening;
        public event Action InteractRequested;
        public event Action HouseExitRequested;

        IReadOnlyList<Entry> entries = Empty;
        readonly List<int> rows = new List<int>();
        Action<string> toggle;
        Action importGuide, useDemo;
        string collectionLabel = "나의 도감";
        GameObject uiObject;
        PanelSettings settings;
        FontAsset typeface;

        VisualElement root, overlay, sheet, footer, emptyState;
        VisualElement detail, detailSheet, detailStage, detailActions;
        Image detailModel;
        PawMark detailPlaceholder;
        Label detailName, detailDate, detailLocation;
        Button detailClose, detailAction;
        ScrollView detailBody, gridScroller;
        Vector2 savedGridOffset;
        string selectedId;
        ListView grid;
        Button launcher, closeButton, importButton, demoButton;
        Button interactButton, houseBubble;
        BubbleTail bubbleTail;
        bool nearbyAvailable, targetIsHouse, hasHouseAnchor;
        Vector2 houseAnchor;
        const float BubbleWidth = 110f, BubbleHeight = 38f;
        Label title, counts, notice, emptyTitle;
        const int RowHeight = 151;
        int columns = 2, screenW, screenH;
        Rect safeArea;

        // World input uses Unity's bottom-left pixel origin. UITK picks in scaled top-left panel coordinates.
        public static bool BlocksPointerStart(Vector2 screenPoint)
        {
            if (BlocksYardInput) return true;
            if (active == null || active.root?.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(active.root.panel, new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            bool Over(Button button) => button != null && button.resolvedStyle.display != DisplayStyle.None && button.worldBound.Contains(p);
            return Over(active.interactButton) || Over(active.houseBubble) || Over(active.launcher);
        }

        public void SetNearby(bool available, bool atHouse, Vector2 houseScreenPoint)
        {
            bool valid = float.IsFinite(houseScreenPoint.x) && float.IsFinite(houseScreenPoint.y);
            if (nearbyAvailable == available && targetIsHouse == atHouse && valid == hasHouseAnchor &&
                (!valid || houseAnchor == houseScreenPoint)) return;
            nearbyAvailable = available;
            targetIsHouse = atHouse;
            hasHouseAnchor = valid;
            houseAnchor = houseScreenPoint;
            UpdateNearbyHud();
        }

        public void Bind(IReadOnlyList<Entry> items, Action<string> onToggle, string collectionLabel = null)
        {
            entries = items ?? Empty;
            toggle = onToggle;
            this.collectionLabel = string.IsNullOrEmpty(collectionLabel) ? "나의 도감" : collectionLabel;
            if (IsOpen) Refresh();
        }

        public void SetSources(Action importGuide, Action useDemo = null)
        {
            this.importGuide = importGuide;
            this.useDemo = useDemo;
            if (footer != null) UpdateActions();
        }

        public void ShowNotice(string message)
        {
            if (notice == null) return;
            notice.text = message ?? "";
            Show(notice, !string.IsNullOrEmpty(message));
            if (IsOpen) SizeSheet();
        }

        public void SetOpen(bool open)
        {
            if (IsOpen == open || root == null) return;
            IsOpen = open;
            Show(overlay, open);
            Show(launcher, !open);
            UpdateNearbyHud();
            if (open)
            {
                ShowNotice(null);
                Opening?.Invoke();
                Refresh();
                if (rows.Count > 0) grid.ScrollToItem(0);
                closeButton.Focus();
            }
            else
            {
                CloseDetail(false);
                closedFrame = Time.frameCount;
                launcher.Focus();
            }
        }

        public bool OpenResident(string speciesId)
        {
            if (root == null || IsOpen || string.IsNullOrEmpty(speciesId)) return false;
            foreach (var entry in entries)
            {
                if (entry.id != speciesId || !entry.placed || !entry.supported) continue;
                IsOpen = true;
                IsResidentOpen = true;
                Show(launcher, false);
                UpdateNearbyHud();
                OpenDetail(speciesId);
                return IsDetailOpen;
            }
            return false;
        }

        public void Refresh()
        {
            if (grid == null) return;
            int placed = 0;
            foreach (var entry in entries) if (entry.placed) placed++;
            title.text = collectionLabel;
            counts.text = $"등록 {entries.Count}종   ·   마당 {placed}종";
            Show(grid, entries.Count != 0);
            Show(emptyState, entries.Count == 0);
            int required = (entries.Count + columns - 1) / columns;
            if (rows.Count != required)
            {
                rows.Clear();
                for (int row = 0; row < required; row++) rows.Add(row);
            }
            SizeSheet();
            grid.RefreshItems();
            if (IsDetailOpen) RefreshDetail();
        }

        void OnEnable()
        {
            active = this;
            uiObject = new GameObject("Collection UI");
            uiObject.SetActive(false);
            uiObject.transform.SetParent(transform, false);
            var document = uiObject.AddComponent<UIDocument>();
            settings = Instantiate(Resources.Load<PanelSettings>("YardCollectionSettings"));
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("YardCollectionTheme");
            settings.sortingOrder = 100;
            document.panelSettings = settings;
            uiObject.SetActive(true);
            root = document.rootVisualElement;
            root.name = "yard-collection";
            root.AddToClassList("yard-root");
            root.pickingMode = PickingMode.Ignore;
            typeface = LoadTypeface();
            Build();
            ApplyLayout();
        }

        static FontAsset LoadTypeface()
        {
            // Select an installed Korean typeface so names remain legible offline on each platform.
            string[] families = Application.platform == RuntimePlatform.Android
                ? new[] { "Noto Sans CJK KR", "Noto Sans KR", "Noto Sans CJK JP" }
                : new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR" };
            foreach (string family in families)
            {
                var font = FontAsset.CreateFontAsset(family, "Regular", 64);
                if (font != null) return font;
            }
            return null;
        }

        void OnDisable()
        {
            if (active == this) active = null;
            CloseDetail(false);
            IsOpen = false;
            nearbyAvailable = targetIsHouse = hasHouseAnchor = false;
            if (uiObject != null) Destroy(uiObject);
            if (settings != null) Destroy(settings);
            if (typeface != null) Destroy(typeface);
            root = overlay = footer = null;
            grid = null;
            notice = null;
        }

        void Update()
        {
            if (root == null) return;
            if (screenW != Screen.width || screenH != Screen.height || safeArea != Screen.safeArea)
                ApplyLayout();
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsDetailOpen) CloseDetail();
                else SetOpen(false);
            }
        }

        void Build()
        {
            launcher = MakeButton("", "paw-button", "paw-button", () => SetOpen(true));
            launcher.tooltip = "등록한 동물 보기";
            launcher.Add(new PawMark());
            root.Add(launcher);
            BuildNearbyHud();

            overlay = Element("collection-overlay");
            overlay.name = "collection-overlay";
            root.Add(overlay);
            overlay.RegisterCallback<ClickEvent>(evt => { if (evt.target == overlay) SetOpen(false); });
            sheet = Element("collection-sheet");
            sheet.name = "collection-sheet";
            overlay.Add(sheet);

            var heading = Element("collection-heading");
            var text = Element("heading-text");
            title = MakeLabel(collectionLabel, "collection-title");
            counts = MakeLabel("", "collection-counts");
            text.Add(title);
            text.Add(counts);
            heading.Add(text);
            closeButton = MakeButton("", "collection-close", "close-button", () => SetOpen(false));
            closeButton.tooltip = "닫기";
            closeButton.Add(new CloseMark());
            heading.Add(closeButton);
            sheet.Add(heading);

            grid = new ListView
            {
                name = "registered-grid", itemsSource = rows, makeItem = MakeRow, bindItem = BindRow,
                fixedItemHeight = RowHeight, virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None, showBorder = false, reorderable = false
            };
            grid.AddToClassList("animal-grid");
            gridScroller = grid.Q<ScrollView>();
            sheet.Add(grid);

            emptyState = Element("empty-state");
            var emblem = Element("empty-emblem");
            emblem.Add(new PawMark());
            emptyState.Add(emblem);
            emptyTitle = MakeLabel("아직 등록한 동물이 없어요", "empty-title");
            emptyState.Add(emptyTitle);
            sheet.Add(emptyState);

            notice = MakeLabel("", "notice");
            notice.name = "collection-notice";
            sheet.Add(notice);
            Show(notice, false);
            footer = Element("collection-footer");
            importButton = MakeButton("도감 불러오기", "import-collection", "source-button", () => importGuide?.Invoke());
            demoButton = MakeButton("시연 도감 보기", "use-demo", "source-button", () => useDemo?.Invoke());
            footer.Add(importButton);
            footer.Add(demoButton);
            sheet.Add(footer);
            UpdateActions();
            BuildDetail();
            Show(overlay, false);
        }

        void BuildNearbyHud()
        {
            interactButton = MakeButton("", "nearby-interact", "nearby-interact", () =>
            {
                if (!IsOpen && nearbyAvailable && closedFrame != Time.frameCount) InteractRequested?.Invoke();
            });
            var disc = Element("nearby-disc");
            disc.pickingMode = PickingMode.Ignore;
            disc.Add(new InteractionTick());
            interactButton.Add(disc);
            root.Add(interactButton);

            houseBubble = MakeButton("마당 나가기", "house-exit-bubble", "house-exit-bubble", () =>
            {
                if (!IsOpen && hasHouseAnchor && closedFrame != Time.frameCount) HouseExitRequested?.Invoke();
            });
            houseBubble.style.width = BubbleWidth;
            houseBubble.style.height = BubbleHeight;
            bubbleTail = new BubbleTail();
            houseBubble.Add(bubbleTail);
            root.Add(houseBubble);
            Show(interactButton, false);
            Show(houseBubble, false);
        }

        void UpdateNearbyHud()
        {
            if (root?.panel == null || interactButton == null) return;
            Show(interactButton, nearbyAvailable && !IsOpen);
            interactButton.tooltip = targetIsHouse ? "마당 나가기" : "동물 만나기";
            bool showBubble = !IsOpen && hasHouseAnchor;
            Show(houseBubble, showBubble);
            if (!showBubble) return;
            var safe = SafeRect;
            float px = settings.scale;
            Vector2 anchor = new Vector2(Mathf.Clamp(houseAnchor.x, safe.xMin, safe.xMax),
                Mathf.Clamp(houseAnchor.y, safe.yMin + px * 5f, safe.yMax - px * 5f));
            var tip = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(anchor.x, Screen.height - anchor.y));
            float safeTop = (Screen.height - safe.yMax) / px;
            bool tailOnTop = tip.y < safeTop + BubbleHeight + 10f;
            float left = Mathf.Clamp(tip.x - BubbleWidth * .5f, safe.xMin / px + 4f, safe.xMax / px - BubbleWidth - 4f);
            houseBubble.style.left = left;
            houseBubble.style.top = tailOnTop ? tip.y + 8f : tip.y - BubbleHeight - 8f;
            bubbleTail.Point(tailOnTop, Mathf.Clamp(tip.x - left, 16f, BubbleWidth - 16f));
        }

        Rect SafeRect => safeArea.width > 0 && safeArea.height > 0 ? safeArea : new Rect(0, 0, Screen.width, Screen.height);

        void BuildDetail()
        {
            detail = Element("detail-backdrop");
            detail.name = "animal-detail";
            root.Add(detail);
            detail.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == detail) CloseDetail();
                evt.StopPropagation();
            });
            detailSheet = Element("detail-sheet");
            detailSheet.name = "detail-sheet";
            detail.Add(detailSheet);

            detailBody = new ScrollView(ScrollViewMode.Vertical);
            detailBody.AddToClassList("detail-body");
            var content = Element("detail-content");
            detailBody.Add(content);
            detailSheet.Add(detailBody);
            detailStage = Element("detail-stage");
            detailModel = new Image { name = "detail-model", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            detailModel.AddToClassList("detail-model");
            detailPlaceholder = new PawMark();
            detailPlaceholder.AddToClassList("detail-placeholder");
            detailStage.Add(detailModel);
            detailStage.Add(detailPlaceholder);
            content.Add(detailStage);

            detailName = MakeLabel("", "detail-name");
            detailName.name = "detail-name";
            content.Add(detailName);
            var records = Element("encounter-record");
            detailDate = AddRecord(records, "처음 만난 날", "detail-first-met-date");
            detailLocation = AddRecord(records, "처음 만난 장소", "detail-first-met-location");
            content.Add(records);

            detailActions = Element("detail-actions");
            detailAction = MakeButton("", "detail-action", "detail-action", () =>
            {
                if (IsDetailOpen && !IsResidentOpen) toggle?.Invoke(selectedId);
            });
            detailActions.Add(detailAction);
            detailSheet.Add(detailActions);
            detailClose = MakeButton("", "detail-close", "close-button", () => CloseDetail());
            detailClose.AddToClassList("detail-close");
            detailClose.tooltip = "동물 정보 닫기";
            detailClose.Add(new CloseMark());
            detailSheet.Add(detailClose);
            Show(detail, false);
        }

        Label AddRecord(VisualElement parent, string caption, string id)
        {
            var row = Element("encounter-row");
            row.Add(MakeLabel(caption, "encounter-label"));
            var value = MakeLabel("", "encounter-value");
            value.name = id;
            row.Add(value);
            parent.Add(row);
            return value;
        }

        void OpenDetail(string id)
        {
            if (!IsOpen || id == null || IsDetailOpen) return;
            selectedId = id;
            if (!IsResidentOpen)
            {
                savedGridOffset = gridScroller.scrollOffset;
                sheet.SetEnabled(false);
            }
            Show(detailActions, !IsResidentOpen);
            SizeDetail();
            Show(detail, true);
            detailBody.scrollOffset = Vector2.zero;
            RefreshDetail();
            if (IsDetailOpen) detailClose.Focus();
        }

        void RefreshDetail()
        {
            foreach (var entry in entries)
            {
                if (entry.id != selectedId) continue;
                if (IsResidentOpen && (!entry.placed || !entry.supported)) break;
                detailName.text = entry.name;
                detailDate.text = string.IsNullOrWhiteSpace(entry.firstMetDate) ? "기록 없음" : entry.firstMetDate;
                detailLocation.text = string.IsNullOrWhiteSpace(entry.firstMetLocation) ? "기록 없음" : entry.firstMetLocation;
                var studio = GetComponent<YardModelPortraits>();
                detailModel.image = studio != null ? studio.Inspect(entry.preview) ?? entry.preview : entry.preview;
                Show(detailPlaceholder, detailModel.image == null);
                detailAction.text = entry.placed ? "내보내기" : entry.supported ? "초대하기" : "모델 준비 중";
                detailAction.SetEnabled(!IsResidentOpen && (entry.supported || entry.placed));
                return;
            }
            CloseDetail();
        }

        void CloseDetail(bool restoreFocus = true)
        {
            if (!IsDetailOpen) return;
            string previous = selectedId;
            selectedId = null;
            detailModel.image = null;
            var studio = GetComponent<YardModelPortraits>();
            if (studio != null) studio.Inspect(null);
            Show(detail, false);
            if (IsResidentOpen)
            {
                IsResidentOpen = IsOpen = false;
                closedFrame = Time.frameCount;
                Show(launcher, true);
                UpdateNearbyHud();
                if (restoreFocus && nearbyAvailable) interactButton.Focus();
                return;
            }
            sheet.SetEnabled(true);
            if (restoreFocus)
            {
                (grid.Q<Button>("animal-" + previous) ?? closeButton).Focus();
                gridScroller.scrollOffset = savedGridOffset;
            }
        }

        void UpdateActions()
        {
            emptyTitle.text = useDemo != null ? "도감을 불러와 주세요" : "아직 등록한 동물이 없어요";
            Show(footer, importGuide != null || useDemo != null);
            Show(importButton, importGuide != null);
            Show(demoButton, useDemo != null);
        }

        void ApplyLayout()
        {
            screenW = Screen.width;
            screenH = Screen.height;
            safeArea = Screen.safeArea;
            float scale = screenW < screenH ? screenW / 400f : Mathf.Min(screenW / 810f, screenH / 600f);
            settings.scale = Mathf.Clamp(scale, .60f, 4f);
            float px = settings.scale;
            Rect safe = SafeRect;
            float width = safe.width / px;
            float right = (screenW - safe.xMax) / px, top = (screenH - safe.yMax) / px;
            launcher.style.right = right + 16;
            launcher.style.top = top + 16;
            interactButton.style.right = right + 18f;
            interactButton.style.top = top + safe.height / px * .70f - 32f;
            UpdateNearbyHud();
            foreach (var backdrop in new[] { overlay, detail })
            {
                backdrop.style.paddingLeft = safe.xMin / px;
                backdrop.style.paddingRight = right;
                backdrop.style.paddingTop = top;
                backdrop.style.paddingBottom = safe.yMin / px;
            }
            float sheetWidth = Mathf.Min(632f, width - 24f);
            sheet.style.width = sheetWidth;
            detailSheet.style.width = Mathf.Min(410f, width - 32f);
            SizeDetail();
            int newColumns = sheetWidth < 485 ? 2 : 3;
            if (newColumns != columns)
            {
                columns = newColumns;
                grid.Rebuild();
                if (IsOpen) Refresh();
            }
            SizeSheet();
        }

        void SizeDetail()
        {
            float height = Mathf.Min(IsResidentOpen ? 400f : 466f, SafeRect.height / settings.scale - 24f);
            detailSheet.style.height = height;
            detailStage.style.height = Mathf.Clamp(height - (IsResidentOpen ? 158f : 224f), 110f, 242f);
        }

        void SizeSheet()
        {
            float height = (safeArea.height > 0 ? safeArea.height : screenH) / settings.scale;
            float body = entries.Count == 0 ? 190f : Mathf.CeilToInt((float)entries.Count / columns) * RowHeight;
            float footerHeight = importGuide != null || useDemo != null ? 55f : 0f;
            float noticeHeight = string.IsNullOrEmpty(notice.text) ? 0f : 42f;
            sheet.style.height = Mathf.Min(700f, height - 32f, 104f + body + footerHeight + noticeHeight);
        }

        sealed class Tile
        {
            public Button root;
            public Image model;
            public PawMark placeholder;
            public Label name;
            public string id;
        }

        VisualElement MakeRow()
        {
            var row = Element("animal-row");
            for (int i = 0; i < columns; i++)
            {
                var tile = new Tile();
                tile.root = MakeButton("", "", "animal-tile", () => OpenDetail(tile.id));
                var stage = Element("model-stage");
                stage.pickingMode = PickingMode.Ignore;
                tile.model = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                tile.model.AddToClassList("model-view");
                tile.placeholder = new PawMark();
                tile.placeholder.AddToClassList("model-placeholder");
                stage.Add(tile.model);
                stage.Add(tile.placeholder);
                tile.name = MakeLabel("", "animal-name");
                tile.name.pickingMode = PickingMode.Ignore;
                tile.root.Add(stage);
                tile.root.Add(tile.name);
                tile.root.userData = tile;
                row.Add(tile.root);
            }
            return row;
        }

        void BindRow(VisualElement row, int index)
        {
            for (int col = 0; col < row.childCount; col++)
            {
                var tile = (Tile)row[col].userData;
                int i = index * columns + col;
                bool visible = i < entries.Count;
                tile.root.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
                if (!visible) { tile.id = null; tile.root.name = ""; tile.model.image = null; continue; }
                var e = entries[i];
                tile.id = e.id;
                tile.root.name = "animal-" + e.id;
                tile.name.text = e.name;
                tile.root.tooltip = e.name;
                tile.model.image = e.preview;
                Show(tile.placeholder, e.preview == null);
            }
        }

        static VisualElement Element(string css)
        {
            var e = new VisualElement();
            e.AddToClassList(css);
            return e;
        }

        Label MakeLabel(string text, string css)
        {
            var e = new Label(text) { enableRichText = false };
            e.AddToClassList(css);
            ApplyFont(e);
            return e;
        }

        Button MakeButton(string text, string id, string css, Action click)
        {
            var e = new Button(click) { text = text, name = id };
            e.AddToClassList(css);
            ApplyFont(e);
            return e;
        }

        void ApplyFont(TextElement text)
        {
            if (typeface != null) text.style.unityFontDefinition = FontDefinition.FromSDFFont(typeface);
        }

        static void Show(VisualElement e, bool visible) => e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        sealed class InteractionTick : VisualElement
        {
            public InteractionTick()
            {
                AddToClassList("interaction-tick");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    float w = contentRect.width, h = contentRect.height;
                    Vector2 At(float x, float y) => new Vector2(x * w, y * h);
                    p.fillColor = resolvedStyle.color;
                    p.BeginPath();
                    p.MoveTo(At(.16f, .55f));
                    p.LineTo(At(.26f, .43f));
                    p.LineTo(At(.45f, .61f));
                    p.BezierCurveTo(At(.55f, .43f), At(.68f, .16f), At(.73f, .08f));
                    p.LineTo(At(.86f, .15f));
                    p.LineTo(At(.49f, .86f));
                    p.ClosePath();
                    p.Fill();
                };
            }
        }

        sealed class BubbleTail : VisualElement
        {
            bool onTop;

            public BubbleTail()
            {
                AddToClassList("house-bubble-tail");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    float w = contentRect.width, h = contentRect.height;
                    if (w <= 0 || h <= 0) return;
                    var a = new Vector2(1f, onTop ? h : 0f);
                    var b = new Vector2(w * .5f, onTop ? 1f : h - 1f);
                    var c = new Vector2(w - 1f, a.y);
                    p.fillColor = parent.resolvedStyle.backgroundColor;
                    p.strokeColor = resolvedStyle.color;
                    p.lineWidth = 1.5f;
                    p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.ClosePath(); p.Fill();
                    p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.Stroke();
                };
            }

            public void Point(bool top, float center)
            {
                // The button's border takes two units on each edge.
                style.left = center - 10f;
                style.top = top ? -10f : BubbleHeight - 5f;
                if (top == onTop) return;
                onTop = top;
                MarkDirtyRepaint();
            }
        }

        sealed class PawMark : VisualElement
        {
            public PawMark()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("paw-mark");
                generateVisualContent += Paint;
            }

            void Paint(MeshGenerationContext context)
            {
                var p = context.painter2D;
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                float size = Mathf.Min(rect.width, rect.height);
                Vector2 Point(float x, float y) => rect.center + (new Vector2(x, y) - Vector2.one * .5f) * size;
                p.fillColor = resolvedStyle.color;
                void Toe(float x, float y, float rx, float ry, float tilt)
                {
                    Vector2 At(float a, float b)
                    {
                        float c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
                        return Point(x + c * a * rx - s * b * ry, y + s * a * rx + c * b * ry);
                    }
                    const float k = .552285f;
                    p.BeginPath(); p.MoveTo(At(1, 0));
                    p.BezierCurveTo(At(1,k),At(k,1),At(0,1));
                    p.BezierCurveTo(At(-k,1),At(-1,k),At(-1,0));
                    p.BezierCurveTo(At(-1,-k),At(-k,-1),At(0,-1));
                    p.BezierCurveTo(At(k,-1),At(1,-k),At(1,0));
                    p.ClosePath(); p.Fill();
                }
                Toe(.16f,.42f,.105f,.156f,-.45f);
                Toe(.37f,.225f,.113f,.172f,-.18f);
                Toe(.64f,.225f,.113f,.172f,.18f);
                Toe(.85f,.42f,.104f,.155f,.45f);
                p.BeginPath(); p.MoveTo(Point(.50f,.423f));
                p.BezierCurveTo(Point(.60f,.423f),Point(.635f,.526f),Point(.714f,.623f));
                p.BezierCurveTo(Point(.775f,.705f),Point(.818f,.768f),Point(.786f,.851f));
                p.BezierCurveTo(Point(.737f,.989f),Point(.579f,.878f),Point(.50f,.883f));
                p.BezierCurveTo(Point(.418f,.878f),Point(.267f,.989f),Point(.219f,.851f));
                p.BezierCurveTo(Point(.184f,.769f),Point(.229f,.695f),Point(.293f,.617f));
                p.BezierCurveTo(Point(.368f,.519f),Point(.406f,.423f),Point(.50f,.423f));
                p.ClosePath(); p.Fill();
            }
        }

        sealed class CloseMark : VisualElement
        {
            public CloseMark()
            {
                AddToClassList("close-mark");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += ctx =>
                {
                    var p = ctx.painter2D;
                    var c = contentRect.center;
                    p.strokeColor = resolvedStyle.color; p.lineWidth = 2f; p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(c + new Vector2(-5,-5)); p.LineTo(c + new Vector2(5,5));
                    p.MoveTo(c + new Vector2(5,-5)); p.LineTo(c + new Vector2(-5,5)); p.Stroke();
                };
            }
        }
    }
}
