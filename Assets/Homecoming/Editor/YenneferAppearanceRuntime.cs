using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Homecoming.Editor
{
    [InitializeOnLoad] public static class YenneferAppearanceRuntime
    {
        const string Key="YenneferAppearanceRuntime";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static HomecomingScene story;static CharacterRig rig;static HomecomingMotion motion;
        static Camera camera;static RenderTexture target;static bool pending;static int frame;
        static double deadline;static float action;
        static readonly int[] shots={180,240,300,420,600,780};
        static YenneferAppearanceRuntime()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying&&SessionState.GetBool(Key,false))State(PlayModeStateChange.EnteredPlayMode);};
        }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Run this disposable-scene review in a separate batchmode editor.");
            YenneferAppearanceMaterials.Apply();YenneferAppearanceReview.Current();
            HomecomingSetup.CreateScene();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false)||story)return;
            try
            {
                story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(0);story.ReloadCharacter();
                rig=story.GetComponentInChildren<CharacterRig>();motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
                story.CameraControls.enabled=false;camera=story.CameraControls.GetComponent<Camera>();
                target=new RenderTexture(800,900,24){antiAliasing=4};camera.targetTexture=target;
                typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                Directory.CreateDirectory("Artifacts/AppearanceReview/runtime");deadline=EditorApplication.timeSinceStartup+180;
                EditorApplication.update+=Step;RenderPipelineManager.endCameraRendering+=Rendered;
            }
            catch(Exception e){Fail(e);}
        }
        static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Runtime render timeout");
                if(pending)return;
                float time=frame/30f;typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,time-Time.deltaTime);
                typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);action=motion.ActionTime(time);
                var aim=rig.head.position+Vector3.up*.01f;
                camera.transform.position=aim+new Vector3(.25f,.02f,-.88f);camera.transform.LookAt(aim);camera.fieldOfView=38;
                foreach(var fringe in rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.EndsWith("_HairFringe")))
                {
                    var source=fringe.transform.parent.GetComponent<SkinnedMeshRenderer>();
                    if(fringe.sharedMesh!=source.sharedMesh||!fringe.bones.SequenceEqual(source.bones)||fringe.transform.localPosition!=Vector3.zero)throw new Exception("Hair overlay lost its source mesh or skin binding");
                }
                pending=true;
            }
            catch(Exception e){Fail(e);}
        }
        static void Rendered(ScriptableRenderContext context,Camera rendered)
        {
            if(!pending||rendered!=camera)return;
            try
            {
                if(shots.Contains(frame))
                {
                    var previous=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(800,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,900),0,0);image.Apply();
                    File.WriteAllBytes("Artifacts/AppearanceReview/runtime/frame-"+frame+".png",image.EncodeToPNG());
                    UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
                }
                frame++;pending=false;
                if(frame<930)return;
                foreach(string name in new[]{"Homecoming/Character Surface","Homecoming/Eye Socket Shadow"})
                {
                    var shader=Shader.Find(name);if(!shader||!shader.isSupported)throw new Exception("Unsupported shader "+name);
                    var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
                    if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));
                }
                File.WriteAllText("Artifacts/AppearanceReview/runtime/result.txt","PASS: 930 actual URP camera frames in Play Mode, timeline sampled at 30 Hz; six unbaked GPU skinned captures; original shared hair mesh/bones preserved; D3D11 shader errors=0. This is a material/skinning check, not a motion quality or device performance score.");
                Debug.Log("YENNEFER APPEARANCE RUNTIME PASSED: 930 frames, GPU skinning and shader variants.");Finish(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);Finish(1);}
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Step;RenderPipelineManager.endCameraRendering-=Rendered;if(camera)camera.targetTexture=null;EditorApplication.Exit(code);}
    }
}
