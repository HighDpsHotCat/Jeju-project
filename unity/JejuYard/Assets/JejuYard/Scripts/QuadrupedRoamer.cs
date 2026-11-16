using UnityEngine;

namespace Jeju.Yard
{
    public sealed class QuadrupedRoamer : YardResidentBehaviour
    {
        public const float CorgiHipHeight = .21f;
        public const float CatCycleSeconds = 48f;
        const float CatWalkStart = 39.8f;
        const float CatWalkTime = 7f;
        const float PostureChange = .8f;
        const float CorgiCycleSeconds = 17f;
        static readonly Vector3 StandingPelvis = new Vector3(0, .46f, -.32f);
        static readonly Vector3 SittingPelvis = new Vector3(0, .215f, -.20f);
        static readonly Vector3 CorgiPelvis = new Vector3(0, .49f, -.36f);
        static readonly Vector3 CorgiSeatedPelvis = new Vector3(0, .268f, -.31f);

        public bool isCat;
        public Transform body, head, tail;
        // Front left, front right, back left, back right.
        public Transform[] legs;
        public Vector3[] hipPositions;
        public Transform[] hocks, hindPaws;
        float elapsed;
        public override Transform InteractionPoint => transform;

        // Instantiate copies this reference into portraits so even a clone made while the
        // live dog sits can recover the authored standing mesh with PoseAt(0).
        [SerializeField, HideInInspector] Mesh standingHindMesh;
        Mesh foldedHindMesh;
        MeshFilter leftHind, rightHind;
        Vector3[] standingVertices, foldedVertices, blendedVertices;
        float previousFold = -1f;

        void Update()
        {
            elapsed = Mathf.Repeat(elapsed + AdvanceAttention(Time.deltaTime), isCat ? CatCycleSeconds : CorgiCycleSeconds);
            ApplyPose(elapsed, Attention);
            FaceObserver();
        }

        public void PoseAt(float seconds) => ApplyPose(seconds, 0f);

        protected override void ApplyPetPose(float elapsedSeconds, float strength)
        {
            if (isCat)
            {
                // Press one cheek into the stroke while staying seated.
                float nuzzle = Mathf.Sin(elapsedSeconds * 5.6f);
                head.localRotation *= Quaternion.Euler(-10f * strength, 7f * nuzzle * strength,
                    (15f + 7f * nuzzle) * strength);
                tail.localRotation *= Quaternion.Euler(0f, 11f * nuzzle * strength, 0f);
            }
            else
            {
                float wag = Mathf.Sin(elapsedSeconds * 21f);
                head.localRotation *= Quaternion.Euler(-14f * strength, 0f, 5f * wag * strength);
                tail.localRotation *= Quaternion.Euler(6f * strength, 32f * wag * strength, 0f);
            }
        }

