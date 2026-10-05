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
        static HomecomingContinuousReview()
        {
            EditorApplication.playModeStateChanged+=State;
            if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying)StartTimeline();
        }
        public static void Run()
        {
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
            if(state==PlayModeStateChange.EnteredEditMode){EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+".Reload",false);SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
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
            bones=new[]{rig.hips,rig.spine,rig.head,rig.leftArm,rig.rightArm,rig.leftElbow,rig.rightElbow,rig.leftThigh,rig.rightThigh,rig.leftCalf,rig.rightCalf};
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
                    for(int b=0;b<bones.Length;b++)
                    {
                        float angle=Quaternion.Angle(previous[b],bones[b].rotation);previous[b]=bones[b].rotation;
                        if(frame>0&&angle>maximumAngle){maximumAngle=angle;worstAngle=bones[b].name+"@"+time;}
                    }
                    for(int side=0;side<2;side++)
                    {
                        var sock=rig.hosiery[side];
                        if(!sock.enabled||!sock.gameObject.activeInHierarchy)throw new Exception("Stocking disappeared: "+character);
                        float action=time-1;
                        if(action>=HomecomingShoeGrip.Detach(side)&&action<HomecomingShoeGrip.Release(side))
                        {
                            var hand=rig.GetComponentsInChildren<Transform>().Single(b=>b.name==(side==0?"J_Bip_L_Hand":"J_Bip_R_Hand"));
                            maximumReach=Mathf.Max(maximumReach,Vector3.Distance(hand.position,motion.Shoes.WristTarget(side,action)));
                            var shoe=story.transform.Find("脱下的通勤鞋").GetChild(side);
                            if(motion.Shoes.ContactError(shoe,side)>.002f)throw new Exception("Held shoe lost contact");
                            var properties=new MaterialPropertyBlock();sock.GetPropertyBlock(properties);
                            float expected=action<motion.Shoes.Cleared(side)?1:0;
                            if(properties.GetFloat("_BootMask")!=expected)throw new Exception("Incorrect boot extraction coverage");
                        }
                    }
                    bool stage=new[]{0,90,150,210,270,300,330,360,390,405,420,450,510,570,630,780,840,960}.Contains(frame);
                    bool movie=(character==0||character==4||character==6)&&frame%5==0;
                    if(stage||movie)
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
                        report.AppendLine(HomecomingCharacters.Names[character]+","+maximumAngle+","+maximumReach+","+worstAngle);
                        File.WriteAllText("Artifacts/ContinuousReview/results.csv",report.ToString());
                        Debug.Log("CONTINUOUS PLAY REVIEW: "+HomecomingCharacters.Names[character]+"; 961 frames; max bone angle="+maximumAngle+" "+worstAngle+"; max wrist error="+maximumReach);
                        if(maximumReach>.04f)throw new Exception("Hand cannot reach shoe: "+character);
                        if(maximumAngle>30)throw new Exception("Abrupt skeletal turn: "+character+" "+worstAngle+" angle="+maximumAngle);
                        story.Replay();if(rig.transform.position.z<3.8f||!rig.leftShoe.activeSelf)throw new Exception("Replay state incorrect");
                        if(++character>=HomecomingCharacters.Names.Length)
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
    }
}
