using System.Collections.Generic;
using UnityEngine;

namespace Jeju.Yard
{
    // All instances share these small, vertex-painted meshes and the animal's existing paint material.
    internal sealed class YardAccessoryMeshes
    {
        Mesh band;
        readonly Dictionary<string,Mesh> capes = new Dictionary<string,Mesh>();
        public Mesh Headband => band != null ? band : (band = MakeHeadband());

        public Mesh Cape(string species)
        {
            if (!capes.TryGetValue(species, out var cape))
            {
                cape = MakeCape(species);
                capes.Add(species,cape);
            }
            return cape;
        }

        public Mesh BirdShoulder(string species, int side)
        {
            string key = species + (side < 0 ? " left shoulder" : " right shoulder");
            if (!capes.TryGetValue(key, out var shoulder))
            {
                shoulder = MakeBirdShoulder(species == "gull", side);
                capes.Add(key, shoulder);
            }
            return shoulder;
        }

        public void Dispose()
        {
            if (band != null) Object.Destroy(band);
            foreach (var cape in capes.Values) Object.Destroy(cape);
        }

        static Mesh MakeHeadband()
        {
            var d = new Geometry();
            const int segments = 16;
            Vector3 Arc(int i, float radius, float z)
            {
                float a = Mathf.Lerp(-102f,102f,(float)i/segments) * Mathf.Deg2Rad;
                return new Vector3(radius*Mathf.Sin(a), radius*Mathf.Cos(a), z);
            }
            for (int i = 0; i < segments; i++)
            {
                int j=i+1;
                var radial=Arc(i,1,0)+Arc(j,1,0);
                // A dark green curved ribbon with a raised golden edge along the brow.
                d.Quad(Arc(i,1.034f,-.12f), Arc(j,1.034f,-.12f), Arc(j,1.034f,.093f), Arc(i,1.034f,.093f), 0x507667,radial);
                d.Quad(Arc(i,1.034f,.093f), Arc(j,1.034f,.093f), Arc(j,1.034f,.131f), Arc(i,1.034f,.131f),0xF0C773,radial);
                d.Quad(Arc(i,1.034f,.131f), Arc(j,1.034f,.131f), Arc(j,.977f,.131f), Arc(i,.977f,.131f),0x966B48,Vector3.forward);
                d.Quad(Arc(i,.977f,-.12f), Arc(j,.977f,-.12f), Arc(j,1.034f,-.12f), Arc(i,1.034f,-.12f),0x38564C,Vector3.back);
                d.Quad(Arc(i,.977f,-.12f), Arc(i,.977f,.131f), Arc(j,.977f,.131f), Arc(j,.977f,-.12f),0x38564C,-radial);
            }
            Star(d,new Vector3(0,1.26f,.15f),.425f,0);
            Star(d,new Vector3(-.55f,1.00f,.13f),.23f,14f);
            Star(d,new Vector3(.55f,1.00f,.13f),.23f,-14f);
            return d.Mesh("Star headband");
        }

        static void Star(Geometry d, Vector3 center, float radius, float tilt)
        {
            Vector3 Corner(int i)
            {
                float a=(90f + tilt + 36f*(i%10))*Mathf.Deg2Rad;
                return center+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius*(i%2==0 ? 1f : .45f);
            }
            var front = center + Vector3.forward*(radius*.33f);
            var depth = Vector3.back*(radius*.22f);
            for(int i=0;i<10;i++)
            {
                var a=Corner(i); var b=Corner(i+1);
                d.Tri(front,a,b,i%2==0 ? 0xFFE398 : 0xF4B84F, Vector3.forward);
                d.Tri(center+depth,b+depth,a+depth,0xDC9542,Vector3.back);
                d.Quad(a,b,b+depth,a+depth,0xC48039,(a+b)*.5f-center);
            }
        }

        // Each station is (forward Z, crown Y, half-width, vertical radius) on the
        // animated torso. The short dolphin mantle ends ahead of the dorsal fin.
        static Vector4 S(float z,float crown,float width,float fall) => new Vector4(z,crown,width,fall);

