using UnityEngine;

namespace Homecoming
{
    public class CharacterRig : MonoBehaviour
    {
        public Transform hips,spine,head,leftArm,rightArm,leftElbow,rightElbow,leftThigh,rightThigh,leftCalf,rightCalf;
        public GameObject leftShoe,rightShoe,bag,leftSlipper,rightSlipper;
        public SkinnedMeshRenderer skirt;
        public bool importedSource;
        public bool longBoots;
        public GameObject leftGroundShoe, rightGroundShoe;
        public SkinnedMeshRenderer[] expressionRenderers;
        public Vector3 hipRestPosition=new Vector3(0,.91f,0);
        [System.Serializable] public struct RestPose { public Transform bone; public Quaternion rotation,parentBasis; }
        public RestPose[] restPoses;
        public void CaptureRestPose()
        {
            hipRestPosition=hips.localPosition;
            var bones=GetComponentsInChildren<Transform>(true); restPoses=new RestPose[bones.Length];
            for(int i=0;i<bones.Length;i++) restPoses[i]=new RestPose{bone=bones[i],rotation=bones[i].localRotation,parentBasis=bones[i].parent?Quaternion.Inverse(transform.rotation)*bones[i].parent.rotation:Quaternion.identity};
        }
        public void Pose(Transform bone,Vector3 angles)
        {
            if(importedSource&&restPoses!=null)
                foreach(var pose in restPoses) if(pose.bone==bone) { bone.localRotation=Quaternion.Inverse(pose.parentBasis)*Quaternion.Euler(angles)*pose.parentBasis*pose.rotation; return; }
            bone.localRotation=Quaternion.Euler(angles);
        }
        public void SetSkirtPose(float amount)
        {
            if(!skirt||!skirt.sharedMesh) return;
            int shape=skirt.sharedMesh.GetBlendShapeIndex("Sitting");
            if(shape>=0) skirt.SetBlendShapeWeight(shape,Mathf.Clamp01(amount)*100);
        }
        public Vector3 HipPosition(float sitting)
        {
            var offset=new Vector3(0,(.61f-hipRestPosition.y)*sitting,.12f*sitting);
            if(importedSource&&restPoses!=null) foreach(var pose in restPoses) if(pose.bone==hips) return hipRestPosition+Quaternion.Inverse(pose.parentBasis)*offset;
            return hipRestPosition+offset;
        }
        public void Expression(float time,bool happy)
        {
            if(expressionRenderers==null) return;
            float interval=Mathf.Repeat(time,4.3f),blink=Mathf.Clamp01(1-Mathf.Abs(interval-.11f)/.11f)*100;
            foreach(var renderer in expressionRenderers)
            {
                for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++)
                {
                    string name=renderer.sharedMesh.GetBlendShapeName(i);
                    renderer.SetBlendShapeWeight(i,name=="blink"?blink:name=="joy"?(happy?22:5):0);
                }
            }
        }
        public void Neutral()
        {
            if(importedSource&&restPoses!=null) { foreach(var pose in restPoses) if(pose.bone) pose.bone.localRotation=pose.rotation; return; }
            foreach(var joint in new[]{spine,head,leftArm,rightArm,leftElbow,rightElbow,leftThigh,rightThigh,leftCalf,rightCalf}) joint.localRotation=Quaternion.identity;
        }
    }
}
