using UnityEngine;

namespace Jeju.Yard
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class YardWalker : MonoBehaviour
    {
        public const float WalkSpeed = 1.8f;
        public const float WallWalkSeconds = 7f;
        public const float BodyRadius = 0.27f;
        public const float CentreToWallContact = WalkSpeed * WallWalkSeconds;

        public Transform visual, torso, leftLeg, rightLeg, leftArm, rightArm;
        public YardJoystick joystick;
        CharacterController body;
        float phase, stride, verticalSpeed;

        void Update()
        {
            if (YardCollectionPanel.BlocksYardInput) { Step(Vector2.zero, Time.deltaTime); return; }
            Vector2 input = new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
                (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
            if (input == Vector2.zero && joystick != null) input = joystick.Direction;
            Step(input, Time.deltaTime);
        }

        // The editor's four-direction walk check drives the same motor as the player.
        public CollisionFlags Step(Vector2 input, float seconds)
        {
            if (body == null) body = GetComponent<CharacterController>();
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 heading = new Vector3(input.x, 0, input.y);
            Vector3 before = transform.position;
            verticalSpeed = body.isGrounded ? -2f : Mathf.Max(-12f, verticalSpeed - 22f * seconds);
            CollisionFlags flags = body.Move((heading * WalkSpeed + Vector3.up * verticalSpeed) * seconds);
            Vector3 moved = transform.position - before;
            moved.y = 0f;
            if (heading.sqrMagnitude > 0.01f)
                visual.rotation = Quaternion.RotateTowards(visual.rotation, Quaternion.LookRotation(heading), 650f * seconds);
            float travelled = moved.magnitude;
            phase += travelled * 8f;
            stride = Mathf.MoveTowards(stride, Mathf.Clamp01(travelled / Mathf.Max(seconds * WalkSpeed, 0.0001f)), 8f * seconds);
            float swing = Mathf.Sin(phase) * stride;
            leftLeg.localRotation = Quaternion.Euler(25f * swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-25f * swing, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-19f * swing, 0, -6f);
            rightArm.localRotation = Quaternion.Euler(19f * swing, 0, 6f);
            torso.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * stride * .038f);
            return flags;
        }
    }
}
