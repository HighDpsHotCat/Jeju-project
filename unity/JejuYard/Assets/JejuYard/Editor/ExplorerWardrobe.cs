using UnityEditor;
using UnityEngine;

namespace Jeju.Yard.EditorArt
{
    public static partial class Explorer
    {
        static readonly Color Navy = Draft.C(0x344B60);
        static readonly Color Cream = Draft.C(0xFFF1C9);
        static readonly Color Coral = Draft.C(0xD57565);
        static readonly Color Rib = Draft.C(0xAB584F);

        // The scene keeps a prefab instance and references its root for the follow camera.
        // Updating its contents in place preserves that file ID and any scene overrides.
        [MenuItem("Jeju/Art/Rebuild explorer wardrobe")]
        public static void RebuildExplorerPrefab()
        {
            const string path = "Assets/JejuYard/Art/Explorer.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var walker = prefab.GetComponent<YardWalker>();
                var combined = walker.torso.Find("Explorer_Coat_and_sunhat");
                if (combined != null) Object.DestroyImmediate(combined.gameObject);
                Dress(walker);
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        static void Dress(YardWalker walker)
        {
            var bodyScale = V(1.17f, .84f, 1.14f);
            var bodyShift = Vector3.down * .04f;
            var headScale = V(1.26f, 1.19f, 1.22f);
            var headShift = Vector3.up * (1.102f - 1.242f * headScale.y);
            foreach (int side in new[] { -1, 1 })
            {
                var hip = side < 0 ? walker.leftLeg : walker.rightLeg;
                hip.localScale = V(1.10f, .72f, 1.08f);
                hip.localPosition = V(side * .133f, .51f * hip.localScale.y, 0f);
                var shoulder = side < 0 ? walker.leftArm : walker.rightArm;
                shoulder.localPosition = Vector3.Scale(bodyScale, V(side * .211f, .868f, 0f)) + bodyShift;
                shoulder.localScale = V(1.08f, .79f, 1.05f);
            }

            var jacket = new Draft(); Jacket(jacket);
            var top = BakePart(jacket, "Explorer_FieldJacket", walker.torso);
            top.transform.localPosition = bodyShift;
            top.transform.localScale = bodyScale;
            var skin = new Draft(); Head(skin);
            var head = BakePart(skin, "Explorer_Head", walker.torso);
            head.transform.localPosition = headShift;
            head.transform.localScale = headScale;
            var sunhat = new Draft(); SunHat(sunhat);
            var hat = BakePart(sunhat, "Explorer_Sunhat", walker.torso);
            hat.transform.localPosition = headShift;
            hat.transform.localScale = headScale;

            var left = walker.leftArm.Find("Explorer_Sleeve_L").GetComponent<MeshFilter>();
            var right = walker.rightArm.Find("Explorer_Sleeve_R").GetComponent<MeshFilter>();
            var tops = new ExplorerAppearance.TopMeshes[ExplorerAppearance.TopCount];
            tops[0] = new ExplorerAppearance.TopMeshes { body = top.sharedMesh, leftSleeve = left.sharedMesh, rightSleeve = right.sharedMesh };
            tops[1] = NewTop("Explorer_Sailor", false, walker.torso);
            tops[2] = NewTop("Explorer_Knit", true, walker.torso);
            var bucket = new Draft(); BucketHat(bucket);
            var straw = new Draft(); StrawHat(straw);
            var hats = new[] { hat.sharedMesh, BakeOption(bucket, "Explorer_BucketHat", walker.torso), BakeOption(straw, "Explorer_StrawHat", walker.torso) };
            var appearance = walker.GetComponent<ExplorerAppearance>();
            if (appearance == null) appearance = walker.gameObject.AddComponent<ExplorerAppearance>();
            appearance.SetWardrobeMeshes(top, left, right, hat, tops, hats);
        }

        static MeshFilter BakePart(Draft draft, string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            var created = draft.Bake(name, parent).GetComponent<MeshFilter>();
            if (existing == null) return created;
            var kept = existing.GetComponent<MeshFilter>();
            kept.sharedMesh = created.sharedMesh;
            Object.DestroyImmediate(created.gameObject);
            return kept;
        }

        // Alternative meshes stay referenced by the prefab. Only the equipped
        // body, pair of sleeves and hat have renderers in the yard.
        static Mesh BakeOption(Draft draft, string name, Transform parent)
        {
            var baked = draft.Bake(name, parent);
            Mesh mesh = baked.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(baked);
            return mesh;
        }

        static ExplorerAppearance.TopMeshes NewTop(string name, bool knit, Transform parent)
        {
            var shirt = new Draft();
            if (knit) Sweater(shirt); else SailorShirt(shirt);
            shirt.Rod(V(0,.884f,0), V(0,1.06f,0), .087f, .083f, Skin, 9);
            return new ExplorerAppearance.TopMeshes
            {
                body = BakeOption(shirt, name, parent),
                leftSleeve = BakeOption(ShirtSleeve(-1, knit), name + "_L", parent),
                rightSleeve = BakeOption(ShirtSleeve(1, knit), name + "_R", parent)
            };
        }

        static Draft ShirtSleeve(int side, bool knit)
        {
            var d = new Draft();
            var elbow = V(side*.045f, -.184f, .022f);
            var cuff = V(side*.045f, knit ? -.230f : -.139f, knit ? .043f : .019f);
            Color wool = knit ? Coral : Cream;
            d.Ball(V(side*.009f, -.048f, 0), V(knit ? .090f : .085f, .101f, .096f), wool, 8, 4);
            d.Rod(V(0,-.045f,0), cuff, .084f, knit ? .067f : .080f, wool, 9);
            d.Rod(cuff, cuff + Vector3.up*.032f, knit ? .067f : .081f, knit ? .071f : .083f, knit ? Rib : Navy, 9);
            if (!knit) d.Rod(cuff, elbow, .057f, .052f, Skin, 8);
            d.Rod(knit ? cuff : elbow, V(side*.039f,-.271f,.058f), .050f, .047f, Skin, 8);
            d.Ball(V(side*.034f,-.270f,.061f),V(.061f,.068f,.061f),Skin,8,4);
            return d;
        }

        static void SailorShirt(Draft d)
        {
            var rings = new[] {
                V(.229f,.448f,.160f),V(.248f,.477f,.178f),V(.245f,.544f,.178f),
                V(.243f,.576f,.177f),V(.241f,.634f,.175f),V(.240f,.666f,.174f),
                V(.237f,.724f,.173f),V(.235f,.754f,.171f),V(.215f,.87f,.158f),V(.101f,.932f,.103f)
            };
            RingBody(d, rings, Cream, 12, new[] { Navy, Cream, Navy, Cream, Navy, Cream, Navy, Cream, Cream });
            // The sailor collar drapes over the shoulders into a V at the neck.
            for (int side=-1; side<=1; side+=2)
            {
                Vector3 Edge(float t, bool outer) => outer
                    ? Chest(rings,side*Mathf.Lerp(.191f,.010f,t),Mathf.Lerp(.862f,.767f,t))
                    : Chest(rings,side*Mathf.Lerp(.082f,.009f,t),Mathf.Lerp(.921f,.802f,t));
                Vector3 Stripe(float t) => Chest(rings,side*Mathf.Lerp(.111f,.014f,t),Mathf.Lerp(.895f,.792f,t)) + Vector3.forward*.003f;
                for (int step=0;step<4;step++)
                {
                    float lo=step*.25f, hi=(step+1)*.25f;
                    d.Quad(Edge(lo,false),Edge(lo,true),Edge(hi,true),Edge(hi,false),Navy,Vector3.forward);
                    d.Rod(Stripe(lo),Stripe(hi),.005f,.005f,Cream,5);
                    float x0=side*.143f*lo, x1=side*.143f*hi;
                    Vector3 Back(float x, float y) => Vector3.Scale(Chest(rings,x,y),V(1,1,-1));
                    d.Quad(Back(x0,.851f),Back(x1,.851f),Back(x1,.737f),Back(x0,.737f),Navy,Vector3.back);
                }
            }
            d.Ball(Chest(rings,0,.784f)+Vector3.forward*.009f,V(.027f,.024f,.014f),Coral,7,3);
            d.Tri(Chest(rings,0,.791f)+Vector3.forward*.018f,Chest(rings,-.026f,.722f),Chest(rings,.015f,.744f),Coral,Vector3.forward);
        }

        // Pockets, panels and the collar lie on the rounded surface of the garment.
        static Vector3 Chest(Vector3[] rings, float x, float y)
        {
            int band = 1;
            while (band < rings.Length-1 && rings[band].y < y) band++;
            Vector3 r=Vector3.Lerp(rings[band-1],rings[band],Mathf.InverseLerp(rings[band-1].y,rings[band].y,y));
            return V(x,y,r.z*Mathf.Sqrt(Mathf.Max(0f,1f - x*x/(r.x*r.x))) + .005f);
        }

        static void Sweater(Draft d)
        {
            var rings = new[] {
                V(.214f,.447f,.158f),V(.242f,.486f,.176f),V(.261f,.55f,.189f),
                V(.247f,.748f,.184f),V(.231f,.833f,.17f),V(.193f,.883f,.15f),V(.104f,.925f,.098f)
            };
            RingBody(d, rings, Coral, 12, new[] { Rib, Coral, Coral, Coral, Coral, Rib });
            // Knitted cream-and-apricot diamonds follow the chest all the way round.
            for (int i=0; i<12; i++)
            {
                float a = (i + .5f) * Mathf.PI / 6f;
                Vector3 Stitch(float angle, float height)
                {
                    float t = Mathf.InverseLerp(.748f,.833f,height);
                    return V(Mathf.Sin(angle)*Mathf.Lerp(.251f,.235f,t),height,Mathf.Cos(angle)*Mathf.Lerp(.188f,.174f,t));
                }
                var outer = V(Mathf.Sin(a),0,Mathf.Cos(a));
                var left = Stitch(a-.106f,.786f); var right = Stitch(a+.106f,.786f);
                d.Tri(left,right,Stitch(a,.827f),Cream,outer);
                d.Tri(left,right,Stitch(a,.752f),Draft.C(0xF3C58F),outer);
            }
        }

        static void HatShell(Draft d, Vector3[] rings, Color[] colors, Color crown, bool woven)
        {
            const int sides = 20;
            for (int i=0;i<sides;i++)
            {
                float a=i*2*Mathf.PI/sides, b=(i+1)*2*Mathf.PI/sides;
                var radial = V(Mathf.Sin((a+b)*.5f),0,Mathf.Cos((a+b)*.5f));
                for (int j=1;j<rings.Length;j++)
                {
                    Color color = colors[j-1];
                    if (woven && j <= 4 && (i+j)%3 == 0) color = Color.Lerp(color,Cream,.18f);
                    Vector3 normal = j == 1 ? Vector3.down : j == 2 ? radial : radial + Vector3.up*.5f;
                    d.Quad(HatPoint(rings[j-1],a),HatPoint(rings[j-1],b),HatPoint(rings[j],b),HatPoint(rings[j],a), color, normal);
                }
                var rim = rings[rings.Length-1];
                d.Tri(HatPoint(rim,a),HatPoint(rim,b),V(0,rim.y+.01f,-.009f),crown,Vector3.up);
            }
        }

        static void BucketHat(Draft d)
        {
            var teal = Draft.C(0x6F9A97);
            var dark = Draft.C(0x293746);
            var rings = new[] {
                V(.285f,1.466f,.260f),V(.396f,1.406f,.345f),V(.403f,1.420f,.350f),
                V(.317f,1.506f,.286f),V(.310f,1.535f,.278f),V(.303f,1.553f,.273f),
                V(.269f,1.641f,.249f),V(.246f,1.652f,.232f)
            };
            HatShell(d,rings,new[] {dark,teal,Navy,dark,teal,Navy,dark},Navy,false);
            d.Quad(V(-.042f,1.559f,.262f),V(.042f,1.559f,.262f),V(.039f,1.598f,.250f),V(-.039f,1.598f,.250f),Cream,Vector3.forward);
        }

        static void StrawHat(Draft d)
        {
            var straw = Draft.C(0xE7C884); var honey = Draft.C(0xCBA669);
            var light = Draft.C(0xF3DA9D); var ribbon = Draft.C(0xB86859);
            var rings = new[] {
                V(.281f,1.463f,.26f),V(.453f,1.453f,.391f),V(.455f,1.468f,.392f),
                V(.408f,1.474f,.354f),V(.352f,1.479f,.31f),V(.311f,1.486f,.283f),
                V(.303f,1.526f,.277f),V(.299f,1.553f,.272f),V(.283f,1.623f,.258f),V(.225f,1.651f,.213f)
            };
            HatShell(d,rings,new[] {honey,light,straw,light,straw,ribbon,ribbon,straw,light},light,true);
            d.Ball(V(.242f,1.530f,.152f),V(.048f,.027f,.021f),ribbon,8,3);
            d.Ball(V(.284f,1.530f,.105f),V(.024f,.026f,.044f),ribbon,8,3);
            d.Ball(V(.267f,1.530f,.131f),V(.023f,.022f,.025f),Coral,7,3);
        }
    }
}
