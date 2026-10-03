using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingGarmentReview
    {
        public static void Run()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            foreach(int index in new[]{4,5,6})
            {
                story.SelectCharacter(index);var rig=story.GetComponentInChildren<CharacterRig>();
                var solver=rig.GetComponentInChildren<HomecomingGarmentDynamics>();
                if(!solver||solver.FreeParticles<20||!AssetDatabase.Contains(solver.SourceMesh))throw new Exception("Missing imported fabric mask: "+index);
                var source=solver.SourceMesh;var original=source.vertices;var skin=solver.GetComponent<SkinnedMeshRenderer>();
                var mask=new SerializedObject(solver).FindProperty("maximumDistance");
                var pinned=Enumerable.Range(0,mask.arraySize).Where(i=>mask.GetArrayElementAtIndex(i).floatValue<=.0001f).ToArray();
                if(pinned.Length<4)throw new Exception("Fabric has no sewn attachment points");
                var motion=new HomecomingMotion(rig);float moving=0,maximum=0;
                foreach(int fps in new[]{60,20})
                {
                    motion.Sample(0);
                    for(int frame=1;frame<=27*fps;frame++)
                    {
                        float time=frame/(float)fps;motion.Sample(time);
                        maximum=Mathf.Max(maximum,solver.MaximumDisplacement);
                        if(time>2&&time<7)moving=Mathf.Max(moving,solver.MaximumDisplacement);
                        var deformed=skin.sharedMesh.vertices;
                        foreach(var p in deformed)if(float.IsNaN(p.sqrMagnitude)||float.IsInfinity(p.sqrMagnitude))throw new Exception("Non-finite fabric vertex");
                        foreach(int i in pinned)if(Vector3.Distance(deformed[i],original[i])>.00001f)throw new Exception("A pinned fabric attachment moved");
                    }
                }
                if(moving<.004f||maximum>.111f)throw new Exception("Fabric is rigid or unstable: "+index+" walking="+moving+" maximum="+maximum);
                var paused=skin.sharedMesh.vertices;motion.Sample(27);
                if(!paused.SequenceEqual(skin.sharedMesh.vertices))throw new Exception("Fabric changed while paused");
                motion.Sample(0);
                if(!original.SequenceEqual(skin.sharedMesh.vertices)||!original.SequenceEqual(source.vertices))throw new Exception("Fabric replay or source preservation failed");
                Debug.Log("GARMENT REVIEW PASSED: "+HomecomingCharacters.Names[index]+"; painted free particles="+solver.FreeParticles+"; 60/20 FPS, pause, replay, immutable source; walking="+moving+" maximum="+maximum);
            }
        }
    }
}
