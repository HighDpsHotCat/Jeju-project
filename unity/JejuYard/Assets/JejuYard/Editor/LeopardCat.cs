using UnityEngine;
using UnityEditor;

namespace Jeju.Yard.EditorArt
{
    public static class LeopardCat
    {
        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
        static readonly Color tawny=Draft.C(0xc49958), ink=Draft.C(0x48372d), cream=Draft.C(0xf2e3c6), amber=Draft.C(0xceb257);
        [MenuItem("Jeju Yard/Author leopard cat")]
        public static void Author()
        {
            var root=new GameObject("Leopard cat");
            try {
                var b=new Draft();
                b.Ball(V(0,.48f,0),V(.157f,.171f,.345f),tawny,20,12);
                b.Ball(V(0,.39f,.018f),V(.125f,.107f,.26f),cream,12,6);
                b.Rod(V(0,.49f,.21f),V(0,.62f,.33f),.14f,.107f,tawny,10);
                b.Ball(V(0,.628f,.34f),V(.14f,.13f,.139f),tawny,16,8);
                for(int side=-1;side<=1;side+=2){
                    for(int row=0;row<3;row++) for(int col=0;col<6;col++){
                        float z=(col-2.5f)*.095f+(row%2)*.019f, y=(row-1)*.062f+.018f*Mathf.Sin(col*2+row);
                        float x=.157f*Mathf.Sqrt(1-z*z/(.345f*.345f)-y*y/(.171f*.171f));
                        b.Ball(V(side*x,.48f+y,z),V(.012f,.017f+(col%2)*.005f,.026f),ink,8,4);
                    }
                    b.Ball(V(side*.095f,.733f,.305f),V(.051f,.063f,.038f),ink,10,6);
                    b.Ball(V(side*.101f,.742f,.276f),V(.021f,.022f,.011f),cream);
                    b.Ball(V(side*.102f,.745f,.334f),V(.027f,.031f,.011f),Draft.C(0xb48a75));
                    b.Rod(V(side*.041f,.747f,.332f),V(side*.057f,.698f,.431f),.012f,.011f,ink);
                    b.Ball(V(side*.075f,.654f,.445f),V(.033f,.025f,.012f),ink);
                    b.Ball(V(side*.075f,.654f,.455f),V(.022f,.017f,.009f),amber);
                    b.Ball(V(side*.076f,.655f,.463f),V(.005f,.014f,.004f),ink);
                    b.Ball(V(side*.038f,.6f,.457f),V(.046f,.032f,.037f),cream);
                    b.Rod(V(side*.089f,.631f,.435f),V(side*.127f,.603f,.381f),.009f,.012f,ink);
                }
                b.Ball(V(0,.622f,.482f),V(.022f,.013f,.014f),Draft.C(0x69453c));
                b.Bake("LeopardCatCoat",root.transform);
                for(int s=-1;s<=1;s+=2) for(int f=-1;f<=1;f+=2){
                    var d=new Draft(); float k=f<0?.059f:.012f;
                    d.Rod(V(0,0,0),V(0,-.172f,k),.057f,.039f,tawny,9);
                    d.Rod(V(0,-.172f,k),V(0,-.345f,0),.041f,.027f,tawny,9);
                    d.Rod(V(0,-.201f,k*.8f),V(0,-.217f,k*.72f),.04f,.038f,ink,9);
                    d.Ball(V(0,-.357f,.022f),V(.038f,.032f,.061f),cream);
                    string joint=(f==1?"Fore":"Hind")+(s==1?"Right":"Left");
                    var part=d.Bake("LeopardCat"+joint,root.transform); part.name=joint;
                    part.transform.localPosition=V(s*.095f,.39f,f*.209f);
                }
                var tail=new Draft(); var prev=Vector3.zero;
                for(int i=1;i<=13;i++){
                    float t=i/13f; var next=V(.10f*t*t,-.18f*Mathf.Sin(t*Mathf.PI*.72f),-.35f*t);
                    tail.Rod(prev,next,.048f-.023f*(i-1)/13f,.048f-.023f*t,(i>=12||i%3==0)?ink:tawny,10); prev=next;
                }
                var tip=tail.Bake("LeopardCatTail",root.transform); tip.name="Tail"; tip.transform.localPosition=V(0,.5f,-.27f);
                if (!PrefabUtility.SaveAsPrefabAsset(root,"Assets/JejuYard/Resources/YardWildlife/leopard_cat.prefab"))
                    throw new System.Exception("Leopard cat prefab was not saved");
                AssetDatabase.SaveAssets();
            } finally { Object.DestroyImmediate(root); }
        }
        public static void BuildPlayer() { Author(); YardStudio.BuildFlutterPlayer(); }
    }
}
