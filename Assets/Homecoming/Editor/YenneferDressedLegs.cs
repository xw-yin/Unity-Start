using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferDressedLegs
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Folder="Assets/ThirdParty/WitcherYenneferDLC/Unity/";
        [Serializable] class Manifest { public Part[] parts;public Surface[] materials; }
        [Serializable] class Part { public string file,name;public int material; }
        [Serializable] class Surface { public string name;public int texture; }
        public static void Build(CharacterRig rig)
        {
            foreach(var old in rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.StartsWith("YenneferDressed")).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            rig.dressedLegs=new SkinnedMeshRenderer[2];rig.dressedHosiery=new SkinnedMeshRenderer[2];
            foreach(bool lace in new[]{false,true})
            {
                var part=data.parts.Single(p=>data.materials[p.material].name=="body_05_wa__yennefer_Material"+(lace?"6":"7"));
                var source=WitcherCharacterImport.ReadPreparedMesh(Source,part.file,part.name);var v=source.vertices;var t=source.triangles;
                var material=Material(lace,data.materials[part.material].texture);
                for(int side=0;side<2;side++)
                {
                    // The lower skin components end below 0.9m; torso, neck
                    // and arms start above that gap. Preserve complete faces.
                    var faces=Enumerable.Range(0,t.Length/3).Where(i=>new[]{t[i*3],t[i*3+1],t[i*3+2]}.All(id=>(v[id].x<0)==(side==0)&&(lace||v[id].y<.9f))).SelectMany(i=>new[]{t[i*3],t[i*3+1],t[i*3+2]}).ToArray();
                    var used=faces.Distinct().ToArray();var map=used.Select((id,i)=>(id,i)).ToDictionary(x=>x.id,x=>x.i);
                    var mesh=new Mesh{name="YenneferDressed"+(lace?"Lace":"Leg")+(side==0?"Left":"Right"),vertices=used.Select(i=>v[i]).ToArray(),normals=used.Select(i=>source.normals[i]).ToArray(),uv=used.Select(i=>source.uv[i]).ToArray(),boneWeights=used.Select(i=>source.boneWeights[i]).ToArray(),bindposes=source.bindposes,triangles=faces.Select(i=>map[i]).ToArray()};
                    mesh.RecalculateBounds();mesh.RecalculateTangents();string path=Folder+"Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(existing){WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                    var obj=new GameObject(mesh.name);obj.transform.SetParent(rig.leftShoe.transform.parent,false);var skin=obj.AddComponent<SkinnedMeshRenderer>();
                    var shoe=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=shoe.bones;skin.rootBone=shoe.rootBone;skin.sharedMaterial=material;skin.updateWhenOffscreen=true;skin.localBounds=shoe.localBounds;
                    if(lace)rig.dressedHosiery[side]=skin;else rig.dressedLegs[side]=skin;
                    Debug.Log("DLC DRESSED SURFACE: "+mesh.name+" vertices="+mesh.vertexCount+" source="+part.file+"; original positions, UVs and weights retained.");
                }
                UnityEngine.Object.DestroyImmediate(source);
            }
            foreach(var sock in rig.hosiery)sock.enabled=false;
            foreach(var lace in rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.StartsWith("YenneferOriginalLace")))lace.enabled=false;
        }
        static Material Material(bool lace,int texture)
        {
            string path=Folder+"Materials/DLCDressed"+(lace?"Lace":"Skin")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Homecoming/Stocking Coverage"));AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("Homecoming/Stocking Coverage");
            mat.color=Color.white;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Texture_"+texture.ToString("D2")+".png"));mat.SetFloat("_Cull",0);mat.SetFloat("_Smoothness",lace?.27f:.18f);
            mat.SetFloat("_SourceAlphaClip",lace?1:0);mat.SetFloat("_SourceCutoff",.18f);mat.SetFloat("_SheerAmount",0);mat.SetFloat("_BootRestMask",1);mat.SetFloat("_BootMask",0);mat.renderQueue=lace?2450:2000;EditorUtility.SetDirty(mat);return mat;
        }
    }
}
