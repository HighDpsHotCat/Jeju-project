using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Jeju.Yard.EditorArt
{
    // Export the yard meshes themselves for the map's read-only model viewer.
    public static class ReviewModelExport
    {
        public static void Run()
        {
            var directory=Environment.GetEnvironmentVariable("JEJU_REVIEW_MODEL_OUTPUT");
            Directory.CreateDirectory(directory);
            foreach(var key in new[]{"dolphin","leopard_cat","rhino_beetle"}) {
                var root=new GameObject("Review");
                try {
                    if(key=="dolphin") {
                        foreach(var name in new[]{"BottlenoseDolphin","BottlenoseFlukes"}) {
                            var part=new GameObject(name);
                            part.transform.SetParent(root.transform,false);
                            if(name=="BottlenoseFlukes") part.transform.localPosition=new Vector3(0,-.063f,-.87f);
                            part.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/JejuYard/Art/"+name+".asset");
                        }
                    } else UnityEngine.Object.Instantiate(Resources.Load<GameObject>("YardWildlife/"+key),root.transform,false);
                    var points=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color32>();
                    var bounds=new Bounds();
                    foreach(var mf in root.GetComponentsInChildren<MeshFilter>()) {
                        var m=mf.sharedMesh;var verts=m.vertices;var ns=m.normals;var cs=m.colors32;
                        var matrix=mf.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                        foreach(int i in m.triangles) {
                            var v=matrix.MultiplyPoint3x4(verts[i]);
                            if(points.Count==0) bounds=new Bounds(v,Vector3.zero); else bounds.Encapsulate(v);
                            points.Add(v);normals.Add(normalMatrix.MultiplyVector(ns[i]).normalized); colors.Add(cs[i]);
                        }
                    }
                    if(points.Count<30) throw new InvalidDataException(key);
                    float scale=1f/bounds.extents.magnitude;
                    using(var buffer=new MemoryStream()) using(var b=new BinaryWriter(buffer)) {
                        for(int i=0;i<points.Count;i++) {
                            var v=(points[i]-bounds.center)*scale;
                            for(int k=0;k<3;k++) b.Write((short)Mathf.RoundToInt(v[k]*32767));
                            for(int k=0;k<3;k++) b.Write((sbyte)Mathf.RoundToInt(normals[i][k]*127));
                            var c=colors[i];b.Write(c.r);b.Write(c.g);b.Write(c.b);
                        }
                        File.WriteAllText(Path.Combine(directory,key+".json"),"\""+Convert.ToBase64String(buffer.ToArray())+"\"");
                    }
                    Debug.Log("Review geometry "+key+": "+points.Count/3+" triangles");
                } finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
    }
}
