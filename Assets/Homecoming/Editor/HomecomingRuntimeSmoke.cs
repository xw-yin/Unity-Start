using System;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
            EditorSceneManager.OpenScene("Assets/Homecoming/Scenes/Homecoming.unity");
            SessionState.SetBool(Running,true); EditorApplication.EnterPlaymode();
        }
        static void OnState(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Running,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) { frames=0; EditorApplication.update-=Check; EditorApplication.update+=Check; }
            if(state==PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Running,false); EditorApplication.Exit(0); }
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
                if(!AssetDatabase.Contains(rig.skirt.sharedMesh)) throw new Exception("运行时未加载保存的角色模型");
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var type=typeof(HomecomingScene); type.GetField("started",flags).SetValue(story,true);
                foreach(float time in new[]{1f,4.8f,7.2f,8.3f,10.6f,13.3f,14f,15.5f,19.3f,20f,21.5f,25.6f,32f})
                {
                    type.GetField("elapsed",flags).SetValue(story,time); type.GetMethod("Update",flags).Invoke(story,null);
                    var motion=(HomecomingMotion)type.GetField("motion",flags).GetValue(story);
                    for(int side=0;side<2;side++)
                    {
                        float action=time-1;
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
                if(!rig.leftSlipper.activeSelf||rig.leftShoe.activeSelf) throw new Exception("换鞋后站立状态错误");
                type.GetMethod("ResetStory",flags).Invoke(story,null);
                if(rig.transform.position.z<3.8f) throw new Exception("重播未恢复门外等待位置");
                if(!rig.leftShoe.activeSelf||rig.leftSlipper.activeSelf) throw new Exception("重播状态错误");
                if(rig.importedSource && (!rig.GetComponent<Animator>().avatar.isHuman)) throw new Exception("导入人物的表情或人形骨骼无效");
                Debug.Log("HOMECOMING PLAY MODE PASSED: outside waiting, retargeted walk, hand-held shoes, ground release, standing pose, replay.");
                EditorApplication.ExitPlaymode();
            }
            catch(Exception error) { Debug.LogException(error); SessionState.SetBool(Running,false); EditorApplication.Exit(1); }
        }
    }
}
