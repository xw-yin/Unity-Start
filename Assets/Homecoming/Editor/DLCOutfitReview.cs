using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class DLCOutfitReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        public static void RebuildAndRun()
        {
            Capture("before");WitcherCharacterImport.Import("Ciri");
            foreach(string name in new[]{"Triss","YenneferDLC"})
            {
                string path="Assets/Homecoming/Resources/Homecoming/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{WitcherCostumeMaterials.Apply(root.GetComponent<CharacterRig>(),"Assets/ThirdParty/Witcher"+name+"/Prepared/");PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Capture("after");HomecomingFootwearReview.Run();HomecomingContinuousReview.Run();
        }
        public static void RepairMaterialsAndRun()
        {
            foreach(string name in new[]{"Ciri","Triss","YenneferDLC"})
            {
                string path="Assets/Homecoming/Resources/Homecoming/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{WitcherCostumeMaterials.Apply(root.GetComponent<CharacterRig>(),"Assets/ThirdParty/Witcher"+(name=="Ciri"?"CiriDLC":name)+"/Prepared/");PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Capture("after");HomecomingFootwearReview.Run();HomecomingContinuousReview.Run();
        }
        public static void FinishWardrobe()
        {
            foreach(string name in new[]{"Ciri","Triss","YenneferDLC"})
            {
                string path="Assets/Homecoming/Resources/Homecoming/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{WitcherCostumeMaterials.Apply(root.GetComponent<CharacterRig>(),"Assets/ThirdParty/Witcher"+(name=="Ciri"?"CiriDLC":name)+"/Prepared/");PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();Capture("after");BlenderModelBridge.ExportDLCOutfits();Debug.Log("DLC OUTFIT FINAL MATERIAL AND RENDER REVIEW PASSED");
        }
        public static void Capture(string label)
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;
            Directory.CreateDirectory("Artifacts/DLCOutfitReview");
            foreach(int index in new[]{0,2,3})
            {
                story.SelectCharacter(index);var rig=story.GetComponentInChildren<CharacterRig>();
                typeof(HomecomingScene).GetField("started",Flags).SetValue(story,true);
                for(int frame=0;frame<=240;frame++)
                {
                    typeof(HomecomingScene).GetField("elapsed",Flags).SetValue(story,frame/30f-Time.deltaTime);
                    typeof(HomecomingScene).GetMethod("Update",Flags).Invoke(story,null);
                }
                if(label=="after")
                {
                    string body=index==0?"body_05_wa__yennefer":index==2?"body_01_wa__triss_dlc":"body_01_wa__ciri_dlc";
                    if(!rig.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.name.StartsWith(body)))throw new Exception("Incorrect DLC costume source: "+HomecomingCharacters.Names[index]);
                    var torso=rig.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name.StartsWith(body)&&s.sharedMesh.bounds.max.y>1.3f&&s.sharedMaterial.shader.name=="Universal Render Pipeline/Lit");
                    if(torso.sharedMaterial.GetFloat("_WorkflowMode")!=0||!torso.sharedMaterial.GetTexture("_SpecGlossMap"))throw new Exception("Original costume specular / roughness input is absent: "+HomecomingCharacters.Names[index]);
                    Debug.Log("DLC OUTFIT SOURCE VERIFIED: "+HomecomingCharacters.Names[index]+" "+body);
                }
                var camera=story.CameraControls.GetComponent<Camera>();var aim=rig.transform.position+new Vector3(0,.85f,0);
                camera.fieldOfView=38;camera.transform.position=aim+new Vector3(.10f,.04f,-2.8f);camera.transform.LookAt(aim);
                HomecomingSetup.Capture(camera,"Artifacts/DLCOutfitReview/"+index+"-"+label+"-front.png",1000,1100);
                if(label=="after")HomecomingContinuousReview.CaptureGpu(camera,"Artifacts/DLCOutfitReview/"+index+"-final-skin-front.png");
                camera.transform.position=aim+new Vector3(.1f,.04f,2.8f);camera.transform.LookAt(aim);
                HomecomingSetup.Capture(camera,"Artifacts/DLCOutfitReview/"+index+"-"+label+"-back.png",1000,1100);
                if(label=="after")HomecomingContinuousReview.CaptureGpu(camera,"Artifacts/DLCOutfitReview/"+index+"-final-skin-back.png");
            }
        }
    }
}