        void ApplyPose(float seconds, float rest)
        {
            float period = isCat ? CatCycleSeconds : CorgiCycleSeconds;
            float t = Mathf.Repeat(seconds, period);
            float walkTime = isCat ? CatWalkTime : 13f;
            float walkClock = isCat ? t - CatWalkStart : t;
            float seat = isCat ? Mathf.SmoothStep(0, 1, Mathf.Max(-walkClock, walkClock - walkTime) / PostureChange) : 0f;
            seat = Mathf.Lerp(seat, 1f, rest);
            float walk = Mathf.Clamp(walkClock, 0, walkTime);
            float moving = Mathf.Clamp01(Mathf.Min(walk, walkTime - walk));
            float progress;
            if (walk < 1f) progress = .5f * walk * walk;
            else if (walk < walkTime - 1f) progress = walk - .5f;
            else progress = walkTime - 1f - .5f * moving * moving;
            progress /= walkTime - 1f;
            moving *= 1f - rest;

            float angle = progress * 2f * Mathf.PI + (isCat ? 2.17f : 4.13f);
            float radiusX = isCat ? .78f : 1.08f;
            float radiusZ = isCat ? .45f : .83f;
            float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            transform.localPosition = new Vector3(radiusX * cos, 0f, radiusZ * sin);
            transform.localRotation = Quaternion.LookRotation(new Vector3(-radiusX * sin, 0f, radiusZ * cos));

            int steps = isCat ? 12 : 20;
            float gait = progress * steps;
            float cycle = gait * 2f * Mathf.PI;
            float breath = Mathf.Sin(2f * Mathf.PI * t * (isCat ? 12f : 5f) / period);
            float glance = Mathf.Sin(2f * Mathf.PI * t * (isCat ? 5f : 2f) / period);
            float wag = Mathf.Sin(2f * Mathf.PI * t * (isCat ? 17f : 30f) / period);
            breath = Mathf.Lerp(breath, Mathf.Sin(Time.time * 2.5f), rest);
            glance *= 1f - rest;
            wag = Mathf.Lerp(wag, Mathf.Sin(Time.time * (isCat ? 2f : 8f)), rest);
            float bob = .005f * breath + (isCat ? .008f : .013f) * Mathf.Cos(2f * cycle) * moving;
            var bodyTilt = Quaternion.Euler((isCat ? -50f : -26f) * seat, 0, 0);
            // The pelvis settles onto the grass as the chest pivots up over the front paws.
            var bodyOffset = isCat ? Vector3.Lerp(StandingPelvis, SittingPelvis, seat) - bodyTilt * StandingPelvis
                : Vector3.Lerp(CorgiPelvis + Vector3.up * (CorgiHipHeight - .31f), CorgiSeatedPelvis, seat) - bodyTilt * CorgiPelvis;
            body.SetLocalPositionAndRotation(bodyOffset + Vector3.up * bob, bodyTilt);
            var look = Quaternion.Euler(3f * Mathf.Sin(2f * cycle) * moving + (isCat ? -3f : 8f) * (1f - moving) * breath,
                (isCat ? 15f : 18f) * glance * (1f - .68f * moving), isCat ? 3f * glance : 0f);
            head.localRotation = Quaternion.Inverse(bodyTilt) * look;
            var tailWag = Quaternion.Euler(isCat ? 7f * breath : 10f * breath, (isCat ? 20f : 33f) * wag,
                isCat ? 13f * glance : 0f);
            var restingTail = Quaternion.Euler(0, -66f + 4f * wag, -90f);
            tail.localRotation = Quaternion.Inverse(bodyTilt) * Quaternion.Slerp(tailWag, restingTail, isCat ? seat : 0f);

            float stepLength = 2f * Mathf.PI * new Vector2(radiusX * sin, radiusZ * cos).magnitude / steps;
            for (int i = 0; i < legs.Length; i++)
            {
                float offset = (i == 1 || i == 2) ? .5f : 0f;
                if (isCat && i >= 2) offset += .24f;
                PoseLeg(i, Mathf.Repeat(gait + offset, 1f), moving, stepLength, seat);
            }
            if (!isCat) FoldCorgiHindLegs(seat);
        }

        void PoseLeg(int index, float phase, float moving, float stepLength, float seat)
        {
            const float stance = .56f;
            float swing = Mathf.Clamp01((phase - stance) / (1f - stance));
            float travel = phase < stance ? .5f - phase / stance : Mathf.SmoothStep(-.5f, .5f, swing);
            var hip = hipPositions[index];
            float reach = travel * stepLength * stance * moving;
            float strideAngle = -Mathf.Asin(Mathf.Clamp(reach / hip.y, -.75f, .75f)) * Mathf.Rad2Deg;
            float lift = Mathf.Sin(Mathf.PI * swing) * moving * (isCat ? .079f : .043f);
            if (isCat && index >= 2 && hocks != null && hocks.Length == 2)
            {
                PoseHindLeg(index - 2, hip, strideAngle, lift, seat);
                return;
            }

            var rotation = Quaternion.Euler(strideAngle, 0, 0);
            float length = index < 2 ? 1f + (isCat ? .44f : .56f) * seat : 1f;
            hip.y = hip.y * length * Mathf.Cos(strideAngle * Mathf.Deg2Rad) + lift;
            float seatedZ = isCat ? (index < 2 ? .155f : -.18f) : (index < 2 ? .26f : -.30f);
            hip.z = Mathf.Lerp(hip.z, seatedZ, seat);
            if (!isCat && index >= 2) hip.x += Mathf.Sign(hip.x) * .025f * seat;
            legs[index].localScale = new Vector3(1, length, 1);
            legs[index].SetLocalPositionAndRotation(hip, rotation);
        }

