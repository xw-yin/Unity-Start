using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Retarget the existing anatomical body, rather than deriving feet from footwear.
    public static class WitcherCharacterFeet
    {
        static string Folder;
        [Serializable] class Manifest { public Node[] nodes; public Part[] parts; }
        [Serializable] class Node { public string name; }
        [Serializable] class Part { public int[] joints; }
        struct Vertex { public Vector3 p,n; public BoneWeight w; }
        public static void Build(CharacterRig rig,string folder)
        {
            Folder=folder;
            // Extend under the original trousers so the bending knee cannot expose an open seam.
            float cut=.64f;
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ThirdParty/VRoidSample/Unity/Meshes/Part_09.asset");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/ThirdParty/VRoidSample/Prepared/manifest.json"));
            var boot=rig.leftShoe.GetComponent<SkinnedMeshRenderer>();
            var names=manifest.parts[9].joints.Select(j=>manifest.nodes[j].name).ToArray();
            var map=names.Select(n=>Array.FindIndex(boot.bones,b=>b.name==n)).ToArray();
            Vector3 Source(string n)=>source.bindposes[Array.IndexOf(names,n)].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 Target(string n)=>boot.sharedMesh.bindposes[Array.FindIndex(boot.bones,b=>b.name==n)].inverse.MultiplyPoint3x4(Vector3.zero);
            var vertices=source.vertices; var normals=source.normals; var weights=source.boneWeights;
            var material=StockingMaterial();
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                Vector3 sa=Source(prefix+"Foot"),sk=Source(prefix+"LowerLeg"),st=Source(prefix+"UpperLeg");
                Vector3 da=Target(prefix+"Foot"),dk=Target(prefix+"LowerLeg"),dt=Target(prefix+"UpperLeg");
                Debug.Log("LOWER BODY BIND "+prefix+" source="+sa+","+sk+","+st+" destination="+da+","+dk+","+dt);
                BoneWeight LegWeight(float y)
                {
                    int fi=Array.FindIndex(boot.bones,x=>x.name==prefix+"Foot"),ci=Array.FindIndex(boot.bones,x=>x.name==prefix+"LowerLeg"),ti=Array.FindIndex(boot.bones,x=>x.name==prefix+"UpperLeg");
                    float ankle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(da.y,da.y+.065f,y));
                    float knee=Mathf.SmoothStep(0,1,Mathf.InverseLerp(dk.y-.025f,dk.y+.065f,y));
                    return y<da.y+.065f ? new BoneWeight{boneIndex0=fi,weight0=1-ankle,boneIndex1=ci,weight1=ankle} : new BoneWeight{boneIndex0=ci,weight0=1-knee,boneIndex1=ti,weight1=knee};
                }
                Vertex Adapt(int index)
                {
                    var p=vertices[index]; Vector3 a,b; float y;
                    if(p.y<sa.y) { a=sa;b=da;y=da.y+(p.y-sa.y)*1.05f; }
                    else if(p.y<sk.y) { float u=Mathf.InverseLerp(sa.y,sk.y,p.y);a=Vector3.Lerp(sa,sk,u);b=Vector3.Lerp(da,dk,u);y=b.y; }
                    else { float u=(p.y-sk.y)/(st.y-sk.y);a=Vector3.LerpUnclamped(sk,st,u);b=Vector3.LerpUnclamped(dk,dt,u);y=b.y; }
                    float radial=Mathf.Lerp(1.1f,1.65f,Mathf.InverseLerp(.16f,.34f,y));
                    radial=Mathf.Lerp(radial,1.05f,Mathf.InverseLerp(.40f,.60f,y));
                    p=new Vector3(b.x+(p.x-a.x)*radial,y,b.z+(p.z-a.z)*radial);
                    return new Vertex{p=p,n=normals[index],w=LegWeight(p.y)};
                }
                var output=new List<Vertex>();var triangles=source.triangles;
                for(int i=0;i<triangles.Length;i+=3)
                {
                    // Use source bone ownership. Inner calf vertices can cross x=0,
                    // so position alone cannot reliably separate the two legs.
                    bool otherSide=false;
                    for(int j=0;j<3;j++)
                    {
                        int index=triangles[i+j];var w=weights[index];float left=0,right=0;
                        var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var influence=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                        for(int k=0;k<4;k++){if(names[ids[k]].StartsWith("J_Bip_L_"))left+=influence[k];if(names[ids[k]].StartsWith("J_Bip_R_"))right+=influence[k];}
                        int owner=left>right?0:right>left?1:vertices[index].x<0?0:1;
                        if(owner!=side)otherSide=true;
                    }
                    if(otherSide)continue;
                    var polygon=new List<Vertex>();
                    for(int j=0;j<3;j++) polygon.Add(Adapt(triangles[i+j]));
                    var clipped=new List<Vertex>();
                    for(int j=0;j<3;j++)
                    {
                        var a=polygon[j];var b=polygon[(j+1)%3];bool inside=a.p.y<=cut,next=b.p.y<=cut;
                        if(inside)clipped.Add(a);
                        if(inside!=next)
                        {
                            float t=(cut-a.p.y)/(b.p.y-a.p.y);var p=Vector3.Lerp(a.p,b.p,t);
                            // Keep the seam on the same calf/knee influences as its neighbouring vertices.
                            clipped.Add(new Vertex{p=p,n=Vector3.Lerp(a.n,b.n,t).normalized,w=LegWeight(p.y)});
                        }
                    }
                    for(int j=1;j+1<clipped.Count;j++){output.Add(clipped[0]);output.Add(clipped[j]);output.Add(clipped[j+1]);}
                }
                var uv=output.Select(v=>
                {
                    float u;
                    if(v.p.y<.11f) u=(v.p.x-da.x)/.065f+.5f;
                    else { var center=Vector3.LerpUnclamped(da,dk,(v.p.y-da.y)/(dk.y-da.y));if(v.p.y>dk.y)center=Vector3.LerpUnclamped(dk,dt,(v.p.y-dk.y)/(dt.y-dk.y));u=Mathf.Atan2(v.p.z-center.z,v.p.x-center.x)/(2*Mathf.PI)+.5f; }
                    float textureV=v.p.y<.10f?.055f:Mathf.Lerp(.16f,.99f,Mathf.InverseLerp(.10f,.54f,v.p.y));
                    return new Vector2(u*3,textureV);
                }).ToArray();
                for(int i=0;i<uv.Length;i+=3)
                {
                    float min=Mathf.Min(uv[i].x,Mathf.Min(uv[i+1].x,uv[i+2].x)),max=Mathf.Max(uv[i].x,Mathf.Max(uv[i+1].x,uv[i+2].x));
                    if(max-min>1.5f)for(int j=0;j<3;j++)if(uv[i+j].x<1.5f)uv[i+j].x+=3;
                }
                var mesh=new Mesh{name=side==0?"AnatomicalLeftStocking":"AnatomicalRightStocking",vertices=output.Select(v=>v.p).ToArray(),normals=output.Select(v=>v.n).ToArray(),uv=uv,boneWeights=output.Select(v=>v.w).ToArray(),bindposes=boot.sharedMesh.bindposes,triangles=Enumerable.Range(0,output.Count).ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();
                string path=Folder+"Meshes/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){WitcherCharacterImport.CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var obj=new GameObject(side==0?"左腿连续丝袜":"右腿连续丝袜");obj.transform.SetParent(boot.transform.parent,false);
                var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=boot.bones;skin.rootBone=rig.hips;skin.sharedMaterial=material;skin.updateWhenOffscreen=true;skin.localBounds=boot.localBounds;
            }
        }
        static Material StockingMaterial()
        {
            string path=Folder+"Materials/InnerLegs.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Homecoming/Stocking Coverage"));AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("Homecoming/Stocking Coverage");mat.color=new Color(.045f,.032f,.028f);
            mat.SetFloat("_Smoothness",.16f);EditorUtility.SetDirty(mat);return mat;
        }
    }
}
