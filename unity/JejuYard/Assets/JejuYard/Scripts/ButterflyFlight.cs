using UnityEngine;

namespace Jeju.Yard
{
    public sealed class ButterflyFlight : YardResidentBehaviour
    {
        public Transform airframe, leftWing, rightWing;
        public Vector3 secondBloom = new Vector3(1.983f, -.018f, -.209f);

        const float LegSeconds = 5f;
        const float RestSeconds = 2f;
        const float HalfCycle = LegSeconds + RestSeconds;
        public const float CycleSeconds = 2f * HalfCycle;
        float elapsed;
        Vector3 perch;
        public override Transform InteractionPoint => airframe;

        protected override void ChooseRestSpot()
        {
            // Both endpoints are actual daisy heads in this flower bed.
            Vector3 current = airframe.localPosition;
            perch = (current - secondBloom).sqrMagnitude < current.sqrMagnitude ? secondBloom : Vector3.zero;
        }

        void Update()
        {
            elapsed = Mathf.Repeat(elapsed + AdvanceAttention(Time.deltaTime), CycleSeconds);
            ApplyPose(elapsed, Attention);
            FaceObserver();
        }

        public void PoseAt(float seconds) => ApplyPose(seconds, 0f);

        void ApplyPose(float seconds, float rest)
        {
            // Start above the flowers, midway through the outward leg.
            float clock = Mathf.Repeat(seconds + 2.25f, CycleSeconds);
            bool returning = clock >= HalfCycle;
            float leg = returning ? clock - HalfCycle : clock;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(leg / LegSeconds));
            Vector3 a = returning ? secondBloom : Vector3.zero;
            Vector3 d = returning ? Vector3.zero : secondBloom;
            Vector3 bend = new Vector3(-.73f, 0f, 1.72f) * (returning ? -1f : 1f);
            Vector3 b = a + bend + Vector3.up * (returning ? 1.03f : 1.48f);
            Vector3 c = d + bend + Vector3.up * 1.18f;
            float u = 1f - t;
            Vector3 p = u*u*u*a + 3f*u*u*t*b + 3f*u*t*t*c + t*t*t*d;
            Vector3 tangent = u*u*(b-a) + 2f*u*t*(c-b) + t*t*(d-c);
            float airborne = Mathf.SmoothStep(0f, 1f, Mathf.Sin(Mathf.PI*t)*3.7f);
            float phase = clock * Mathf.PI * 2f;
            p.y += airborne * (.062f*Mathf.Sin(phase*1.5f) + .032f*Mathf.Sin(phase*2f));
            airframe.localPosition = Vector3.Lerp(p, perch, rest);
            float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
            float bank = airborne * (10f*Mathf.Sin(phase*.5f) - 9f*Mathf.Sin(Mathf.PI*t));
            airframe.localRotation = Quaternion.Euler(-8f * airborne * Mathf.Sin(phase), yaw, bank);

            // Increase the beat just before lift-off while the feet still touch the petals.
            float flightBeat = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LegSeconds - .55f, LegSeconds, leg));
            if (leg > LegSeconds)
                flightBeat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HalfCycle - .45f, HalfCycle, leg));
            float folded = 69f + 5f*Mathf.Sin(phase*.5f);
            float beat = 26f + 43f*Mathf.Sin(phase*4f + .45f*Mathf.Sin(phase*.5f));
            float lift = Mathf.Lerp(Mathf.Lerp(folded, beat, flightBeat), 71f + 4f*Mathf.Sin(Time.time*2.8f), rest);
            leftWing.localRotation = Quaternion.Euler(0f, 0f, -lift);
            rightWing.localRotation = Quaternion.Euler(0f, 0f, lift);
        }
    }
}
