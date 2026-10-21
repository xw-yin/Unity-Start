using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Homecoming.Editor
{
    public static class YenneferAppearanceReview
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        public const string OutputRoot="Artifacts/CostumeReview";
        [Serializable] class Surface {public string name,mesh,material,shader;public bool enabled;public int vertices;public float smoothness,normalStrength;public string albedo,normal,specular;}
        [Serializable] class Inventory {public Surface[] surfaces;}
        public static void Baseline()=>Capture("baseline");
        public static void Current()=>Capture("current");
        public static void CostumeIteration(){Baseline();YenneferAppearanceMaterials.Apply();Current();}
        [MenuItem("Homecoming/审阅叶奈法外观还原")]
        public static void Review()=>Current();
        static void Capture(string label)
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(0);story.ReloadCharacter();
            var rig=story.GetComponentInChildren<CharacterRig>();
            var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
            motion.Sample(motion.ElapsedForAction(7.01f));motion.ResetFace();
            var cam=story.CameraControls.GetComponent<Camera>();
            string folder=OutputRoot+"/"+label;Directory.CreateDirectory(folder);
            var headSkin=rig.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.StartsWith("h_01_wa__yennefer_hq"));
            var headMesh=new Mesh();headSkin.BakeMesh(headMesh);var vertices=headMesh.vertices.Select(v=>headSkin.transform.TransformPoint(v)).ToArray();var headBounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)headBounds.Encapsulate(v);UnityEngine.Object.DestroyImmediate(headMesh);
            var face=headBounds.center+Vector3.up*headBounds.size.y*.12f;
            Debug.Log("APPEARANCE HEAD BOUNDS "+headBounds+" HEAD BONE "+rig.head.position+" FACE AIM "+face);
            Take(cam,face,new Vector3(0,.015f,-.65f),38,folder+"/room-face.png",1000,1100);
            var body=rig.transform.position+new Vector3(0,.92f,0);
            Take(cam,body,new Vector3(.35f,.05f,-3.05f),38,folder+"/room-body.png",1000,1300);
            var collar=rig.transform.position+new Vector3(0,1.38f,0);
            Take(cam,collar,new Vector3(.32f,.03f,-1.12f),38,folder+"/room-costume.png",1100,1000);
            var lights=UnityEngine.Object.FindObjectsByType<Light>();foreach(var light in lights)light.enabled=false;
            cam.cullingMask=1<<8;cam.backgroundColor=new Color(.24f,.25f,.27f);cam.clearFlags=CameraClearFlags.SolidColor;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.40f,.42f,.45f);RenderSettings.ambientEquatorColor=new Color(.30f,.31f,.33f);RenderSettings.ambientGroundColor=new Color(.18f,.18f,.19f);
            StudioLight("Key",new Vector3(25,-25,0),1.0f,new Color(1,.96f,.92f),true);
            StudioLight("Fill",new Vector3(12,48,0),.45f,new Color(.88f,.94f,1),false);
            StudioLight("Rim",new Vector3(25,150,0),.65f,new Color(.92f,.95f,1),false);
            var volume=UnityEngine.Object.FindAnyObjectByType<Volume>();
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);if(volume)volume.sharedProfile=profile;
            Take(cam,face,new Vector3(0,.015f,-.65f),38,folder+"/face-front.png",1000,1100);
            Take(cam,face,new Vector3(.35f,.015f,-.55f),38,folder+"/face-three-quarter.png",1000,1100);
            Take(cam,face,new Vector3(.65f,.015f,-.07f),38,folder+"/face-side.png",1000,1100);
            Take(cam,collar,new Vector3(.32f,.03f,-1.12f),38,folder+"/costume-front.png",1100,1000);
            Take(cam,collar,new Vector3(1.12f,.04f,-.28f),38,folder+"/costume-side.png",1100,1000);
            foreach(var view in new[]{("front",new Vector3(0,0,-3.05f)),("side",new Vector3(3.05f,0,0)),("back",new Vector3(0,0,3.05f))})
                Take(cam,body,view.Item2,38,folder+"/body-"+view.Item1+".png",1000,1300);
            string PathOf(Material m,string property)=>m.HasProperty(property)&&m.GetTexture(property)?AssetDatabase.GetAssetPath(m.GetTexture(property)):"";
            var inventory=new Inventory{surfaces=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new Surface{name=s.name,mesh=AssetDatabase.GetAssetPath(s.sharedMesh),material=AssetDatabase.GetAssetPath(s.sharedMaterial),shader=s.sharedMaterial.shader.name,enabled=s.enabled,vertices=s.sharedMesh.vertexCount,smoothness=s.sharedMaterial.HasProperty("_Smoothness")?s.sharedMaterial.GetFloat("_Smoothness"):0,normalStrength=s.sharedMaterial.HasProperty("_BumpScale")?s.sharedMaterial.GetFloat("_BumpScale"):0,albedo=PathOf(s.sharedMaterial,"_BaseMap"),normal=PathOf(s.sharedMaterial,"_BumpMap"),specular=PathOf(s.sharedMaterial,"_SpecGlossMap")}).ToArray()};
            File.WriteAllText(folder+"/inventory.json",JsonUtility.ToJson(inventory,true));
            Debug.Log("YENNEFER APPEARANCE CAPTURED: "+label+"; identical neutral pose, camera and studio lights; no topology edits.");
        }
        static void StudioLight(string name,Vector3 angles,float intensity,Color color,bool shadows)
        {var obj=new GameObject(name);obj.transform.rotation=Quaternion.Euler(angles);var light=obj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.color=color;light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowBias=.025f;light.shadowNormalBias=.08f;}
        static void Take(Camera cam,Vector3 aim,Vector3 offset,float fov,string path,int width,int height)
        {cam.transform.position=aim+offset;cam.transform.LookAt(aim);cam.fieldOfView=fov;HomecomingSetup.Capture(cam,path,width,height);}
    }
}
