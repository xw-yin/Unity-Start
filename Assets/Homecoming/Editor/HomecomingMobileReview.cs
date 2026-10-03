using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Homecoming.Editor
{
    public static class HomecomingMobileReview
    {
        public static void Run()
        {
            SaveLightingProfile();
            QualitySettings.SetQualityLevel(0,true);
            EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>(); story.Build();
            var type=typeof(HomecomingScene); const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var controls=story.CameraControls; var camera=controls.GetComponent<Camera>();
            if(type.GetMethod("OnGUI",flags)!=null) throw new Exception("Legacy dialogue overlay still exists.");
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if(!pipeline || pipeline.renderScale!=1 || pipeline.msaaSampleCount!=4 || !pipeline.supportsSoftShadows) throw new Exception("Mobile quality not applied.");
            var ui=HomecomingMobileUI.Create(story);
            var buttons=ui.GetComponentsInChildren<Button>();
            if(buttons.Length!=5+HomecomingCharacters.Names.Length) throw new Exception("Unexpected mobile controls.");
            foreach(var button in buttons)
                if(button.GetComponent<RectTransform>().sizeDelta.y<88) throw new Exception("Touch targets too small.");
            buttons[0].onClick.Invoke(); if(!story.Started) throw new Exception("Play button did not start.");
            buttons[0].onClick.Invoke(); if(!story.Paused) throw new Exception("Pause button failed.");
            float elapsed=(float)type.GetField("elapsed",flags).GetValue(story);
            type.GetMethod("Update",flags).Invoke(story,null);
            if((float)type.GetField("elapsed",flags).GetValue(story)!=elapsed) throw new Exception("Paused animation advanced.");
            buttons[0].onClick.Invoke();
            var area=new Rect(92,42,2350,1128); ui.UpdateSafeArea(area,new Vector2Int(2532,1170));
            var safe=ui.transform.Find("安全区域").GetComponent<RectTransform>();
            if(Mathf.Abs(safe.anchorMin.x-92/2532f)>.0001f || Mathf.Abs(safe.anchorMin.y-42/1170f)>.0001f) throw new Exception("Safe area is incorrect.");
            type.GetField("elapsed",flags).SetValue(story,16.8f); type.GetMethod("Update",flags).Invoke(story,null);
            var uiUpdate=typeof(HomecomingMobileUI).GetMethod("Update",flags);
            buttons[2].onClick.Invoke(); controls.SnapForReview(); uiUpdate.Invoke(ui,null);
            var canvas=ui.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=.2f;
            Canvas.ForceUpdateCanvases();
            HomecomingSetup.Capture(camera,"Artifacts/Mobile-room.png",1600,900);
            buttons[3].onClick.Invoke(); controls.SnapForReview(); uiUpdate.Invoke(ui,null);
            HomecomingSetup.Capture(camera,"Artifacts/Mobile-character.png",1600,900);
            buttons[4].onClick.Invoke(); controls.SnapForReview(); uiUpdate.Invoke(ui,null);
            HomecomingSetup.Capture(camera,"Artifacts/Mobile-boots.png",1600,900);
            Vector3 before=camera.transform.position;
            controls.Drag(new Vector2(300,80),1170); controls.Zoom(1.5f); controls.SnapForReview();
            if(Vector3.Distance(before,camera.transform.position)<.1f) throw new Exception("Camera gesture did not move the view.");
            for(int i=0;i<3;i++)
            {
                controls.SetView(i);
                foreach(float direction in new[]{-10000f,10000f})
                {
                    controls.Drag(new Vector2(direction,direction),900); controls.Zoom(.0001f); controls.SnapForReview();
                    var p=camera.transform.position;
                    if(p.x<-3.51f || p.x>3.66f || p.y<.29f || p.y>2.86f || p.z<-3.11f || p.z>3.13f) throw new Exception("Camera crossed room bounds.");
                }
            }
            buttons[1].onClick.Invoke();
            if(!story.Started || story.Paused || story.Finished || story.GetComponentInChildren<CharacterRig>().transform.position.z<3.8f) throw new Exception("Replay failed.");
            Debug.Log("HOMECOMING MOBILE REVIEW PASSED: device quality, five touch buttons, safe area, pause/resume/replay, three views, drag/zoom, room bounds, dialogue removed.");
        }
        internal static void SaveLightingProfile()
        {
            const string path="Assets/Homecoming/Resources/Homecoming/IndoorLighting.asset";
            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path)) return;
            var profile=HomecomingPresentation.CreateProfile(); AssetDatabase.CreateAsset(profile,path);
            foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
        }
    }
}
