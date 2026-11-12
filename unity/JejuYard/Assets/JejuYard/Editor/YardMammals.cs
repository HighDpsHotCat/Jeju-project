using System;
using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class YardMammals
    {
        static readonly Color Red = Draft.C(0xCB783C), Copper = Draft.C(0xB76C3B), Honey = Draft.C(0xE09B50);
        static readonly Color Gold = Draft.C(0xF2BD5D), Ginger = Draft.C(0xCC8B46), Stripe = Draft.C(0xB7793E);
        static readonly Color Cream = Draft.C(0xFFF0CC), White = Draft.C(0xFAF0D8), Ink = Draft.C(0x413731);
        static readonly Color Pink = Draft.C(0xDE9983), Nose = Draft.C(0xBC7B73);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector4 Ring(float z, float y, float width, float height) => new Vector4(z,y,width,height);

        public static void Build(Transform parent)
        {
            BuildAnimal(parent, false, V(-1.65f,0,-3.25f));
            BuildAnimal(parent, true, V(4.45f,0,.55f));
        }

        static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        static void BuildAnimal(Transform parent, bool cat, Vector3 at)
        {
            var anchor = Pivot(cat ? "Yellow tabby cat" : "Welsh corgi", parent, at);
            var animal = Pivot("Walker", anchor, Vector3.zero).gameObject.AddComponent<QuadrupedRoamer>();
            animal.isCat = cat;
            string prefix = cat ? "YellowTabby" : "WelshCorgi";
            var torso = new Draft();
            var face = new Draft();
            var tail = new Draft();
            if (cat) { CatCoat(torso); CatFace(face); CatTail(tail); }
            else { CorgiCoat(torso); CorgiFace(face); CorgiTail(tail); }

            animal.body = torso.Bake(prefix + "_Coat", animal.transform).transform;
            animal.head = face.Bake(prefix + "_Face", animal.body).transform;
            animal.head.localPosition = cat ? V(0,.59f,.29f) : V(0,.64f,.32f);
            animal.tail = tail.Bake(prefix + "_Tail", animal.body).transform;
            animal.tail.localPosition = cat ? V(0,.46f,-.38f) : V(0,.49f,-.53f);

            animal.legs = new Transform[4];
            animal.hipPositions = new Vector3[4];
            for (int pair = 0; pair < 2; pair++)
            {
                bool front = pair == 0;
                float height = cat ? .35f : .31f;
                var leg = new Draft();
                Leg(leg, cat, front, height);
                var left = leg.Bake(prefix + (front ? "_Foreleg" : "_Hindleg"), animal.transform);
                var right = UnityEngine.Object.Instantiate(left, animal.transform, false);
                right.name = (front ? "Foreleg" : "Hindleg") + " right";
                left.name = (front ? "Foreleg" : "Hindleg") + " left";
                for (int side = 0; side < 2; side++)
                {
                    int index = pair * 2 + side;
                    animal.legs[index] = side == 0 ? left.transform : right.transform;
                    animal.hipPositions[index] = V((side == 0 ? -1 : 1) * (cat ? .125f : .17f), height,
                        cat ? (front ? .245f : -.245f) : (front ? .31f : -.34f));
                }
            }
            animal.PoseAt(0);
        }

        // Successive elliptical sections join into a continuous silhouette from rump to chest or muzzle.
        static void Coat(Draft d, Vector4[] rings, Func<int,float,float,Color> paint, int sides = 16)
        {
            Vector3 Point(Vector4 ring, float angle) => V(ring.z * Mathf.Cos(angle),
                ring.y + ring.w * Mathf.Sin(angle), ring.x);
            for (int i = 0; i < sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides, b = (i + 1) * 2f * Mathf.PI / sides;
                float x = Mathf.Cos((a+b)*.5f), y = Mathf.Sin((a+b)*.5f);
                for (int j = 0; j < rings.Length - 1; j++)
                    d.Quad(Point(rings[j],a), Point(rings[j],b), Point(rings[j+1],b), Point(rings[j+1],a),
                        paint(j,x,y), V(x,y,0));
                Vector4 rear = rings[0], tip = rings[rings.Length - 1];
                d.Tri(V(0,rear.y,rear.x), Point(rear,a), Point(rear,b), paint(0,x,y),Vector3.back);
                d.Tri(V(0,tip.y,tip.x), Point(tip,a), Point(tip,b), paint(rings.Length-2,x,y),Vector3.forward);
            }
        }

        static void CorgiCoat(Draft d)
        {
            Coat(d,new[] { Ring(-.65f,.48f,.095f,.095f), Ring(-.57f,.48f,.24f,.18f),
                Ring(-.39f,.49f,.286f,.242f), Ring(-.12f,.49f,.296f,.235f), Ring(.12f,.50f,.28f,.238f),
                Ring(.24f,.52f,.265f,.252f), Ring(.35f,.54f,.252f,.265f), Ring(.47f,.55f,.19f,.23f),
                Ring(.53f,.57f,.10f,.14f) },
                (i,x,y) => y < -.45f || i >= 5 ? White : y > .60f && i <= 2 ? Copper : y < .14f ? Honey : Red);
        }

        static void CorgiFace(Draft d)
        {
            Coat(d,new[] { Ring(-.205f,.15f,.12f,.15f), Ring(-.125f,.145f,.26f,.24f),
                Ring(.055f,.15f,.30f,.275f), Ring(.185f,.137f,.28f,.26f), Ring(.31f,.10f,.224f,.201f),
                Ring(.38f,.044f,.158f,.135f), Ring(.485f,.036f,.13f,.094f), Ring(.525f,.045f,.09f,.063f) },
                (i,x,y) => i >= 5 || y < -.20f || (i > 0 && y > 0f && Mathf.Abs(x) < (i >= 3 ? .34f : .23f))
                    ? White : y > .72f ? Red : Honey);
            for (int s = -1; s <= 1; s += 2)
            {
                Ear(d, s, false);
                d.Ball(V(s*.20f,.194f,.299f),V(.042f,.054f,.037f),Ink,10,5);
                d.Ball(V(s*.20f-.009f,.213f,.326f),V(.010f,.013f,.011f),White,6,3);
                d.Rod(V(s*.186f,.258f,.275f),V(s*.209f,.249f,.265f),.014f,.009f,Cream,6);
                d.Ball(V(s*.069f,.018f,.439f),V(.106f,.081f,.108f),White,9,4);
                d.Rod(V(s*.108f,-.032f,.496f),V(s*.064f,-.041f,.524f),.006f,.008f,Ink,5);
            }
            d.Ball(V(0,.054f,.533f),V(.078f,.054f,.044f),Ink,9,4);
            d.Ball(V(-.020f,.076f,.563f),V(.021f,.008f,.006f),Draft.C(0x796A58),7,3);
            d.Ball(V(.021f,-.055f,.468f),V(.036f,.038f,.05f),Pink,8,4);
        }

        static void CatCoat(Draft d)
        {
            Coat(d,new[] { Ring(-.49f,.444f,.08f,.10f), Ring(-.415f,.46f,.154f,.185f),
                Ring(-.31f,.468f,.188f,.205f), Ring(-.26f,.465f,.195f,.204f),
                Ring(-.14f,.45f,.18f,.181f), Ring(-.09f,.45f,.177f,.179f),
                Ring(.035f,.45f,.168f,.163f), Ring(.08f,.465f,.169f,.175f),
                Ring(.20f,.478f,.174f,.191f), Ring(.255f,.491f,.175f,.195f),
                Ring(.34f,.515f,.155f,.17f), Ring(.41f,.548f,.09f,.11f) },
                (i,x,y) => y < -.48f ? Cream : (i==2 || i==4 || i==6 || i==8) && y > (i==6 ? .2f : -.22f)
                    ? Stripe : y > .94f ? Ginger : Gold);
        }

        static void CatFace(Draft d)
        {
            Coat(d,new[] { Ring(-.16f,.11f,.112f,.13f), Ring(-.09f,.115f,.185f,.189f),
                Ring(.027f,.11f,.222f,.208f), Ring(.136f,.101f,.214f,.191f),
                Ring(.217f,.092f,.17f,.15f), Ring(.264f,.065f,.101f,.091f) },
                (i,x,y) => y < -.46f ? Cream : i >= 2 && y>.51f && Mathf.Abs(x)>.12f && Mathf.Abs(x)<.39f
                    ? Stripe : Gold);
            for (int s = -1; s <= 1; s += 2)
            {
                Ear(d,s,true);
                d.Ball(V(s*.143f,.145f,.202f),V(.044f,.050f,.031f),Ink,9,4);
                d.Ball(V(s*.142f,.146f,.220f),V(.034f,.038f,.020f),Draft.C(0x91AA75),9,4);
                d.Ball(V(s*.142f,.148f,.238f),V(.012f,.030f,.008f),Ink,8,4);
                d.Ball(V(s*.142f-.009f,.161f,.243f),V(.010f,.010f,.004f),White,6,3);
                d.Ball(V(s*.051f,.049f,.245f),V(.074f,.054f,.05f),Cream,9,4);
                d.Rod(V(s*.187f,.094f,.17f),V(s*.211f,.112f,.119f),.006f,.014f,Stripe,5);
                d.Rod(V(s*.178f,.067f,.161f),V(s*.214f,.075f,.110f),.006f,.011f,Stripe,5);
                for (int k = 0; k < 2; k++)
                    d.Rod(V(s*.076f,.042f+.012f*k,.272f),V(s*(.243f-.018f*k),.036f+.034f*k,.28f-.037f*k),
                        .006f,.002f,Draft.C(0xA87D55),4);
            }
            d.Tri(V(-.037f,.081f,.292f),V(.037f,.081f,.292f),V(0,.048f,.304f),Nose,Vector3.forward);
            d.Rod(V(0,.049f,.299f),V(0,.028f,.288f),.004f,.004f,Ink,5);
        }

        static void Ear(Draft d, int side, bool cat)
        {
            float x = cat ? .134f : .175f, y = cat ? .22f : .284f;
            var center = V(side*x,y,cat ? -.008f : -.016f);
            Vector3 E(float sideways, float high, float front) =>
                center + V(side*sideways*(cat ? .77f : 1f),high*(cat ? .75f : 1f),front);
            var rim = new[] { E(-.10f,-.014f,.072f),E(.102f,-.027f,.056f),E(.152f,.13f,.030f),
                E(.151f,.264f,-.012f),E(.116f,.293f,-.017f),E(-.04f,.165f,.029f) };
            var ridge = E(.039f,.092f,.082f);
            var back = E(.039f,.077f,-.071f);
            var outward = V(side,0,1);
            var color = cat ? Gold : Honey;
            for (int i=0;i<rim.Length;i++)
            {
                int j=(i+1)%rim.Length;
                d.Tri(rim[i],rim[j],ridge,color,outward);
                d.Tri(rim[i],rim[j],back,cat ? Ginger : Copper,(rim[i]+rim[j])*.5f-center+Vector3.back*.08f);
            }
            var baseL=E(-.054f,.022f,.079f); var baseR=E(.074f,.025f,.077f);
            var tip=E(.119f,.239f,.021f); var fold=E(.053f,.10f,.089f);
            d.Tri(baseL,baseR,fold,Pink,outward);
            d.Tri(baseL,fold,tip,Draft.C(0xE6A28C),outward);
            d.Tri(baseR,fold,tip,Draft.C(0xC98876),outward);
        }

        static void Leg(Draft d, bool cat, bool fore, float height)
        {
            float width = cat ? .058f : .084f;
            float ankleZ = fore ? 0f : -.023f;
            var knee = V(0,-height*.50f,fore ? 0f : .041f);
            var ankle = V(0,-height+.052f,ankleZ);
            d.Rod(V(0,.07f,0),knee,width*1.28f,width,cat ? Gold : Honey,8);
            d.Rod(knee,ankle,width,width*.78f,cat ? Gold : White,8);
            if (cat) d.Rod(Vector3.Lerp(knee,ankle,.26f),Vector3.Lerp(knee,ankle,.44f),width*.97f,width*.94f,Ginger,8);
            d.Ball(V(0,-height+.049f,.018f+ankleZ),V(width*1.08f,.049f,cat ? .082f : .104f),cat ? Cream : White,9,4);
        }

        static void CorgiTail(Draft d)
        {
            var bend = V(0,.105f,-.168f);
            d.Rod(V(0,-.018f,.08f),bend,.092f,.082f,Red,9);
            d.Rod(bend,V(0,.173f,-.227f),.082f,.051f,Honey,9);
            d.Rod(V(0,.173f,-.227f),V(0,.24f,-.237f),.051f,.014f,White,9);
        }

        static void CatTail(Draft d)
        {
            var joints = new[] { V(0,0,.07f), V(.006f,.085f,-.13f), V(.022f,.229f,-.226f),
                V(.04f,.35f,-.258f), V(.06f,.439f,-.256f), V(.075f,.525f,-.228f),
                V(.093f,.570f,-.164f), V(.105f,.575f,-.092f) };
            float[] radii = { .068f,.067f,.062f,.059f,.056f,.051f,.046f,.033f };
            for (int i=0;i<joints.Length-1;i++)
            {
                Color tint = i >= 2 && i%2==0 ? Stripe : Gold;
                d.Rod(joints[i],joints[i+1],radii[i],radii[i+1],tint,9);
            }
            d.Ball(joints[joints.Length-1],V(.033f,.033f,.036f),Stripe,8,4);
        }
    }
}
