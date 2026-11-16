using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Jeju.Yard
{
    [Serializable]
    public sealed class YardGuideSnapshot
    {
        [Serializable]
        public sealed class Animal
        {
            public string id, name, modelKey, firstMetDate, firstMetLocation;

            public Animal(string id, string name, string modelKey, string firstMetDate = null, string firstMetLocation = null)
            {
                this.id = id;
                this.name = name;
                this.modelKey = modelKey;
                this.firstMetDate = firstMetDate;
                this.firstMetLocation = firstMetLocation;
            }

            public string DisplayDate => firstMetDate?.Replace('-', '.') ?? "";
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

        static readonly Regex AddressSeparators = new Regex(@"[\s,()/]+");
        static readonly Regex City = new Regex(@"^[가-힣]{1,10}시$");
        static readonly Regex UpperRegion = new Regex(@"^[가-힣]{1,10}[도구군]$");
        static readonly HashSet<string> JejuCityAreas = new HashSet<string>(
            ("한림읍 애월읍 구좌읍 조천읍 한경면 추자면 우도면 일도1동 일도2동 이도1동 이도2동 삼도1동 삼도2동 " +
             "용담1동 용담2동 건입동 화북동 삼양동 봉개동 아라동 오라동 연동 노형동 외도동 이호동 도두동").Split(' '));
        static readonly HashSet<string> SeogwipoAreas = new HashSet<string>(
            ("대정읍 남원읍 성산읍 안덕면 표선면 송산동 정방동 중앙동 천지동 효돈동 영천동 동홍동 서홍동 " +
             "대륜동 대천동 중문동 예래동").Split(' '));

        static string ValidDate(string input)
        {
            return DateTime.TryParseExact(input?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        }

        static string CoarseLocation(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string city = "";
            // Only read the administrative prefix, so road names, building wings and
            // apartment numbers further down an address cannot become a neighborhood.
            foreach (var token in AddressSeparators.Split(input.Trim()))
            {
                if (token == "" || token == "대한민국" || token == "한국") continue;
                if (City.IsMatch(token) && city == "") { city = token; continue; }
                if (UpperRegion.IsMatch(token)) continue;
                if (((city == "" || city == "제주시") && JejuCityAreas.Contains(token)) ||
                    ((city == "" || city == "서귀포시") && SeogwipoAreas.Contains(token)))
                    return city == "" ? token : city + " " + token;
                break;
            }
            return city;
        }

        public static bool TryParse(string json, out YardGuideSnapshot guide) => TryParse(json, out guide, out _);

        public static bool TryParse(string json, out YardGuideSnapshot guide, out bool cleaned)
        {
            guide = null;
            cleaned = false;
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
                    string date = ValidDate(animal.firstMetDate);
                    string location = CoarseLocation(animal.firstMetLocation);
                    cleaned |= date != (animal.firstMetDate ?? "") || location != (animal.firstMetLocation ?? "");
                    animal.firstMetDate = date;
                    animal.firstMetLocation = location;
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
                    new Animal("JJ-000821", "웰시코기", "corgi", "2026-05-02", "제주시 애월읍"),
                    new Animal("JJ-001834", "노란 고양이", "tabby", "2026-05-05", "제주시 연동"),
                    new Animal("JJ-002426", "갈매기", "gull", "2026-05-08", "서귀포시 성산읍"),
                    new Animal("JJ-003483", "참새", "sparrow", "2026-05-12", "제주시 조천읍"),
                    new Animal("JJ-003382", "호랑나비", "swallowtail", "2026-05-16", "제주시 한림읍"),
                    new Animal("JJ-004729", "남방큰돌고래", "dolphin", "2026-05-21", "서귀포시 대정읍")
                }
            };
        }
    }
}
