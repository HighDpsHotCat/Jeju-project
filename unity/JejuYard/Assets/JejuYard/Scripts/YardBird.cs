using UnityEngine;

namespace Jeju.Yard
{
    public sealed class YardBird : MonoBehaviour
    {
        public enum Species { Seagull, Sparrow }
        public Species species;
        public Transform flightFrame, torso, head, leftWing, rightWing, feet;
        public Transform leftPrimaries, rightPrimaries;

        static readonly Vector3 HopEnd = new Vector3(1.05f, 0f, -.38f);
        static readonly Vector3 FlightControlA = new Vector3(2.36f, 0f, .25f);
        static readonly Vector3 FlightControlB = new Vector3(-1.15f, 0f, 1.44f);
        float elapsed;

        void Update()
        {
            float cycle = species == Species.Seagull ? 18f : 12f;
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, cycle);
            PoseAt(elapsed);
        }

        public void PoseAt(float seconds)
        {
            if (species == Species.Seagull) Gull(seconds);
            else Sparrow(seconds);
        }

        void Gull(float seconds)
        {
            float a = Mathf.Repeat(seconds, 18f) * (2f * Mathf.PI / 18f);
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            float lift = .28f * Mathf.Sin(2f*a) - .13f * Mathf.Cos(3f*a);
            flightFrame.localPosition = new Vector3(3.45f*s, 2.04f + lift, -1.46f*c);
            var tangent = new Vector3(3.45f*c, .56f*Mathf.Cos(2f*a) + .39f*Mathf.Sin(3f*a), 1.46f*s);
            flightFrame.localRotation = Quaternion.LookRotation(tangent)
                * Quaternion.AngleAxis(6f + 10f*s*s, Vector3.forward);

            // The bird soars through the turns, with a few stronger strokes in the straights.
            float effort = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.25f, .63f, Mathf.Cos(2f*a)));
            float beat = Mathf.Sin(25f*a + .32f);
            Wings(6f + 39f * beat * effort, 4f + 5f * (1f - beat) * effort,
                -3f - 18f * Mathf.Cos(25f*a + .32f) * effort);
            torso.localRotation = Quaternion.Euler(-2f*effort*beat, 0, 0);
            head.localRotation = Quaternion.Euler(6f - 7f*lift, 9f*Mathf.Sin(a), 0);
        }

        void Sparrow(float seconds)
        {
            float time = Mathf.Repeat(seconds, 12f);
            float hopYaw = Yaw(HopEnd);
            float takeoffYaw = Yaw(FlightControlA - HopEnd);
            float landYaw = Yaw(-FlightControlB);
            Vector3 position;
            float yaw, pitch = 0f, roll = 0f, airborne = 0f, peck = 0f;
            if (time < 2.1f)
            {
                float step = time / .7f;
                float within = Mathf.Min(1f, Mathf.Repeat(step, 1f) / .76f);
                float arc = 4f*within*(1f-within);
                position = HopEnd * ((Mathf.Floor(step) + within) / 3f);
                position.y = .18f*arc;
                yaw = hopYaw;
                pitch = 5f*arc;
            }
            else if (time < 5.4f)
            {
                float pause = time - 2.1f;
                position = HopEnd;
                yaw = Mathf.LerpAngle(hopYaw, takeoffYaw, Mathf.SmoothStep(0, 1, (pause-1.6f)/1.4f));
                peck = Peck(pause, .55f) + Peck(pause, 1.2f);
            }
            else if (time < 9.2f)
            {
                float u = (time - 5.4f) / 3.8f;
                float v = 1f-u;
                position = v*v*v*HopEnd + 3f*v*v*u*FlightControlA + 3f*v*u*u*FlightControlB;
                position.y = 1.28f*Mathf.Sin(Mathf.PI*u);
                Vector3 tangent = v*v*(FlightControlA-HopEnd) + 2f*v*u*(FlightControlB-FlightControlA) - u*u*FlightControlB;
                yaw = Yaw(tangent);
                airborne = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f-u) / .105f);
                pitch = -16f * Mathf.Cos(Mathf.PI*u) * airborne;
                roll = 9f*Mathf.Sin(Mathf.PI*u);
            }
            else
            {
                float pause = time - 9.2f;
                position = Vector3.zero;
                yaw = Mathf.LerpAngle(landYaw, hopYaw, Mathf.SmoothStep(0, 1, pause/2.2f));
                peck = Peck(pause, .82f) + Peck(pause, 1.39f);
            }

            flightFrame.localPosition = position;
            flightFrame.localRotation = Quaternion.Euler(0f, yaw, 0f);
            torso.localRotation = Quaternion.Euler(pitch + 33f*peck, 0f, roll);
            head.localRotation = Quaternion.Euler(45f*peck, (1f-peck)*(1f-airborne)*9f*Mathf.Sin(time*Mathf.PI), 0f);
            float beat = Mathf.Sin(time * (2f*Mathf.PI*4f));
            Wings(Mathf.Lerp(-9f, 10f + 48f*beat, airborne), Mathf.Lerp(74f, 9f + 6f*beat, airborne),
                Mathf.Lerp(-10f, -24f * Mathf.Cos(time * (2f*Mathf.PI*4f)), airborne));
            feet.localRotation = Quaternion.Euler(56f*airborne, 0f, 0f);
        }

        static float Peck(float time, float center)
        {
            float t = Mathf.Clamp01(1f - Mathf.Abs(time-center)/.25f);
            return t*t*(3f-2f*t);
        }

        static float Yaw(Vector3 heading) => Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;

        void Wings(float rise, float sweep, float flex)
        {
            rightWing.localRotation = Quaternion.Euler(0, sweep, rise);
            leftWing.localRotation = Quaternion.Euler(0, -sweep, -rise);
            rightPrimaries.localRotation = Quaternion.AngleAxis(flex, Vector3.forward);
            leftPrimaries.localRotation = Quaternion.AngleAxis(-flex, Vector3.forward);
        }
    }
}
