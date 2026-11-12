using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class Explorer
    {
        static readonly Color Skin = Draft.C(0xFFCE9E);
        static readonly Color Hair = Draft.C(0x503931);
        static readonly Color Ink = Draft.C(0x322A27);
        static readonly Color Olive = Draft.C(0x879657);
        static readonly Color LeafShadow = Draft.C(0x647747);
        static readonly Color Linen = Draft.C(0xF7E7BC);
        static readonly Color Boot = Draft.C(0x995C39);
        static readonly Color Leather = Draft.C(0x735138);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static YardWalker Build()
        {
            var player = new GameObject("Tangerine explorer");
            player.transform.position = Vector3.up * .01f;
            var capsule = player.AddComponent<CharacterController>();
            capsule.radius = YardWalker.BodyRadius;
            capsule.height = 1.69f;
            capsule.center = Vector3.up * (capsule.height * .5f);
            capsule.skinWidth = .01f;
            capsule.minMoveDistance = 0;
            capsule.stepOffset = .16f;
            var walker = player.AddComponent<YardWalker>();
            walker.joystick = player.AddComponent<YardJoystick>();
            walker.visual = Pivot("Facing", player.transform, Vector3.zero);
            walker.visual.localRotation = Quaternion.Euler(0, 180, 0);
            walker.torso = Pivot("Walking bounce", walker.visual, Vector3.zero);
            walker.leftLeg = Leg(walker.visual, -1);
            walker.rightLeg = Leg(walker.visual, 1);
            walker.leftArm = Arm(walker.torso, -1);
            walker.rightArm = Arm(walker.torso, 1);

            var body = new Draft();
            Jacket(body);
            Head(body);
            SunHat(body);
            body.Bake("Explorer_Coat_and_sunhat", walker.torso);
            return walker;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        static Transform Leg(Transform parent, int side)
        {
            var joint = Pivot(side < 0 ? "Left hip" : "Right hip", parent, V(.112f * side, .51f, 0));
            var leg = new Draft();
            leg.Ball(V(0,-.115f,0), V(.108f,.184f,.122f), Draft.C(0xA59D70), 9, 4);
            leg.Rod(V(0,-.29f,0), V(0,-.17f,0), .081f, .092f, Draft.C(0x8C895E), 8);
            leg.Rod(V(0,-.335f,0), V(0,-.267f,0), .084f, .088f, Linen, 8);
            leg.Rod(V(0,-.445f,-.012f), V(0,-.319f,-.012f), .093f, .094f, Boot, 9);
            leg.Ball(V(0,-.429f,.039f), V(.105f,.081f,.151f), Boot, 10, 4);
            leg.Ball(V(0,-.472f,.040f), V(.109f,.038f,.155f), Draft.C(0x51453B), 10, 4);
            leg.Rod(V(-.040f,-.365f,.067f), V(.040f,-.355f,.067f), .009f,.009f,Linen,4);
            leg.Bake(side < 0 ? "Explorer_Boot_L" : "Explorer_Boot_R", joint);
            return joint;
        }

        static Transform Arm(Transform torso, int side)
        {
            var joint = Pivot(side < 0 ? "Left shoulder" : "Right shoulder", torso, V(side*.210f,.868f,0));
            var arm = new Draft();
            arm.Ball(V(side*.008f,-.036f,0),V(.079f,.096f,.091f),Olive,8,4);
            var elbow = V(side*.046f,-.173f,.020f);
            arm.Rod(V(0,-.04f,0),elbow,.074f,.067f,Olive,8);
            arm.Rod(elbow,V(side*.046f,-.206f,.032f),.073f,.068f,Draft.C(0xC4BB83),8);
            arm.Rod(V(side*.045f,-.198f,.028f),V(side*.04f,-.271f,.059f),.05f,.048f,Skin,7);
            arm.Ball(V(side*.034f,-.269f,.063f),V(.061f,.068f,.060f),Skin,8,4);
            arm.Bake(side < 0 ? "Explorer_Sleeve_L" : "Explorer_Sleeve_R", joint);
            return joint;
        }

        static void Jacket(Draft d)
        {
            RingBody(d, new[] { V(.223f,.454f,.158f), V(.255f,.491f,.180f), V(.230f,.765f,.167f), V(.203f,.879f,.147f), V(.107f,.931f,.10f) }, Olive, 12);
            d.Quad(V(-.065f,.477f,.174f),V(.065f,.477f,.174f),V(.068f,.870f,.150f),V(-.068f,.870f,.150f),Linen,Vector3.forward);
            d.Rod(V(0,.884f,0),V(0,1.06f,0),.087f,.083f,Skin,9);
            // Folded neckerchief, twin pack straps, and a little stitched field pocket.
            d.Tri(V(-.125f,.902f,.130f),V(.112f,.902f,.132f),V(.030f,.797f,.164f),Draft.C(0xECA64C),Vector3.forward);
            d.Tri(V(-.169f,.892f,.102f),V(-.07f,.928f,.100f),V(-.081f,.833f,.163f),LeafShadow,Vector3.forward);
            d.Tri(V(.169f,.892f,.102f),V(.07f,.928f,.100f),V(.081f,.833f,.163f),LeafShadow,Vector3.forward);
            d.Ball(V(0,.709f,-.155f), V(.193f,.204f,.105f), Draft.C(0xB88956), 10, 5);
            d.Ball(V(0,.787f,-.199f), V(.198f,.084f,.075f), Draft.C(0xC79962), 10, 4);
            d.Box(V(0,.702f,-.245f),V(.052f,.090f,.025f),Leather);
            d.Box(V(0,.675f,-.261f),V(.047f,.024f,.014f),Draft.C(0xEFC875));
            for(int side=-1; side<=1; side+=2)
            {
                float x=side*.139f;
                d.Rod(V(x,.881f,-.108f),V(x,.918f,.023f),.026f,.026f,Leather,6);
                d.Rod(V(x,.918f,.023f),V(x,.863f,.125f),.026f,.026f,Leather,6);
                d.Rod(V(x,.863f,.125f),V(x*1.19f,.655f,.129f),.026f,.021f,Leather,6);
                d.Quad(V(x-.036f,.583f,.143f),V(x+.036f,.583f,.143f),V(x+.038f,.632f,.143f),V(x-.038f,.632f,.143f),LeafShadow,Vector3.forward);
            }
        }

        // Each ring stores horizontal radii in x/z and its height in y.
        static void RingBody(Draft d, Vector3[] rings, Color color, int sides)
        {
            for (int band=1; band<rings.Length; band++)
                for(int i=0; i<sides; i++)
                {
                    float a=i*2*Mathf.PI/sides, b=(i+1)*2*Mathf.PI/sides;
                    var low=rings[band-1]; var high=rings[band];
                    d.Quad(V(low.x*Mathf.Sin(a),low.y,low.z*Mathf.Cos(a)), V(low.x*Mathf.Sin(b),low.y,low.z*Mathf.Cos(b)),
                           V(high.x*Mathf.Sin(b),high.y,high.z*Mathf.Cos(b)), V(high.x*Mathf.Sin(a),high.y,high.z*Mathf.Cos(a)),
                           color,V(Mathf.Sin((a+b)*.5f),0,Mathf.Cos((a+b)*.5f)));
                }
        }

        static void Head(Draft d)
        {
            const float y=1.242f;
            d.Ball(V(0,y,.012f), V(.329f,.312f,.292f), Skin, 16, 8);
            for(int side=-1;side<=1;side+=2)
            {
                d.Ball(V(.306f*side,y-.026f,.002f),V(.063f,.087f,.058f),Skin,8,4);
                d.Ball(V(.321f*side,y-.024f,.040f),V(.033f,.042f,.016f),Draft.C(0xE5A182),7,3);
                float x=.126f*side;
                d.Ball(V(x,y+.031f,.273f), V(.039f,.051f,.026f), Ink,10,5);
                d.Ball(V(x-.009f,y+.051f,.294f), V(.012f,.012f,.009f), Draft.C(0xFFF6E6),7,3);
                d.Rod(V(x-side*.030f,y+.107f,.260f),V(x+side*.033f,y+.112f,.243f),.009f,.010f,Hair,5);
                d.Ball(V(.204f*side,y-.052f,.230f),V(.042f,.020f,.012f),Draft.C(0xEFAC8D),8,3);
            }
            d.Ball(V(0,y-.028f,.296f),V(.032f,.028f,.030f),Draft.C(0xEEAE83),8,4);
            var smile = Draft.C(0x9D6049);
            d.Rod(V(-.05f,y-.094f,.283f),V(0,y-.106f,.281f),.007f,.009f,smile,5);
            d.Rod(V(0,y-.106f,.281f),V(.048f,y-.091f,.283f),.009f,.006f,smile,5);

            // A single side-swept hair shell avoids individual expensive strands.
            const int cuts=24;
            for(int i=0;i<cuts;i++)
            {
                float a=i*2*Mathf.PI/cuts,b=(i+1)*2*Mathf.PI/cuts;
                Vector3 outer=V(Mathf.Sin((a+b)/2),.18f,Mathf.Cos((a+b)/2));
                for(int k=0;k<4;k++)
                    d.Quad(HairPoint(a,k*.25f),HairPoint(b,k*.25f),HairPoint(b,(k+1)*.25f),HairPoint(a,(k+1)*.25f),Hair,outer);
            }
            d.Ball(V(-.118f,1.095f,-.270f),V(.117f,.116f,.104f),Draft.C(0x39312A),9,4);
            d.Ball(V(-.144f,1.024f,-.307f),V(.130f,.111f,.112f),Hair,10,5);
        }

        static Vector3 HairPoint(float a,float t)
        {
            float front=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.79f,Mathf.Cos(a)));
            float edge=Mathf.Lerp(-.67f,.52f,front) + .07f*Mathf.Sin(a*3f+.6f)
                       - front*.20f*Mathf.Pow(Mathf.Max(0,Mathf.Cos(a*4f+.8f)),5);
            float angle=Mathf.Lerp(Mathf.Asin(Mathf.Clamp(edge,-1,1)),1.56f,t);
            return V(.342f*Mathf.Cos(angle)*Mathf.Sin(a),1.244f+.326f*Mathf.Sin(angle),.010f+.307f*Mathf.Cos(angle)*Mathf.Cos(a));
        }

        static void SunHat(Draft d)
        {
            // Hollow, upturned front brim leaves the eyes visible from the raised game camera.
            var rings = new[]{V(.292f,1.458f,.271f),V(.408f,1.445f,.370f),V(.416f,1.468f,.376f),
                V(.315f,1.505f,.282f),V(.288f,1.545f,.259f),V(.250f,1.649f,.237f),
                V(.183f,1.704f,.179f),V(.103f,1.715f,.107f)};
            var colors = new[]{0xB76D34,0xEC9D4C,0xEAA055,0xC48747,0xE99F52,0xEEAE64,0xF0B66D};
            const int n=16;
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                Vector3 radial=V(Mathf.Sin((a+b)*.5f),0,Mathf.Cos((a+b)*.5f));
                for(int j=1;j<rings.Length;j++)
                {
                    Vector3 outDir=j==1 ? Vector3.down : (j>=3 ? radial+Vector3.up : radial);
                    d.Quad(HatPoint(rings[j-1],a),HatPoint(rings[j-1],b),HatPoint(rings[j],b),HatPoint(rings[j],a),
                        Draft.C(colors[j-1]),outDir);
                }
                d.Tri(HatPoint(rings[7],a),HatPoint(rings[7],b),V(0,1.725f,-.009f),Draft.C(0xF3BB72),Vector3.up);
                Vector3 low=V(.310f,1.508f,.280f),high=V(.293f,1.546f,.268f);
                d.Quad(HatPoint(low,a),HatPoint(low,b),HatPoint(high,b),HatPoint(high,a),Draft.C(0x926640),radial);
            }
            // A small citrus pin gives the hat a recognisable island detail.
            d.Ball(V(-.222f,1.550f,.183f),V(.034f,.033f,.029f),Draft.C(0xFAAC39),8,4);
            d.Tri(V(-.222f,1.579f,.182f),V(-.257f,1.610f,.178f),V(-.246f,1.577f,.208f),Draft.C(0x6B9555),Vector3.forward);
        }

        static Vector3 HatPoint(Vector3 r,float a)
        {
            float x=Mathf.Sin(a)*r.x,z=Mathf.Cos(a)*r.z;
            return V(x,r.y+.090f*z-.035f*x,z-.009f);
        }
    }
}
