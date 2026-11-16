using UnityEngine;

namespace Jeju.Yard
{
    public sealed class GrazingDeer : YardResidentBehaviour
    {
        public Transform neck;
        public Transform[] legs;
        float clock;
        public override Transform InteractionPoint => transform;

        void Update()
        {
            clock += AdvanceAttention(Time.deltaTime);
            PoseAt(clock);
            FaceObserver();
        }

        public void PoseAt(float seconds)
        {
            float phase = seconds * .25f;
            float graze = Mathf.SmoothStep(0,1,Mathf.Sin(phase)) * (1f - Attention);
            float progress = phase + .84f * Mathf.Cos(phase);
            transform.localPosition = new Vector3(.83f * Mathf.Sin(progress), 0, .62f * Mathf.Cos(progress));
            transform.localRotation = Quaternion.LookRotation(new Vector3(.83f * Mathf.Cos(progress),0,-.62f * Mathf.Sin(progress)));
            neck.localRotation = Quaternion.Euler(72f * graze, 6f * Mathf.Sin(phase * 2),0);
            for (int i=0; i<legs.Length; i++)
            {
                float offset = (i == 0 || i == 3) ? 0 : Mathf.PI;
                legs[i].localRotation = Quaternion.Euler((1f - graze) * (1f - Attention) * 11f * Mathf.Sin(progress * 12f + offset),0,0);
            }
        }

        protected override void ApplyPetPose(float seconds, float strength)
        {
            neck.localRotation *= Quaternion.Euler(-8f * strength,0,6f * Mathf.Sin(seconds * 5f) * strength);
        }
    }
}
