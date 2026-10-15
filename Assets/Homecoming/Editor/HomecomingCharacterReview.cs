using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Homecoming.Editor
{
    public static class HomecomingCharacterReview
    {
        public static void ImportAndReview() { WitcherCharacterImport.ImportAll();Run();HomecomingGarmentReview.Run(); }
        public static void ImportDLCAndReview() { WitcherCharacterImport.Import("Iris");WitcherCharacterImport.Import("Syanna");Run();HomecomingGarmentReview.Run(); }
        public static void ImportRevisionAndReview() { YenneferDLCImport.Import();WitcherCharacterImport.ImportAll();Run();HomecomingGarmentReview.Run();HomecomingFootwearReview.Run(); }
        public static void ImportDressAndFootwear() { WitcherCharacterImport.Import("Iris");WitcherCharacterImport.Import("AnnaHenrietta");HomecomingFootwearReview.Run(); }
        public static void ReviewExistingRevision() { HomecomingFootwearReview.Run();Run();HomecomingGarmentReview.Run(); }
        public static void Run()
        {
            HomecomingSetup.CreateScene();
            var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            var ui=HomecomingMobileUI.Create(story);
            var buttons=ui.GetComponentsInChildren<Button>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(HomecomingScene);
            var camera=story.CameraControls.GetComponent<Camera>();
            Directory.CreateDirectory("Artifacts/Characters");
            float worstReach=0;
            string worst="";
            foreach(int index in new[]{6,4,5,0,1,2,3,6,0})
            {
                buttons.Single(b=>b.name==HomecomingCharacters.Names[index]).onClick.Invoke();
                var rig=story.GetComponentInChildren<CharacterRig>();
                if(story.CharacterIndex!=index || !rig || rig.transform.position.z<3.8f || story.Started || (float)type.GetField("elapsed",flags).GetValue(story)!=0)throw new Exception("Character switch failed: "+index);
                if(!rig.GetComponent<Animator>().avatar.isHuman)throw new Exception("Invalid character avatar");
                type.GetField("started",flags).SetValue(story,true);
                for(int frame=0;frame<=32*30;frame++)
                {
                    float t=frame/30f,action=t-1;
                    type.GetField("elapsed",flags).SetValue(story,t);type.GetMethod("Update",flags).Invoke(story,null);
                    var motion=(HomecomingMotion)type.GetField("motion",flags).GetValue(story);
                    action=story.ActionTime;
                    for(int side=0;side<2;side++)
                    {
                        if(action>=HomecomingShoeGrip.Detach(side) && action<=HomecomingShoeGrip.Release(side))
                        {
                            var hand=rig.GetComponentsInChildren<Transform>().Single(b=>b.name==(motion.Shoes.HandSide(side)==0?"J_Bip_L_Hand":"J_Bip_R_Hand"));
                            float error=Vector3.Distance(hand.position,motion.Shoes.WristTarget(side,action));
                            if(error>worstReach){worstReach=error;worst="character="+index+" time="+t+" side="+side+" hand="+hand.position+" target="+motion.Shoes.WristTarget(side,action)+" shoulder="+(motion.Shoes.HandSide(side)==0?rig.leftArm:rig.rightArm).position;}
                        }
                    }
                    if(frame==195 || frame==312 || frame==330 || frame==345 || frame==405 || frame==435 || frame==810)
                    {
                        story.CameraControls.SetView(1);story.CameraControls.SnapForReview();
                        HomecomingSetup.Capture(camera,"Artifacts/Characters/"+HomecomingCharacters.Names[index]+"-"+frame+".png",1000,800);
                    }
                }
                if(rig.bag || rig.leftSlipper || rig.rightSlipper || rig.leftShoe.activeSelf || rig.rightShoe.activeSelf)throw new Exception("Character footwear did not finish: "+index);
                story.Replay();
                if(rig.transform.position.z<3.8f || !rig.leftShoe.activeSelf || rig.bag || rig.leftSlipper || rig.rightSlipper)throw new Exception("Character replay failed: "+index);
            }
            if(worstReach>.04f)throw new Exception("Character boot reach exceeds tolerance: "+worstReach+" "+worst);
            Debug.Log("CHARACTER REVIEW PASSED: all 7 models, 9 UI switches, 8649 timeline samples, avatars, shoe removal, accessory removal and replay; maximum wrist error="+worstReach);
        }
    }
}