        void FoldCorgiHindLegs(float fold)
        {
            if ((fold == 0f && standingHindMesh == null) || Mathf.Approximately(fold, previousFold)) return;
            if (leftHind == null)
            {
                leftHind = legs[2].GetComponent<MeshFilter>();
                rightHind = legs[3].GetComponent<MeshFilter>();
                if (standingHindMesh == null) standingHindMesh = leftHind.sharedMesh;
            }
            previousFold = fold;
            if (fold == 0f)
            {
                leftHind.sharedMesh = rightHind.sharedMesh = standingHindMesh;
                return;
            }
            // The short-legged prefab has each rear leg in one mesh. Fold the knee
            // forward and lower the thigh, keeping the pads flat on the ground.
            if (foldedHindMesh == null)
            {
                foldedHindMesh = Instantiate(standingHindMesh);
                foldedHindMesh.MarkDynamic();
                standingVertices = standingHindMesh.vertices;
                foldedVertices = new Vector3[standingVertices.Length];
                blendedVertices = new Vector3[standingVertices.Length];
                for (int i = 0; i < standingVertices.Length; i++)
                {
                    Vector3 p = standingVertices[i];
                    float height = p.y + CorgiHipHeight;
                    float shin = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.09f, .145f, height));
                    float thigh = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.145f, .255f, height));
                    p.y -= .020f * shin + .085f * thigh;
                    p.z += .07f + .070f * shin - .15f * thigh;
                    foldedVertices[i] = p;
                }
            }
            for (int i = 0; i < blendedVertices.Length; i++)
                blendedVertices[i] = Vector3.Lerp(standingVertices[i], foldedVertices[i], fold);
            foldedHindMesh.vertices = blendedVertices;
            foldedHindMesh.RecalculateNormals();
            foldedHindMesh.RecalculateBounds();
            leftHind.sharedMesh = rightHind.sharedMesh = foldedHindMesh;
        }

        void OnDestroy()
        {
            if (foldedHindMesh != null) Destroy(foldedHindMesh);
        }

        void PoseHindLeg(int side, Vector3 hip, float strideAngle, float lift, float seat)
        {
            var thigh = legs[side + 2];
            var hock = hocks[side];
            var paw = hindPaws[side];
            // The knee goes forwards and the heel folds back, leaving the rear paw flat.
            var thighTilt = Quaternion.Euler(Mathf.Lerp(strideAngle, -66f, seat), 0, 0);
            var shinTilt = Quaternion.Euler(Mathf.Lerp(strideAngle, -12f, seat), 0, 0);
            float restingOffset = hip.y + hock.localPosition.y + paw.localPosition.y;
            hip.y = restingOffset - (thighTilt * hock.localPosition).y - (shinTilt * paw.localPosition).y + lift;
            hip.z = Mathf.Lerp(hip.z, -.20f, seat);
            hip.x += (side == 0 ? -.050f : .050f) * seat;
            thigh.SetLocalPositionAndRotation(hip, thighTilt);
            hock.localRotation = Quaternion.Inverse(thighTilt) * shinTilt;
            paw.localRotation = Quaternion.Inverse(shinTilt);
        }
    }
}
