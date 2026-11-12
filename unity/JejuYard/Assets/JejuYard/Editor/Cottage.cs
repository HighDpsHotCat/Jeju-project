using System;
using UnityEngine;
using Random = System.Random;

namespace Jeju.Yard.EditorArt
{
    // All parts use vertex paint and share the yard material, including the roof bindings.
    public static class Cottage
    {
        static readonly Color Timber = Draft.C(0x754E39);
        static readonly Color Rail = Draft.C(0x97704C);
        static readonly Color Straw = Draft.C(0xC8A16C);
        static readonly Color Hemp = Draft.C(0xA48560);
        static readonly Color[] Basalt = {
            Draft.C(0x67695E), Draft.C(0x777970), Draft.C(0x525C58),
            Draft.C(0x676F67), Draft.C(0x7C7668), Draft.C(0x5C6663),
        };

        public static GameObject Build()
        {
            var root = new GameObject("Jeju choga");
            var random = new Random(58173);
            var walls = new Draft();
            walls.Box(new Vector3(0, .99f, .31f), new Vector3(6.67f, 1.98f, 2.95f), Draft.C(0x746C59));
            // The side walls end at the porch, which shelters the sliding timber doors.
            StoneWall(walls, new Vector3(-3.34f, 0, .09f), Vector3.forward, Vector3.left, 3.48f, random);
            StoneWall(walls, new Vector3(3.34f, 0, .09f), Vector3.back, Vector3.right, 3.48f, random);
            StoneWall(walls, new Vector3(0, 0, 1.80f), Vector3.right, Vector3.forward, 6.68f, random);
            foreach (float side in new [] { -1f, 1f })
            {
                float x = side * 3.03f;
                walls.Box(new Vector3(x, 1.0f, -1.33f), new Vector3(.63f, 2.0f, .72f), Draft.C(0x68695C));
                StoneWall(walls, new Vector3(x, 0, -1.68f), Vector3.right, Vector3.back, .63f, random);
            }
            walls.Bake("Choga_Basalt", root.transform);
            BoxCollider house = root.AddComponent<BoxCollider>();
            house.center = new Vector3(0, .99f, .06f);
            house.size = new Vector3(6.84f, 1.98f, 3.65f);

            var porch = new Draft();
            Porch(porch, random);
            porch.Bake("Choga_MaruAndJoinery", root.transform);

            var roof = new Draft();
            MakeRoof(roof, random);
            roof.Bake("Choga_ThatchedRoof", root.transform);

            var jars = new Draft();
            Onggi(jars, new Vector3(-3.38f, 0, -2.15f), .54f, .79f, 0x955C46);
            Onggi(jars, new Vector3(-2.67f, 0, -2.40f), .36f, .58f, 0x705547);
            Onggi(jars, new Vector3(-3.13f, 0, -2.73f), .30f, .43f, 0xAE7653);
            var pottery = jars.Bake("Choga_Onggi", root.transform);
            BoxCollider pots = pottery.AddComponent<BoxCollider>();
            pots.center = new Vector3(-3.08f, .32f, -2.38f);
            pots.size = new Vector3(1.37f, .64f, .94f);
            return root;
        }

        static float Range(Random r, float min, float max) => Mathf.Lerp(min, max, (float)r.NextDouble());

        static Color Shade(Color c, float multiplier)
        {
            return new Color(c.r * multiplier, c.g * multiplier, c.b * multiplier, 1);
        }

        static void StoneWall(Draft d, Vector3 middle, Vector3 along, Vector3 face, float width, Random r)
        {
            float y = .03f;
            for (int course = 0; course < 5; course++)
            {
                float h = course == 4 ? .42f : .395f;
                float left = -width * .5f;
                while (left < width * .5f - .015f)
                {
                    float w = Range(r, .51f, .89f);
                    if (course % 2 == 1 && left == -width * .5f) w *= .68f;
                    float remaining = width * .5f - left;
                    w = remaining < w + .23f ? remaining : w;
                    var pos = middle + along * (left + w * .5f) + Vector3.up * (y + h * .5f);
                    Stone(d, pos, along, face, w * .98f, h * .97f, r);
                    left += w;
                }
                y += h;
            }
        }

