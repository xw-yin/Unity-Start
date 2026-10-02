using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Homecoming.Editor
{
    public static class HomecomingWalkBake
    {
        public static void BakeAndReview() { Bake(); HomecomingMotionReview.Run(); }
        public static void Bake()
        {
            const string source="Assets/SourceFiles/StarterAssets/ThirdPersonController/Character/Animations/Locomotion--Walk_N.anim.fbx";
            var clip=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().First(x=>!x.name.StartsWith("__"));
            var root=Object.Instantiate((Resources.Load<GameObject>("Homecoming/YenneferDLC") ?? Resources.Load<GameObject>("Homecoming/Yennefer") ?? Resources.Load<GameObject>("Homecoming/XiaVRoid")));
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var rig=root.GetComponent<CharacterRig>();
            var animator=root.GetComponent<Animator>();
            var coordinate=root.transform.Find("VRM坐标系");
            var description=animator.avatar.humanDescription;
            foreach(var rest in description.skeleton)
                foreach(var bone in root.GetComponentsInChildren<Transform>())
                    if(bone.name==rest.name && bone.name.StartsWith("J_Bip_")) { bone.localPosition=rest.position; bone.localRotation=rest.rotation; }
            coordinate.localRotation=Quaternion.identity;
            description.skeleton=root.GetComponentsInChildren<Transform>().Select(bone=>new SkeletonBone{name=bone.name,position=bone.localPosition,rotation=bone.localRotation,scale=bone.localScale}).ToArray();
            var calibrated=AvatarBuilder.BuildHumanAvatar(root,description);
            if(!calibrated.isHuman||!calibrated.isValid) throw new System.Exception("Walk calibration avatar is invalid");
            animator.avatar=calibrated; animator.enabled=true; animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.fireEvents=false;
            var graph=PlayableGraph.Create("Bake imported humanoid walk"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(graph,"Walk",animator);
            var playable=AnimationClipPlayable.Create(graph,clip); playable.SetApplyFootIK(false);
            output.SetSourcePlayable(playable); graph.Play();
            var bones=rig.restPoses.Where(x=>x.bone.name.StartsWith("J_Bip_")).Select(x=>x.bone).ToArray();
            var asset=ScriptableObject.CreateInstance<HomecomingWalkCycle>();
            asset.frames=60; asset.duration=clip.length; asset.source=source;
            asset.bones=bones.Select(x=>x.name).ToArray(); asset.rotations=new Quaternion[60*bones.Length]; asset.hipPositions=new Vector3[60];
            var lf=animator.GetBoneTransform(HumanBodyBones.LeftFoot); var lt=animator.GetBoneTransform(HumanBodyBones.LeftToes);
            var rf=animator.GetBoneTransform(HumanBodyBones.RightFoot); var rt=animator.GetBoneTransform(HumanBodyBones.RightToes);
            var shoe=rig.leftShoe.GetComponent<SkinnedMeshRenderer>(); var shoe2=rig.rightShoe.GetComponent<SkinnedMeshRenderer>();
            var mesh=new Mesh(); float spanMin=100,spanMax=-100;
            for(int frame=0;frame<60;frame++)
            {
                coordinate.localRotation=Quaternion.identity;
                playable.SetTime(frame*clip.length/60); graph.Evaluate(0);
                coordinate.localRotation=Quaternion.Euler(0,180,0);
                root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                if(frame==0)
                {
                    var direction=(lt.position-lf.position)+(rt.position-rf.position); direction.y=0;
                    asset.forward=direction.normalized;
                    Debug.Log("WALK RETARGET: length="+clip.length+" speed="+clip.averageSpeed+" scale="+animator.humanScale+" forward="+asset.forward);
                }
                // Normalize the retargeted avatar to the scene's existing -Z front.
                var correction=Quaternion.FromToRotation(asset.forward,Vector3.back);
                rig.hips.rotation=correction*rig.hips.rotation;
                var relative=root.transform.InverseTransformPoint(rig.hips.position); relative.x=0; relative.z=0;
                rig.hips.position=root.transform.TransformPoint(relative);
                var leftRotation=lf.rotation; var rightRotation=rf.rotation;
                var leftTarget=lf.position; var rightTarget=rf.position;
                leftTarget.z=rig.hips.position.z+(leftTarget.z-rig.hips.position.z)*.72f;
                rightTarget.z=rig.hips.position.z+(rightTarget.z-rig.hips.position.z)*.72f;
                HomecomingMotion.Solve(rig.leftThigh,rig.leftCalf,lf,leftTarget,rig.hips.position+new Vector3(.15f,-.05f,-1));
                HomecomingMotion.Solve(rig.rightThigh,rig.rightCalf,rf,rightTarget,rig.hips.position+new Vector3(-.15f,-.05f,-1));
                lf.rotation=leftRotation; rf.rotation=rightRotation;
                float minimum=100;
                foreach(var skin in new[]{shoe,shoe2})
                {
                    skin.BakeMesh(mesh);
                    foreach(var v in mesh.vertices) minimum=Mathf.Min(minimum,skin.transform.TransformPoint(v).y);
                }
                rig.hips.position+=Vector3.up*(.015f-minimum);
                float foot=lf.position.z-rig.hips.position.z;
                spanMin=Mathf.Min(spanMin,foot); spanMax=Mathf.Max(spanMax,foot);
                asset.hipPositions[frame]=rig.hips.localPosition;
                for(int i=0;i<bones.Length;i++) asset.rotations[frame*bones.Length+i]=bones[i].localRotation;
            }
            asset.forward=Vector3.back;
            // A cycle contains two steps; the contact foot travels this distance
            // relative to the pelvis, which sets distance-matched playback speed.
            asset.strideDistance=2*(spanMax-spanMin);
            Debug.Log("WALK BAKED: stride="+asset.strideDistance+" bones="+bones.Length);
            const string path="Assets/Homecoming/Resources/Homecoming/WalkCycle.asset";
            var existing=AssetDatabase.LoadAssetAtPath<HomecomingWalkCycle>(path);
            if(existing) { EditorUtility.CopySerialized(asset,existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(asset); }
            else AssetDatabase.CreateAsset(asset,path);
            AssetDatabase.SaveAssets(); graph.Destroy(); Object.DestroyImmediate(mesh); Object.DestroyImmediate(root); Object.DestroyImmediate(calibrated);
        }
    }
}
