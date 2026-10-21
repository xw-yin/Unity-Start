using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class YenneferFaceImport
    {
        const string Source="Assets/ThirdParty/WitcherYenneferDLC/Prepared/";
        const string Library="Assets/ThirdParty/WitcherYenneferDLC/Unity/YenneferFace.asset";
        [Serializable] class Poses {public string source,sourceSHA256;public string[] bones,ignoredSourceBones;public HomecomingFaceLibrary.Pose[] poses;}
        [Serializable] class Clips {public string source,sourceSHA256;public HomecomingFaceLibrary.Clip[] clips;}
        [Serializable] class Manifest {public Node[] nodes;}
        [Serializable] class Node {public string name;public float[] position;}
        [MenuItem("Homecoming/导入叶奈法原版面部姿态与动画")]
        public static void Run()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab");
            try{ApplyToRig(root.GetComponent<CharacterRig>());PrefabUtility.SaveAsPrefabAsset(root,"Assets/Homecoming/Resources/Homecoming/YenneferDLC.prefab");}
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        public static void ApplyToRig(CharacterRig rig)
        {
            var poses=JsonUtility.FromJson<Poses>(File.ReadAllText(Source+"Face/poses.json"));
            var clips=JsonUtility.FromJson<Clips>(File.ReadAllText(Source+"Face/clips.json"));
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Source+"manifest.json"));
            var library=AssetDatabase.LoadAssetAtPath<HomecomingFaceLibrary>(Library);
            if(!library){library=ScriptableObject.CreateInstance<HomecomingFaceLibrary>();AssetDatabase.CreateAsset(library,Library);}
            library.poseSource=poses.source;library.poseSHA256=poses.sourceSHA256;library.clipSource=clips.source;library.clipSHA256=clips.sourceSHA256;
            library.bones=poses.bones;library.poses=poses.poses;library.clips=clips.clips;library.ignoredSourceBones=poses.ignoredSourceBones;
            library.restPositions=poses.bones.Select(n=>manifest.nodes.First(x=>x.name==n).position).Select(v=>new Vector3(v[0],v[1],v[2])).ToArray();
            library.ignoredTracks=clips.clips.SelectMany(c=>c.tracks).Select(t=>t.name).Where(n=>!poses.poses.Any(p=>p.name==n)).Distinct().ToArray();
            rig.faceLibrary=library;EditorUtility.SetDirty(library);
            Debug.Log("YENNEFER ORIGINAL FACE IMPORTED: "+poses.poses.Length+" source poses / "+poses.bones.Length+" deformation bones / "+clips.clips.Length+" original clips; non-geometric wrinkle/iris tracks excluded.");
        }
    }
}
