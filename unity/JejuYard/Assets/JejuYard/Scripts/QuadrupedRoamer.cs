using UnityEngine;

namespace Jeju.Yard
{
    public sealed class QuadrupedRoamer : MonoBehaviour
    {
        public bool isCat;
        public Transform body;
        public Transform head;
        public Transform tail;
        // Front left, front right, back left, back right.
        public Transform[] legs;
        public Vector3[] hipPositions;
        float elapsed;

        void Update()
        {
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, isCat ? 23f : 17f);
            PoseAt(elapsed);
        }

        public void PoseAt(float seconds)
        {
            float period = isCat ? 23f : 17f;
            float walkTime = isCat ? 16f : 13f;
            float t = Mathf.Repeat(seconds, period);
            // Integrating these one-second starts and stops keeps heading continuous at rest.
            float moving = Mathf.Clamp01(Mathf.Min(t, walkTime - t));
            float progress;
            if (t < 1f) progress = .5f * t * t;
            else if (t < walkTime - 1f) progress = t - .5f;
            else progress = walkTime - 1f - .5f * moving * moving;
            progress /= walkTime - 1f;

            float angle = progress * 2f * Mathf.PI + (isCat ? 2.17f : 4.13f);
            float radiusX = isCat ? 1.16f : 1.08f;
            float radiusZ = isCat ? .70f : .83f;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            transform.localPosition = new Vector3(radiusX * cos, 0f, radiusZ * sin);
            transform.localRotation = Quaternion.LookRotation(new Vector3(-radiusX * sin, 0f, radiusZ * cos));

            float gait = progress * (isCat ? 16f : 17f);
            float cycle = gait * 2f * Mathf.PI;
            float breath = Mathf.Sin(2f * Mathf.PI * t * 5f / period);
            body.localPosition = Vector3.up * (.007f * breath + (isCat ? .008f : .018f) * Mathf.Cos(2f * cycle) * moving);
            float glance = Mathf.Sin(2f * Mathf.PI * t * 2f / period);
            head.localRotation = Quaternion.Euler(3f * Mathf.Sin(2f * cycle) * moving + (isCat ? -5f : 8f) * (1f - moving) * breath,
                (isCat ? 22f : 18f) * glance * (1f - .68f * moving), isCat ? 4f * glance : 0f);
            float wag = Mathf.Sin(2f * Mathf.PI * t * (isCat ? 11f : 30f) / period);
            tail.localRotation = Quaternion.Euler(isCat ? 7f * breath : 10f * breath, (isCat ? 20f : 33f) * wag,
                isCat ? 13f * glance : 0f);

            float stepLength = 2f * Mathf.PI * new Vector2(radiusX * sin, radiusZ * cos).magnitude / (isCat ? 16f : 17f);
            for (int i = 0; i < legs.Length; i++)
            {
                // The corgi trots in diagonal pairs. The cat walks with four separate footfalls.
                float offset = (i == 1 || i == 2) ? .5f : 0f;
                if (isCat && i >= 2) offset += .24f;
                PoseLeg(i, Mathf.Repeat(gait + offset, 1f), moving, stepLength);
            }
        }

        void PoseLeg(int index, float phase, float moving, float stepLength)
        {
            const float stance = .56f;
            float swing = Mathf.Clamp01((phase - stance) / (1f - stance));
            float travel = phase < stance ? .5f - phase / stance : Mathf.SmoothStep(-.5f, .5f, swing);
            var hip = hipPositions[index];
            float reach = travel * stepLength * stance * moving;
            float radians = -Mathf.Asin(Mathf.Clamp(reach / hip.y, -.75f, .75f));
            float lift = Mathf.Sin(Mathf.PI * swing) * moving * (isCat ? .079f : .061f);
            // The leg turns about the hip. Compensate for the shortened vertical projection
            // so that the foot stays on the ground during its backwards, weight-bearing stroke.
            hip.y = hip.y * Mathf.Cos(radians) + lift;
            legs[index].SetLocalPositionAndRotation(hip, Quaternion.Euler(radians * Mathf.Rad2Deg, 0, 0));
        }
    }
}
