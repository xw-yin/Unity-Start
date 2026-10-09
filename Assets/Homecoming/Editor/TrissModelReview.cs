using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class TrissModelReview
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        [Serializable] class Report { public int originalSkirtVertices,actualSkirtVertices,changedWeights,sourceShapes;public bool neutralPositionsEqual;public string[] adaptedShapes; }
        static Mesh OriginalSkirt()
        {
            var pieces=new[]{"Part_07.bin","Part_10.bin"}.Select(file=>WitcherCharacterImport.ReadPreparedMesh("Assets/ThirdParty/WitcherTriss/Prepared/",file,"Original DLC skirt layer")).ToArray();
            var mesh=new Mesh{name="Original DLC underskirt and outer skirt",vertices=pieces.SelectMany(m=>m.vertices).ToArray(),normals=pieces.SelectMany(m=>m.normals).ToArray(),uv=pieces.SelectMany(m=>m.uv).ToArray(),boneWeights=pieces.SelectMany(m=>m.boneWeights).ToArray(),bindposes=pieces[0].bindposes,subMeshCount=2};
            mesh.SetTriangles(pieces[0].triangles,0);mesh.SetTriangles(pieces[1].triangles.Select(i=>i+pieces[0].vertexCount).ToArray(),1);mesh.RecalculateBounds();mesh.RecalculateTangents();
            foreach(var piece in pieces)UnityEngine.Object.DestroyImmediate(piece);return mesh;
        }
        public static void ReviewCloth()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(2);
            var rig=story.GetComponentInChildren<CharacterRig>();var solver=rig.skirt.GetComponent<HomecomingGarmentDynamics>();
            var source=solver.SourceMesh;var original=source.vertices;var originalWeights=source.boneWeights;var motion=new HomecomingMotion(rig);
            float maximum=0,walking=0,gripError=0;var baked=new Mesh();
            try
            {
                foreach(int fps in new[]{60,20})
                {
                    motion.Sample(0);
                    for(int frame=1;frame<=32*fps;frame++)
                    {
                        float time=frame/(float)fps;motion.Sample(time);
                        maximum=Mathf.Max(maximum,solver.MaximumDisplacement);
                        if(time>2&&time<7)walking=Mathf.Max(walking,solver.MaximumDisplacement);
                        foreach(var p in rig.skirt.sharedMesh.vertices)if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude))throw new Exception("Non-finite Triss skirt");
                        if((rig.LeftDressLift>.5f||rig.RightDressLift>.5f)&&frame%5==0)
                        {
                            rig.skirt.BakeMesh(baked);var vertices=baked.vertices;
                            foreach(int side in new[]{0,1})
                            {
                                if((side==0?rig.LeftDressLift:rig.RightDressLift)<=.5f)continue;
                                var target=rig.DressHoldPoint(side);
                                gripError=Mathf.Max(gripError,vertices.Min(v=>Vector3.Distance(rig.skirt.transform.TransformPoint(v),target)));
                            }
                        }
                    }
                    var paused=rig.skirt.sharedMesh.vertices;motion.Sample(32);
                    if(!paused.SequenceEqual(rig.skirt.sharedMesh.vertices))throw new Exception("Paused Triss fabric changed");
                    motion.Sample(0);
                    if(!original.SequenceEqual(rig.skirt.sharedMesh.vertices)||!original.SequenceEqual(source.vertices)||!originalWeights.SequenceEqual(source.boneWeights))throw new Exception("Triss source or replay changed");
                }
                if(walking<.004f||maximum>.111f||gripError>.012f)throw new Exception("Triss cloth is unstable or detached: "+walking+" / "+maximum+" / "+gripError);
                Directory.CreateDirectory("Artifacts/TrissModelReview");
                File.WriteAllText("Artifacts/TrissModelReview/fabric-check.txt","60/20 FPS; walking="+walking+"m; maximum="+maximum+"m; hem target error="+gripError+"m; pause, replay and source weights preserved.");
                Debug.Log("TRISS CLOTH REVIEW PASSED; hem target error="+gripError);
            }
            finally{UnityEngine.Object.DestroyImmediate(baked);}
        }
        public static void RepairAndReview()
        {
            const string path="Assets/Homecoming/Resources/Homecoming/Triss.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig=prefab.GetComponent<CharacterRig>();
                var source=OriginalSkirt();
                try
                {
                    if(!source.vertices.SequenceEqual(rig.skirt.sharedMesh.vertices)||!source.bindposes.SequenceEqual(rig.skirt.sharedMesh.bindposes))throw new InvalidOperationException("Prepared Triss skirt does not match the current mesh and binding");
                    rig.skirt.sharedMesh.boneWeights=source.boneWeights;
                    EditorUtility.SetDirty(rig.skirt.sharedMesh);
                    rig.skirt.GetComponent<HomecomingGarmentDynamics>().ConfigureSeatedHipCompensation(true);
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }
                finally{UnityEngine.Object.DestroyImmediate(source);}
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            AssetDatabase.SaveAssets();
            HomecomingSetup.CreateScene();
            SessionState.SetInt("Homecoming.ContinuousReview.First",2);
            SessionState.SetInt("Homecoming.ContinuousReview.Last",2);
            HomecomingContinuousReview.Run();
        }
        public static void Probe()
        {
            Directory.CreateDirectory("Artifacts/TrissModelReview");
            foreach(string variant in new[]{"standing","runtime","no-lining","no-underlayer","source-skirt","no-body","skin-flat-normal"})
            {
                HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(2);
                var rig=story.GetComponentInChildren<CharacterRig>();
                typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                int end=variant=="standing"?210:240;
                for(int frame=0;frame<=end;frame++){typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,frame/30f-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);}
                var surfaces=rig.GetComponentsInChildren<SkinnedMeshRenderer>();
                if(variant=="no-lining"){var mesh=UnityEngine.Object.Instantiate(rig.skirt.sharedMesh);mesh.SetTriangles(Array.Empty<int>(),0);rig.skirt.sharedMesh=mesh;}
                if(variant=="no-underlayer")foreach(var s in surfaces.Where(s=>s.name.StartsWith("l_01_wa__triss_dlc")))s.enabled=false;
                if(variant=="no-body")foreach(var s in surfaces.Where(s=>s.name.StartsWith("twals_01_wa__body_triss")))s.enabled=false;
                if(variant=="skin-flat-normal")foreach(var s in surfaces.Where(s=>s.name.StartsWith("twals_01_wa__body_triss"))){var mat=new Material(s.sharedMaterial);mat.DisableKeyword("_NORMALMAP");mat.SetTexture("_BumpMap",null);s.sharedMaterial=mat;}
                if(variant=="source-skirt")rig.skirt.sharedMesh=OriginalSkirt();
                var camera=story.CameraControls.GetComponent<Camera>();var aim=rig.transform.position+new Vector3(0,.85f,0);camera.transform.position=aim+new Vector3(.1f,.04f,-2.8f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                HomecomingSetup.Capture(camera,"Artifacts/TrissModelReview/"+variant+".png",1000,1100);
            }
            // Check what the importer changed against the actual prepared mesh.
            var prefab=PrefabUtility.LoadPrefabContents("Assets/Homecoming/Resources/Homecoming/Triss.prefab");
            try
            {
                var rig=prefab.GetComponent<CharacterRig>();var source=OriginalSkirt();var actual=rig.skirt.sharedMesh;
                int changed=source.boneWeights.Zip(actual.boneWeights,(a,b)=>a.Equals(b)?0:1).Sum();
                var report=new Report{originalSkirtVertices=source.vertexCount,actualSkirtVertices=actual.vertexCount,neutralPositionsEqual=source.vertices.SequenceEqual(actual.vertices),changedWeights=changed,sourceShapes=source.blendShapeCount,adaptedShapes=Enumerable.Range(0,actual.blendShapeCount).Select(actual.GetBlendShapeName).ToArray()};
                File.WriteAllText("Artifacts/TrissModelReview/skirt-changes.json",JsonUtility.ToJson(report,true));
                UnityEngine.Object.DestroyImmediate(source);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            Debug.Log("TRISS DLC MODEL PROBE COMPLETE");
        }
    }
}
