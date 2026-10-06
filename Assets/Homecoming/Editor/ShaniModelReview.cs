using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Homecoming.Editor
{
    public static class ShaniModelReview
    {
        public static void ReimportAndReview(){WitcherCharacterImport.Import("Shani");Review();}
        public static void FinalReview(){Review();HandMovementReview.Validate();HomecomingFootwearReview.Run();HomecomingContinuousReview.Run();}
        [MenuItem("Homecoming/检查夏妮手臂眼睛白袜")]
        public static void Review()
        {
            HomecomingSetup.CreateScene();var story=UnityEngine.Object.FindAnyObjectByType<HomecomingScene>();story.enabled=false;story.SelectCharacter(1);
            var rig=story.GetComponentInChildren<CharacterRig>();var renderers=rig.GetComponentsInChildren<SkinnedMeshRenderer>();
            var eyes=renderers.Single(r=>r.name=="he_01_wa__shani_lod0_12_Body");
            var shadow=renderers.Single(r=>r.name=="he_01_wa__shani_lod0_13_Body");
            if(!eyes.enabled||!eyes.sharedMaterial.GetTexture("_BaseMap")||shadow.enabled)throw new Exception("Iris is missing or covered by the opaque eye-shadow pass.");
            var arms=renderers.Single(r=>r.name.StartsWith("ta_01_wa__body_shani"));
            var vertices=arms.sharedMesh.vertices;
            foreach(int sign in new[]{-1,1})if(!vertices.Any(v=>v.x*sign>.17f&&v.x*sign<.21f&&v.y>1.2f))throw new Exception("Original upper arm is absent.");
            for(int side=0;side<2;side++)
            {
                var sock=rig.hosiery[side];var prefix=side==0?"W3_l_0":"W3_r_0";int ribbonVertices=0;
                foreach(var weight in sock.sharedMesh.boneWeights)
                {
                    var ids=new[]{weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};var ws=new[]{weight.weight0,weight.weight1,weight.weight2,weight.weight3};bool ribbon=false;
                    for(int i=0;i<4;i++)if(ws[i]>0&&sock.bones[ids[i]].name.StartsWith(prefix))
                    {ribbon=true;if(!sock.bones[ids[i]].IsChildOf(side==0?rig.leftCalf:rig.rightCalf))throw new Exception("Sock ribbon is not attached to its calf.");}
                    if(ribbon)ribbonVertices++;
                }
                if(ribbonVertices<300)throw new Exception("Original sock ribbon tails were discarded: "+ribbonVertices);
                Debug.Log("SHANI SOCK RIBBON VERTICES: "+side+"="+ribbonVertices);
            }
            var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(HomecomingScene).GetField("started",flags).SetValue(story,true);
            Directory.CreateDirectory("Artifacts/ShaniModelReview");
            for(int f=0;f<=810;f++)
            {
                typeof(HomecomingScene).GetField("elapsed",flags).SetValue(story,f/30f-Time.deltaTime);typeof(HomecomingScene).GetMethod("Update",flags).Invoke(story,null);
                if(!new[]{90,300,390,450,570,630,810}.Contains(f))continue;
                var camera=story.CameraControls.GetComponent<Camera>();
                void Shot(string name,Vector3 position,Vector3 target,float fov){camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fov;HomecomingSetup.Capture(camera,"Artifacts/ShaniModelReview/"+f+"-"+name+".png",1100,1000);}
                Shot("pose",rig.transform.position+new Vector3(.85f,1.15f,-1.8f),rig.hips.position+new Vector3(0,.08f,-.08f),46);
                if(f==90)Shot("eyes",rig.head.position+new Vector3(.15f,.1f,-.65f),rig.head.position+new Vector3(0,.055f,-.025f),30);
                if(f==390||f==90)
                {var point=Vector3.Lerp(rig.leftArm.position,rig.leftElbow.position,.75f);Shot("arm",point+new Vector3(.55f,.06f,-.55f),point,40);}
                if(f==570){var point=Vector3.Lerp(rig.rightArm.position,rig.rightElbow.position,.75f);Shot("arm",point+new Vector3(-.55f,.06f,-.55f),point,40);}
                if(f>=630){var point=(rig.leftCalf.position+rig.rightCalf.position)*.5f+Vector3.down*.15f;Shot("socks",point+new Vector3(.55f,.12f,-1.05f),point,38);}
            }
            Debug.Log("SHANI MODEL REVIEW PASSED: original arm coverage, visible iris, original sock ribbons retained and bound to calves.");
        }
    }
}
