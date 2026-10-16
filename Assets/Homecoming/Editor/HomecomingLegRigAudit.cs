using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingLegRigAudit
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public class Metrics
        {
            public int character,frames;
            public float maximumAnkleDeviation,minimumKneeFlex=180,maximumKneeAxisError,maximumGripError,maximumSupportFloorHeight;
            public float[] footExit=new float[2];
            public Quaternion[] footRest=new Quaternion[2];
        }
        [Serializable] class Report { public List<Metrics> characters=new List<Metrics>(); }
        public static void Technical(){HomecomingMotionReview.Run();Run();HomecomingFootwearReview.Run();}
        public static void ReloadAndRun(){Reload();Run();}
        public static void Reload()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.ReloadCharacter();
            typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
            var clock=typeof(HomecomingScene).GetField("elapsed",Flags);
            var field=typeof(HomecomingScene).GetField("motion",Flags);
            var update=typeof(HomecomingScene).GetMethod("Update",Flags);
            var motion=(HomecomingMotion)field.GetValue(story);
            for(int frame=0;frame<=Mathf.CeilToInt(motion.ElapsedForAction(12.4f)*60);frame++)
            {clock.SetValue(story,frame/60f-Time.deltaTime);update.Invoke(story,null);}
            var previous=story.GetComponentInChildren<CharacterRig>();story.CameraControls.SetView(1);
            field.SetValue(story,null);update.Invoke(story,null);
            var current=story.GetComponentInChildren<CharacterRig>();
            if(ReferenceEquals(previous,current)||!story.Started||story.Paused||story.CameraControls.ViewIndex!=1||story.ActionTime>0)
                throw new Exception("Script reload reused the posed rig or lost playback/view state");
            motion=(HomecomingMotion)field.GetValue(story);
            for(int frame=0;frame<=Mathf.CeilToInt(motion.ElapsedForAction(12.4f)*60);frame++)
            {clock.SetValue(story,frame/60f-Time.deltaTime);update.Invoke(story,null);}
            var foot=current.GetComponentsInChildren<Transform>().Single(t=>t.name=="J_Bip_L_Foot");
            var rest=current.restPoses.Single(p=>p.bone==foot).rotation;
            if(Quaternion.Angle(rest,foot.localRotation)>45)throw new Exception("Ankle twisted after script reload");
            Debug.Log("LEG RIG RELOAD PASSED: fresh character and fit meshes; playback and view retained; local ankle limit retained.");
        }
        [MenuItem("Homecoming/检查交叉腿关节与足部")]
        public static void Run()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.ReloadCharacter();
            var report=new Report();
            foreach(int index in new[]{0,2})
            {
                story.SelectCharacter(index);
                var rig=story.GetComponentInChildren<CharacterRig>();rig.Neutral();
                var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                var feet=rig.GetComponentsInChildren<Transform>().Where(t=>t.name=="J_Bip_L_Foot"||t.name=="J_Bip_R_Foot").OrderBy(t=>t.name).ToArray();
                var thighs=new[]{rig.leftThigh,rig.rightThigh};var knees=new[]{rig.leftCalf,rig.rightCalf};
                var kneeRest=knees.Select(k=>k.localRotation).ToArray();
                var hinges=thighs.Select((t,s)=>t.InverseTransformDirection(Vector3.Cross(knees[s].position-t.position,rig.transform.forward).normalized)).ToArray();
                var lengths=thighs.Select((t,s)=>new[]{Vector3.Distance(t.position,knees[s].position),Vector3.Distance(knees[s].position,feet[s].position)}).ToArray();
                var metrics=new Metrics{character=index,footRest=feet.Select(f=>f.localRotation).ToArray()};
                var exitChecked=new bool[2];var baked=new Mesh();
                typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                for(int frame=0;frame<=Mathf.CeilToInt(motion.ElapsedForAction(21)*60);frame++)
                {
                    float elapsed=frame/60f;
                    typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,elapsed-Time.deltaTime);
                    typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                    float action=motion.ActionTime(elapsed);metrics.frames++;
                    var proxies=story.transform.Find("脱下的通勤鞋");
                    for(int side=0;side<2;side++)
                    {
                        if(Mathf.Abs(Vector3.Distance(thighs[side].position,knees[side].position)-lengths[side][0])>.001f||
                           Mathf.Abs(Vector3.Distance(knees[side].position,feet[side].position)-lengths[side][1])>.001f)
                            throw new Exception("Leg length changed: "+index+" / "+side);
                        if(action>=HomecomingShoeGrip.Detach(side)&&action<HomecomingShoeGrip.Release(side))
                        {
                            float error=motion.Shoes.ContactError(proxies.GetChild(side),side);
                            metrics.maximumGripError=Mathf.Max(metrics.maximumGripError,error);
                            if(error>.025f)throw new Exception("Boot grip lost: "+index+" / "+side+" action="+action+" gap="+error);
                        }
                        if(!exitChecked[side]&&action>=motion.Shoes.Cleared(side))
                        {
                            metrics.footExit[side]=motion.Shoes.FootExitClearance(proxies.GetChild(side),side);exitChecked[side]=true;
                            if(metrics.footExit[side]<0)throw new Exception("Foot still below boot lip: "+index+" / "+side+" clearance="+metrics.footExit[side]);
                        }
                        if(action>HomecomingShoeGrip.Start(side)-.75f&&action<HomecomingShoeGrip.Release(side)+.8f)
                        {
                            float ankleTransition=Quaternion.Angle(metrics.footRest[side],feet[side].localRotation);
                            metrics.maximumAnkleDeviation=Mathf.Max(metrics.maximumAnkleDeviation,ankleTransition);
                            if(ankleTransition>45)throw new Exception("Ankle transition exceeded local limit: "+index+" action="+action+" angle="+ankleTransition);
                        }
                        if(action<HomecomingShoeGrip.Start(side)+1.1f||action>=HomecomingShoeGrip.Release(side))continue;
                        float ankle=Quaternion.Angle(metrics.footRest[side],feet[side].localRotation);
                        float flex=180-Vector3.Angle(thighs[side].position-knees[side].position,feet[side].position-knees[side].position);
                        var turn=knees[side].localRotation*Quaternion.Inverse(kneeRest[side]);turn.ToAngleAxis(out float angle,out Vector3 axis);
                        float axisError=Mathf.Min(Vector3.Angle(axis,hinges[side]),Vector3.Angle(-axis,hinges[side]));
                        metrics.maximumAnkleDeviation=Mathf.Max(metrics.maximumAnkleDeviation,ankle);
                        metrics.minimumKneeFlex=Mathf.Min(metrics.minimumKneeFlex,flex);
                        metrics.maximumKneeAxisError=Mathf.Max(metrics.maximumKneeAxisError,axisError);
                        // Production limits for this shot, measured relative to the source rig.
                        if(ankle>45||flex<30||axisError>2)throw new Exception("Crossed joint limit: "+index+" side="+side+" action="+action+" ankle="+ankle+" flex="+flex+" kneeAxis="+axisError);
                        int support=1-side;
                        var surface=action<HomecomingShoeGrip.Detach(support)?(support==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>():rig.hosiery[support];
                        surface.BakeMesh(baked);float bottom=baked.vertices.Min(v=>surface.transform.TransformPoint(v).y);
                        metrics.maximumSupportFloorHeight=Mathf.Max(metrics.maximumSupportFloorHeight,bottom);
                        if(bottom<-.012f||bottom>.022f)throw new Exception("Supporting foot left floor: "+index+" action="+action+" lowest="+bottom);
                    }
                }
                UnityEngine.Object.DestroyImmediate(baked);report.characters.Add(metrics);
                Debug.Log("LEG RIG AUDIT PASSED: "+JsonUtility.ToJson(metrics));
            }
            Directory.CreateDirectory("Artifacts/LegRigReview");
            File.WriteAllText("Artifacts/LegRigReview/joint-audit.json",JsonUtility.ToJson(report,true));
        }
    }
}
