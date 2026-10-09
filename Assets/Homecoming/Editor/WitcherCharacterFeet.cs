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
            bool yennefer=folder.Contains("WitcherYenneferDLC/");
            bool triss=folder.Contains("WitcherTriss/");
            string sourceFolder=yennefer?"Assets/ThirdParty/WitcherYenneferOriginalFeet/Prepared/":triss?"Assets/ThirdParty/WitcherTrissOriginalFeet/Prepared/":Source;
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(sourceFolder+"manifest.json"));
            var part=data.parts.Single();
            var source=WitcherCharacterImport.ReadPreparedMesh(sourceFolder,part.file,part.name);
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
            material.shader=Shader.Find("Homecoming/Stocking Coverage");
            material.color=new Color(.018f,.018f,.018f);material.SetFloat("_Smoothness",.18f);
            // Hosiery is a separate opaque surface; never tint a skin texture black.
            material.SetTexture("_BaseMap",null);
            if(material.HasProperty("_BumpMap"))material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_DETAIL_NORMALMAP");
            if(folder.Contains("WitcherTriss/"))
            {
                // DLC Triss has bare legs, not the default character's hosiery.
                material.color=Color.white;
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(sourceFolder+"Texture_00.png"));
            }
            material.SetFloat("_Cull",2);EditorUtility.SetDirty(material);
            var skinMaterial=AssetDatabase.LoadAssetAtPath<Material>(folder+"Materials/OriginalLegSkin.mat");
            if(!skinMaterial){skinMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(skinMaterial,folder+"Materials/OriginalLegSkin.mat");}
            skinMaterial.color=Color.white;skinMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(sourceFolder+"Texture_00.png"));EditorUtility.SetDirty(skinMaterial);
            rig.anatomicalLegs=new SkinnedMeshRenderer[2];rig.hosiery=new SkinnedMeshRenderer[2];
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                bool Own(int index)
                {
                    var w=weights[index];float own=0,other=0;
                    var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    for(int j=0;j<4;j++)
                    {
                        string name=names[ids[j]];
                        if(name.StartsWith(prefix)||(triss&&name.StartsWith(side==0?"W3_l_":"W3_r_")))own+=amounts[j];
                        else if(name.StartsWith("J_Bip_")||(triss&&name.StartsWith(side==0?"W3_r_":"W3_l_")))other+=amounts[j];
                    }
                    return own>other;
                }
                var used=new List<int>();var triangles=new List<int>();var lookup=new Dictionary<int,int>();
                var sourceTriangles=source.triangles;
                for(int i=0;i<sourceTriangles.Length;i+=3)
                {
                    // Select whole source triangles under the garment overlap.
                    var face=new[]{sourceTriangles[i],sourceTriangles[i+1],sourceTriangles[i+2]};
                    float garmentOverlap=folder.Contains("WitcherIris/")?.90f:yennefer?.85f:.68f;
                    if(face.Any(v=>!Own(v))||(triss?face.Average(v=>positions[v].y)>=.68f:face.Any(v=>positions[v].y>garmentOverlap)))continue;
                    foreach(int v in face){if(!lookup.ContainsKey(v)){lookup[v]=used.Count;used.Add(v);}triangles.Add(lookup[v]);}
                }
                if(triangles.Count<300)throw new Exception("Original game leg geometry is missing");
                var mesh=new Mesh{name=side==0?"OriginalGameLeftLeg":"OriginalGameRightLeg",vertices=used.Select(i=>positions[i]).ToArray(),normals=used.Select(i=>originalNormals[i]).ToArray(),uv=used.Select(i=>originalUV[i]).ToArray(),boneWeights=used.Select(i=>mapped[i]).ToArray(),bindposes=boot.sharedMesh.bindposes,triangles=triangles.ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                if(triss)
                {
                    // A small temporary fit keeps the original foot inside the
                    // sandal sole and straps. Base geometry is restored as it exits.
                    int footIndex=Array.FindIndex(boot.bones,b=>b.name==prefix+"Foot"),toeIndex=Array.FindIndex(boot.bones,b=>b.name==prefix+"ToeBase");
                    var footOrigin=boot.sharedMesh.bindposes[footIndex].inverse.MultiplyPoint3x4(Vector3.zero);
                    var toeAxis=(boot.sharedMesh.bindposes[toeIndex].inverse.MultiplyPoint3x4(Vector3.zero)-footOrigin).normalized;
                    var delta=used.Select(i=>
                    {
                        float heelFit=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.035f,Vector3.Dot(positions[i]-footOrigin,toeAxis)));
                        return -originalNormals[i]*(.0035f+.0015f*heelFit)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.18f,positions[i].y)));
                    }).ToArray();
                    mesh.AddBlendShapeFrame("BootFit",100,delta,null,null);
                }
                string path=folder+"Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var leg=new GameObject(side==0?"左腿原始腿脚":"右腿原始腿脚");leg.transform.SetParent(boot.transform.parent,false);
                var skin=leg.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=boot.bones;skin.rootBone=rig.hips;skin.sharedMaterial=skinMaterial;skin.updateWhenOffscreen=true;skin.localBounds=boot.localBounds;
                // The opaque fitted sock replaces only the visible surface. Retain
                // the original anatomical mesh independently without coplanar rendering.
                skin.enabled=false;rig.anatomicalLegs[side]=skin;
                var sockMesh=UnityEngine.Object.Instantiate(mesh);sockMesh.name=side==0?"ContinuousLeftHosiery":"ContinuousRightHosiery";
                string sockPath=folder+"Meshes/"+sockMesh.name+".asset";var oldSock=AssetDatabase.LoadAssetAtPath<Mesh>(sockPath);
                if(oldSock){WitcherCharacterImport.CopyMesh(sockMesh,oldSock);UnityEngine.Object.DestroyImmediate(sockMesh);sockMesh=oldSock;}else AssetDatabase.CreateAsset(sockMesh,sockPath);
                var obj=new GameObject(side==0?"左腿连续丝袜":"右腿连续丝袜");obj.transform.SetParent(boot.transform.parent,false);
                var sock=obj.AddComponent<SkinnedMeshRenderer>();sock.sharedMesh=sockMesh;sock.bones=boot.bones;sock.rootBone=rig.hips;sock.sharedMaterial=material;sock.updateWhenOffscreen=true;sock.localBounds=boot.localBounds;rig.hosiery[side]=sock;
            }
            UnityEngine.Object.DestroyImmediate(source);
            if(yennefer)RemoveCoveredYenneferSurfaces(rig,folder);
            if(yennefer)YenneferSheerHosiery.Build(rig,folder);
            if(folder.Contains("WitcherShani/"))BuildOriginalShaniSocks(rig,folder,material);
            Debug.Log("ORIGINAL GAME LEGS: "+(yennefer?"yennefer/twals_01_wa__body_notcensor":triss?"triss/twals_01_wa__body_triss":"ls_02_wa__body_barefoot")+"; source triangles, weights and UVs retained; stockings remain attached.");
        }


        // Replace partial outfit leg panels to avoid overlapping skin strips.
        static void RemoveCoveredYenneferSurfaces(CharacterRig rig,string folder)
        {
            foreach(var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>().ToArray())
            {
                if(renderer.name=="body_05_wa__yennefer_lod0_6_Body")
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);continue;
                }
                if(renderer.name!="body_05_wa__yennefer_lod0_8_Body")continue;
                var source=renderer.sharedMesh;var vertices=source.vertices;
                var faces=source.triangles;var keep=new List<int>();
                for(int i=0;i<faces.Length;i+=3)
                    if(vertices[faces[i]].y>.82f&&vertices[faces[i+1]].y>.82f&&vertices[faces[i+2]].y>.82f)
                        keep.AddRange(new[]{faces[i],faces[i+1],faces[i+2]});
                var used=keep.Distinct().ToArray();var lookup=used.Select((v,i)=>new{v,i}).ToDictionary(x=>x.v,x=>x.i);
                var mesh=new Mesh{name="OriginalUpperBodySkin",vertices=used.Select(i=>vertices[i]).ToArray(),normals=used.Select(i=>source.normals[i]).ToArray(),uv=used.Select(i=>source.uv[i]).ToArray(),boneWeights=used.Select(i=>source.boneWeights[i]).ToArray(),bindposes=source.bindposes,triangles=keep.Select(i=>lookup[i]).ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                string path=folder+"Meshes/OriginalUpperBodySkin.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                renderer.sharedMesh=mesh;
            }
        }

        // The game's wedding/no-shoes variant includes actual closed sock feet.
        // Keep its UVs, normals, triangles and deformation weights, rather than
        // colouring the generic anatomical feet white.
        static void BuildOriginalShaniSocks(CharacterRig rig,string folder,Material material)
        {
            const string path="Assets/ThirdParty/WitcherShaniOriginalSocks/Prepared/";
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(path+"manifest.json"));
            var part=data.parts.Single();
            var source=WitcherCharacterImport.ReadPreparedMesh(path,part.file,part.name);
            var boot=rig.leftShoe.GetComponent<SkinnedMeshRenderer>();
            var names=part.joints.Select(i=>data.nodes[i].name).ToArray();
            var map=names.Select(n=>Array.FindIndex(boot.bones,b=>b.name==n)).ToArray();
            var weights=source.boneWeights;
            var mapped=new BoneWeight[weights.Length];
            for(int i=0;i<weights.Length;i++)
            {
                var w=weights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                for(int j=0;j<4;j++)if(amounts[j]>0&&map[ids[j]]<0)throw new Exception("Missing Shani sock bone: "+names[ids[j]]);
                mapped[i]=new BoneWeight{boneIndex0=Mathf.Max(0,map[w.boneIndex0]),boneIndex1=Mathf.Max(0,map[w.boneIndex1]),boneIndex2=Mathf.Max(0,map[w.boneIndex2]),boneIndex3=Mathf.Max(0,map[w.boneIndex3]),weight0=w.weight0,weight1=w.weight1,weight2=w.weight2,weight3=w.weight3};
            }
            foreach(string textureName in new[]{"Texture_00.png","Texture_01.png","OriginalSockWeave.png"})
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path+textureName);
                bool normal=textureName!="Texture_00.png";
                importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=!normal;importer.SaveAndReimport();
            }
            material.color=Color.white;material.SetFloat("_Smoothness",.10f);
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"Texture_00.png"));
            material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"Texture_01.png"));
            material.SetFloat("_BumpScale",.7f);material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_DetailNormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path+"OriginalSockWeave.png"));
            material.SetFloat("_DetailNormalMapScale",.30f);material.SetTextureScale("_DetailNormalMap",new Vector2(100,99));
            material.EnableKeyword("_DETAIL_NORMALMAP");EditorUtility.SetDirty(material);
            var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var faces=source.triangles;
            for(int side=0;side<2;side++)
            {
                // Knee ribbons are weighted to original W3 helper bones, not
                // only J_Bip leg bones. Include their complete source triangles.
                bool Own(int v)=>side==0?vertices[v].x<0:vertices[v].x>0;
                var used=new List<int>();var lookup=new Dictionary<int,int>();var triangles=new List<int>();
                for(int i=0;i<faces.Length;i+=3)
                {
                    var face=new[]{faces[i],faces[i+1],faces[i+2]};
                    // Retain whole original triangles beneath the existing trousers.
                    if(face.Any(v=>!Own(v)||vertices[v].y>.50f))continue;
                    foreach(int v in face){if(!lookup.ContainsKey(v)){lookup[v]=used.Count;used.Add(v);}triangles.Add(lookup[v]);}
                }
                if(triangles.Count<300)throw new Exception("Original Shani sock foot is missing");
                var mesh=new Mesh{name=side==0?"ContinuousLeftHosiery":"ContinuousRightHosiery",vertices=used.Select(v=>vertices[v]).ToArray(),normals=used.Select(v=>normals[v]).ToArray(),uv=used.Select(v=>uv[v]).ToArray(),boneWeights=used.Select(v=>mapped[v]).ToArray(),bindposes=boot.sharedMesh.bindposes,triangles=triangles.ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                var existing=rig.hosiery[side].sharedMesh;WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);
                rig.hosiery[side].sharedMaterial=material;
                rig.hosiery[side].name=side==0?"夏妮原版左白袜":"夏妮原版右白袜";
            }
            UnityEngine.Object.DestroyImmediate(source);
            Debug.Log("ORIGINAL SHANI SOCKS: l_03_wa__shani.w2mesh; closed sock feet, original UVs, normals, weights and textures retained.");
        }
    }
}
