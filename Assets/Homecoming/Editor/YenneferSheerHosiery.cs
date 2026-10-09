using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferSheerHosiery
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Folder="Assets/ThirdParty/WitcherYenneferDLC/Unity/";
        const string Skin="Assets/ThirdParty/WitcherYenneferOriginalFeet/Prepared/Texture_00.png";
        [Serializable] class Manifest { public Part[] parts;public Surface[] materials; }
        [Serializable] class Part { public string name,file;public int material; }
        [Serializable] class Surface { public string name; }
        public static void Build(CharacterRig rig,string folder)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            var part=manifest.parts.Single(p=>manifest.materials[p.material].name=="body_05_wa__yennefer_Material6");
            foreach(var old in rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name==part.name||s.name.StartsWith("YenneferOriginalLace")).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            var source=WitcherCharacterImport.ReadPreparedMesh(Source,part.file,part.name);
            var lace=AssetDatabase.LoadAssetAtPath<Material>(folder+"Materials/OriginalLaceHosiery.mat");
            if(!lace){lace=new Material(Shader.Find("Homecoming/Stocking Coverage"));AssetDatabase.CreateAsset(lace,folder+"Materials/OriginalLaceHosiery.mat");}
            Configure(lace,true);
            foreach(var sock in rig.hosiery)Configure(sock.sharedMaterial,false);
            for(int side=0;side<2;side++)
            {
                var leg=rig.anatomicalLegs[side];var lv=leg.sharedMesh.vertices;var ln=leg.sharedMesh.normals;var lu=leg.sharedMesh.uv;var lw=leg.sharedMesh.boneWeights;var lt=leg.sharedMesh.triangles;
                var body=rig.hosiery[side].sharedMesh;string bodyName=body.name;WitcherCharacterImport.CopyMesh(leg.sharedMesh,body);body.name=bodyName;body.ClearBlendShapes();body.uv2=leg.sharedMesh.uv;
                var sv=source.vertices;var sn=source.normals;var su=source.uv;var st=source.triangles;
                var used=new List<int>();var lookup=new Dictionary<int,int>();var indices=new List<int>();
                bool positive=leg.sharedMesh.bounds.center.x>0;
                for(int i=0;i<st.Length;i+=3)
                {
                    var face=new[]{st[i],st[i+1],st[i+2]};
                    if(face.Any(v=>(sv[v].x>0)!=positive))continue;
                    foreach(int v in face){if(!lookup.ContainsKey(v)){lookup[v]=used.Count;used.Add(v);}indices.Add(lookup[v]);}
                }
                var vertices=new Vector3[used.Count];var normals=new Vector3[used.Count];var skinUV=new Vector2[used.Count];var weights=new BoneWeight[used.Count];
                for(int i=0;i<used.Count;i++)
                {
                    var p=sv[used[i]];float nearest=float.MaxValue;int triangle=0;Vector3 bary=Vector3.zero,point=Vector3.zero;
                    for(int j=0;j<lt.Length;j+=3)
                    {
                        var q=Closest(p,lv[lt[j]],lv[lt[j+1]],lv[lt[j+2]],out var b);float distance=(p-q).sqrMagnitude;
                        if(distance<nearest){nearest=distance;triangle=j;bary=b;point=q;}
                    }
                    if(nearest>.05f*.05f)throw new Exception("Original lace does not fit the selected source leg: "+side+" gap="+Mathf.Sqrt(nearest));
                    var ids=new[]{lt[triangle],lt[triangle+1],lt[triangle+2]};
                    var normal=(ln[ids[0]]*bary.x+ln[ids[1]]*bary.y+ln[ids[2]]*bary.z).normalized;
                    vertices[i]=point+normal*.0015f;normals[i]=normal;
                    skinUV[i]=lu[ids[0]]*bary.x+lu[ids[1]]*bary.y+lu[ids[2]]*bary.z;
                    var combined=new Dictionary<int,float>();
                    for(int k=0;k<3;k++)
                    {
                        var w=lw[ids[k]];var bi=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var bw=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                        for(int b=0;b<4;b++)if(bw[b]>0){combined.TryGetValue(bi[b],out var value);combined[bi[b]]=value+bw[b]*bary[k];}
                    }
                    var top=combined.OrderByDescending(x=>x.Value).Take(4).ToArray();float sum=top.Sum(x=>x.Value);var result=new BoneWeight();
                    for(int k=0;k<top.Length;k++)switch(k){case 0:result.boneIndex0=top[k].Key;result.weight0=top[k].Value/sum;break;case 1:result.boneIndex1=top[k].Key;result.weight1=top[k].Value/sum;break;case 2:result.boneIndex2=top[k].Key;result.weight2=top[k].Value/sum;break;case 3:result.boneIndex3=top[k].Key;result.weight3=top[k].Value/sum;break;}
                    weights[i]=result;
                }
                string name=side==0?"YenneferOriginalLaceLeft":"YenneferOriginalLaceRight";
                var mesh=new Mesh{name=name,vertices=vertices,normals=normals,uv=used.Select(i=>su[i]).ToArray(),uv2=skinUV,boneWeights=weights,bindposes=leg.sharedMesh.bindposes,triangles=indices.ToArray()};
                mesh.RecalculateBounds();mesh.RecalculateTangents();string path=folder+"Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){existing.Clear();existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.uv=mesh.uv;existing.uv2=mesh.uv2;existing.boneWeights=mesh.boneWeights;existing.bindposes=mesh.bindposes;existing.triangles=mesh.triangles;existing.RecalculateBounds();existing.RecalculateTangents();EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var obj=new GameObject(name);obj.transform.SetParent(leg.transform.parent,false);obj.transform.localPosition=leg.transform.localPosition;obj.transform.localRotation=leg.transform.localRotation;obj.transform.localScale=leg.transform.localScale;
                var skin=obj.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=leg.bones;skin.rootBone=rig.hips;skin.sharedMaterial=lace;skin.updateWhenOffscreen=true;skin.localBounds=leg.localBounds;
                var cuff=LaceBottomContour(mesh);var center=mesh.bounds.center;
                var boundary=rig.hosiery[side].sharedMesh.vertices.Select(p=>new Vector2(p.y,CuffHeight(p,center,cuff))).ToArray();
                rig.hosiery[side].sharedMesh.uv3=boundary;EditorUtility.SetDirty(rig.hosiery[side].sharedMesh);
                ExtendOriginalPattern(mesh,rig.hosiery[side].sharedMesh,cuff);
            }
            UnityEngine.Object.DestroyImmediate(source);
            YenneferDressedLegs.Build(rig);
            if(rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.StartsWith("OriginalYenneferBoot"))YenneferOriginalBoots.FitCoverage(rig);
        }
        static void Configure(Material mat,bool lace)
        {
            mat.shader=Shader.Find("Homecoming/Stocking Coverage");mat.color=new Color(.012f,.012f,.014f);
            mat.SetTexture("_SkinMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Skin));mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Texture_04.png"));
            mat.SetTexture("_BumpMap",null);mat.DisableKeyword("_NORMALMAP");mat.DisableKeyword("_DETAIL_NORMALMAP");
            mat.SetFloat("_SheerAmount",1);mat.SetFloat("_Denier",20);mat.SetFloat("_LaceAmount",1);mat.SetFloat("_SkinUseSecondUV",1);mat.SetFloat("_Smoothness",.27f);mat.renderQueue=-1;EditorUtility.SetDirty(mat);
            mat.SetFloat("_UseStockingBoundary",lace?0:1);
            mat.SetFloat("_OpticalDensity",.035f);mat.SetFloat("_GrazingFloor",.55f);mat.SetFloat("_LaceRelief",.06f);mat.SetFloat("_LaceBodyRepeat",lace?0:.875f);
        }
        static void ExtendOriginalPattern(Mesh lace,Mesh body,Vector3[] contour)
        {
            var p=lace.vertices;var uv=lace.uv;var center=lace.bounds.center;
            var ring=contour.Select(q=>Enumerable.Range(0,p.Length).First(i=>(p[i]-q).sqrMagnitude<.000000001f)).OrderBy(i=>Mathf.Atan2(p[i].z-center.z,p[i].x-center.x)).ToArray();
            var u=ring.Select(i=>uv[i].x).ToArray();
            for(int i=1;i<u.Length;i++)u[i]=u[i-1]+Mathf.DeltaAngle(0,(u[i]-u[i-1])*120)/120;
            float direction=Mathf.Sign(u[u.Length-1]-u[0]);
            float meanY=p.Average(v=>v.y),meanV=uv.Average(v=>v.y),numerator=0,denominator=0;
            for(int i=0;i<p.Length;i++){numerator+=(p[i].y-meanY)*(uv[i].y-meanV);denominator+=(p[i].y-meanY)*(p[i].y-meanY);}
            float slope=numerator/denominator;var points=body.vertices;var mapped=new Vector2[points.Length];
            for(int i=0;i<points.Length;i++)
            {
                float angle=Mathf.Atan2(points[i].z-center.z,points[i].x-center.x);
                for(int j=0;j<ring.Length;j++)
                {
                    int a=ring[j],b=ring[(j+1)%ring.Length];float left=Mathf.Atan2(p[a].z-center.z,p[a].x-center.x),right=Mathf.Atan2(p[b].z-center.z,p[b].x-center.x),value=angle;
                    if(j==ring.Length-1){right+=Mathf.PI*2;if(value<left)value+=Mathf.PI*2;}
                    if(value<left||value>right)continue;
                    float t=Mathf.InverseLerp(left,right,value),next=j==ring.Length-1?u[0]+direction*3:u[j+1];
                    mapped[i]=new Vector2(Mathf.Lerp(u[j],next,t),Mathf.Lerp(uv[a].y,uv[b].y,t)+(points[i].y-Mathf.Lerp(p[a].y,p[b].y,t))*slope);break;
                }
            }
            // Preserve the original anatomical UVs for skin; use the native
            // lace seam, orientation and texel scale for the fabric continuation.
            if(body.uv2.Length!=body.vertexCount)body.uv2=body.uv;
            var native=lace.uv2;int axis=native.Max(q=>q.x)-native.Min(q=>q.x)>native.Max(q=>q.y)-native.Min(q=>q.y)?0:1;
            var order=Enumerable.Range(0,native.Length).OrderBy(i=>native[i][axis]).ToArray();var targetU=uv.Select(q=>q.x).ToArray();float previous=targetU[order[0]];
            foreach(int id in order.Skip(1)){targetU[id]=previous+Mathf.DeltaAngle(0,(targetU[id]-previous)*120)/120;previous=targetU[id];}
            var normal=new Matrix4x4();normal.m33=1;var rhsU=Vector3.zero;var rhsV=Vector3.zero;
            for(int i=0;i<native.Length;i++)
            {
                var row=new Vector3(native[i].x,native[i].y,1);for(int a=0;a<3;a++)for(int b=0;b<3;b++)normal[a,b]+=row[a]*row[b];rhsU+=row*targetU[i];rhsV+=row*uv[i].y;
            }
            var fitU=normal.inverse.MultiplyVector(rhsU);var fitV=normal.inverse.MultiplyVector(rhsV);Debug.Log("NATIVE LACE AFFINE: determinant="+normal.determinant+" U="+fitU+" V="+fitV);var skinUV=body.uv2;var lower=body.uv3;
            for(int i=0;i<points.Length;i++)
            {
                var row=new Vector3(skinUV[i].x,skinUV[i].y,1);var natural=new Vector2(Vector3.Dot(row,fitU),Vector3.Dot(row,fitV));
                float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.035f,lower[i].y-points[i].y));
                if(float.IsNaN(natural.x)||float.IsInfinity(natural.x)||float.IsNaN(natural.y)||float.IsInfinity(natural.y))throw new Exception("Invalid native lace UV fit");
                if(blend<1)natural.x=mapped[i].x+Mathf.DeltaAngle(0,(natural.x-mapped[i].x)*120)/120;
                mapped[i]=Vector2.Lerp(mapped[i],natural,blend);
            }
            var triangles=body.triangles;var vertices=body.vertices.ToList();var normals=body.normals.ToList();var weights=body.boneWeights.ToList();var skin=body.uv2.ToList();var boundary=body.uv3.ToList();var pattern=mapped.ToList();
            var duplicates=new Dictionary<long,int>();
            for(int face=0;face<triangles.Length;face+=3)
            {
                float anchor=mapped[triangles[face]].x;
                for(int corner=0;corner<3;corner++)
                {
                    int id=triangles[face+corner];float adjusted=mapped[id].x;int turns=0;
                    turns=-Mathf.RoundToInt((adjusted-anchor)/3);adjusted+=turns*3;
                    if(turns==0)continue;long key=((long)id<<32)|(uint)turns;
                    if(!duplicates.TryGetValue(key,out int duplicate))
                    {
                        duplicate=vertices.Count;duplicates.Add(key,duplicate);vertices.Add(vertices[id]);normals.Add(normals[id]);weights.Add(weights[id]);skin.Add(skin[id]);boundary.Add(boundary[id]);pattern.Add(new Vector2(adjusted,mapped[id].y));
                    }
                    triangles[face+corner]=duplicate;
                }
            }
            body.vertices=vertices.ToArray();body.normals=normals.ToArray();body.boneWeights=weights.ToArray();body.uv2=skin.ToArray();body.uv3=boundary.ToArray();body.uv=pattern.ToArray();body.triangles=triangles;body.RecalculateBounds();body.RecalculateTangents();EditorUtility.SetDirty(body);
            Debug.Log("ORIGINAL LACE CONTINUATION: native lower edge phase, UV slope="+slope+"; complete source leg / foot geometry retained.");
        }
        static Vector3[] LaceBottomContour(Mesh mesh)
        {
            var vertices=mesh.vertices;var triangles=mesh.triangles;var welded=new Dictionary<Vector3Int,int>();var positions=new List<Vector3>();var ids=new int[vertices.Length];
            for(int i=0;i<vertices.Length;i++){var key=Vector3Int.RoundToInt(vertices[i]*100000);if(!welded.TryGetValue(key,out int id)){id=positions.Count;welded.Add(key,id);positions.Add(vertices[i]);}ids[i]=id;}
            var edges=new Dictionary<long,int>();
            for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++){int a=ids[triangles[i+j]],b=ids[triangles[i+(j+1)%3]];if(a>b){int t=a;a=b;b=t;}long key=((long)a<<32)|(uint)b;edges.TryGetValue(key,out int count);edges[key]=count+1;}
            var lower=new HashSet<int>();float middle=mesh.bounds.center.y;
            foreach(var edge in edges.Where(e=>e.Value==1)){int a=(int)(edge.Key>>32),b=(int)edge.Key;if(positions[a].y<middle&&positions[b].y<middle){lower.Add(a);lower.Add(b);}}
            if(lower.Count<8)throw new Exception("Original lace lower contour is incomplete");
            return lower.Select(i=>positions[i]).OrderBy(p=>Mathf.Atan2(p.z-mesh.bounds.center.z,p.x-mesh.bounds.center.x)).ToArray();
        }
        static float CuffHeight(Vector3 point,Vector3 center,Vector3[] contour)
        {
            float angle=Mathf.Atan2(point.z-center.z,point.x-center.x);
            for(int i=0;i<contour.Length;i++)
            {
                var a=contour[i];var b=contour[(i+1)%contour.Length];float left=Mathf.Atan2(a.z-center.z,a.x-center.x),right=Mathf.Atan2(b.z-center.z,b.x-center.x),value=angle;
                if(i==contour.Length-1){right+=2*Mathf.PI;if(value<left)value+=2*Mathf.PI;}
                if(value>=left&&value<=right)return Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(left,right,value));
            }
            return contour.Max(p=>p.y);
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c,out Vector3 bary)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0){bary=new Vector3(1,0,0);return a;}
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3){bary=new Vector3(0,1,0);return b;}
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0){float v=d1/(d1-d3);bary=new Vector3(1-v,v,0);return a+v*ab;}
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6){bary=new Vector3(0,0,1);return c;}
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0){float w=d2/(d2-d6);bary=new Vector3(1-w,0,w);return a+w*ac;}
            float va=d3*d6-d5*d4;if(va<=0&&(d4-d3)>=0&&(d5-d6)>=0){float w=(d4-d3)/((d4-d3)+(d5-d6));bary=new Vector3(0,1-w,w);return b+w*(c-b);}
            float inverse=1/(va+vb+vc);float v2=vb*inverse,w2=vc*inverse;bary=new Vector3(1-v2-w2,v2,w2);return a+ab*v2+ac*w2;
        }
        public static void ApplyAndReview()
        {
            string path="Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{Build(root.GetComponent<CharacterRig>(),Folder);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Review();
        }
        public static void FinalReview(){ApplyAndReview();BlenderModelBridge.ExportAll();HomecomingContinuousReview.Run();}
        public static void ReferenceReview(){ApplyAndReview();CaptureInCostume();BlenderModelBridge.ExportAll();}
        public static void CaptureInCostume()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;var scene=typeof(HomecomingScene);scene.GetField("started",flags).SetValue(story,true);
            var rig=story.GetComponentInChildren<CharacterRig>();var camera=story.CameraControls.GetComponent<Camera>();
            for(int frame=0;frame<=870;frame++)
            {
                scene.GetField("elapsed",flags).SetValue(story,frame/30f-Time.deltaTime);scene.GetMethod("Update",flags).Invoke(story,null);
                if(!new[]{240,330,675,870}.Contains(frame))continue;
                var target=rig.transform.TransformPoint(new Vector3(0,.66f,-.05f));camera.transform.position=target+new Vector3(.25f,.14f,-1.3f);camera.transform.LookAt(target);camera.fieldOfView=40;
                HomecomingSetup.Capture(camera,"Artifacts/YenneferSheerHosiery/reference-costume-"+frame+".png",1100,1000);
            }
        }
        public static void Review()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;var rig=story.GetComponentInChildren<CharacterRig>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;var scene=typeof(HomecomingScene);
            scene.GetField("started",flags).SetValue(story,true);var motion=(HomecomingMotion)scene.GetField("motion",flags).GetValue(story);
            var lace=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name.StartsWith("YenneferOriginalLace")).ToArray();
            if(lace.Length!=2||lace.Sum(s=>s.sharedMesh.vertexCount)!=266)throw new Exception("Original Yennefer lace is incomplete");
            Directory.CreateDirectory("Artifacts/YenneferSheerHosiery");
            var camera=story.CameraControls.GetComponent<Camera>();
            var gaps=new System.Text.StringBuilder("frame,side,maxLaceToSourceSkinGap\n");
            for(int frame=0;frame<=1170;frame++)
            {
                float time=frame/30f;
                scene.GetField("elapsed",flags).SetValue(story,time>=32?32:time-Time.deltaTime);scene.GetField("idleElapsed",flags).SetValue(story,time>=32?time-32-Time.deltaTime:0);
                scene.GetMethod("Update",flags).Invoke(story,null);
                if(!new[]{240,330,390,600,675,870,990,1170}.Contains(frame))continue;
                for(int side=0;side<2;side++)
                {
                    var body=new Mesh();var band=new Mesh();rig.anatomicalLegs[side].BakeMesh(body);lace.Single(s=>s.name.EndsWith(side==0?"Left":"Right")).BakeMesh(band);
                    var positions=body.vertices;var triangles=body.triangles;float maximum=0;
                    foreach(var point in band.vertices)
                    {
                        float nearest=float.MaxValue;
                        for(int i=0;i<triangles.Length;i+=3){var q=Closest(point,positions[triangles[i]],positions[triangles[i+1]],positions[triangles[i+2]],out var unused);nearest=Mathf.Min(nearest,(point-q).sqrMagnitude);}
                        maximum=Mathf.Max(maximum,Mathf.Sqrt(nearest));
                    }
                    gaps.AppendLine(frame+","+side+","+maximum);UnityEngine.Object.DestroyImmediate(body);UnityEngine.Object.DestroyImmediate(band);
                    if(maximum>.015f)throw new Exception("Lace separated from the source thigh during motion: "+frame+" side="+side+" gap="+maximum);
                }
                foreach(var sock in rig.hosiery){var properties=new MaterialPropertyBlock();sock.GetPropertyBlock(properties);if(time>=20.1f&&properties.GetFloat("_BootMask")!=0)throw new Exception("Boot still masks released sheer foot");}
                var target=rig.transform.TransformPoint(new Vector3(0,.46f,-.04f));camera.transform.position=target+new Vector3(.65f,.16f,-1.35f);camera.transform.LookAt(target);camera.fieldOfView=40;
                HomecomingSetup.Capture(camera,"Artifacts/YenneferSheerHosiery/"+frame+".png",1000,1000);
            }
            // Isolate socks for joining, toe and heel inspection, outside the skirt.
            motion.Sample(27);foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())skin.enabled=rig.hosiery.Contains(skin)||lace.Contains(skin);
            var aim=rig.transform.TransformPoint(new Vector3(0,.42f,-.02f));camera.transform.position=aim+new Vector3(.35f,.08f,-1.55f);camera.transform.LookAt(aim);camera.fieldOfView=36;
            foreach(int denier in new[]{10,20,30})
            {
                foreach(var material in rig.hosiery.Select(s=>s.sharedMaterial).Concat(lace.Select(s=>s.sharedMaterial)).Distinct())material.SetFloat("_Denier",denier);
                HomecomingSetup.Capture(camera,"Artifacts/YenneferSheerHosiery/"+denier+"D.png",1000,1000);
            }
            foreach(var material in rig.hosiery.Select(s=>s.sharedMaterial).Concat(lace.Select(s=>s.sharedMaterial)).Distinct())material.SetFloat("_Denier",20);
            AssetDatabase.SaveAssets();HomecomingFootwearReview.Run();
            File.WriteAllText("Artifacts/YenneferSheerHosiery/lace-fit.csv",gaps.ToString());
            Debug.Log("YENNEFER SHEER HOSIERY REVIEW PASSED: original 266-vertex lace/UVs, continuous source feet, 10/20/30D previews and retained foot coverage.");
        }
    }
}
