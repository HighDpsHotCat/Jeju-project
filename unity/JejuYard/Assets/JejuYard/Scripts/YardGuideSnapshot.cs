using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Jeju.Yard
{
    [Serializable]
    public sealed class YardGuideSnapshot
    {
        [Serializable]
        public sealed class Animal
        {
            public string id, name, modelKey;

            public Animal(string id, string name, string modelKey)
            {
                this.id = id;
                this.name = name;
                this.modelKey = modelKey;
            }
        }

        public int version;
        public string profile;
        public bool examples;
        public Animal[] animals;

        // The embedded player shares its host's app container. A Mac editor or standalone
        // player can also read the guide published by the separate sandboxed Flutter app.
        public static string LinkedPath
        {
            get
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                return Path.Combine(home, "Library/Containers/com.example.jejuProject/Data/Library/Application Support/JejuYard/yard-collection.json");
#else
                return Path.Combine(Application.persistentDataPath, "yard-collection.json");
#endif
            }
        }

        public static string ImportedPath => Path.Combine(Application.persistentDataPath, "yard-import.json");

        public static bool TryParse(string json, out YardGuideSnapshot guide)
        {
            guide = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var parsed = JsonUtility.FromJson<YardGuideSnapshot>(json);
                if (parsed == null || parsed.version != 1 || string.IsNullOrWhiteSpace(parsed.profile) ||
                    parsed.animals == null) return false;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var animal in parsed.animals)
                {
                    if (animal == null || string.IsNullOrWhiteSpace(animal.id) ||
                        string.IsNullOrWhiteSpace(animal.name) || !seen.Add(animal.id)) return false;
                }
                guide = parsed;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static void SaveImport(string json)
        {
            string target = ImportedPath;
            Directory.CreateDirectory(Application.persistentDataPath);
            string staging = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(staging, json, new UTF8Encoding(false));
                if (File.Exists(target)) File.Replace(staging, target, null);
                else File.Move(staging, target);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
            }
        }

        public static YardGuideSnapshot Demo()
        {
            return new YardGuideSnapshot
            {
                version = 1, profile = "standalone", examples = true,
                animals = new[]
                {
                    new Animal("JJ-000821", "웰시코기", "corgi"),
                    new Animal("JJ-001834", "노란 고양이", "tabby"),
                    new Animal("JJ-002426", "갈매기", "gull"),
                    new Animal("JJ-003483", "참새", "sparrow"),
                    new Animal("JJ-003382", "호랑나비", "swallowtail"),
                    new Animal("JJ-004729", "남방큰돌고래", "dolphin")
                }
            };
        }
    }
}
