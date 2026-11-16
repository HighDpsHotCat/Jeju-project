using UnityEngine;

namespace Jeju.Yard
{
    public sealed class DolphinVisitor : YardResidentBehaviour
    {
        public Transform tail;
        public MeshRenderer wake;
        public float waterLevel;
        public const float VisitPeriod = 45f;

        const float Reach = 9f;
        const float Nearshore = 16.2f;
        const float BendRadius = 2f;
        const float BendCenter = Nearshore - BendRadius;
        const float StraightLength = Reach + BendCenter;
        const float BendLength = BendRadius * Mathf.PI / 2f;
        const float RouteLength = 2f * StraightLength + BendLength;
        static readonly float StartAngle = Mathf.Acos(1f - 2f * Reach / RouteLength);
        float elapsed;
        public override Transform InteractionPoint => transform;

        void Update()
        {
            elapsed = Mathf.Repeat(elapsed + AdvanceAttention(Time.deltaTime), VisitPeriod);
            ApplyPose(elapsed, Attention, true);
        }

        public void PoseAt(float seconds) => ApplyPose(seconds, 0f, false);

        protected override void ApplyPetPose(float elapsedSeconds, float strength)
        {
            float beat = Mathf.Sin(elapsedSeconds * 10f);
            transform.localRotation *= Quaternion.Euler((-11f - 3f * beat) * strength, 0f,
                6f * strength);
            tail.localRotation *= Quaternion.Euler(18f * beat * strength, 0f, 0f);
        }

        void ApplyPose(float seconds, float rest, bool followObserver)
        {
            Vector3 point = SwimPosition(seconds);
            Vector3 tangent = SwimPosition(seconds + .035f) - SwimPosition(seconds - .035f);
            point.y = Mathf.Lerp(point.y, waterLevel - .12f + .035f * Mathf.Sin(Time.time * 1.7f), rest);
            transform.localPosition = point;
            transform.localRotation = Quaternion.LookRotation(tangent, Vector3.up);
            if (followObserver) FaceObserver();
            float stroke = Mathf.Lerp(15f * Mathf.Sin(seconds * 2f * Mathf.PI * 37f / VisitPeriod),
                5f * Mathf.Sin(Time.time * 2.3f), rest);
            tail.localRotation = Quaternion.Euler(stroke, 0, 0);

            // Body and wake are siblings; both follow the bay, but only the swimmer pitches.
            Vector3 trail = SwimPosition(seconds - .28f);
            trail = Vector3.Lerp(trail, point - transform.localRotation * Vector3.forward * .26f, rest);
            float visibility = Mathf.InverseLerp(-.83f, -.24f, trail.y - waterLevel);
            wake.enabled = visibility > .025f;
            if (wake.enabled)
            {
                Vector3 heading = point - trail;
                heading.y = 0;
                trail.y = waterLevel + .018f;
                wake.transform.localPosition = trail;
                wake.transform.localRotation = Quaternion.LookRotation(heading, Vector3.up);
                float spread = Mathf.SmoothStep(0f, 1f, visibility) * (1f + .23f * Mathf.Max(0f, point.y - waterLevel));
                spread *= 1f - .72f * rest;
                wake.transform.localScale = new Vector3(spread * Mathf.Abs(transform.localScale.x), 1,
                    spread * Mathf.Abs(transform.localScale.z));
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (wake != null) wake.enabled = false;
        }

        Vector3 SwimPosition(float seconds)
        {
            // Follow both beaches on the inshore leg, then circle back in deeper water.
            float angle = seconds * 2f * Mathf.PI / VisitPeriod + StartAngle;
            float across = Mathf.Sin(angle);
            float along = RouteLength * .5f * (1f - Mathf.Cos(angle));
            float offshore = 1.05f * (1f - across);
            Vector3 point;
            if (along <= StraightLength)
                point = new Vector3(Nearshore + offshore, 0, Reach - along);
            else if (along < StraightLength + BendLength)
            {
                float bend = (along - StraightLength) / BendRadius;
                float radius = BendRadius + offshore;
                point = new Vector3(BendCenter + radius * Mathf.Cos(bend), 0, -BendCenter - radius * Mathf.Sin(bend));
            }
            else
                point = new Vector3(BendCenter - (along - StraightLength - BendLength), 0, -Nearshore - offshore);

            // The visitor breaches off each beach and slips underwater at both turns.
            float fromBreach = Mathf.Min(Mathf.Abs(along - Reach), Mathf.Abs(RouteLength - Reach - along));
            float leap = Mathf.SmoothStep(1f, 0f, fromBreach / 3.3f);
            float dive = Mathf.SmoothStep(1f, 0f, Mathf.Abs(across) / .5f);
            float bob = .10f * Mathf.Cos(seconds * 2f * Mathf.PI * 9f / VisitPeriod);
            point.y = waterLevel - .23f + bob + 1.13f * leap - 1.05f * dive;
            return point;
        }
    }
}
