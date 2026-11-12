using System.Collections.Generic;
using UnityEngine;

namespace Jeju.Yard
{
    // Read positions after animal motion and camera follow, so the selected animal and
    // the speech bubble stay attached to what the player actually sees this frame.
    [DefaultExecutionOrder(100)]
    public sealed class YardProximity : MonoBehaviour
    {
        const float LandReach = 2.1f;
        const float ShoreReach = 4.85f;
        const float CottageReach = 1.9f;
        const float AttentionLeaveMargin = .5f;
        const float FocusMargin = .12f;

        sealed class Resident
        {
            public string modelKey;
            public YardResidentBehaviour actor;
            public float reach;
            public bool near;
        }

        readonly List<Resident> residents = new List<Resident>();
        YardCollectionController collection;
        YardCollectionPanel panel;
        YardModelPortraits portraits;
        ExplorerAppearance outfit;
        YardWalker explorer;
        Transform player;
        BoxCollider cottage;
        Camera lens;
        Resident focused, menuResident;
        string menuCollectionKey, menuSpeciesId;
        bool nearCottage, focusCottage;
        static readonly Vector2 NoBubble = new Vector2(float.NaN, float.NaN);

        public void Initialize(YardCollectionController owner, YardCollectionPanel hud,
            IReadOnlyDictionary<string, GameObject> models)
        {
            collection = owner;
            panel = hud;
            portraits = owner.GetComponent<YardModelPortraits>();
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<YardWalker>(out var walker))
                {
                    explorer = walker;
                    player = walker.transform;
                    outfit = walker.GetComponent<ExplorerAppearance>();
                }
                if (root.CompareTag("MainCamera")) lens = root.GetComponent<Camera>();
                if (root.name == "Jeju choga" || root.name == "JejuChoga")
                    cottage = root.GetComponent<BoxCollider>();
            }
            foreach (var model in models)
            {
                var actor = model.Value.GetComponentInChildren<YardResidentBehaviour>(true);
                if (actor != null)
                    residents.Add(new Resident { modelKey = model.Key, actor = actor,
                        reach = model.Key == "dolphin" ? ShoreReach : LandReach });
            }
            panel.InteractRequested += Interact;
            panel.HouseExitRequested += ExitAtCottage;
            panel.AnimalActionRequested += PerformAction;
            panel.HouseActionRequested += PerformHouseAction;
            panel.AnimalAccessoryRequested += ToggleAccessory;
            panel.OutfitRequested += ChangeOutfit;
            panel.WardrobeClosed += portraits.ReleaseExplorerPreview;
        }

        public void SetWardrobeScope(string scope)
        {
            if (outfit == null) return;
            outfit.SetScope(scope);
            if (panel.WardrobeOpen) RefreshOutfit();
        }

        static float GroundDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void LateUpdate()
        {
            if (panel != null) Resolve();
        }

        void Resolve()
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                ResetNearby();
                return;
            }
            if (menuResident != null && (!panel.AnimalMenuOpen || panel.IsOpen ||
                menuCollectionKey != collection.ActiveCollectionKey ||
                menuSpeciesId != collection.PlacedSpeciesId(menuResident.modelKey)))
                CloseMenu();
            Vector3 position = player.position;
            float best = float.PositiveInfinity;
            Vector2 bubble = NoBubble;
            if (cottage != null && cottage.enabled && cottage.gameObject.activeInHierarchy)
            {
                // ClosestPoint uses the rotated footprint, including the sides of the cottage.
                Vector3 houseCentre = cottage.transform.TransformPoint(cottage.center);
                var atWaist = new Vector3(position.x, houseCentre.y, position.z);
                Vector3 wall = cottage.ClosestPoint(atWaist);
                float distance = GroundDistance(position, wall);
                nearCottage = distance <= CottageReach;
                if (nearCottage)
                {
                    best = distance / CottageReach - (focusCottage ? FocusMargin : 0f);
                    if (lens != null)
                    {
                        Vector3 eave = Vector3.Lerp(houseCentre, wall, .62f);
                        eave.y = cottage.bounds.max.y + 1.15f;
                        Vector3 screen = lens.WorldToScreenPoint(eave);
                        if (screen.z > 0) bubble = screen;
                    }
                }
            }
            else nearCottage = false;

            Resident next = null;
            bool menuInReach = false;
            foreach (var resident in residents)
            {
                Transform point = resident.actor.InteractionPoint;
                bool visible = resident.actor.isActiveAndEnabled && point != null && point.gameObject.activeInHierarchy;
                float distance = visible ? GroundDistance(position, point.position) : float.PositiveInfinity;
                // A resident can keep looking briefly beyond reach. The button follows actual reach.
                bool near = distance <= resident.reach + (resident.near ? AttentionLeaveMargin : 0f);
                if (near != resident.near)
                {
                    resident.actor.SetObserver(near ? player : null);
                    resident.near = near;
                }
                if (distance > resident.reach) continue;
                if (resident == menuResident) menuInReach = true;
                float score = distance / resident.reach - (focused == resident ? FocusMargin : 0f);
                if (score >= best) continue;
                best = score;
                next = resident;
            }
            // Keep the selected animal when two residents approach each other.
            if (menuResident != null)
            {
                if (menuInReach && TryAnchor(menuResident, out Vector2 anchor))
                {
                    next = menuResident;
                    panel.MoveAnimalMenu(anchor);
                }
                else CloseMenu();
            }
            if (nearCottage && (panel.HouseMenuOpen || panel.WardrobeOpen)) next = null;
            focused = next;
            focusCottage = focused == null && nearCottage;
            panel.SetNearby(focused != null || focusCottage, focusCottage, bubble);
        }

        bool TryAnchor(Resident resident, out Vector2 anchor)
        {
            anchor = default;
            if (lens == null) return false;
            Vector3 screen = lens.WorldToScreenPoint(resident.actor.InteractionPoint.position);
            anchor = screen;
            return screen.z > 0;
        }

        void CloseMenu()
        {
            menuResident = null;
            menuCollectionKey = menuSpeciesId = null;
            panel.CloseAnimalMenu();
        }

        void Interact()
        {
            if (panel.IsOpen || player == null) return;
            if (panel.AnimalMenuOpen)
            {
                PerformAction(panel.SelectedAnimalAction);
                return;
            }
            if (panel.HouseMenuOpen)
            {
                PerformHouseAction(panel.SelectedHouseAction);
                return;
            }
            Resolve();
            if (focused != null && TryAnchor(focused, out Vector2 anchor))
            {
                string species = collection.PlacedSpeciesId(focused.modelKey);
                if (species == null) return;
                menuResident = focused;
                menuCollectionKey = collection.ActiveCollectionKey;
                menuSpeciesId = species;
                panel.ShowAnimalMenu(anchor, YardAnimalAccessories.Supports(focused.modelKey));
            }
            else if (focusCottage) panel.ShowHouseMenu();
        }

        void PerformAction(YardCollectionPanel.AnimalAction action)
        {
            if (panel.IsOpen || !panel.AnimalMenuOpen || menuResident == null) return;
            Resolve();
            if (menuResident == null) return;
            var resident = menuResident;
            switch (action)
            {
                case YardCollectionPanel.AnimalAction.Info:
                    string speciesId = menuSpeciesId, collectionKey = menuCollectionKey;
                    CloseMenu();
                    collection.OpenResident(resident.modelKey, speciesId, collectionKey);
                    break;
                case YardCollectionPanel.AnimalAction.Pet:
                    if (collection.PetResident(resident.modelKey, out bool gained, out bool full))
                    {
                        resident.actor.PlayPetReaction();
                        panel.ShowPetFeedback(gained, full);
                    }
                    break;
                case YardCollectionPanel.AnimalAction.Decorate:
                    if (collection.TryGetAccessories(resident.modelKey, menuSpeciesId, menuCollectionKey, out var equipped))
                        panel.ShowDecorationPreview((equipped & YardAccessories.StarHeadband) != 0,
                            (equipped & YardAccessories.RedCape) != 0);
                    break;
            }
        }

        void ToggleAccessory(int slot)
        {
            if (panel.IsOpen || !panel.AnimalMenuOpen || menuResident == null || (slot != 0 && slot != 1)) return;
            Resolve();
            if (menuResident == null) return;
            string model = menuResident.modelKey, species = menuSpeciesId, scope = menuCollectionKey;
            if (!collection.TryGetAccessories(model, species, scope, out var equipped)) return;
            var next = equipped ^ (slot == 0 ? YardAccessories.StarHeadband : YardAccessories.RedCape);
            if (collection.TrySetAccessories(model, species, scope, next))
                panel.UpdateAnimalAccessories((next & YardAccessories.StarHeadband) != 0, (next & YardAccessories.RedCape) != 0);
        }

        void PerformHouseAction(YardCollectionPanel.HouseAction action)
        {
            if (panel.IsOpen || player == null) return;
            Resolve();
            if (!nearCottage) return;
            if (action == YardCollectionPanel.HouseAction.Exit) YardExit.Leave();
            else if (action == YardCollectionPanel.HouseAction.Wardrobe && outfit != null)
            {
                if (menuResident != null) CloseMenu();
                panel.ShowWardrobe(outfit.SelectedTop, outfit.SelectedHat);
                RefreshOutfit();
            }
        }

        void ChangeOutfit(bool hat, int index)
        {
            if (!panel.WardrobeOpen || outfit == null) return;
            Resolve();
            if (!nearCottage || !panel.WardrobeOpen) return;
            if (hat)
            {
                if (index < 0 || index >= ExplorerAppearance.HatCount || index == outfit.SelectedHat) return;
                outfit.SelectHat(index);
            }
            else
            {
                if (index < 0 || index >= ExplorerAppearance.TopCount || index == outfit.SelectedTop) return;
                outfit.SelectTop(index);
            }
            RefreshOutfit();
        }

        void RefreshOutfit()
        {
            panel.UpdateOutfit(outfit.SelectedTop, outfit.SelectedHat);
            panel.SetWardrobePreview(portraits.GetExplorerPreview(explorer.visual));
        }

        void ExitAtCottage()
        {
            if (panel.IsOpen || player == null) return;
            Resolve();
            if (nearCottage) YardExit.Leave();
        }

        void OnDisable() => ResetNearby();

        void ResetNearby()
        {
            foreach (var resident in residents)
                if (resident.near && resident.actor != null)
                {
                    resident.actor.SetObserver(null);
                    resident.near = false;
                }
            nearCottage = focusCottage = false;
            focused = null;
            if (panel != null)
            {
                if (menuResident != null || panel.AnimalMenuOpen) CloseMenu();
                panel.SetNearby(false, false, NoBubble);
            }
        }

        void OnDestroy()
        {
            if (panel == null) return;
            panel.InteractRequested -= Interact;
            panel.HouseExitRequested -= ExitAtCottage;
            panel.AnimalActionRequested -= PerformAction;
            panel.HouseActionRequested -= PerformHouseAction;
            panel.AnimalAccessoryRequested -= ToggleAccessory;
            panel.OutfitRequested -= ChangeOutfit;
            panel.WardrobeClosed -= portraits.ReleaseExplorerPreview;
        }
    }
}