        static void Stone(Draft d, Vector3 c, Vector3 side, Vector3 normal, float w, float h, Random r)
        {
            var outline = new [] {new Vector2(-.47f,-.27f),new Vector2(-.32f,-.49f),
                new Vector2(.27f,-.5f),new Vector2(.48f,-.28f),new Vector2(.49f,.23f),
                new Vector2(.24f,.47f),new Vector2(-.27f,.48f),new Vector2(-.49f,.19f)};
            var outer = new Vector3[outline.Length];
            var inner = new Vector3[outline.Length];
            float depth = Range(r, .073f,.14f);
            Color tint = Basalt[r.Next(Basalt.Length)];
            for (int j = 0; j < outline.Length; j++)
            {
                Vector2 p = outline[j] + new Vector2(Range(r,-.024f,.024f), Range(r,-.065f,.065f));
                Vector3 tangent = side * (p.x * w) + Vector3.up * (p.y * h);
                outer[j] = c + tangent - normal * .05f;
                inner[j] = c + tangent * Range(r,.75f,.9f) + normal * depth;
            }
            Vector3 centre = c + normal * (depth+.025f);
            for (int j=0;j<outline.Length;j++)
            {
                int n=(j+1)%outline.Length;
                Vector3 rim=(outer[j]+outer[n])*.5f-c;
                d.Quad(outer[j],outer[n],inner[n],inner[j],Shade(tint, Range(r,.92f,1.04f)), rim+normal*.1f);
                d.Tri(inner[j],inner[n],centre, Shade(tint,Range(r,.95f,1.035f)), normal);
            }
            if (r.NextDouble()<.22)
            {
                Vector3 mark = centre + normal*.005f + side*(w*.1f);
                d.Tri(mark-side*.040f,mark+Vector3.up*.027f,mark+side*.045f, Shade(tint,.68f),normal);
            }
        }

