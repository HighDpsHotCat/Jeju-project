using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class YardSongbirds
    {
        enum Kind { Bulbul, WhiteEye, Magpie }
        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
        static Color C(int rgb) => Draft.C(rgb);
        static Transform Joint(string name, Transform parent, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent,false);
            t.localPosition = position;
            return t;
        }

        public static void Build(Transform parent)
        {
            BuildBird(Kind.Bulbul, "Brown-eared bulbul", V(-1.8f,0,3.7f),1.36f,parent);
            BuildBird(Kind.WhiteEye, "Warbling white-eye", V(2.55f,0,2f),.88f,parent);
            BuildBird(Kind.Magpie, "Oriental magpie", V(3.25f,0,-2.25f),1.55f,parent);
        }

        static void BuildBird(Kind kind, string name, Vector3 at, float scale, Transform parent)
        {
            var bird = Joint(name,parent,at).gameObject.AddComponent<YardBird>();
            bird.species = YardBird.Species.Sparrow; // Small-bird perch, hop and flight rig.
            bird.transform.localScale = Vector3.one * scale;
            bird.flightFrame = Joint("Flight path",bird.transform,Vector3.zero);
            bird.torso = Joint("Breast",bird.flightFrame,Vector3.zero);
            string asset = kind.ToString();
            bool olive = kind == Kind.WhiteEye, pied = kind == Kind.Magpie;
            Color back = C(olive ? 0x89943F : pied ? 0x263645 : 0x797674);
            Color breast = C(olive ? 0xE7E0A7 : pied ? 0xF5F2E7 : 0xACAAA3);
            Color wing = C(olive ? 0x6A773A : pied ? 0x294B63 : 0x645D57);
            Color tips = C(olive ? 0x515C39 : pied ? 0x354F5B : 0x524D48);
            var body = new Draft();
            body.Ball(V(0,0,0),V(.13f,.143f,olive ? .187f : .24f),breast,12,6);
            body.Ball(V(0,.041f,-.049f),V(.135f,.13f,.207f),back,12,6);
            float tail = olive ? .31f : pied ? .71f : .47f;
            for (int f=-2; f<=2; f++)
                body.Rod(V(f*.015f,.046f,-.134f),V(f*.031f,.092f,-tail+.038f*Mathf.Abs(f)),
                    .030f,.014f,f==0 && pied ? C(0x3C655F) : tips,6);
            body.Bake(asset+"Coat",bird.torso);

            bird.head = Joint("Head pivot",bird.torso,V(0,.084f,.106f));
            var face = new Draft();
            face.Ball(V(0,.055f,.049f),V(.119f,.117f,.136f),back,12,6);
            face.Ball(V(0,-.008f,.100f),V(.089f,.078f,.075f),
                C(olive ? 0xE5CC55 : pied ? 0x263138 : 0xB7AFA4),10,5);
            if (kind == Kind.Bulbul)
                for (int i=-1;i<=1;i++)
                    face.Rod(V(i*.025f,.146f,.042f),V(i*.035f,.190f-.008f*Mathf.Abs(i),-.031f),.028f,.002f,back,6);
            for(int s=-1;s<=1;s+=2)
            {
                if(kind == Kind.Bulbul)
                    face.Ball(V(s*.106f,.050f,.045f),V(.018f,.039f,.052f),C(0x9E643F),10,5);
                var eye = V(s*.1f,.081f,.11f);
                if(olive) face.Ball(eye,V(.022f,.041f,.040f),C(0xFFFCED),12,6);
                face.Ball(eye+V(s*.009f,0,0),V(.019f,.024f,.025f),C(0x292B2B),10,5);
                face.Ball(eye+V(s*.023f,.009f,.005f),V(.005f,.006f,.006f),Color.white);
            }
            face.Rod(V(0,.048f,.149f),V(0,.036f,pied ? .294f : olive ? .233f : .266f),
                pied ? .039f : .026f,.003f,C(0x353638),7);
            face.Bake(asset+"Face",bird.head);

            var legs = new Draft();
            for(int s=-1;s<=1;s+=2)
            {
                var ankle=V(s*.052f,-.134f,.013f);
                legs.Rod(V(s*.052f,0,0),ankle,.012f,.007f,C(0x746761),6);
                for(int toe=-1;toe<=1;toe++)
                    legs.Rod(ankle,ankle+V(toe*.029f,0,.055f-.009f*Mathf.Abs(toe)),.006f,.002f,C(0x605853),5);
                legs.Rod(ankle,ankle+V(0,0,-.035f),.006f,.002f,C(0x605853),5);

                var inner = new Draft();
                float wrist = olive ? .18f : .23f;
                inner.Ball(V(s*wrist*.47f,0,-.037f),V(wrist*.58f,.037f,.112f),wing,12,5);
                if(pied) inner.Ball(V(s*.078f,.017f,-.047f),V(.077f,.030f,.099f),breast,10,5);
                var upper = inner.Bake(asset+(s<0 ? "LeftWing" : "RightWing"),bird.torso).transform;
                var outer = new Draft();
                for(int f=0;f<5;f++)
                {
                    float span = olive ? .16f : pied ? .36f : .25f;
                    outer.Rod(V(-s*.042f,0,.037f-.028f*f),V(s*(span-.020f*f),-.014f,-.052f-.024f*f),
                        .031f,.005f,f==2 && pied ? C(0x417180) : tips,6);
                }
                var feathers = outer.Bake(asset+(s<0 ? "LeftTips" : "RightTips"),upper).transform;
                feathers.localPosition=V(s*wrist,.008f,0);
                if(s<0){ bird.leftWing=upper; bird.leftPrimaries=feathers; }
                else { bird.rightWing=upper; bird.rightPrimaries=feathers; }
            }
            bird.feet=legs.Bake(asset+"Feet",bird.flightFrame).transform;
            bird.PoseAt(0);
        }
    }
}
