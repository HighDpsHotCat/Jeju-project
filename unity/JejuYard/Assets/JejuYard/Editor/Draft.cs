using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jeju.Yard.EditorArt
{
    // The authors work in metres. Finished meshes share a single painted material.
    public sealed class Draft
    {
        const string Art = "Assets/JejuYard/Art/";
        readonly List<Vector3> points = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color32> paint = new List<Color32>();
        readonly List<int> indices = new List<int>();
        readonly Dictionary<(Vector3, Vector3, Color32), int> vertices = new Dictionary<(Vector3, Vector3, Color32), int>();

        public static Color C(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        int Vertex(Vector3 p, Vector3 n, Color32 color)
        {
            var key = (p, n, color);
            if (vertices.TryGetValue(key, out int existing)) return existing;
            int id = points.Count;
            vertices.Add(key, id); points.Add(p); normals.Add(n); paint.Add(color);
            return id;
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, Vector3 outward)
        {
            Vector3 n = Vector3.Cross(b-a, c-a);
            if (n.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(n, outward) < 0f) { (b,c) = (c,b); n = -n; }
            n.Normalize();
            indices.Add(Vertex(a,n,col)); indices.Add(Vertex(b,n,col)); indices.Add(Vertex(c,n,col));
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e, Color col, Vector3 outward)
        {
            Tri(a,b,c,col,outward); Tri(a,c,e,col,outward);
        }

        public void Box(Vector3 center, Vector3 size, Color col)
        {
            Vector3 h = size * .5f;
            void Face(Vector3 n, Vector3 side, Vector3 up)
            {
                Vector3 p = center + Vector3.Scale(h, n);
                Vector3 s = Vector3.Scale(h, side), u = Vector3.Scale(h, up);
                Quad(p-s-u,p+s-u,p+s+u,p-s+u,col,n);
            }
            Face(Vector3.right,Vector3.forward,Vector3.up); Face(Vector3.left,Vector3.forward,Vector3.up);
            Face(Vector3.forward,Vector3.right,Vector3.up); Face(Vector3.back,Vector3.right,Vector3.up);
            Face(Vector3.up,Vector3.right,Vector3.forward); Face(Vector3.down,Vector3.right,Vector3.forward);
        }

        public void Ball(Vector3 center, Vector3 radii, Color col, int segments=8, int rings=4)
        {
            segments = Mathf.Max(3,segments); rings = Mathf.Max(2,rings);
            Vector3 Point(int row, int side)
            {
                if (row == 0) return center - Vector3.up*radii.y;
                if (row == rings) return center + Vector3.up*radii.y;
                float phi = row*Mathf.PI/rings-Mathf.PI*.5f;
                float theta = ((side % segments) + ((row & 1) == 0 ? .35f : 0f))*Mathf.PI*2f/segments;
                return center+Vector3.Scale(radii,new Vector3(Mathf.Cos(theta)*Mathf.Cos(phi),Mathf.Sin(phi),Mathf.Sin(theta)*Mathf.Cos(phi)));
            }
            for(int j=0;j<rings;j++)
                for(int i=0;i<segments;i++)
                {
                    Vector3 a=Point(j,i), b=Point(j,i+1), c=Point(j+1,i+1), d=Point(j+1,i);
                    Tri(a,b,c,col,(a+b+c)/3f-center); Tri(a,c,d,col,(a+c+d)/3f-center);
                }
        }

        public void Rod(Vector3 start, Vector3 end, float radiusA, float radiusB, Color col, int sides=6)
        {
            Vector3 axis = (end-start).normalized;
            if (axis.sqrMagnitude < .5f) return;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) > .9f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(axis,u);
            Vector3 Ring(int i)
            {
                float t = (i%sides)*2*Mathf.PI/sides;
                return u*Mathf.Cos(t)+v*Mathf.Sin(t);
            }
            for(int i=0;i<sides;i++)
            {
                Vector3 r=Ring(i), s=Ring(i+1), a=start+radiusA*r, b=start+radiusA*s, c=end+radiusB*s, d=end+radiusB*r;
                Quad(a,b,c,d,col,r+s); Tri(start,b,a,col,-axis); Tri(end,d,c,col,axis);
            }
        }

        public GameObject Bake(string uniqueName, Transform parent)
        {
            var mesh = new Mesh {name = uniqueName, indexFormat = points.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16};
            mesh.SetVertices(points); mesh.SetNormals(normals); mesh.SetColors(paint); mesh.SetTriangles(indices,0);
            mesh.RecalculateBounds(); mesh.Optimize();
            string path = Art+uniqueName+".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh,path); saved=mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Art+"Paint.mat");
            if (material == null)
            {
                var shader = Shader.Find("Jeju/Island Paint");
                if (shader == null || !shader.isSupported && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                    throw new System.InvalidOperationException("Jeju/Island Paint shader unavailable");
                material=new Material(shader) {name="Paint"}; AssetDatabase.CreateAsset(material, Art+"Paint.mat");
            }
            var obj = new GameObject(uniqueName);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh=saved;
            obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            return obj;
        }
    }
}
