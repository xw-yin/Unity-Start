using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Homecoming.Editor
{
    // Run the actual scene Update in Play Mode at a deterministic 30 Hz.
    [InitializeOnLoad]
    public static class HomecomingContinuousReview
    {
        const string Key="Homecoming.ContinuousReview";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static HomecomingScene story;
        static CharacterRig rig;
        static int character,frame;
        static Transform[] bones;
        static Quaternion[] previous;
        static float maximumAngle,maximumReach;
        static string worstAngle;
        static readonly StringBuilder report=new StringBuilder();
        internal static bool Fast=>SessionState.GetBool(Key+".Fast",false);
        static HomecomingContinuousReview()
        {
            EditorApplication.playModeStateChanged+=State;
            if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying)StartTimeline();
        }
        public static void Run()=>RunCore(false);
        public static void FastPair()
        {
            SessionState.SetInt(Key+".First",0);SessionState.SetInt(Key+".Last",2);RunCore(true);
        }
        public static void GarmentThenAll()
        {
            HomecomingGarmentReview.Run();
            BootExtractionReview.FastAll();
        }
        public static void FastAll()
        {
            SessionState.SetInt(Key+".First",0);SessionState.SetInt(Key+".Last",6);RunCore(true);
        }
        static void RunCore(bool fast)
        {
            SessionState.SetBool(Key+".Fast",fast);
            SessionState.SetBool(Key,true);
            SessionState.SetBool(Key+".Reload",EditorSettings.enterPlayModeOptionsEnabled);
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            Directory.CreateDirectory("Artifacts/ContinuousReview");
            EditorApplication.EnterPlaymode();
        }
        public static void RebuildGownsAndRun()
        {
            WitcherCharacterImport.Import("Iris");WitcherCharacterImport.Import("AnnaHenrietta");
            HomecomingFootwearReview.Run();Run();
        }
        public static void ReviewDuchessOnly()
        {
            SessionState.SetInt(Key+".First",6);Run();
        }
        public static void FullReview()
        {
            HomecomingFootwearReview.Run();HomecomingGarmentReview.Run();
            SessionState.SetInt(Key+".First",0);Run();
        }
        public static void FinalGownReview()
        {
            WitcherCharacterImport.Import("Iris");WitcherCharacterImport.Import("AnnaHenrietta");
            HomecomingFootwearReview.Run();HomecomingGarmentReview.Run();
            SessionState.SetInt(Key+".First",4);Run();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                StartTimeline();
            }
            if(state==PlayModeStateChange.EnteredEditMode){EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+".Reload",false);SessionState.SetBool(Key,false);SessionState.EraseInt(Key+".Last");EditorApplication.Exit(0);}
        }
        static void StartTimeline()
        {
            character=SessionState.GetInt(Key+".First",0);frame=-1;report.Clear();report.AppendLine("character,maxBoneAnglePerFrame,maxWristError,worstBoneAndTime");
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Select()
        {
            story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            story.SelectCharacter(character);story.enabled=false;
            rig=story.GetComponentInChildren<CharacterRig>();
            typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
            bones=new[]{rig.hips,rig.spine,rig.head,rig.leftArm,rig.rightArm,rig.leftElbow,rig.rightElbow,rig.leftThigh,rig.rightThigh,rig.leftCalf,rig.rightCalf}
                .Concat(rig.GetComponentsInChildren<Transform>().Where(t=>new[]{"J_Bip_L_Hand","J_Bip_R_Hand","J_Bip_L_Foot","J_Bip_R_Foot","J_Bip_L_ToeBase","J_Bip_R_ToeBase"}.Contains(t.name))).ToArray();
            previous=bones.Select(b=>b.rotation).ToArray();maximumAngle=maximumReach=0;worstAngle="";frame=0;
        }
        static void Tick()
        {
            try
            {
                if(frame<0){Select();return;}
                // Several simulation steps per editor callback; each still runs the
                // actual gameplay Update, shoe proxies, expressions and cloth.
                for(int batch=0;batch<8;batch++)
                {
                    float time=frame/30f;
                    typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,time-Time.deltaTime);
                    typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                    var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                    BootExtractionReview.Capture(story,rig,motion,character,frame);
                    for(int b=0;b<bones.Length;b++)
                    {
                        float angle=Quaternion.Angle(previous[b],bones[b].rotation);previous[b]=bones[b].rotation;
                        if(frame>0&&angle>maximumAngle){maximumAngle=angle;worstAngle=bones[b].name+"@"+time;}
                    }
                    for(int side=0;side<2;side++)
                    {
                        var sock=rig.hosiery[side];
                        float action=time-1;
                        bool dressed=rig.dressedLegs!=null&&rig.dressedLegs.Length==2;
                        bool extracted=action>=HomecomingShoeGrip.Detach(side);
                        if(!sock.gameObject.activeInHierarchy||sock.enabled!=(dressed?extracted:true))throw new Exception("Incorrect dressed / extracted stocking layer: "+character);
                        if(dressed&&(rig.dressedLegs[side].enabled==extracted||rig.dressedHosiery[side].enabled==extracted))throw new Exception("DLC outfit surface is not exclusive with the full foot layer");
                        if(action>=HomecomingShoeGrip.Detach(side)&&action<HomecomingShoeGrip.Release(side))
                        {
                            var hand=rig.GetComponentsInChildren<Transform>().Single(b=>b.name==(side==0?"J_Bip_L_Hand":"J_Bip_R_Hand"));
                            maximumReach=Mathf.Max(maximumReach,Vector3.Distance(hand.position,motion.Shoes.WristTarget(side,action)));
                            var shoe=story.transform.Find("脱下的通勤鞋").GetChild(side);
                            if(motion.Shoes.ContactError(shoe,side)>.002f)throw new Exception("Held shoe lost contact: character="+character+" frame="+frame+" side="+side+" error="+motion.Shoes.ContactError(shoe,side));
                            var properties=new MaterialPropertyBlock();sock.GetPropertyBlock(properties);
                            float coverageEnd=motion.Shoes.AnatomicalExtraction?HomecomingShoeGrip.Detach(side):motion.Shoes.Cleared(side);
                            float expected=action<coverageEnd?1:0;
                            if(properties.GetFloat("_BootMask")!=expected)throw new Exception("Incorrect boot extraction coverage");
                        }
                    }
                    bool stage=new[]{0,90,150,210,270,300,330,360,390,405,420,450,510,570,630,780,840,960}.Contains(frame);
                    bool movie=(character==0||character==4||character==6)&&frame%5==0;
                    if(!Fast&&new[]{0,2,3}.Contains(character)&&new[]{240,330,675}.Contains(frame))
                    {
                        var camera=story.CameraControls.GetComponent<Camera>();var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
                        var aim=rig.transform.position+new Vector3(0,.85f,0);camera.transform.position=aim+new Vector3(.1f,.04f,-2.8f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                        CaptureGpu(camera,"Artifacts/DLCOutfitReview/"+character+"-"+frame+"-gpu-front.png");camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
                    }
                    if(!Fast&&character==0&&new[]{240,389,390,405,420,570,600,675}.Contains(frame))
                    {
                        var camera=story.CameraControls.GetComponent<Camera>();var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
                        var aim=rig.transform.TransformPoint(new Vector3(0,.39f,-.04f));camera.transform.position=aim+new Vector3(.28f,.13f,-1.55f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                        CaptureGpu(camera,"Artifacts/ContinuousReview/0/"+frame+"-gpu-front.png");
                        camera.transform.position=aim+new Vector3(1.3f,.12f,-.7f);camera.transform.LookAt(aim);CaptureGpu(camera,"Artifacts/ContinuousReview/0/"+frame+"-gpu-side.png");
                        camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
                    }
                    if(!Fast&&(stage||movie))
                    {
                        var camera=story.CameraControls.GetComponent<Camera>();
                        if(time<8){story.CameraControls.SetView(0);story.CameraControls.SnapForReview();}
                        else
                        {
                            camera.transform.position=new Vector3(-.65f,1.25f,-1.8f);
                            camera.transform.LookAt(new Vector3(-1.25f,.8f,1.25f));camera.fieldOfView=44;
                        }
                        string folder="Artifacts/ContinuousReview/"+character;Directory.CreateDirectory(folder);
                        HomecomingSetup.Capture(camera,folder+"/"+frame.ToString("D4")+".png",960,720);
                    }
                    if(++frame>960)
                    {
                        var originalRig=Resources.Load<GameObject>(HomecomingCharacters.ResourcesPaths[character]).GetComponent<CharacterRig>();
                        for(int side=0;side<2;side++)
                        {
                            var renderer=rig.hosiery[side];var actual=renderer.sharedMesh;var original=originalRig.hosiery[side].sharedMesh;
                            if(!actual.vertices.SequenceEqual(original.vertices)||!actual.boneWeights.SequenceEqual(original.boneWeights)||!actual.uv.SequenceEqual(original.uv)||!actual.triangles.SequenceEqual(original.triangles))
                                throw new Exception("Original hosiery positions / weights were not restored after extraction: "+character+" / "+side);
                            foreach(string fit in new[]{"BootFit","WornHighHeel"})
                            {
                                int index=actual.GetBlendShapeIndex(fit);
                                if(index>=0&&renderer.GetBlendShapeWeight(index)>.0001f)throw new Exception("Worn corrective remains active after footwear removal: "+character+" / "+side+" / "+fit);
                            }
                        }
                        report.AppendLine(HomecomingCharacters.Names[character]+","+maximumAngle+","+maximumReach+","+worstAngle);
                        File.WriteAllText("Artifacts/ContinuousReview/results.csv",report.ToString());
                        Debug.Log("CONTINUOUS PLAY REVIEW: "+HomecomingCharacters.Names[character]+"; 961 frames; max bone angle="+maximumAngle+" "+worstAngle+"; max wrist error="+maximumReach);
                        if(maximumReach>.04f)throw new Exception("Hand cannot reach shoe: "+character);
                        if(maximumAngle>30)throw new Exception("Abrupt skeletal turn: "+character+" "+worstAngle+" angle="+maximumAngle);
                        story.Replay();if(rig.transform.position.z<3.8f||!rig.leftShoe.activeSelf)throw new Exception("Replay state incorrect");
                        if(++character>SessionState.GetInt(Key+".Last",HomecomingCharacters.Names.Length-1))
                        {
                            Debug.Log("CONTINUOUS PLAY REVIEW PASSED: complete timelines, held footwear, intact hosiery, bone continuity and replay.");
                            EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();return;
                        }
                        Select();return;
                    }
                }
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.update-=Tick;SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        // Keep the actual SkinnedMeshRenderers, blendshapes and property blocks
        // on the runtime rendering path, instead of replacing them with BakeMesh.
        internal static void CaptureGpu(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var target=new RenderTexture(1100,1000,24);var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;Texture2D image=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(1100,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1100,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
