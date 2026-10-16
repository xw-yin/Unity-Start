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
    public static class HomecomingLegRigReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [MenuItem("Homecoming/近景审阅腿脚变形")]
        public static void Run()=>Capture(new[]{0});
        public static void Film()=>Capture(new[]{0},true,false);
        public static void FilmSide()=>Capture(new[]{0},true,false,true);
        public static void Pair()=>Capture(new[]{0,2});
        public static void Validate()=>Capture(new[]{0,2},false,false);
        public static void ValidateAll()=>Capture(Enumerable.Range(0,HomecomingCharacters.Names.Length).ToArray(),false,false);
        public static void Delivery(){HomecomingLegRigAudit.Run();Pair();Film();}
        public static void Regression(){ValidateAll();HomecomingFootwearReview.Run();HomecomingMotionReview.Run();}
        public static void All()=>Capture(Enumerable.Range(0,HomecomingCharacters.Names.Length).ToArray());
        [Serializable] public class BoneRecord{public string name,parent;public Vector3 position,localPosition;public Quaternion rotation,localRotation;}
        [Serializable] public class BoneList{public BoneRecord[] bones;}
        static void Capture(int[] characters,bool film=false,bool render=true,bool sideFilm=false)
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.ReloadCharacter();
            var requested=new[]{249,282,300,321,342,354,360,372,378,414,447,465,486,507,525,546,582,660,720,750,795,825};
            foreach(int character in characters)
            {
                story.SelectCharacter(character);typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                var rig=story.GetComponentInChildren<CharacterRig>();
                var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                var camera=story.CameraControls.GetComponent<Camera>();
                var feet=rig.GetComponentsInChildren<Transform>().Where(t=>t.name=="J_Bip_L_Foot"||t.name=="J_Bip_R_Foot").OrderBy(t=>t.name).ToArray();
                string folder="Artifacts/LegRigReview/current/"+character;Directory.CreateDirectory(folder);
                var report=new StringBuilder("action,leftKneeX,leftKneeY,leftKneeZ,rightKneeX,rightKneeY,rightKneeZ,leftAnkleY,rightAnkleY,leftFlex,rightFlex\n");
                var segmentLengths=new[]{Vector3.Distance(rig.leftThigh.position,rig.leftCalf.position),Vector3.Distance(rig.leftCalf.position,feet[0].position),Vector3.Distance(rig.rightThigh.position,rig.rightCalf.position),Vector3.Distance(rig.rightCalf.position,feet[1].position)};
                var done=new HashSet<int>();float maximumAnkle=0,minimumKnee=180;float maximumWrist=0,worstWristAction=0;int worstWristSide=0,movieFrame=0;string worstWristDetails="";
                string filmFolder=folder+(sideFilm?"/film-side":"/film");
                if(film)Directory.CreateDirectory(filmFolder);
                for(int frame=0;frame<=Mathf.CeilToInt(motion.ElapsedForAction(27.6f)*30);frame++)
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
                        var thigh=side==0?rig.leftThigh:rig.rightThigh;var calf=side==0?rig.leftCalf:rig.rightCalf;
                        if(rig.longBoots&&action>HomecomingShoeGrip.Start(side)+.5f&&action<HomecomingShoeGrip.Release(side))
                        {
                            var rest=rig.restPoses.Single(p=>p.bone==feet[side]).rotation;
                            maximumAnkle=Mathf.Max(maximumAnkle,Quaternion.Angle(rest,feet[side].localRotation));
                            minimumKnee=Mathf.Min(minimumKnee,180-Vector3.Angle(thigh.position-calf.position,feet[side].position-calf.position));
                        }
                        if(Mathf.Abs(Vector3.Distance(thigh.position,calf.position)-segmentLengths[side*2])>.001f||Mathf.Abs(Vector3.Distance(calf.position,feet[side].position)-segmentLengths[side*2+1])>.001f)throw new Exception("Leg segment length changed");
                        if(!rig.anatomicalLegs[side].gameObject.activeInHierarchy||!rig.hosiery[side].gameObject.activeInHierarchy)throw new Exception("A leg surface disappeared with its shoe");
                    }
                    if(film&&action>=8&&action<=20.8f)
                    {
                        var movieAim=rig.transform.position+new Vector3(0,.84f,-.10f);
                        camera.transform.position=movieAim+(sideFilm?new Vector3(2.8f,.07f,-.65f):new Vector3(.60f,.07f,-2.85f));camera.transform.LookAt(movieAim);camera.fieldOfView=40;
                        HomecomingSetup.Capture(camera,filmFolder+"/"+(movieFrame++).ToString("D4")+".png",640,800);
                    }
                    if(!requested.Contains(shot)||!done.Add(shot))continue;
                    float Flex(Transform thigh,Transform knee,Transform foot)=>180-Vector3.Angle(thigh.position-knee.position,foot.position-knee.position);
                    var lk=rig.leftCalf.position;var rk=rig.rightCalf.position;
                    report.AppendLine(string.Join(",",new[]{action,lk.x,lk.y,lk.z,rk.x,rk.y,rk.z,feet[0].position.y,feet[1].position.y,Flex(rig.leftThigh,rig.leftCalf,feet[0]),Flex(rig.rightThigh,rig.rightCalf,feet[1])}.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                    if(!render)continue;
                    var boneDump=rig.GetComponentsInChildren<Transform>(true).Where(b=>b.name.StartsWith("J_Bip_")||b.name.Contains("legRoll")||b.name.Contains("kneeRoll")).Select(b=>new BoneRecord{name=b.name,parent=b.parent?b.parent.name:"",position=b.position,localPosition=b.localPosition,rotation=b.rotation,localRotation=b.localRotation}).ToArray();
                    File.WriteAllText(folder+"/"+shot+"-bones.json",JsonUtility.ToJson(new BoneList{bones=boneDump},true));
                    var aim=rig.transform.position+new Vector3(0,.73f,-.10f);
                    camera.fieldOfView=40;
                    camera.transform.position=aim+new Vector3(.08f,.04f,-2.55f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,folder+"/"+shot+"-front.png",1000,1000);
                    camera.transform.position=aim+new Vector3(2.25f,.05f,-.55f);camera.transform.LookAt(aim);
                    HomecomingSetup.Capture(camera,folder+"/"+shot+"-side.png",1000,1000);
                    var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var enabled=skins.Select(s=>s.enabled).ToArray();
                    var proxies=story.transform.Find("脱下的通勤鞋");bool proxyEnabled=proxies&&proxies.gameObject.activeSelf;
                    // Bake separate diagnostic objects. Never replace live materials: doing
                    // so polluted subsequent final-material frames in the old capture loop.
                    var diagnosticRoot=new GameObject("Temporary leg diagnostics");
                    var meshes=new List<Mesh>();var materials=new List<Material>();
                    try
                    {
                        if(proxies)proxies.gameObject.SetActive(false);
                        foreach(var skin in skins)skin.enabled=false;
                        var anatomy=DiagnosticMeshes(rig.anatomicalLegs,diagnosticRoot.transform,meshes,materials);
                        camera.transform.position=aim+new Vector3(.08f,.04f,-2.55f);camera.transform.LookAt(aim);
                        HomecomingSetup.Capture(camera,folder+"/"+shot+"-legs.png",1000,1000);
                        var lowerAim=(rig.leftCalf.position+rig.rightCalf.position+feet[0].position+feet[1].position)*.25f;
                        camera.transform.position=lowerAim+new Vector3(.9f,.06f,-1.0f);camera.transform.LookAt(lowerAim);camera.fieldOfView=45;
                        HomecomingSetup.Capture(camera,folder+"/"+shot+"-feet.png",1000,1000);
                        foreach(var obj in anatomy)obj.SetActive(false);
                        DiagnosticMeshes(rig.hosiery,diagnosticRoot.transform,meshes,materials);
                        HomecomingSetup.Capture(camera,folder+"/"+shot+"-hosiery.png",1000,1000);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(diagnosticRoot);
                        foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                        foreach(var material in materials)UnityEngine.Object.DestroyImmediate(material);
                        for(int i=0;i<skins.Length;i++)skins[i].enabled=enabled[i];
                        if(proxies)proxies.gameObject.SetActive(proxyEnabled);
                    }
                }
                File.WriteAllText(folder+"/knees.csv",report.ToString());
                File.WriteAllText(folder+"/result.json","{\"character\":"+character+",\"filmFps\":30,\"filmFrames\":"+movieFrame+",\"maximumWristError\":"+maximumWrist.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"}");
                Debug.Log("LEG RIG ANGLES: "+character+" maximum ankle local deviation="+maximumAnkle+" minimum crossed knee flex="+minimumKnee);
                Debug.Log("CROSSED KNEES CAPTURE: "+character+" / "+done.Count+" poses; blue left leg, orange right leg; actual Unity skinning; wrist error="+maximumWrist+" action="+worstWristAction+" side="+worstWristSide+worstWristDetails);
            }
        }
        static GameObject[] DiagnosticMeshes(SkinnedMeshRenderer[] source,Transform parent,List<Mesh> meshes,List<Material> materials)
        {
            var objects=new GameObject[source.Length];
            for(int side=0;side<source.Length;side++)
            {
                var skin=source[side];var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);
                var obj=new GameObject(skin.name);obj.transform.SetParent(parent,false);
                obj.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);obj.transform.localScale=skin.transform.lossyScale;
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=side==0?new Color(.13f,.65f,.95f):new Color(1,.32f,.18f);material.SetFloat("_Cull",0);materials.Add(material);
                obj.AddComponent<MeshRenderer>().sharedMaterial=material;objects[side]=obj;
            }
            return objects;
        }
    }
}
