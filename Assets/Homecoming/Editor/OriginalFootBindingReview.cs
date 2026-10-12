using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class OriginalFootBindingReview
    {
        [Serializable] class Manifest{public Node[] nodes;public Part[] parts;}
        [Serializable] class Node{public string name;}
        [Serializable] class Part{public string file,name;public int[] joints;}
        public static void Run()
        {
            foreach(var item in new[]{new[]{"YenneferDLC","WitcherYenneferOriginalFeet"},new[]{"Triss","WitcherTrissOriginalFeet"}})
            {
                var instance=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Homecoming/"+item[0]));
                var rig=instance.GetComponent<CharacterRig>();rig.Neutral();
                var boot=rig.leftShoe.GetComponent<SkinnedMeshRenderer>();
                string folder="Assets/ThirdParty/"+item[1]+"/Prepared/";
                var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"manifest.json"));var part=data.parts.Single();
                var mesh=WitcherCharacterImport.ReadPreparedMesh(folder,part.file,part.name);
                var map=part.joints.Select(i=>Array.FindIndex(boot.bones,b=>b.name==data.nodes[i].name)).ToArray();
                var sourceBind=mesh.bindposes;var targetBind=boot.sharedMesh.bindposes;
                float maximum=0;int worst=-1;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    var p=Vector3.zero;for(int j=0;j<4;j++)if(amounts[j]>0)p+=(targetBind[map[ids[j]]].inverse*sourceBind[ids[j]]).MultiplyPoint3x4(vertices[i])*amounts[j];
                    float delta=Vector3.Distance(p,vertices[i]);if(delta>maximum){maximum=delta;worst=i;}
                }
                Debug.Log("SOURCE FOOT BINDING: "+item[0]+" max rest-space mismatch="+maximum+" vertex="+worst);
                foreach(int i in Enumerable.Range(0,part.joints.Length).Where(i=>data.nodes[part.joints[i]].name.Contains("Foot")||data.nodes[part.joints[i]].name.Contains("ToeBase")))
                {
                    var source=sourceBind[i].inverse;var target=targetBind[map[i]].inverse;
                    Debug.Log("SOURCE FOOT FRAME: "+data.nodes[part.joints[i]].name+" position delta="+Vector3.Distance(source.GetColumn(3),target.GetColumn(3))+" rotation delta="+Quaternion.Angle(source.rotation,target.rotation));
                }
                UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
