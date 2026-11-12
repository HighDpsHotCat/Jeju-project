using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Jeju.Yard
{
    [DisallowMultipleComponent]
    public sealed class YardCollectionController : MonoBehaviour
    {
        [Serializable] sealed class Placement { public string[] ids; }

        enum Source { Unlinked, Linked, Imported, StandaloneDemo }
        const string DemoOptIn = "jeju.yard.standaloneDemo";

        readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        readonly Dictionary<string, YardAnimalAccessories.Mount> accessoryMounts = new Dictionary<string, YardAnimalAccessories.Mount>();
        readonly Dictionary<string, string> residents = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, YardGuideSnapshot.Animal> registered = new Dictionary<string, YardGuideSnapshot.Animal>(StringComparer.Ordinal);
        readonly HashSet<string> placed = new HashSet<string>(StringComparer.Ordinal);
        readonly List<YardCollectionPanel.Entry> cards = new List<YardCollectionPanel.Entry>();

        YardCollectionPanel panel;
        YardModelPortraits portraits;
        YardProximity proximity;
        YardGuideSnapshot collection;
        Source source;
        string activeJson, placementKey;

        public bool IsPlaced(string id) => id != null && placed.Contains(id);
        public string ActiveCollectionKey => placementKey;
        public int RegisteredCount => registered.Count;
        public bool IsOpen => panel != null && panel.IsOpen;

#if JEJU_YARD_VISITS
        public YardVisitSnapshot CaptureVisitSnapshot()
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in placed)
                if (registered.TryGetValue(id, out var animal) && HasModel(animal))
                    keys.Add(animal.modelKey);
            return new YardVisitSnapshot(keys);
        }

        public bool TryReadVisitSnapshot(string json, out YardVisitSnapshot snapshot) =>
            YardVisitSnapshot.TryRead(json, models.Keys, out snapshot);
