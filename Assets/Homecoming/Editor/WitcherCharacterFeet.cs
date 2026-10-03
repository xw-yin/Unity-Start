using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Original Witcher female lower body. No procedurally modelled or VRoid feet.
    public static class WitcherCharacterFeet
    {
        const string Source="Assets/ThirdParty/WitcherGameFemaleLegs/Prepared/";
        [Serializable] class Manifest { public Node[] nodes; public Part[] parts; }
        [Serializable] class Node { public string name; }
        [Serializable] class Part { public string name,file; public int[] joints; }
        public static void Build(CharacterRig rig,string folder)
        {
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            var part=data.parts.Single();
            var source=WitcherCharacterImport.ReadPreparedMesh(Source,part.file,part.name);
            var boot=rig.leftShoe.GetComponent<SkinnedMeshRenderer>();
            var names=part.joints.Select(i=>data.nodes[i].name).ToArray();
            var map=names.Select(n=>Array.FindIndex(boot.bones,b=>b.name==n)).ToArray();
            var vertices=source.vertices;var weights=source.boneWeights;
            var originalNormals=source.normals;var originalUV=source.uv;
            var mapped=new BoneWeight[weights.Length];var positions=new Vector3[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                Vector3 offset=Vector3.zero;
                for(int j=0;j<4;j++)if(amounts[j]>0)
                {
                    if(map[ids[j]]<0)throw new Exception("Missing original leg bone: "+names[ids[j]]);
                    var a=source.bindposes[ids[j]].inverse.MultiplyPoint3x4(Vector3.zero);
                    var b=boot.sharedMesh.bindposes[map[ids[j]]].inverse.MultiplyPoint3x4(Vector3.zero);
                    offset+=(b-a)*amounts[j];
                }
                positions[i]=vertices[i]+offset;
                mapped[i]=new BoneWeight{boneIndex0=Mathf.Max(0,map[w.boneIndex0]),boneIndex1=Mathf.Max(0,map[w.boneIndex1]),boneIndex2=Mathf.Max(0,map[w.boneIndex2]),boneIndex3=Mathf.Max(0,map[w.boneIndex3]),weight0=w.weight0,weight1=w.weight1,weight2=w.weight2,weight3=w.weight3};
            }
            var material=AssetDatabase.LoadAssetAtPath<Material>(folder+"Materials/InnerLegs.mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,folder+"Materials/InnerLegs.mat");}
            material.shader=Shader.Find("Universal Render Pipeline/Lit");
            material.color=new Color(.065f,.052f,.045f);material.SetFloat("_Smoothness",.22f);
            // Keep the game UVs and surface detail under the retained hosiery.
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Texture_00.png"));
            material.SetFloat("_Cull",2);EditorUtility.SetDirty(material);
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                bool Own(int index)
                {
                    var w=weights[index];float own=0,other=0;
                    var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int j=0;j<4;j++){if(names[ids[j]].StartsWith(prefix))own+=amounts[j];else if(names[ids[j]].StartsWith("J_Bip_"))other+=amounts[j];}
                    return own>other;
                }
                var used=new List<int>();var triangles=new List<int>();var lookup=new Dictionary<int,int>();
                var sourceTriangles=source.triangles;
                for(int i=0;i<sourceTriangles.Length;i+=3)
                {
                    // Select whole source triangles under the garment overlap.
                    var face=new[]{sourceTriangles[i],sourceTriangles[i+1],sourceTriangles[i+2]};
                    if(face.Any(v=>!Own(v)||positions[v].y>.68f))continue;
                    foreach(int v in face){if(!lookup.ContainsKey(v)){lookup[v]=used.Count;used.Add(v);}triangles.Add(lookup[v]);}
                }
                if(triangles.Count<300)throw new Exception("Original game leg geometry is missing");
                var mesh=new Mesh{name=side==0?"OriginalGameLeftLeg":"OriginalGameRightLeg",vertices=used.Select(i=>positions[i]).ToArray(),normals=used.Select(i=>originalNormals[i]).ToArray(),uv=used.Select(i=>originalUV[i]).ToArray(),boneWeights=used.Select(i=>mapped[i]).ToArray(),bindposes=boot.sharedMesh.bindposes,triangles=triangles.ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                string path=folder+"Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var obj=new GameObject(side==0?"左腿连续丝袜":"右腿连续丝袜");obj.transform.SetParent(boot.transform.parent,false);
                var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=boot.bones;skin.rootBone=rig.hips;skin.sharedMaterial=material;skin.updateWhenOffscreen=true;skin.localBounds=boot.localBounds;
            }
            UnityEngine.Object.DestroyImmediate(source);
            Debug.Log("ORIGINAL GAME LEGS: ls_02_wa__body_barefoot; source triangles, weights and UVs retained; stockings remain attached.");
        }
    }
}
