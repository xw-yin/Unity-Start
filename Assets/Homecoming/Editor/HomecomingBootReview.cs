using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
namespace Homecoming.Editor {
 public static class HomecomingBootReview {
  public static void Run(){
   HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();var rig=story.GetComponentInChildren<CharacterRig>();
   if(!rig.longBoots)throw new Exception("Long boot profile not enabled");
   foreach(var skin in new[]{rig.leftShoe.GetComponent<SkinnedMeshRenderer>(),rig.rightShoe.GetComponent<SkinnedMeshRenderer>()})if(skin.sharedMesh.bounds.size.y<.45f||skin.sharedMesh.bounds.size.y>.60f)throw new Exception("Boot shaft geometry missing or includes trousers");
   var f=BindingFlags.Instance|BindingFlags.NonPublic;typeof(HomecomingScene).GetField("started",f).SetValue(story,true);
   var hands=rig.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="J_Bip_L_Hand"||x.name=="J_Bip_R_Hand").OrderBy(x=>x.name).ToArray();float maxReach=0,maxFloor=0;string worst="";
   for(int frame=0;frame<=330;frame++){
    float actual=frame/10f,action=actual-1;typeof(HomecomingScene).GetField("elapsed",f).SetValue(story,actual);typeof(HomecomingScene).GetMethod("Update",f).Invoke(story,null);
    var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",f).GetValue(story);
    for(int side=0;side<2;side++){
     if(action>HomecomingShoeGrip.Start(side)+1.4f&&action<HomecomingShoeGrip.Release(side)){float error=Vector3.Distance(hands[side].position,motion.Shoes.WristTarget(side,action));if(error>maxReach){maxReach=error;worst=" time="+actual+" side="+side+" wrist="+hands[side].position+" target="+motion.Shoes.WristTarget(side,action);}}
    }
    var group=story.transform.Find("脱下的通勤鞋");if(group)foreach(var mesh in group.GetComponentsInChildren<MeshFilter>())if(mesh.gameObject.activeInHierarchy)foreach(var v in mesh.sharedMesh.vertices){float y=mesh.transform.TransformPoint(v).y;maxFloor=Mathf.Max(maxFloor,-y);if(y<-.012f)throw new Exception("Held boot crosses floor at "+actual+" y="+y);}
   }
   if(maxReach>.035f)throw new Exception("Hand cannot reach boot cuff during extraction: "+maxReach+worst);
   Debug.Log("LONG BOOT REVIEW PASSED: complete calf shafts, 331 timeline samples, maximum wrist error="+maxReach+", floor penetration="+maxFloor);
  }
 }
}
