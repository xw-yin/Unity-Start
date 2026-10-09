using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Homecoming.Editor
{
    // Inspect the user's currently loaded objects without rebuilding the scene.
    [InitializeOnLoad]
    public static class HomecomingLiveModelSnapshot
    {
        const string Request="Artifacts/live-model-inspect.request";
        const string RefreshRequest="Artifacts/live-model-refresh.request";
        static HomecomingLiveModelSnapshot(){EditorApplication.update+=Tick;}
        [Serializable] class Surface
        {
            public string name,mesh,path,shader;public bool active,enabled;
            public int vertices,uv4,fitIndex;public float fit,materialMask,materialRestMask,blockMask,blockRestMask;
        }
        [Serializable] class Snapshot
        {
            public bool playing;public string scene;public float elapsed;public Surface[] surfaces;
        }
        static void Tick()
        {
            if(File.Exists(RefreshRequest)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating)
            {
                File.Delete(RefreshRequest);
                var active=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
                Directory.CreateDirectory("Artifacts/LiveModelSnapshot");
                if(EditorApplication.isPlaying&&active)
                {
                    bool started=active.Started,paused=active.Paused;
                    active.ReloadCharacter();
                    if(started){active.TogglePlayback();if(paused)active.TogglePlayback();}
                    File.WriteAllText("Artifacts/LiveModelSnapshot/refresh.txt","Reloaded current runtime character "+active.CharacterIndex+" from updated assets; timeline reset; started="+started+" paused="+paused);
                    File.WriteAllText(Request,"Inspect refreshed runtime character");
                }
                else File.WriteAllText("Artifacts/LiveModelSnapshot/refresh.txt","Updated assets are imported; no running character to reload.");
            }
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            File.Delete(Request);
            try
            {
                var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
                var report=new Snapshot{playing=EditorApplication.isPlaying,scene=SceneManager.GetActiveScene().name,elapsed=story?(float)typeof(HomecomingScene).GetField("elapsed",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(story):-1};
                report.surfaces=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(s=>s.GetComponentInParent<CharacterRig>()).Select(s=>
                {
                    var block=new MaterialPropertyBlock();s.GetPropertyBlock(block);var m=s.sharedMaterial;int fit=s.sharedMesh.GetBlendShapeIndex("BootFit");
                    return new Surface{name=s.name,mesh=s.sharedMesh.name,path=AssetDatabase.GetAssetPath(s.sharedMesh),shader=m.shader.name,active=s.gameObject.activeInHierarchy,enabled=s.enabled,vertices=s.sharedMesh.vertexCount,uv4=s.sharedMesh.uv4.Length,fitIndex=fit,fit=fit>=0?s.GetBlendShapeWeight(fit):0,materialMask=m.HasProperty("_BootMask")?m.GetFloat("_BootMask"):0,materialRestMask=m.HasProperty("_BootRestMask")?m.GetFloat("_BootRestMask"):0,blockMask=block.GetFloat("_BootMask"),blockRestMask=block.GetFloat("_BootRestMask")};
                }).ToArray();
                Directory.CreateDirectory("Artifacts/LiveModelSnapshot");File.WriteAllText("Artifacts/LiveModelSnapshot/current.json",JsonUtility.ToJson(report,true));
                Debug.Log("LIVE MODEL SNAPSHOT SAVED: playing="+report.playing+" surfaces="+report.surfaces.Length);
            }
            catch(Exception error){File.WriteAllText("Artifacts/LiveModelSnapshot/error.txt",error.ToString());}
        }
    }
}
