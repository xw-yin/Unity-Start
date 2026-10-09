using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferBootIntersections
    {
        [Serializable] public class Surface { public float[] vertices,boundary;public int[] triangles; }
        [Serializable] public class Side { public Surface boot,sock,lace;public float[] worldToBoot,minimum,maximum;public bool masked,restMasked; }
        [Serializable] public class Sample { public int frame;public Side[] sides; }
        static float[] Vector(Vector3 v)=>new[]{v.x,v.y,v.z};
        static Surface Read(Mesh mesh,Transform transform)=>new Surface{vertices=mesh.vertices.SelectMany(v=>Vector(transform.TransformPoint(v))).ToArray(),triangles=mesh.triangles,boundary=mesh.uv4.Select(v=>v.y-v.x).ToArray()};
        static Surface Read(SkinnedMeshRenderer skin)
        {
            var mesh=new Mesh();skin.BakeMesh(mesh);var result=Read(mesh,skin.transform);UnityEngine.Object.DestroyImmediate(mesh);return result;
        }
        public static void Capture(HomecomingScene story,CharacterRig rig,int frame)
        {
            var sample=new Sample{frame=frame,sides=new Side[2]};float action=frame/30f-1;
            for(int side=0;side<2;side++)
            {
                var skin=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                Surface boot;
                if(action>=HomecomingShoeGrip.Detach(side)){var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);boot=Read(proxy.GetComponent<MeshFilter>().sharedMesh,proxy);}
                else boot=Read(skin);
                bool dressed=rig.dressedLegs!=null&&rig.dressedLegs.Length==2&&action<HomecomingShoeGrip.Detach(side);
                var properties=new MaterialPropertyBlock();(dressed?rig.dressedLegs[side]:rig.hosiery[side]).GetPropertyBlock(properties);var matrix=properties.GetMatrix("_BootWorldToLocal");
                var lace=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="YenneferOriginalLace"+(side==0?"Left":"Right"));
                sample.sides[side]=new Side{boot=boot,sock=Read(dressed?rig.dressedLegs[side]:rig.hosiery[side]),lace=Read(dressed?rig.dressedHosiery[side]:lace),minimum=Vector(properties.GetVector("_BootBoundsMin")),maximum=Vector(properties.GetVector("_BootBoundsMax")),worldToBoot=Enumerable.Range(0,16).Select(i=>matrix[i/4,i%4]).ToArray(),masked=properties.GetFloat("_BootMask")>.5f,restMasked=properties.GetFloat("_BootRestMask")>.5f};
            }
            const string folder="Artifacts/YenneferLaceBoots/Geometry";Directory.CreateDirectory(folder);File.WriteAllText(folder+"/"+frame+".json",JsonUtility.ToJson(sample));
        }
    }
}
