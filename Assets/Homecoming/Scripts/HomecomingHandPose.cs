using System.Linq;
using UnityEngine;
namespace Homecoming
{
    // Hand axes and contact anchors come from the imported finger skeleton.
    public sealed class HomecomingHandPose
    {
        public enum Hold { Rest, Boot, Fabric, Support }
        readonly Transform[] hands=new Transform[2];
        readonly Transform[,,] fingers=new Transform[2,5,3];
        readonly Quaternion[,,] rest=new Quaternion[2,5,3];
        readonly Vector3[,] forward=new Vector3[2,5],tipForward=new Vector3[2,5];
        readonly float[,] tipLength=new float[2,5];
        readonly Vector3[] normal=new Vector3[2];
        readonly Quaternion[] basis=new Quaternion[2];
        readonly Vector3[,] offsets=new Vector3[2,4];
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
                if(n.z<0)n=-n;normal[side]=n;
                basis[side]=Quaternion.LookRotation(n,direction);
                for(int f=0;f<5;f++)
                {
                    var a=fingers[side,f,0];var b=fingers[side,f,1];
                    forward[side,f]=a&&b?hand.InverseTransformDirection(b.position-a.position).normalized:direction;
                    var tip=fingers[side,f,2];
                    if(tip&&b){tipForward[side,f]=tip.InverseTransformDirection((tip.position-b.position).normalized);tipLength[side,f]=Vector3.Distance(tip.position,b.position)*.7f;}
                }
                for(int hold=0;hold<4;hold++)
                {
                    Fingers(side,(Hold)hold,hold==0?0:1);
                    offsets[side,hold]=hand.InverseTransformPoint(Contact(side,(Hold)hold));
                }
                for(int f=0;f<5;f++)for(int j=0;j<3;j++)if(fingers[side,f,j])fingers[side,f,j].localRotation=rest[side,f,j];
            }
        }
        public Transform Hand(int side)=>hands[side];
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
        public Quaternion BootRotation(int side,Quaternion boot)=>Rotation(side,boot*Vector3.forward,boot*Vector3.down);
        public Quaternion RelaxedRotation(int side,Quaternion root)=>Rotation(side,root*(side==0?Vector3.left:Vector3.right),Vector3.down);
        public Vector3 Wrist(int side,Hold hold,Vector3 contact,Quaternion rotation)=>contact-rotation*offsets[side,(int)hold];
        public Vector3 Contact(int side,Hold hold)
        {
            var hand=hands[side];if(!hand)return Vector3.zero;
            if(hold==Hold.Fabric)return (Tip(side,0)+Tip(side,4))*.5f;
            if(hold==Hold.Boot)
            {
                Vector3 sum=Vector3.zero;int count=0;
                for(int f=0;f<3;f++)if(fingers[side,f,1]){sum+=fingers[side,f,1].position;count++;}
                return count>0?sum/count:hand.position;
            }
            var middle=fingers[side,1,0];return middle?Vector3.Lerp(hand.position,middle.position,.65f):hand.position;
        }
        public float PinchGap(int side)=>Vector3.Distance(Tip(side,0),Tip(side,4));
        Vector3 Tip(int side,int finger)
        {
            var tip=fingers[side,finger,2];
            if(!tip)return hands[side].position;
            return tip.position+tip.TransformDirection(tipForward[side,finger])*tipLength[side,finger];
        }
        public void Fingers(int side,Hold hold,float amount)
        {
            var hand=hands[side];if(!hand)return;
            amount=Mathf.Clamp01(amount);
            for(int f=0;f<5;f++)for(int j=0;j<3;j++)
            {
                var bone=fingers[side,f,j];if(!bone)continue;
                float relaxed=j==0?7:j==1?14:8;
                float closed=hold==Hold.Boot?(j==0?18:j==1?48:28):hold==Hold.Fabric?(f==0?(j==0?24:j==1?50:32):(j==0?22:j==1?40:26)):(j==0?10:j==1?22:12);
                if(f==4){relaxed=j==0?2:5;closed=hold==Hold.Fabric?(j==0?16:30):hold==Hold.Boot?(j==0?12:24):6;}
                var axis=Vector3.Cross(forward[side,f],normal[side]).normalized;
                axis=bone.parent.InverseTransformDirection(hand.TransformDirection(axis));
                bone.localRotation=Quaternion.AngleAxis(Mathf.Lerp(relaxed,closed,amount),axis)*rest[side,f,j];
                // Oppose the thumb toward the index, rather than curl it like a finger.
                if(f==4&&j==0&&fingers[side,0,0])
                {
                    var thumbDirection=hand.TransformDirection(forward[side,4]);
                    var toward=Vector3.ProjectOnPlane(fingers[side,0,0].position-bone.position,hand.TransformDirection(normal[side])).normalized;
                    var oppose=Quaternion.FromToRotation(thumbDirection,toward);
                    bone.rotation=Quaternion.Slerp(Quaternion.identity,oppose,amount*.35f)*bone.rotation;
                }
            }
            if(hold==Hold.Fabric&&amount>0&&fingers[side,0,0]&&fingers[side,4,0])
            {
                // Opposing finger pads meet around the cloth; a midpoint between
                // two distant fingertips is not a physical pinch.
                for(int pass=0;pass<4;pass++)
                {
                    var point=(Tip(side,0)+Tip(side,4))*.5f;
                    for(int digit=0;digit<2;digit++)
                    {
                        int f=digit==0?0:4;var root=fingers[side,f,0];
                        var turn=Quaternion.FromToRotation(Tip(side,f)-root.position,point-root.position);
                        root.rotation=Quaternion.Slerp(Quaternion.identity,turn,amount*.65f)*root.rotation;
                    }
                }
            }
        }
    }
}
