using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class Dolphin
    {
        static readonly Color Back = Draft.C(0x66869B);
        static readonly Color Flank = Draft.C(0x879FA9);
        static readonly Color Belly = Draft.C(0xD2DCD4);
        static readonly Color Fin = Draft.C(0x526F84);
        static readonly Color Eye = Draft.C(0x273F49);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static DolphinVisitor Build(Transform parent)
        {
            var bay = new GameObject("Dolphin visit").transform;
            bay.SetParent(parent, false);
            var visitor = new GameObject("Indo-Pacific bottlenose dolphin").AddComponent<DolphinVisitor>();
            visitor.transform.SetParent(bay, false);
            visitor.waterLevel = Coast.WaterLevel;

            var body = new Draft();
            // Each cross-section follows the back, melon and the small step to the beak.
            Body(body, new[] {
                Section(-.94f, -.07f, .095f, .10f), Section(-.73f, -.03f, .16f, .17f),
                Section(-.44f, .005f, .25f, .245f), Section(-.02f, .03f, .32f, .31f),
                Section(.33f, .045f, .345f, .325f), Section(.59f, .07f, .31f, .30f),
                Section(.76f, .085f, .26f, .26f), Section(.89f, .065f, .195f, .195f),
                Section(.955f, .005f, .14f, .105f), Section(1.16f, -.008f, .115f, .075f),
                Section(1.265f, .005f, .055f, .045f), Section(1.28f, .01f, .018f, .018f)
            });
            // A swept dorsal fin, two tapered pectorals, and the paired horizontal flukes.
            FinFaces(body, new[] { V(0,.23f,.24f), V(0,.44f,.065f), V(0,.62f,-.055f),
                V(0,.76f,-.245f), V(0,.75f,-.29f), V(0,.54f,-.24f), V(0,.38f,-.29f), V(0,.195f,-.45f) },
                V(0,.43f,-.12f), Vector3.right * .048f, Back, Fin);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                FinFaces(body, new[] { V(sign*.20f,-.06f,.49f), V(sign*.42f,-.13f,.35f),
                    V(sign*.61f,-.22f,.10f), V(sign*.70f,-.28f,-.16f), V(sign*.68f,-.29f,-.21f),
                    V(sign*.49f,-.23f,-.12f), V(sign*.21f,-.14f,.11f) },
                    V(sign*.43f,-.165f,.14f), Vector3.up * .034f, Fin, Flank);
                body.Ball(V(sign*.303f,.083f,.58f), V(.040f,.052f,.048f), Eye, 8, 4);
                body.Ball(V(sign*.339f,.10f,.595f), V(.009f,.014f,.013f), Draft.C(0xF4F0D8), 6, 3);
                body.Rod(V(sign*.131f,-.007f,.975f), V(sign*.106f,-.018f,1.165f), .009f, .007f, Fin, 4);
            }
            body.Bake("BottlenoseDolphin", visitor.transform);

            var tail = new Draft();
            Body(tail, new[] { Section(-.49f,.005f,.074f,.06f), Section(-.38f,-.012f,.066f,.079f),
                Section(-.14f,-.024f,.087f,.098f), Section(.10f,.003f,.12f,.124f) });
            for (int sign = -1; sign <= 1; sign += 2)
            {
                FinFaces(tail, new[] { V(0,0,-.33f), V(sign*.19f,.025f,-.34f),
                    V(sign*.40f,.04f,-.42f), V(sign*.61f,.045f,-.60f), V(sign*.65f,.035f,-.70f),
                    V(sign*.50f,.012f,-.66f), V(sign*.28f,0,-.60f), V(sign*.10f,-.012f,-.635f),
                    V(0,-.016f,-.56f) }, V(sign*.25f,.01f,-.49f), Vector3.up * .045f, Back, Flank);
            }
            visitor.tail = tail.Bake("BottlenoseFlukes", visitor.transform).transform;
            visitor.tail.localPosition = V(0,-.063f,-.87f);
            var foam = new Draft();
            for (int sign = -1; sign <= 1; sign += 2)
            {
                WakeArc(foam, sign, .66f, .9f, .18f, Draft.C(0xD6F3E9));
                WakeArc(foam, sign, .93f, .77f, -.42f, Draft.C(0xA6E2D9));
            }
            visitor.wake = foam.Bake("DolphinWake", bay).GetComponent<MeshRenderer>();
            visitor.wake.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visitor.wake.receiveShadows = false;
            visitor.PoseAt(0f);
            return visitor;
        }

        static void WakeArc(Draft draft, int sign, float width, float depth, float offset, Color color)
        {
            Vector3 lastInner = Vector3.zero, lastOuter = Vector3.zero;
            const int segments = 7;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = Mathf.Lerp(.45f, 2.07f, t);
                Vector3 radial = V(sign * Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 center = V(width * radial.x, 0, depth * radial.z + offset);
                float halfWidth = .033f * Mathf.Sin(t * Mathf.PI);
                Vector3 inner = center - radial * halfWidth, outer = center + radial * halfWidth;
                if (i > 0) draft.Quad(lastInner, lastOuter, outer, inner, color, Vector3.up);
                lastInner = inner;
                lastOuter = outer;
            }
        }

        // z, central height and the two radii describe an elliptical cross-section.
        static Vector4 Section(float z, float y, float width, float height) => new Vector4(z, y, width, height);

        static void Body(Draft draft, Vector4[] sections)
        {
            const int sides = 12;
            Vector3 Point(Vector4 s, float angle) => V(s.z * Mathf.Cos(angle), s.y + s.w * Mathf.Sin(angle), s.x);
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides, b = (i + 1) * 2f * Mathf.PI / sides;
                float mid = (a + b) * .5f;
                Vector3 normal = V(Mathf.Cos(mid), Mathf.Sin(mid), 0);
                Color color = normal.y < -.48f ? Belly : normal.y < .15f ? Flank : Back;
                for (int j = 1; j < sections.Length; j++)
                {
                    var prev = sections[j - 1];
                    var next = sections[j];
                    draft.Quad(Point(prev,a), Point(prev,b), Point(next,b), Point(next,a), color, normal);
                }
                var rear = sections[0];
                var nose = sections[sections.Length - 1];
                draft.Tri(V(0,rear.y,rear.x), Point(rear,a), Point(rear,b), color, Vector3.back);
                draft.Tri(V(0,nose.y,nose.x), Point(nose,a), Point(nose,b), color, Vector3.forward);
            }
        }

        static void FinFaces(Draft draft, Vector3[] edge, Vector3 center, Vector3 thickness, Color top, Color bottom)
        {
            for (int i = 0; i < edge.Length; i++)
            {
                Vector3 a = edge[i], b = edge[(i + 1) % edge.Length];
                draft.Tri(a, b, center + thickness, top, thickness);
                draft.Tri(a, b, center - thickness, bottom, -thickness);
            }
        }
    }
}
