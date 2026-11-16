using UnityEngine;

namespace Jeju.Yard
{
    // The imported FBX pieces retain their joint origins and vertex-painted coats.
    public sealed class YardWildlifeResident : YardResidentBehaviour
    {
        [SerializeField] Transform frame;
        [SerializeField] Transform[] limbs;
        [SerializeField] Quaternion[] rest;
        [SerializeField] bool bird;
        [SerializeField] float altitude;
        float clock;

        public override Transform InteractionPoint => frame;

        public void Configure(Transform visual, string key)
        {
            frame = visual;
            bird = key == "mallard" || key == "crow" || key == "swallow";
            altitude = key == "swallow" ? .9f : key == "crow" ? .45f : 0f;
            string[] joints = bird ? new[] { "WingLeft", "WingRight" } :
                new[] { "ForeLeft", "ForeRight", "HindLeft", "HindRight", "Tail" };
            limbs = new Transform[joints.Length];
            rest = new Quaternion[joints.Length];
            foreach (var part in visual.GetComponentsInChildren<MeshRenderer>(true))
            {
                int index = System.Array.IndexOf(joints, part.name);
                if (index < 0) continue;
                limbs[index] = part.transform;
                rest[index] = part.transform.localRotation;
            }
            clock = Mathf.Abs(transform.localPosition.x);
            Pose();
        }

        void Update()
        {
            clock += AdvanceAttention(Time.deltaTime);
            Pose();
            FaceObserver();
        }

        void Pose()
        {
            float t = clock * (altitude > 0 ? .85f : .48f);
            frame.localPosition = new Vector3(.55f * Mathf.Sin(t), altitude + .016f * (1 + Mathf.Cos(clock * 8)),
                .38f * Mathf.Cos(t));
            frame.localRotation = Quaternion.LookRotation(new Vector3(.55f * Mathf.Cos(t), 0, -.38f * Mathf.Sin(t)));
            float swing = Mathf.Sin(clock * (altitude > 0 ? 9f : 6f)) * (1f - Attention);
            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i] == null) continue;
                bool tail = !bird && i == 4;
                var axis = bird ? Vector3.forward : tail ? Vector3.up : Vector3.right;
                float sign = i == 1 || i == 2 ? -1f : 1f;
                float angle = bird ? (altitude > 0 ? 39f : 7f) : 20f;
                var jointAxis = limbs[i].parent.InverseTransformDirection(frame.TransformDirection(axis));
                limbs[i].localRotation = Quaternion.AngleAxis(sign * swing * angle, jointAxis) * rest[i];
            }
        }

        protected override void ApplyPetPose(float elapsedSeconds, float strength)
        {
            frame.localPosition += Vector3.up * (.075f * strength * Mathf.Abs(Mathf.Sin(elapsedSeconds * 8)));
        }
    }
}
