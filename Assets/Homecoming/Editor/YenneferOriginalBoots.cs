using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // DLC body material 1 contains the boot shells; material 8 adds cuff / straps.
    public static class YenneferOriginalBoots
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/DLCBoots/";
        const string Folder="Assets/ThirdParty/WitcherYenneferDLC/Unity/";
        public static void Restore(CharacterRig rig)
        {
            for(int side=0;side<2;side++)
            {
                string name="OriginalYenneferBootDLC"+(side==0?"Left":"Right");
                var pieces=Enumerable.Range(0,2).Select(i=>WitcherCharacterImport.ReadPreparedMesh(Source,"OriginalBoot"+(side==0?"Left":"Right")+"_"+i+".bin",name)).ToArray();
                var mesh=new Mesh{name=name,vertices=pieces.SelectMany(m=>m.vertices).ToArray(),normals=pieces.SelectMany(m=>m.normals).ToArray(),uv=pieces.SelectMany(m=>m.uv).ToArray(),boneWeights=pieces.SelectMany(m=>m.boneWeights).ToArray(),bindposes=pieces[0].bindposes,subMeshCount=2};
                mesh.SetTriangles(pieces[0].triangles,0);mesh.SetTriangles(pieces[1].triangles.Select(v=>v+pieces[0].vertexCount).ToArray(),1);mesh.RecalculateBounds();mesh.RecalculateTangents();
                var contour=Cuff(mesh);var marker=new Vector2[mesh.vertexCount];
                foreach(int i in contour)marker[i]=Vector2.one;mesh.uv2=marker;
                var shoe=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                WitcherCharacterImport.CopyMesh(mesh,shoe.sharedMesh);
                shoe.sharedMesh.uv2=mesh.uv2;
                if(!shoe.sharedMesh.vertices.SequenceEqual(mesh.vertices)||!shoe.sharedMesh.boneWeights.SequenceEqual(mesh.boneWeights)||!shoe.sharedMesh.uv.SequenceEqual(mesh.uv)||!shoe.sharedMesh.triangles.SequenceEqual(mesh.triangles)||!shoe.sharedMesh.bindposes.SequenceEqual(mesh.bindposes))throw new Exception("Boot differs from the original game export");
                var materials=new Material[2];
                for(int i=0;i<2;i++)
                {
                    string path=Folder+"Materials/DLCBoot"+(i==0?"Shell":"Straps")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="YenneferDLCBoot"+i};AssetDatabase.CreateAsset(mat,path);}
                    mat.color=Color.white;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/WitcherYenneferDLC/Prepared/Texture_00.png"));
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/WitcherYenneferDLC/Prepared/Texture_01.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.7f);
                    mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",.28f);mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);materials[i]=mat;
                }
                shoe.sharedMaterials=materials;
                Debug.Log("ORIGINAL BOOT RESTORED: "+name+" vertices="+mesh.vertexCount+" triangles="+mesh.triangles.Length/3+" height="+mesh.bounds.size.y+" submeshes="+mesh.subMeshCount+" weighted joints="+mesh.boneWeights.SelectMany(w=>new[]{w.boneIndex0,w.weight1>0?w.boneIndex1:-1,w.weight2>0?w.boneIndex2:-1,w.weight3>0?w.boneIndex3:-1}).Where(i=>i>=0).Distinct().Count());
                foreach(var piece in pieces)UnityEngine.Object.DestroyImmediate(piece);UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        internal static int[] Cuff(Mesh mesh)
        {
            var v=mesh.vertices;var welded=new System.Collections.Generic.Dictionary<Vector3Int,int>();var ids=new int[v.Length];
            for(int i=0;i<v.Length;i++){var key=Vector3Int.RoundToInt(v[i]*100000);if(!welded.TryGetValue(key,out int id)){id=welded.Count;welded.Add(key,id);}ids[i]=id;}
            var edges=new System.Collections.Generic.Dictionary<long,int>();var t=mesh.triangles;
            for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++){int a=ids[t[i+j]],b=ids[t[i+(j+1)%3]];long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);edges.TryGetValue(key,out int count);edges[key]=count+1;}
            var positions=new Vector3[welded.Count];for(int i=0;i<v.Length;i++)positions[ids[i]]=v[i];
            var links=new System.Collections.Generic.Dictionary<int,System.Collections.Generic.List<int>>();
            foreach(var edge in edges.Where(e=>e.Value==1))
            {
                int a=(int)(edge.Key>>32),b=(int)edge.Key;if(positions[a].y<=.55f||positions[b].y<=.55f)continue;
                if(!links.ContainsKey(a))links[a]=new System.Collections.Generic.List<int>();if(!links.ContainsKey(b))links[b]=new System.Collections.Generic.List<int>();links[a].Add(b);links[b].Add(a);
            }
            var remaining=new System.Collections.Generic.HashSet<int>(links.Keys);var best=new System.Collections.Generic.HashSet<int>();float area=0;
            while(remaining.Count>0)
            {
                int start=remaining.First();remaining.Remove(start);var pending=new System.Collections.Generic.Stack<int>();pending.Push(start);var group=new System.Collections.Generic.HashSet<int>{start};
                while(pending.Count>0)foreach(int next in links[pending.Pop()])if(remaining.Remove(next)){group.Add(next);pending.Push(next);}
                var bounds=new Bounds(positions[start],Vector3.zero);foreach(int id in group)bounds.Encapsulate(positions[id]);
                float score=bounds.size.x*bounds.size.z;if(score>area){area=score;best=group;}
            }
            if(best.Count<8)throw new Exception("Original boot circular opening is absent");
            return Enumerable.Range(0,v.Length).Where(i=>best.Contains(ids[i])).ToArray();
        }
        public static void FitCoverage(CharacterRig rig)
        {
            for(int side=0;side<2;side++)
            {
                var shoe=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                var v=shoe.sharedMesh.vertices;var contour=Cuff(shoe.sharedMesh).Select(i=>v[i]).GroupBy(p=>Vector3Int.RoundToInt(p*100000)).Select(g=>g.First()).ToArray();var center=contour.Aggregate(Vector3.zero,(sum,p)=>sum+p)/contour.Length;
                var sorted=contour.OrderBy(p=>Mathf.Atan2(p.z-center.z,p.x-center.x)).ToArray();
                foreach(var surface in new[]{rig.hosiery[side],rig.dressedLegs[side],rig.dressedHosiery[side]}.Concat(rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name=="YenneferOriginalLace"+(side==0?"Left":"Right"))))
                {
                    var points=surface.sharedMesh.vertices;var boundary=new Vector2[points.Length];
                    for(int i=0;i<points.Length;i++)
                    {
                        float angle=Mathf.Atan2(points[i].z-center.z,points[i].x-center.x);float height=0;
                        for(int j=0;j<sorted.Length;j++)
                        {
                            var a=sorted[j];var b=sorted[(j+1)%sorted.Length];float aa=Mathf.Atan2(a.z-center.z,a.x-center.x),bb=Mathf.Atan2(b.z-center.z,b.x-center.x);if(bb<=aa)bb+=Mathf.PI*2;float current=angle;if(current<aa)current+=Mathf.PI*2;
                            if(current>=aa&&current<=bb){height=Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(aa,bb,current));break;}
                        }
                        boundary[i]=new Vector2(points[i].y,height+.002f);
                    }
                    surface.sharedMesh.uv4=boundary;EditorUtility.SetDirty(surface.sharedMesh);
                }
                foreach(var surface in new[]{rig.anatomicalLegs[side],rig.hosiery[side],rig.dressedLegs[side],rig.dressedHosiery[side]}.Concat(rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name=="YenneferOriginalLace"+(side==0?"Left":"Right"))))FitOpening(surface.sharedMesh,center,sorted);
            }
        }
        static void FitOpening(Mesh mesh,Vector3 center,Vector3[] contour)
        {
            var vertices=mesh.vertices;var delta=new Vector3[vertices.Length];float largest=0;
            for(int i=0;i<vertices.Length;i++)
            {
                var offset=new Vector2(vertices[i].x-center.x,vertices[i].z-center.z);float angle=Mathf.Atan2(offset.y,offset.x),radius=0;
                for(int j=0;j<contour.Length;j++)
                {
                    var a=contour[j];var b=contour[(j+1)%contour.Length];float aa=Mathf.Atan2(a.z-center.z,a.x-center.x),bb=Mathf.Atan2(b.z-center.z,b.x-center.x),value=angle;if(j==contour.Length-1){bb+=Mathf.PI*2;if(value<aa)value+=Mathf.PI*2;}
                    if(value>=aa&&value<=bb){radius=Mathf.Lerp(new Vector2(a.x-center.x,a.z-center.z).magnitude,new Vector2(b.x-center.x,b.z-center.z).magnitude,Mathf.InverseLerp(aa,bb,value));break;}
                }
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(center.y-.12f,center.y-.035f,vertices[i].y))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(center.y+.015f,center.y+.07f,vertices[i].y)));
                float limit=Mathf.Max(.01f,radius*.88f-.002f)+(mesh.name.StartsWith("YenneferOriginalLace")?.0015f:0);
                if(offset.magnitude>limit){var fitted=offset.normalized*limit;delta[i]=new Vector3(fitted.x-offset.x,0,fitted.y-offset.y)*blend;largest=Mathf.Max(largest,delta[i].magnitude);}
            }
            // Rebuilding an existing fit replaces that frame, retaining any
            // source blendshapes and every original neutral vertex / triangle.
            var originals=new System.Collections.Generic.List<(string name,float weight,Vector3[] v,Vector3[] n,Vector3[] t)>();
            for(int i=0;i<mesh.blendShapeCount;i++)if(mesh.GetBlendShapeName(i)!="BootFit")for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++)
            {var dv=new Vector3[mesh.vertexCount];var dn=new Vector3[mesh.vertexCount];var dt=new Vector3[mesh.vertexCount];mesh.GetBlendShapeFrameVertices(i,f,dv,dn,dt);originals.Add((mesh.GetBlendShapeName(i),mesh.GetBlendShapeFrameWeight(i,f),dv,dn,dt));}
            mesh.ClearBlendShapes();foreach(var shape in originals)mesh.AddBlendShapeFrame(shape.name,shape.weight,shape.v,shape.n,shape.t);
            mesh.AddBlendShapeFrame("BootFit",100,delta,null,null);EditorUtility.SetDirty(mesh);Debug.Log("TEMPORARY BOOT OPENING FIT: "+mesh.name+" maximum adjustment="+largest+"; neutral source geometry unchanged.");
        }
    }
}
