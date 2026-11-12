using System;
using UnityEngine;
using Rand = System.Random;

namespace Jeju.Yard.EditorArt
{
    public static class Landscape
    {
        static readonly Color[] Basalt = Palette(0x555859, 0x696865, 0x666A66, 0x77736C, 0x5C6064, 0x686969, 0x787A6C);
        static readonly Color Lawn = Draft.C(0x9BB96F);
        static readonly Color[] Grass = Palette(0x6F9459, 0x89A35C, 0xABC773, 0x789959, 0xA1B966);
        static readonly Color[] Leaves = Palette(0x507F51, 0x719756, 0x628A50, 0x849D57, 0x4C7955);
        static readonly Color[] Oranges = Palette(0xEB9236, 0xE88631, 0xF2A13A);
        static readonly Color[] Petals = Palette(0xCD5862, 0xF07970, 0xDD6266);
        static readonly Color Moss = Draft.C(0x99A478);
        static readonly Vector3 Up = Vector3.up;
        static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
        static Color[] Palette(params int[] codes) => Array.ConvertAll(codes, Draft.C);
        static float Range(Rand r, float lo, float hi) => Mathf.Lerp(lo,hi,(float)r.NextDouble());
        static Color Pick(Rand r, Color[] colors) => colors[r.Next(colors.Length)];

        public static GameObject Build()
        {
            var root = new GameObject("Courtyard · Jeju");
            var yard = root.transform;
            var rng = new Rand(4703);
            Ground(yard,rng);
            Paths(yard,rng);
            Wall(yard,rng,true);
            Wall(yard,rng,false);
            Orchard(yard,rng);
            Meadow(yard,rng);
            return root;
        }

        static void Ground(Transform root, Rand rng)
        {
            var d = new Draft();
            Coast.Build(root, d, Lawn);
            var islands = new[] {new Vector2(-4.3f,-2.2f),new Vector2(3.8f,-2.7f),
                new Vector2(4.9f,3.8f),new Vector2(-6.4f,-7.7f),new Vector2(7.1f,-6.4f),
                new Vector2(6.9f,8.5f),new Vector2(-10.0f,1.0f),new Vector2(-3.0f,10.5f)};
            foreach (var spot in islands) {
                float w=Range(rng,1.5f,2.8f), h=Range(rng,.8f,1.7f);
                Vector3 p=V(spot.x,.002f,spot.y);
                var tint=Color.Lerp(Lawn,Draft.C(0x72935F),.28f);
                Patch(d,rng,p,w,h,tint,19);
                Patch(d,rng,p+V(w*.12f,.0015f,h*.09f),w*.72f,h*.70f,
                    Color.Lerp(Lawn,Draft.C(0x78975B),.39f),17);
                for(int j=0;j<13;j++) {
                    float theta=Range(rng,0,Mathf.PI*2);
                    var accent=p+V(Mathf.Cos(theta)*w*Range(rng,.9f,1.1f),.003f,
                        Mathf.Sin(theta)*h*Range(rng,.9f,1.2f));
                    Patch(d,rng,accent,Range(rng,.07f,.22f),Range(rng,.05f,.12f),tint,5);
                }
            }
            // Small groups of painted blades give the grass texture without alpha cards.
            for(int i=0;i<550;i++) {
                float x=Range(rng,-14.45f,14.45f), z=Range(rng,-14.45f,14.45f);
                if((Mathf.Abs(x)>13 && Mathf.Abs(z)>13) || !Coast.OnLawn(V(x,0,z),.28f)) continue;
                var pos=V(x,.006f,z);
                Color shade=Color.Lerp(Lawn,Pick(rng,Grass),.64f);
                int blades=rng.Next(1,4);
                for(int k=0;k<blades;k++){
                    var a=pos+V(k*.045f,0,Range(rng,-.09f,.09f));
                    d.Tri(a,a+V(.11f,0,.038f),a+V(.07f,0,.16f),shade,Up);
                }
            }
            d.Bake("GrassTerrace",root);
        }

