using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Homecoming.Editor
{
    // Continuous actual gameplay evaluation; no interpolation between screenshots.
    [InitializeOnLoad]
    public static class CharacterFitCaptureReview
    {
        const string Key="Homecoming.CharacterFitCapture";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static HomecomingScene story;
        static CharacterRig rig;
        static int character,frame;
        static string folder;
        static bool Walking=>SessionState.GetString(Key+".Stage","").StartsWith("walk-");
        static int LastFrame=>Walking?240:SessionState.GetString(Key+".Stage","")=="arm-probe"?600:SessionState.GetString(Key+".Stage","").EndsWith("probe")?450:960;
        static int FirstSavedFrame=>LastFrame==600?540:LastFrame==450?300:0;
        static readonly MethodInfo update=typeof(HomecomingScene).GetMethod("Update",Flags);
        static readonly FieldInfo elapsed=typeof(HomecomingScene).GetField("elapsed",Flags);
        static CharacterFitCaptureReview(){EditorApplication.playModeStateChanged+=State;}
        public static void Baseline()=>Run("baseline");
        public static void Candidate()=>Run("candidate");
        public static void Probe()=>Run("probe");
        public static void ArmProbe()=>Run("arm-probe");
        public static void WalkBaseline()=>Run("walk-baseline");
        public static void WalkCandidate()=>Run("walk-candidate");
        public static void SeatBootBaseline()=>Run("seat-boot-baseline");
        public static void SeatBootCandidate()=>Run("seat-boot-candidate");
        public static void SeatBootProbe()=>Run("seat-boot-probe");
        static Vector3 hipUp;
        static void Run(string stage)
        {
            if(stage.StartsWith("seat-boot-"))
            {
                SessionState.SetBool("Homecoming.BootExtractionReview",true);
                SessionState.SetString("Homecoming.BootExtractionReview.Output","Artifacts/SeatBootReview/"+stage);
            }
            SessionState.SetString(Key+".Stage",stage);SessionState.SetBool(Key,true);
            SessionState.SetBool(Key+".Reload",EditorSettings.enterPlayModeOptionsEnabled);
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){character=0;Select();EditorApplication.update+=Tick;}
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+".Reload",false);
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }
        static void Select()
        {
            story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            story.SelectCharacter(character);story.enabled=false;
            typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
            rig=story.GetComponentInChildren<CharacterRig>();frame=0;
            rig.Neutral();hipUp=rig.hips.InverseTransformDirection(rig.transform.up);
            folder="Artifacts/CharacterFitCapture/"+SessionState.GetString(Key+".Stage","baseline")+"/"+character;
            Directory.CreateDirectory(folder);
            if(Walking)File.WriteAllText(folder+"/walking.csv","frame,time,hipHeight,headHeight,hipRoll,hipPitch,rootYaw\n");
            File.WriteAllText(folder+"/capture.json","{\"fps\":30,\"firstFrame\":"+FirstSavedFrame+",\"lastFrame\":"+LastFrame+",\"kind\":\"continuous_actual_gameplay\",\"width\":640,\"height\":640,\"clock\":\"offline fixed step\"}");
        }
        static void Tick()
        {
            try
            {
                for(int batch=0;batch<4;batch++)
                {
                    float time=frame/30f;elapsed.SetValue(story,time-Time.deltaTime);update.Invoke(story,null);
                    if(SessionState.GetString(Key+".Stage","").StartsWith("seat-boot-")||SessionState.GetString(Key+".Stage","")=="probe")
                        BootExtractionReview.Capture(story,rig,(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story),character,frame);
                    var camera=story.CameraControls.GetComponent<Camera>();
                    var aim=rig.transform.position+new Vector3(0,.84f,0);
                    camera.transform.position=aim+new Vector3(.36f,.04f,-2.85f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                    if(frame>=FirstSavedFrame)Capture(camera,folder+"/front/"+frame.ToString("D4")+".png");
                    if(frame<=240&&frame>=FirstSavedFrame)
                    {
                        // An actor-relative side view exposes foot clearance and
                        // shoulder/hip phase without the bench hiding the legs.
                        camera.transform.position=aim+rig.transform.right*2.8f-rig.transform.forward*.25f;
                        camera.transform.LookAt(aim);
                        Capture(camera,folder+"/walk-side/"+frame.ToString("D4")+".png");
                    }
                    if(Walking)
                    {
                        var up=rig.transform.InverseTransformDirection(rig.hips.TransformDirection(hipUp));
                        File.AppendAllText(folder+"/walking.csv",string.Join(",",new[]{frame,time,rig.hips.position.y-rig.transform.position.y,rig.head.position.y-rig.transform.position.y,Mathf.Atan2(-up.x,up.y)*Mathf.Rad2Deg,Mathf.Atan2(up.z,up.y)*Mathf.Rad2Deg,rig.transform.eulerAngles.y}.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))+"\n");
                    }
                    var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                    float action=motion.ActionTime(time);
                    int shot=Mathf.RoundToInt(motion.StoryTime(time)*30);
                    if(LastFrame!=600&&((action>=HomecomingShoeGrip.Start(0)&&action<=HomecomingShoeGrip.Release(0))||(action>=HomecomingShoeGrip.Start(1)&&action<=HomecomingShoeGrip.Release(1))))
                    {
                        aim=(rig.leftCalf.position+rig.transform.position+Vector3.up*.35f)*.5f;
                        camera.transform.position=aim+new Vector3(.85f,.05f,-1.15f);camera.transform.LookAt(aim);camera.fieldOfView=43;
                        Capture(camera,folder+"/contact/"+frame.ToString("D4")+".png");
                    }
                    if(frame>=FirstSavedFrame&&new[]{360,389,390,396,405,414,420,432,435,570,580,581,582,594,600,612,720,750,810,840,960}.Contains(shot))
                    {
                        CaptureDiagnostics(camera,shot,time);
                    }
                    if(++frame>LastFrame)
                    {
                        Debug.Log("CHARACTER FIT CONTINUOUS CAPTURE COMPLETE: "+character+" evaluated "+frame+" frames; saved "+(LastFrame-FirstSavedFrame+1)+" front frames");
                        if(character==0){character=2;Select();return;}
                        EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();return;
                    }
                }
            }
            catch(Exception error){Debug.LogException(error);EditorApplication.update-=Tick;SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        static void CaptureDiagnostics(Camera camera,int number,float time)
        {
            string path=folder+"/diagnostic/"+number;Directory.CreateDirectory(path);
            var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
            var proxies=story.transform.Find("脱下的通勤鞋");
            var report=new ContactReport{frame=number,elapsed=time,action=motion.ActionTime(time),anatomical=motion.Shoes.AnatomicalExtraction,sides=new ContactSide[2]};
            for(int side=0;side<2;side++)
            {
                var ankle=rig.GetComponentsInChildren<Transform>().Single(t=>t.name==(side==0?"J_Bip_L_Foot":"J_Bip_R_Foot"));
                var proxy=proxies.GetChild(side);float action=motion.ActionTime(time);
                report.sides[side]=new ContactSide{ankle=ankle.position,knee=(side==0?rig.leftCalf:rig.rightCalf).position,proxy=proxy.position,proxyRotation=proxy.rotation,desiredWrist=motion.Shoes.WristTarget(side,action),wrist=motion.Hands.Hand(side).position,cuff=motion.Shoes.HeelOnFoot(side),contactError=motion.Shoes.ContactError(proxy,side),pinchGap=motion.Hands.PinchGap(side)};
            }
            File.WriteAllText(path+"/contacts.json",JsonUtility.ToJson(report,true));
            var surfaces=rig.GetComponentsInChildren<SkinnedMeshRenderer>();
            var blocks=surfaces.Select(s=>{var b=new MaterialPropertyBlock();s.GetPropertyBlock(b);return b;}).ToArray();
            for(int i=0;i<surfaces.Length;i++)
            {
                var b=new MaterialPropertyBlock();surfaces[i].GetPropertyBlock(b);
                b.SetFloat("_BootMask",0);b.SetFloat("_BootRestMask",0);surfaces[i].SetPropertyBlock(b);
            }
            var aim=rig.transform.position+new Vector3(0,.60f,0);
            camera.transform.position=aim+new Vector3(.95f,.04f,-1.65f);camera.transform.LookAt(aim);camera.fieldOfView=42;
            Capture(camera,path+"/unmasked.png");
            for(int i=0;i<surfaces.Length;i++)surfaces[i].SetPropertyBlock(blocks[i]);
            var hand=rig.GetComponentsInChildren<Transform>().Single(b=>b.name=="J_Bip_L_Hand");
            aim=hand.position;camera.transform.position=aim+new Vector3(.50f,.19f,-.55f);camera.transform.LookAt(aim);camera.fieldOfView=47;
            Capture(camera,path+"/hand.png");
            var handSkins=surfaces.Where(s=>s.sharedMesh.boneWeights.Any(w=>
                w.weight0>.1f&&s.bones[w.boneIndex0].name.Contains("Hand")||
                w.weight1>.1f&&s.bones[w.boneIndex1].name.Contains("Hand"))).ToArray();
            var materials=handSkins.Select(s=>s.sharedMaterials).ToArray();
            var diagnostic=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.25f,.75f,.85f)};
            var shoeRenderer=proxies.GetChild(0).GetComponent<MeshRenderer>();var shoeMaterials=shoeRenderer.sharedMaterials;
            var shoeDiagnostic=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.85f,.40f,.13f)};
            shoeRenderer.sharedMaterials=shoeMaterials.Select(_=>shoeDiagnostic).ToArray();
            for(int i=0;i<handSkins.Length;i++)handSkins[i].sharedMaterials=materials[i].Select(_=>diagnostic).ToArray();
            Capture(camera,path+"/hand-solid.png");
            for(int i=0;i<handSkins.Length;i++)handSkins[i].sharedMaterials=materials[i];
            shoeRenderer.sharedMaterials=shoeMaterials;UnityEngine.Object.DestroyImmediate(shoeDiagnostic);
            UnityEngine.Object.DestroyImmediate(diagnostic);
            if(number==405)
            {
                var mesh=new Mesh();foreach(var skin in surfaces.Where(s=>rig.hosiery.Contains(s)))
                {
                    skin.BakeMesh(mesh);var v=mesh.vertices;var faces=mesh.triangles;
                    File.WriteAllText(path+"/"+skin.sharedMesh.name+".json",JsonUtility.ToJson(new Surface{vertices=v.SelectMany(p=>{var q=skin.transform.TransformPoint(p);return new[]{q.x,q.y,q.z};}).ToArray(),triangles=faces}));
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        [Serializable] class Surface{public float[] vertices;public int[] triangles;}
        [Serializable] class ContactReport{public int frame;public float elapsed,action;public bool anatomical;public ContactSide[] sides;}
        [Serializable] class ContactSide{public Vector3 ankle,knee,proxy,desiredWrist,wrist,cuff;public Quaternion proxyRotation;public float contactError,pinchGap;}
        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));var target=new RenderTexture(640,640,24);
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;Texture2D image=null;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(640,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;if(image)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
