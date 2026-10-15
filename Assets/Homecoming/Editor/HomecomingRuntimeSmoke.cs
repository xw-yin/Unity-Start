using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Homecoming.Editor
{
    [InitializeOnLoad]
    public static class HomecomingRuntimeSmoke
    {
        const string Running="Homecoming.RuntimeSmoke.Running";
        static int frames;
        static HomecomingRuntimeSmoke()
        {
            EditorApplication.playModeStateChanged+=OnState;
            if(SessionState.GetBool(Running,false)&&EditorApplication.isPlaying) { frames=0; EditorApplication.update+=Check; }
        }
        public static void Run()
        {
            SessionState.SetBool(Running+".PreviousReloadSetting",EditorSettings.enterPlayModeOptionsEnabled);
            EditorSettings.enterPlayModeOptionsEnabled=false;
            EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            SessionState.SetBool(Running,true); EditorApplication.EnterPlaymode();
        }
        static void OnState(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Running,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) { frames=0; EditorApplication.update-=Check; EditorApplication.update+=Check; }
            if(state==PlayModeStateChange.EnteredEditMode) { EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Running+".PreviousReloadSetting",false); SessionState.SetBool(Running,false); EditorApplication.Exit(0); }
        }
        static void Check()
        {
            if(!EditorApplication.isPlaying||++frames<15) return;
            EditorApplication.update-=Check;
            try
            {
                var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
                var rig=story.GetComponentInChildren<CharacterRig>();
                if(rig.transform.position.z<3.8f) throw new Exception("人物未在门外等待");
                var walk=Resources.Load<HomecomingWalkCycle>("Homecoming/WalkCycle");
                if(!walk||walk.frames<30||walk.strideDistance<.3f) throw new Exception("人形走路缓存缺失");
                var cloth=rig.GetComponent<HomecomingClothDynamics>();
                var sourceMesh=cloth&&cloth.SourceMesh?cloth.SourceMesh:rig.skirt.sharedMesh;
                if(!AssetDatabase.Contains(sourceMesh)) throw new Exception("运行时未加载保存的角色模型");
                if(cloth&&cloth.SourceMesh==rig.skirt.sharedMesh) throw new Exception("布料动态正在直接修改源模型");
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var type=typeof(HomecomingScene); type.GetField("started",flags).SetValue(story,true);
                foreach(float time in new[]{1f,4.8f,7.2f,8.3f,10.6f,13.3f,14f,15.5f,19.3f,20f,21.5f,25.6f,32f})
                {
                    type.GetField("elapsed",flags).SetValue(story,time); type.GetMethod("Update",flags).Invoke(story,null);
                    var motion=(HomecomingMotion)type.GetField("motion",flags).GetValue(story);
                    for(int side=0;side<2;side++)
                    {
                        float action=story.ActionTime;
                        if(action<HomecomingShoeGrip.Detach(side)) continue;
                        var proxy=story.transform.Find("脱下的通勤鞋").GetChild(side);
                        if(!proxy.gameObject.activeSelf) throw new Exception("脱下的鞋未显示");
                        if((side==0?rig.leftShoe:rig.rightShoe).activeSelf) throw new Exception("脱下鞋后仍显示脚上的鞋");
                        if(action<HomecomingShoeGrip.Release(side))
                        {
                            if(motion.Shoes.ContactError(proxy,side)>.002f) throw new Exception("鞋子脱离手部");
                        }
                        else if(Vector3.Distance(proxy.position,motion.Shoes.GroundOrigin(side))>.003f) throw new Exception("放下的鞋未保持在地面");
                    }
                }
                if(rig.bag||rig.leftSlipper||rig.rightSlipper||rig.leftShoe.activeSelf) throw new Exception("换鞋后站立状态错误");
                type.GetMethod("ResetStory",flags).Invoke(story,null);
                if(rig.transform.position.z<3.8f) throw new Exception("重播未恢复门外等待位置");
                if(!rig.leftShoe.activeSelf||rig.leftSlipper||rig.rightSlipper) throw new Exception("重播状态错误");
                if(rig.importedSource && (!rig.GetComponent<Animator>().avatar.isHuman)) throw new Exception("导入人物的表情或人形骨骼无效");
                CheckMobileInput(story);
                var characterButtons=story.GetComponentInChildren<HomecomingMobileUI>().GetComponentsInChildren<Button>();
                foreach(int index in new[]{1,2,3,4,5,6,0})
                {
                    characterButtons.Single(b=>b.name==HomecomingCharacters.Names[index]).onClick.Invoke();
                    rig=story.GetComponentInChildren<CharacterRig>();
                    if(story.CharacterIndex!=index || story.Started || rig.transform.position.z<3.8f || !rig.GetComponent<Animator>().avatar.isHuman)throw new Exception("Play mode character switch failed: "+index);
                    story.TogglePlayback();
                    foreach(float time in new[]{13.01f,14.4f,19.01f,20.4f,27f})
                    {
                        type.GetField("elapsed",flags).SetValue(story,time);type.GetMethod("Update",flags).Invoke(story,null);
                        for(int side=0;side<2;side++)
                        {
                            var sock=rig.hosiery[side];
                            if(!sock||!sock.enabled||!sock.gameObject.activeInHierarchy)throw new Exception("Stocking disappeared during runtime extraction: "+index);
                            var properties=new MaterialPropertyBlock();sock.GetPropertyBlock(properties);
                            if(story.ActionTime>=HomecomingShoeGrip.Detach(side)&&properties.GetFloat("_BootMask")!=0)throw new Exception("Removed boot still clips stocking / foot: "+index);
                        }
                    }
                    type.GetField("elapsed",flags).SetValue(story,27f);type.GetMethod("Update",flags).Invoke(story,null);
                    if(rig.bag || rig.leftSlipper || rig.rightSlipper || rig.leftShoe.activeSelf || rig.rightShoe.activeSelf)throw new Exception("Play mode character footwear failed: "+index);
                    story.Replay();
                    if(rig.transform.position.z<3.8f || !rig.leftShoe.activeSelf)throw new Exception("Play mode character replay failed: "+index);
                }
                Debug.Log("HOMECOMING CHARACTER PLAY MODE PASSED: all seven models selected by actual UI callbacks, footwear and replay.");
                Debug.Log("HOMECOMING PLAY MODE PASSED: outside waiting, retargeted walk, hand-held shoes, ground release, standing pose, replay.");
                EditorApplication.ExitPlaymode();
            }
            catch(Exception error) { Debug.LogException(error); SessionState.SetBool(Running,false); EditorApplication.Exit(1); }
        }
        static void CheckMobileInput(HomecomingScene story)
        {
            var ui=story.GetComponentInChildren<HomecomingMobileUI>();
            if(!ui) throw new Exception("Mobile UI missing in Play Mode.");
            var buttons=ui.GetComponentsInChildren<Button>();
            buttons[0].onClick.Invoke(); buttons[0].onClick.Invoke();
            if(!story.Paused) throw new Exception("Runtime pause failed.");
            buttons[0].onClick.Invoke(); if(story.Paused) throw new Exception("Runtime resume failed.");
            buttons[3].onClick.Invoke();
            var controls=story.CameraControls;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(HomecomingCameraController); var read=type.GetMethod("ReadInput",flags);
            var yaw=type.GetField("yaw",flags); var distance=type.GetField("distance",flags);
            var touchscreen=InputSystem.AddDevice<Touchscreen>();
            void Touch(int id,UnityEngine.InputSystem.TouchPhase phase,Vector2 position)
            {
                InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=id,phase=phase,position=position});
                InputSystem.Update(); read.Invoke(controls,null);
            }
            var center=new Vector2(Screen.width*.5f,Screen.height*.5f);
            float before=(float)yaw.GetValue(controls);
            Touch(1,UnityEngine.InputSystem.TouchPhase.Began,center);
            Touch(1,UnityEngine.InputSystem.TouchPhase.Moved,center+new Vector2(40,0));
            Touch(1,UnityEngine.InputSystem.TouchPhase.Moved,center+new Vector2(80,0));
            if(Mathf.Abs((float)yaw.GetValue(controls)-before)<1) throw new Exception("Single-finger touch did not orbit.");
            Touch(2,UnityEngine.InputSystem.TouchPhase.Began,center-new Vector2(120,0));
            float beforeZoom=(float)distance.GetValue(controls);
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,center-new Vector2(180,0));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Moved,center-new Vector2(220,0));
            if(Mathf.Abs((float)distance.GetValue(controls)-beforeZoom)<.05f) throw new Exception("Pinch did not zoom.");
            Touch(1,UnityEngine.InputSystem.TouchPhase.Ended,center+new Vector2(80,0));
            Touch(2,UnityEngine.InputSystem.TouchPhase.Canceled,center-new Vector2(220,0));
            Canvas.ForceUpdateCanvases();
            var rect=buttons[0].GetComponent<RectTransform>();
            var buttonPoint=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            before=(float)yaw.GetValue(controls);
            Touch(3,UnityEngine.InputSystem.TouchPhase.Began,buttonPoint);
            Touch(3,UnityEngine.InputSystem.TouchPhase.Moved,buttonPoint+new Vector2(150,0));
            Touch(3,UnityEngine.InputSystem.TouchPhase.Moved,buttonPoint+new Vector2(250,0));
            if(Mathf.Abs((float)yaw.GetValue(controls)-before)>.01f) throw new Exception("UI touch also moved the camera.");
            Touch(3,UnityEngine.InputSystem.TouchPhase.Canceled,buttonPoint);
            InputSystem.RemoveDevice(touchscreen);
            Debug.Log("HOMECOMING TOUCH INPUT PASSED: real touch events orbit, pinch, cancellation and UI gesture exclusion.");
        }
    }
}