#endif

        void Awake()
        {
            panel = GetComponent<YardCollectionPanel>();
            if (panel == null) panel = gameObject.AddComponent<YardCollectionPanel>();
            portraits = GetComponent<YardModelPortraits>();
            if (portraits == null) portraits = gameObject.AddComponent<YardModelPortraits>();
            BindSceneModels();
            proximity = gameObject.AddComponent<YardProximity>();
            proximity.Initialize(this, panel, models);
            panel.Opening += OpenCollection;
            Reload();
        }

        void OnDestroy()
        {
            if (panel != null) panel.Opening -= OpenCollection;
        }

        void OpenCollection()
        {
            Reload();
            foreach (var card in cards)
                if (card.supported && card.preview == null)
                {
                    string key = registered[card.id].modelKey;
                    card.preview = Preview(key, card.id);
                }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && panel != null) Reload();
        }

        void BindSceneModels()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "Yard animals" || root.name == "YardAnimals")
                {
                    AddModel("corgi",root.transform.Find("Welsh corgi"));
                    AddModel("tabby",root.transform.Find("Yellow tabby cat"));
                    AddModel("gull",root.transform.Find("Seagull"));
                    AddModel("sparrow",root.transform.Find("Tree sparrow"));
                    AddModel("swallowtail",root.transform.Find("Asian swallowtail"));
                }
                var dolphin = root.GetComponentInChildren<DolphinVisitor>(true);
                if (dolphin != null) AddModel("dolphin",dolphin.transform.parent);
            }
        }

        void AddModel(string key, Transform anchor)
        {
            if (anchor == null) return;
            models[key] = anchor.gameObject;
            if (YardAnimalAccessories.Supports(key))
            {
                var mount = new YardAnimalAccessories.Mount(key,anchor.gameObject,portraits.AccessoryArt,false);
                if (mount.Available) accessoryMounts[key] = mount;
            }
        }

        // Called when the app regains focus and immediately before showing the collection.
        public void Reload()
        {
            try
            {
                string path = YardGuideSnapshot.LinkedPath;
                if (File.Exists(path)) LoadFile(path, Source.Linked);
                else if (File.Exists(YardGuideSnapshot.ImportedPath)) LoadFile(YardGuideSnapshot.ImportedPath, Source.Imported);
                else if (PlayerPrefs.GetInt(DemoOptIn, 0) == 1) EnterDemo();
                else Apply(null, Source.Unlinked, null);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                if (collection == null) Apply(null, Source.Unlinked, null);
                panel.ShowNotice("도감 파일을 읽지 못했어요. 잠시 뒤 다시 열어 주세요.");
                Debug.LogWarning("Could not read the local yard guide: " + ex.Message);
            }
        }

        void LoadFile(string path, Source next)
        {
            string json = File.ReadAllText(path);
            if (source == next && activeJson == json) return;
            if (YardGuideSnapshot.TryParse(json, out var snapshot, out bool cleaned))
            {
                bool migrateImport = next == Source.Imported && cleaned;
                if (migrateImport) json = JsonUtility.ToJson(snapshot);
                Apply(snapshot, next, json);
                if (migrateImport) YardGuideSnapshot.SaveImport(json);
            }
            else
            {
                // A damaged guide must not turn into a demo or erase a saved placement.
                if (source != next) Apply(null,next,null);
                panel.ShowNotice("도감 형식을 확인할 수 없어요. 앱에서 다시 보내 주세요.");
            }
        }

        // The Flutter host may also supply its export via UnitySendMessage. Store it so that
        // opening the panel again or relaunching a separate player keeps the linked profile.
        public void LoadGuideSnapshot(string json)
        {
            if (File.Exists(YardGuideSnapshot.LinkedPath)) { Reload(); return; }
            if (!YardGuideSnapshot.TryParse(json, out var snapshot, out bool cleaned))
            {
                panel.ShowNotice("도감 앱에서 ‘마당으로 보내기’를 먼저 눌러 주세요.");
                return;
            }
            try
            {
                if (cleaned) json = JsonUtility.ToJson(snapshot);
                YardGuideSnapshot.SaveImport(json);
                Apply(snapshot,Source.Imported,json);
                panel.ShowNotice("도감을 불러왔어요.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                panel.ShowNotice("도감을 기기에 저장하지 못했어요.");
                Debug.LogWarning("Could not save the imported yard guide: " + ex.Message);
            }
        }

        void ImportClipboard()
        {
            if (File.Exists(YardGuideSnapshot.LinkedPath)) { Reload(); return; }
            LoadGuideSnapshot(GUIUtility.systemCopyBuffer);
        }

        void EnterDemo()
        {
            if (source == Source.StandaloneDemo && collection != null) return;
            Apply(YardGuideSnapshot.Demo(),Source.StandaloneDemo,null);
        }

        void UseDemo()
        {
            if (File.Exists(YardGuideSnapshot.LinkedPath) || File.Exists(YardGuideSnapshot.ImportedPath))
            {
                Reload();
                return;
            }
            PlayerPrefs.SetInt(DemoOptIn,1);
            PlayerPrefs.Save();
            EnterDemo();
        }

        void Apply(YardGuideSnapshot snapshot, Source next, string json)
        {
            collection = snapshot;
            source = next;
            activeJson = json;
            registered.Clear();
            placed.Clear();
            placementKey = null;
            if (snapshot != null)
            {
                foreach (var animal in snapshot.animals) registered.Add(animal.id,animal);
                string scope = source == Source.StandaloneDemo ? "demo" : snapshot.examples ? "examples" : "guide";
                placementKey = "jeju.yard.placement.v1." + scope + "." +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(snapshot.profile));
                RestorePlacement();
            }
            UpdateWorld();
            portraits.KeepRegistered(registered);
            proximity.SetWardrobeScope(placementKey);
            BindPanel();
        }

        void RestorePlacement()
        {
            if (!PlayerPrefs.HasKey(placementKey))
            {
                if (source == Source.StandaloneDemo)
                    foreach (var animal in collection.animals)
                        if (HasModel(animal)) placed.Add(animal.id);
                return;
            }
            Placement saved;
            try { saved = JsonUtility.FromJson<Placement>(PlayerPrefs.GetString(placementKey)); }
            catch (ArgumentException) { return; }
            if (saved?.ids == null) return;
            foreach (string id in saved.ids)
                if (id != null && registered.TryGetValue(id,out var item) && HasModel(item)) placed.Add(id);
            if (placed.Count != saved.ids.Length) SavePlacement();
        }

        void SavePlacement()
        {
            var ids = new string[placed.Count];
            placed.CopyTo(ids);
            Array.Sort(ids,StringComparer.Ordinal);
            PlayerPrefs.SetString(placementKey,JsonUtility.ToJson(new Placement { ids=ids }));
            PlayerPrefs.Save();
        }

        bool HasModel(YardGuideSnapshot.Animal animal) =>
            !string.IsNullOrEmpty(animal.modelKey) && models.ContainsKey(animal.modelKey);

        public string PlacedSpeciesId(string modelKey)
        {
            if (string.IsNullOrEmpty(modelKey) || !models.TryGetValue(modelKey, out var model) ||
                !model.activeInHierarchy) return null;
            return residents.TryGetValue(modelKey, out string id) ? id : null;
        }

        public bool TryGetAccessories(string modelKey, string expectedSpeciesId, string expectedCollectionKey, out YardAccessories equipped)
        {
            equipped = YardAccessories.None;
            Reload();
            if (string.IsNullOrEmpty(modelKey) || !accessoryMounts.ContainsKey(modelKey) || string.IsNullOrEmpty(expectedSpeciesId) ||
                placementKey != expectedCollectionKey || PlacedSpeciesId(modelKey) != expectedSpeciesId) return false;
            equipped = YardAnimalAccessories.Read(placementKey, expectedSpeciesId);
            return true;
        }

        public bool TrySetAccessories(string modelKey, string expectedSpeciesId, string expectedCollectionKey, YardAccessories equipped)
        {
            if ((equipped & ~YardAnimalAccessories.All) != 0 ||
                !TryGetAccessories(modelKey,expectedSpeciesId,expectedCollectionKey,out var previous)) return false;
            if (previous == equipped) return true;
            YardAnimalAccessories.Save(placementKey,expectedSpeciesId,equipped);
            accessoryMounts[modelKey].Wear(equipped);
            portraits.UpdateOutfit(modelKey,expectedSpeciesId,equipped);
            return true;
        }

        Texture Preview(string modelKey, string id)
        {
            var outfit = YardAnimalAccessories.Supports(modelKey) ? YardAnimalAccessories.Read(placementKey,id) : YardAccessories.None;
            return portraits.Get(modelKey, models[modelKey], id, outfit);
        }

        public void OpenResident(string modelKey, string expectedSpeciesId = null, string expectedCollectionKey = null)
        {
            Reload();
            string id = PlacedSpeciesId(modelKey);
            if (id == null || (expectedSpeciesId != null && expectedSpeciesId != id) ||
                (expectedCollectionKey != null && expectedCollectionKey != placementKey)) return;
            foreach (var card in cards)
                if (card.id == id)
                {
                    card.preview = Preview(modelKey,id);
                    panel.OpenResident(id);
                    return;
                }
        }

        public bool PetResident(string modelKey, out bool gained, out bool full)
        {
            gained = full = false;
            string id = PlacedSpeciesId(modelKey);
            if (id == null) return false;
            string scope = placementKey;
            Reload();
            if (scope != placementKey || PlacedSpeciesId(modelKey) != id) return false;
            int points = YardAffection.Pet(scope, id, DateTime.UtcNow.Ticks, out gained);
            full = points == YardAffection.Maximum;
            if (gained && panel.IsDetailOpen) panel.RefreshAffection();
            return true;
        }

        public void Toggle(string id)
        {
            if (id == null || !registered.TryGetValue(id,out var animal) || !HasModel(animal)) return;
            if (!placed.Remove(id)) placed.Add(id);
            SavePlacement();
            UpdateWorld();
            BindPanel();
        }

        void UpdateWorld()
        {
            residents.Clear();
            // Legacy records can share a model. Its visible occupant is always the first
            // placed species in guide order, and it wears only that species' saved pieces.
            if (collection != null)
                foreach (var animal in collection.animals)
                    if (placed.Contains(animal.id) && !residents.ContainsKey(animal.modelKey))
                        residents.Add(animal.modelKey,animal.id);
            foreach (var model in models)
            {
                bool inYard = residents.TryGetValue(model.Key,out var id);
                if (accessoryMounts.TryGetValue(model.Key,out var mount))
                    mount.Wear(inYard ? YardAnimalAccessories.Read(placementKey,id) : YardAccessories.None);
                model.Value.SetActive(inYard);
            }
        }

        void BindPanel()
        {
            cards.Clear();
            if (collection != null)
            {
                foreach (var animal in collection.animals)
                {
                    bool supported = HasModel(animal);
                    cards.Add(new YardCollectionPanel.Entry
                    {
                        id = animal.id, name = animal.name, placed = placed.Contains(animal.id),
                        firstMetDate = animal.DisplayDate, firstMetLocation = animal.firstMetLocation,
                        supported = supported,
                        preview = panel.IsOpen && !panel.WardrobeOpen && supported ? Preview(animal.modelKey,animal.id) : null
                    });
                }
            }
            string title = source == Source.StandaloneDemo ? "시연 도감" :
                collection != null && collection.examples ? "예시 도감" : "나의 도감";
            panel.SetSources(source == Source.Linked ? (Action)null : ImportClipboard,
                source == Source.Unlinked ? (Action)UseDemo : null);
            panel.Bind(cards,Toggle,title,ReadAffection);
        }

        YardAffection.Bond ReadAffection(string id) => YardAffection.GetBond(placementKey, id);
    }
}
