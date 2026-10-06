using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Adapt original game surfaces; no generated arm or stocking geometry.
    public static class ShaniModelRefinement
    {
        [Serializable] class Manifest { public Node[] nodes; public Part[] parts; }
        [Serializable] class Node { public string name; }
        [Serializable] class Part { public string file,name; public int[] joints; }

        public static void AttachCostumeBones(CharacterRig rig,Transform[] nodes)
        {
            foreach(var bone in nodes)
            {
                if(!Regex.IsMatch(bone.name,@"^W3_[lr]_\d{2}_\d{2}$")||bone.parent!=rig.head)continue;
                bool left=bone.name.StartsWith("W3_l_");
                float height=bone.position.y;
                var anchor=height<.65f?(left?rig.leftCalf:rig.rightCalf):height>1.15f?(left?rig.leftArm:rig.rightArm):rig.spine;
                bone.SetParent(anchor,true);
            }
        }

        public static void RemoveStockingTiesFromBoot(Mesh mesh)
        {
            // In b_01 Material2, the isolated red knee-tie islands are wholly
            // between 0.30 and 0.46 m. Sole trim and the white cuff lie outside.
            // The complete original l_03 stocking supplies these ties instead.
            var vertices=mesh.vertices;var faces=mesh.triangles;
            var adjacency=Enumerable.Range(0,vertices.Length).Select(_=>new List<int>()).ToArray();
            for(int i=0;i<faces.Length;i+=3)for(int j=0;j<3;j++)
            {adjacency[faces[i+j]].Add(faces[i+(j+1)%3]);adjacency[faces[i+j]].Add(faces[i+(j+2)%3]);}
            var seen=new bool[vertices.Length];var remove=new bool[vertices.Length];int removed=0;
            for(int seed=0;seed<vertices.Length;seed++)
            {
                if(seen[seed])continue;var members=new List<int>();var queue=new Stack<int>();queue.Push(seed);seen[seed]=true;
                while(queue.Count>0){int v=queue.Pop();members.Add(v);foreach(int n in adjacency[v])if(!seen[n]){seen[n]=true;queue.Push(n);}}
                if(members.Min(v=>vertices[v].y)>.30f&&members.Max(v=>vertices[v].y)<.46f)
                    foreach(int v in members){remove[v]=true;removed++;}
            }
            if(removed<300)throw new Exception("Original Shani knee-tie islands were not identified.");
            var kept=new List<int>();for(int i=0;i<faces.Length;i+=3)if(!remove[faces[i]]&&!remove[faces[i+1]]&&!remove[faces[i+2]]){kept.Add(faces[i]);kept.Add(faces[i+1]);kept.Add(faces[i+2]);}
            var used=kept.Distinct().ToArray();var map=used.Select((v,i)=>(v,i)).ToDictionary(x=>x.v,x=>x.i);
            var replacement=new Mesh{name=mesh.name,vertices=used.Select(v=>vertices[v]).ToArray(),normals=used.Select(v=>mesh.normals[v]).ToArray(),uv=used.Select(v=>mesh.uv[v]).ToArray(),boneWeights=used.Select(v=>mesh.boneWeights[v]).ToArray(),bindposes=mesh.bindposes,triangles=kept.Select(v=>map[v]).ToArray()};
            replacement.RecalculateBounds();WitcherCharacterImport.CopyMesh(replacement,mesh);UnityEngine.Object.DestroyImmediate(replacement);
        }

        public static void CompleteOriginalArms(SkinnedMeshRenderer skin)
        {
            const string folder="Assets/ThirdParty/WitcherShaniArmCoverage/Prepared/";
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(folder+"manifest.json"));var part=data.parts.Single();
            var source=WitcherCharacterImport.ReadPreparedMesh(folder,part.file,part.name);var target=skin.sharedMesh;
            var boneMap=part.joints.Select(i=>Array.FindIndex(skin.bones,b=>b.name==data.nodes[i].name)).ToArray();
            var sourceVertices=source.vertices;var sourceNormals=source.normals;var sourceUV=source.uv;var sourceWeights=source.boneWeights;
            var vertices=target.vertices.ToList();var normals=target.normals.ToList();var uv=target.uv.ToList();var weights=target.boneWeights.ToList();var triangles=target.triangles.ToList();
            var lookup=new Dictionary<int,int>();
            Vector3 FitUnderSleeve(Vector3 p)
            {
                string side=p.x<0?"L":"R";
                var upper=skin.bones.First(b=>b.name=="J_Bip_"+side+"_UpperArm");var elbow=skin.bones.First(b=>b.name=="J_Bip_"+side+"_LowerArm");
                var start=skin.transform.InverseTransformPoint(upper.position);var end=skin.transform.InverseTransformPoint(elbow.position);var axis=(end-start).normalized;
                float along=Vector3.Dot(p-start,axis);var center=start+axis*along;
                // The game hides this upper-arm surface under the shirt. Fit
                // only the newly restored covered skin inside that sleeve;
                // leave the original exposed forearm and its seam untouched.
                float radius=Mathf.Lerp(.40f,1,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,.94f,along/Vector3.Distance(start,end))));
                return center+(p-center)*radius;
            }
            int Map(int index)
            {
                if(lookup.TryGetValue(index,out int mapped))return mapped;
                // Both assets use the game's same woman-average arm topology.
                // Weld the matching overlap; preserve existing hand vertices.
                for(int j=0;j<vertices.Count;j++)if((vertices[j]-sourceVertices[index]).sqrMagnitude<.00003f*.00003f&&(uv[j]-sourceUV[index]).sqrMagnitude<.00001f)
                {lookup[index]=j;return j;}
                var w=sourceWeights[index];int Bone(int b,float weight){if(weight>0&&boneMap[b]<0)throw new Exception("Missing original arm bone");return Mathf.Max(0,boneMap[b]);}
                mapped=vertices.Count;lookup[index]=mapped;vertices.Add(FitUnderSleeve(sourceVertices[index]));normals.Add(sourceNormals[index]);uv.Add(sourceUV[index]);
                weights.Add(new BoneWeight{boneIndex0=Bone(w.boneIndex0,w.weight0),boneIndex1=Bone(w.boneIndex1,w.weight1),boneIndex2=Bone(w.boneIndex2,w.weight2),boneIndex3=Bone(w.boneIndex3,w.weight3),weight0=w.weight0,weight1=w.weight1,weight2=w.weight2,weight3=w.weight3});return mapped;
            }
            string Key(int a,int b,int c){var ids=new[]{a,b,c};Array.Sort(ids);return string.Join(",",ids);}
            var existing=new HashSet<string>();for(int i=0;i<triangles.Count;i+=3)existing.Add(Key(triangles[i],triangles[i+1],triangles[i+2]));
            int added=0;var faces=source.triangles;
            for(int i=0;i<faces.Length;i+=3)
            {
                var ids=new[]{faces[i],faces[i+1],faces[i+2]};
                // The cut is hidden inside the original sleeves at the shoulder.
                if(ids.Any(v=>Mathf.Abs(sourceVertices[v].x)<.17f))continue;
                int a=Map(ids[0]),b=Map(ids[1]),c=Map(ids[2]);if(!existing.Add(Key(a,b,c)))continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);added++;
            }
            if(added<100)throw new Exception("Original upper-arm coverage was not restored.");
            target.vertices=vertices.ToArray();target.normals=normals.ToArray();target.uv=uv.ToArray();target.boneWeights=weights.ToArray();target.triangles=triangles.ToArray();target.RecalculateBounds();target.RecalculateTangents();EditorUtility.SetDirty(target);
            UnityEngine.Object.DestroyImmediate(source);Debug.Log("SHANI ORIGINAL ARM COVERAGE: "+added+" source triangles; matching forearm seam welded.");
        }
    }
}
