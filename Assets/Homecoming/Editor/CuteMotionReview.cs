using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class CuteMotionReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static FieldInfo Field(string name)=>typeof(HomecomingScene).GetField(name,Flags);
        static void Step(HomecomingScene story,float time)
        {
            Field("elapsed").SetValue(story,time>=32?32:time-Time.deltaTime);
            Field("idleElapsed").SetValue(story,time>=32?time-32-Time.deltaTime:0);
            typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
        }
        public static void FullReview(){Run();HandMovementReview.Validate();HomecomingFootwearReview.Run();BlenderModelBridge.ExportAll();HomecomingContinuousReview.Run();}
        public static void ShaniOnly(){Run(1,2);}
        public static void DuchessOnly(){Run(6,7);}
        public static void RepairAndFullReview(){RepairSurfaces();FullReview();}
        public static void FinalEvidence(){RepairSurfaces();ValidateWaiting();CaptureFilm();BlenderModelBridge.ExportAll();}
        public static void FinishReview(){RepairSurfaces();ValidateWaiting();Run(0,7,false);HomecomingFootwearReview.Run();CaptureFilm();BlenderModelBridge.ExportAll();HomecomingContinuousReview.Run();}
        public static void InspectYenneferSurfaces()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;Field("started").SetValue(story,true);
            var rig=story.GetComponentInChildren<CharacterRig>();for(int frame=0;frame<=870;frame++)Step(story,frame/30f);
            var camera=story.CameraControls.GetComponent<Camera>();var target=rig.transform.TransformPoint(new Vector3(0,.78f,0));camera.transform.position=target+new Vector3(.05f,.02f,-1.4f);camera.transform.LookAt(target);camera.fieldOfView=38;
            HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/yennefer-all.png",900,1000);
            foreach(var sock in rig.hosiery)sock.enabled=false;
            HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/yennefer-no-hosiery.png",900,1000);
            var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>();foreach(var skin in skins)skin.enabled=skin==rig.skirt;
            HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/yennefer-skirt-only.png",900,1000);
            var cloth=rig.GetComponent<HomecomingClothDynamics>();rig.skirt.sharedMesh=cloth.SourceMesh;
            HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/yennefer-source-skirt.png",900,1000);
            Debug.Log("YENNEFER SURFACE INSPECTION COMPLETE");
        }
        public static void ValidateWaiting()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            for(int character=0;character<7;character++)
            {
                story.SelectCharacter(character);story.Replay();Field("started").SetValue(story,false);
                var rig=story.GetComponentInChildren<CharacterRig>();
                for(int frame=0;frame<=270;frame++)
                {
                    Field("idleElapsed").SetValue(story,frame/30f-Time.deltaTime);
                    typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                }
                if(story.Started||story.Finished||(float)Field("elapsed").GetValue(story)!=0)throw new Exception("Waiting advanced the story");
                foreach(var eye in rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.StartsWith("he_")&&!s.sharedMaterial.GetTexture("_BaseMap")))
                    if(eye.enabled)throw new Exception("Opaque untextured eye-shadow pass still enabled: "+character);
                var before=rig.head.rotation;var idle=(float)Field("idleElapsed").GetValue(story);Field("paused").SetValue(story,true);
                typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                if(Quaternion.Angle(before,rig.head.rotation)>.01f||(float)Field("idleElapsed").GetValue(story)!=idle)throw new Exception("Waiting pause moved");
                Field("paused").SetValue(story,false);var motion=(HomecomingMotion)Field("motion").GetValue(story);
                var hands=new[]{motion.Hands.Hand(0).position,motion.Hands.Hand(1).position};story.TogglePlayback();Step(story,1/30f);
                if(Quaternion.Angle(before,rig.head.rotation)>15||Enumerable.Range(0,2).Any(s=>Vector3.Distance(hands[s],motion.Hands.Hand(s).position)>.10f))throw new Exception("Waiting to greeting transition jumped");
                var camera=story.CameraControls.GetComponent<Camera>();var target=rig.head.position+new Vector3(0,.05f,-.02f);
                camera.transform.position=target+new Vector3(.05f,.025f,-.75f);camera.transform.LookAt(target);camera.fieldOfView=32;
                HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/"+character+"-face.png",700,700);
                // A smile must not accumulate when a new motion controller is created.
                motion.Sample(29);var smiling=rig.GetComponentsInChildren<Transform>().Single(b=>b.name=="W3_left_corner_lip1");
                story.Replay();var rest=smiling.localPosition;story.Replay();
                if(Vector3.Distance(rest,smiling.localPosition)>.00001f)throw new Exception("Replay accumulated facial displacement");
            }
            Debug.Log("WAITING AND FACE REVIEW PASSED: seven characters; stationary waiting, pause/start transition, original iris visible, replay resets native smile bones.");
        }
        public static void RepairSurfaces()
        {
            foreach(string resource in HomecomingCharacters.ResourcesPaths)
            {
                string path="Assets/Homecoming/Resources/"+resource+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rig=root.GetComponent<CharacterRig>();bool changed=false;
                    bool open=resource.EndsWith("Triss");
                    if(rig.openToeFootwear!=open){rig.openToeFootwear=open;changed=true;}
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if(skin.name.StartsWith("he_")&&!skin.sharedMaterial.GetTexture("_BaseMap")&&skin.enabled){skin.enabled=false;changed=true;}
                    if(changed)PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        [MenuItem("Homecoming/审阅可爱动作与全部脚袜鞋")]
        public static void Run()
        {
            Run(0,7);
        }
        static void Run(int first,int endCharacter,bool captureDetails=true)
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            Directory.CreateDirectory("Artifacts/CuteMotion");
            var report=new StringBuilder("character,frame,side,wristBend,fingerClothIntersections\n");
            for(int character=first;character<endCharacter;character++)
            {
                story.SelectCharacter(character);var rig=story.GetComponentInChildren<CharacterRig>();Field("started").SetValue(story,true);
                var all=rig.GetComponentsInChildren<Transform>();
                var camera=story.CameraControls.GetComponent<Camera>();
                for(int frame=0;frame<=1200;frame++)
                {
                    Step(story,frame/30f);
                    if(frame>=810&&frame%15==0)
                    {
                        var body=StandingHandReview.BodyTriangles(rig);
                        for(int side=0;side<2;side++)
                        {
                            string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                            var hand=all.Single(b=>b.name==prefix+"Hand");var elbow=side==0?rig.leftElbow:rig.rightElbow;
                            var middle=all.Single(b=>b.name==prefix+"Middle1");
                            float flex=Vector3.Angle(hand.position-elbow.position,middle.position-hand.position);
                            int hits=0;string hitInfo="";
                            foreach(string finger in new[]{"Index","Middle","Ring","Little","Thumb"})
                            {
                                var chain=Enumerable.Range(1,3).Select(j=>all.Single(b=>b.name==prefix+finger+j)).ToArray();
                                for(int j=0;j<3;j++)
                                {
                                    var end=j<2?chain[j+1].position:chain[2].position+(chain[2].position-chain[1].position)*.7f;
                                    foreach(var triangle in body)if(StandingHandReview.Crosses(chain[j].position,end,triangle)){hits++;hitInfo+=triangle.surface+":"+finger+j+" "+rig.transform.InverseTransformPoint(chain[j].position)+";";}
                                }
                            }
                            report.AppendLine(character+","+frame+","+side+","+flex+","+hits);
                            if(hits>0||flex>70)
                            {
                                camera.transform.position=rig.transform.position+new Vector3(.9f,1.2f,-1.8f);camera.transform.LookAt(rig.hips.position);camera.fieldOfView=45;
                                HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/failed-"+character+"-"+frame+".png",1000,1000);
                                throw new Exception("Cute hand clearance failed: "+character+"@"+frame+" side="+side+" hits="+hits+" wrist="+flex+" "+hitInfo);
                            }
                        }
                    }
                    if(new[]{870,990,1050}.Contains(frame))
                    {
                        var target=rig.transform.TransformPoint(new Vector3(0,.94f,0));camera.transform.position=target+new Vector3(.3f,.05f,-2.6f);camera.transform.LookAt(target);camera.fieldOfView=39;
                        HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/"+character+"-"+frame+".png",800,1000);
                    }
                    if(captureDetails&&new[]{240,315,405,675}.Contains(frame))
                    {
                        var target=rig.transform.TransformPoint(new Vector3(0,.30f,-.12f));camera.transform.position=target+new Vector3(.6f,.2f,-1.05f);camera.transform.LookAt(target);camera.fieldOfView=42;
                        HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/"+character+"-footwear-"+frame+".png",1000,850);
                    }
                }
                // Finished remains a UI state, while the idle keeps moving.
                var before=rig.head.rotation;Field("idleElapsed").SetValue(story,1f);Field("elapsed").SetValue(story,32f);
                typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                if(!story.Finished||Quaternion.Angle(before,rig.head.rotation)<.1f)throw new Exception("Finished idle stopped");
                Field("paused").SetValue(story,true);before=rig.head.rotation;
                typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                if(Quaternion.Angle(before,rig.head.rotation)>.01f)throw new Exception("Paused idle moved");
                story.Replay();if(story.Finished||!story.Started||story.Paused)throw new Exception("Replay did not reset idle");
                if(captureDetails)CaptureComponents(rig,camera,character);
                Debug.Log("CUTE MOTION REVIEW CHARACTER PASSED "+character+": 1201 frames, tail movement/pause/replay and independent lower-body views.");
            }
            File.WriteAllText("Artifacts/CuteMotion/metrics.csv",report.ToString());
            Debug.Log("CUTE MOTION REVIEW PASSED: seven characters; animated tail; no tested finger segments through clothing.");
        }
        static void CaptureComponents(CharacterRig rig,Camera camera,int character)
        {
            rig.Neutral();rig.transform.SetPositionAndRotation(new Vector3(-1.25f,1,1.135f),Quaternion.identity);rig.hips.localPosition=rig.hipRestPosition;rig.SetSkirtPose(0);rig.SetDressLift(0,0);
            var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var skin in skins){skin.gameObject.SetActive(true);skin.enabled=false;skin.SetPropertyBlock(null);}
            for(int role=0;role<3;role++)
            {
                foreach(var skin in skins)skin.enabled=false;
                var selected=role==0?rig.anatomicalLegs:role==1?rig.hosiery:new[]{rig.leftShoe.GetComponent<SkinnedMeshRenderer>(),rig.rightShoe.GetComponent<SkinnedMeshRenderer>()};
                foreach(var skin in selected)skin.enabled=true;
                var target=rig.transform.TransformPoint(new Vector3(0,.16f,-.025f));
                foreach(var view in new[]{("front",new Vector3(.3f,.18f,-.9f)),("heel",new Vector3(.3f,.12f,.9f)),("sole",new Vector3(.2f,-.5f,-.7f))})
                {
                    camera.transform.position=target+view.Item2;camera.transform.LookAt(target);camera.fieldOfView=35;
                    HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/"+character+"-component-"+role+"-"+view.Item1+".png",900,800);
                }
            }
        }
        public static void CaptureFilm()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(1);Field("started").SetValue(story,true);
            var rig=story.GetComponentInChildren<CharacterRig>();var camera=story.CameraControls.GetComponent<Camera>();Directory.CreateDirectory("Artifacts/CuteMotion/Film");
            for(int frame=0;frame<1170;frame++)
            {
                Step(story,frame/30f);if(frame<810)continue;
                var target=rig.transform.TransformPoint(new Vector3(0,.95f,0));camera.transform.position=target+new Vector3(.35f,.05f,-2.8f);camera.transform.LookAt(target);camera.fieldOfView=39;
                HomecomingSetup.Capture(camera,"Artifacts/CuteMotion/Film/"+(frame-810).ToString("D4")+".png",640,800);
            }
            Debug.Log("CUTE FILM COMPLETE: twelve seconds at 30 fps.");
        }
    }
}
