using System;
using System.IO;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingClothReview
    {
        public static void Run()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            story.ReloadCharacter();
            var rig=story.GetComponentInChildren<CharacterRig>();
            var cloth=rig.GetComponent<HomecomingClothDynamics>();
            var source=cloth&&cloth.SourceMesh?cloth.SourceMesh:rig.skirt.sharedMesh;
            var original = source.vertices;
            var motion = new HomecomingMotion(rig);
            Directory.CreateDirectory("Artifacts/Cloth");
            var camera = UnityEngine.Object.FindAnyObjectByType<HomecomingScene>().GetComponentInChildren<Camera>();
            float maximum = 0, moving = 0,seatCorrection=0;
            for(int frame = 0; frame <= 1620; frame++)
            {
                float time = frame / 60f;
                motion.Sample(time);
                var dynamic = rig.GetComponent<HomecomingClothDynamics>();
                maximum = Mathf.Max(maximum, dynamic.MaximumDisplacement);
                seatCorrection=Mathf.Max(seatCorrection,dynamic.MaximumSeatCorrection);
                if(time > 2 && time < 7) moving = Mathf.Max(moving, dynamic.MaximumDisplacement);
                var vertices = rig.skirt.sharedMesh.vertices;
                for(int i = 0; i < vertices.Length; i++)
                {
                    if(float.IsNaN(vertices[i].sqrMagnitude) || float.IsInfinity(vertices[i].sqrMagnitude)) throw new Exception("Invalid cloth vertex");
                    if(original[i].y >= .93f && Vector3.Distance(original[i], vertices[i]) > .00001f) throw new Exception("Cloth waist moved");
                }
                if(frame == 270 || frame == 750 || frame == 1110)
                {
                    camera.transform.position = rig.transform.position + new Vector3(1.05f,1.15f,-2.3f);
                    camera.transform.LookAt(rig.transform.position + new Vector3(0,.72f,0));
                    HomecomingSetup.Capture(camera,"Artifacts/Cloth/frame-" + frame + ".png");
                }
            }
            if(moving < .008f || maximum > .065f) throw new Exception("Cloth movement is missing or unbounded: " + maximum);
            var beforePause = rig.skirt.sharedMesh.vertices;
            motion.Sample(27);
            var afterPause = rig.skirt.sharedMesh.vertices;
            for(int i=0;i<beforePause.Length;i++) if(beforePause[i] != afterPause[i]) throw new Exception("Cloth drifts during pause");
            motion.Sample(0);
            var replay = rig.skirt.sharedMesh.vertices;
            for(int i=0;i<original.Length;i++) if(Vector3.Distance(original[i],replay[i])>.00001f) throw new Exception("Cloth replay did not reset");
            var persisted = source.vertices;
            for(int i=0;i<original.Length;i++) if(original[i] != persisted[i]) throw new Exception("Source mesh was modified");
            for(int frame=1;frame<=540;frame++)
            {
                motion.Sample(frame / 20f);
                if(rig.GetComponent<HomecomingClothDynamics>().MaximumDisplacement > .065f) throw new Exception("Cloth is unstable at 20 FPS");
            }
            Debug.Log("CLOTH REVIEW PASSED: 1621 frames; pinned waist, finite bounded vertices, pause/replay, immutable source; maximum=" + maximum + " walking=" + moving+" static seat correction="+seatCorrection);
            HomecomingMotionReview.Run();
        }
    }
}
