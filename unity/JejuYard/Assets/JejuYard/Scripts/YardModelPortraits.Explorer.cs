using UnityEngine;
using UnityEngine.Rendering;

namespace Jeju.Yard
{
    public sealed partial class YardModelPortraits
    {
        RenderTexture explorerPhoto;

        // Photograph the currently dressed visual only when the wardrobe opens or changes.
        public Texture GetExplorerPreview(Transform visual)
        {
            if (visual == null) return null;
            EnsureCamera();
            var pivot = new GameObject("Wardrobe portrait");
            pivot.SetActive(false);
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(500f, 100f, 500f);
            try
            {
                Transform model = Instantiate(visual, pivot.transform, false);
                model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                pivot.SetActive(true);
                var visible = model.GetComponentsInChildren<MeshRenderer>();
                if (visible.Length == 0) return null;
                Bounds bounds = visible[0].bounds;
                foreach (var part in visible)
                {
                    part.gameObject.layer = PreviewLayer;
                    part.shadowCastingMode = ShadowCastingMode.Off;
                    part.receiveShadows = false;
                    bounds.Encapsulate(part.bounds);
                }
                pivot.SetActive(false);
                if (explorerPhoto == null) explorerPhoto = CreateTarget(480, 352, "Wardrobe preview");
                Draw(new Portrait { pivot = pivot, bounds = bounds, elevation = 12f }, explorerPhoto, 21f);
                return explorerPhoto;
            }
            finally
            {
                pivot.SetActive(false);
                Destroy(pivot);
            }
        }

        public void ReleaseExplorerPreview()
        {
            if (explorerPhoto == null) return;
            explorerPhoto.Release();
            Destroy(explorerPhoto);
            explorerPhoto = null;
        }

        partial void DisposeExplorerPreview() => ReleaseExplorerPreview();
    }
}
