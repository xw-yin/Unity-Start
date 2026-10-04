using System;
using System.Linq;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingFootwearReview
    {
        public static void RebuildYenneferAndAudit(){YenneferDLCImport.Import();Run();CaptureDetails();}
        public static void AuditAndCapture(){Run();CaptureDetails();}
        public static void RefreshSurfacesAndAudit()
        {
            foreach(string resource in HomecomingCharacters.ResourcesPaths)
            {
                string path="Assets/Homecoming/Resources/"+resource+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rig=root.GetComponent<CharacterRig>();
                    foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMesh.name.StartsWith("OriginalGame")||s.sharedMesh.name.StartsWith("Continuous")||s.name.StartsWith("DLC_Stocking_")).ToArray())UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                    float height=rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh.bounds.size.y;
                    rig.footwearKind=height>.30f?CharacterRig.FootwearKind.TallBoot:height>.16f?CharacterRig.FootwearKind.AnkleBoot:CharacterRig.FootwearKind.Shoe;
                    rig.longBoots=rig.footwearKind==CharacterRig.FootwearKind.TallBoot;
                    string folder=AssetDatabase.GetAssetPath(rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                    folder=folder.Substring(0,folder.IndexOf("Meshes/",StringComparison.Ordinal));
                    WitcherCharacterFeet.Build(rig,folder);rig.CaptureRestPose();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Run();CaptureDetails();
        }
        public static void CaptureDetails()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var type=typeof(HomecomingScene);
            foreach(int index in new[]{0,4,6})
            {
                story.SelectCharacter(index);var rig=story.GetComponentInChildren<CharacterRig>();
                type.GetField("started",flags).SetValue(story,true);
                var camera=story.CameraControls.GetComponent<Camera>();
                foreach(float time in new[]{10.1f,11.95f,14.4f,20.4f})
                {
                    type.GetField("elapsed",flags).SetValue(story,time);type.GetMethod("Update",flags).Invoke(story,null);
                    camera.transform.position=new Vector3(-.45f,.78f,-.10f);camera.transform.LookAt(new Vector3(-1.25f,.55f,1.25f));camera.fieldOfView=42;
                    HomecomingSetup.Capture(camera,"Artifacts/FootwearAudit/"+index+"-"+time+".png",1100,1000);
                }
                type.GetField("elapsed",flags).SetValue(story,22.4f);type.GetMethod("Update",flags).Invoke(story,null);
                camera.transform.position=new Vector3(-.55f,.50f,-.05f);camera.transform.LookAt(new Vector3(-1.25f,.23f,1.25f));camera.fieldOfView=32;
                HomecomingSetup.Capture(camera,"Artifacts/FootwearAudit/"+index+"-stockings.png",1100,1000);
            }
        }
        [MenuItem("Homecoming/审阅独立腿袜鞋")]
        public static void Run()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            Directory.CreateDirectory("Artifacts/FootwearAudit");
            var report=new StringBuilder("character,profile,side,legVertices,hosieryVertices,shoeVertices,legMinY,legMaxY,shoeHeight\n");
            for(int index=0;index<HomecomingCharacters.Names.Length;index++)
            {
                story.SelectCharacter(index);var rig=story.GetComponentInChildren<CharacterRig>();
                var legs=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMesh.name.StartsWith("OriginalGame")).ToArray();
                if(legs.Length!=2||legs.Any(s=>s.sharedMesh.vertexCount<100||s.transform.IsChildOf(rig.leftShoe.transform)||s.transform.IsChildOf(rig.rightShoe.transform)))throw new Exception("Original game legs are absent or attached to the removable shoe: "+index);
                var meshes=legs.Select(s=>s.sharedMesh).ToArray();
                if(rig.anatomicalLegs==null||rig.anatomicalLegs.Length!=2||rig.hosiery==null||rig.hosiery.Length!=2)throw new Exception("Separate leg / hosiery references absent: "+index);
                for(int side=0;side<2;side++)
                {
                    var leg=rig.anatomicalLegs[side];var sock=rig.hosiery[side];var shoe=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                    if(leg==sock||leg.sharedMesh==sock.sharedMesh||sock.sharedMesh==shoe.sharedMesh||leg.sharedMesh==shoe.sharedMesh)throw new Exception("Leg, sock and shoe do not have independent meshes");
                    if(index==1)
                    {
                        if(!sock.name.StartsWith("夏妮原版")||!AssetDatabase.GetAssetPath(sock.sharedMaterial.GetTexture("_BaseMap")).Contains("WitcherShaniOriginalSocks/"))throw new Exception("Shani is missing her original game sock model/material");
                        if(sock.sharedMesh.bounds.min.y>.02f||sock.sharedMesh.bounds.max.y<.45f)throw new Exception("Original Shani sock foot is incomplete");
                    }
                    else if(!leg.sharedMesh.vertices.SequenceEqual(sock.sharedMesh.vertices)||!leg.sharedMesh.triangles.SequenceEqual(sock.sharedMesh.triangles)||!leg.sharedMesh.boneWeights.SequenceEqual(sock.sharedMesh.boneWeights))throw new Exception("Sock does not retain complete source leg and foot surface");
                    if(!leg.bones.SequenceEqual(sock.bones)||leg.transform.localToWorldMatrix!=sock.transform.localToWorldMatrix)throw new Exception("Sock does not follow the anatomical leg");
                    if(sock.transform.IsChildOf(rig.leftShoe.transform)||sock.transform.IsChildOf(rig.rightShoe.transform))throw new Exception("Sock moves with removable footwear");
                    report.AppendLine(HomecomingCharacters.Names[index]+","+rig.footwearKind+","+side+","+leg.sharedMesh.vertexCount+","+sock.sharedMesh.vertexCount+","+shoe.sharedMesh.vertexCount+","+leg.sharedMesh.bounds.min.y+","+leg.sharedMesh.bounds.max.y+","+shoe.sharedMesh.bounds.size.y);
                }
                if(index==0)
                {
                    if(rig.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.name.StartsWith("DLC_Stocking_")))throw new Exception("Legacy cylindrical stockings still present");
                    foreach(var sock in rig.hosiery.Concat(rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMaterial.name=="body_05_wa__yennefer_Material6")))
                    {
                        var mat=sock.sharedMaterial;
                        if(mat.GetTexture("_BaseMap")||(mat.HasProperty("_BumpMap")&&mat.GetTexture("_BumpMap"))||mat.color.maxColorComponent>.025f||Mathf.Abs(mat.color.r-mat.color.b)>.001f)throw new Exception("Yennefer hosiery still has a pattern or non-black tint");
                    }
                }
                var motion=new HomecomingMotion(rig);
                foreach(float time in new[]{0f,10.95f,13.4f,19.4f,27f})
                {
                    motion.Sample(time);
                    if(legs.Any(s=>!s.gameObject.activeSelf)||!meshes.SequenceEqual(legs.Select(s=>s.sharedMesh)))throw new Exception("Hosiery changes when a shoe is removed");
                    foreach(var sock in rig.hosiery)
                    {
                        if(!sock.enabled||!sock.gameObject.activeInHierarchy)throw new Exception("Stocking disappeared with the shoe");
                        var baked=new Mesh();sock.BakeMesh(baked);
                        if(baked.vertexCount!=sock.sharedMesh.vertexCount||baked.vertices.Any(v=>float.IsNaN(v.x)||float.IsInfinity(v.y)))throw new Exception("Invalid leg / foot deformation after removing shoe");
                        UnityEngine.Object.DestroyImmediate(baked);
                    }
                }
                if(rig.skirt.sharedMesh.GetBlendShapeIndex("BootLeft")>=0)
                {
                    motion.Sample(10.95f);
                    if(rig.LeftDressLift<.99f)throw new Exception("Shoe reach began before the gown was raised");
                    var hand=rig.GetComponentsInChildren<Transform>().Single(t=>t.name=="J_Bip_R_Hand");
                    float error=Vector3.Distance(hand.position,rig.DressHoldPoint(0)+new Vector3(0,.055f,-.015f));
                    if(error>.06f)throw new Exception("The free hand does not hold the raised gown: "+index+" error="+error+" hand="+hand.position+" hem="+rig.DressHoldPoint(0));
                    motion.Sample(16.95f);
                    if(rig.RightDressLift<.99f)throw new Exception("Right shoe reach began before the gown was raised");
                }
                // The duchess' original stocking submesh must stay in the body.
                if(index==6)
                {
                    var stocking=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name.StartsWith("l_02_wa__anna_henrietta")&&s.name.EndsWith("_Body"));
                    if(stocking.transform.IsChildOf(rig.leftShoe.transform)||stocking.transform.IsChildOf(rig.rightShoe.transform))throw new Exception("Original duchess stockings were merged with shoes");
                }
                Debug.Log("FOOTWEAR INVENTORY: "+HomecomingCharacters.Names[index]+"; "+rig.footwearKind+"; legs / complete fitted hosiery / shoes independently persisted.");
            }
            File.WriteAllText("Artifacts/FootwearAudit/inventory.csv",report.ToString());
            Debug.Log("FOOTWEAR REVIEW PASSED: seven original-game lower bodies; retained stockings; gown lifted before shoe reach; free-hand hem contact.");
        }
    }
}
