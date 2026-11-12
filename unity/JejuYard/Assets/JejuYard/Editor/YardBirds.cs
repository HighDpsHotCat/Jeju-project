using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class YardBirds
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Color C(int hex) => Draft.C(hex);

        public static void Build(Transform parent)
        {
            BuildBird(parent, true);
            BuildBird(parent, false);
        }

        static void BuildBird(Transform parent, bool gull)
        {
            string name = gull ? "Seagull" : "Tree sparrow";
            string asset = gull ? "Seagull" : "TreeSparrow";
            var anchor = Joint(name, parent, gull ? V(2.8f, 0, -12.25f) : V(.55f, 0, 2.45f));
            var bird = anchor.gameObject.AddComponent<YardBird>();
            bird.species = gull ? YardBird.Species.Seagull : YardBird.Species.Sparrow;
            bird.flightFrame = Joint("Flight path", anchor, Vector3.zero);
            bird.torso = Joint("Breast", bird.flightFrame, gull ? Vector3.zero : V(0,.235f,0));

            var plumage = new Draft();
            Ellipsoid(plumage, Vector3.zero,
                gull ? V(.175f,.187f,.415f) : V(.153f,.155f,.222f),
                gull ? 0xFBF6E7 : 0xE7C9A1, gull ? 0xBCBEC3 : 0x947051, .39f);
            Tail(plumage, gull);
            if (gull)
            {
                for (int side=-1; side<=1; side+=2)
                {
                    var heel = V(side*.071f,-.123f,-.13f);
                    var toes = V(side*.083f,-.112f,-.35f);
                    plumage.Rod(heel, toes, .020f, .012f, C(0xB68A58), 5);
                    plumage.Tri(toes, toes+V(-.035f,-.005f,-.073f), toes+V(.035f,-.005f,-.073f),
                        C(0xDAA45C), Vector3.down);
                }
            }
            plumage.Bake(asset+"Body", bird.torso);
            bird.head = Joint("Head pivot", bird.torso, gull ? V(0,.075f,.233f) : V(0,.087f,.107f));
            Head(gull).Bake(asset+"Head", bird.head);

            for (int side=-1; side<=1; side+=2)
            {
                Vector3 socket = gull ? V(side*.105f,.079f,.035f) : V(side*.093f,.063f,.047f);
                BuildWing(bird, asset, socket, side, gull);
            }
            if (!gull)
            {
                var legs = new Draft();
                for (int side=-1; side<=1; side+=2)
                {
                    var ankle = V(side*.058f,-.130f,.024f);
                    legs.Rod(V(side*.056f,0,-.010f), ankle, .013f, .009f, C(0xAB7965), 5);
                    for (int toe=-1; toe<=1; toe++)
                        legs.Rod(ankle, ankle+V(toe*.028f,-.004f,.052f-.012f*Mathf.Abs(toe)),
                            .007f,.002f,C(0x9D6F5F),4);
                    legs.Rod(ankle, ankle+V(0,-.003f,-.033f),.006f,.002f,C(0x9D6F5F),4);
                }
                bird.feet = legs.Bake(asset+"Feet", bird.flightFrame).transform;
                bird.feet.localPosition = V(0,.14f,-.025f);
            }
            bird.PoseAt(0);
        }

        static Transform Joint(string name, Transform parent, Vector3 offset)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = offset;
            return t;
        }

        static void Ellipsoid(Draft draft, Vector3 center, Vector3 radii, int lower, int upper, float capHeight)
        {
            const int around=12, rows=7;
            Vector3 Point(int level, int index)
            {
                float latitude = Mathf.PI * (level/(float)rows - .5f);
                float angle = 2f*Mathf.PI*(index%around)/around;
                return center + Vector3.Scale(radii, V(Mathf.Cos(latitude)*Mathf.Cos(angle),
                    Mathf.Sin(latitude), Mathf.Cos(latitude)*Mathf.Sin(angle)));
            }
            for (int j=0; j<rows; j++)
            {
                Color paint = C(Mathf.Sin(Mathf.PI*((j+.5f)/rows-.5f)) >= capHeight ? upper : lower);
                for (int i=0; i<around; i++)
                {
                    Vector3 a=Point(j,i), b=Point(j,i+1), c=Point(j+1,i+1), d=Point(j+1,i);
                    draft.Quad(a,b,c,d,paint,(a+b+c+d)/4f-center);
                }
            }
        }

        static Draft Head(bool gull)
        {
            var d = new Draft();
            var center = gull ? V(0,.07f,.075f) : V(0,.059f,.042f);
            var size = gull ? V(.143f,.148f,.167f) : V(.126f,.132f,.137f);
            Ellipsoid(d, center, size, gull ? 0xF6F2E5 : 0xEDDBC1, gull ? 0xFFFBED : 0x905232,
                gull ? .21f : .18f);
            if (!gull)
            {
                d.Ball(V(0,-.005f,.11f), V(.085f,.077f,.070f), C(0x48382E), 9, 4);
                // A white cheek and dark cheek spot are distinctive markings of a tree sparrow.
                for (int s=-1; s<=1; s+=2)
                {
                    d.Ball(V(s*.096f,.034f,.057f), V(.040f,.060f,.077f), C(0xF8E9CC), 10, 5);
                    d.Ball(V(s*.129f,.035f,.050f), V(.015f,.028f,.034f), C(0x50392F), 8, 4);
                }
            }
            for (int s=-1; s<=1; s+=2)
            {
                Vector3 eye = gull ? V(s*.122f,.102f,.141f) : V(s*.103f,.078f,.118f);
                float r = gull ? .031f : .024f;
                d.Ball(eye,V(r*.64f,r,r),C(gull ? 0xD5B65E : 0x6B4F3E),10,5);
                eye.x += s*r*.30f;
                d.Ball(eye,V(r*.60f,r*.81f,r*.81f),C(0x302C30),10,5);
                d.Ball(eye+V(s*r*.46f,.006f,.005f), V(.004f,.0055f,.0055f), C(0xFFFBF1),6,3);
            }

            Vector3 basePoint = gull ? V(0,.057f,.198f) : V(0,.049f,.15f);
            float w = gull ? .061f : .050f;
            float length = gull ? .197f : .100f;
            float h = gull ? .049f : .051f;
            Vector3 a = basePoint - Vector3.right*w, b = basePoint + Vector3.right*w;
            Vector3 upper = basePoint + V(0,h,0), lower = basePoint - V(0,h*.65f,0);
            Vector3 tip = basePoint + V(0,-h*.23f,length);
            d.Tri(a,upper,tip,C(gull ? 0xE9B23F : 0x4C4541),Vector3.up);
            d.Tri(upper,b,tip,C(gull ? 0xF2BE4A : 0x5A5148),Vector3.up);
            d.Tri(b,lower,tip,C(gull ? 0xD79A39 : 0x796350),Vector3.down);
            d.Tri(lower,a,tip,C(gull ? 0xC38C37 : 0x665749),Vector3.down);
            if (gull)
                for (int s=-1; s<=1; s+=2)
                    d.Ball(basePoint+V(s*.020f,-.014f,.119f),V(.007f,.009f,.021f),C(0xBB653E),7,3);
            return d;
        }

        static void Tail(Draft d, bool gull)
        {
            float scale = gull ? 1f : .63f;
            for (int i=-2; i<=2; i++)
            {
                float side=Mathf.Abs(i);
                var root=V(i*.021f,.015f,-.22f)*scale;
                var end=V(i*.075f,.083f-side*.006f,-.70f+side*.037f)*scale;
                Vector3 p=root;
                float oldWidth=.025f*scale;
                float[] stops={.35f,.66f,.84f,1f};
                for (int j=0; j<stops.Length; j++)
                {
                    float t=stops[j];
                    Vector3 next=Vector3.Lerp(root,end,t);
                    float width=(j==3 ? .035f : .049f)*scale;
                    Color color=C(gull ? (j==2 ? 0x4D5360 : 0xE6E4DB) : (i%2==0 ? 0x76543E : 0x8F6B4E));
                    Vector3 left=p-Vector3.right*oldWidth, right=p+Vector3.right*oldWidth;
                    Vector3 nextLeft=next-Vector3.right*width, nextRight=next+Vector3.right*width;
                    var ridge=Vector3.up*.009f*scale;
                    d.Quad(left,p+ridge,next+ridge,nextLeft,color,Vector3.up);
                    d.Quad(p+ridge,right,nextRight,next+ridge,color,Vector3.up);
                    d.Quad(left,right,nextRight,nextLeft,color,Vector3.down);
                    p=next; oldWidth=width;
                }
            }
        }

        // The long elbow and tapered black primaries form the gull's silhouette. The sparrow
        // uses shorter, rounder stations and two buff bars across the chestnut coverts.
        static void BuildWing(YardBird bird, string asset, Vector3 socket, int side, bool gull)
        {
            float[] span = gull ? new[]{0f,.22f,.49f,.68f,.82f,.95f,1.08f} : new[]{0f,.11f,.22f,.32f,.39f,.44f,.48f};
            float[] leading = gull ? new[]{.08f,.15f,.21f,.18f,.09f,-.04f,-.19f} : new[]{.04f,.10f,.13f,.11f,.06f,0f,-.075f};
            float[] trailing = gull ? new[]{-.24f,-.27f,-.21f,-.15f,-.21f,-.23f,-.19f} : new[]{-.18f,-.19f,-.16f,-.14f,-.11f,-.10f,-.075f};
            float[] arc = gull ? new[]{0f,.055f,.11f,.093f,.07f,.043f,0f} : new[]{0f,.032f,.053f,.050f,.040f,.026f,.008f};
            float[] lanes = {0f,.26f,.35f,.63f,.73f,1f};
            const int wrist = 2;
            var inner = new Draft();
            var outer = new Draft();
            Vector3 hinge = V(side*span[wrist], arc[wrist], 0f);
            Vector3 Point(int station, float u, bool top)
            {
                float chord = leading[station]-trailing[station];
                float y = arc[station] + chord*(top ? .12f*Mathf.Sin(Mathf.PI*u) : -.09f*Mathf.Sin(Mathf.PI*u));
                return V(side*span[station], y, Mathf.Lerp(leading[station],trailing[station],u));
            }
            for (int i=0; i<span.Length-1; i++)
                for (int j=0; j<lanes.Length-1; j++)
                {
                    Draft d = i<wrist ? inner : outer;
                    Vector3 origin = i<wrist ? Vector3.zero : hinge;
                    int pigment;
                    if (gull)
                        pigment = i>=4 ? (i==5 && j==2 ? 0xE0DED3 : 0x475261) :
                            j==4 ? 0xEAEBDF : j<2 ? 0xB6BFCA : 0xA1AEBD;
                    else
                        pigment = (i<4 && (j==1 || j==3)) ? 0xE5C297 :
                            j==4 || i>=4 ? 0x755743 : j<2 ? 0xBD8A59 : 0x946444;
                    float start=lanes[j], end=lanes[j+1];
                    d.Quad(Point(i,start,true)-origin,Point(i+1,start,true)-origin,Point(i+1,end,true)-origin,Point(i,end,true)-origin,C(pigment),Vector3.up);
                    d.Quad(Point(i,start,false)-origin,Point(i+1,start,false)-origin,Point(i+1,end,false)-origin,Point(i,end,false)-origin,
                        C(gull ? (i>=4 ? 0x6D7780 : 0xE2E3D8) : 0xBA976E), Vector3.down);
                }
            string wingName = asset+(side < 0 ? "LeftWing" : "RightWing");
            Transform wing = inner.Bake(wingName,bird.torso).transform;
            wing.localPosition = socket;
            Transform tip = outer.Bake(wingName+"Primaries",wing).transform;
            tip.localPosition = hinge;
            if (side<0) { bird.leftWing=wing; bird.leftPrimaries=tip; }
            else { bird.rightWing=wing; bird.rightPrimaries=tip; }
        }
    }
}
