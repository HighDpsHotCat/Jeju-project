using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class YardButterfly
    {
        static readonly Color Ink = Draft.C(0x383932);
        static readonly Color Honey = Draft.C(0xF0C65F);
        static readonly Color Sun = Draft.C(0xFFE39A);
        static readonly Color Blue = Draft.C(0x99C8BE);
        static readonly Color Orange = Draft.C(0xED925B);
        static Vector2 P(float x, float z) => new Vector2(x, z);

        public static ButterflyFlight Build(Transform parent, bool cabbageWhite = false)
        {
            var butterfly = new GameObject(cabbageWhite ? "Small white" : "Asian swallowtail").AddComponent<ButterflyFlight>();
            butterfly.transform.SetParent(parent, false);
            // Perches on two existing daisy heads in the east flower bed.
            butterfly.transform.localPosition = cabbageWhite ? new Vector3(3.35f,.38f,4.18f) : new Vector3(2.635f, .367f, 3.554f);
            var frame = new GameObject("Flutter").transform;
            frame.SetParent(butterfly.transform, false);
            butterfly.airframe = frame;
            Body().Bake(cabbageWhite ? "SmallWhiteBody" : "SwallowtailBody", frame);
            if (cabbageWhite) butterfly.transform.localScale = Vector3.one * .78f;
            var right = cabbageWhite ? WhiteWing().Bake("SmallWhiteWing",frame) : Wing().Bake("SwallowtailWing",frame);
            right.transform.localPosition = new Vector3(.013f, .010f, 0f);
            right.name = "Right wings";
            var left = Object.Instantiate(right, frame);
            left.name = "Left wings";
            left.transform.localPosition = new Vector3(-.013f, .010f, 0f);
            left.transform.localScale = new Vector3(-1f, 1f, 1f);
            butterfly.leftWing = left.transform;
            butterfly.rightWing = right.transform;
            butterfly.PoseAt(0f);
            return butterfly;
        }

        static Draft Body()
        {
            var mesh = new Draft();
            mesh.Rod(new Vector3(0,0,-.123f),new Vector3(0,.007f,-.048f),.008f,.020f,Ink,7);
            mesh.Rod(new Vector3(0,.007f,-.048f),new Vector3(0,.012f,.037f),.020f,.023f,Ink,7);
            mesh.Ball(new Vector3(0,.014f,.039f),new Vector3(.030f,.029f,.049f),Ink,8,4);
            mesh.Ball(new Vector3(0,.023f,.083f),new Vector3(.026f,.025f,.026f),Ink,8,3);
            for (int side = -1; side <= 1; side += 2)
            {
                mesh.Rod(new Vector3(side*.013f,.024f,-.079f),new Vector3(side*.018f,.029f,.047f),
                    .0032f,.005f,Honey,4);
                var brow = new Vector3(side*.011f,.038f,.091f);
                var elbow = new Vector3(side*.035f,.067f,.118f);
                var tip = new Vector3(side*.062f,.074f,.150f);
                mesh.Rod(brow,elbow,.003f,.0024f,Ink,4);
                mesh.Rod(elbow,tip,.0024f,.0022f,Ink,4);
                mesh.Ball(tip,new Vector3(.0048f,.0045f,.008f),Ink,6,3);
                for (int i=0; i<3; i++)
                {
                    float z = .036f - i*.022f;
                    var knee = new Vector3(side*.033f,-.015f,z+.009f);
                    mesh.Rod(new Vector3(side*.012f,0,z),knee,.0024f,.002f,Ink,4);
                    mesh.Rod(knee,new Vector3(side*.044f,-.040f,z+.020f-i*.017f),.002f,.0015f,Ink,4);
                }
            }
            return mesh;
        }

        static Draft WhiteWing()
        {
            var mesh = new Draft();
            Color ivory = Draft.C(0xFFF9DC);
            Panel(mesh, P(.14f,-.05f), new[]{P(.009f,0),P(.015f,-.12f),P(.09f,-.175f),
                P(.18f,-.17f),P(.24f,-.11f),P(.255f,-.036f),P(.18f,.03f)},Draft.C(0xEBE9BF),0);
            Panel(mesh,P(.16f,.058f),new[]{P(.008f,-.03f),P(.13f,-.06f),P(.24f,-.035f),
                P(.313f,.055f),P(.34f,.15f),P(.31f,.215f),P(.22f,.192f),P(.024f,.072f)},ivory,0);
            Panel(mesh,P(.306f,.156f),new[]{P(.247f,.197f),P(.31f,.215f),P(.34f,.15f),
                P(.313f,.055f),P(.297f,.125f)},Ink,.001f);
            Spot(mesh,P(.208f,.074f),.020f,.016f,Ink);
            Spot(mesh,P(.151f,-.005f),.014f,.012f,Ink);
            return mesh;
        }

        static Draft Wing()
        {
            var mesh = new Draft();
            var forewing = new[] {
                P(.006f,-.020f),P(.115f,-.045f),P(.198f,-.053f),P(.257f,-.040f),
                P(.293f,-.012f),P(.316f,.020f),P(.340f,.060f),P(.361f,.109f),
                P(.382f,.167f),P(.393f,.225f),P(.384f,.241f),P(.357f,.237f),
                P(.285f,.209f),P(.202f,.171f),P(.109f,.124f),P(.016f,.073f)
            };
            var hindwing = new[] {
                P(.012f,-.093f),P(.056f,-.147f),P(.080f,-.166f),P(.104f,-.163f),
                P(.132f,-.198f),P(.152f,-.193f),P(.163f,-.213f),P(.170f,-.265f),
                P(.182f,-.280f),P(.191f,-.274f),P(.198f,-.204f),P(.216f,-.177f),
                P(.235f,-.182f),P(.250f,-.153f),P(.270f,-.148f),P(.271f,-.124f),
                P(.288f,-.106f),P(.281f,-.078f),P(.258f,-.049f),P(.180f,-.056f),P(.068f,-.041f)
            };
            Panel(mesh, P(.013f,.035f), forewing, Ink, 0f);
            Panel(mesh, P(.020f,-.048f), hindwing, Ink, 0f);

            // The four main yellow cells are separated by the dark wing veins.
            Quad(mesh,P(.024f,.042f),P(.066f,.063f),P(.110f,.001f),P(.052f,-.009f),Honey);
            Quad(mesh,P(.075f,.075f),P(.132f,.107f),P(.182f,-.013f),P(.124f,-.021f),Sun);
            Quad(mesh,P(.146f,.111f),P(.206f,.144f),P(.231f,-.010f),P(.197f,-.021f),Honey);
            Quad(mesh,P(.219f,.150f),P(.283f,.180f),P(.280f,.031f),P(.245f,-.001f),Sun);
            Quad(mesh,P(.297f,.181f),P(.323f,.195f),P(.309f,.100f),P(.291f,.065f),Honey);
            for (int i=0; i<5; i++)
            {
                var center = Vector2.Lerp(P(.268f,-.024f),P(.373f,.174f),i/4f);
                Spot(mesh,center,.0065f+i*.0007f,.011f,Sun);
            }
            Quad(mesh,P(.030f,-.058f),P(.081f,-.056f),P(.104f,-.125f),P(.064f,-.113f),Sun);
            Quad(mesh,P(.092f,-.057f),P(.148f,-.064f),P(.157f,-.144f),P(.115f,-.132f),Honey);
            Quad(mesh,P(.161f,-.065f),P(.218f,-.064f),P(.209f,-.130f),P(.169f,-.144f),Sun);
            Quad(mesh,P(.231f,-.062f),P(.256f,-.065f),P(.259f,-.098f),P(.223f,-.123f),Honey);
            Spot(mesh,P(.092f,-.146f),.012f,.009f,Orange);
            Spot(mesh,P(.136f,-.165f),.013f,.008f,Blue);
            Spot(mesh,P(.173f,-.171f),.015f,.009f,Blue);
            Spot(mesh,P(.209f,-.154f),.013f,.009f,Blue);
            Spot(mesh,P(.242f,-.129f),.011f,.008f,Orange);
            return mesh;
        }

        static void Quad(Draft mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            Panel(mesh,(a+b+c+d)*.25f,new[]{a,b,c,d},color,.00065f);
        }

        static void Spot(Draft mesh,Vector2 center,float width,float depth,Color color)
        {
            var rim = new Vector2[6];
            for (int i=0; i<rim.Length; i++)
            {
                float angle = i*Mathf.PI/3f;
                rim[i] = center + P(width*Mathf.Cos(angle),depth*Mathf.Sin(angle));
            }
            Panel(mesh,center,rim,color,.00065f);
        }

        static void Panel(Draft mesh,Vector2 center,Vector2[] rim,Color pigment,float thickness)
        {
            // Duplicate painted faces so the underside still reads when a wing folds upright.
            Vector3 Point(Vector2 p,float offset) => new Vector3(p.x,.028f*p.x+offset,p.y);
            Color underside = Color.Lerp(pigment,Draft.C(0xAE9B72),.15f);
            for (int i=0; i<rim.Length; i++)
            {
                Vector2 a=rim[i], b=rim[(i+1)%rim.Length];
                mesh.Tri(Point(center,thickness),Point(a,thickness),Point(b,thickness),pigment,Vector3.up);
                mesh.Tri(Point(center,-thickness),Point(a,-thickness),Point(b,-thickness),underside,Vector3.down);
            }
        }
    }
}