        // The proximal wing covers the flank at rest, then sweeps outward in flight.
        // Fit the cape's short side gores to its first three feather stations, stopping
        // before the moving wrist. The same local surface fits both poses.
        static Mesh MakeBirdShoulder(bool gull, int side)
        {
            // Span, leading Z, trailing Z and upper arch, matching the wing designer.
            Vector4 root = gull ? new Vector4(0,.08f,-.24f,0) : new Vector4(0,.04f,-.18f,0);
            Vector4 middle = gull ? new Vector4(.22f,.15f,-.27f,.055f) : new Vector4(.11f,.10f,-.19f,.032f);
            Vector4 wrist = gull ? new Vector4(.49f,.21f,-.21f,.11f) : new Vector4(.22f,.13f,-.16f,.053f);
            float length = wrist.x * .88f;
            Vector3 Point(float along, float across)
            {
                float span = along * length;
                Vector4 section = span <= middle.x
                    ? Vector4.Lerp(root, middle, span / middle.x)
                    : Vector4.Lerp(middle, wrist, (span - middle.x) / (wrist.x - middle.x));
                // Upper coverts are arched along their chord. Leave a slim feather edge
                // visible and give the back hem a rounded, narrowing profile.
                float lead = Mathf.Lerp(.11f,.22f,along);
                float trail = Mathf.Lerp(.92f,.77f,along);
                float u = Mathf.Lerp(lead,trail,across);
                float chord = section.y-section.z;
                float height = section.w + chord*.12f*Mathf.Sin(Mathf.PI*u) + (gull ? .011f : .008f);
                return new Vector3(side*section.x, height, Mathf.Lerp(section.y,section.z,u));
            }
            var d = new Geometry();
            float[] seams = {0f, middle.x / length, .82f, .90f, 1f};
            for(int r=0;r<seams.Length-1;r++)
                for(int col=0;col<5;col++)
                {
                    float a=col/5f, b=(col+1)/5f;
                    var p=Point(seams[r],a); var q=Point(seams[r],b);
                    var s=Point(seams[r+1],a); var t=Point(seams[r+1],b);
                    int color = r == 2 ? 0xECC17A : col == 0 ? 0xB93343 : col < 3 ? 0xD74C4D : 0xCC4047;
                    d.Quad(p,q,t,s,color,Vector3.up);
                    d.Quad(p,q,t,s,0x8F2D39,Vector3.down);
                }
            return d.Mesh("Red cape shoulder");
        }

        static Mesh MakeCape(string species)
        {
            Vector4[] stations;
            switch(species)
            {
                case "corgi":
                    stations=new[]{S(.32f,.811f,.27f,.269f),S(.19f,.79f,.295f,.25f),S(-.08f,.747f,.312f,.235f),S(-.37f,.751f,.305f,.25f)}; break;
                case "tabby":
                    stations=new[]{S(.25f,.705f,.18f,.205f),S(.13f,.685f,.189f,.20f),S(-.06f,.655f,.193f,.19f),S(-.30f,.697f,.206f,.21f)}; break;
                case "gull":
                    stations=new[]{S(.18f,.187f,.17f,.175f),S(.10f,.200f,.182f,.188f),S(-.095f,.201f,.184f,.193f),S(-.266f,.16f,.153f,.153f)}; break;
                case "sparrow":
                    stations=new[]{S(.085f,.159f,.143f,.151f),S(.055f,.168f,.155f,.163f),S(-.057f,.166f,.158f,.162f),S(-.165f,.129f,.127f,.123f)}; break;
                default: // dolphin
                    stations=new[]{S(.70f,.375f,.286f,.278f),S(.57f,.392f,.324f,.312f),S(.36f,.394f,.36f,.327f),S(.16f,.382f,.354f,.324f)}; break;
            }

            var d=new Geometry();
            const int columns=8;
            Vector3 Point(Vector4 s,int column,float rear)
            {
                float side=column/(columns*.5f)-1f;
                float a=side*1.40f;
                float fold=Mathf.Abs(side)==.5f ? .055f : 0f;
                return new Vector3(
                    Mathf.Sin(a)*s.z*(1f+fold*rear),
                    s.y-s.w*(1f-Mathf.Cos(a))+s.w*fold*rear,
                    s.x+Mathf.Abs(side)*s.z*.13f-(1f-Mathf.Abs(side))*s.z*.16f*rear);
            }
            void Panel(Vector4 near, Vector4 far, float progressA, float progressB, bool braid)
            {
                for(int c=0;c<columns;c++)
                {
                    var a=Point(near,c,progressA); var b=Point(near,c+1,progressA);
                    var e=Point(far,c,progressB); var f=Point(far,c+1,progressB);
                    var outwards=new Vector3((a.x+b.x)/near.z,1,0);
                    int color=braid ? 0xECC17A : c%4==1 ? 0xCC4047 : c%4==2 ? 0xD74C4D : 0xB93343;
                    d.Quad(a,b,f,e,color,outwards);
                    d.Quad(a,b,f,e,0x8F2D39,-outwards);
                }
            }
            Panel(stations[0],stations[1],0,.23f,false);
            Panel(stations[1],stations[2],.23f,.60f,false);
            var trimA=Vector4.Lerp(stations[2],stations[3],.79f);
            var trimB=Vector4.Lerp(stations[2],stations[3],.89f);
            Panel(stations[2],trimA,.60f,.916f,false);
            Panel(trimA,trimB,.916f,.956f,true);
            Panel(trimB,stations[3],.956f,1,false);
            return d.Mesh("Red cape - "+species);
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Color32> colors=new List<Color32>();
            readonly List<int> indices=new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e, int rgb, Vector3 outward)
            {
                Tri(a,b,c,rgb,outward);
                Tri(a,c,e,rgb,outward);
            }

            public void Tri(Vector3 a,Vector3 b,Vector3 c,int rgb,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0f) (b,c)=(c,b);
                Color32 color=new Color32((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb,255);
                int start=vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                colors.Add(color); colors.Add(color); colors.Add(color);
                indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            }

            public Mesh Mesh(string name)
            {
                var mesh=new Mesh {name=name};
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(indices,0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
