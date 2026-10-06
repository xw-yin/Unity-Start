using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Homecoming.Editor {
public static class HandMovementReview {
 public static void FullReview(){Capture();Validate();HomecomingContinuousReview.FullReview();}
 public static void FilmAndFullReview(){Validate();CaptureFilm();HomecomingContinuousReview.FullReview();}
 [MenuItem("Homecoming/审阅全流程手部动作")]
 public static void Validate(){
  HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;var report=new System.Text.StringBuilder("character,maxHandBoneAngle,maxWristTravelRelativeToBody,maxWristBend,maxGripError,maxFabricError,maxPinchGap,worstBone\n");
  for(int character=0;character<7;character++) {
   story.SelectCharacter(character);var rig=story.GetComponentInChildren<CharacterRig>();
   var bones=rig.GetComponentsInChildren<Transform>().Where(b=>b.name.StartsWith("J_Bip_")&&(b.name.Contains("Hand")||b.name.Contains("Index")||b.name.Contains("Middle")||b.name.Contains("Ring")||b.name.Contains("Little")||b.name.Contains("Thumb")||b==rig.leftElbow||b==rig.rightElbow)).ToArray();
   var previous=bones.Select(b=>b.rotation).ToArray();var wrist=new Vector3[2];float angle=0,travel=0,gripError=0,fabricError=0,gap=0,bend=0;string worst="",worstTravel="",worstBend="";
   typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
   for(int frame=0;frame<=960;frame++) {
    float time=frame/30f,action=time-1;typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,time-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
    var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",flags).GetValue(story);

    for(int i=0;i<bones.Length;i++){float change=Quaternion.Angle(previous[i],bones[i].rotation);if(frame>0&&change>angle){angle=change;worst=bones[i].name+"@"+time;}previous[i]=bones[i].rotation;}
    for(int side=0;side<2;side++) {
     var hand=motion.Hands.Hand(side);if(frame>0){float delta=Vector3.Distance(rig.transform.InverseTransformPoint(hand.position),wrist[side]);if(delta>travel){travel=delta;worstTravel=side+"@"+time;}}wrist[side]=rig.transform.InverseTransformPoint(hand.position);
     var elbow=side==0?rig.leftElbow:rig.rightElbow;var middle=hand.GetComponentsInChildren<Transform>().FirstOrDefault(x=>x.name.EndsWith("Middle1"));
     if(time>=2.6f&&time<=6.2f){
      var inward=rig.transform.right*(side==0?-1:1);
      if(Vector3.Dot(motion.Hands.Palm(side,hand.rotation),inward)<.65f)throw new Exception("Walking palm turned away from thigh: "+character+"@"+time);
      if(Vector3.Angle(middle.position-hand.position,hand.position-elbow.position)>15)throw new Exception("Walking wrist folds: "+character+"@"+time);
     }
     if(middle){float flex=Vector3.Angle(middle.position-hand.position,hand.position-elbow.position);if(flex>bend){bend=flex;worstBend=side+"@"+time;}}
     if(action>=HomecomingShoeGrip.Detach(side)&&action<HomecomingShoeGrip.Release(side)){
      gripError=Mathf.Max(gripError,Vector3.Distance(hand.position,motion.Shoes.WristTarget(side,action)));
      var shoulder=side==0?rig.leftArm:rig.rightArm;
      if(elbow.position.y>shoulder.position.y+.02f)throw new Exception("Raised elbow while holding footwear below the torso: "+character+" side="+side+"@"+time);
     }
     float lift=side==0?rig.RightDressLift:rig.LeftDressLift;
     if(lift>.98f&&rig.skirt.sharedMesh.GetBlendShapeIndex("BootLeft")>=0) {fabricError=Mathf.Max(fabricError,Vector3.Distance(motion.Hands.Contact(side,HomecomingHandPose.Hold.Fabric),rig.DressHoldPoint(1-side)));gap=Mathf.Max(gap,motion.Hands.PinchGap(side));}
    }
   }
   if(angle>28||bend>95||gripError>.025f||fabricError>.025f||gap>.018f||travel>.10f)throw new Exception("Hand motion failed: "+character+" angle="+angle+" bend="+bend+" grip="+gripError+" fabric="+fabricError+" pinch="+gap+" travel="+travel+" worst="+worst+" bendAt="+worstBend);
   report.AppendLine(character+","+angle+","+travel+","+bend+","+gripError+","+fabricError+","+gap+","+worst);
   Debug.Log("HAND REVIEW METRICS "+character+" angle="+angle+" travel="+travel+" grip="+gripError+" fabric="+fabricError+" pinch="+gap+" worst="+worst+" travelAt="+worstTravel+" bend="+bend+" bendAt="+worstBend);
  }
  Directory.CreateDirectory("Artifacts/HandMovementReview");File.WriteAllText("Artifacts/HandMovementReview/metrics.csv",report.ToString());Debug.Log("HAND MOVEMENT REVIEW PASSED: seven 961-frame timelines; actual finger contact, pinch, wrist flex and continuity.");
 }
 // Run in the isolated validation project; encode the numbered PNGs at 30 fps.
 public static void CaptureFilm(){
  HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(1);var rig=story.GetComponentInChildren<CharacterRig>();
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);Directory.CreateDirectory("Artifacts/HandMotionFilm");
  for(int frame=0;frame<=960;frame++){
   typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,frame/30f-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
   if(frame<90)continue;var camera=story.CameraControls.GetComponent<Camera>();camera.transform.position=rig.transform.position+new Vector3(.85f,1.15f,-1.8f);camera.transform.LookAt(rig.hips.position+new Vector3(0,.08f,-.08f));camera.fieldOfView=46;
   HomecomingSetup.Capture(camera,"Artifacts/HandMotionFilm/"+(frame-90).ToString("D4")+".png",720,960);
  }
  Debug.Log("HAND MOTION FILM COMPLETE: 871 frames at 30 fps.");
 }
 [MenuItem("Homecoming/保存手部动作关键帧")]
 public static void Capture(){
  HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  foreach(int character in new[]{0,1,2,5,6}) {
   story.SelectCharacter(character);var rig=story.GetComponentInChildren<CharacterRig>();
   var hands=rig.GetComponentsInChildren<Transform>().Where(x=>x.name=="J_Bip_L_Hand"||x.name=="J_Bip_R_Hand").ToArray();
   Debug.Log("HAND DIMENSIONS "+character+" shoulder="+rig.leftArm.position+" elbow="+rig.leftElbow.position+" wrist="+hands[0].position+" arm length="+(Vector3.Distance(rig.leftArm.position,rig.leftElbow.position)+Vector3.Distance(rig.leftElbow.position,hands[0].position)));
   typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
   for(int frame=0;frame<=960;frame++) {
    float time=frame/30f;typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,time-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
    if(new[]{0,90,240,270,315,345,360,375,390,405,420,447,465,495,525,555,585,615,660,720,780,840}.Contains(frame)) {
     var camera=story.CameraControls.GetComponent<Camera>();
     camera.transform.position=rig.transform.position+new Vector3(.85f,1.15f,-1.65f);camera.transform.LookAt(rig.hips.position+new Vector3(0,.08f,-.08f));camera.fieldOfView=42;
     string folder="Artifacts/HandMovementReview/"+character;Directory.CreateDirectory(folder);HomecomingSetup.Capture(camera,folder+"/"+frame.ToString("D4")+".png",1000,1000);
    }
   }
  }
  Debug.Log("HAND MOVEMENT CAPTURE COMPLETE");
 }
}}