        // Softly scalloped earth around the porch, with flat hand-cut stepping stones.
        static void Patch(Draft d,Rand r,Vector3 center,float width,float depth,Color color,int count)
        {
            var loop=new Vector3[count];
            for(int i=0;i<count;i++){
                float t=2*Mathf.PI*i/count, w=Range(r,.90f,1.08f);
                loop[i]=center+V(Mathf.Cos(t)*w*width,0,Mathf.Sin(t)*w*depth);
            }
            for(int i=0;i<count;i++) d.Tri(center,loop[i],loop[(i+1)%count],color,Up);
        }

        static Vector3 Trail(float t)
        {
            float u=1-t;
            return u*u*V(1.0f,0,-5.1f)+2*u*t*V(-.2f,0,.8f)+t*t*V(-5.45f,0,3.2f);
        }

        static void Paths(Transform root,Rand rng)
        {
            var d=new Draft();
            var clay=Draft.C(0xBFB083);
            Patch(d,rng,V(-5.4f,.012f,4.4f),4.2f,2.8f,clay,25);
            Vector3 previousLeft=Vector3.zero, previousRight=Vector3.zero;
            for(int i=0;i<=24;i++){
                float t=i/24f;
                var p=Trail(t)+Up*.017f;
                var tangent=Trail(t+.001f)-Trail(t-.001f);
                var lateral=Vector3.Cross(Up,tangent).normalized;
                float width=.09f+.78f*Mathf.Sqrt(t)+.40f*Mathf.Sin(t*Mathf.PI);
                var l=p+lateral*(width+Range(rng,-.07f,.08f));
                var r=p-lateral*(width+Range(rng,-.08f,.07f));
                if(i>0)d.Quad(previousLeft,previousRight,r,l,
                    Color.Lerp(Lawn,clay,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.64f,t))),Up);
                previousLeft=l; previousRight=r;
            }
            var stones=Palette(0xAEAA94,0xBAB5A2,0xA5A596);
            for(int i=0;i<10;i++){
                var p=Trail(.27f+.071f*i)+V(i%2==0?.11f:-.13f,.026f,0);
                float w=Range(rng,.22f,.36f), h=Range(rng,.15f,.22f);
                Patch(d,rng,p,w,h,Pick(rng,stones),7);
            }
            for(int i=0;i<54;i++){
                var p=Trail(Range(rng,.12f,1))+V(Range(rng,-.68f,.68f),.022f,Range(rng,-.4f,.4f));
                Patch(d,rng,p,Range(rng,.018f,.044f),Range(rng,.018f,.044f),Draft.C(0xB5AB84),4);
            }
            d.Bake("PorchPath",root);
        }

        static readonly Vector2[] Corners = {
            new Vector2(-.72f,-1),new Vector2(.60f,-1),new Vector2(1,-.55f),
            new Vector2(.96f,.55f),new Vector2(.64f,1),new Vector2(-.70f,.97f),
            new Vector2(-1,.54f),new Vector2(-1,-.55f)
        };

        // Three hand-jittered rings and a chipped crown give each basalt block a different silhouette.
        static void Rock(Draft d,Rand r,Vector3 p,Vector3 across,Vector3 outwards,
            float width,float depth,float height,Color color,bool moss)
        {
            int n=Corners.Length;
            var lower=new Vector3[n]; var widest=new Vector3[n]; var shoulder=new Vector3[n]; var crown=new Vector3[n];
            Vector3 drift=across*Range(r,-.06f,.06f)+outwards*Range(r,-.045f,.045f);
            for(int i=0;i<n;i++){
                Vector3 radial=across*(Corners[i].x*width*.5f*Range(r,.95f,1.06f))+
                    outwards*(Corners[i].y*depth*.5f*Range(r,.96f,1.035f));
                lower[i]=p+radial*Range(r,.78f,.88f);
                widest[i]=p+radial+Up*height*Range(r,.19f,.34f);
                shoulder[i]=p+radial*Range(r,.89f,1.01f)+Up*height*Range(r,.63f,.82f)+drift*.3f;
                crown[i]=p+radial*Range(r,.56f,.70f)+Up*height*Range(r,.94f,1.03f)+drift;
            }
            Vector3 peak=p+Up*height*1.01f+drift;
            Color cap=moss ? Color.Lerp(color,Moss,Range(r,.42f,.68f)):Color.Lerp(color,Color.white,.075f);
            for(int i=0;i<n;i++){
                int j=(i+1)%n;
                var normal=((widest[i]+widest[j])*.5f-p).normalized;
                d.Quad(lower[i],lower[j],widest[j],widest[i],Color.Lerp(color,Color.black,.10f),normal);
                d.Quad(widest[i],widest[j],shoulder[j],shoulder[i],color,normal);
                d.Quad(shoulder[i],shoulder[j],crown[j],crown[i],Color.Lerp(color,cap,.55f),normal+Up*.3f);
                d.Tri(crown[i],crown[j],peak,cap,Up);
            }
            // A pair of small dark flecks sits flush with the inner face of selected rocks.
            if(r.NextDouble()<.42){
                var a=widest[0]; var b=widest[1]; var c=shoulder[0];
                float mid=Range(r,.28f,.66f);
                var mark=Vector3.Lerp(a,b,mid)+(c-a)*.53f-outwards*.01f;
                float s=Range(r,.025f,.051f);
                d.Quad(mark-across*s,mark+Up*s*.65f,mark+across*s,mark-Up*s*.60f,
                    Color.Lerp(color,Color.black,.37f),-outwards);
            }
        }

        static void Wall(Transform root,Rand rng,bool north)
        {
            const float thickness=.86f;
            float inner=YardWalker.CentreToWallContact+YardWalker.BodyRadius;
            Vector3 outward=north ? Vector3.forward : Vector3.left;
            Vector3 along=Vector3.Cross(Up,outward);
            float mid=inner+thickness*.5f;
            var d=new Draft();
            float start=north ? -inner-thickness : -inner;
            float end=inner;
            // The last stones reach the wet sand where the coast meets each retained wall.
            float length=end-start;
            var middle=outward*mid+along*((start+end)*.5f);
            var coreSize=north ? V(length,1.16f,.54f) : V(.54f,1.16f,length);
            d.Box(middle+Up*.22f,coreSize,Draft.C(0x484C49));
            for(int row=0;row<3;row++){
                int count=27+row*2;
                float[] widths=new float[count];
                float total=0;
                for(int i=0;i<count;i++) {widths[i]=Range(rng,.68f,1.36f);total+=widths[i];}
                float x=start;
                for(int i=0;i<count;i++){
                    float w=widths[i]*length/total;
                    float y=row==0?-.025f:row==1?.355f:.686f;
                    float height=row==0?.47f:row==1?.44f:Range(rng,.34f,.53f);
                    float depth=row==0? .86f : row==1?.84f:Range(rng,.67f,.83f);
                    Vector3 p=outward*(mid+Range(rng,-.025f,.025f))+along*(x+w*.5f)+Up*y;
                    p.y+=Coast.SandHeight(p);
                    Rock(d,rng,p,along,outward,w*1.035f,depth,height,Pick(rng,Basalt),
                        row==2 && rng.NextDouble()<.24);
                    x+=w;
                }
            }
            var wall=d.Bake(north ? "NorthBasalt" : "WestBasalt",root);
            var box=wall.AddComponent<BoxCollider>();
            box.center=middle+Up*.41f;
            box.size=north ? V(length,1.42f,thickness) : V(thickness,1.42f,length);
        }

        static void Orchard(Transform root,Rand rng)
        {
            var d=new Draft();
            Citrus(d,root,rng,V(6.0f,0,6.7f),1.07f);
            Citrus(d,root,rng,V(8.7f,0,8.65f),.82f);
            Citrus(d,root,rng,V(-10.3f,0,-4.6f),.85f);
            Citrus(d,root,rng,V(7.65f,0,-5.95f),.92f);
            d.Bake("MandarinGrove",root);
        }

        static void Citrus(Draft d,Transform root,Rand rng,Vector3 at,float s)
        {
            var bark=Draft.C(0x886951);
            var baseTop=at+V(.09f,1.42f,-.02f)*s;
            d.Rod(at,at+V(-.04f,.47f,.04f)*s,.27f*s,.19f*s,bark,7);
            d.Rod(at+V(-.04f,.42f,.04f)*s,baseTop,.19f*s,.14f*s,bark,7);
            d.Ball(at+V(.06f,2.73f,.05f)*s,V(1.05f,.94f,.96f)*s,Pick(rng,Leaves),8,4);
            int lobes=5;
            float rotation=Range(rng,0,6.28f);
            for(int i=0;i<lobes;i++){
                float angle=rotation+i*2*Mathf.PI/lobes;
                var radial=V(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tangent=Vector3.Cross(Up,radial);
                var center=at+(radial*.74f+Up*Range(rng,2.01f,2.40f))*s;
                d.Rod(baseTop-Up*.29f*s,center,.12f*s,.06f*s,bark,6);
                d.Ball(center,V(1.00f,.85f,.96f)*s,Pick(rng,Leaves),8,4);
                for(int j=0;j<3;j++){
                    var fruit=center+(radial*(j==1?.96f:.89f)+tangent*((j-1)*.32f)+Up*Range(rng,-.43f,.15f))*s;
                    float radius=Range(rng,.12f,.155f)*s;
                    d.Ball(fruit,V(radius,radius*.89f,radius),Pick(rng,Oranges),7,3);
                    var top=fruit+Up*radius*.79f;
                    d.Tri(top-tangent*.053f*s,top+tangent*.1f*s,top+radial*.08f*s, Draft.C(0x55794C),Up);
                    var twig=top+radial*.025f*s;
                    var leafTip=twig+(tangent*.22f+Up*.055f)*s;
                    var mid=(twig+leafTip)*.5f+Up*.044f*s;
                    d.Quad(twig,mid+radial*.074f*s,leafTip,mid-radial*.064f*s,Draft.C(0x73915B),Up+radial);
                }
            }
            var collider=new GameObject("Mandarin trunk");
            collider.transform.SetParent(root,false);
            collider.transform.localPosition=at;
            var body=collider.AddComponent<CapsuleCollider>();
            body.center=Up*(1.16f*s); body.height=2.32f*s; body.radius=.24f*s;
        }

        static void Meadow(Transform root,Rand rng)
        {
            var d=new Draft();
            var beds=new[]{V(8.8f,0,4.9f),V(-10.9f,0,9.3f),V(-9.8f,0,-8.6f),V(6.3f,0,-8.75f),V(4.4f,0,10.6f),
                V(-7.7f,0,1.62f),V(-2.3f,0,2.05f),V(3.4f,0,3.25f),
                V(-4.65f,0,-2.4f),V(4.05f,0,-2.65f),V(-1.45f,0,-5.7f)};
            for(int k=0;k<beds.Length;k++){
                var bed=beds[k];
                int plants=k<5?21:12;
                for(int i=0;i<plants;i++){
                    float angle=Range(rng,0,Mathf.PI*2), dist=Mathf.Sqrt(Range(rng,0,1));
                    var p=bed+V(Mathf.Cos(angle)*dist*1.30f,0,Mathf.Sin(angle)*dist*.64f);
                    if(!Coast.OnLawn(p,.22f)) continue;
                    Tuft(d,rng,p,.19f);
                    if(i%2==0)Wildflower(d,rng,p, k%3==0 ? Draft.C(0xF5C858):Draft.C(0xFFF7DE));
                }
            }
            for(int i=0;i<140;i++){
                float x=Range(rng,-12.5f,12.5f),z=Range(rng,-12.5f,12.5f);
                if(Mathf.Abs(x)<1.2f || Mathf.Abs(z)<1.2f || (x < -1.15f && x > -9.8f && z>.9f && z<8.4f))continue;
                if(Coast.OnLawn(V(x,0,z),.18f)) Tuft(d,rng,V(x,0,z),Range(rng,.08f,.15f));
            }
            foreach(var p in new[]{V(8.35f,0,-3.35f),V(8.5f,0,-4.45f),V(-11.75f,0,5.4f),
                V(-10.95f,0,10.75f),V(4.3f,0,11.85f),V(-5.9f,0,-8.95f)})Camellia(d,rng,p);
            d.Bake("CamelliasAndMeadow",root);
        }

        static void Tuft(Draft d,Rand rng,Vector3 basePoint,float size)
        {
            for(int i=0;i<4;i++){
                float angle=Range(rng,0,6.28f);
                var dir=V(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var tangent=Vector3.Cross(Up,dir);
                var a=basePoint+dir*.026f+Up*.016f;
                var tip=a+(dir*Range(rng,.60f,1.1f)+Up*Range(rng,.7f,1.35f))*size;
                var mid=Vector3.Lerp(a,tip,.57f)+Up*size*.13f;
                var col=Pick(rng,Grass);
                d.Quad(a,mid-tangent*size*.22f,tip,mid+tangent*size*.22f,col,Up);
            }
        }

        static void Wildflower(Draft d,Rand rng,Vector3 p,Color petal)
        {
            float h=Range(rng,.25f,.43f);
            var top=p+V(Range(rng,-.045f,.045f),h,0);
            d.Rod(p,top,.015f,.011f,Draft.C(0x6E9454),4);
            float size=Range(rng,.094f,.133f);
            var normal=V(.08f,.78f,-.62f).normalized;
            var stem=p+Up*h*.39f;
            d.Quad(stem,stem+V(.055f,.065f,.035f),stem+V(.125f,.105f,0),stem+V(.062f,.042f,-.044f),Draft.C(0x5B8D50),Up);
            Blossom(d,top,normal,size,petal,Draft.C(0xD6A246),6);
        }

        static void Camellia(Draft d,Rand rng,Vector3 p)
        {
            var foliage=Palette(0x4D795B,0x5B8660,0x64915C);
            const int lobes=4;
            for(int i=0;i<lobes;i++){
                float angle=2*Mathf.PI*i/lobes+.34f;
                var dir=V(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var center=p+dir*.39f+Up*Range(rng,.44f,.64f);
                d.Ball(center,V(.55f,.54f,.53f),Pick(rng,foliage),7,4);
                for(int j=0;j<3;j++){
                    float tilt=Range(rng,.23f,.92f);
                    var normal=(dir+Vector3.Cross(Up,dir)*Range(rng,-.6f,.6f)+Up*tilt).normalized;
                    var pos=center+normal*.52f;
                    Blossom(d,pos,normal,Range(rng,.060f,.10f),Pick(rng,Petals),Draft.C(0xF9CF70),6);
                }
            }
        }

        static void Blossom(Draft d,Vector3 p,Vector3 normal,float radius,Color petal,Color pollen,int count)
        {
            Vector3 u=Vector3.Cross(normal,Vector3.right).normalized;
            Vector3 v=Vector3.Cross(u,normal).normalized;
            var core=p+normal*(radius*.27f);
            for(int i=0;i<count;i++){
                float t=2*Mathf.PI*i/count;
                var dir=Mathf.Cos(t)*u+Mathf.Sin(t)*v;
                var across=-Mathf.Sin(t)*u+Mathf.Cos(t)*v;
                var foot=p+dir*radius*.13f;
                var end=p+dir*radius+normal*radius*.24f;
                var left=p+dir*radius*.60f+across*radius*.31f;
                var right=p+dir*radius*.60f-across*radius*.31f;
                var rim=end-dir*radius*.085f;
                d.Tri(foot,left,right,petal,normal);
                d.Quad(left,rim+across*radius*.19f,rim-across*radius*.19f,right,petal,normal);
                var edge=p+dir*radius*.29f+normal*radius*.22f;
                float next=t+2*Mathf.PI/count;
                var nextEdge=p+(Mathf.Cos(next)*u+Mathf.Sin(next)*v)*radius*.29f+normal*radius*.22f;
                d.Tri(core,edge,nextEdge,pollen,normal);
            }
        }
    }
}
