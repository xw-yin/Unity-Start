using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class WitcherCharacterImport
    {
        static string Source;
        static string Destination;
        static string CharacterName;
        [Serializable] class Manifest { public Node[] nodes; public Surface[] materials; public Part[] parts; public Human[] humanBones; }
        [Serializable] class Node { public string name; public int parent; public float[] position,rotation,scale; }
        [Serializable] class Surface { public string name,alpha; public int texture,normal; public float[] color; public float cutoff; public bool doubleSided; }
        [Serializable] class Part { public string name,file,role; public int node,material; public int[] joints; public string[] expressions; public float[] cloth; }
        [Serializable] class Human { public string bone; public int node; }

        [MenuItem("Homecoming/导入巫师可替换人物")]
        public static void ImportAll()
        {
            foreach(var name in new[]{"Shani","Triss","Ciri","Iris","Syanna","AnnaHenrietta"}) Import(name);
        }
        public static void Import(string name)
        {
            CharacterName=name; Source="Assets/ThirdParty/Witcher"+name+"/Prepared/";
            Destination="Assets/ThirdParty/Witcher"+name+"/Unity/";
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
                if(surface.name.StartsWith("c_01_") || surface.name.StartsWith("c_05_"))
                    material.color=CharacterName=="Ciri"?new Color(.95f,.96f,.94f):CharacterName=="Iris"||CharacterName=="Syanna"?new Color(.12f,.095f,.075f):CharacterName=="Triss"?new Color(.92f,.25f,.075f):new Color(.85f,.36f,.13f);
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
            var root=new GameObject(CharacterName+" · 原游戏人物");
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
            foreach(var node in nodes)if(node.name=="W3_dress_helper")node.SetParent(rig.hips,true);
            var expressions=new List<SkinnedMeshRenderer>();
            var garments=new List<SkinnedMeshRenderer>();var fabricFreedom=new List<float>();
            var leftBoots=new List<SkinnedMeshRenderer>();var rightBoots=new List<SkinnedMeshRenderer>();
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
                if(part.role=="ShoeLeft") leftBoots.Add(renderer);
                if(part.role=="ShoeRight") rightBoots.Add(renderer);
                if(part.role=="Cloth"){garments.Add(renderer);fabricFreedom.AddRange(part.cloth);}
                if(part.role=="Body" && !rig.skirt) rig.skirt=renderer;
            }
            rig.expressionRenderers=expressions.ToArray();
            rig.leftShoe=MergeBoots(leftBoots,rig,"LeftBoot");
            rig.rightShoe=MergeBoots(rightBoots,rig,"RightBoot");
            if(garments.Count>0)
            {
                rig.skirt=MergeBoots(garments,rig,"OriginalGarments").GetComponent<SkinnedMeshRenderer>();
                if(CharacterName=="Iris" || CharacterName=="AnnaHenrietta")
                {
                    // The authored lap fold replaces thigh skinning; combining
                    // both would rotate the full skirt into the chest twice.
                    int hip=Array.IndexOf(rig.skirt.bones,rig.hips);
                    var garmentVertices=rig.skirt.sharedMesh.vertices;var garmentWeights=rig.skirt.sharedMesh.boneWeights;
                    for(int i=0;i<garmentWeights.Length;i++)
                    {
                        var w=garmentWeights[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var amounts=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                        bool detachedRoot=false;
                        for(int j=0;j<4;j++)if(amounts[j]>0&&(rig.skirt.bones[ids[j]].name.Contains("dress")||rig.skirt.bones[ids[j]].name=="W3_Root"))detachedRoot=true;
                        // Original dress helper bones sit under the game root;
                        // this scene's seated pelvis must carry their waist seam.
                        if(garmentVertices[i].y<.94f||detachedRoot)garmentWeights[i]=new BoneWeight{boneIndex0=hip,weight0=1};
                    }
                    rig.skirt.sharedMesh.boneWeights=garmentWeights;
                    BuildDressSittingShape(rig.skirt.sharedMesh);
                }
                rig.skirt.gameObject.AddComponent<HomecomingGarmentDynamics>().Configure(fabricFreedom.ToArray());
            }
            WitcherCharacterFeet.Build(rig,Destination);
            rig.longBoots=rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh.bounds.size.y>.30f;
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
            var slipperMaterial=Palette(CharacterName+"家居拖鞋",new Color(.85f,.46f,.40f));
            rig.leftSlipper=footwear.BuildFootwear("左家居拖鞋",new Vector3(0,-.065f,.045f),slipperMaterial,Bone("leftFoot"),true);
            rig.rightSlipper=footwear.BuildFootwear("右家居拖鞋",new Vector3(0,-.065f,.045f),slipperMaterial,Bone("rightFoot"),true);
            rig.leftSlipper.transform.localRotation=rig.rightSlipper.transform.localRotation=Quaternion.Euler(0,180,0);
            var old=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Homecoming/Resources/Homecoming/Xia.prefab");
            var originalRig=old.GetComponent<CharacterRig>();
            rig.bag=UnityEngine.Object.Instantiate(originalRig.bag);
            rig.bag.name="通勤手提包"; rig.bag.transform.position=Bone("rightHand").position+new Vector3(.015f,-.22f,.015f); rig.bag.transform.rotation=Quaternion.identity; rig.bag.transform.SetParent(Bone("rightHand"),true); rig.bag.SetActive(true);
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

            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Homecoming/Resources/Homecoming/"+CharacterName+".prefab");
            AssetDatabase.SaveAssets(); UnityEngine.Object.DestroyImmediate(root); AssetDatabase.Refresh();
            Debug.Log("CHARACTER IMPORT PASSED: "+CharacterName+"; source meshes, textures, humanoid rig, anatomical feet and removable boots.");
        }
        static void BuildDressSittingShape(Mesh mesh)
        {
            var v=mesh.vertices;var delta=new Vector3[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                var p=v[i];float free=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,.82f,p.y));
                if(free==0)continue;
                float front=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.12f,.16f,p.z));
                float frontY=p.y>.55f?.93f-(.93f-p.y)*.12f:.8844f-(.55f-p.y)*1.05f;
                float forward=p.y>.55f?(.93f-p.y)*1.15f:.437f+(.55f-p.y)*.16f;
                var seated=new Vector3(p.x*1.08f,Mathf.Lerp(p.y+.32f,frontY,front),p.z+Mathf.Lerp(-.10f,forward,front));
                delta[i]=(seated-p)*free;
            }
            var seatedVertices=v.Select((p,i)=>p+delta[i]).ToArray();
            var temporary=new Mesh{vertices=seatedVertices,triangles=mesh.triangles};temporary.RecalculateNormals();
            var seatedNormals=temporary.normals;var originalNormals=mesh.normals;
            mesh.AddBlendShapeFrame("Sitting",100,delta,seatedNormals.Select((n,i)=>n-originalNormals[i]).ToArray(),null);
            foreach(int side in new[]{0,1})
            {
                var knee=new Vector3[v.Length];
                for(int i=0;i<v.Length;i++)
                {
                    var p=v[i];float front=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.16f,p.z));
                    float lateral=Mathf.Exp(-Mathf.Pow((p.x+(side==0?.09f:-.09f))/.19f,2));
                    float fold=Mathf.Sin(Mathf.PI*Mathf.Clamp01((.94f-p.y)/.94f))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.94f,.82f,p.y));
                    float hem=Mathf.Pow(Mathf.Clamp01((.94f-p.y)/.94f),2);
                    // Gathering raises the hem toward the lap, rather than
                    // leaving the gripped edge out beyond arm's reach.
                    knee[i]=new Vector3(0,.12f*fold+.70f*hem,.025f*fold-.36f*hem)*front*lateral;
                }
                temporary.vertices=seatedVertices.Select((p,i)=>p+knee[i]).ToArray();temporary.RecalculateNormals();
                mesh.AddBlendShapeFrame(side==0?"BootLeft":"BootRight",100,knee,temporary.normals.Select((n,i)=>n-seatedNormals[i]).ToArray(),null);
            }
            UnityEngine.Object.DestroyImmediate(temporary);EditorUtility.SetDirty(mesh);
        }
        static GameObject MergeBoots(List<SkinnedMeshRenderer> parts,CharacterRig rig,string name)
        {
            if(parts.Count==0)throw new Exception(CharacterName+" has no removable "+name);
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var w=new List<BoneWeight>();var triangles=new List<int[]>();var materials=new List<Material>();
            foreach(var part in parts)
            {
                var source=part.sharedMesh;int offset=v.Count;
                v.AddRange(source.vertices);n.AddRange(source.normals);uv.AddRange(source.uv);w.AddRange(source.boneWeights);
                triangles.Add(source.triangles.Select(i=>i+offset).ToArray());materials.Add(part.sharedMaterial);
            }
            var mesh=new Mesh{name=name,vertices=v.ToArray(),normals=n.ToArray(),uv=uv.ToArray(),boneWeights=w.ToArray(),bindposes=parts[0].sharedMesh.bindposes,subMeshCount=triangles.Count};
            for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();
            string path=Destination+"Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){CopyMesh(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            var obj=new GameObject(name);obj.transform.SetParent(parts[0].transform.parent,false);var skin=obj.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh=mesh;skin.sharedMaterials=materials.ToArray();skin.bones=parts[0].bones;skin.rootBone=rig.hips;skin.updateWhenOffscreen=true;skin.localBounds=parts[0].localBounds;
            foreach(var part in parts)UnityEngine.Object.DestroyImmediate(part.gameObject);
            return obj;
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
            if(!avatar.isValid||!avatar.isHuman) throw new Exception("Character humanoid avatar is invalid");
            avatar.name=CharacterName+" Humanoid"; string path=Destination+"HumanoidAvatar.asset";
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
            var obj=ModelMesh.Object(name,mesh,skin.sharedMaterial,root,Vector3.zero); obj.GetComponent<Renderer>().sharedMaterials=skin.sharedMaterials;obj.SetActive(false); return obj;
        }
        internal static Mesh ReadPreparedMesh(string folder,string file,string name)
        {
            string previous=Source;Source=folder;
            try{return ReadMesh(new Part{name=name,file=file,expressions=Array.Empty<string>()});}
            finally{Source=previous;}
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
            target.Clear(); target.ClearBlendShapes(); target.indexFormat=source.indexFormat; target.vertices=source.vertices; target.normals=source.normals; target.uv=source.uv; target.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++)target.SetTriangles(source.GetTriangles(sub),sub);
            target.boneWeights=source.boneWeights; target.bindposes=source.bindposes; target.bounds=source.bounds; target.name=source.name;
            for(int i=0;i<source.blendShapeCount;i++) { var v=new Vector3[source.vertexCount]; var n=new Vector3[source.vertexCount]; var t=new Vector3[source.vertexCount]; source.GetBlendShapeFrameVertices(i,0,v,n,t); target.AddBlendShapeFrame(source.GetBlendShapeName(i),100,v,n,t); }
            target.RecalculateTangents(); target.UploadMeshData(false); EditorUtility.SetDirty(target);
        }
        static Vector3 ReadVector(BinaryReader input)=>new Vector3(input.ReadSingle(),input.ReadSingle(),input.ReadSingle());
        static Vector3 V(float[] values)=>new Vector3(values[0],values[1],values[2]);
    }
}
