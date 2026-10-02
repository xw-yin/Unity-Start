using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferDLCImport
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Destination="Assets/ThirdParty/WitcherYenneferDLC/Unity/";
        [Serializable] class Manifest { public Node[] nodes; public Surface[] materials; public Part[] parts; public Human[] humanBones; }
        [Serializable] class Node { public string name; public int parent; public float[] position,rotation,scale; }
        [Serializable] class Surface { public string name,alpha; public int texture,normal; public float[] color; public float cutoff; public bool doubleSided; }
        [Serializable] class Part { public string name,file,role; public int node,material; public int[] joints; public string[] expressions; }
        [Serializable] class Human { public string bone; public int node; }

        public static void ImportAndBuild()
        {
            Import(); HomecomingWalkBake.Bake(); HomecomingSetup.ValidateAndCapture();
        }
        [MenuItem("Homecoming/导入叶奈法DLC服装")]
        public static void Import()
        {
            Directory.CreateDirectory(Destination+"Meshes"); Directory.CreateDirectory(Destination+"Materials");
            AssetDatabase.Refresh();
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            foreach(string path in Directory.GetFiles(Source,"Texture_*.png"))
            {
                var importer=AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;
                if(importer) { importer.textureType=TextureImporterType.Default; importer.alphaIsTransparency=true; importer.sRGBTexture=true; importer.maxTextureSize=2048; importer.textureCompression=TextureImporterCompression.Compressed; importer.SaveAndReimport(); }
            }
            var normalTextures=data.materials.Where(m=>m.normal>=0).Select(m=>m.normal).Distinct();
            foreach(int i in normalTextures) { var importer=(TextureImporter)AssetImporter.GetAtPath(Source+"Texture_"+i.ToString("D2")+".png"); importer.textureType=TextureImporterType.NormalMap; importer.sRGBTexture=false; importer.SaveAndReimport(); }
            var materials=new Material[data.materials.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var surface=data.materials[i]; string path=Destination+"Materials/Surface_"+i.ToString("D2")+".mat";
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=surface.name};
                material.color=new Color(surface.color[0],surface.color[1],surface.color[2],surface.color[3]);
                if(surface.texture>=0) material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Texture_"+surface.texture.ToString("D2")+".png"));
                material.SetFloat("_Cull",surface.doubleSided?0:2); material.SetFloat("_Smoothness",.28f);
                material.SetFloat("_BumpScale",surface.name.StartsWith("h_01_")?.35f:.7f);
                if(surface.normal>=0) { material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Texture_"+surface.normal.ToString("D2")+".png")); material.EnableKeyword("_NORMALMAP"); }
                if(surface.alpha=="MASK") {material.SetFloat("_AlphaClip",1);material.EnableKeyword("_ALPHATEST_ON");}
                material.SetFloat("_Cutoff",surface.alpha=="MASK"?surface.cutoff:surface.alpha=="BLEND"?.005f:0);
                if(surface.alpha=="BLEND") { material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10); material.SetFloat("_ZWrite",0); material.renderQueue=3000; }
                else material.renderQueue=surface.alpha=="MASK"?2450:2000;
                var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(existing) { existing.shader=material.shader; existing.CopyPropertiesFromMaterial(material); EditorUtility.SetDirty(existing); materials[i]=existing; UnityEngine.Object.DestroyImmediate(material); }
                else { AssetDatabase.CreateAsset(material,path); materials[i]=material; }
            }
            var root=new GameObject("叶奈法 · DLC服装");
            var coordinates=new GameObject("VRM坐标系").transform; coordinates.SetParent(root.transform,false); coordinates.localRotation=Quaternion.Euler(0,180,0);
            var nodes=new Transform[data.nodes.Length];
            for(int i=0;i<nodes.Length;i++)
            {
                nodes[i]=new GameObject(data.nodes[i].name).transform;
                nodes[i].SetParent(coordinates,false);
            }
            for(int i=0;i<nodes.Length;i++)
            {
                var node=data.nodes[i]; nodes[i].SetParent(node.parent<0?coordinates:nodes[node.parent],false);
                nodes[i].localPosition=V(node.position); nodes[i].localRotation=new Quaternion(node.rotation[0],node.rotation[1],node.rotation[2],node.rotation[3]); nodes[i].localScale=V(node.scale);
            }
            var rig=root.AddComponent<CharacterRig>(); rig.importedSource=true; rig.longBoots=true;
            Transform Bone(string name)=>nodes[data.humanBones.First(x=>x.bone==name).node];
            rig.hips=Bone("hips"); rig.spine=Bone("spine"); rig.head=Bone("head");
            rig.leftArm=Bone("leftUpperArm"); rig.rightArm=Bone("rightUpperArm");
            rig.leftElbow=Bone("leftLowerArm"); rig.rightElbow=Bone("rightLowerArm");
            rig.leftThigh=Bone("leftUpperLeg"); rig.rightThigh=Bone("rightUpperLeg");
            rig.leftCalf=Bone("leftLowerLeg"); rig.rightCalf=Bone("rightLowerLeg");
            var expressions=new List<SkinnedMeshRenderer>();
            for(int i=0;i<data.parts.Length;i++)
            {
                var part=data.parts[i]; var mesh=ReadMesh(part);
                string meshPath=Destination+"Meshes/Part_"+i.ToString("D2")+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(existing) { CopyMesh(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
                else AssetDatabase.CreateAsset(mesh,meshPath);
                var obj=new GameObject(part.name); obj.transform.SetParent(nodes[part.node],false);
                var renderer=obj.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh=mesh;
                renderer.bones=part.joints.Select(index=>nodes[index]).ToArray(); renderer.rootBone=rig.hips; renderer.updateWhenOffscreen=true;
                renderer.localBounds=new Bounds(new Vector3(0,.95f,0),new Vector3(2.6f,2.6f,2.6f));
                ModelMesh.ApplyMaterial(renderer,materials[part.material]);
                if(part.expressions.Length>0) expressions.Add(renderer);
                if(part.role=="ShoeLeft") rig.leftShoe=obj;
                if(part.role=="ShoeRight") rig.rightShoe=obj;
                if(part.name.StartsWith("dress_05_")) rig.skirt=renderer;
            }
            rig.expressionRenderers=expressions.ToArray();
            BuildAvatar(root,data,nodes);
            // The prepared source uses a normalized T pose. Capture a relaxed pose for the scene.
            rig.leftArm.localRotation=Quaternion.Euler(0,0,78); rig.rightArm.localRotation=Quaternion.Euler(0,0,-78);
            foreach(var human in data.humanBones)
                if(human.bone.Contains("Proximal") && !human.bone.Contains("Thumb")) nodes[human.node].localRotation=Quaternion.Euler(0,human.bone.StartsWith("left")?-12:12,0);
            Material Palette(string name,Color color)
            {
                var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name}; material.color=color; material.SetFloat("_Smoothness",.25f); return material;
            }
            var footwear=new XiaModelBuilder(Palette("Skin",new Color(.94f,.70f,.54f)),Palette("Hair",new Color(.14f,.065f,.04f)),Palette("Blouse",Color.white),Palette("Skirt",new Color(.12f,.25f,.22f)),Palette("Leather",new Color(.20f,.09f,.055f)),Palette("HomeSlipper",new Color(.85f,.46f,.40f)));
            var slipperMaterial=Palette("叶奈法家居拖鞋",new Color(.85f,.46f,.40f));
            rig.leftSlipper=footwear.BuildFootwear("左家居拖鞋",new Vector3(0,-.065f,.045f),slipperMaterial,Bone("leftFoot"),true);
            rig.rightSlipper=footwear.BuildFootwear("右家居拖鞋",new Vector3(0,-.065f,.045f),slipperMaterial,Bone("rightFoot"),true);
            rig.leftSlipper.transform.localRotation=rig.rightSlipper.transform.localRotation=Quaternion.Euler(0,180,0);
            var old=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Homecoming/Resources/Homecoming/Xia.prefab");
            var originalRig=old.GetComponent<CharacterRig>();
            rig.bag=UnityEngine.Object.Instantiate(originalRig.bag);
            rig.bag.name="通勤手提包"; rig.bag.transform.position=Bone("rightHand").position+new Vector3(.015f,-.22f,.015f); rig.bag.transform.rotation=Quaternion.identity; rig.bag.transform.SetParent(Bone("rightHand"),true); rig.bag.SetActive(true);
            var sockMaterial=Palette("叶奈法黑色短袜",new Color(.025f,.025f,.033f));
            foreach(var sourceShoe in new[]{rig.leftShoe,rig.rightShoe})
            {
                var sourceSkin=sourceShoe.GetComponent<SkinnedMeshRenderer>();
                var sockMesh=UnityEngine.Object.Instantiate(sourceSkin.sharedMesh); sockMesh.name=sourceShoe.name+"_InnerSock";
                var v=sockMesh.vertices; var center=sockMesh.bounds.center;
                var ankle=sourceShoe==rig.leftShoe?Bone("leftFoot"):Bone("rightFoot");
                var ankleLocal=sourceShoe.transform.InverseTransformPoint(ankle.position);
                var calf=sourceShoe==rig.leftShoe?Bone("leftLowerLeg"):Bone("rightLowerLeg");
                var calfLocal=sourceShoe.transform.InverseTransformPoint(calf.position);
                var weights=sockMesh.boneWeights;
                int footIndex=Array.IndexOf(sourceSkin.bones,ankle),calfIndex=Array.IndexOf(sourceSkin.bones,calf);
                for(int i=0;i<v.Length;i++) {
                    var p=v[i]; float leg=Mathf.Clamp01((p.y-ankleLocal.y)/.13f);
                    var axis=Vector3.Lerp(ankleLocal,calfLocal,Mathf.Clamp01((p.y-ankleLocal.y)/(calfLocal.y-ankleLocal.y)));
                    p.x=Mathf.Lerp(center.x+(p.x-center.x)*.82f,axis.x+(p.x-axis.x)*.80f,leg);
                    p.z=Mathf.Lerp(center.z+(p.z-center.z)*.88f,axis.z+(p.z-axis.z)*.80f,leg);
                    if(p.y<.065f&&p.z<ankleLocal.z) { p.y=.025f;p.z=Mathf.Max(p.z,ankleLocal.z-.035f); }
                    p.y=Mathf.Max(.015f,p.y);v[i]=p;
                    weights[i]=new BoneWeight{boneIndex0=footIndex,weight0=1-leg,boneIndex1=calfIndex,weight1=leg};
                }
                sockMesh.boneWeights=weights;
                sockMesh.vertices=v;
                var footTriangles=new List<int>();var originalTriangles=sockMesh.triangles;
                for(int t=0;t<originalTriangles.Length;t+=3)if(Mathf.Max(v[originalTriangles[t]].y,Mathf.Max(v[originalTriangles[t+1]].y,v[originalTriangles[t+2]].y))<.17f){footTriangles.Add(originalTriangles[t]);footTriangles.Add(originalTriangles[t+1]);footTriangles.Add(originalTriangles[t+2]);}
                sockMesh.triangles=footTriangles.ToArray();sockMesh.RecalculateBounds();
                string sockPath=Destination+"Meshes/"+sockMesh.name+".asset";var savedSock=AssetDatabase.LoadAssetAtPath<Mesh>(sockPath);
                if(savedSock){CopyMesh(sockMesh,savedSock);UnityEngine.Object.DestroyImmediate(sockMesh);sockMesh=savedSock;}else AssetDatabase.CreateAsset(sockMesh,sockPath);
                var sock=new GameObject(sourceShoe==rig.leftShoe?"左脚短袜":"右脚短袜");sock.transform.SetParent(sourceShoe.transform.parent,false);
                var renderer=sock.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=sockMesh;renderer.bones=sourceSkin.bones;renderer.rootBone=sourceSkin.rootBone;renderer.updateWhenOffscreen=true;renderer.sharedMaterial=sockMaterial;
            }
            rig.leftGroundShoe=GroundShoe(rig.leftShoe,root.transform,"LeftGroundShoe"); rig.rightGroundShoe=GroundShoe(rig.rightShoe,root.transform,"RightGroundShoe");
            rig.leftSlipper.SetActive(false); rig.rightSlipper.SetActive(false);
            // Persist accessory meshes/materials; all author-provided assets remain in ThirdParty.
            int accessory=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!AssetDatabase.Contains(filter.sharedMesh)) { string path=Destination+"Meshes/Accessory_"+(accessory++).ToString("D2")+".asset"; var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(mesh) { CopyMesh(filter.sharedMesh,mesh); filter.sharedMesh=mesh; } else AssetDatabase.CreateAsset(filter.sharedMesh,path); }
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                if(!AssetDatabase.Contains(renderer.sharedMaterial))
                {
                    string path=Destination+"Materials/"+renderer.sharedMaterial.name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material) { material.CopyPropertiesFromMaterial(renderer.sharedMaterial); EditorUtility.SetDirty(material); renderer.sharedMaterial=material; }
                    else AssetDatabase.CreateAsset(renderer.sharedMaterial,path);
                }
            rig.CaptureRestPose(); rig.Expression(1,false);
            
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab");
            AssetDatabase.SaveAssets(); UnityEngine.Object.DestroyImmediate(root); AssetDatabase.Refresh();
            Debug.Log("YENNEFER IMPORT PASSED: travel outfit, original mesh, UVs, textures, humanoid rig and separated footwear.");
        }
        static void BuildAvatar(GameObject root,Manifest data,Transform[] nodes)
        {
            var human=new List<HumanBone>();
            foreach(var bone in data.humanBones)
            {
                string humanName=HumanTrait.BoneName.FirstOrDefault(name=>name.Replace(" ","").Equals(bone.bone,StringComparison.OrdinalIgnoreCase));
                if(humanName!=null) human.Add(new HumanBone{boneName=nodes[bone.node].name,humanName=humanName,limit=new HumanLimit{useDefaultValues=true}});
            }
            var skeleton=root.GetComponentsInChildren<Transform>().Select(bone=>new SkeletonBone{name=bone.name,position=bone.localPosition,rotation=bone.localRotation,scale=bone.localScale}).ToArray();
            var avatar=AvatarBuilder.BuildHumanAvatar(root,new HumanDescription{human=human.ToArray(),skeleton=skeleton,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0,hasTranslationDoF=false});
            if(!avatar.isValid||!avatar.isHuman) throw new Exception("Yennefer humanoid avatar is invalid");
            avatar.name="Yennefer Humanoid"; string path=Destination+"HumanoidAvatar.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if(existing) { EditorUtility.CopySerialized(avatar,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(avatar); avatar=existing; }
            else AssetDatabase.CreateAsset(avatar,path);
            var animator=root.AddComponent<Animator>(); animator.avatar=avatar; animator.enabled=false;
        }
        static GameObject GroundShoe(GameObject source,Transform root,string name)
        {
            var skin=source.GetComponent<SkinnedMeshRenderer>(); var mesh=new Mesh(); skin.BakeMesh(mesh); mesh.name=name;
            var vertices=mesh.vertices; var normals=mesh.normals;
            var matrix=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(vertices[0]),Vector3.zero);
            for(int i=0;i<vertices.Length;i++) { vertices[i]=matrix.MultiplyPoint3x4(vertices[i]); normals[i]=matrix.MultiplyVector(normals[i]).normalized; bounds.Encapsulate(vertices[i]); }
            var center=bounds.center; center.y=bounds.min.y;
            for(int i=0;i<vertices.Length;i++) vertices[i]-=center;
            mesh.vertices=vertices; mesh.normals=normals; mesh.RecalculateBounds();
            var obj=ModelMesh.Object(name,mesh,skin.sharedMaterial,root,Vector3.zero); obj.SetActive(false); return obj;
        }
        static Mesh ReadMesh(Part part)
        {
            using(var input=new BinaryReader(File.OpenRead(Source+part.file)))
            {
                int count=input.ReadInt32(),indices=input.ReadInt32(),bones=input.ReadInt32(),shapes=input.ReadInt32();
                var vertices=new Vector3[count]; var normals=new Vector3[count]; var uv=new Vector2[count]; var weights=new BoneWeight[count];
                for(int i=0;i<count;i++)
                {
                    vertices[i]=ReadVector(input); normals[i]=ReadVector(input); uv[i]=new Vector2(input.ReadSingle(),input.ReadSingle());
                    var weight=new BoneWeight{boneIndex0=input.ReadInt32(),boneIndex1=input.ReadInt32(),boneIndex2=input.ReadInt32(),boneIndex3=input.ReadInt32(),weight0=input.ReadSingle(),weight1=input.ReadSingle(),weight2=input.ReadSingle(),weight3=input.ReadSingle()};
                    float sum=weight.weight0+weight.weight1+weight.weight2+weight.weight3;
                    if(sum>.0001f) { weight.weight0/=sum; weight.weight1/=sum; weight.weight2/=sum; weight.weight3/=sum; } weights[i]=weight;
                }
                var triangles=new int[indices]; for(int i=0;i<indices;i++) triangles[i]=input.ReadInt32();
                var bindposes=new Matrix4x4[bones]; for(int i=0;i<bones;i++) for(int c=0;c<4;c++) for(int r=0;r<4;r++) bindposes[i][r,c]=input.ReadSingle();
                var mesh=new Mesh{name=part.name,vertices=vertices,normals=normals,uv=uv,triangles=triangles,boneWeights=weights,bindposes=bindposes};
                for(int shape=0;shape<shapes;shape++)
                {
                    var delta=new Vector3[count]; var deltaNormal=new Vector3[count];
                    for(int i=0;i<count;i++) { delta[i]=ReadVector(input); deltaNormal[i]=ReadVector(input); }
                    mesh.AddBlendShapeFrame(part.expressions[shape],100,delta,deltaNormal,null);
                }
                mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
            }
        }
        static void CopyMesh(Mesh source,Mesh target)
        {
            target.Clear(); target.ClearBlendShapes(); target.indexFormat=source.indexFormat; target.vertices=source.vertices; target.normals=source.normals; target.uv=source.uv; target.triangles=source.triangles; target.boneWeights=source.boneWeights; target.bindposes=source.bindposes; target.bounds=source.bounds; target.name=source.name;
            for(int i=0;i<source.blendShapeCount;i++) { var v=new Vector3[source.vertexCount]; var n=new Vector3[source.vertexCount]; var t=new Vector3[source.vertexCount]; source.GetBlendShapeFrameVertices(i,0,v,n,t); target.AddBlendShapeFrame(source.GetBlendShapeName(i),100,v,n,t); }
            target.RecalculateTangents(); target.UploadMeshData(false); EditorUtility.SetDirty(target);
        }
        static Vector3 ReadVector(BinaryReader input)=>new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());
        static Vector3 V(float[] values)=>new Vector3(values[0],values[1],values[2]);
    }
}
