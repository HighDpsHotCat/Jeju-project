using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Rand = System.Random;

namespace Jeju.Yard.EditorArt
{
    public static class Coast
    {
        public const float WaterLevel = -.30f;
        const float ShoreDistance = YardWalker.CentreToWallContact + YardWalker.BodyRadius;
        const float Bend = 7.6f;
        const float Radius = ShoreDistance - Bend;
        static readonly float ArcLength = Radius * Mathf.PI / 2f;
        static readonly Vector3 Up = Vector3.up;
        static readonly Section[] Shore = Trace();

        struct Section
        {
            public float station, width;
            public Vector3 edge, outward;

            public Vector3 Point(float offset, float y)
            {
                return edge + outward * offset + Up * y;
            }

            public Vector3 Sand(float offset)
            {
                return Point(offset, Mathf.Lerp(0f, WaterLevel, 1f + offset / width));
            }
        }

        // A single continuous profile drives the coloured mesh, the walking surface and the surf line.
        static Section At(float station)
        {
            Vector3 edge, outward;
            if (station <= Bend)
            {
                edge = new Vector3(station, 0f, -ShoreDistance);
                outward = Vector3.back;
            }
            else if (station < Bend + ArcLength)
            {
                float angle = (station - Bend) / Radius;
                outward = new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle));
                edge = new Vector3(Bend, 0f, -Bend) + outward * Radius;
            }
            else
            {
                edge = new Vector3(ShoreDistance, 0f, station - Bend - ArcLength - Bend);
                outward = Vector3.right;
            }
            // Keep the four points where the walking axes and wall ends meet the beach straight.
            float along = Mathf.Min(Mathf.Abs(edge.x), Mathf.Abs(edge.z));
            float swell = Mathf.SmoothStep(0f, 1f, (along - 1.3f) / 2.8f)
                * Mathf.SmoothStep(0f, 1f, (12.35f - along) / 2f);
            edge += outward * (swell * (.25f * Mathf.Sin(.69f * station) + .20f * Mathf.Sin(.35f * station + .9f)));
            float variation = .38f * Mathf.Sin(station * .46f) + .23f * Mathf.Sin(station * .93f + 1f);
            return new Section { station = station, edge = edge, outward = outward, width = 2.82f + swell * variation };
        }

        static Section[] Trace()
        {
            var profile = new List<Section> { At(-80f) };
            float end = Bend * 2f + ArcLength + 20f;
            int segments = Mathf.CeilToInt((end + 20f) / .55f);
            for (int i = 0; i <= segments; i++)
                profile.Add(At(Mathf.Lerp(-20f, end, (float)i / segments)));
            profile.Add(At(Bend * 2f + ArcLength + 80f));
            return profile.ToArray();
        }

        static float Offset(Vector3 p, out float width)
        {
            p.y = 0f;
            float nearest = float.MaxValue;
            float offset = 0f;
            width = 0f;
            for (int i = 1; i < Shore.Length; i++)
            {
                Section a = Shore[i - 1], b = Shore[i];
                Vector3 segment = b.edge - a.edge;
                float t = Mathf.Clamp01(Vector3.Dot(p - a.edge, segment) / segment.sqrMagnitude);
                Vector3 delta = p - (a.edge + t * segment);
                if (delta.sqrMagnitude >= nearest) continue;
                nearest = delta.sqrMagnitude;
                offset = Vector3.Dot(delta, Vector3.Lerp(a.outward, b.outward, t));
                width = Mathf.Lerp(a.width, b.width, t);
            }
            return offset;
        }

        public static bool OnLawn(Vector3 p, float margin)
        {
            float offset = Offset(p, out float width);
            return offset < -width - margin;
        }

        public static float SandHeight(Vector3 p)
        {
            float offset = Offset(p, out float width);
            return Mathf.Lerp(0f, WaterLevel, 1f + offset / width);
        }

        static void LandStrip(Draft visible, Draft collider, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color)
        {
            visible.Quad(a, b, c, d, Draft.C(color), Up);
            collider.Quad(a, b, c, d, Color.white, Up);
        }

        public static void Build(Transform root, Draft grass, Color lawn)
        {
            var shore = new Draft();
            var sea = new Draft();
            var ripples = new Draft();
            var collision = new Draft();
            var rng = new Rand(3621);
            Vector3 farCorner = new Vector3(-80f, 0f, 80f);
            for (int i = 1; i < Shore.Length; i++)
            {
                Section a = Shore[i - 1], b = Shore[i];
                Vector3 turfA = a.Sand(-a.width), turfB = b.Sand(-b.width);
                grass.Tri(Vector3.zero, turfA, turfB, lawn, Up);
                collision.Tri(Vector3.zero, turfA, turfB, Color.white, Up);

                float Dune(Section s) => -s.width + .22f + .10f * Mathf.Sin(s.station * 1.8f);
                float Wet(Section s) => -.74f - .16f * Mathf.Sin(s.station * .82f);
                LandStrip(shore, collision, turfA, turfB, b.Sand(Dune(b)), a.Sand(Dune(a)), 0xC3BB84);
                LandStrip(shore, collision, a.Sand(Dune(a)), b.Sand(Dune(b)), b.Sand(Wet(b)), a.Sand(Wet(a)), 0xE9CF9B);
                LandStrip(shore, collision, a.Sand(Wet(a)), b.Sand(Wet(b)), b.Sand(0f), a.Sand(0f), 0xD9BC8F);

                // The invisible face only meets the walker after they cross all the dry and wet sand.
                collision.Quad(a.Point(0f, -1f), b.Point(0f, -1f), b.Point(0f, 1.3f), a.Point(0f, 1.3f),
                    Color.white, -a.outward - b.outward);

                WaterStrip(sea, a, b);
            }
            Vector3 first = Shore[0].Sand(-Shore[0].width);
            Vector3 last = Shore[Shore.Length - 1].Sand(-Shore[Shore.Length - 1].width);
            grass.Quad(Vector3.zero, last, farCorner, first, lawn, Up);
            collision.Quad(Vector3.zero, last, farCorner, first, Color.white, Up);
            // Close the northwest quarter with two triangles sharing the far corner.
            // The open water continues past both distant ends of the visible shoreline.
            sea.Quad(new Vector3(-110f, WaterLevel - .025f, -110f), new Vector3(110f, WaterLevel - .025f, -110f),
                new Vector3(110f, WaterLevel - .025f, 110f), new Vector3(-110f, WaterLevel - .025f, 110f),
                Draft.C(0x4E9FB4), Up);

            DressBeach(shore, ripples, rng);
            shore.Bake("BasaltBeach", root);
            var water = sea.Bake("CoastalWater", root);
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var foam = ripples.Bake("ShoreRipples", root);
            foam.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            foam.AddComponent<ShoreWash>();
            var floor = collision.Bake("BeachWalkingSurface", root);
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(floor.GetComponent<MeshFilter>());
        }

        static readonly float[] DepthBands = { 0f, .48f, 1.3f, 2.6f, 4.3f, 6.6f, 9.8f, 14f, 20f, 28f };
        static readonly int[] WaterColors = { 0xA5D7C9, 0x96D3C7, 0x85CDC6, 0x76C4C3, 0x68BDBF,
            0x60B5BC, 0x58ADB9, 0x53A6B7, 0x4E9FB4 };

        static float Offshore(Section s, float depth)
        {
            float strength = Mathf.Min(depth * .13f, .75f);
            return depth + strength * (Mathf.Sin(s.station * .37f + depth * .12f) + .35f * Mathf.Sin(s.station * .78f));
        }

        static void WaterStrip(Draft sea, Section a, Section b)
        {
            for (int j = 0; j < WaterColors.Length; j++)
            {
                float near = DepthBands[j], far = DepthBands[j + 1];
                sea.Quad(a.Point(Offshore(a, near), WaterLevel), b.Point(Offshore(b, near), WaterLevel),
                    b.Point(Offshore(b, far), WaterLevel), a.Point(Offshore(a, far), WaterLevel), Draft.C(WaterColors[j]), Up);
            }
        }

        static float Range(Rand rng, float lo, float hi) => Mathf.Lerp(lo, hi, (float)rng.NextDouble());

        static void DressBeach(Draft shore, Draft surf, Rand rng)
        {
            float end = Bend * 2f + ArcLength + 18f;
            for (float s = -18f; s < end; s += Range(rng, .26f, .64f))
            {
                Section section = At(s);
                float offset = Range(rng, -section.width + .25f, -.2f);
                var p = section.Sand(offset);
                Vector3 along = Vector3.Cross(section.outward, Up);
                float size = Range(rng, .038f, .11f);
                shore.Quad(p - along * size, p + Up * .012f + section.outward * size * .30f, p + along * size,
                    p - section.outward * size * .36f + Up * .006f, Draft.C(rng.Next(3) == 0 ? 0xCEB284 : 0xF2DDAF), Up);
            }
            foreach (float s in new[] {-11.3f, -8.8f, -3.2f, 2.8f, 6.6f, 12.6f, 17.8f, 24.4f, 29.8f, 34.7f})
            {
                Section section = At(s);
                float offset = Range(rng, -2f, -.65f);
                FanShell(shore, section.Sand(offset) + Up * .016f, section.outward, Range(rng, .13f, .20f));
            }
            foreach (float s in new[] {-13.0f, -7.3f, 5f, 13.5f, 21.2f, 31.5f, 36.4f})
            {
                int count = rng.Next(3, 6);
                for (int j = 0; j < count; j++)
                {
                    Section section = At(s + Range(rng, -.7f, .7f));
                    float offset = Range(rng, -.63f, .55f);
                    float size = Range(rng, .095f, .30f);
                    Vector3 p = section.Sand(offset) - Up * .035f;
                    shore.Ball(p, new Vector3(size * 1.35f, size * .48f, size),
                        Draft.C(j % 2 == 0 ? 0x7F8982 : 0x909A8F), 7, 3);
                }
            }
            for (float s = -23f; s < end + 5f;)
            {
                float length = Range(rng, 1.0f, 3.4f);
                Crest(surf, s, length, Range(rng, .09f, .26f), Range(rng, .046f, .09f), 0xE1F2DA);
                s += length + Range(rng, .2f, 1.1f);
            }
            for (int i = 0; i < 155; i++)
            {
                float depth = Range(rng, .65f, 14f);
                float s = Range(rng, -26f, end + 8f);
                int pigment = depth < 2.6f ? 0xB5E3D6 : depth < 7f ? 0x99D8D1 : 0x84C8CE;
                Crest(surf, s, Range(rng, .35f, 1.8f), depth, Range(rng, .018f, .046f), pigment);
            }
        }

        static void Crest(Draft surf, float start, float length, float offset, float width, int color)
        {
            const int pieces = 6;
            Vector3 left = Vector3.zero, right = Vector3.zero;
            for (int i = 0; i <= pieces; i++)
            {
                float t = (float)i / pieces;
                Section section = At(start + t * length);
                float arch = Mathf.Sin(Mathf.PI * t);
                var p = section.Point(offset + .11f * arch, WaterLevel + .013f);
                Vector3 side = section.outward * (width * arch);
                if (i > 0) surf.Quad(left, p - side, p + side, right, Draft.C(color), Up);
                left = p - side;
                right = p + side;
            }
        }

        static void FanShell(Draft mesh, Vector3 p, Vector3 facing, float size)
        {
            Vector3 across = Vector3.Cross(Up, facing);
            Vector3 hinge = p - facing * (size * .46f);
            Vector3 tip = p + Up * (size * .39f);
            Vector3 previous = hinge;
            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.Lerp(-1.16f, 1.16f, i / 10f);
                float length = size * (i % 2 == 0 ? 1f : .91f);
                Vector3 rim = hinge + (facing * Mathf.Cos(angle) + across * Mathf.Sin(angle)) * length + Up * .012f;
                int shade = i % 2 == 0 ? 0xF5DBC1 : 0xDBAB92;
                mesh.Tri(previous, rim, tip, Draft.C(shade), Up);
                previous = rim;
            }
            mesh.Tri(previous, hinge, tip, Draft.C(0xEDC9B0), Up);
        }
    }
}
