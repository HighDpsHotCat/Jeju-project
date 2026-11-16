using UnityEngine;

namespace Jeju.Yard
{
    public abstract class YardResidentBehaviour : MonoBehaviour
    {
        public abstract Transform InteractionPoint { get; }

        Transform observer;
        Quaternion facing, lastRotation;
        float transition;
        bool turning;
        protected float Attention { get; private set; }
        float petRemaining, petStrength, petElapsed;

        public void PlayPetReaction()
        {
            if (isActiveAndEnabled) petRemaining = 1.25f;
        }

        protected abstract void ApplyPetPose(float elapsedSeconds, float strength);

        // Each Update rebuilds the underlying pose. Add the touch motion after it,
        // even when looking at the player has stopped the animal's travel clock.
        protected void LateUpdate()
        {
            if (petRemaining <= 0f && petStrength <= 0f) return;
            float dt = Time.deltaTime;
            petRemaining = Mathf.Max(0f, petRemaining - dt);
            float target = Mathf.SmoothStep(0f, 1f, petRemaining / .5f);
            petStrength = Mathf.MoveTowards(petStrength, target, dt * 7f);
            if (petStrength <= 0f)
            {
                petElapsed = 0f;
                return;
            }
            petElapsed += dt;
            ApplyPetPose(petElapsed, petStrength);
        }

        public void SetObserver(Transform value)
        {
            if (observer == value) return;
            if (value != null && transition <= 0f)
            {
                facing = lastRotation = InteractionPoint.localRotation;
                turning = true;
                ChooseRestSpot();
            }
            observer = value;
        }

        protected virtual void ChooseRestSpot() { }

        // The travel clock eases to a stop and resumes from the same place on its route.
        protected float AdvanceAttention(float deltaTime)
        {
            transition = Mathf.MoveTowards(transition, observer != null ? 1f : 0f,
                deltaTime * (observer != null ? .86f : .72f));
            Attention = Mathf.SmoothStep(0f, 1f, transition);
            return deltaTime * (1f - Attention);
        }

        protected void FaceObserver() => FaceObserver(Vector3.up);

        protected void FaceObserver(Vector3 up)
        {
            if (!turning) return;
            Transform frame = InteractionPoint;
            if (observer != null)
            {
                Vector3 toward = Vector3.ProjectOnPlane(observer.position - frame.position, up);
                if (toward.sqrMagnitude > .0001f)
                {
                    Quaternion desired = Quaternion.LookRotation(toward, up);
                    if (frame.parent != null) desired = Quaternion.Inverse(frame.parent.rotation) * desired;
                    facing = Quaternion.RotateTowards(facing, desired, 180f * Time.deltaTime);
                }
            }
            // When two headings cross 180°, Slerp's shortest arc can change sides.
            // Limit the actual frame-to-frame turn, including the return to the route.
            var target = Quaternion.Slerp(frame.localRotation, facing, Attention);
            lastRotation = Quaternion.RotateTowards(lastRotation, target, 210f * Time.deltaTime);
            frame.localRotation = lastRotation;
            if (Attention == 0f && observer == null && Quaternion.Angle(lastRotation, target) < .05f)
                turning = false;
        }

        protected virtual void OnDisable()
        {
            observer = null;
            turning = false;
            transition = Attention = 0f;
            petRemaining = petStrength = petElapsed = 0f;
        }
    }
}