        static void Porch(Draft d, Random random)
        {
            var dark = Draft.C(0x593E31);
            var sunwood = Draft.C(0xBF956A);
            d.Box(new Vector3(0,.12f,-1.75f),new Vector3(5.82f,.23f,.89f),dark);
            for(int i=0;i<22;i++)
                d.Box(new Vector3(-2.80f+i*.267f,.255f,-1.77f), new Vector3(.256f,.074f,.92f),Shade(sunwood,Range(random,.88f,1.06f)));
            d.Box(new Vector3(0,.183f,-2.226f),new Vector3(5.92f,.14f,.08f),Rail);
            for(int i=0;i<3;i++)
            {
                float x = -1.62f + i*.83f;
                d.Ball(new Vector3(x,.095f,-2.37f),new Vector3(.53f,.112f,.30f),Basalt[i+1],8,3);
            }
            // Paper windows above solid lower panels, with paired handles on the doors.
            d.Box(new Vector3(0,1.11f,-1.18f),new Vector3(5.89f,1.72f,.12f),dark);
            for(int panel=0;panel<6;panel++)
            {
                float x=-2.385f+panel*.954f;
                if (panel==3)
                {
                    // A slid-open door reveals the cooler interior behind the porch.
                    d.Box(new Vector3(x,1.12f,-1.251f),new Vector3(.89f,1.59f,.037f),Draft.C(0x4B403A));
                    d.Box(new Vector3(x+.27f,1.10f,-1.289f),new Vector3(.32f,1.59f,.045f),Timber);
                    for(int k=0;k<3;k++)
                        d.Box(new Vector3(x+.16f+k*.105f,1.10f,-1.32f),new Vector3(.096f,1.47f,.024f),Shade(Rail,.86f+k*.035f));
                    d.Box(new Vector3(x-.17f,.37f,-1.292f),new Vector3(.54f,.07f,.10f),sunwood);
                    continue;
                }
                d.Box(new Vector3(x,1.10f,-1.258f),new Vector3(.915f,1.59f,.064f),Rail);
                d.Box(new Vector3(x,1.40f,-1.299f),new Vector3(.785f,.82f,.023f), Draft.C(panel<2 ? 0xE3CF9D : 0xF2DDB3));
                for(int k=-1;k<=1;k++)
                    d.Box(new Vector3(x+k*.204f,1.40f,-1.323f),new Vector3(.032f,.84f,.024f),Timber);
                for(int k=-1;k<=1;k++)
                    d.Box(new Vector3(x,1.40f+k*.212f,-1.332f),new Vector3(.81f,.032f,.024f),Timber);
                for(int j=0;j<3;j++)
                    d.Box(new Vector3(x+(j-1)*.262f,.655f,-1.3f),new Vector3(.247f,.56f,.034f),Shade(sunwood,Range(random,.80f,.96f)));
                if(panel==2||panel==3)
                    d.Rod(new Vector3(x+(panel==2?.32f:-.32f),.92f,-1.347f),
                        new Vector3(x+(panel==2?.32f:-.32f),1.06f,-1.347f),.021f,.021f,dark,5);
            }
            for(int i=0;i<3;i++)
            {
                float x = i==0 ? -2.85f : i==1 ? -.93f : 2.82f;
                d.Ball(new Vector3(x,.32f,-1.89f),new Vector3(.20f,.11f,.18f),Basalt[0],8,3);
                d.Rod(new Vector3(x,.32f,-1.89f),new Vector3(x,2.13f,-1.89f),.095f,.087f,Timber,7);
            }
            d.Box(new Vector3(0,1.97f,-1.88f),new Vector3(6.63f,.21f,.18f),Timber);
            for(int i=0;i<14;i++)
            {
                float x=-3.15f+i*.485f;
                d.Rod(new Vector3(x,2.10f,-1.42f),new Vector3(x,2.00f,-2.14f),.073f,.064f,Rail,6);
            }
        }

        // u traverses a rounded rectangle. Keeping the same u through the roof profile
        // carries each straw seam smoothly over the rounded hip and into the ridge.
        static Vector3 RoofPoint(float u, float t, float lift=0)
        {
            u = Mathf.Repeat(u, 64f);
            float hx = Mathf.Lerp(3.86f,1.20f, Mathf.Pow(t,1.45f));
            float hz = Mathf.Lerp(2.31f,.13f, Mathf.Pow(t,1.13f));
            float r = Mathf.Lerp(1.04f,.115f,t);
            Vector2 p;
            if (u<18) p=new Vector2(Mathf.Lerp(-hx+r,hx-r,u/18),-hz);
            else if (u<21) p=Arc(hx-r,-hz+r,r,-90f,0f,(u-18)/3);
            else if (u<29) p=new Vector2(hx,Mathf.Lerp(-hz+r,hz-r,(u-21)/8));
            else if (u<32) p=Arc(hx-r,hz-r,r,0,90,(u-29)/3);
            else if (u<50) p=new Vector2(Mathf.Lerp(hx-r,-hx+r,(u-32)/18),hz);
            else if (u<53) p=Arc(-hx+r,hz-r,r,90,180,(u-50)/3);
            else if (u<61) p=new Vector2(-hx,Mathf.Lerp(hz-r,-hz+r,(u-53)/8));
            else p=Arc(-hx+r,-hz+r,r,180,270,(u-61)/3);
            float scallop=(.023f*Mathf.Cos(u*1.9f)+.018f*Mathf.Sin(u*.57f))*(1-t);
            return new Vector3(p.x,2.19f+1.65f*t+.36f*Mathf.Sin(t*Mathf.PI)+scallop+lift,p.y);
        }

