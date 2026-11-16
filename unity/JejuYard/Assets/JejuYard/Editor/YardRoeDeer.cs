using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static class YardRoeDeer
    {
        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
        static readonly Color Coat = Draft.C(0xA96E44), Fawn = Draft.C(0xC88C55);
        static readonly Color Cream = Draft.C(0xF4D7AA), Dark = Draft.C(0x39312B);
        public static void Build(Transform parent)
        {
            var anchor = new GameObject("Roe deer").transform;
            anchor.SetParent(parent,false);
            anchor.localPosition = V(-2.8f,0,.8f);
            var walker = new GameObject("Grazing buck").AddComponent<GrazingDeer>();
            walker.transform.SetParent(anchor,false);
            var body = new Draft();
            body.Ball(V(0,.79f,-.04f),V(.22f,.27f,.50f),Coat,14,7);
            body.Ball(V(0,.81f,-.44f),V(.16f,.17f,.09f),Cream,10,5);
            body.Ball(V(0,.78f,.28f),V(.19f,.25f,.23f),Fawn,12,6);
            body.Bake("RoeDeerBody",walker.transform);
            var head = new Draft();
            head.Rod(Vector3.zero,V(0,.40f,.14f),.16f,.092f,Fawn,10);
            head.Ball(V(0,.42f,.18f),V(.13f,.16f,.17f),Fawn,12,6);
            head.Rod(V(0,.41f,.23f),V(0,.355f,.43f),.105f,.060f,Coat,9);
            head.Ball(V(0,.36f,.425f),V(.059f,.045f,.037f),Dark);
            for (int s=-1; s<=1; s+=2)
            {
                Vector3 ear = V(s*.182f,.67f,.085f);
                head.Rod(V(s*.074f,.49f,.14f),ear,.066f,.009f,Coat,7);
                head.Rod(V(s*.106f,.535f,.165f),ear+V(0,-.017f,.012f),.035f,.003f,Cream,6);
                head.Ball(V(s*.115f,.448f,.255f),V(.021f,.027f,.025f),Dark);
                head.Ball(V(s*.129f,.459f,.263f),V(.005f,.006f,.006f),Color.white);
                Color horn = Draft.C(0x78563F);
                var basePoint=V(s*.065f,.53f,.192f);
                var fork=V(s*.082f,.71f,.142f);
                head.Rod(basePoint,fork,.025f,.019f,horn);
                head.Rod(fork,V(s*.115f,.83f,.091f),.019f,.004f,horn);
                head.Rod(fork,V(s*.115f,.785f,.197f),.014f,.003f,horn);
            }
            walker.neck = head.Bake("RoeDeerNeck",walker.transform).transform;
            walker.neck.localPosition = V(0,.83f,.30f);
            walker.legs = new Transform[4];
            for (int pair=0; pair<2; pair++)
            {
                var leg = new Draft();
                float kneeZ = pair == 0 ? .015f : .091f;
                float hockZ = pair == 0 ? 0 : -.050f;
                leg.Rod(Vector3.zero,V(0,-.26f,kneeZ),.083f,.043f,Coat,8);
                leg.Rod(V(0,-.26f,kneeZ),V(0,-.44f,hockZ),.045f,.027f,Fawn,7);
                leg.Rod(V(0,-.44f,hockZ),V(0,-.62f,.02f),.029f,.023f,Fawn);
                leg.Ball(V(0,-.625f,.034f),V(.032f,.032f,.054f),Dark);
                var left = leg.Bake(pair==0 ? "RoeDeerForeleg" : "RoeDeerHindleg",walker.transform).transform;
                var right = Object.Instantiate(left,walker.transform);
                left.localPosition=V(-.135f,.655f,pair==0 ? .26f : -.32f);
                right.localPosition=Vector3.Scale(left.localPosition,V(-1,1,1));
                walker.legs[2*pair]=left;
                walker.legs[2*pair+1]=right;
            }
            walker.PoseAt(0);
        }
    }
}
