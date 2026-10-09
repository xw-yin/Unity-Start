using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class StandingHandReview
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        internal struct Triangle { public Vector3 a,b,c; public Bounds bounds; public string surface; }
        public static void DuchessOnly(){CuteMotionReview.DuchessOnly();}
        public static void FullReview(){CuteMotionReview.FullReview();}
        [MenuItem("Homecoming/审阅站立手臂与衣服间距")]
        public static void Run(){CuteMotionReview.Run();}
        static void Run(int first)
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            Directory.CreateDirectory("Artifacts/StandingHands");
            var report=new StringBuilder("character,frame,side,elbowBend,wristBend,wristOutside,wristForward,fingerClothIntersections\n");
            var errors=new List<string>();
            for(int character=first;character<7;character++)
            {
                story.SelectCharacter(character);var rig=story.GetComponentInChildren<CharacterRig>();
                typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                for(int frame=0;frame<=810;frame++)
                {
                    typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,frame/30f-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                    if(!new[]{0,30,810}.Contains(frame))continue;
                    var bones=rig.GetComponentsInChildren<Transform>();
                    var body=BodyTriangles(rig);
                    for(int side=0;side<2;side++)
                    {
                        string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                        var upper=side==0?rig.leftArm:rig.rightArm;var elbow=side==0?rig.leftElbow:rig.rightElbow;
                        var wrist=bones.Single(b=>b.name==prefix+"Hand");var middle=bones.Single(b=>b.name==prefix+"Middle1");
                        float bend=Vector3.Angle(elbow.position-upper.position,wrist.position-elbow.position);
                        float flex=Vector3.Angle(wrist.position-elbow.position,middle.position-wrist.position);
                        var delta=rig.transform.InverseTransformVector(wrist.position-upper.position);
                        float outside=delta.x*(side==0?1:-1),forward=-delta.z;
                        int hits=0;var hitSurfaces=new HashSet<string>();
                        foreach(string finger in new[]{"Index","Middle","Ring","Little","Thumb"})
                        {
                            var chain=Enumerable.Range(1,3).Select(j=>bones.Single(b=>b.name==prefix+finger+j)).ToArray();
                            for(int j=0;j<2;j++)foreach(var triangle in body)if(Crosses(chain[j].position,chain[j+1].position,triangle)){hits++;hitSurfaces.Add(triangle.surface);}
                            var end=chain[2].position+(chain[2].position-chain[1].position)*.7f;
                            foreach(var triangle in body)if(Crosses(chain[2].position,end,triangle)){hits++;hitSurfaces.Add(triangle.surface);}
                        }
                        report.AppendLine(character+","+frame+","+side+","+bend+","+flex+","+outside+","+forward+","+hits);
                        if(bend<10||bend>28||flex>8||outside<.08f||forward<.055f||hits>0)
                            errors.Add("Standing hand "+character+"@"+frame+" side="+side+" elbow="+bend+" wrist="+flex+" outside="+outside+" forward="+forward+" cloth crossings="+hits+" surfaces="+string.Join(";",hitSurfaces));
                    }
                    var camera=story.CameraControls.GetComponent<Camera>();
                    var target=rig.transform.TransformPoint(new Vector3(0,1.05f,0));
                    foreach(var item in new[]{("front",new Vector3(0,.05f,-2.2f)),("side",new Vector3(-2.2f,.05f,0)),("back",new Vector3(0,.05f,2.2f))})
                    {
                        camera.transform.position=target+rig.transform.TransformDirection(item.Item2);camera.transform.LookAt(target);camera.fieldOfView=35;
                        HomecomingSetup.Capture(camera,"Artifacts/StandingHands/"+character+"-"+frame+"-"+item.Item1+".png",1000,1000);
                    }
                }
            }
            File.WriteAllText("Artifacts/StandingHands/metrics.csv",report.ToString());
            if(errors.Count>0)throw new Exception(string.Join("\n",errors));
            Debug.Log("STANDING HAND REVIEW PASSED: seven characters, start/end; relaxed elbows, aligned wrists and no finger segments through body clothing.");
        }
        static bool ArmBone(string name)=>name.Contains("Arm")||name.Contains("Hand")||name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little")||name.Contains("Thumb");
        internal static List<Triangle> BodyTriangles(CharacterRig rig)
        {
            var result=new List<Triangle>();var baked=new Mesh();
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                skin.BakeMesh(baked);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;var indices=baked.triangles;
                var arm=new bool[vertices.Length];
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];float influence=0;
                    var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int k=0;k<4;k++)if(values[k]>0&&ArmBone(skin.bones[ids[k]].name))influence+=values[k];
                    arm[i]=influence>.2f;
                }
                for(int i=0;i<indices.Length;i+=3)
                {
                    if(arm[indices[i]]||arm[indices[i+1]]||arm[indices[i+2]])continue;
                    var a=skin.transform.TransformPoint(vertices[indices[i]]);var b=skin.transform.TransformPoint(vertices[indices[i+1]]);var c=skin.transform.TransformPoint(vertices[indices[i+2]]);
                    var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);bounds.Expand(.0001f);
                    result.Add(new Triangle{a=a,b=b,c=c,bounds=bounds,surface=skin.name});
                }
            }
            UnityEngine.Object.DestroyImmediate(baked);return result;
        }
        internal static bool Crosses(Vector3 start,Vector3 end,Triangle tri)
        {
            var bounds=new Bounds(start,Vector3.zero);bounds.Encapsulate(end);if(!bounds.Intersects(tri.bounds))return false;
            var ray=end-start;var a=tri.b-tri.a;var b=tri.c-tri.a;var p=Vector3.Cross(ray,b);float determinant=Vector3.Dot(a,p);
            if(Mathf.Abs(determinant)<1e-10f)return false;
            float inverse=1/determinant;var t=start-tri.a;float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;
            var q=Vector3.Cross(t,a);float v=Vector3.Dot(ray,q)*inverse;if(v<0||u+v>1)return false;
            float distance=Vector3.Dot(b,q)*inverse;return distance>.001f&&distance<.999f;
        }
    }
}
