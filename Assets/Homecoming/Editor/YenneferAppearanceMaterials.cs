using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Homecoming.Editor
{
    // Source channels come from the local game material graph snapshot. Rendering
    // adaptations are explicit; these are not the original REDengine shaders.
    public static class YenneferAppearanceMaterials
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Folder="Assets/ThirdParty/WitcherYenneferDLC/Unity/Materials/";
        const string Prefab="Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab";
        [Serializable] class Manifest {public Surface[] materials;}
        [Serializable] class Surface {public string name;}
        [MenuItem("Homecoming/还原叶奈法皮肤头发与眼部材质")]
        public static void Apply()
        {
            var root=PrefabUtility.LoadPrefabContents(Prefab);
            try {ApplyToRig(root.GetComponent<CharacterRig>());PrefabUtility.SaveAsPrefabAsset(root,Prefab);}
            finally {PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void ApplyAndReview(){Apply();YenneferAppearanceReview.Current();}
        public static void ApplyToRig(CharacterRig rig)
        {
            foreach(string file in new[]{"FaceAmbient","HairShift","EyeShadow"})Import(Source+"Appearance/"+file+".png",false,true);
            foreach(string file in new[]{"SkinDetail","EyeNormal"})Import(Source+"Appearance/"+file+".png",true,false);
            Import(Source+"Appearance/FaceDiffuse.png",false,false,true);Import(Source+"Appearance/FaceNormal.png",true,false,true);
            var shader=Shader.Find("Homecoming/Character Surface");var shadowShader=Shader.Find("Homecoming/Eye Socket Shadow");
            if(!shader||!shadowShader)throw new InvalidOperationException("Character appearance shaders are missing");
            var data=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            for(int i=0;i<data.materials.Length;i++)
            {
                var name=data.materials[i].name;var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Surface_"+i.ToString("D2")+".mat");if(!mat)continue;
                if(name.StartsWith("h_01_wa__yennefer_hq")||name.StartsWith("h_wa__neck_transition")||name=="body_05_wa__yennefer_Material7")
                {
                    bool face=name.StartsWith("h_01_");Skin(mat,shader,face);
                    if(face){mat.SetTexture("_BaseMap",Texture("FaceDiffuse"));mat.SetTexture("_BumpMap",Texture("FaceNormal"));}
                    mat.SetTexture("_AmbientMap",face?Texture("FaceAmbient"):Texture2D.whiteTexture);
                    mat.SetTexture("_DetailNormalMap",face?Texture("SkinDetail"):null);
                    mat.SetFloat("_DetailStrength",face?.08f:0);mat.SetFloat("_DetailTiling",8);
                }
                else if(name.StartsWith("c_01_wa__yennefer"))
                {
                    mat.shader=shader;mat.EnableKeyword("_CHARACTER_HAIR");mat.SetFloat("_Hair",1);
                    mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Smoothness",.45f);mat.SetFloat("_UseSurfaceMap",0);
                    mat.SetColor("_BaseColor",new Color(.26f,.26f,.28f));mat.SetTexture("_ShiftMap",Texture("HairShift"));
                    mat.SetVector("_ShiftTiling",name.EndsWith("Material0")?new Vector4(5,5,0,0):new Vector4(2,3,0,0));
                    mat.SetFloat("_ShiftScale",.20f);mat.SetFloat("_ShiftOffset",.22f);mat.SetFloat("_HairExponent",48);mat.SetColor("_HairSpecular",new Color(.14f,.15f,.16f));mat.SetColor("_SpecColor",new Color(.02f,.02f,.02f));mat.SetFloat("_Cutoff",name.EndsWith("Material0")?.35f:.5f);
                    mat.SetFloat("_SrcBlend",1);mat.SetFloat("_DstBlend",0);mat.SetFloat("_ZWrite",1);mat.SetFloat("_AlphaToMask",1);mat.DisableKeyword("_HAIR_FRINGE");
                    mat.renderQueue=2450;mat.SetOverrideTag("RenderType","TransparentCutout");
                }
                else if(name=="he_01_wa__yennefer_Material0")
                {
                    mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetFloat("_WorkflowMode",0);mat.EnableKeyword("_SPECULAR_SETUP");
                    mat.SetFloat("_Smoothness",.8f);mat.SetColor("_SpecColor",new Color(.025f,.025f,.025f));
                    mat.SetTexture("_BumpMap",Texture("EyeNormal"));mat.SetFloat("_BumpScale",.25f);mat.EnableKeyword("_NORMALMAP");mat.renderQueue=2000;
                }
                else if(name=="he_01_wa__yennefer_Material1")
                {
                    mat.shader=shadowShader;mat.SetTexture("_BaseMap",Texture("EyeShadow"));mat.SetFloat("_Strength",.35f);mat.SetFloat("_Exponent",2.2f);mat.renderQueue=2990;
                    foreach(var renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.sharedMaterial==mat))
                    {renderer.enabled=true;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
                }
                else if(name=="he_01_wa__yennefer_Material4")
                {
                    mat.SetFloat("_AlphaClip",1);mat.EnableKeyword("_ALPHATEST_ON");mat.SetFloat("_Cutoff",.08367346f);mat.SetFloat("_AlphaToMask",1);mat.SetFloat("_Smoothness",.25f);
                }
                else continue;
                if(mat.shader==shader&&mat.GetTexture("_BumpMap"))Import(AssetDatabase.GetAssetPath(mat.GetTexture("_BumpMap")),true,false,name.StartsWith("h_01_"));
                EditorUtility.SetDirty(mat);
            }
            BuildFringes(rig);
            Debug.Log("YENNEFER APPEARANCE INPUTS: original diffuse/normal retained; skin AO=R roughness=G, source pore normal, strand shift, original eye normal and socket mask; no geometry edits.");
        }
        static void BuildFringes(CharacterRig rig)
        {
            foreach(var old in rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.name.EndsWith("_HairFringe")).ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            var source=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMaterial&&r.sharedMaterial.IsKeywordEnabled("_CHARACTER_HAIR")).ToArray();
            for(int i=0;i<source.Length;i++)
            {
                var skin=source[i];string path=Folder+"HairFringe_"+i.ToString("D2")+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(skin.sharedMaterial);AssetDatabase.CreateAsset(material,path);}else{material.shader=skin.sharedMaterial.shader;material.CopyPropertiesFromMaterial(skin.sharedMaterial);}
                material.EnableKeyword("_HAIR_FRINGE");material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);material.SetFloat("_ZWrite",0);material.SetFloat("_AlphaToMask",0);material.renderQueue=3000;material.SetOverrideTag("RenderType","Transparent");EditorUtility.SetDirty(material);
                material.SetShaderPassEnabled("ShadowCaster",false);material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("DepthNormals",false);
                var obj=new GameObject(skin.name+"_HairFringe");obj.transform.SetParent(skin.transform,false);obj.layer=skin.gameObject.layer;
                var fringe=obj.AddComponent<SkinnedMeshRenderer>();fringe.sharedMesh=skin.sharedMesh;fringe.bones=skin.bones;fringe.rootBone=skin.rootBone;fringe.localBounds=skin.localBounds;fringe.updateWhenOffscreen=true;fringe.sharedMaterial=material;fringe.shadowCastingMode=ShadowCastingMode.Off;fringe.renderingLayerMask=skin.renderingLayerMask;
            }
        }
        static void Skin(Material m,Shader shader,bool face)
        {
            m.shader=shader;m.DisableKeyword("_CHARACTER_HAIR");m.SetFloat("_Hair",0);m.SetColor("_BaseColor",Color.white);
            m.SetFloat("_UseSurfaceMap",face?1:0);m.SetFloat("_BumpScale",face?.5f:.65f);m.SetFloat("_Smoothness",.5f);
            m.SetColor("_SpecColor",new Color(.028f,.0285f,.03f));m.SetFloat("_OcclusionStrength",.3f);
            m.SetFloat("_Wrap",.2f);m.SetFloat("_ScatterStrength",.5f);m.SetColor("_ScatterColor",new Color(.16f,.05f,.035f));m.renderQueue=2000;
        }
        static Texture2D Texture(string name)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Appearance/"+name+".png");
        static void Import(string path,bool normal,bool linear,bool nativeHead=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new InvalidOperationException("Missing source texture "+path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.flipGreenChannel=normal;importer.sRGBTexture=!normal&&!linear;importer.alphaIsTransparency=false;importer.maxTextureSize=nativeHead?4096:2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.compressionQuality=100;
            if(nativeHead)
            {
                var desktop=importer.GetPlatformTextureSettings("Standalone");desktop.overridden=true;desktop.maxTextureSize=4096;desktop.format=TextureImporterFormat.BC7;importer.SetPlatformTextureSettings(desktop);
                var mobile=importer.GetPlatformTextureSettings("iPhone");mobile.overridden=true;mobile.maxTextureSize=2048;mobile.format=TextureImporterFormat.ASTC_4x4;importer.SetPlatformTextureSettings(mobile);
            }
            importer.SaveAndReimport();
        }
    }
}
