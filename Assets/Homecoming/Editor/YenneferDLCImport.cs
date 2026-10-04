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
                if(surface.name=="DLC_UnderBootStockings"||surface.name=="body_05_wa__yennefer_Material6")
                {
                    material.SetTexture("_BaseMap",null);material.SetTexture("_BumpMap",null);
                    material.DisableKeyword("_NORMALMAP");material.color=new Color(.018f,.018f,.018f);material.SetFloat("_Smoothness",.18f);
                }
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
                var part=data.parts[i];
                // Legacy exports contained cylindrical replacement stockings.
                // Use the original game leg/foot surface built below instead.
                if(part.name.StartsWith("DLC_Stocking_"))continue;
                var mesh=ReadMesh(part);
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
            foreach(var shoe in new[]{rig.leftShoe,rig.rightShoe})
            {
                var skin=shoe.GetComponent<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;var v=mesh.vertices;
                var foot=shoe==rig.leftShoe?Bone("leftFoot"):Bone("rightFoot");
                var calf=shoe==rig.leftShoe?Bone("leftLowerLeg"):Bone("rightLowerLeg");
                var ankle=shoe.transform.InverseTransformPoint(foot.position);var knee=shoe.transform.InverseTransformPoint(calf.position);
                float[] levels={.16f,.20f,.25f,.30f,.35f,.40f,.45f,.50f,.54f};var centers=new Vector3[levels.Length];
                for(int ring=0;ring<levels.Length;ring++)
                {
                    var band=v.Where(p=>Mathf.Abs(p.y-levels[ring])<.027f).ToArray();
                    centers[ring]=new Vector3((band.Min(p=>p.x)+band.Max(p=>p.x))*.5f,levels[ring],(band.Min(p=>p.z)+band.Max(p=>p.z))*.5f);
                }
                var w=new BoneWeight[v.Length];int bone=Array.IndexOf(skin.bones,foot);
                for(int i=0;i<v.Length;i++)
                {
                    float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.22f,v[i].y));
                    var axis=Vector3.LerpUnclamped(ankle,knee,(v[i].y-ankle.y)/(knee.y-ankle.y));
                    var center=centers[0];for(int ring=1;ring<levels.Length;ring++)if(v[i].y>=levels[ring-1])center=Vector3.Lerp(centers[ring-1],centers[ring],Mathf.InverseLerp(levels[ring-1],levels[ring],v[i].y));
                    v[i].x+=blend*(axis.x-center.x);v[i].z+=blend*(axis.z-center.z);
                    v[i].x=axis.x+(v[i].x-axis.x)*(1+.25f*blend);v[i].z=axis.z+(v[i].z-axis.z)*(1+.25f*blend);
                    if(v[i].y>.12f)v[i].y=.12f+(v[i].y-.12f)*.833333f;
                    w[i]=new BoneWeight{boneIndex0=bone,weight0=1};
                }
                mesh.vertices=v;mesh.boneWeights=w;mesh.RecalculateBounds();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);
            }
            YenneferLowerBody.Build(rig);
            rig.footwearKind=CharacterRig.FootwearKind.TallBoot;
            if(rig.skirt)
            {
                var mesh=rig.skirt.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var weights=new BoneWeight[vertices.Length];var delta=new Vector3[vertices.Length];var deltaNormals=new Vector3[vertices.Length];
                int hip=Array.IndexOf(rig.skirt.bones,rig.hips);
                for(int i=0;i<vertices.Length;i++)
                {
                    weights[i]=new BoneWeight{boneIndex0=hip,weight0=1};
                    float down=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.93f,.54f,vertices[i].y));
                    float fold=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.93f,.85f,vertices[i].y));
                    var bend=Quaternion.Euler((vertices[i].z>.10f?-70:60)*fold,0,0);var pivot=new Vector3(0,.905f,0);
                    delta[i]=pivot+bend*(vertices[i]-pivot)-vertices[i];delta[i].x+=vertices[i].x*.08f*down;
                    deltaNormals[i]=bend*normals[i]-normals[i];
                }
                mesh.boneWeights=weights;mesh.AddBlendShapeFrame("Sitting",100,delta,deltaNormals,null);EditorUtility.SetDirty(mesh);
            }
            BuildAvatar(root,data,nodes);
            // The prepared source uses a normalized T pose. Capture a relaxed pose for the scene.
            rig.leftArm.localRotation=Quaternion.Euler(0,0,78); rig.rightArm.localRotation=Quaternion.Euler(0,0,-78);
            foreach(var human in data.humanBones)
                if(human.bone.Contains("Proximal") && !human.bone.Contains("Thumb")) nodes[human.node].localRotation=Quaternion.Euler(0,human.bone.StartsWith("left")?-12:12,0);
            rig.leftGroundShoe=GroundShoe(rig.leftShoe,root.transform,"LeftGroundShoe"); rig.rightGroundShoe=GroundShoe(rig.rightShoe,root.transform,"RightGroundShoe");
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
        internal static void CopyMesh(Mesh source,Mesh target)
        {
            target.Clear(); target.ClearBlendShapes(); target.indexFormat=source.indexFormat; target.vertices=source.vertices; target.normals=source.normals; target.uv=source.uv; target.triangles=source.triangles; target.boneWeights=source.boneWeights; target.bindposes=source.bindposes; target.bounds=source.bounds; target.name=source.name;
            for(int i=0;i<source.blendShapeCount;i++) { var v=new Vector3[source.vertexCount]; var n=new Vector3[source.vertexCount]; var t=new Vector3[source.vertexCount]; source.GetBlendShapeFrameVertices(i,0,v,n,t); target.AddBlendShapeFrame(source.GetBlendShapeName(i),100,v,n,t); }
            target.RecalculateTangents(); target.UploadMeshData(false); EditorUtility.SetDirty(target);
        }
        static Vector3 ReadVector(BinaryReader input)=>new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());
        static Vector3 V(float[] values)=>new Vector3(values[0],values[1],values[2]);
    }
}
