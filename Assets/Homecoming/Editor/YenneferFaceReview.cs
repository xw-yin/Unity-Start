using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferFaceReview
    {
        public const string Folder="Artifacts/FaceReview";
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        public static void ImportAndReview()
        {
            Directory.CreateDirectory(Folder);HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(0);story.ReloadCharacter();
            var rig=story.GetComponentInChildren<CharacterRig>();var motion=Motion(story);motion.Sample(motion.ElapsedForAction(7.01f));motion.ResetFace();
            var camera=story.CameraControls.GetComponent<Camera>();Aim(camera,rig);HomecomingSetup.Capture(camera,Folder+"/before-neutral.png",900,1000);
            YenneferFaceImport.Run();story.ReloadCharacter();rig=story.GetComponentInChildren<CharacterRig>();motion=Motion(story);motion.Sample(motion.ElapsedForAction(7.01f));motion.ResetFace();Aim(camera,rig);
            if(motion.Face==null)throw new InvalidOperationException("Native face player not installed");
            void Take(string name){HomecomingSetup.Capture(camera,Folder+"/"+name+".png",900,1000);}
            Take("source-neutral");
            foreach(float weight in new[]{.5f,1f})
            {
                motion.Face.PreviewPoses(("eyelids_upper_left_down",weight),("eyelids_upper_right_down",weight),("eyelids_lower_left_up",.125f*weight),("eyelids_lower_right_up",.125f*weight));
                Take("source-blink-"+weight.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
            }
            foreach(string dir in new[]{"left","right","up","down"})
            {
                motion.Face.PreviewPoses(("eye_left_"+dir,.20f),("eye_right_"+dir,.20f));Take("source-gaze-"+dir);
            }
            motion.Face.PreviewPoses(("corner_left_up",.12f),("corner_right_up",.12f),("cheek_left_up",.1f),("cheek_right_up",.1f));Take("source-soft-smile");
            motion.Face.PreviewPose("jaw_open_a",.25f);Take("source-jaw-025");
            foreach(float time in new[]{0f,3f,7f,12f,18f}){motion.Face.Sample(time,0);Take("native-neutral-"+time);motion.Face.Sample(time,.35f);Take("native-content-"+time);}
            motion.Face.Reset();
            Debug.Log("SOURCE FACE GALLERY CAPTURED: source neutral, eyelids, gaze, lips, jaw and actual animation curves; visual approval pending.");
        }
        public static HomecomingMotion Motion(HomecomingScene story)=>(HomecomingMotion)typeof(HomecomingScene).GetField("motion",Flags).GetValue(story);
        public static void Aim(Camera camera,CharacterRig rig)
        {
            var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.StartsWith("h_01_wa__yennefer_hq"));
            var mesh=new Mesh();skin.BakeMesh(mesh);var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();UnityEngine.Object.DestroyImmediate(mesh);
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            var aim=bounds.center+Vector3.up*bounds.size.y*.12f;
            camera.transform.position=aim+new Vector3(.08f,.015f,-.68f);camera.transform.LookAt(aim);camera.fieldOfView=38;
        }
    }
}
