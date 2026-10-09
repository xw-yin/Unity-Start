using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Explicit interchange keeps Unity asset identities and the original game rig.
    public static class BlenderModelBridge
    {
        [Serializable] public class Bone { public string name; public int parent; public float[] matrix; }
        [Serializable] public class SurfaceMaterial { public string name, texture,skinTexture,normal,specular; public float[] color,uvScale;public float sheer,denier,lace,skinUV2,stockingBoundary,density,grazing,laceBodyRepeat,bumpScale,workflow; }
        [Serializable] public class Shape { public string name; public float[] delta; }
        [Serializable] public class Surface
        {
            public string name, path, role; public bool enabled;
            public float[] vertices, normals, uv, uv2, uv3, uv4, weights, matrix;
            public int[] triangles, submeshSizes, boneIndices, joints;
            public SurfaceMaterial[] materials; public Shape[] shapes;
        }
        [Serializable] public class Pose { public int frame; public float[] bones, shapes; }
        [Serializable] public class Package
        {
            public string character; public int index; public Bone[] bones;
            public Surface[] surfaces; public Pose[] poses;
        }
        [Serializable] public class Edit { public string path; public float[] vertices, normals, weights; public int[] boneIndices; }
        [Serializable] public class Edits { public Edit[] surfaces; }
        [Serializable] public class VertexEdit { public int index; public float[] normal,weights; public int[] bones; }
        [Serializable] public class SparseSurface { public string path, fingerprint; public int vertexCount; public VertexEdit[] vertices; }
        [Serializable] public class SparseEdits { public SparseSurface[] surfaces; }
        static string DirectoryPath => Path.GetFullPath("Artifacts/BlenderOptimization/Exchange");
        static float[] Matrix(Matrix4x4 m) => Enumerable.Range(0,16).Select(i=>m[i/4,i%4]).ToArray();
        static float[] Vectors(Vector3[] a) => a.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
        static Vector3[] Vectors(float[] a) => Enumerable.Range(0,a.Length/3).Select(i=>new Vector3(a[i*3],a[i*3+1],a[i*3+2])).ToArray();

        [MenuItem("Homecoming/Blender/导出全部角色和检查姿势")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(DirectoryPath);
            for(int index=0;index<HomecomingCharacters.ResourcesPaths.Length;index++) Export(index);
            Debug.Log("BLENDER MODEL EXPORT COMPLETE");
        }
        public static void ExportYennefer(){Directory.CreateDirectory(DirectoryPath);Export(0);Debug.Log("YENNEFER BLENDER MODEL EXPORT COMPLETE");}
        public static void ExportDLCOutfits(){Directory.CreateDirectory(DirectoryPath);foreach(int index in new[]{0,2,3})Export(index);Debug.Log("DLC OUTFIT BLENDER EXPORT COMPLETE");}
        static string TexturePath(Material mat,string property){var tex=mat.HasProperty(property)?mat.GetTexture(property):null;return tex?Path.GetFullPath(AssetDatabase.GetAssetPath(tex)):"";}
        static void Export(int index)
        {
            var prefab=Resources.Load<GameObject>(HomecomingCharacters.ResourcesPaths[index]);
            var root=UnityEngine.Object.Instantiate(prefab); var rig=root.GetComponent<CharacterRig>();
            rig.Neutral(); root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var bones=root.GetComponentsInChildren<Transform>(true);
            var ids=bones.Select((b,i)=>(b,i)).ToDictionary(x=>x.b,x=>x.i);
            var bindRest=new Dictionary<Transform,Matrix4x4>();
            foreach(var skin in skins)
            {
                var toRoot=root.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix;
                for(int j=0;j<skin.bones.Length;j++)
                    if(!bindRest.ContainsKey(skin.bones[j]))bindRest.Add(skin.bones[j],toRoot*skin.sharedMesh.bindposes[j].inverse);
            }
            var package=new Package{character=prefab.name,index=index,
                bones=bones.Select(b=>new Bone{name=b.name,parent=b.parent&&ids.ContainsKey(b.parent)?ids[b.parent]:-1,matrix=Matrix(bindRest.TryGetValue(b,out var bind)?bind:root.transform.worldToLocalMatrix*b.localToWorldMatrix)}).ToArray()};
            package.surfaces=skins.Select(s=>
            {
                var m=s.sharedMesh;var w=m.boneWeights;
                string role=rig.dressedLegs!=null&&rig.dressedLegs.Contains(s)?"dressed_leg":rig.dressedHosiery!=null&&rig.dressedHosiery.Contains(s)?"dressed_lace":s.name.StartsWith("YenneferOriginalLace")?"retained_lace":rig.hosiery!=null&&rig.hosiery.Contains(s)?"hosiery":rig.anatomicalLegs!=null&&rig.anatomicalLegs.Contains(s)?"leg":s.gameObject==rig.leftShoe||s.gameObject==rig.rightShoe?"shoe":s==rig.skirt?"garment":"body";
                var shapes=new List<Shape>();
                for(int k=0;k<m.blendShapeCount;k++){var d=new Vector3[m.vertexCount];m.GetBlendShapeFrameVertices(k,0,d,null,null);shapes.Add(new Shape{name=m.GetBlendShapeName(k),delta=Vectors(d)});}
                return new Surface{name=s.name,path=AssetDatabase.GetAssetPath(m),role=role,enabled=s.enabled&&s.gameObject.activeInHierarchy,
                    vertices=Vectors(m.vertices),normals=Vectors(m.normals),uv=m.uv.SelectMany(v=>new[]{v.x,v.y}).ToArray(),uv2=m.uv2.SelectMany(v=>new[]{v.x,v.y}).ToArray(),uv3=m.uv3.SelectMany(v=>new[]{v.x,v.y}).ToArray(),uv4=m.uv4.SelectMany(v=>new[]{v.x,v.y}).ToArray(),
                    triangles=Enumerable.Range(0,m.subMeshCount).SelectMany(k=>m.GetTriangles(k)).ToArray(),submeshSizes=Enumerable.Range(0,m.subMeshCount).Select(k=>m.GetTriangles(k).Length).ToArray(),
                    matrix=Matrix(root.transform.worldToLocalMatrix*s.transform.localToWorldMatrix),joints=s.bones.Select(b=>ids[b]).ToArray(),
                    boneIndices=w.SelectMany(v=>new[]{v.boneIndex0,v.boneIndex1,v.boneIndex2,v.boneIndex3}).ToArray(),weights=w.SelectMany(v=>new[]{v.weight0,v.weight1,v.weight2,v.weight3}).ToArray(),shapes=shapes.ToArray(),
                    materials=s.sharedMaterials.Select(mat=>
                    {var color=mat.color;var tex=mat.HasProperty("_BaseMap")?mat.GetTexture("_BaseMap"):mat.mainTexture;var scale=mat.HasProperty("_BaseMap")?mat.GetTextureScale("_BaseMap"):Vector2.one;
                     var skin=mat.HasProperty("_SkinMap")?mat.GetTexture("_SkinMap"):null;
                     return new SurfaceMaterial{name=mat.name,uvScale=new[]{scale.x,scale.y},texture=tex?Path.GetFullPath(AssetDatabase.GetAssetPath(tex)):"",skinTexture=skin?Path.GetFullPath(AssetDatabase.GetAssetPath(skin)):"",normal=TexturePath(mat,"_BumpMap"),specular=TexturePath(mat,"_SpecGlossMap"),bumpScale=mat.HasProperty("_BumpScale")?mat.GetFloat("_BumpScale"):1,workflow=mat.HasProperty("_WorkflowMode")?mat.GetFloat("_WorkflowMode"):1,sheer=mat.HasProperty("_SheerAmount")?mat.GetFloat("_SheerAmount"):0,denier=mat.HasProperty("_Denier")?mat.GetFloat("_Denier"):0,lace=mat.HasProperty("_LaceAmount")?mat.GetFloat("_LaceAmount"):0,skinUV2=mat.HasProperty("_SkinUseSecondUV")?mat.GetFloat("_SkinUseSecondUV"):0,stockingBoundary=mat.HasProperty("_UseStockingBoundary")?mat.GetFloat("_UseStockingBoundary"):0,density=mat.HasProperty("_OpticalDensity")?mat.GetFloat("_OpticalDensity"):0,grazing=mat.HasProperty("_GrazingFloor")?mat.GetFloat("_GrazingFloor"):0,laceBodyRepeat=mat.HasProperty("_LaceBodyRepeat")?mat.GetFloat("_LaceBodyRepeat"):0,color=new[]{color.r,color.g,color.b,color.a}};}).ToArray()};
            }).ToArray();
            // Continuous sampling matters for the contact solver and garment dynamics.
            var motion=new HomecomingMotion(rig);var poses=new List<Pose>();
            for(int frame=0;frame<=1170;frame++)
            {
                motion.Sample(frame/30f);
                if(!new[]{0,90,240,300,390,450,570,630,810,870,930,990,1050,1110,1170}.Contains(frame))continue;
                poses.Add(new Pose{frame=frame,bones=bones.SelectMany(b=>Matrix(root.transform.worldToLocalMatrix*b.localToWorldMatrix)).ToArray(),
                    shapes=skins.SelectMany(s=>Enumerable.Range(0,s.sharedMesh.blendShapeCount).Select(k=>s.GetBlendShapeWeight(k)*.01f)).ToArray()});
            }
            package.poses=poses.ToArray();
            File.WriteAllText(Path.Combine(DirectoryPath,prefab.name+".json"),JsonUtility.ToJson(package));
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("BLENDER EXPORTED "+prefab.name+": "+skins.Length+" surfaces");
        }

        [MenuItem("Homecoming/Blender/应用已校验的模型修改")]
        public static void ApplyEdits()
        {
            string path=Path.Combine(DirectoryPath,"edits.json");
            var edits=JsonUtility.FromJson<Edits>(File.ReadAllText(path));
            foreach(var edit in edits.surfaces)
            {
                if(!edit.path.StartsWith("Assets/ThirdParty/Witcher",StringComparison.Ordinal)||!edit.path.EndsWith(".asset",StringComparison.Ordinal))throw new Exception("Unexpected mesh destination: "+edit.path);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(edit.path);
                if(!mesh||edit.vertices.Length!=mesh.vertexCount*3||edit.normals.Length!=mesh.vertexCount*3||edit.weights.Length!=mesh.vertexCount*4||edit.boneIndices.Length!=mesh.vertexCount*4)throw new Exception("Blender mesh identity/count mismatch: "+edit.path);
                if(edit.vertices.Any(v=>float.IsNaN(v)||float.IsInfinity(v)))throw new Exception("Non-finite Blender vertex");
                var weights=new BoneWeight[mesh.vertexCount];
                for(int i=0;i<weights.Length;i++)
                {
                    int p=i*4;float total=edit.weights.Skip(p).Take(4).Sum();
                    if(Mathf.Abs(total-1)>.0001f)throw new Exception("Unnormalized Blender weights: "+edit.path);
                    for(int k=0;k<4;k++)if(edit.weights[p+k]<0||edit.boneIndices[p+k]<0||edit.boneIndices[p+k]>=mesh.bindposes.Length)throw new Exception("Invalid Blender bone weight");
                    weights[i]=new BoneWeight{boneIndex0=edit.boneIndices[p],boneIndex1=edit.boneIndices[p+1],boneIndex2=edit.boneIndices[p+2],boneIndex3=edit.boneIndices[p+3],weight0=edit.weights[p],weight1=edit.weights[p+1],weight2=edit.weights[p+2],weight3=edit.weights[p+3]};
                }
                mesh.vertices=Vectors(edit.vertices);mesh.normals=Vectors(edit.normals);mesh.boneWeights=weights;mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
                Debug.Log("BLENDER EDIT APPLIED: "+edit.path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("BLENDER MODEL EDITS SAVED");
        }

        static string Fingerprint(Mesh mesh)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    writer.Write(mesh.vertexCount);
                    foreach(var v in mesh.vertices){writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}
                    var triangles=mesh.triangles;writer.Write(triangles.Length);
                    foreach(int index in triangles)writer.Write(index);
                    writer.Write(mesh.bindposes.Length);
                }
                using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
        }
        [MenuItem("Homecoming/Blender/应用保存的接缝和法线修正")]
        public static void ApplySavedRefinements(){ApplySavedRefinements("Assets/ThirdParty/Witcher");AssetDatabase.SaveAssets();}
        public static void ApplySavedRefinements(string destinationPrefix)
        {
            const string source="DCC/Blender/refinements.json";
            if(!File.Exists(source))return;
            var edits=JsonUtility.FromJson<SparseEdits>(File.ReadAllText(source));
            var pending=new List<(Mesh mesh,SparseSurface edit)>();
            // Validate the full set before touching assets. Geometry hashes keep
            // these vertex-index patches from landing on a different source mesh.
            foreach(var edit in edits.surfaces.Where(e=>e.path.StartsWith(destinationPrefix,StringComparison.Ordinal)))
            {
                if(!edit.path.StartsWith("Assets/ThirdParty/Witcher",StringComparison.Ordinal)||edit.path.Contains("..")||!edit.path.EndsWith(".asset",StringComparison.Ordinal))throw new Exception("Invalid Blender override path");
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(edit.path);
                if(!mesh||mesh.vertexCount!=edit.vertexCount||Fingerprint(mesh)!=edit.fingerprint)throw new Exception("Blender override source fingerprint changed: "+edit.path);
                foreach(var vertex in edit.vertices)
                {
                    if(vertex.index<0||vertex.index>=mesh.vertexCount||vertex.normal.Length!=3||vertex.bones.Length!=4||vertex.weights.Length!=4||Mathf.Abs(vertex.weights.Sum()-1)>.0001f)throw new Exception("Invalid Blender vertex override");
                    if(vertex.weights.Any(w=>float.IsNaN(w)||float.IsInfinity(w)||w<0)||vertex.normal.Any(w=>float.IsNaN(w)||float.IsInfinity(w))||vertex.bones.Any(b=>b<0||b>=mesh.bindposes.Length))throw new Exception("Invalid Blender normal or bone index");
                }
                pending.Add((mesh,edit));
            }
            foreach(var item in pending)
            {
                var normals=item.mesh.normals;var weights=item.mesh.boneWeights;
                foreach(var vertex in item.edit.vertices)
                {
                    normals[vertex.index]=new Vector3(vertex.normal[0],vertex.normal[1],vertex.normal[2]).normalized;
                    weights[vertex.index]=new BoneWeight{boneIndex0=vertex.bones[0],boneIndex1=vertex.bones[1],boneIndex2=vertex.bones[2],boneIndex3=vertex.bones[3],weight0=vertex.weights[0],weight1=vertex.weights[1],weight2=vertex.weights[2],weight3=vertex.weights[3]};
                }
                item.mesh.normals=normals;item.mesh.boneWeights=weights;item.mesh.RecalculateTangents();EditorUtility.SetDirty(item.mesh);
                Debug.Log("BLENDER SAVED REFINEMENT: "+item.edit.path+"; changed vertices="+item.edit.vertices.Length);
            }
        }
        public static void RefineAndReview()
        {
            ApplySavedRefinements();ShaniModelReview.Review();HandMovementReview.Validate();HomecomingFootwearReview.Run();HomecomingGarmentReview.Run();HomecomingContinuousReview.Run();
        }
    }
}
