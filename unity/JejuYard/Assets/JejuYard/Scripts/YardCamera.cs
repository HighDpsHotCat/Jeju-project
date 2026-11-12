using UnityEngine;

namespace Jeju.Yard
{
    [RequireComponent(typeof(Camera))]
    public sealed class YardCamera : MonoBehaviour
    {
        public Transform player;
        Vector3 velocity;
        Camera lens;

        void Start()
        {
            if (Application.isMobilePlatform)
            {
                Application.targetFrameRate = 30;
                QualitySettings.shadowCascades = 0;
                QualitySettings.shadowDistance = 36f;
            }
            Frame(true);
        }
        void LateUpdate() { Frame(false); }

        public void Frame(bool snap)
        {
            if (player == null) return;
            if (lens == null) lens = GetComponent<Camera>();
            lens.orthographic = true;
            float portrait = 1f - Mathf.InverseLerp(.65f, 1.3f, lens.aspect);
            lens.orthographicSize = Mathf.Lerp(5.55f, 7.65f, portrait);
            Quaternion tilt = Quaternion.Euler(34f, 0f, 0f);
            float nearHome = 1f - Mathf.SmoothStep(0f, 1f, player.position.magnitude / 9f);
            Vector3 focus = player.position + new Vector3(-Mathf.Lerp(2.35f, 2.65f, portrait)*nearHome,
                .70f, 1.0f + 1.55f*nearHome);
            focus.x = Mathf.Clamp(focus.x, -11f, 11f);
            focus.z = Mathf.Clamp(focus.z, -11f, 11f);
            Vector3 destination = focus - tilt * Vector3.forward * 28f;
            transform.rotation = tilt;
            transform.position = snap ? destination : Vector3.SmoothDamp(transform.position, destination, ref velocity, .16f);
        }
    }
}
