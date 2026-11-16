using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jeju.Yard
{
    // One offstage camera serves the collection. The yard keeps its own actors and animations.
    public sealed partial class YardModelPortraits : MonoBehaviour
    {
        const int PreviewLayer = 31;
        const int Width = 256, Height = 208;
        readonly Dictionary<(string model, string id), Portrait> byKey = new Dictionary<(string, string), Portrait>();
        internal readonly YardAccessoryMeshes AccessoryArt = new YardAccessoryMeshes();
        YardCollectionPanel panel;
        readonly List<Portrait> portraits = new List<Portrait>();
        Camera lens;
        Portrait inspected;
        RenderTexture closeUp;
        int nextPortrait;
        float nextDraw;

        sealed class Portrait
        {
            public string modelKey, speciesId;
            public YardAnimalAccessories.Mount accessories;
            public YardAccessories outfit;
            public GameObject pivot;
            public RenderTexture texture;
            public Bounds bounds;
            public float elevation;
        }

        void EnsureCamera()
        {
            if (lens != null) return;
            var studio = new GameObject("Collection portrait camera");
            studio.transform.SetParent(transform, false);
            lens = studio.AddComponent<Camera>();
            lens.enabled = false;
            lens.orthographic = true;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = Color.clear;
            lens.cullingMask = 1 << PreviewLayer;
            lens.nearClipPlane = .05f;
            lens.farClipPlane = 25f;
            lens.allowHDR = false;
            lens.useOcclusionCulling = false;
            panel = GetComponent<YardCollectionPanel>();
        }

        public Texture Get(string modelKey, GameObject source) => Get(modelKey, source, modelKey, YardAccessories.None);

        public Texture Get(string modelKey, GameObject source, string speciesId, YardAccessories outfit)
        {
            if (string.IsNullOrEmpty(modelKey) || source == null) return null;
            if (byKey.TryGetValue((modelKey, speciesId), out var existing))
            {
                SetOutfit(existing, outfit);
                return existing.texture;
            }
            EnsureCamera();
            var pivot = new GameObject(modelKey + " portrait");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(500f, 100f, 500f);
            pivot.SetActive(false);
            var copy = Instantiate(source, pivot.transform, false);
            copy.SetActive(true);
            var accessories = YardAnimalAccessories.Supports(modelKey)
                ? new YardAnimalAccessories.Mount(modelKey, copy, AccessoryArt, true) : null;
            accessories?.Wear(outfit);
            var frame = SetPose(copy);
            if (frame == null) { Destroy(pivot); return null; }
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;

            // Remove travel offsets, the dolphin wake and other surroundings from the shot.
            frame.SetParent(pivot.transform, false);
            frame.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (frame.gameObject != copy)
            {
                copy.SetActive(false);
                Destroy(copy);
            }

            var portrait = new Portrait
            {
                pivot = pivot, modelKey = modelKey, speciesId = speciesId,
                accessories = accessories, outfit = outfit, elevation = source.GetComponentInChildren<ButterflyFlight>(true) != null ? 43f : 17f
            };
            if (!Reframe(portrait)) { Destroy(pivot); return null; }
            var target = portrait.texture = CreateTarget(Width, Height, modelKey + " portrait");
            byKey.Add((modelKey,speciesId), portrait);
            portraits.Add(portrait);
            Draw(portrait, target, 52f);
            return target;
        }

        public void UpdateOutfit(string modelKey, string speciesId, YardAccessories outfit)
        {
            if (byKey.TryGetValue((modelKey,speciesId), out var portrait)) SetOutfit(portrait, outfit);
        }

        void SetOutfit(Portrait portrait, YardAccessories outfit)
        {
            if (portrait.outfit == outfit) return;
            portrait.accessories?.Wear(outfit);
            portrait.outfit = outfit;
            Reframe(portrait);
            Draw(portrait,portrait.texture,52f);
            if (inspected == portrait) Draw(portrait,closeUp,41f);
        }

        static bool Reframe(Portrait portrait)
        {
            // Frame active pieces only; an unequipped band must not enlarge the shot.
            portrait.pivot.SetActive(true);
            bool started = false;
            foreach (var renderer in portrait.pivot.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.gameObject.layer = PreviewLayer;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!started) { portrait.bounds = renderer.bounds; started = true; }
                else portrait.bounds.Encapsulate(renderer.bounds);
            }
            portrait.pivot.SetActive(false);
            return started;
        }

        public void KeepRegistered(IReadOnlyDictionary<string,YardGuideSnapshot.Animal> registrations)
        {
            for (int i = portraits.Count - 1; i >= 0; i--)
            {
                var p = portraits[i];
                if (registrations.TryGetValue(p.speciesId, out var animal) && animal.modelKey == p.modelKey) continue;
                if (inspected == p) Inspect(null);
                portraits.RemoveAt(i);
                byKey.Remove((p.modelKey,p.speciesId));
                p.texture.Release();
                Destroy(p.texture);
                Destroy(p.pivot);
            }
            nextPortrait = 0;
        }

        static RenderTexture CreateTarget(int width, int height, string name)
        {
            var descriptor = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 16) { msaaSamples = 4 };
            descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            var target = new RenderTexture(descriptor) { name = name, filterMode = FilterMode.Bilinear };
            target.Create();
            return target;
        }

        // The larger view reuses the card's mesh and camera. Only the chosen animal is redrawn.
        public Texture Inspect(Texture thumbnail)
        {
            if (inspected != null && inspected.texture == thumbnail) return closeUp;
            inspected = null;
            if (thumbnail != null)
            {
                foreach (var portrait in portraits)
                    if (portrait.texture == thumbnail) { inspected = portrait; break; }
            }
            if (inspected == null)
            {
                if (closeUp != null) { closeUp.Release(); Destroy(closeUp); closeUp = null; }
                return thumbnail;
            }
            if (closeUp == null) closeUp = CreateTarget(560, 384, "Animal detail");
            Draw(inspected, closeUp, 41f);
            return closeUp;
        }

        static Transform SetPose(GameObject copy)
        {
            var deer = copy.GetComponentInChildren<GrazingDeer>(true);
            if (deer != null) { deer.PoseAt(0f); return deer.transform; }
            var mammal = copy.GetComponentInChildren<QuadrupedRoamer>(true);
            if (mammal != null) { mammal.PoseAt(0f); return mammal.transform; }
            var bird = copy.GetComponentInChildren<YardBird>(true);
            if (bird != null) { bird.PoseAt(0f); return bird.flightFrame; }
            var butterfly = copy.GetComponentInChildren<ButterflyFlight>(true);
            if (butterfly != null) { butterfly.PoseAt(.15f); return butterfly.airframe; }
            var dolphin = copy.GetComponentInChildren<DolphinVisitor>(true);
            if (dolphin != null) { dolphin.PoseAt(0f); return dolphin.transform; }
            return null;
        }

        void LateUpdate()
        {
            if (!YardCollectionPanel.BlocksYardInput || portraits.Count == 0 || Time.unscaledTime < nextDraw ||
                (panel != null && panel.WardrobeOpen)) return;
            if (inspected != null)
            {
                Draw(inspected, closeUp, 41f + 13f * Mathf.Sin(Time.unscaledTime * .5f));
                nextDraw = Time.unscaledTime + 1f / 20f;
                return;
            }
            // Stagger the portraits so even a full grid costs at most one small render per frame.
            var portrait = portraits[nextPortrait];
            float angle = 52f + 16f * Mathf.Sin(Time.unscaledTime * .52f);
            Draw(portrait, portrait.texture, angle);
            nextPortrait = (nextPortrait + 1) % portraits.Count;
            nextDraw = Time.unscaledTime + 1f / 30f;
        }

        void Draw(Portrait portrait, RenderTexture target, float angle)
        {
            var direction = Quaternion.Euler(-portrait.elevation, angle, 0f) * Vector3.forward;
            var rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var extent = portrait.bounds.extents;
            float Project(Vector3 axis) => Mathf.Abs(axis.x)*extent.x + Mathf.Abs(axis.y)*extent.y + Mathf.Abs(axis.z)*extent.z;
            lens.aspect = (float)target.width / target.height;
            float halfHeight = Project(rotation * Vector3.up);
            float halfWidth = Project(rotation * Vector3.right);
            lens.orthographicSize = Mathf.Max(.1f, Mathf.Max(halfHeight, halfWidth / lens.aspect)) * 1.08f;
            lens.transform.SetPositionAndRotation(portrait.bounds.center + direction * 8f, rotation);
            lens.targetTexture = target;
            var previous = RenderTexture.active;
            portrait.pivot.SetActive(true);
            try { lens.Render(); }
            finally
            {
                portrait.pivot.SetActive(false);
                lens.targetTexture = null;
                RenderTexture.active = previous;
            }
        }

        partial void DisposeExplorerPreview();

        void OnDestroy()
        {
            DisposeExplorerPreview();
            Inspect(null);
            AccessoryArt.Dispose();
            foreach (var portrait in portraits)
            {
                portrait.texture.Release();
                Destroy(portrait.texture);
            }
        }
    }
}
