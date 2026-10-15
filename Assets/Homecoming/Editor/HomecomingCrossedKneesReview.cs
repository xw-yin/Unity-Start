using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingCrossedKneesReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("Homecoming/审阅膝盖交叉坐姿")]
        public static void Run()=>Capture(new[]{0});
        public static void Film()=>Capture(new[]{0},true);
        public static void Pair()=>Capture(new[]{0,2});
        public static void Validate()=>Capture(new[]{0,2},false,false);
        public static void ValidateAll()=>Capture(Enumerable.Range(0,HomecomingCharacters.Names.Length).ToArray(),false,false);
        public static void Delivery(){Pair();Film();}
        public static void Regression(){ValidateAll();HomecomingFootwearReview.Run();HomecomingMotionReview.Run();}
        public static void All()=>Capture(Enumerable.Range(0,HomecomingCharacters.Names.Length).ToArray());
        static void Capture(int[] characters,bool film=false,bool render=true)
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            var requested=new[]{249,282,300,321,342,360,378,414,447,465,486,507,525,546,582,660};
            foreach(int character in characters)
            {
                story.SelectCharacter(character);typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                var rig=story.GetComponentInChildren<CharacterRig>();
                var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                var camera=story.CameraControls.GetComponent<Camera>();
                var feet=rig.GetComponentsInChildren<Transform>().Where(t=>t.name=="J_Bip_L_Foot"||t.name=="J_Bip_R_Foot").OrderBy(t=>t.name).ToArray();
                string folder="Artifacts/CrossedKneesReview/"+character;Directory.CreateDirectory(folder);
                var report=new StringBuilder("action,leftKneeX,leftKneeY,leftKneeZ,rightKneeX,rightKneeY,rightKneeZ,leftAnkleY,rightAnkleY,leftFlex,rightFlex\n");
                var segmentLengths=new[]{Vector3.Distance(rig.leftThigh.position,rig.leftCalf.position),Vector3.Distance(rig.leftCalf.position,feet[0].position),Vector3.Distance(rig.rightThigh.position,rig.rightCalf.position),Vector3.Distance(rig.rightCalf.position,feet[1].position)};
                var done=new HashSet<int>();float maximumWrist=0,worstWristAction=0;int worstWristSide=0,movieFrame=0;string worstWristDetails="";
                if(film)Directory.CreateDirectory(folder+"/film");
                for(int frame=0;frame<=Mathf.CeilToInt(motion.ElapsedForAction(22.1f)*30);frame++)
                {
                    float time=frame/30f;
                    typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,time-Time.deltaTime);
                    typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                    float action=motion.ActionTime(time);int shot=Mathf.RoundToInt(action*30);
                    for(int side=0;side<2;side++)if(motion.Shoes.AnatomicalExtraction&&Mathf.Abs(action-motion.Shoes.Cleared(side))<1f/60f)
                    {
                        var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);
                        Debug.Log("CROSSED FOOT EXIT "+character+" / "+side+" clearance="+motion.Shoes.FootExitClearance(proxy,side));
                    }
                    for(int side=0;side<2;side++)
                    {
                        if(action>=HomecomingShoeGrip.Detach(side)&&action<HomecomingShoeGrip.Release(side))
                            { float error=Vector3.Distance(motion.Hands.Hand(motion.Shoes.HandSide(side)).position,motion.Shoes.WristTarget(side,action));if(error>maximumWrist){maximumWrist=error;worstWristAction=action;worstWristSide=side;var arm=motion.Shoes.HandSide(side)==0?rig.leftArm:rig.rightArm;var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);var hand=motion.Hands.Hand(motion.Shoes.HandSide(side));worstWristDetails=" actualAnchor="+Vector3.Distance(hand.position,motion.Hands.Wrist(motion.Shoes.HandSide(side),HomecomingHandPose.Hold.Boot,motion.Shoes.ProxyContact(proxy,side),hand.rotation))+" contact="+motion.Shoes.ContactError(proxy,side)+" upper="+arm.position+" wrist="+motion.Hands.Hand(motion.Shoes.HandSide(side)).position+" target="+motion.Shoes.WristTarget(side,action);} }
                        if(rig.longBoots&&action>HomecomingShoeGrip.Start(side)+.5f&&action<HomecomingShoeGrip.Release(side))
                        {
                            var top=side==0?rig.leftCalf:rig.rightCalf;var below=side==0?rig.rightCalf:rig.leftCalf;var gap=top.position-below.position;
                            if(gap.y<.08f||Vector3.ProjectOnPlane(gap,Vector3.up).magnitude>.16f)throw new Exception("Knees are not stacked: "+character+" side="+side+" action="+action+" gap="+gap);
                        }
                        var thigh=side==0?rig.leftThigh:rig.rightThigh;var calf=side==0?rig.leftCalf:rig.rightCalf;
                        if(Mathf.Abs(Vector3.Distance(thigh.position,calf.position)-segmentLengths[side*2])>.001f||Mathf.Abs(Vector3.Distance(calf.position,feet[side].position)-segmentLengths[side*2+1])>.001f)throw new Exception("Leg segment length changed");
                        if(!rig.anatomicalLegs[side].gameObject.activeInHierarchy||!rig.hosiery[side].gameObject.activeInHierarchy)throw new Exception("A leg surface disappeared with its shoe");
                    }
                    if(film&&action>=8&&action<=20.8f)
                    {
                        var movieAim=rig.transform.position+new Vector3(0,.84f,-.10f);
                        camera.transform.position=movieAim+new Vector3(.60f,.07f,-2.85f);camera.transform.LookAt(movieAim);camera.fieldOfView=40;
                        HomecomingSetup.Capture(camera,folder+"/film/"+(movieFrame++).ToString("D4")+".png",640,800);
                    }
                    if(!requested.Contains(shot)||!done.Add(shot))continue;
                    float Flex(Transform thigh,Transform knee,Transform foot)=>180-Vector3.Angle(thigh.position-knee.position,foot.position-knee.position);
                    var lk=rig.leftCalf.position;var rk=rig.rightCalf.position;
                    report.AppendLine(string.Join(",",new[]{action,lk.x,lk.y,lk.z,rk.x,rk.y,rk.z,feet[0].position.y,feet[1].position.y,Flex(rig.leftThigh,rig.leftCalf,feet[0]),Flex(rig.rightThigh,rig.rightCalf,feet[1])}.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                    if(!render)continue;
                    var aim=rig.transform.position+new Vector3(0,.73f,-.10f);
                    camera.fieldOfView=40;
                    camera.transform.position=aim+new Vector3(.08f,.04f,-2.55f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,folder+"/"+shot+"-front.png",1000,1000);
                    camera.transform.position=aim+new Vector3(2.25f,.05f,-.55f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,folder+"/"+shot+"-side.png",1000,1000);
                    var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var enabled=skins.Select(s=>s.enabled).ToArray();var materials=skins.Select(s=>s.sharedMaterials).ToArray();
                    var blocks=skins.Select(s=>{var b=new MaterialPropertyBlock();s.GetPropertyBlock(b);return b;}).ToArray();
                    var diagnostic=new List<Material>();
                    var proxies=story.transform.Find("脱下的通勤鞋");bool proxyEnabled=proxies&&proxies.gameObject.activeSelf;if(proxies)proxies.gameObject.SetActive(false);
                    for(int i=0;i<skins.Length;i++)
                    {
                        int side=Array.IndexOf(rig.anatomicalLegs,skins[i]);skins[i].enabled=side>=0;
                        if(side<0)continue;
                        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=side==0?new Color(.13f,.65f,.95f):new Color(1,.32f,.18f);material.SetFloat("_Cull",0);diagnostic.Add(material);
                        skins[i].sharedMaterial=material;skins[i].SetPropertyBlock(null);
                    }
                    camera.transform.position=aim+new Vector3(.08f,.04f,-2.55f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,folder+"/"+shot+"-legs.png",1000,1000);
                    for(int i=0;i<skins.Length;i++){skins[i].enabled=enabled[i];skins[i].sharedMaterials=materials[i];skins[i].SetPropertyBlock(blocks[i]);}
                    if(proxies)proxies.gameObject.SetActive(proxyEnabled);foreach(var material in diagnostic)UnityEngine.Object.DestroyImmediate(material);
                }
                File.WriteAllText(folder+"/knees.csv",report.ToString());
                File.WriteAllText(folder+"/result.json","{\"character\":"+character+",\"filmFps\":30,\"filmFrames\":"+movieFrame+",\"maximumWristError\":"+maximumWrist.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"}");
                Debug.Log("CROSSED KNEES CAPTURE: "+character+" / "+done.Count+" poses; blue left leg, orange right leg; actual Unity skinning; wrist error="+maximumWrist+" action="+worstWristAction+" side="+worstWristSide+worstWristDetails);
            }
        }
    }
}
