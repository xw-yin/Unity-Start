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
        public enum FootwearKind { Shoe, AnkleBoot, TallBoot }
        public FootwearKind footwearKind;
        public SkinnedMeshRenderer[] anatomicalLegs, hosiery;
        public GameObject leftGroundShoe, rightGroundShoe;
        public SkinnedMeshRenderer[] expressionRenderers;
        public Vector3 hipRestPosition=new Vector3(0,.91f,0);
        [System.Serializable] public struct RestPose { public Transform bone; public Quaternion rotation,parentBasis; }
        public RestPose[] restPoses;
        public float LeftDressLift { get; private set; }
        public float RightDressLift { get; private set; }
        readonly int[] hemIndices={-1,-1};
        readonly Vector3[] hemRest=new Vector3[2],hemSit=new Vector3[2],hemLift=new Vector3[2];
        public void RemoveHomeAccessories()
        {
            foreach(var accessory in new[]{bag,leftSlipper,rightSlipper})
            {
                if(!accessory)continue;
                accessory.SetActive(false);
                if(Application.isPlaying)Destroy(accessory);else DestroyImmediate(accessory);
            }
            bag=null;leftSlipper=null;rightSlipper=null;
        }
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
        public void SetDressLift(float left,float right)
        {
            LeftDressLift=Mathf.Clamp01(left);RightDressLift=Mathf.Clamp01(right);
            if(!skirt||!skirt.sharedMesh)return;
            int a=skirt.sharedMesh.GetBlendShapeIndex("BootLeft"),b=skirt.sharedMesh.GetBlendShapeIndex("BootRight");
            if(a>=0)skirt.SetBlendShapeWeight(a,Mathf.Clamp01(left)*100);
            if(b>=0)skirt.SetBlendShapeWeight(b,Mathf.Clamp01(right)*100);
        }
        public Vector3 DressHoldPoint(int side)
        {
            if(!skirt)return (side==0?leftCalf:rightCalf).position;
            var solver=skirt.GetComponent<HomecomingGarmentDynamics>();
            var mesh=solver&&solver.SourceMesh?solver.SourceMesh:skirt.sharedMesh;
            if(hemIndices[side]<0)
            {
                var v=mesh.vertices;float closest=float.MaxValue;
                // Grip the front fold near the knee; grabbing the floor-length
                // bottom edge from a seated pose would exceed arm reach.
                var wanted=new Vector3(side==0?-.06f:.06f,.80f,.08f);
                for(int i=0;i<v.Length;i++)
                {
                    if(v[i].z<.02f||v[i].y>.86f)continue;
                    float score=(v[i]-wanted).sqrMagnitude;
                    if(score<closest){closest=score;hemIndices[side]=i;}
                }
                if(hemIndices[side]<0)throw new System.InvalidOperationException("The gown has no front hem");
                int index=hemIndices[side];hemRest[side]=v[index];
                var delta=new Vector3[v.Length];
                mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex("Sitting"),0,delta,null,null);hemSit[side]=delta[index];
                mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex(side==0?"BootLeft":"BootRight"),0,delta,null,null);hemLift[side]=delta[index];
            }
            var p=hemRest[side]+hemSit[side]*skirt.GetBlendShapeWeight(mesh.GetBlendShapeIndex("Sitting"))*.01f+hemLift[side]*(side==0?LeftDressLift:RightDressLift);
            var weight=mesh.boneWeights[hemIndices[side]];var bones=skirt.bones;var binds=mesh.bindposes;
            return (bones[weight.boneIndex0].localToWorldMatrix*binds[weight.boneIndex0]).MultiplyPoint3x4(p)*weight.weight0+
                   (bones[weight.boneIndex1].localToWorldMatrix*binds[weight.boneIndex1]).MultiplyPoint3x4(p)*weight.weight1+
                   (bones[weight.boneIndex2].localToWorldMatrix*binds[weight.boneIndex2]).MultiplyPoint3x4(p)*weight.weight2+
                   (bones[weight.boneIndex3].localToWorldMatrix*binds[weight.boneIndex3]).MultiplyPoint3x4(p)*weight.weight3;
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
