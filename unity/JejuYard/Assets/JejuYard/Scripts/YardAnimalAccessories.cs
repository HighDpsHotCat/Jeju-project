using System;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jeju.Yard
{
    [Flags]
    public enum YardAccessories { None = 0, StarHeadband = 1, RedCape = 2 }

    public static class YardAnimalAccessories
    {
        internal const YardAccessories All = YardAccessories.StarHeadband | YardAccessories.RedCape;

        public static bool Supports(string modelKey) => modelKey == "corgi" || modelKey == "tabby" ||
            modelKey == "gull" || modelKey == "sparrow" || modelKey == "dolphin";

        public static string PreferenceKey(string collectionKey, string speciesId) =>
            collectionKey + ".outfit.v1." + Convert.ToBase64String(Encoding.UTF8.GetBytes(speciesId));

        internal static YardAccessories Read(string collectionKey, string speciesId) =>
            (YardAccessories)PlayerPrefs.GetInt(PreferenceKey(collectionKey, speciesId), 0) & All;

        internal static void Save(string collectionKey, string speciesId, YardAccessories outfit)
        {
            string key = PreferenceKey(collectionKey, speciesId);
            if (outfit == YardAccessories.None) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, (int)outfit);
            PlayerPrefs.Save();
        }

        // Each mount is local to a live skeleton or a single portrait skeleton. No global
        // search, per-frame positioning, material instances, or separate animation is needed.
        internal sealed class Mount
        {
            readonly string modelKey;
            readonly YardAccessoryMeshes art;
            readonly Transform head, back;
            readonly Transform leftWing, rightWing;
            readonly Material material;
            readonly Vector3 bandPosition, bandScale;
            readonly bool portrait;
            Transform band, cape;
            Transform leftShoulder, rightShoulder;
            public bool Available => head != null && back != null && material != null;

            public Mount(string modelKey, GameObject source, YardAccessoryMeshes art, bool portrait)
            {
                this.modelKey = modelKey;
                this.art = art;
                this.portrait = portrait;
                if (modelKey == "dolphin")
                {
                    var swimmer = source.GetComponentInChildren<DolphinVisitor>(true);
                    if (swimmer == null) return;
                    head = back = swimmer.transform;
                    bandPosition = new Vector3(0,.079f,.756f);
                    bandScale = new Vector3(.275f,.273f,.27f);
                }
                else if (modelKey == "corgi" || modelKey == "tabby")
                {
                    var mammal = source.GetComponentInChildren<QuadrupedRoamer>(true);
                    if (mammal == null) return;
                    head = mammal.head;
                    back = mammal.body;
                    bandPosition = mammal.isCat ? new Vector3(0,.105f,.082f) : new Vector3(0,.150f,.106f);
                    bandScale = mammal.isCat ? new Vector3(.221f,.219f,.217f) : new Vector3(.302f,.281f,.293f);
                }
                else
                {
                    var bird = source.GetComponentInChildren<YardBird>(true);
                    if (bird == null) return;
                    head = bird.head;
                    back = bird.torso;
                    leftWing = bird.leftWing;
                    rightWing = bird.rightWing;
                    bool gull = modelKey == "gull";
                    bandPosition = gull ? new Vector3(0,.07f,.107f) : new Vector3(0,.06f,.061f);
                    bandScale = gull ? new Vector3(.145f,.15f,.147f) : new Vector3(.132f,.135f,.133f);
                }
                if (head == null || back == null) return;
                var surface = back.GetComponentInChildren<MeshRenderer>(true);
                if (surface == null) return;
                material = surface.sharedMaterial;
                // A freshly cloned live actor can already be wearing either or both pieces.
                band = head.Find("Star headband");
                cape = back.Find("Red cape");
                leftShoulder = leftWing != null ? leftWing.Find("Red cape shoulder") : null;
                rightShoulder = rightWing != null ? rightWing.Find("Red cape shoulder") : null;
            }

            public void Wear(YardAccessories outfit)
            {
                if (!Available) return;
                bool hasBand = (outfit & YardAccessories.StarHeadband) != 0;
                bool hasCape = (outfit & YardAccessories.RedCape) != 0;
                if (hasBand && band == null)
                {
                    band = Piece("Star headband", head, art.Headband);
                    band.localPosition = bandPosition;
                    band.localScale = bandScale;
                }
                if (hasCape && cape == null) cape = Piece("Red cape", back, art.Cape(modelKey));
                if (hasCape && leftWing != null && leftShoulder == null)
                    leftShoulder = Piece("Red cape shoulder", leftWing, art.BirdShoulder(modelKey, -1));
                if (hasCape && rightWing != null && rightShoulder == null)
                    rightShoulder = Piece("Red cape shoulder", rightWing, art.BirdShoulder(modelKey, 1));
                if (band != null) band.gameObject.SetActive(hasBand);
                if (cape != null) cape.gameObject.SetActive(hasCape);
                if (leftShoulder != null) leftShoulder.gameObject.SetActive(hasCape);
                if (rightShoulder != null) rightShoulder.gameObject.SetActive(hasCape);
            }

            Transform Piece(string name, Transform parent, Mesh mesh)
            {
                var piece = new GameObject(name);
                piece.transform.SetParent(parent, false);
                piece.layer = portrait ? 31 : parent.gameObject.layer;
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (portrait)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                return piece.transform;
            }
        }
    }
}
