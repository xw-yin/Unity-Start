using System.Linq;
using UnityEngine;
namespace Homecoming
{
    // Hand axes and contact anchors come from the imported finger skeleton.
    public sealed class HomecomingHandPose
    {
        public enum Hold { Rest, Boot, Fabric, Support, Greeting }
        readonly Transform[] hands=new Transform[2];
        readonly Transform[,,] fingers=new Transform[2,5,3];
        readonly Quaternion[,,] rest=new Quaternion[2,5,3];
        readonly Quaternion[,,,] poses=new Quaternion[2,5,5,3];
        readonly Vector3[,] forward=new Vector3[2,5],tipForward=new Vector3[2,5];
        readonly Vector3[,,] segment=new Vector3[2,5,3];
        readonly float[,] tipLength=new float[2,5];
        readonly Vector3[,] tipOffsets=new Vector3[2,5];
        readonly Vector3[] normal=new Vector3[2];
        readonly Quaternion[] basis=new Quaternion[2];
        readonly Vector3[,] offsets=new Vector3[2,5];
        static readonly string[] Names={"Index","Middle","Ring","Little","Thumb"};
        public HomecomingHandPose(CharacterRig rig)
        {
            var all=rig.GetComponentsInChildren<Transform>(true);
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"J_Bip_L_":"J_Bip_R_";
                hands[side]=all.FirstOrDefault(t=>t.name==prefix+"Hand");
                if(!hands[side])continue;
                for(int f=0;f<5;f++)for(int joint=0;joint<3;joint++)
                {
                    var bone=all.FirstOrDefault(t=>t.name==prefix+Names[f]+(joint+1));
                    fingers[side,f,joint]=bone;
                    if(bone)rest[side,f,joint]=bone.localRotation;
                }
                var hand=hands[side];var middle=fingers[side,1,0];
                var index=fingers[side,0,0];var little=fingers[side,3,0];
                Vector3 direction=middle?hand.InverseTransformPoint(middle.position).normalized:Vector3.down;
                var n=index&&little?hand.InverseTransformDirection(Vector3.Cross(index.position-hand.position,little.position-hand.position)).normalized:Vector3.forward;
                if(side==1)n=-n;normal[side]=n;
                basis[side]=Quaternion.LookRotation(n,direction);
                for(int f=0;f<5;f++)
                {
                    var a=fingers[side,f,0];var b=fingers[side,f,1];
                    forward[side,f]=a&&b?Vector3.ProjectOnPlane(hand.InverseTransformDirection(b.position-a.position),n).normalized:direction;
                    for(int j=0;j<3;j++)
                    {
                        var joint=fingers[side,f,j];var next=j<2?fingers[side,f,j+1]:null;
                        if(joint)segment[side,f,j]=joint.InverseTransformDirection(next?next.position-joint.position:joint.position-fingers[side,f,1].position).normalized;
                    }
                    var tip=fingers[side,f,2];
                    if(tip&&b){tipForward[side,f]=tip.InverseTransformDirection((tip.position-b.position).normalized);tipLength[side,f]=Vector3.Distance(tip.position,b.position)*.7f;tipOffsets[side,f]=tipForward[side,f]*tipLength[side,f];}
                }
                CalibrateTips(rig,side);
                for(int hold=0;hold<5;hold++)
                {
                    BuildFingers(side,(Hold)hold,hold==0?0:1);
                    for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(fingers[side,f,j])poses[side,hold,f,j]=fingers[side,f,j].localRotation;
                    offsets[side,hold]=hand.InverseTransformPoint(Contact(side,(Hold)hold));
                }
                for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(fingers[side,f,j])fingers[side,f,j].localRotation=rest[side,f,j];
            }
        }
        public Transform Hand(int side)=>hands[side];
        public Vector3 Palm(int side,Quaternion rotation)=>rotation*normal[side];
        public Vector3 Direction(int side,Quaternion rotation)=>rotation*(basis[side]*Vector3.up);
        public Quaternion Rotation(int side,Vector3 palm,Vector3 direction)
        {
            direction=Vector3.ProjectOnPlane(direction,palm).normalized;
            if(direction.sqrMagnitude<.01f)direction=Vector3.ProjectOnPlane(Vector3.down,palm).normalized;
            return Quaternion.LookRotation(palm,direction)*Quaternion.Inverse(basis[side]);
        }
        public Quaternion LimitWrist(int side,Quaternion rotation,Vector3 forearm,float maximum)
        {
            var forward=Vector3.RotateTowards(forearm,Direction(side,rotation),maximum*Mathf.Deg2Rad,0);
            var palm=Vector3.ProjectOnPlane(rotation*normal[side],forward).normalized;
            return Rotation(side,palm,forward);
        }
        public Quaternion BootRotation(int side,Quaternion boot)=>Rotation(side,boot*((side==0?Vector3.left:Vector3.right)+Vector3.forward).normalized,boot*Vector3.down);
        public Quaternion RelaxedRotation(int side,Quaternion root)=>Rotation(side,root*(side==0?Vector3.left:Vector3.right),Vector3.down);
        public Vector3 Wrist(int side,Hold hold,Vector3 contact,Quaternion rotation)=>contact-rotation*offsets[side,(int)hold];
        public Vector3 Contact(int side,Hold hold)
        {
            var hand=hands[side];if(!hand)return Vector3.zero;
            if(hold==Hold.Fabric)return (Tip(side,0)+Tip(side,4))*.5f;
            if(hold==Hold.Boot)return (Tip(side,0)+Tip(side,4))*.5f;
            var middle=fingers[side,1,0];return middle?Vector3.Lerp(hand.position,middle.position,.65f):hand.position;
        }
        public float PinchGap(int side)=>Vector3.Distance(Tip(side,0),Tip(side,4));
        void CalibrateTips(CharacterRig rig,int side)
        {
            var samples=new System.Collections.Generic.List<Vector3>[5];
            for(int f=0;f<5;f++)samples[f]=new System.Collections.Generic.List<Vector3>();
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var bones=skin.bones;var ids=Enumerable.Range(0,5).Select(f=>System.Array.IndexOf(bones,fingers[side,f,2])).ToArray();
                if(ids.All(i=>i<0))continue;
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var matrices=bones.Select((b,i)=>b.localToWorldMatrix*mesh.bindposes[i]).ToArray();
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];int digit=-1;
                    for(int f=0;f<5;f++)if(ids[f]>=0&&((w.boneIndex0==ids[f]&&w.weight0>.5f)||(w.boneIndex1==ids[f]&&w.weight1>.5f)||(w.boneIndex2==ids[f]&&w.weight2>.5f)||(w.boneIndex3==ids[f]&&w.weight3>.5f))){digit=f;break;}
                    if(digit<0)continue;var p=vertices[i];
                    samples[digit].Add(matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3);
                }
            }
            for(int f=0;f<5;f++)
            {
                if(samples[f].Count<4)continue;var tip=fingers[side,f,2];var direction=tip.TransformDirection(tipForward[side,f]);
                float end=samples[f].Max(p=>Vector3.Dot(p-tip.position,direction));
                var cap=samples[f].Where(p=>Vector3.Dot(p-tip.position,direction)>=end-.003f).ToArray();
                tipOffsets[side,f]=tip.InverseTransformPoint(cap.Aggregate(Vector3.zero,(sum,p)=>sum+p)/cap.Length);
            }
        }
        Vector3 Tip(int side,int finger)
        {
            var tip=fingers[side,finger,2];
            if(!tip)return hands[side].position;
            return tip.TransformPoint(tipOffsets[side,finger]);
        }
        public void Fingers(int side,Hold hold,float amount)
        {
            // Author each complete pose once. Blend local joint rotations, so
            // opening the grip cannot switch between iterative IK solutions.
            amount=Mathf.Clamp01(amount);
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(fingers[side,f,j])
                fingers[side,f,j].localRotation=Quaternion.Slerp(poses[side,0,f,j],poses[side,(int)hold,f,j],amount);
        }
        public void Overlay(int side,Hold hold,float amount,float weight)
        {
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(fingers[side,f,j])
            {
                var target=Quaternion.Slerp(poses[side,0,f,j],poses[side,(int)hold,f,j],Mathf.Clamp01(amount));
                fingers[side,f,j].localRotation=Quaternion.Slerp(fingers[side,f,j].localRotation,target,Mathf.Clamp01(weight));
            }
        }
        void BuildFingers(int side,Hold hold,float amount)
        {
            var hand=hands[side];if(!hand)return;
            amount=Mathf.Clamp01(amount);
            for(int f=0;f<5;f++)
            {
                float cumulative=0;
                for(int j=0;j<3;j++)
                {
                    var bone=fingers[side,f,j];if(!bone)continue;
                    bone.localRotation=rest[side,f,j];
                    // Rest uses a loose cascade, with most curvature in the
                    // first two joints and a soft fingertip, not a grip curl.
                    float relaxed=j==0?10+f*2:j==1?14+f*2:5;
                    // Index/thumb define the pinch; the remaining digits support
                    // the rim with a softer cascade instead of four equal hooks.
                    float closed=hold==Hold.Boot?(j==0?28+f*2.2f:j==1?62-f*5.5f:35-f*3):hold==Hold.Fabric?(f==0?(j==0?25:j==1?42:20):(j==0?20:j==1?30:14)):(j==0?8:j==1?12:6);
                    if(f==4){relaxed=j==0?8:6;closed=hold==Hold.Boot?(j==0?25:30):hold==Hold.Fabric?(j==0?25:30):10;}
                    if(hold==Hold.Greeting)closed=f==4?8:j==0?5+f*2:j==1?8+f*2:3;
                    cumulative+=Mathf.Lerp(relaxed,closed,amount);
                    var baseDirection=forward[side,f];
                    var axis=Vector3.Cross(baseDirection,normal[side]).normalized;
                    var desired=hand.TransformDirection(Quaternion.AngleAxis(cumulative,axis)*baseDirection);
                    // Align the actual segment, including the bend already in
                    // its bind geometry, instead of adding rotations to a curl.
                    bone.rotation=Quaternion.FromToRotation(bone.TransformDirection(segment[side,f,j]),desired)*bone.rotation;
                }
                if(f==4&&amount>0&&hold!=Hold.Greeting&&fingers[side,0,1])
                {
                    var thumb=fingers[side,4,0];var target=fingers[side,0,1].position;
                    var oppose=Quaternion.FromToRotation(Tip(side,4)-thumb.position,target-thumb.position);
                    thumb.rotation=Quaternion.Slerp(Quaternion.identity,oppose,amount*.6f)*thumb.rotation;
                }
            }
            if((hold==Hold.Fabric||hold==Hold.Boot)&&amount>0&&fingers[side,0,0]&&fingers[side,4,0])
            {
                // Opposing finger pads meet around the cloth; a midpoint between
                // two distant fingertips is not a physical pinch.
                for(int pass=0;pass<8;pass++)
                {
                    var point=(Tip(side,0)+Tip(side,4))*.5f;
                    var separation=hand.TransformDirection(normal[side])*(hold==Hold.Boot?.0035f:.003f);
                    for(int digit=0;digit<2;digit++)
                    {
                        int f=digit==0?0:4;var root=fingers[side,f,0];
                        var target=point+(f==0?separation:-separation);
                        var turn=Quaternion.FromToRotation(Tip(side,f)-root.position,target-root.position);
                        root.rotation=Quaternion.Slerp(Quaternion.identity,turn,amount*.65f)*root.rotation;
                    }
                }
            }
        }
    }
}