        static Vector2 Arc(float x,float z,float radius,float a,float b,float t)
        {
            float angle=Mathf.Lerp(a,b,t)*Mathf.Deg2Rad;
            return new Vector2(x+Mathf.Cos(angle)*radius,z+Mathf.Sin(angle)*radius);
        }

        static void MakeRoof(Draft d, Random random)
        {
            const int count=64, bands=6;
            Color strawEdge=Draft.C(0xBF9054);
            for(int i=0;i<count;i++)
            {
                Vector3 a=RoofPoint(i,0), b=RoofPoint(i+1,0);
                Vector3 side=new Vector3((a.x+b.x)*.18f,0,(a.z+b.z)*.4f).normalized;
                Vector3 c=a-Vector3.up*Range(random,.13f,.21f)-side*.04f;
                Vector3 e=b-Vector3.up*Range(random,.13f,.21f)-side*.04f;
                d.Quad(a,b,e,c,Shade(strawEdge,Range(random,.86f,1.08f)),side);
                d.Tri(c,e,new Vector3(0,2.05f,0),Shade(strawEdge,.69f), Vector3.down);
                for(int j=0;j<bands;j++)
                {
                    float low=(float)j/bands, high=(float)(j+1)/bands;
                    Vector3 outward = (side*(1f-low*.65f)+Vector3.up).normalized;
                    d.Quad(RoofPoint(i,low),RoofPoint(i+1,low),RoofPoint(i+1,high),RoofPoint(i,high),
                        Shade(Straw,Range(random,.962f,1.032f)*Mathf.Lerp(.93f,1.09f,low)),outward);
                }
                d.Tri(RoofPoint(i,1),RoofPoint(i+1,1),new Vector3(0,3.85f,0),Shade(Straw,1.05f),Vector3.up);
            }
            // Individual cut sheaves hang over the deep fascia. Each has a rounded
            // shoulder, split tips and a bright leading edge against the darker cut.
            for (int i=0;i<116;i++)
            {
                float u=i*64f/116f+Range(random,-.075f,.075f);
                float width=Range(random,.27f,.43f);
                Vector3 crown=RoofPoint(u,.095f,.045f);
                Vector3 l=RoofPoint(u-width,0,.012f), r=RoofPoint(u+width,0,.012f);
                Vector3 outward=new Vector3(crown.x*.19f,0,crown.z*.53f).normalized;
                Vector3 mid=RoofPoint(u+.035f,.014f,.018f)+outward*.056f;
                Vector3 end=mid+Vector3.down*Range(random,.19f,.315f)+outward*Range(random,.01f,.063f);
                Color c=Shade(strawEdge,Range(random,.91f,1.13f));
                d.Tri(l,crown,mid,c,outward+Vector3.up);
                d.Tri(mid,crown,r,Shade(c,1.055f),outward+Vector3.up);
                d.Tri(l,mid,end,Shade(c,.91f),outward);
                d.Tri(mid,r,end,Shade(c,1.045f),outward);
                d.Tri(mid,r,end+Vector3.Lerp(r-mid,Vector3.zero,.32f)+Vector3.up*.028f,Shade(c,.88f),outward);
            }

            // Overlapping ridges run down the slope like aligned handfuls of
            // reed. Stagger their lengths and starts to break up the weave.
            for(int i=0;i<560;i++)
            {
                float u=Range(random,0,64), foot=Range(random,.005f,.89f);
                float tip=Mathf.Min(.995f,foot+Range(random,.09f,.27f));
                float middle=Mathf.Lerp(foot,tip,.48f), w=Range(random,.022f,.091f);
                float bend=Range(random,-.14f,.14f);
                Vector3 low=RoofPoint(u,foot,.036f);
                Vector3 high=RoofPoint(u+bend,tip,.039f);
                Vector3 left=RoofPoint(u-w+bend*.5f,middle,.034f);
                Vector3 right=RoofPoint(u+w+bend*.5f,middle,.036f);
                Vector3 ridge=RoofPoint(u+bend*.5f,middle,Range(random,.042f,.066f));
                Color streak=Shade(Straw,Range(random,.78f,1.13f));
                d.Tri(low,left,ridge,streak,Vector3.up);
                d.Tri(ridge,left,high,streak,Vector3.up);
                d.Tri(low,ridge,right,Shade(streak,1.055f),Vector3.up);
                d.Tri(right,ridge,high,Shade(streak,1.055f),Vector3.up);
            }
            // Widely spaced hemp lashings cross on the hips. A loose belt gathers
            // the reed ends at the eaves, while their tips remain visible below.
            for(int i=0;i<count;i++)
            {
                float l=.085f+.012f*Mathf.Sin(i*.62f),r=.085f+.012f*Mathf.Sin((i+1)*.62f);
                d.Quad(RoofPoint(i,l-.009f,.076f),RoofPoint(i+1,r-.009f,.076f),
                    RoofPoint(i+1,r+.009f,.076f),RoofPoint(i,l+.009f,.076f),Hemp,Vector3.up);
            }
            for(int direction=-1;direction<=1;direction+=2)
                for(int i=0;i<10;i++)
                    for(int j=0;j<8;j++)
                    {
                        float t=j/8f,q=(j+1)/8f;
                        float from=i*6.4f+direction*(t*3.8f+.11f*Mathf.Sin(t*5+i));
                        float to=i*6.4f+direction*(q*3.8f+.11f*Mathf.Sin(q*5+i));
                        d.Quad(RoofPoint(from-.045f,t,.079f),RoofPoint(from+.045f,t,.079f),
                            RoofPoint(to+.045f,q,.079f),RoofPoint(to-.045f,q,.079f),Hemp,Vector3.up);
                    }
            // A short cylindrical straw cap ties the two thick roof slopes together.
            for (int i=0;i<7;i++)
            {
                float z=(i-3)*.039f;
                float y=3.857f+.075f*Mathf.Sin(Mathf.PI*i/6f);
                d.Rod(new Vector3(-1.29f+Range(random,-.09f,.02f),y,z),
                    new Vector3(1.29f+Range(random,-.02f,.09f),y,z),.048f,.042f,
                    Shade(Straw,Range(random,.91f,1.09f)),6);
            }
            for (int i=0;i<5;i++)
            {
                float x=-1.14f+i*.57f;
                Vector3 prev=new Vector3(x,3.83f,-.158f);
                for(int k=1;k<=6;k++)
                {
                    float angle=k*Mathf.PI/6f;
                    Vector3 next=new Vector3(x,3.83f+.151f*Mathf.Sin(angle),-.158f*Mathf.Cos(angle));
                    d.Rod(prev,next,.015f,.015f,Hemp,4);
                    prev=next;
                }
            }
        }

        static void Onggi(Draft d,Vector3 foot,float radius,float height,int hex)
        {
            // A shouldered storage jar with a raised collar, lipped lid and slip stripe.
            float[] y={0,.08f,.29f,.55f,.72f,.85f,.90f,.935f,.95f,1f};
            float[] r={.56f,.66f,.93f,1,.92f,.68f,.65f,.79f,.76f,.22f};
            Color clay=Draft.C(hex);
            const int sides=12;
            for(int j=0;j<y.Length-1;j++)
            {
                Color band=j>=6?Shade(clay,.74f):Shade(clay,j==4?1.11f:1);
                for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                    Vector3 n=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),
                        m=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    Vector3 lower=foot+Vector3.up*(y[j]*height),upper=foot+Vector3.up*(y[j+1]*height);
                    d.Quad(lower+n*(r[j]*radius),lower+m*(r[j]*radius),upper+m*(r[j+1]*radius),
                        upper+n*(r[j+1]*radius),band,n+m+Vector3.up*(r[j]-r[j+1]));
                    if(j==y.Length-2)
                        d.Tri(upper+n*(r[j+1]*radius),upper+m*(r[j+1]*radius),upper,band,Vector3.up);
                }
            }
        }
    }
}
