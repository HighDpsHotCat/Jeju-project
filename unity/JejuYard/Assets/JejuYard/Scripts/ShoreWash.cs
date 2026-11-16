using UnityEngine;

namespace Jeju.Yard
{
    public sealed class ShoreWash : MonoBehaviour
    {
        Vector3 origin;

        void Awake() => origin = transform.localPosition;

        void Update()
        {
            float swell = Mathf.Sin(Time.time * 1.15f) * .13f;
            transform.localPosition = origin + new Vector3(swell, 0f, -swell);
        }
    }
}
