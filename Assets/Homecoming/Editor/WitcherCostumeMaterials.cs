using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class WitcherCostumeMaterials
    {
        [Serializable] class Manifest { public Surface[] materials; }
        [Serializable] class Surface { public string name,normal,specular,sourceShader,albedo;public float tileX,tileY; }
        [Serializable] class SourceManifest { public SourceMaterial[] materials; }
        [Serializable] class SourceMaterial { public string name; }
        public static void Apply(CharacterRig rig,string source)
        {
            string file=source+"costume-materials.json";if(!File.Exists(file))return;
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
            var mapping=JsonUtility.FromJson<SourceManifest>(File.ReadAllText(source+"manifest.json"));
            string folder=source.Replace("Prepared/","Unity/Materials/");
            var activeMaterials=rig.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
            foreach(var surface in data.materials)
            {
                ImportMap(source+surface.normal,true);ImportMap(source+surface.specular,false);
                if(!string.IsNullOrEmpty(surface.albedo))ImportMap(source+surface.albedo,false);
                int index=Array.FindIndex(mapping.materials,m=>m.name==surface.name);
                var mapped=index>=0?AssetDatabase.LoadAssetAtPath<Material>(folder+"Surface_"+index.ToString("D2")+".mat"):null;
                var targets=activeMaterials.Where(m=>m==mapped).ToArray();
                if(source.Contains("YenneferDLC")&&surface.name=="body_05_wa__yennefer_Material1")targets=targets.Concat(activeMaterials.Where(m=>AssetDatabase.GetAssetPath(m)==folder+"DLCBootShell.mat")).Distinct().ToArray();
                if(source.Contains("YenneferDLC")&&surface.name=="body_05_wa__yennefer_Material8")targets=targets.Concat(activeMaterials.Where(m=>AssetDatabase.GetAssetPath(m)==folder+"DLCBootStraps.mat")).Distinct().ToArray();
                foreach(var mat in targets)
                {
                    if(mat.shader.name!="Universal Render Pipeline/Lit")continue;
                    mat.SetFloat("_WorkflowMode",0);mat.EnableKeyword("_SPECULAR_SETUP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetTexture("_SpecGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+surface.specular));mat.SetColor("_SpecColor",Color.white);
                    mat.SetFloat("_Smoothness",1);mat.SetFloat("_SmoothnessTextureChannel",0);mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    // Match the dark fabric response across REDengine / URP:
                    // source colored highlights must not turn cloth into chrome.
                    if(source.Contains("YenneferDLC"))mat.SetFloat("_Smoothness",surface.name.Contains("Material2")||surface.name.Contains("Material4")?.75f:surface.name.Contains("dress_05")||surface.name.EndsWith("Material0")||surface.name.EndsWith("Material3")?.4f:.6f);
                    if(source.Contains("CiriDLC")&&surface.name.StartsWith("item_03_wa__ciri_dlc"))mat.SetFloat("_Smoothness",.3f);
                    mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+surface.normal));mat.SetFloat("_BumpScale",1);mat.EnableKeyword("_NORMALMAP");
                    if(!string.IsNullOrEmpty(surface.albedo))
                    {
                        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(source+surface.albedo));mat.SetTextureScale("_BaseMap",new Vector2(surface.tileX,surface.tileY));
                        mat.SetTexture("_SpecGlossMap",null);mat.DisableKeyword("_METALLICSPECGLOSSMAP");mat.SetColor("_SpecColor",new Color(.12f,.12f,.12f));mat.SetFloat("_Smoothness",.35f);mat.SetFloat("_BumpScale",.65f);
                    }
                    EditorUtility.SetDirty(mat);Debug.Log("ORIGINAL COSTUME MATERIAL: "+mat.name+"; source colored specular, roughness and cloth normals restored.");
                }
            }
            if(source.Contains("YenneferDLC"))YenneferBootMaterials.Apply(rig);
        }
        static void ImportMap(string path,bool normal)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Missing costume map: "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
        }
    }
}
