using UnityEngine;
using UnityEditor;

namespace Homecoming.Editor
{
    public static class HomecomingCharacterRefine
    {
        public static void CaptureComparison()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            var story=Object.FindAnyObjectByType<HomecomingScene>(); story.Build();
            var rig=story.GetComponentInChildren<CharacterRig>(); rig.Neutral();
            var camera=story.GetComponentInChildren<Camera>();
            camera.transform.position=rig.transform.position+new Vector3(.5f,.95f,-2.8f);
            camera.transform.LookAt(rig.transform.position+new Vector3(0,.83f,0)); camera.fieldOfView=38;
            camera.backgroundColor=new Color(.13f,.16f,.20f);
            foreach(var renderer in story.GetComponentsInChildren<Renderer>())
                if(!renderer.transform.IsChildOf(rig.transform)) renderer.enabled=false;
            HomecomingSetup.Capture(camera,"Artifacts/Legs-refined.png",800,1000);
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int part=skin.name=="F00_000_00_Body_00_SKIN"?9:skin.name=="F00_008_01_Bottoms_01_CLOTH"?11:-1;
                if(part>=0) skin.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ThirdParty/VRoidSample/Unity/Meshes/Part_"+part.ToString("D2")+".asset");
            }
            HomecomingSetup.Capture(camera,"Artifacts/Legs-original.png",800,1000);
            Debug.Log("LEG COMPARISON RENDERED");
        }
        public static void RefineAndReview()
        {
            const string path="Assets/Homecoming/Resources/Homecoming/XiaVRoid.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            Apply(root);
            PrefabUtility.SaveAsPrefabAsset(root,path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            HomecomingMotionReview.Run();
        }
        public static void Apply(GameObject root)
        {
            var rig=root.GetComponent<CharacterRig>();
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                int part=skin.name=="F00_000_00_Body_00_SKIN"?9:skin.name=="F00_008_01_Bottoms_01_CLOTH"?11:-1;
                if(part<0) continue;
                // Always derive from the untouched source, so repeated refinement
                // and model re-import never inflate an already edited mesh.
                var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ThirdParty/VRoidSample/Unity/Meshes/Part_"+part.ToString("D2")+".asset");
                var mesh=Object.Instantiate(source); mesh.name=part==9?"Xia refined body":"Xia refined legwear";
                var vertices=mesh.vertices; var normals=mesh.normals; var weights=mesh.boneWeights;
                var lHip=skin.transform.InverseTransformPoint(rig.leftThigh.position);
                var rHip=skin.transform.InverseTransformPoint(rig.rightThigh.position);
                var lKnee=skin.transform.InverseTransformPoint(rig.leftCalf.position);
                var rKnee=skin.transform.InverseTransformPoint(rig.rightCalf.position);
                var lAnkle=skin.transform.InverseTransformPoint(rig.leftCalf.GetChild(0).position);
                var rAnkle=skin.transform.InverseTransformPoint(rig.rightCalf.GetChild(0).position);
                int changed=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=vertices[i]; if(v.y<.16f||v.y>.845f) continue;
                    float influence=0,leftWeight=0,rightWeight=0;
                    var w=weights[i];
                    foreach(var item in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})
                    {
                        string bone=skin.bones[item.Item1].name;
                        if(bone.Contains("UpperLeg")||bone.Contains("LowerLeg")) { influence+=item.Item2; if(bone.Contains("_L_")) leftWeight+=item.Item2; else rightWeight+=item.Item2; }
                    }
                    if(influence<.25f) continue;
                    bool left=leftWeight>rightWeight;
                    Vector3 hip=left?lHip:rHip,knee=left?lKnee:rKnee,ankle=left?lAnkle:rAnkle;
                    Vector3 center=v.y>knee.y?Vector3.Lerp(knee,hip,Mathf.InverseLerp(knee.y,hip.y,v.y)):Vector3.Lerp(ankle,knee,Mathf.InverseLerp(ankle.y,knee.y,v.y));
                    float thigh=Mathf.Exp(-Mathf.Pow((v.y-.68f)/.16f,2));
                    float calf=Mathf.Exp(-Mathf.Pow((v.y-.33f)/.115f,2));
                    float fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.16f,.24f,v.y))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.79f,.845f,v.y)));
                    float sx=1+(.30f*thigh+.42f*calf)*fade*Mathf.Clamp01(Mathf.Abs(leftWeight-rightWeight));
                    float sz=1+(.23f*thigh+.33f*calf)*fade*Mathf.Clamp01(Mathf.Abs(leftWeight-rightWeight));
                    vertices[i]=new Vector3(center.x+(v.x-center.x)*sx,v.y,center.z+(v.z-center.z)*sz);
                    normals[i]=new Vector3(normals[i].x/sx,normals[i].y,normals[i].z/sz).normalized;
                    changed++;
                }
                mesh.vertices=vertices; mesh.normals=normals; mesh.RecalculateBounds();
                string destination="Assets/Homecoming/Models/"+(part==9?"XiaRefinedBody":"XiaRefinedLegwear")+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(destination);
                if(saved) { EditorUtility.CopySerialized(mesh,saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); mesh=saved; }
                else AssetDatabase.CreateAsset(mesh,destination);
                skin.sharedMesh=mesh;
                Debug.Log("CHARACTER REFINED: "+skin.name+" vertices="+changed+" (UVs and skin weights preserved)");
            }
        }
    }
}
