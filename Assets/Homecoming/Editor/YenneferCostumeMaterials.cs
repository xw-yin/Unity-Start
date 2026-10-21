using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    // Source parameters are kept beside the converted maps; this is a URP adaptation.
    public static class YenneferCostumeMaterials
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Maps=Source+"CostumeAppearance/";
        [Serializable] class Manifest {public Input[] materials;}
        [Serializable] class Input {public string name;public Parameters parameters;}
        [Serializable] class Parameters
        {
            public string SpecularColor,RSpecScale,Translucency,DetailPower,DetailTile,Pattern_Size,Pattern_Roughness_Influence;
        }
        public static void ApplyToRig(CharacterRig rig)
        {
            var recipe=JsonUtility.FromJson<Manifest>(File.ReadAllText(Maps+"source.json"));
            var slots=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json")).materials;
            string folder=Source.Replace("Prepared/","Unity/Materials/");
            var names=slots.Select((m,i)=>new{path=folder+"Surface_"+i.ToString("D2")+".mat",m.name}).ToDictionary(m=>m.path,m=>m.name);
            foreach(string name in new[]{"FeatherDiffuse","ScarfDiffuse"})Import(MapPath(name),false,false);
            foreach(string name in new[]{"FeatherNormal","ScarfNormal","ScarfDetail"})Import(MapPath(name),true,true);
            foreach(string name in new[]{"FeatherSurface","ScarfSurface","ScarfPattern"})Import(Maps+name+".png",false,true);
            var materials=rig.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m&&!m.IsKeywordEnabled("_HAIR_FRINGE")).Distinct().ToArray();
            int specialMaterials=0;
            foreach(var material in materials)
            {
                string name=names.TryGetValue(AssetDatabase.GetAssetPath(material),out var originalName)?originalName:material.name;
                // Remove the old global gloss reduction. The maps already carry
                // source roughness, and multiplying smoothness hid leather folds.
                bool garment=name.StartsWith("body_05_wa__yennefer_")||name.StartsWith("dress_05_")||name.StartsWith("collar_01_")||name.StartsWith("DLCBoot");
                if(garment&&material.shader.name=="Universal Render Pipeline/Lit"&&material.GetTexture("_SpecGlossMap"))
                {
                    bool metal=name=="body_05_wa__yennefer_Material2"||name=="body_05_wa__yennefer_Material4";
                    if(!metal)material.SetFloat("_Smoothness",1);
                    var normal=material.GetTexture("_BumpMap");if(normal)Import(AssetDatabase.GetAssetPath(normal),true,true);
                    EditorUtility.SetDirty(material);
                }
                var input=recipe.materials.FirstOrDefault(m=>m.name==name);if(input==null)continue;
                specialMaterials++;
                bool feather=name.StartsWith("feathers_05_");string prefix=feather?"Feather":"Scarf";
                var shader=Shader.Find("Homecoming/Character Surface");if(!shader)throw new InvalidOperationException("Character Surface shader missing");
                material.shader=shader;
                material.SetTexture("_BaseMap",Texture(prefix+"Diffuse"));material.SetTextureScale("_BaseMap",Vector2.one);material.SetColor("_BaseColor",Color.white);
                material.SetTexture("_BumpMap",Texture(prefix+"Normal"));material.SetFloat("_BumpScale",feather?.8f:.7f);
                material.SetTexture("_AmbientMap",Texture(prefix+"Surface"));material.SetFloat("_UseSurfaceMap",1);
                var color=Vector(input.parameters.SpecularColor);var specular=new Color(color[0]/255,color[1]/255,color[2]/255).linear;
                material.SetColor("_SpecColor",specular*Number(input.parameters.RSpecScale,1));
                material.DisableKeyword("_HAIR_FRINGE");material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_ZWrite",1);material.SetFloat("_AlphaToMask",1);
                SetKeyword(material,"_CHARACTER_HAIR",feather);SetKeyword(material,"_CHARACTER_FEATHER",feather);SetKeyword(material,"_CHARACTER_COSTUME",!feather);
                material.SetFloat("_Hair",feather?1:0);material.SetFloat("_Feather",feather?1:0);material.SetFloat("_Costume",feather?0:1);
                if(feather)
                {
                    material.SetFloat("_Cutoff",.35f);material.SetFloat("_Translucency",Number(input.parameters.Translucency,0));
                    material.renderQueue=2450;material.SetOverrideTag("RenderType","TransparentCutout");
                }
                else
                {
                    // REDengine detail strength is not a direct normal amplitude
                    // in URP. Attenuate its microscopic layer to retain the folds.
                    material.SetTexture("_DetailNormalMap",Texture("ScarfDetail"));material.SetFloat("_DetailStrength",Number(input.parameters.DetailPower,.9f)*.35f);
                    material.SetFloat("_DetailTiling",Vector(input.parameters.DetailTile)[0]);material.SetTexture("_PatternMap",Texture("ScarfPattern"));
                    material.SetFloat("_PatternTiling",1/Vector(input.parameters.Pattern_Size)[0]);material.SetFloat("_PatternStrength",.08f);
                    material.SetFloat("_PatternRoughness",Number(input.parameters.Pattern_Roughness_Influence,.7f));
                    material.renderQueue=2000;material.SetOverrideTag("RenderType","Opaque");
                }
                EditorUtility.SetDirty(material);
            }
            if(specialMaterials!=2)throw new InvalidOperationException("Original feather/scarf material mapping failed: "+specialMaterials+"/2");
            Debug.Log("YENNEFER COSTUME: original feather/scarf albedo, normal, alpha roughness, scarf detail and ornament; mesh topology unchanged.");
        }
        static void SetKeyword(Material m,string key,bool value){if(value)m.EnableKeyword(key);else m.DisableKeyword(key);}
        static string MapPath(string name)=>name switch
        {
            "FeatherDiffuse"=>Source+"Texture_12.png", "FeatherNormal"=>Source+"Texture_13.png",
            "ScarfDiffuse"=>Source+"Texture_24.png", "ScarfNormal"=>Source+"Texture_25.png", _=>Maps+name+".png"
        };
        static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath(name));
        static float Number(string text,float fallback)=>float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:fallback;
        static float[] Vector(string text)=>text.Split(';').Select(s=>float.Parse(s.Trim(),CultureInfo.InvariantCulture)).ToArray();
        static void Import(string path,bool normal,bool linear)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new InvalidOperationException("Missing original costume input "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.flipGreenChannel=normal;
            importer.sRGBTexture=!linear&&!normal;importer.alphaIsTransparency=false;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;importer.anisoLevel=4;importer.SaveAndReimport();
        }
    }
}
