using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jeju.Yard
{
    public sealed partial class YardCollectionPanel
    {
        public enum AnimalAction { Info, Pet, Decorate }
        public bool AnimalMenuOpen { get; private set; }
        public AnimalAction SelectedAnimalAction { get; private set; }
        public event Action<AnimalAction> AnimalActionRequested;

        const float MenuWidth = 122f, MenuHeight = 140f, DecorationWidth = 174f;
        readonly Button[] actionOptions = new Button[3];
        VisualElement actionMenu, optionList, menuPointer, decoration, petFeedback;
        Button previousAction, nextAction, decorationClose;
        Label gainLabel;
        Vector2 animalAnchor;
        bool decorationOpen, petting;
        float petStarted;

        void BuildAnimalMenu()
        {
            actionMenu = Element("animal-action-menu");
            actionMenu.name = "animal-action-menu";
            actionMenu.pickingMode = PickingMode.Ignore;
            optionList = Element("animal-action-list");
            optionList.pickingMode = PickingMode.Ignore;
            actionMenu.Add(optionList);
            AddAnimalOption(AnimalAction.Info, "정보", "animal-action-info");
            AddAnimalOption(AnimalAction.Pet, "쓰다듬기", "animal-action-pet");
            AddAnimalOption(AnimalAction.Decorate, "꾸미기", "animal-action-decorate");
            menuPointer = new ActionGlyph(ActionGlyph.Kind.Arrow);
            menuPointer.AddToClassList("animal-menu-pointer");
            actionMenu.Add(menuPointer);
            root.Add(actionMenu);

            decoration = Element("decoration-preview");
            decoration.name = "decoration-preview";
            var slot = Element("accessory-slot");
            slot.Add(new ActionGlyph(ActionGlyph.Kind.Hanger));
            decoration.Add(slot);
            decoration.Add(MakeLabel("아직 장식품이 없어요", "accessory-empty"));
            decorationClose = MakeButton("돌아가기", "decoration-close", "accessory-back", () => SetDecoration(false));
            decoration.Add(decorationClose);
            actionMenu.Add(decoration);

            previousAction = MakeNavButton(true);
            nextAction = MakeNavButton(false);
            petFeedback = Element("pet-feedback");
            petFeedback.name = "pet-feedback";
            petFeedback.pickingMode = PickingMode.Ignore;
            foreach (var size in new[] { "heart-left", "heart-center", "heart-right" })
            {
                var heart = new ActionGlyph(ActionGlyph.Kind.Heart);
                heart.AddToClassList(size);
                petFeedback.Add(heart);
            }
            gainLabel = MakeLabel("+5", "pet-gain");
            gainLabel.name = "pet-gain";
            gainLabel.pickingMode = PickingMode.Ignore;
            petFeedback.Add(gainLabel);
            root.Add(petFeedback);
            CloseAnimalMenu();
        }

        void AddAnimalOption(AnimalAction action, string label, string id)
        {
            var button = MakeButton(label, id, "animal-option", () =>
            {
                if (!AnimalMenuOpen || IsOpen) return;
                SelectAction(action);
                AnimalActionRequested?.Invoke(action);
            });
            var mark = new ActionGlyph(ActionGlyph.Kind.Arrow);
            mark.AddToClassList("selection-marker");
            button.Add(mark);
            actionOptions[(int)action] = button;
            optionList.Add(button);
        }

        Button MakeNavButton(bool previous)
        {
            var button = MakeButton("", previous ? "animal-action-up" : "animal-action-down", "animal-action-nav", () =>
            {
                if (!AnimalMenuOpen || IsOpen) return;
                if (decorationOpen) SetDecoration(false);
                int count = actionOptions.Length;
                SelectAction((AnimalAction)(((int)SelectedAnimalAction + (previous ? count - 1 : 1)) % count));
                interactButton.Focus();
            });
            button.tooltip = previous ? "이전 항목" : "다음 항목";
            var arrow = new ActionGlyph(ActionGlyph.Kind.Arrow);
            arrow.AddToClassList("animal-nav-arrow");
            arrow.EnableInClassList("point-down", !previous);
            button.Add(arrow);
            root.Add(button);
            return button;
        }

        public void ShowAnimalMenu(Vector2 animalScreenPoint)
        {
            if (actionMenu == null || !nearbyAvailable || targetIsHouse || IsOpen) return;
            bool opening = !AnimalMenuOpen;
            AnimalMenuOpen = true;
            if (opening)
            {
                Show(actionMenu, true);
                Show(previousAction, true);
                Show(nextAction, true);
                SetDecoration(false);
                SelectAction(AnimalAction.Info);
            }
            MoveAnimalMenu(animalScreenPoint);
        }

        public void MoveAnimalMenu(Vector2 animalScreenPoint)
        {
            if (!AnimalMenuOpen) return;
            if (!float.IsFinite(animalScreenPoint.x) || !float.IsFinite(animalScreenPoint.y))
            {
                CloseAnimalMenu();
                return;
            }
            animalAnchor = animalScreenPoint;
            PositionAnimalMenu();
        }

        public void CloseAnimalMenu()
        {
            AnimalMenuOpen = decorationOpen = petting = false;
            if (actionMenu == null) return;
            Show(actionMenu, false);
            Show(previousAction, false);
            Show(nextAction, false);
            Show(petFeedback, false);
            if (interactButton != null) interactButton.tooltip = targetIsHouse ? "마당 나가기" : "동물 만나기";
        }

        void RefreshAnimalMenu()
        {
            if (!AnimalMenuOpen) return;
            if (!nearbyAvailable || targetIsHouse || IsOpen) { CloseAnimalMenu(); return; }
            interactButton.tooltip = decorationOpen ? "돌아가기" : actionOptions[(int)SelectedAnimalAction].text;
            PositionAnimalMenu();
        }

        void SelectAction(AnimalAction action)
        {
            SelectedAnimalAction = action;
            for (int i = 0; i < actionOptions.Length; i++)
                actionOptions[i].EnableInClassList("animal-option-selected", i == (int)action);
            interactButton.tooltip = actionOptions[(int)action].text;
        }

        public void ShowDecorationPreview()
        {
            if (AnimalMenuOpen && !IsOpen) SetDecoration(!decorationOpen);
        }

        void SetDecoration(bool open)
        {
            bool wasOpen = decorationOpen;
            decorationOpen = open;
            Show(optionList, !open);
            Show(menuPointer, !open);
            Show(decoration, open);
            PositionAnimalMenu();
            interactButton.tooltip = open ? "돌아가기" : actionOptions[(int)SelectedAnimalAction].text;
            if (open) decorationClose.Focus();
            else if (wasOpen) interactButton.Focus();
        }

        public void ShowPetFeedback(bool gained, bool full)
        {
            if (!AnimalMenuOpen || IsOpen) return;
            petting = true;
            petStarted = Time.unscaledTime;
            petFeedback.EnableInClassList("pet-full", full);
            Show(gainLabel, gained);
            Show(petFeedback, true);
            UpdatePetFeedback();
        }

        void UpdateAnimalMenu()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (decorationOpen) SetDecoration(false);
                else CloseAnimalMenu();
            }
            if (petting) UpdatePetFeedback();
        }

        Vector2 AnimalPanelPoint => RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(animalAnchor.x, Screen.height - animalAnchor.y));

        void PositionAnimalMenu()
        {
            if (!AnimalMenuOpen || root?.panel == null) return;
            Rect safe = SafeRect;
            float scale = settings.scale;
            float leftEdge = safe.xMin / scale + 6f;
            float topEdge = (Screen.height - safe.yMax) / scale + 6f;
            float bottomEdge = (Screen.height - safe.yMin) / scale - 6f;
            float rightEdge = safe.xMax / scale - 90f; // Reserve the check and its two arrows.
            float width = decorationOpen ? DecorationWidth : MenuWidth;
            Vector2 point = AnimalPanelPoint;
            float left = Mathf.Clamp(point.x - width * .5f, leftEdge, Mathf.Max(leftEdge, rightEdge - width));
            bool below = point.y + 14f + MenuHeight <= bottomEdge;
            float top = below ? point.y + 14f : point.y - MenuHeight - 28f;
            actionMenu.style.left = left;
            actionMenu.style.top = Mathf.Clamp(top, topEdge, Mathf.Max(topEdge, bottomEdge - MenuHeight));
            actionMenu.style.width = width;
            menuPointer.style.left = Mathf.Clamp(point.x - left - 8f, 15f, width - 31f);
            menuPointer.style.top = below ? -7f : MenuHeight - 4f;
            menuPointer.EnableInClassList("point-down", !below);

            float checkY = (Screen.height - safe.yMax + safe.height * .70f) / scale;
            float right = (Screen.width - safe.xMax) / scale + 24f;
            previousAction.style.right = nextAction.style.right = right;
            previousAction.style.top = Mathf.Max(topEdge, checkY - 82f);
            nextAction.style.top = Mathf.Min(bottomEdge - 44f, checkY + 38f);
        }

        void UpdatePetFeedback()
        {
            float t = (Time.unscaledTime - petStarted) / 1.15f;
            if (t >= 1f)
            {
                petting = false;
                Show(petFeedback, false);
                return;
            }
            float px = settings.scale;
            Rect safe = SafeRect;
            Vector2 point = AnimalPanelPoint;
            petFeedback.style.left = Mathf.Clamp(point.x - 35f, safe.xMin / px + 5f, safe.xMax / px - 75f);
            petFeedback.style.top = Mathf.Max((Screen.height - safe.yMax) / px + 6f, point.y - 79f - 31f * t);
            petFeedback.style.opacity = Mathf.Clamp01((1f - t) * 3f);
        }

        bool ActionMenuConsumes(Vector2 p)
        {
            if (!AnimalMenuOpen) return false;
            return actionMenu.worldBound.Contains(p) || previousAction.worldBound.Contains(p) || nextAction.worldBound.Contains(p);
        }

        sealed class ActionGlyph : VisualElement
        {
            public enum Kind { Arrow, Heart, Hanger }
            readonly Kind kind;

            public ActionGlyph(Kind kind)
            {
                this.kind = kind;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Paint;
            }

            void Paint(MeshGenerationContext ctx)
            {
                float width = contentRect.width, height = contentRect.height;
                if (width <= 0 || height <= 0) return;
                Vector2 P(float x, float y) => new Vector2(x * width, y * height);
                var p = ctx.painter2D;
                p.fillColor = resolvedStyle.color;
                p.strokeColor = resolvedStyle.color;
                p.BeginPath();
                if (kind == Kind.Arrow)
                {
                    p.MoveTo(P(.08f,.84f)); p.LineTo(P(.50f,.12f)); p.LineTo(P(.92f,.84f));
                    p.ClosePath(); p.Fill();
                }
                else if (kind == Kind.Heart)
                {
                    p.MoveTo(P(.5f,.91f));
                    p.BezierCurveTo(P(.29f,.75f),P(.06f,.57f),P(.07f,.32f));
                    p.BezierCurveTo(P(.08f,.08f),P(.36f,.04f),P(.5f,.25f));
                    p.BezierCurveTo(P(.66f,.03f),P(.93f,.09f),P(.93f,.33f));
                    p.BezierCurveTo(P(.93f,.53f),P(.73f,.73f),P(.5f,.91f));
                    p.ClosePath(); p.Fill();
                    p.lineWidth = 1.4f; p.strokeColor = new Color32(255, 244, 224, 255); p.Stroke();
                }
                else
                {
                    p.lineWidth = 1.8f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                    p.MoveTo(P(.40f,.32f));
                    p.BezierCurveTo(P(.41f,.08f),P(.74f,.17f),P(.58f,.36f));
                    p.LineTo(P(.50f,.44f)); p.LineTo(P(.50f,.50f));
                    p.LineTo(P(.12f,.74f)); p.QuadraticCurveTo(P(.06f,.81f),P(.17f,.82f));
                    p.LineTo(P(.84f,.82f)); p.QuadraticCurveTo(P(.92f,.81f),P(.86f,.74f));
                    p.LineTo(P(.50f,.50f)); p.Stroke();
                }
            }
        }
    }
}
