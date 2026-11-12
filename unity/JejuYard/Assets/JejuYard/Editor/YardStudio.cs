using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Jeju.Yard.EditorArt
{
    public static class YardStudio
    {
        internal const string ScenePath = "Assets/Scenes/JejuYard.unity";
        static readonly string Output = Path.GetFullPath("Builds");

        [MenuItem("Jeju Yard/Rebuild courtyard from authored models")]
        public static void Assemble()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var courtyard = Landscape.Build();
            Dolphin.Build(courtyard.transform);
            YardAnimals.Build();
            new GameObject("YardCollection").AddComponent<YardCollectionController>();
            var house = Cottage.Build();
            PrefabUtility.SaveAsPrefabAssetAndConnect(house,"Assets/JejuYard/Art/JejuChoga.prefab",InteractionMode.AutomatedAction);
            house.transform.position = new Vector3(-5.4f,0,5.6f);
            house.transform.rotation = Quaternion.Euler(0,15f,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(house.transform);
            var actor = Explorer.Build();
            PrefabUtility.SaveAsPrefabAssetAndConnect(actor.gameObject,"Assets/JejuYard/Art/Explorer.prefab",InteractionMode.AutomatedAction);
            var sunObj = new GameObject("Late morning sunlight");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.05f; sun.color = new Color(1f,.953f,.83f);
            sun.transform.rotation = Quaternion.Euler(52f,-32f,0f); sun.shadows=LightShadows.Soft;
            sun.shadowStrength=.48f; sun.shadowBias=.035f; sun.shadowNormalBias=.24f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.76f,.85f,.94f);
            RenderSettings.ambientEquatorColor=new Color(.65f,.67f,.56f);
            RenderSettings.ambientGroundColor=new Color(.49f,.48f,.35f);
            RenderSettings.skybox=null;
            var cameraObj = new GameObject("Courtyard camera");
            cameraObj.tag = "MainCamera";
            var cam = cameraObj.AddComponent<Camera>(); cam.nearClipPlane=.25f; cam.farClipPlane=100f;
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=Draft.C(0xC0DBD3);
            cam.allowHDR=false; cam.allowMSAA=true;
            var follow=cameraObj.AddComponent<YardCamera>(); follow.player=actor.transform; follow.Frame(true);

            PlayerSettings.productName="Jeju Yard"; PlayerSettings.companyName="Jeju Explorer";
            PlayerSettings.colorSpace=ColorSpace.Gamma; PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution=false; PlayerSettings.defaultScreenWidth=1152; PlayerSettings.defaultScreenHeight=768;
            PlayerSettings.resizableWindow=true;
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Could not save "+ScenePath);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(AssetDatabase.GUIDFromAssetPath(ScenePath),true) };
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(-2,.8f,3),Quaternion.Euler(38,10,0),15f);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Jeju Yard/Check walking distances")]
        public static void Check()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the courtyard scene before running its movement check.");
            var actor=UnityEngine.Object.FindAnyObjectByType<YardWalker>();
            var body=actor.GetComponent<CharacterController>();
            Vector3 spawn=actor.transform.position;
            if (Mathf.Abs(spawn.x)+Mathf.Abs(spawn.z)>.001f) throw new Exception("Spawn must be the centre");
            var originalVisual=actor.visual.rotation;
            var tests=new[] {Vector2.up,Vector2.down,Vector2.left,Vector2.right};
            try
            {
                foreach(int hz in new[]{30,60})
                {
                    foreach(Vector2 direction in tests)
                    {
                        body.enabled=false; actor.transform.position=spawn; body.enabled=true; Physics.SyncTransforms();
                        float dt=1f/hz, firstTouch=-1;
                        for(int i=1;i<=9*hz;i++)
                        {
                            var flags=actor.Step(direction,dt);
                            if (firstTouch<0 && (flags & CollisionFlags.Sides)!=0) firstTouch=i*dt;
                        }
                        float distance=new Vector2(actor.transform.position.x-spawn.x,actor.transform.position.z-spawn.z).magnitude;
                        bool atSea = direction == Vector2.right || direction == Vector2.down;
                        string edge = atSea ? "waterline" : "basalt wall";
                        Debug.Log($"YARD WALK {direction} at {hz}Hz: {edge} at {firstTouch:F3}s, stopped at {distance:F3}m, y={actor.transform.position.y:F3}");
                        if (Mathf.Abs(firstTouch-YardWalker.WallWalkSeconds)>dt+.023f || Mathf.Abs(distance-YardWalker.CentreToWallContact)>.045f)
                            throw new Exception($"Wrong travel to {edge} in direction {direction}: {firstTouch}s / {distance}m");
                        if (atSea && Mathf.Abs(actor.transform.position.y - Coast.WaterLevel) > .18f)
                            throw new Exception($"Character did not reach the sloping beach in direction {direction}");
                    }
                }
            }
            finally
            {
                body.enabled=false; actor.transform.position=spawn; body.enabled=true; actor.visual.rotation=originalVisual;
                actor.torso.localPosition=Vector3.zero; actor.leftLeg.localRotation=actor.rightLeg.localRotation=Quaternion.identity;
                actor.leftArm.localRotation=Quaternion.Euler(0,0,-6); actor.rightArm.localRotation=Quaternion.Euler(0,0,6);
                Physics.SyncTransforms();
            }
            var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>();
            long triangles=filters.Sum(f=>(long)f.sharedMesh.GetIndexCount(0)/3);
            int mats=UnityEngine.Object.FindObjectsByType<MeshRenderer>().Select(r=>r.sharedMaterial).Distinct().Count();
            Debug.Log($"YARD GEOMETRY {triangles} triangles, {filters.Length} mesh renderers, {mats} shared materials.");
        }

        public static void AuthorAndCheck()
        {
            Assemble(); Check();
        }

        [MenuItem("Jeju Yard/Render project previews")]
        public static void Preview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Directory.CreateDirectory(Output);
            var cam=Camera.main;
            var follow=cam.GetComponent<YardCamera>();
            cam.aspect=1376f/900f; follow.Frame(true);
            Take(cam,"yard-play",1376,900,cam.transform.rotation,cam.transform.position,cam.orthographicSize);
            Quaternion angle=Quaternion.Euler(42,-11,0);
            Take(cam,"yard-overview",1376,970,angle,new Vector3(.8f,.5f,-.7f)-angle*Vector3.forward*42f,14.85f);
            angle=Quaternion.Euler(23,-12,0);
            Take(cam,"yard-cottage",1376,900,angle,new Vector3(-5.4f,1.6f,4.9f)-angle*Vector3.forward*20f,3.95f);
            cam.aspect=.60f; follow.Frame(true);
            Take(cam,"yard-portrait",600,1000,cam.transform.rotation,cam.transform.position,cam.orthographicSize);
            var visitor=UnityEngine.Object.FindAnyObjectByType<DolphinVisitor>();
            var walker=follow.player.GetComponent<YardWalker>();
            Vector3 spawn=follow.player.position;
            Quaternion facing=walker.visual.rotation;
            try
            {
                follow.player.position=new Vector3(0,Coast.WaterLevel+.05f,-YardWalker.CentreToWallContact);
                walker.visual.rotation=Quaternion.LookRotation(Vector3.back);
                visitor.PoseAt(9.9f);
                cam.aspect=1.5f; follow.Frame(true);
                Take(cam,"yard-beach",1350,900,cam.transform.rotation,cam.transform.position,cam.orthographicSize);

                follow.player.position=new Vector3(YardWalker.CentreToWallContact,Coast.WaterLevel+.05f,0);
                walker.visual.rotation=Quaternion.LookRotation(Vector3.right);
                visitor.PoseAt(0);
                cam.aspect=.48f; follow.Frame(true);
                Take(cam,"yard-beach-portrait",540,1125,cam.transform.rotation,cam.transform.position,cam.orthographicSize);
            }
            finally
            {
                follow.player.position=spawn;
                walker.visual.rotation=facing;
                visitor.PoseAt(0);
                cam.ResetAspect();
                follow.Frame(true);
            }
        }

        static void Take(Camera cam,string name,int w,int h,Quaternion angle,Vector3 pos,float size)
        {
            var previous=RenderTexture.active;
            var rt = RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            var path=Path.Combine(Output,name+".png");
            var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            try
            {
                cam.targetTexture=rt; cam.aspect=(float)w/h;
                cam.transform.SetPositionAndRotation(pos,angle); cam.orthographicSize=size;
                cam.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,w,h),0,0); tex.Apply();
                File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex); cam.targetTexture=null; cam.ResetAspect();
                RenderTexture.active=previous; RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static void BuildMac()
        {
            string app=Path.Combine(Output,"macOS","Jeju Yard.app");
            Directory.CreateDirectory(Path.GetDirectoryName(app));
            var report=BuildPipeline.BuildPlayer(new[]{ScenePath},app,BuildTarget.StandaloneOSX,BuildOptions.None);
            if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Mac player failed: "+report.summary.result);
            Debug.Log("YARD PLAYER "+report.summary.totalSize+" bytes");
        }
    }
}
