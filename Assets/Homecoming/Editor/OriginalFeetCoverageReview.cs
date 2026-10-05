using System;
using System.Linq;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace Homecoming.Editor {
public static class OriginalFeetCoverageReview {
 [MenuItem("Homecoming/验证原版腿脚与鞋内遮挡")]
 public static void Build() {
  foreach(string name in new[]{"YenneferDLC","Shani"}) {
   string path="Assets/Homecoming/Resources/Homecoming/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try {
    var rig=root.GetComponent<CharacterRig>();
    string folder=AssetDatabase.GetAssetPath(rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh).Split(new[]{"Meshes/"},StringSplitOptions.None)[0];
    foreach(var r in rig.anatomicalLegs.Concat(rig.hosiery).ToArray())if(r)UnityEngine.Object.DestroyImmediate(r.gameObject);
    WitcherCharacterFeet.Build(rig,folder);rig.RemoveHomeAccessories();rig.CaptureRestPose();PrefabUtility.SaveAsPrefabAsset(root,path);
   } finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  AssetDatabase.SaveAssets();HomecomingFootwearReview.Run();Capture();
 }
 public static void Capture() {
  var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  Directory.CreateDirectory("Artifacts/OriginalFeetCoverage");
  for(int character=0;character<=1;character++) {
   story.SelectCharacter(character);var rig=story.GetComponentInChildren<CharacterRig>();
   typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
   for(int frame=0;frame<=960;frame++) {
    float time=frame/30f;
    typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,time-Time.deltaTime);
    typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
    for(int side=0;side<2;side++) {
     var sock=rig.hosiery[side];var props=new MaterialPropertyBlock();sock.GetPropertyBlock(props);
     if(time>1.1f&&time<27) {
      var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",flags).GetValue(story);
      bool attached=time-1<motion.Shoes.Cleared(side);
      if(props.GetFloat("_BootMask")!=(attached?1:0))throw new Exception("Incorrect coverage "+character+" "+side+" "+time);
      if(attached&&props.GetVector("_BootBoundsMax").y<=props.GetVector("_BootBoundsMin").y)throw new Exception("No boot coverage bounds");
     }
     if(!sock.enabled||!sock.gameObject.activeInHierarchy)throw new Exception("Missing sock foot");
    }
    if(new[]{0,180,330,375,405,420,510,660,840}.Contains(frame)) {
     var camera=story.CameraControls.GetComponent<Camera>();
     camera.transform.position=new Vector3(-.55f,.58f,-.05f);camera.transform.LookAt(new Vector3(-1.25f,.40f,1.25f));camera.fieldOfView=36;
     HomecomingSetup.Capture(camera,"Artifacts/OriginalFeetCoverage/"+character+"-"+frame+".png",1200,1000);
    }
   }
   if(character==0) {
    string skin=AssetDatabase.GetAssetPath(rig.anatomicalLegs[0].sharedMaterial.GetTexture("_BaseMap"));
    if(!skin.Contains("WitcherYenneferOriginalFeet/"))throw new Exception("Yennefer uses generic feet");
    foreach(var leg in rig.anatomicalLegs)leg.enabled=true;foreach(var sock in rig.hosiery)sock.enabled=false;
    var camera=story.CameraControls.GetComponent<Camera>();camera.transform.position=new Vector3(-.55f,.48f,-.05f);camera.transform.LookAt(new Vector3(-1.25f,.25f,1.25f));camera.fieldOfView=32;
    HomecomingSetup.Capture(camera,"Artifacts/OriginalFeetCoverage/Yennefer-original-barefoot-reference.png",1200,1000);
    foreach(var leg in rig.anatomicalLegs)leg.enabled=false;foreach(var sock in rig.hosiery)sock.enabled=true;
    foreach(var leg in rig.anatomicalLegs){var v=leg.sharedMesh.vertices;if(v.Min(p=>p.y)>.02f||v.Max(p=>p.y)<.6f)throw new Exception("Yennefer native leg incomplete");}
   }
  }
  Debug.Log("ORIGINAL FEET COVERAGE PASSED: 1922 frames, Shani named socks masked inside boots, full retained feet after detachment, Yennefer native legs and feet.");
 }
}}
