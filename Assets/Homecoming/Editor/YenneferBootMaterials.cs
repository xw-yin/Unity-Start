using System;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Calibrate only the boots; their shared glove source material stays separate.
    public static class YenneferBootMaterials
    {
        const string Root="Assets/ThirdParty/WitcherYenneferDLC/";
        [MenuItem("Homecoming/Repair Yennefer Boot Materials")]
        public static void RepairPrefab()
        {
            const string path="Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{Apply(root.GetComponent<CharacterRig>());PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        public static void Apply(CharacterRig rig)
        {
            var worn=rig.leftShoe?rig.leftShoe.GetComponent<SkinnedMeshRenderer>():null;
            if(!worn||!worn.sharedMesh.name.StartsWith("OriginalYenneferBootDLC"))return;
            string specPath=Root+"Prepared/BootPanel_SpecGloss.png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(specPath);
            if(!texture)throw new Exception("Prepare original boot reflectance first: "+specPath);
            var importer=(TextureImporter)AssetImporter.GetAtPath(specPath);
            if(!importer.sRGBTexture||importer.alphaIsTransparency)
            {importer.sRGBTexture=true;importer.alphaIsTransparency=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();}
            var materials=new Material[3];
            materials[0]=AssetDatabase.LoadAssetAtPath<Material>(Root+"Unity/Materials/DLCBootShell.mat");
            materials[1]=AssetDatabase.LoadAssetAtPath<Material>(Root+"Unity/Materials/DLCBootStraps.mat");
            string panelPath=Root+"Unity/Materials/DLCBootPanels.mat";
            materials[2]=AssetDatabase.LoadAssetAtPath<Material>(panelPath);
            if(!materials[2])
            {
                var original=AssetDatabase.LoadAssetAtPath<Material>(Root+"Unity/Materials/Surface_05.mat");
                materials[2]=new Material(original){name="DLCBootPanels"};AssetDatabase.CreateAsset(materials[2],panelPath);
            }
            for(int slot=0;slot<3;slot++)
            {
                var mat=materials[slot];if(!mat)throw new Exception("Missing original boot material "+slot);
                mat.SetFloat("_WorkflowMode",0);mat.EnableKeyword("_SPECULAR_SETUP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_SmoothnessTextureChannel",0);mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                mat.SetFloat("_Smoothness",slot==2?1f:.7f);
                mat.SetFloat("_BumpScale",slot==2?.7f:.6f);
                if(slot==2)mat.SetTexture("_SpecGlossMap",texture);
                EditorUtility.SetDirty(mat);
            }
            foreach(var obj in new[]{rig.leftShoe,rig.rightShoe,rig.leftGroundShoe,rig.rightGroundShoe})
                if(obj)obj.GetComponent<Renderer>().sharedMaterials=materials;
        }
    }
}
