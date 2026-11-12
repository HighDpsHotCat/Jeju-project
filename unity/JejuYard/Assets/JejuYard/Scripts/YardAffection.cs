using System;
using System.Text;
using UnityEngine;

namespace Jeju.Yard
{
    public static class YardAffection
    {
        public const int Maximum = 100;
        public const int PointsPerHeart = 5;
        public const long CooldownTicks = 6 * TimeSpan.TicksPerHour;

        [Serializable]
        sealed class Bond
        {
            public int points;
            public long lastAwardUtcTicks;
        }

        public static string PreferenceKey(string collectionKey, string speciesId)
        {
            if (string.IsNullOrEmpty(collectionKey) || string.IsNullOrEmpty(speciesId))
                throw new ArgumentException("An affection record requires a collection and species.");
            return collectionKey + ".affection.v1." + Convert.ToBase64String(Encoding.UTF8.GetBytes(speciesId));
        }

        static Bond Read(string key)
        {
            var json = PlayerPrefs.GetString(key, "");
            if (json.Length > 0)
            {
                try
                {
                    var saved = JsonUtility.FromJson<Bond>(json);
                    if (saved != null && saved.lastAwardUtcTicks >= 0 && saved.lastAwardUtcTicks <= DateTime.MaxValue.Ticks)
                    {
                        saved.points = Mathf.Clamp(saved.points, 0, Maximum);
                        return saved;
                    }
                }
                catch (ArgumentException) { }
            }
            return new Bond();
        }

        public static int GetPoints(string collectionKey, string speciesId) => Read(PreferenceKey(collectionKey, speciesId)).points;

        public static int Pet(string collectionKey, string speciesId, long utcTicks, out bool gained)
        {
            if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks) throw new ArgumentOutOfRangeException(nameof(utcTicks));
            string key = PreferenceKey(collectionKey, speciesId);
            var bond = Read(key);
            gained = false;
            if (bond.points == Maximum || (bond.points > 0 && utcTicks - bond.lastAwardUtcTicks < CooldownTicks))
                return bond.points;
            bond.points = Mathf.Min(Maximum, bond.points + PointsPerHeart);
            bond.lastAwardUtcTicks = utcTicks;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(bond));
            PlayerPrefs.Save();
            gained = true;
            return bond.points;
        }
    }
}
