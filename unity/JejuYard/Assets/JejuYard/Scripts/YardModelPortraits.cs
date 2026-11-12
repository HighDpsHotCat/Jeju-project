using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jeju.Yard
{
    // One offstage camera serves the collection. The yard keeps its own actors and animations.
    public sealed class YardModelPortraits : MonoBehaviour
    {
        const int PreviewLayer = 31;
        const int Width = 256, Height = 208;
        readonly Dictionary<string, Portrait> byKey = new Dictionary<string, Portrait>();
        readonly List<Portrait> portraits = new List<Portrait>();
        Camera lens;
        int nextPortrait;
        float nextDraw;

        sealed class Portrait
        {
            public GameObject pivot;
            public RenderTexture texture;
            public Bounds bounds;
            public float elevation;
        }

        public Texture Get(string modelKey, GameObject source)
        {
            if (string.IsNullOrEmpty(modelKey) || source == null) return null;
            if (byKey.TryGetValue(modelKey, out var existing)) return existing.texture;

            if (lens == null)
            {
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
            }

            var pivot = new GameObject(modelKey + " portrait");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(500f, 100f, 500f);
            pivot.SetActive(false);
            var copy = Instantiate(source, pivot.transform, false);
            copy.SetActive(true);
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

            var renderers = frame.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) { Destroy(pivot); return null; }
            pivot.SetActive(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                renderer.gameObject.layer = PreviewLayer;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                bounds.Encapsulate(renderer.bounds);
            }
            pivot.SetActive(false);

            var descriptor = new RenderTextureDescriptor(Width, Height, RenderTextureFormat.ARGB32, 16) { msaaSamples = 4 };
            descriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            var target = new RenderTexture(descriptor)
            {
                name = modelKey + " portrait", filterMode = FilterMode.Bilinear
            };
            target.Create();
            var portrait = new Portrait
            {
                pivot = pivot, texture = target, bounds = bounds,
                elevation = modelKey == "swallowtail" ? 43f : 17f
            };
            byKey.Add(modelKey, portrait);
            portraits.Add(portrait);
            Draw(portrait, 52f);
            return target;
        }

        static Transform SetPose(GameObject copy)
        {
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
            if (!YardCollectionPanel.BlocksYardInput || portraits.Count == 0 || Time.unscaledTime < nextDraw) return;
            // Stagger the portraits so even a full grid costs at most one small render per frame.
            var portrait = portraits[nextPortrait];
            float angle = 52f + 16f * Mathf.Sin(Time.unscaledTime * .52f);
            Draw(portrait, angle);
            nextPortrait = (nextPortrait + 1) % portraits.Count;
            nextDraw = Time.unscaledTime + 1f / 30f;
        }

        void Draw(Portrait portrait, float angle)
        {
            var direction = Quaternion.Euler(-portrait.elevation, angle, 0f) * Vector3.forward;
            var rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var extent = portrait.bounds.extents;
            float Project(Vector3 axis) => Mathf.Abs(axis.x)*extent.x + Mathf.Abs(axis.y)*extent.y + Mathf.Abs(axis.z)*extent.z;
            lens.aspect = (float)Width / Height;
            float halfHeight = Project(rotation * Vector3.up);
            float halfWidth = Project(rotation * Vector3.right);
            lens.orthographicSize = Mathf.Max(.1f, Mathf.Max(halfHeight, halfWidth / lens.aspect)) * 1.08f;
            lens.transform.SetPositionAndRotation(portrait.bounds.center + direction * 8f, rotation);
            lens.targetTexture = portrait.texture;
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

        void OnDestroy()
        {
            foreach (var portrait in portraits)
            {
                portrait.texture.Release();
                Destroy(portrait.texture);
            }
        }
    }
}
