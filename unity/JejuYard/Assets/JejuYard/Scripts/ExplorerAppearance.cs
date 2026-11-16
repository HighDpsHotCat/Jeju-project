using System;
using UnityEngine;

namespace Jeju.Yard
{
    [DisallowMultipleComponent]
    public sealed class ExplorerAppearance : MonoBehaviour
    {
        [Serializable]
        public struct TopMeshes
        {
            public Mesh body, leftSleeve, rightSleeve;
        }

        [Serializable]
        sealed class Selection { public int top, hat; }

        public const int TopCount = 3, HatCount = 3;
        static readonly string[] TopLabels = { "탐험 재킷", "줄무늬 세일러 티", "산호빛 니트" };
        static readonly string[] HatLabels = { "감귤 탐험모", "남색 버킷햇", "밀짚모자" };
        [SerializeField] MeshFilter topSocket, leftArmSocket, rightArmSocket, hatSocket;
        [SerializeField] TopMeshes[] tops;
        [SerializeField] Mesh[] hats;
        string preferenceKey;

        public int SelectedTop { get; private set; }
        public int SelectedHat { get; private set; }
        public static string TopName(int index) => (uint)index < TopCount ? TopLabels[index] : "";
        public static string HatName(int index) => (uint)index < HatCount ? HatLabels[index] : "";

        void Awake()
        {
            if (preferenceKey == null) SetScope(null);
        }

        public void SetScope(string activeCollectionKey)
        {
            string key = "jeju.yard.appearance.v1." + (string.IsNullOrEmpty(activeCollectionKey) ? "unlinked" : activeCollectionKey);
            if (key == preferenceKey) return;
            preferenceKey = key;
            Selection saved = null;
            if (PlayerPrefs.HasKey(key))
            {
                try { saved = JsonUtility.FromJson<Selection>(PlayerPrefs.GetString(key)); }
                catch (ArgumentException) { }
            }
            Apply(saved != null && (uint)saved.top < TopCount ? saved.top : 0,
                  saved != null && (uint)saved.hat < HatCount ? saved.hat : 0);
        }

        public bool SelectTop(int index) => (uint)index < TopCount && Save(index, SelectedHat);
        public bool SelectHat(int index) => (uint)index < HatCount && Save(SelectedTop, index);

        public void ResetToDefault()
        {
            if (preferenceKey == null) SetScope(null);
            Apply(0, 0);
            PlayerPrefs.DeleteKey(preferenceKey);
            PlayerPrefs.Save();
        }

        bool Save(int top, int hat)
        {
            if (preferenceKey == null) SetScope(null);
            if (SelectedTop == top && SelectedHat == hat) return true;
            Apply(top, hat);
            PlayerPrefs.SetString(preferenceKey, JsonUtility.ToJson(new Selection { top = top, hat = hat }));
            PlayerPrefs.Save();
            return true;
        }

        void Apply(int top, int hat)
        {
            var outfit = tops[top];
            topSocket.sharedMesh = outfit.body;
            leftArmSocket.sharedMesh = outfit.leftSleeve;
            rightArmSocket.sharedMesh = outfit.rightSleeve;
            hatSocket.sharedMesh = hats[hat];
            SelectedTop = top;
            SelectedHat = hat;
        }

#if UNITY_EDITOR
        public void SetWardrobeMeshes(MeshFilter body, MeshFilter left, MeshFilter right,
            MeshFilter headwear, TopMeshes[] outfits, Mesh[] headwearMeshes)
        {
            topSocket = body;
            leftArmSocket = left;
            rightArmSocket = right;
            hatSocket = headwear;
            tops = outfits;
            hats = headwearMeshes;
            Apply(0, 0);
        }
#endif
    }
}
