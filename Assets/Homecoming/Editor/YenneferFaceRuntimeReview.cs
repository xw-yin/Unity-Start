using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homecoming.Editor
{
    // Separate batch editor only. Every PNG is an actual Play Mode URP render,
    // with fixed 1/30 s source-animation steps; wall-clock capture is unrestricted.
    [InitializeOnLoad] public static class YenneferFaceRuntimeReview
    {
        const string Key="Homecoming.OriginalFaceReview";
        static HomecomingScene story;static CharacterRig rig;static HomecomingMotion motion;
        static Camera camera;static RenderTexture target;static bool pending;static int frame;
        static double deadline;static float maximumOffset,maximumTurn;
        static Vector3 hipPosition,headPosition;static Quaternion hipTurn,headTurn;
        static readonly StringBuilder report=new StringBuilder("frame,time,blinkLeft,blinkRight,maxLocalOffset,maxLocalTurn\n");
        static YenneferFaceRuntimeReview()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{if(EditorApplication.isPlaying&&SessionState.GetBool(Key,false))State(PlayModeStateChange.EnteredPlayMode);};
        }
        public static void Run()
        {
            if(!Application.isBatchMode)throw new InvalidOperationException("Use a separate batchmode project copy for face review");
            YenneferFaceImport.Run();HomecomingSetup.CreateScene();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false)||story)return;
            try
            {
                story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(0);story.ReloadCharacter();
                rig=story.GetComponentInChildren<CharacterRig>();motion=YenneferFaceReview.Motion(story);motion.Sample(motion.ElapsedForAction(7.01f));
                if(motion.Face==null)throw new InvalidOperationException("No original face player");
                story.CameraControls.enabled=false;camera=story.CameraControls.GetComponent<Camera>();YenneferFaceReview.Aim(camera,rig);
                target=new RenderTexture(576,640,24){antiAliasing=4};camera.targetTexture=target;
                hipPosition=rig.hips.localPosition;hipTurn=rig.hips.localRotation;headPosition=rig.head.localPosition;headTurn=rig.head.localRotation;
                Directory.CreateDirectory(YenneferFaceReview.Folder+"/film");deadline=EditorApplication.timeSinceStartup+240;
                EditorApplication.update+=Step;RenderPipelineManager.endCameraRendering+=Rendered;
            }
            catch(Exception e){Fail(e);}
        }
        static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Face GPU capture timeout");
                if(pending)return;float time=frame/30f;float mood=.35f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,25,time));
                motion.Face.Sample(time,mood);
                if(Vector3.Distance(hipPosition,rig.hips.localPosition)>1e-6f||Quaternion.Angle(hipTurn,rig.hips.localRotation)>.001f||Vector3.Distance(headPosition,rig.head.localPosition)>1e-6f||Quaternion.Angle(headTurn,rig.head.localRotation)>.001f)throw new Exception("Face animation changed the body or head pose");
                maximumOffset=Mathf.Max(maximumOffset,motion.Face.MaximumOffset);maximumTurn=Mathf.Max(maximumTurn,motion.Face.MaximumTurn);
                if(float.IsNaN(maximumOffset)||maximumOffset>.05f||maximumTurn>65)throw new Exception("Face transform diverged; inspect source mapping");
                report.AppendLine(FormattableString.Invariant($"{frame},{time:R},{motion.Face.Weight("eyelids_upper_left_down"):R},{motion.Face.Weight("eyelids_upper_right_down"):R},{motion.Face.MaximumOffset:R},{motion.Face.MaximumTurn:R}"));
                pending=true;
            }
            catch(Exception e){Fail(e);}
        }
        static void Rendered(ScriptableRenderContext context,Camera rendered)
        {
            if(!pending||rendered!=camera)return;
            try
            {
                var previous=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(576,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,576,640),0,0);image.Apply();
                File.WriteAllBytes(YenneferFaceReview.Folder+"/film/"+frame.ToString("D4")+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);RenderTexture.active=previous;
                frame++;pending=false;if(frame<1200)return;
                // Re-seeking through arbitrary times must not accumulate facial transforms.
                motion.Face.Sample(3.25f,.12f);var transforms=rig.GetComponentsInChildren<Transform>(true).Where(t=>rig.faceLibrary.bones.Contains(t.name)).ToArray();
                var positions=transforms.Select(t=>t.localPosition).ToArray();var turns=transforms.Select(t=>t.localRotation).ToArray();
                foreach(float t in new[]{8f,19.82f,0f,39.2f})motion.Face.Sample(t,.35f);
                motion.Face.Sample(3.25f,.12f);
                for(int i=0;i<transforms.Length;i++)if(Vector3.Distance(positions[i],transforms[i].localPosition)>1e-7f||Quaternion.Angle(turns[i],transforms[i].localRotation)>.01f)throw new Exception("Face replay drift");
                File.WriteAllText(YenneferFaceReview.Folder+"/runtime.csv",report.ToString());
                File.WriteAllText(YenneferFaceReview.Folder+"/runtime-result.txt",$"PASS: 1200 contiguous actual Play Mode URP frames at 30 Hz source steps; 40 seconds, fixed camera/body. Device={SystemInfo.graphicsDeviceType}; maximum local facial offset={maximumOffset:R} m, turn={maximumTurn:R} degrees; replay deterministic; body/head unchanged. Geometric pose and artistic performance are reviewed separately.");
                Debug.Log("ORIGINAL FACE RUNTIME PASSED: 1200 frames, maximum offset="+maximumOffset+", angle="+maximumTurn);Finish(0);
            }
            catch(Exception e){Fail(e);}
        }
        static void Fail(Exception e){Debug.LogException(e);Finish(1);}
        static void Finish(int code){SessionState.SetBool(Key,false);EditorApplication.update-=Step;RenderPipelineManager.endCameraRendering-=Rendered;if(camera)camera.targetTexture=null;EditorApplication.Exit(code);}
    }
}
