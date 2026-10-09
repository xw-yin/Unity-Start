using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferBootReview
    {
        const string Prefab="Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab";
        const string Output="Artifacts/YenneferBootReview/";
        public static void FinalReview(){Review();BlenderModelBridge.ExportAll();HomecomingContinuousReview.Run();}
        public static void RestoreAndFinalReview(){RestoreAndReview();HomecomingContinuousReview.Run();}

        [MenuItem("Homecoming/恢复并审阅叶奈法原版长靴")]
        public static void RestoreAndReview()
        {
            Directory.CreateDirectory(Output);
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                var rig=root.GetComponent<CharacterRig>();
                YenneferOriginalBoots.Restore(rig);
                for(int side=0;side<2;side++)
                {
                    var shoe=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                    var baked=new Mesh();shoe.BakeMesh(baked);
                    var v=baked.vertices;var n=baked.normals;
                    var matrix=rig.transform.worldToLocalMatrix*shoe.transform.localToWorldMatrix;
                    var bounds=new Bounds(matrix.MultiplyPoint3x4(v[0]),Vector3.zero);
                    for(int i=0;i<v.Length;i++){v[i]=matrix.MultiplyPoint3x4(v[i]);n[i]=matrix.MultiplyVector(n[i]).normalized;bounds.Encapsulate(v[i]);}
                    var origin=bounds.center;origin.y=bounds.min.y;
                    for(int i=0;i<v.Length;i++)v[i]-=origin;
                    baked.vertices=v;baked.normals=n;baked.RecalculateBounds();
                    var proxy=(side==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>();
                    baked.name=proxy.sharedMesh.name;WitcherCharacterImport.CopyMesh(baked,proxy.sharedMesh);
                    proxy.GetComponent<Renderer>().sharedMaterials=shoe.sharedMaterials;
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                YenneferSheerHosiery.Build(rig,"Assets/ThirdParty/WitcherYenneferDLC/Unity/");
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Review();BlenderModelBridge.ExportYennefer();
        }
        public static void Review()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var rig=story.GetComponentInChildren<CharacterRig>();var camera=story.CameraControls.GetComponent<Camera>();
            typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
            var report=new StringBuilder("frame,side,wornToProxyMaximumDistance\n");
            var mesh=new Mesh();
            for(int frame=0;frame<=870;frame++)
            {
                typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,frame/30f-Time.deltaTime);
                typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
                if(frame%15==0||(frame>=375&&frame<=435&&frame%3==0)||(frame>=555&&frame<=615&&frame%3==0))YenneferBootIntersections.Capture(story,rig,frame);
                if(frame==390||frame==570)
                {
                    int side=frame==390?0:1;
                    var skin=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();skin.BakeMesh(mesh);
                    var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);var v=proxy.GetComponent<MeshFilter>().sharedMesh.vertices;
                    float distance=0;var worn=mesh.vertices;
                    for(int i=0;i<v.Length;i++)distance=Mathf.Max(distance,Vector3.Distance(proxy.TransformPoint(v[i]),skin.transform.TransformPoint(worn[i])));
                    report.AppendLine(frame+","+side+","+distance);
                    Debug.Log("BOOT TRANSFER: frame="+frame+" maximum vertex mismatch="+distance);
                    if(distance>.001f)throw new Exception("Skinned boot jumps during transfer to the hand");
                }
                if(!new[]{150,240,330,389,390,405,420,450,570,600,675,870}.Contains(frame))continue;
                var aim=rig.transform.TransformPoint(new Vector3(0,.39f,-.04f));
                camera.transform.position=aim+new Vector3(.28f,.13f,-1.55f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                HomecomingSetup.Capture(camera,Output+frame+"-front.png",1100,1000);
                if(frame==240||frame==390||frame==420||frame==675)
                {
                    camera.transform.position=aim+new Vector3(1.3f,.12f,-.7f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,Output+frame+"-side.png",1100,1000);
                }
            }
            UnityEngine.Object.DestroyImmediate(mesh);File.WriteAllText(Output+"transfer.csv",report.ToString());
            HomecomingFootwearReview.Run();Debug.Log("YENNEFER ORIGINAL BOOT REVIEW COMPLETE");
        }
    }
}
