using UnityEngine;

namespace Jeju.Yard
{
    [DefaultExecutionOrder(-20)]
    public sealed class YardJoystick : MonoBehaviour
    {
        public Vector2 Direction { get; private set; }
        const int NoFinger = -2;
        int finger = NoFinger;
        Vector2 origin, thumb;
        Texture2D disc;
        float Radius => Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .089f, 36f, 96f);

        void Awake()
        {
            // One antialiased disc serves both the base and the thumb, offline.
            disc = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            disc.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(31.6f - new Vector2(x - 31.5f, y - 31.5f).magnitude));
            disc.SetPixels32(pixels);
            disc.Apply(false, true);
        }

        void Update()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (finger == NoFinger && touch.phase == TouchPhase.Began) Begin(touch.position, touch.fingerId);
                    if (finger != touch.fingerId) continue;
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) Clear();
                    else Drag(touch.position);
                }
            }
            else
            {
                if (finger >= 0) Clear();
                if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition, -1);
                if (finger == -1 && Input.GetMouseButton(0)) Drag(Input.mousePosition);
                if (Input.GetMouseButtonUp(0)) Clear();
            }
        }

        void Begin(Vector2 pos, int id)
        {
            if (!Screen.safeArea.Contains(pos) || pos.y > Screen.height * .69f) return;
            finger = id;
            origin = pos;
            thumb = Vector2.zero;
        }

        void Drag(Vector2 pos)
        {
            thumb = Vector2.ClampMagnitude((pos - origin) / Radius, 1f);
            Direction = thumb.magnitude > .09f ? thumb : Vector2.zero;
        }

        void Clear() { finger = NoFinger; thumb = Direction = Vector2.zero; }
        void OnApplicationFocus(bool focus) { if (!focus) Clear(); }
        void OnDisable() => Clear();
        void OnDestroy() { if (disc != null) Destroy(disc); }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || disc == null || (!Input.touchSupported && finger == NoFinger)) return;
            float r = Radius;
            Vector2 p = finger == NoFinger ? Screen.safeArea.min + Vector2.one * (r * 1.6f) : origin;
            p.y = Screen.height - p.y;
            Color previous = GUI.color;
            GUI.color = new Color(.15f, .23f, .16f, finger == NoFinger ? .18f : .32f);
            GUI.DrawTexture(new Rect(p.x-r, p.y-r, 2*r, 2*r), disc);
            Vector2 t = p + new Vector2(thumb.x, -thumb.y) * r;
            GUI.color = new Color(1f, .98f, .88f, .74f);
            GUI.DrawTexture(new Rect(t.x-r*.43f, t.y-r*.43f, r*.86f, r*.86f), disc);
            GUI.color = previous;
        }
    }
}
