using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming.Editor
{
    [InitializeOnLoad]
    public static class HomecomingSetup
    {
        const string ScenePath = "Assets/Homecoming/Scenes/Homecoming.unity";
        static HomecomingSetup() { EditorApplication.delayCall += EnsureScene; }
        static void EnsureScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || System.IO.File.Exists(ScenePath)) return;
            // Never replace an unsaved scene: the menu remains available in that case.
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) return;
            CreateScene();
        }
        [MenuItem("Homecoming/创建或重新生成归家场景")]
        public static void CreateScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            HomecomingMobileReview.SaveLightingProfile();
            System.IO.Directory.CreateDirectory("Assets/Homecoming/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("暮色归家 · Homecoming");
            root.AddComponent<HomecomingScene>().Build();
            System.IO.Directory.CreateDirectory("Assets/Homecoming/Materials");
            AssetDatabase.Refresh();
            var saved = new System.Collections.Generic.Dictionary<Material,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var source=renderer.sharedMaterial;
                if(!source) continue;
                if(AssetDatabase.Contains(source)) continue;
                var texture=source.GetTexture("_BaseMap") as Texture2D;
                if(texture && !AssetDatabase.Contains(texture))
                {
                    string texturePath="Assets/Homecoming/Materials/"+texture.name+".asset";
                    var existingTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if(existingTexture) { EditorUtility.CopySerialized(texture,existingTexture); EditorUtility.SetDirty(existingTexture); texture=existingTexture; }
                    else AssetDatabase.CreateAsset(texture,texturePath);
                    source.SetTexture("_BaseMap",texture);
                }
                if(!saved.TryGetValue(source,out var material))
                {
                    string path="Assets/Homecoming/Materials/"+source.name+".mat";
                    material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material) material.CopyPropertiesFromMaterial(source);
                    else { material=source; AssetDatabase.CreateAsset(material,path); }
                    EditorUtility.SetDirty(material);
                    saved.Add(source,material);
                }
                renderer.sharedMaterial=material;
            }
            System.IO.Directory.CreateDirectory("Assets/Homecoming/Models");
            System.IO.Directory.CreateDirectory("Assets/Homecoming/Resources/Homecoming");
            AssetDatabase.Refresh();
            var meshAssets=new System.Collections.Generic.Dictionary<Mesh,Mesh>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh && !AssetDatabase.Contains(filter.sharedMesh))
                {
                    var source=filter.sharedMesh;
                    if(!meshAssets.TryGetValue(source,out var mesh)) { mesh=SaveMesh(source,HierarchyPath(filter.transform)); meshAssets.Add(source,mesh); }
                    filter.sharedMesh=mesh;
                }
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(!AssetDatabase.Contains(renderer.sharedMesh)) renderer.sharedMesh=SaveMesh(renderer.sharedMesh,HierarchyPath(renderer.transform));
            var rig=root.GetComponentInChildren<CharacterRig>();
            Vector3 position=rig.transform.localPosition;
            rig.transform.localPosition=Vector3.zero;
            if(!rig.importedSource)
            {
                PrefabUtility.SaveAsPrefabAsset(rig.gameObject,"Assets/Homecoming/Resources/Homecoming/Xia.prefab");
                CharacterModelExport.Save(rig,"Assets/Homecoming/Exports");
            }
            rig.transform.localPosition=position;
            EditorSceneManager.SaveScene(scene,ScenePath);
            var existing=EditorBuildSettings.scenes;
            var scenes=new System.Collections.Generic.List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            foreach(var entry in existing) if(entry.path!=ScenePath) scenes.Add(entry);
            EditorBuildSettings.scenes=scenes.ToArray();
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1,1.5f),Quaternion.Euler(18,0,0),7);
            AssetDatabase.SaveAssets();
        }
        static string HierarchyPath(Transform item)
        {
            string path=item.name+"_"+item.GetSiblingIndex();
            while(item.parent) { item=item.parent; path=item.name+"_"+item.GetSiblingIndex()+"/"+path; }
            return path;
        }
        static Mesh SaveMesh(Mesh source,string identity)
        {
            string path="Assets/Homecoming/Models/Model_"+Hash128.Compute(identity)+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh)
            {
                mesh.Clear(); mesh.ClearBlendShapes(); mesh.indexFormat=source.indexFormat;
                mesh.vertices=source.vertices; mesh.normals=source.normals; mesh.uv=source.uv; mesh.triangles=source.triangles;
                mesh.boneWeights=source.boneWeights; mesh.bindposes=source.bindposes; mesh.bounds=source.bounds; mesh.name=source.name;
                for(int shape=0;shape<source.blendShapeCount;shape++) for(int frame=0;frame<source.GetBlendShapeFrameCount(shape);frame++)
                {
                    var vertices=new Vector3[source.vertexCount]; var normals=new Vector3[source.vertexCount]; var tangents=new Vector3[source.vertexCount];
                    source.GetBlendShapeFrameVertices(shape,frame,vertices,normals,tangents);
                    mesh.AddBlendShapeFrame(source.GetBlendShapeName(shape),source.GetBlendShapeFrameWeight(shape,frame),vertices,normals,tangents);
                }
                mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh);
            }
            else { mesh=source; AssetDatabase.CreateAsset(mesh,path); }
            return mesh;
        }
        public static void BuildBatch() { CreateScene(); }
        public static void ValidateAndCapture()
        {
            CreateScene();
            var story=Object.FindAnyObjectByType<HomecomingScene>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var type=typeof(HomecomingScene);
            type.GetField("started",flags).SetValue(story,true);
            foreach(float time in new[]{1f,3.999f,5.999f,7.5f,8.5f,25.6f,29f,32f})
            {
                type.GetField("elapsed",flags).SetValue(story,time);
                type.GetMethod("Update",flags).Invoke(story,null);
            }
            var slippers=(GameObject)type.GetField("slippers",flags).GetValue(story);
            if(slippers.activeSelf || story.transform.Find("脱下的通勤鞋")==null)
                throw new System.Exception("换鞋流程未完成");
            type.GetMethod("ResetStory",flags).Invoke(story,null);
            if(!slippers.activeSelf) throw new System.Exception("重播未恢复拖鞋");
            var rig=story.GetComponentInChildren<CharacterRig>();
            if(!rig || rig.skirt.sharedMesh.boneWeights.Length!=rig.skirt.sharedMesh.vertexCount)
                throw new System.Exception("角色蒙皮数据缺失");
            foreach(var weight in rig.skirt.sharedMesh.boneWeights)
                if(Mathf.Abs(weight.weight0+weight.weight1+weight.weight2+weight.weight3-1)>.001f) throw new System.Exception("裙子权重未归一化");
            if(!rig.importedSource && rig.skirt.sharedMesh.blendShapeCount==0) throw new System.Exception("裙子坐姿形态缺失");
            if(!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Homecoming/Resources/Homecoming/Xia.prefab")) throw new System.Exception("角色预制体缺失");
            var camera=story.GetComponentInChildren<Camera>();
            camera.transform.position=rig.transform.position+new Vector3(.8f,1.14f,-3.3f);
            camera.transform.LookAt(rig.transform.position+new Vector3(0,.92f,0)); camera.fieldOfView=38;
            if(rig.importedSource) { camera.transform.position=rig.transform.position+new Vector3(.5f,.95f,-2.8f); camera.transform.LookAt(rig.transform.position+new Vector3(0,.83f,0)); }
            var hidden=new System.Collections.Generic.List<Renderer>();
            foreach(var renderer in story.GetComponentsInChildren<Renderer>())
                if(!renderer.transform.IsChildOf(rig.transform) && renderer.enabled) { hidden.Add(renderer); renderer.enabled=false; }
            var background=camera.backgroundColor; camera.backgroundColor=new Color(.13f,.16f,.20f);
            Capture(camera,"Artifacts/Xia-model-preview.png",800,1000);
            camera.transform.position=rig.transform.position+new Vector3(.25f,1.68f,-.85f);
            camera.transform.LookAt(rig.head.position+new Vector3(0,-.025f,0)); camera.fieldOfView=35;
            if(rig.importedSource) { camera.transform.position=rig.head.position+new Vector3(.18f,.10f,-.78f); camera.transform.LookAt(rig.head.position+new Vector3(0,.08f,0)); }
            Capture(camera,"Artifacts/Xia-face-preview.png");
            foreach(var renderer in hidden) renderer.enabled=true;
            camera.backgroundColor=background;
            camera.fieldOfView=44;
            type.GetField("started",flags).SetValue(story,true);
            foreach(float time in new[]{8.999f,13.999f,16.8f}) { type.GetField("elapsed",flags).SetValue(story,time); type.GetMethod("Update",flags).Invoke(story,null); }
            Capture(camera,"Artifacts/Homecoming-preview.png");
            camera.transform.position=rig.transform.position+new Vector3(.95f,1.02f,-2.6f);
            camera.transform.LookAt(rig.hips.position+new Vector3(0,.05f,-.18f)); camera.fieldOfView=40;
            Capture(camera,"Artifacts/Xia-seated-preview.png");
            Debug.Log("HOMECOMING VALIDATION PASSED: modeled character, skin weights, sequence, shoe swap, replay, camera renders.");
        }
        internal static void Capture(Camera camera,string path,int width=1280,int height=720)
        {
            // Editor pose sampling does not run Unity's LateUpdate skinning pass. Bake for this render only.
            var frozen=new System.Collections.Generic.List<(SkinnedMeshRenderer renderer,GameObject preview,Mesh mesh)>();
            foreach(var skin in camera.transform.parent.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!skin.enabled) continue;
                var mesh=new Mesh(); skin.BakeMesh(mesh);
                var preview=ModelMesh.Object("临时姿态检查",mesh,skin.sharedMaterial,skin.transform,Vector3.zero);
                var properties=new MaterialPropertyBlock();skin.GetPropertyBlock(properties);preview.GetComponent<Renderer>().SetPropertyBlock(properties);
                frozen.Add((skin,preview,mesh)); skin.enabled=false;
            }
            var target=new RenderTexture(width,height,24);
            camera.targetTexture=target; camera.Render();
            var previous=RenderTexture.active; RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            System.IO.Directory.CreateDirectory("Artifacts"); System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            foreach(var item in frozen) { Object.DestroyImmediate(item.preview); Object.DestroyImmediate(item.mesh); item.renderer.enabled=true; }
        }
    }
}
