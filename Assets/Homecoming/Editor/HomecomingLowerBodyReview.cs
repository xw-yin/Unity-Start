using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingLowerBodyReview
    {
        public static void BuildAndReview(){YenneferDLCImport.Import();Run();}
        public static void FinalReview(){BuildAndReview();HomecomingBootReview.Run();HomecomingMotionReview.Run();Debug.Log("LOWER BODY FINAL REVIEW PASSED");}
        public static void Run()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();var rig=story.GetComponentInChildren<CharacterRig>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
            Directory.CreateDirectory("Artifacts/LowerBody");
            var camera=story.GetComponentInChildren<Camera>();
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(skin.name.EndsWith("脚短袜"))throw new Exception("Boot-derived fake foot is still present");
                if(skin.name.EndsWith("腿连续丝袜"))
                {
                    if(skin.sharedMesh.bounds.min.y<-.005f||skin.sharedMesh.bounds.max.y<.65f||skin.sharedMesh.vertexCount<600)throw new Exception("Incomplete original game foot/stocking");
                    foreach(var w in skin.sharedMesh.boneWeights)if(Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)>.001f)throw new Exception("Invalid lower-body skin weights");
                }
            }
            float worstReach=0;string worst="";
            var reachLog=new System.Text.StringBuilder("time,side,dx,dy,dz\n");
            for(int frame=0;frame<=220;frame++)
            {
                float time=10+frame*.05f,action=time-1;
                typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,time);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
                var motion=(HomecomingMotion)typeof(HomecomingScene).GetField("motion",flags).GetValue(story);
                foreach(var hand in rig.GetComponentsInChildren<Transform>())
                {
                    int side=hand.name=="J_Bip_L_Hand"?0:hand.name=="J_Bip_R_Hand"?1:-1;if(side<0||action<HomecomingShoeGrip.Detach(side)||action>HomecomingShoeGrip.Release(side))continue;
                    float error=Vector3.Distance(hand.position,motion.Shoes.WristTarget(side,action));if(error>worstReach){worstReach=error;worst=time+" side="+side+" wrist="+hand.position+" target="+motion.Shoes.WristTarget(side,action);}
                    var delta=hand.position-motion.Shoes.WristTarget(side,action);reachLog.AppendLine(time+","+side+","+delta.x+","+delta.y+","+delta.z);
                }
                if((action>=12&&action<=14.4f)||(action>=18&&action<=20.4f))
                {
                    int side=action<16?0:1;
                    var skin=rig.transform.GetComponentInChildren<SkinnedMeshRenderer>();
                    foreach(var s in rig.GetComponentsInChildren<SkinnedMeshRenderer>())if(s.name==(side==0?"左腿连续丝袜":"右腿连续丝袜"))skin=s;
                    var baked=new Mesh();skin.BakeMesh(baked);Write("Artifacts/LowerBody/"+frame+"-leg.bin",baked,skin.transform);UnityEngine.Object.DestroyImmediate(baked);
                    using(var writer=new BinaryWriter(File.Create("Artifacts/LowerBody/"+frame+"-rest.bin")))foreach(var v in skin.sharedMesh.vertices)writer.Write(v.y);
                    Write("Artifacts/LowerBody/"+frame+"-restmesh.bin",skin.sharedMesh,skin.transform);
                    var boot=story.transform.Find("脱下的通勤鞋").GetChild(side).GetComponent<MeshFilter>();Write("Artifacts/LowerBody/"+frame+"-boot.bin",boot.sharedMesh,boot.transform);
                }
                if(frame%10==0)
                {
                    camera.transform.position=new Vector3(-.40f,.82f,-.20f);camera.transform.LookAt(new Vector3(-1.25f,.48f,1.20f));camera.fieldOfView=40;
                    HomecomingSetup.Capture(camera,"Artifacts/LowerBody/frame-"+frame+".png",1200,900);
                    if(frame==100)
                    {
                        var skins=rig.GetComponentsInChildren<SkinnedMeshRenderer>();var enabled=Array.ConvertAll(skins,s=>s.enabled);
                        for(int i=0;i<skins.Length;i++)skins[i].enabled=skins[i].name=="左腿连续丝袜";
                        HomecomingSetup.Capture(camera,"Artifacts/LowerBody/isolated-leg.png",1200,900);
                        for(int i=0;i<skins.Length;i++)skins[i].enabled=enabled[i];
                    }
                }
            }
            Debug.Log("LOWER BODY REVIEW: exported 50 ms leg/boot surfaces and close-up frames. Reach="+worstReach+" "+worst);
            if(worstReach>.025f)throw new Exception("Boot cuff is beyond the hand's reachable range: "+worstReach+" "+worst);
            File.WriteAllText("Artifacts/LowerBody/reach.csv",reachLog.ToString());
            typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,28f);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
            camera.transform.position=new Vector3(-.45f,.45f,-.10f);camera.transform.LookAt(new Vector3(-1.25f,.22f,1.10f));camera.fieldOfView=35;
            HomecomingSetup.Capture(camera,"Artifacts/LowerBody/feet-final.png",1200,900);
            var finalSkins=rig.GetComponentsInChildren<SkinnedMeshRenderer>();var finalEnabled=Array.ConvertAll(finalSkins,s=>s.enabled);
            foreach(string name in new[]{"左腿连续丝袜","右腿连续丝袜","original"})
            {
                for(int i=0;i<finalSkins.Length;i++)finalSkins[i].enabled=name=="original"?!finalSkins[i].name.EndsWith("腿连续丝袜")&&finalEnabled[i]:finalSkins[i].name==name;
                HomecomingSetup.Capture(camera,"Artifacts/LowerBody/feet-"+(name=="左腿连续丝袜"?"left":name=="右腿连续丝袜"?"right":"original")+".png",1200,900);
            }
            for(int i=0;i<finalSkins.Length;i++)finalSkins[i].enabled=finalEnabled[i];
        }
        static void Write(string path,Mesh mesh,Transform transform)
        {
            using(var w=new BinaryWriter(File.Create(path)))
            {
                var vertices=mesh.vertices;var triangles=mesh.triangles;w.Write(vertices.Length);w.Write(triangles.Length);
                foreach(var v in vertices){var p=transform.TransformPoint(v);w.Write(p.x);w.Write(p.y);w.Write(p.z);}
                foreach(var t in triangles)w.Write(t);
            }
        }
    }
}
