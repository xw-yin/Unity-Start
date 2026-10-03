using System;
using System.Linq;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class HomecomingFootwearReview
    {
        public static void Run()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();
            for(int index=0;index<HomecomingCharacters.Names.Length;index++)
            {
                story.SelectCharacter(index);var rig=story.GetComponentInChildren<CharacterRig>();
                var legs=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMesh.name.StartsWith("OriginalGame")).ToArray();
                if(legs.Length!=2||legs.Any(s=>s.sharedMesh.vertexCount<100||s.transform.IsChildOf(rig.leftShoe.transform)||s.transform.IsChildOf(rig.rightShoe.transform)))throw new Exception("Original game legs are absent or attached to the removable shoe: "+index);
                var meshes=legs.Select(s=>s.sharedMesh).ToArray();
                var motion=new HomecomingMotion(rig);
                foreach(float time in new[]{0f,10.95f,13.4f,19.4f,27f})
                {
                    motion.Sample(time);
                    if(legs.Any(s=>!s.gameObject.activeSelf)||!meshes.SequenceEqual(legs.Select(s=>s.sharedMesh)))throw new Exception("Hosiery changes when a shoe is removed");
                }
                if(rig.skirt.sharedMesh.GetBlendShapeIndex("BootLeft")>=0)
                {
                    motion.Sample(10.95f);
                    if(rig.LeftDressLift<.99f)throw new Exception("Shoe reach began before the gown was raised");
                    var hand=rig.GetComponentsInChildren<Transform>().Single(t=>t.name=="J_Bip_R_Hand");
                    float error=Vector3.Distance(hand.position,rig.DressHoldPoint(0)+new Vector3(0,.055f,-.015f));
                    if(error>.06f)throw new Exception("The free hand does not hold the raised gown: "+index+" error="+error+" hand="+hand.position+" hem="+rig.DressHoldPoint(0));
                    motion.Sample(16.95f);
                    if(rig.RightDressLift<.99f)throw new Exception("Right shoe reach began before the gown was raised");
                }
                // The duchess' original stocking submesh must stay in the body.
                if(index==6)
                {
                    var stocking=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name.StartsWith("l_02_wa__anna_henrietta")&&s.name.EndsWith("_Body"));
                    if(stocking.transform.IsChildOf(rig.leftShoe.transform)||stocking.transform.IsChildOf(rig.rightShoe.transform))throw new Exception("Original duchess stockings were merged with shoes");
                }
            }
            Debug.Log("FOOTWEAR REVIEW PASSED: seven original-game lower bodies; retained stockings; gown lifted before shoe reach; free-hand hem contact.");
        }
    }
}
