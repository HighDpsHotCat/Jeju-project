#if JEJU_YARD_VISITS
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jeju.Yard
{
    // A visitor can render these model keys in a separate yard view. Reading a
    // visit never changes this device's placements or opens the local guide.
    [Serializable]
    public sealed class YardVisitSnapshot
    {
        const int CurrentVersion = 1;
        [SerializeField] int version;
        [SerializeField] string[] modelKeys;

        public int Version => version;
        public IReadOnlyList<string> ModelKeys => Array.AsReadOnly(modelKeys);
        public string ToJson() => JsonUtility.ToJson(this);

        internal YardVisitSnapshot(SortedSet<string> keys)
        {
            version = CurrentVersion;
            modelKeys = new string[keys.Count];
            keys.CopyTo(modelKeys);
        }

        internal static bool TryRead(string json, ICollection<string> supported, out YardVisitSnapshot snapshot)
        {
            snapshot = null;
            // A v1 payload contains at most six short model keys.
            if (string.IsNullOrWhiteSpace(json) || json.Length > 512) return false;
            YardVisitSnapshot data;
            try { data = JsonUtility.FromJson<YardVisitSnapshot>(json); }
            catch (ArgumentException) { return false; }
            if (data == null || data.version != CurrentVersion || data.modelKeys == null ||
                data.modelKeys.Length > supported.Count) return false;

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string key in data.modelKeys)
                if (string.IsNullOrEmpty(key) || !supported.Contains(key) || !keys.Add(key)) return false;
            snapshot = new YardVisitSnapshot(keys);
            return true;
        }
    }
}
#endif
