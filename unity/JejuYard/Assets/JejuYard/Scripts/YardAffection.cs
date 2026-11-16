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

        public enum RewardState { NeverPetted, Eligible, CoolingDown, Full }

        [Serializable]
        public struct Bond
        {
            public int points;
            public long lastAwardUtcTicks;

            public RewardState RewardAt(long utcTicks, out long remainingTicks)
            {
                if (utcTicks < 0 || utcTicks > DateTime.MaxValue.Ticks)
                    throw new ArgumentOutOfRangeException(nameof(utcTicks));
                remainingTicks = 0;
                if (points >= Maximum) return RewardState.Full;
                if (points == 0) return RewardState.NeverPetted;
                long elapsed = utcTicks - lastAwardUtcTicks;
                if (elapsed >= CooldownTicks) return RewardState.Eligible;
                remainingTicks = CooldownTicks - elapsed;
                return RewardState.CoolingDown;
            }
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
                    if (JsonUtility.FromJson(json, typeof(Bond)) is Bond saved &&
                        saved.lastAwardUtcTicks >= 0 && saved.lastAwardUtcTicks <= DateTime.MaxValue.Ticks)
                    {
                        saved.points = Mathf.Clamp(saved.points, 0, Maximum);
                        return saved;
                    }
                }
                catch (ArgumentException) { }
            }
            return default;
        }

        public static Bond GetBond(string collectionKey, string speciesId) => Read(PreferenceKey(collectionKey, speciesId));
        public static int GetPoints(string collectionKey, string speciesId) => GetBond(collectionKey, speciesId).points;

        public static int Pet(string collectionKey, string speciesId, long utcTicks, out bool gained)
        {
            string key = PreferenceKey(collectionKey, speciesId);
            var bond = Read(key);
            var state = bond.RewardAt(utcTicks, out _);
            gained = state == RewardState.NeverPetted || state == RewardState.Eligible;
            if (!gained) return bond.points;
            bond.points = Mathf.Min(Maximum, bond.points + PointsPerHeart);
            bond.lastAwardUtcTicks = utcTicks;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(bond));
            PlayerPrefs.Save();
            return bond.points;
        }
    }
}
