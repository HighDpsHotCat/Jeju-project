using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jeju.Yard
{
    public sealed partial class YardCollectionPanel
    {
        public enum HouseAction { Wardrobe, Exit }
        public bool HouseMenuOpen { get; private set; }
        public HouseAction SelectedHouseAction { get; private set; }
        public bool WardrobeOpen { get; private set; }
        public event Action<HouseAction> HouseActionRequested;
        public event Action<int> AnimalAccessoryRequested;
        public event Action<bool, int> OutfitRequested;
        public event Action WardrobeClosed;

        readonly Button[] accessoryOptions = new Button[2], homeOptions = new Button[2], outfitOptions = new Button[3];
        readonly Label[] accessoryStates = new Label[2], wornTags = new Label[3];
        readonly WearableGlyph[] outfitSwatches = new WearableGlyph[3];
        VisualElement houseMenu, housePointer, wardrobe, wardrobeSheet;
        Button wardrobeClose, topsTab, hatsTab;
        Image wardrobePreview;
        bool hatsSelected;
        int currentTop, currentHat;
        bool CottageAvailable => nearbyAvailable && targetIsHouse;

        void BuildAccessoryOptions()
        {
            var title = MakeLabel("동물 꾸미기", "outfit-heading");
            decoration.Add(title);
            for (int i = 0; i < accessoryOptions.Length; i++)
            {
                int slot = i;
                string label = i == 0 ? "별 머리띠" : "빨간 망토";
                var button = MakeButton(label, i == 0 ? "animal-accessory-headband" : "animal-accessory-cape",
                    "wearable-option", () =>
                    {
                        if (AnimalMenuOpen && decorationOpen && canDecorate && !IsOpen)
                            AnimalAccessoryRequested?.Invoke(slot);
                    });
                button.Add(new WearableGlyph(WearableGlyph.Slot.Accessory, i));
                var state = MakeLabel("", "wearable-state");
                state.pickingMode = PickingMode.Ignore;
                button.Add(state);
                accessoryOptions[i] = button;
                accessoryStates[i] = state;
                decoration.Add(button);
            }
        }

        public void UpdateAnimalAccessories(bool headband, bool cape)
        {
            for (int i = 0; i < accessoryOptions.Length; i++)
            {
                var button = accessoryOptions[i];
                if (button == null) return;
                bool worn = i == 0 ? headband : cape;
                button.EnableInClassList("wearable-equipped", worn);
                button.tooltip = button.text + (worn ? " 벗기" : " 입히기");
                accessoryStates[i].text = worn ? "착용 중" : "미착용";
            }
        }

        void BuildHouseWardrobe()
        {
            houseMenu = Element("house-action-menu");
            houseMenu.name = "house-action-menu";
            BuildHouseOption(HouseAction.Wardrobe, "옷 꾸미기", "house-action-wardrobe");
            BuildHouseOption(HouseAction.Exit, "마당 나가기", "house-action-exit");
            housePointer = new ActionGlyph(ActionGlyph.Kind.Arrow);
            housePointer.AddToClassList("animal-menu-pointer");
            houseMenu.Add(housePointer);
            root.Add(houseMenu);
            Show(houseMenu, false);

            wardrobe = Element("collection-overlay");
            wardrobe.name = "wardrobe";
            wardrobe.RegisterCallback<ClickEvent>(evt => { if (evt.target == wardrobe) CloseWardrobe(); });
            wardrobeSheet = Element("wardrobe-sheet");
            wardrobe.Add(wardrobeSheet);
            var heading = Element("wardrobe-heading");
            heading.Add(MakeLabel("옷 꾸미기", "wardrobe-title"));
            wardrobeClose = MakeButton("", "wardrobe-close", "close-button", CloseWardrobe);
            wardrobeClose.tooltip = "옷장 닫기";
            wardrobeClose.Add(new CloseMark());
            heading.Add(wardrobeClose);
            wardrobeSheet.Add(heading);
            var body = Element("wardrobe-body");
            wardrobeSheet.Add(body);
            var stage = Element("wardrobe-stage");
            wardrobePreview = new Image { name = "wardrobe-preview", scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            wardrobePreview.AddToClassList("wardrobe-portrait");
            stage.Add(wardrobePreview);
            body.Add(stage);
            var controls = Element("wardrobe-controls");
            var tabs = Element("wardrobe-tabs");
            topsTab = MakeButton("상의", "wardrobe-tops", "wardrobe-tab", () => SelectWardrobeTab(false));
            hatsTab = MakeButton("모자", "wardrobe-hats", "wardrobe-tab", () => SelectWardrobeTab(true));
            tabs.Add(topsTab); tabs.Add(hatsTab);
            controls.Add(tabs);
            for (int i = 0; i < outfitOptions.Length; i++)
            {
                int index = i;
                var button = MakeButton("", "wardrobe-option-" + i, "wearable-option", () =>
                {
                    if (WardrobeOpen && (hatsSelected ? currentHat : currentTop) != index)
                        OutfitRequested?.Invoke(hatsSelected, index);
                });
                outfitSwatches[i] = new WearableGlyph(WearableGlyph.Slot.Top, i);
                button.Add(outfitSwatches[i]);
                var worn = MakeLabel("착용 중", "wearable-state");
                worn.pickingMode = PickingMode.Ignore;
                button.Add(worn);
                outfitOptions[i] = button;
                wornTags[i] = worn;
                controls.Add(button);
            }
            body.Add(controls);
            root.Add(wardrobe);
            Show(wardrobe, false);
        }

        void BuildHouseOption(HouseAction action, string titleText, string id)
        {
            var button = MakeButton(titleText, id, "animal-option", () =>
            {
                if (!HouseMenuOpen || !CottageAvailable || IsOpen) return;
                SelectHouseAction(action);
                HouseActionRequested?.Invoke(action);
            });
            var marker = new ActionGlyph(ActionGlyph.Kind.Arrow);
            marker.AddToClassList("selection-marker");
            button.Add(marker);
            homeOptions[(int)action] = button;
            houseMenu.Add(button);
        }

        public void ShowHouseMenu()
        {
            if (houseMenu == null || IsOpen || !CottageAvailable) return;
            if (!HouseMenuOpen)
            {
                if (AnimalMenuOpen) CloseAnimalMenu();
                HouseMenuOpen = true;
                SelectHouseAction(HouseAction.Wardrobe);
                Show(houseMenu, true);
                Show(previousAction, true);
                Show(nextAction, true);
            }
            UpdateNearbyHud();
        }

        public void CloseHouseMenu()
        {
            if (!HouseMenuOpen) return;
            HouseMenuOpen = false;
            Show(houseMenu, false);
            if (!AnimalMenuOpen) { Show(previousAction, false); Show(nextAction, false); }
            UpdateNearbyHud();
        }

        void SelectHouseAction(HouseAction value)
        {
            SelectedHouseAction = value;
            for (int i = 0; i < homeOptions.Length; i++)
                homeOptions[i].EnableInClassList("animal-option-selected", i == (int)value);
            interactButton.tooltip = homeOptions[(int)value].text;
        }

        void RefreshHouseMenu()
        {
            if (WardrobeOpen && !CottageAvailable) { CloseWardrobe(); return; }
            if (!HouseMenuOpen) return;
            if (!CottageAvailable || IsOpen) { CloseHouseMenu(); return; }
            interactButton.tooltip = homeOptions[(int)SelectedHouseAction].text;
            Rect safe = SafeRect;
            float px = settings.scale;
            float top = (Screen.height - safe.yMax) / px + 8f;
            float bottom = (Screen.height - safe.yMin) / px - 8f;
            Vector2 point = hasHouseAnchor
                ? RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(houseAnchor.x, Screen.height - houseAnchor.y))
                : new Vector2(safe.center.x / px, (Screen.height - safe.center.y) / px);
            const float width = 132f, height = 92f;
            float leftEdge = safe.xMin / px + 6f;
            float rightEdge = safe.xMax / px - 90f;
            float left = Mathf.Clamp(point.x - width * .5f, leftEdge, Mathf.Max(leftEdge, rightEdge - width));
            bool above = point.y - height - 8f >= top;
            houseMenu.style.left = left;
            houseMenu.style.top = Mathf.Clamp(above ? point.y - height - 8f : point.y + 8f, top, Mathf.Max(top, bottom - height));
            Show(housePointer, hasHouseAnchor);
            housePointer.style.left = Mathf.Clamp(point.x - left - 8f, 15f, width - 31f);
            housePointer.style.top = above ? height - 4f : -7f;
            housePointer.EnableInClassList("point-down", above);
            PositionActionArrows(safe, px, top, bottom);
        }

        public void ShowWardrobe(int top, int hat)
        {
            if (wardrobe == null || (IsOpen && !WardrobeOpen) || !CottageAvailable) return;
            if (WardrobeOpen) { UpdateOutfit(top, hat); return; }
            CloseHouseMenu();
            if (AnimalMenuOpen) CloseAnimalMenu();
            WardrobeOpen = IsOpen = true;
            hatsSelected = false;
            UpdateOutfit(top, hat);
            Show(launcher, false);
            UpdateNearbyHud();
            Show(wardrobe, true);
            wardrobeClose.Focus();
        }

        public void CloseWardrobe()
        {
            if (!WardrobeOpen) return;
            WardrobeOpen = IsOpen = false;
            closedFrame = Time.frameCount;
            Show(wardrobe, false);
            wardrobePreview.image = null;
            WardrobeClosed?.Invoke();
            Show(launcher, true);
            if (CottageAvailable) { ShowHouseMenu(); interactButton.Focus(); }
            else UpdateNearbyHud();
        }

        public void SetWardrobePreview(Texture preview)
        {
            if (wardrobePreview != null) wardrobePreview.image = preview;
        }

        public void UpdateOutfit(int top, int hat)
        {
            currentTop = top; currentHat = hat;
            if (WardrobeOpen) SelectWardrobeTab(hatsSelected);
        }

        void SelectWardrobeTab(bool hats)
        {
            hatsSelected = hats;
            topsTab.EnableInClassList("wardrobe-tab-current", !hats);
            hatsTab.EnableInClassList("wardrobe-tab-current", hats);
            for (int i = 0; i < outfitOptions.Length; i++)
            {
                var button = outfitOptions[i];
                bool worn = i == (hats ? currentHat : currentTop);
                button.text = hats ? ExplorerAppearance.HatName(i) : ExplorerAppearance.TopName(i);
                button.tooltip = button.text + (worn ? " 착용 중" : " 착용하기");
                button.EnableInClassList("wearable-equipped", worn);
                outfitSwatches[i].SetSlot(hats ? WearableGlyph.Slot.Hat : WearableGlyph.Slot.Top);
                Show(wornTags[i], worn);
            }
        }

        void SizeWardrobe(float width, float height)
        {
            bool wide = width >= 580 && width > height;
            wardrobeSheet.EnableInClassList("wardrobe-wide", wide);
            wardrobeSheet.style.width = Mathf.Min(wide ? 645f : 394f, width - 24f);
            wardrobeSheet.style.height = Mathf.Min(wide ? 348f : 530f, height - 24f);
        }

        sealed class WearableGlyph : VisualElement
        {
            public enum Slot { Top, Hat, Accessory }
            Slot slot;
            readonly int item;

            public WearableGlyph(Slot slot, int item)
            {
                this.slot = slot;
                this.item = item;
                AddToClassList("wearable-swatch");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Paint;
            }

            public void SetSlot(Slot value)
            {
                if (value == slot) return;
                slot = value;
                MarkDirtyRepaint();
            }

            static Color Shade(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

            void Paint(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float size = Mathf.Min(contentRect.width, contentRect.height);
                if (size <= 0) return;
                Vector2 At(float x, float y) => contentRect.center + (new Vector2(x,y) - Vector2.one * .5f) * size;
                void Shape(int color)
                {
                    p.fillColor = Shade(color);
                    p.ClosePath();
                    p.Fill();
                    p.strokeColor = Shade(0x655747);
                    p.lineWidth = 1.2f;
                    p.Stroke();
                }
                void Line(float x0, float y0, float x1, float y1, int color, float width)
                {
                    p.strokeColor = Shade(color); p.lineWidth = width; p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(At(x0,y0)); p.LineTo(At(x1,y1)); p.Stroke();
                }
                void Star(float x, float y, float radius)
                {
                    p.BeginPath();
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                        float r = i % 2 == 0 ? radius : radius * .48f;
                        var end = At(x + Mathf.Cos(angle) * r, y + Mathf.Sin(angle) * r);
                        if (i == 0) p.MoveTo(end); else p.LineTo(end);
                    }
                    Shape(0xF6CB63);
                }

                if (slot == Slot.Accessory && item == 0)
                {
                    p.strokeColor = Shade(0x846C45); p.lineWidth = 3f; p.lineCap = LineCap.Round;
                    p.BeginPath(); p.MoveTo(At(.14f,.83f)); p.BezierCurveTo(At(.14f,.29f),At(.83f,.29f),At(.86f,.83f)); p.Stroke();
                    Star(.25f,.33f,.18f); Star(.72f,.28f,.19f);
                }
                else if (slot == Slot.Accessory)
                {
                    p.BeginPath(); p.MoveTo(At(.38f,.21f)); p.LineTo(At(.62f,.21f));
                    p.QuadraticCurveTo(At(.75f,.55f),At(.9f,.77f));
                    p.QuadraticCurveTo(At(.55f,.95f),At(.13f,.79f)); p.QuadraticCurveTo(At(.31f,.51f),At(.38f,.21f)); Shape(0xBD554E);
                    Line(.38f,.28f,.62f,.28f,0xF5C66F,2.2f); Line(.5f,.36f,.43f,.73f,0xD97B6C,1.6f);
                }
                else if (slot == Slot.Top)
                {
                    p.BeginPath(); p.MoveTo(At(.34f,.18f)); p.QuadraticCurveTo(At(.5f,.35f),At(.66f,.18f));
                    p.LineTo(At(.79f,.25f)); p.LineTo(At(.94f,.42f)); p.LineTo(At(.78f,.55f)); p.LineTo(At(.71f,.46f));
                    p.LineTo(At(.73f,.85f)); p.LineTo(At(.27f,.85f)); p.LineTo(At(.29f,.46f));
                    p.LineTo(At(.22f,.55f)); p.LineTo(At(.06f,.42f)); p.LineTo(At(.21f,.25f));
                    Shape(item == 0 ? 0x89956C : item == 1 ? 0xF4EDD4 : 0xD98A77);
                    if (item == 0)
                    {
                        Line(.5f,.35f,.5f,.78f,0xEAD4A9,1.7f); Line(.39f,.24f,.56f,.39f,0xECAD65,2.7f);
                    }
                    else if (item == 1)
                    {
                        Line(.31f,.5f,.69f,.5f,0x49697B,2f); Line(.31f,.65f,.69f,.65f,0x49697B,2f);
                    }
                    else
                    {
                        Line(.32f,.42f,.5f,.54f,0xF8DDB2,2f); Line(.5f,.54f,.68f,.42f,0xF8DDB2,2f);
                    }
                }
                else
                {
                    int crown = item == 0 ? 0xDF9C5F : item == 1 ? 0x465965 : 0xE3C387;
                    int ribbon = item == 0 ? 0x996C48 : item == 1 ? 0x6FA79F : 0xCE8174;
                    p.BeginPath(); p.MoveTo(At(.16f,.62f)); p.LineTo(At(.27f,.33f));
                    p.QuadraticCurveTo(At(.31f,.22f),At(.51f,.24f)); p.QuadraticCurveTo(At(.7f,.22f),At(.76f,.35f));
                    p.LineTo(At(.85f,.62f)); p.LineTo(At(.95f,.68f)); p.QuadraticCurveTo(At(.52f,.88f),At(.05f,.68f));
                    p.LineTo(At(.16f,.62f)); Shape(crown);
                    Line(.24f,.57f,.77f,.57f,ribbon,2.7f);
                }
            }
        }
    }
}
