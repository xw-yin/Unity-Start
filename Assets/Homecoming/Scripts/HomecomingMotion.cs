using UnityEngine;
using System.Linq;

namespace Homecoming
{
    // Contact targets are in scene space. The skeleton is solved after the pelvis,
    // so imported bone axes never determine where a foot or a hand ends up.
    public sealed class HomecomingMotion
    {
        readonly CharacterRig rig;
        readonly Transform lf,rf,lh,rh;
        readonly Transform[] shoulders=new Transform[2];
        readonly Quaternion[] upperArmFrames=new Quaternion[2],forearmFrames=new Quaternion[2];
        readonly Quaternion[] thighFrames=new Quaternion[2],calfFrames=new Quaternion[2];
        readonly Vector3[] priorLegDirection=new Vector3[2],priorLegBend=new Vector3[2];
        bool legContinuity;
        readonly Quaternion leftFootRest,rightFootRest;
        readonly Quaternion leftCalfRest,rightCalfRest;
        public const float Duration=27;
        const float BootLegLift=.28f,BootLegBack=.02f,BootLean=12,BootHipForward=.04f;
        readonly HomecomingWalkCycle walk;
        readonly Transform[] walkBones;
        readonly Vector3[] route=new Vector3[321];
        readonly float[] distances=new float[321];
        float routeLength;
        readonly Sole leftSole,rightSole;
        readonly Sole leftStockingSole,rightStockingSole;
        readonly HomecomingFootContact[] barefoot=new HomecomingFootContact[2];
        readonly HomecomingFootwearLast[] wornFeet=new HomecomingFootwearLast[2];
        readonly Quaternion[] flatFootRest=new Quaternion[2];
        public float HeelHeight(int side)=>barefoot[side]?.HeelHeight??0;
        public float BallHeight(int side)=>barefoot[side]?.BallHeight??0;
        float sampleAction;
        readonly HomecomingClothDynamics cloth;
        readonly HomecomingGarmentDynamics[] garments;
        readonly bool longDress;
        readonly bool wideGown;
        readonly bool dlcBoots;
        readonly SkinnedMeshRenderer[] bootFit;
        readonly int[] bootFitSides,bootFitIndices;
        readonly float[] thighClearance=new float[2];
        readonly Vector3[] handTargets=new Vector3[2];
        readonly Quaternion[] handRotations=new Quaternion[2];
        readonly HomecomingHandPose.Hold[] handModes=new HomecomingHandPose.Hold[2];
        readonly float[] bootFingerAmounts=new float[2],fingerAmounts=new float[2],fabricReach=new float[2],supportReach=new float[2];
        readonly Vector3[] elbowBend=new Vector3[2];
        readonly Vector3[] solvedDirection=new Vector3[2],solvedBend=new Vector3[2],priorDirection=new Vector3[2],priorBend=new Vector3[2];
        readonly Transform[] smileBones=new Transform[2];
        readonly Vector3[] smileRest=new Vector3[2];
        // Facial bones: Witcher characters ship zero facial blendshapes, so the
        // face is frozen unless these bones are driven. Same safe pattern as
        // smileBones: cache rest localPosition, apply millimeter-scale positional
        // nudges only (no rotations), all restored by ResetFace().
        readonly Transform[] eyelidBones=new Transform[4]; // 0,1 upper L/R; 2,3 lower L/R
        readonly Vector3[] eyelidRest=new Vector3[4];
        readonly Transform[] browBones=new Transform[2];
        readonly Vector3[] browRest=new Vector3[2];
        Transform jawBone; Vector3 jawRest;
        readonly Quaternion headRest;
        readonly HomecomingAuthoredPose endingPose;
        readonly Quaternion[] previousHandTurn=new Quaternion[2];
        float previousSample=-1,handStep;bool elbowsInitialized;
        public HomecomingShoeGrip Shoes { get; private set; }
        public HomecomingHandPose Hands { get; private set; }
        public void ResolveHeldContact(Transform proxy,int side,float action)
        {
            if(action<HomecomingShoeGrip.Detach(side)||action>=HomecomingShoeGrip.Release(side))return;
            var upper=side==0?rig.leftArm:rig.rightArm;
            var elbow=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
            var rotation=hand.rotation;
            var target=Hands.Wrist(side,HomecomingHandPose.Hold.Boot,Shoes.ProxyContact(proxy,side),rotation);
            if(shoulders[side])
            {
                // A small clavicle contribution keeps a reaching arm connected
                // to the torso instead of stretching the arm or moving the boot.
                float used=0;
                for(int pass=0;pass<3;pass++)
                {
                    float span=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position)-.002f;
                    float excess=Vector3.Distance(upper.position,target)-span;
                    if(excess<=0||used>=12)break;
                    var shoulder=shoulders[side];
                    float length=Vector3.Distance(upper.position,shoulder.position);
                    float angle=Mathf.Min(12-used,Mathf.Max(1,excess/Mathf.Max(.05f,length)*Mathf.Rad2Deg*1.4f));
                    var towards=Quaternion.FromToRotation(upper.position-shoulder.position,target-shoulder.position);
                    shoulder.rotation=Quaternion.RotateTowards(Quaternion.identity,towards,angle)*shoulder.rotation;
                    used+=angle;
                }
            }
            // Final contact is solved after the actual boot deformation and floor
            // placement; the independently placed prop remains fixed.
            // Retain the designed elbow plane through near-straight reaches;
            // the elbow position itself becomes an unstable pole there.
            // This pass corrects position only. Reconstructing a full roll frame
            // a second time can flip an almost straight arm by tens of degrees.
            Solve(upper,elbow,hand,target,upper.position+elbowBend[side]);
            hand.rotation=rotation;
        }
        sealed class Sole
        {
            readonly Vector3[] vertices;
            readonly BoneWeight[] weights;
            readonly Transform[] bones;
            readonly Matrix4x4[] bindposes,matrices;
            public Sole(SkinnedMeshRenderer skin)
            {
                vertices=skin.sharedMesh.vertices; weights=skin.sharedMesh.boneWeights;
                bones=skin.bones; bindposes=skin.sharedMesh.bindposes; matrices=new Matrix4x4[bones.Length];
            }
            public float Minimum()
            {
                for(int i=0;i<bones.Length;i++) matrices[i]=bones[i].localToWorldMatrix*bindposes[i];
                float minimum=float.MaxValue;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i]; var v=vertices[i];
                    float y=matrices[w.boneIndex0].MultiplyPoint3x4(v).y*w.weight0;
                    if(w.weight1>0) y+=matrices[w.boneIndex1].MultiplyPoint3x4(v).y*w.weight1;
                    if(w.weight2>0) y+=matrices[w.boneIndex2].MultiplyPoint3x4(v).y*w.weight2;
                    if(w.weight3>0) y+=matrices[w.boneIndex3].MultiplyPoint3x4(v).y*w.weight3;
                    minimum=Mathf.Min(minimum,y);
                }
                return minimum;
            }
        }
        public HomecomingMotion(CharacterRig source)
        {
            rig=source;garments=rig.GetComponentsInChildren<HomecomingGarmentDynamics>();
            dlcBoots=rig.leftShoe&&rig.leftShoe.GetComponent<SkinnedMeshRenderer>()&&rig.leftShoe.GetComponent<SkinnedMeshRenderer>().sharedMesh.name.StartsWith("OriginalYenneferBootDLC");
            bootFit=dlcBoots||rig.openToeFootwear?rig.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.sharedMesh.GetBlendShapeIndex("BootFit")>=0).ToArray():new SkinnedMeshRenderer[0];
            bootFitIndices=bootFit.Select(s=>s.sharedMesh.GetBlendShapeIndex("BootFit")).ToArray();
            bootFitSides=bootFit.Select(s=>s==rig.hosiery[0]||s==rig.anatomicalLegs[0]||s.name.EndsWith("Left")?0:1).ToArray();
            longDress=rig.skirt&&rig.skirt.sharedMesh.GetBlendShapeIndex("BootLeft")>=0;
            wideGown=longDress&&rig.skirt.sharedMesh.bounds.size.x>.95f;
            if(garments.Length==0 && rig.longBoots && rig.skirt && rig.skirt.sharedMesh.GetBlendShapeIndex("Sitting")>=0)
            {
                cloth=rig.GetComponent<HomecomingClothDynamics>();
                if(!cloth) cloth=rig.gameObject.AddComponent<HomecomingClothDynamics>();
            }
            if(rig.importedSource)
            {
                leftSole=new Sole(rig.leftShoe.GetComponent<SkinnedMeshRenderer>());
                rightSole=new Sole(rig.rightShoe.GetComponent<SkinnedMeshRenderer>());
                if(rig.hosiery!=null&&rig.hosiery.Length==2)
                {
                    if(rig.hosiery[0])leftStockingSole=new Sole(rig.hosiery[0]);
                    if(rig.hosiery[1])rightStockingSole=new Sole(rig.hosiery[1]);
                }
            }
            walk=Resources.Load<HomecomingWalkCycle>("Homecoming/NativeWalkCycle");
            if(walk) { walkBones=new Transform[walk.bones.Length];
                var all=rig.GetComponentsInChildren<Transform>(true);
                for(int i=0;i<walkBones.Length;i++) foreach(var bone in all) if(bone.name==walk.bones[i]) { walkBones[i]=bone; break; }
            }
            BuildRoute();
            MeasureThighClearance();
            Hands=new HomecomingHandPose(rig);
            Shoes=new HomecomingShoeGrip(rig,Hands);
            foreach(var bone in rig.GetComponentsInChildren<Transform>(true))
            {
                if(bone.name=="J_Bip_L_Foot") lf=bone;
                if(bone.name=="J_Bip_R_Foot") rf=bone;
                if(bone.name=="J_Bip_L_Hand") lh=bone;
                if(bone.name=="J_Bip_R_Hand") rh=bone;
                if(bone.name=="J_Bip_L_Shoulder")shoulders[0]=bone;
                if(bone.name=="J_Bip_R_Shoulder")shoulders[1]=bone;
                if(bone.name=="W3_left_corner_lip1"){smileBones[0]=bone;smileRest[0]=bone.localPosition;}
                if(bone.name=="W3_right_corner_lip1"){smileBones[1]=bone;smileRest[1]=bone.localPosition;}
                if(bone.name=="W3_upper_left_eyelid1"){eyelidBones[0]=bone;eyelidRest[0]=bone.localPosition;}
                if(bone.name=="W3_upper_right_eyelid1"){eyelidBones[1]=bone;eyelidRest[1]=bone.localPosition;}
                if(bone.name=="W3_lowwer_left_eyelid1"){eyelidBones[2]=bone;eyelidRest[2]=bone.localPosition;}
                if(bone.name=="W3_lowwer_right_eyelid1"){eyelidBones[3]=bone;eyelidRest[3]=bone.localPosition;}
                if(bone.name=="W3_left_eyebrow1"){browBones[0]=bone;browRest[0]=bone.localPosition;}
                if(bone.name=="W3_right_eyebrow1"){browBones[1]=bone;browRest[1]=bone.localPosition;}
                if(bone.name=="W3_jaw"){jawBone=bone;jawRest=bone.localPosition;}
            }
            headRest=RestRotation(rig.head);
            for(int side=0;side<2;side++)
            {
                var upper=side==0?rig.leftArm:rig.rightArm;var lower=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
                var u=lower.position-upper.position;var v=hand.position-lower.position;
                var normal=Vector3.Cross(u,v);
                if(normal.sqrMagnitude<.000001f)normal=Vector3.ProjectOnPlane(rig.transform.forward,u);
                normal.Normalize();
                upperArmFrames[side]=Quaternion.Inverse(Quaternion.LookRotation(upper.InverseTransformDirection(u),upper.InverseTransformDirection(normal)));
                forearmFrames[side]=Quaternion.Inverse(Quaternion.LookRotation(lower.InverseTransformDirection(v),lower.InverseTransformDirection(normal)));
                upper=side==0?rig.leftThigh:rig.rightThigh;lower=side==0?rig.leftCalf:rig.rightCalf;hand=side==0?lf:rf;
                u=lower.position-upper.position;v=hand.position-lower.position;
                normal=Vector3.Cross(u,rig.transform.forward).normalized;
                thighFrames[side]=Quaternion.Inverse(Quaternion.LookRotation(upper.InverseTransformDirection(u),upper.InverseTransformDirection(normal)));
                calfFrames[side]=Quaternion.Inverse(Quaternion.LookRotation(lower.InverseTransformDirection(v),lower.InverseTransformDirection(normal)));
            }
            endingPose=new HomecomingAuthoredPose(rig);
            if(lf) leftFootRest=RestRotation(lf);
            if(rf) rightFootRest=RestRotation(rf);
            if(rig.importedSource&&rig.hosiery!=null&&rig.hosiery.Length==2)
            {
                var all=rig.GetComponentsInChildren<Transform>(true);
                for(int side=0;side<2;side++)
                {
                    var foot=side==0?lf:rf;var toe=all.First(b=>b.name==(side==0?"J_Bip_L_ToeBase":"J_Bip_R_ToeBase"));
                    barefoot[side]=new HomecomingFootContact(rig.hosiery[side],foot,toe);
                    flatFootRest[side]=barefoot[side].Flat(side==0?leftFootRest:rightFootRest);
                    if(dlcBoots||rig.openToeFootwear)
                    {
                        wornFeet[side]=rig.hosiery[side].GetComponent<HomecomingFootwearLast>();
                        if(!wornFeet[side])wornFeet[side]=rig.hosiery[side].gameObject.AddComponent<HomecomingFootwearLast>();
                        wornFeet[side].Initialize(rig.openToeFootwear?"TrissFootLast_":"FootLast_");
                    }
                }
            }
            leftCalfRest=RestRotation(rig.leftCalf);rightCalfRest=RestRotation(rig.rightCalf);
        }
        static float Ease(float t,float a,float b) { float u=Mathf.Clamp01((t-a)/(b-a)); return u*u*u*(u*(u*6-15)+10); }
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        public void Sample(float t)
        {
            SamplePose(t);
            if(t>=26.7f)
            {
                if(endingPose.Available)endingPose.Apply(Ease(t,26.7f,28.6f));
                else ApplyCutePose(t-27,Ease(t,26.7f,28),false);
            }
            else if(t<1.5f) ApplyCutePose(t+1,Ease(t,0,.65f)*(1-Ease(t,.85f,1.5f)));
            FitBoots(t-1);
            if(cloth) cloth.Sample(rig,t);
            foreach(var garment in garments)garment.Sample(rig,t);
        }
        public void SampleWaiting(float time)
        {
            SamplePose(0);
            ApplyCutePose(time,.35f,false);
            FitBoots(-1);
            if(cloth)cloth.Sample(rig,time);
            foreach(var garment in garments)garment.Sample(rig,time);
        }
        void FitBoots(float action)
        {
            for(int i=0;i<bootFit.Length;i++)bootFit[i].SetBlendShapeWeight(bootFitIndices[i],100*(1-Ease(action,HomecomingShoeGrip.Detach(bootFitSides[i])+.05f,HomecomingShoeGrip.Detach(bootFitSides[i])+.9f)));
            for(int side=0;side<2;side++)if(wornFeet[side])wornFeet[side].Set(1-Ease(action,HomecomingShoeGrip.Detach(side)+.65f,Shoes.Cleared(side)+.20f));
        }
        public void ResetFace()
        {
            for(int i=0;i<2;i++)if(smileBones[i])smileBones[i].localPosition=smileRest[i];
            for(int i=0;i<4;i++)if(eyelidBones[i])eyelidBones[i].localPosition=eyelidRest[i];
            for(int i=0;i<2;i++)if(browBones[i])browBones[i].localPosition=browRest[i];
            if(jawBone)jawBone.localPosition=jawRest;
        }
        void ApplyCutePose(float time,float amount,bool gestures=true)
        {
            if(amount<=0||!lf||!rf||!lh||!rh)return;
            var root=rig.transform;
            float cycle=Mathf.Repeat(Mathf.Max(0,time),10);
            float wave=gestures?Ease(cycle,.55f,1.35f)*(1-Ease(cycle,3.1f,4.1f)):0;
            float gather=gestures?Ease(cycle,4.5f,5.6f)*(1-Ease(cycle,8.1f,9.5f)):0;
            // Move the pelvis over planted feet; never slide the soles with the sway.
            var left=lf.position;var right=rf.position;var lr=lf.rotation;var rr=rf.rotation;
            rig.hips.position+=root.right*(.018f*Mathf.Sin(time*.72f)*amount)+root.up*(.0025f*Mathf.Sin(time*2.1f)*amount);
            rig.Pose(rig.spine,new Vector3(-1.5f,1.2f*Mathf.Sin(time*.72f),2.2f*Mathf.Sin(time*.72f))*amount);
            rig.Pose(rig.head,new Vector3(-2+2*Mathf.Sin(time*1.1f),-5*wave,6*Mathf.Sin(time*.72f+.6f))*amount);
            var faceUp=rig.head.rotation*Quaternion.Inverse(headRest)*Vector3.up;
            for(int i=0;i<2;i++)if(smileBones[i])smileBones[i].localPosition=smileRest[i]+smileBones[i].parent.InverseTransformVector(faceUp*(.0014f*amount));
            // Blink: quick close-open on a ~3.1s cycle (mirrors the old blendshape
            // blink timing from CharacterRig.Expression). Upper lids move down,
            // lower lids move up; all in head space so head tilts don't skew it.
            float blinkCycle=Mathf.Repeat(time,3.1f);
            float blink=Mathf.Clamp01(1-Mathf.Abs(blinkCycle-.12f)/.12f)*amount;
            for(int i=0;i<4;i++)if(eyelidBones[i])
            {
                float dir=i<2?-1:1; // upper lids down, lower lids up
                eyelidBones[i].localPosition=eyelidRest[i]+eyelidBones[i].parent.InverseTransformVector(faceUp*(dir*.0012f*blink));
            }
            // Brows lift slightly with the smile; jaw drifts with the breath so
            // the mouth is never perfectly frozen. All millimeter-scale.
            for(int i=0;i<2;i++)if(browBones[i])browBones[i].localPosition=browRest[i]+browBones[i].parent.InverseTransformVector(faceUp*(.0008f*amount));
            if(jawBone)
            {
                float jawOpen=(.5f+.5f*Mathf.Sin(time*1.1f))*.0016f*amount;
                jawBone.localPosition=jawRest+jawBone.parent.InverseTransformVector(faceUp*(-jawOpen));
            }
            Solve(rig.leftThigh,rig.leftCalf,lf,left,rig.hips.position+root.right*.13f-root.forward);
            Solve(rig.rightThigh,rig.rightCalf,rf,right,rig.hips.position-root.right*.13f-root.forward);
            lf.rotation=lr;rf.rotation=rr;
            for(int side=0;side<2;side++)
            {
                var upper=side==0?rig.leftArm:rig.rightArm;var elbow=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
                float sign=side==0?1:-1,hello=side==1?wave:0;
                float armAmount=Mathf.Max(.2f,Mathf.Max(gather,hello))*amount;
                if(armAmount<.00001f)continue;
                var target=hand.position+root.right*(sign*.018f*amount)-root.forward*(.06f*amount);
                var together=root.TransformPoint(new Vector3(sign*.07f,1.10f+(side==1?.045f:0),-.36f));
                var greeting=upper.position+root.right*(sign*(.20f+.018f*Mathf.Sin(time*5)))-root.forward*.20f+root.up*.015f;
                target=Vector3.Lerp(target,together,gather*amount);
                target=Vector3.Lerp(target,greeting,hello*amount);
                // Elbows stay away from the torso while raising a hand, with a
                // continuous pole instead of a switch to a grasping pose.
                var pole=Vector3.Lerp(elbow.position,upper.position+root.forward*.14f+root.right*(sign*(.035f+.20f*hello*amount)),armAmount);
                SolveArm(upper,elbow,hand,target,pole);
                var axis=(hand.position-elbow.position).normalized;
                var baseTurn=Hands.Rotation(side,root.right*(-sign),axis);
                var gathered=Hands.Rotation(side,-root.up+root.forward*.25f,(root.right*(-sign*.65f)-root.forward*.65f-root.up*.2f).normalized);
                var waving=Hands.Rotation(side,-root.forward,root.up+root.right*(.22f*Mathf.Sin(time*5)));
                var desired=Quaternion.Slerp(baseTurn,gathered,gather*amount);
                desired=Quaternion.Slerp(desired,waving,hello*amount);
                desired=Hands.LimitWrist(side,desired,axis,Mathf.Lerp(35,65,hello*amount));
                var palm=Vector3.ProjectOnPlane(Hands.Palm(side,desired),axis).normalized;
                var actual=Vector3.ProjectOnPlane(Hands.Palm(side,hand.rotation),axis).normalized;
                elbow.rotation=Quaternion.FromToRotation(actual,palm)*elbow.rotation;
                hand.rotation=desired;
                Hands.Fingers(side,HomecomingHandPose.Hold.Rest,0);
                Hands.Overlay(side,HomecomingHandPose.Hold.Greeting,1,hello*amount);
            }
            KeepAboveFloor();
        }
        void SamplePose(float t)
        {
            handStep=t-previousSample;
            legContinuity=t>=8&&previousSample>=8&&handStep>0&&handStep<=.2f;
            for(int side=0;side<2;side++)
            {
                var thigh=side==0?rig.leftThigh:rig.rightThigh;var knee=side==0?rig.leftCalf:rig.rightCalf;var foot=side==0?lf:rf;
                priorLegDirection[side]=(foot.position-thigh.position).normalized;
                priorLegBend[side]=Vector3.ProjectOnPlane(knee.position-thigh.position,priorLegDirection[side]).normalized;
            }
            if(t<8||handStep<0||handStep>.2f)elbowsInitialized=false;
            for(int side=0;side<2;side++){priorDirection[side]=solvedDirection[side];priorBend[side]=solvedBend[side];}
            previousSample=t;
            sampleAction=t-1;
            rig.Neutral();
            ResetFace();
            rig.SetDressLift(0,0);
            if(t<8)
            {
                rig.SetSkirtPose(0);
                // Play the walk at its native speed: NativeWalkCycle covers
                // strideDistance (1.55m) per duration (1.0s). The old 5.8s
                // timeline walked at 0.97 m/s, stretching each 5.4cm lateral
                // sway to 1.6s — visible waddling. Now ~3.64s, sway at 1.0s.
                float walkDuration=routeLength/1.55f;
                float distance=routeLength*Travel(t,walkDuration);
                rig.transform.localPosition=Route(distance);
                var direction=Route(Mathf.Min(routeLength,distance+.55f))-Route(Mathf.Max(0,distance-.55f));
                Quaternion heading=direction.sqrMagnitude>.000001f ? Quaternion.LookRotation(direction)*Quaternion.Euler(0,180,0) : Quaternion.identity;
                rig.transform.localRotation=Quaternion.Slerp(heading,Quaternion.identity,Ease(t,4.7f,5.2f));
                rig.hips.localPosition=rig.hipRestPosition;
                StandPose();
                float weight=Ease(t,1.5f,2)*(1-Ease(t,4.6f,5.2f));
                if(walk)
                {
                    float phase=distance/walk.strideDistance;
                    for(int i=0;i<walkBones.Length;i++) if(walkBones[i])
                    {
                        string name=walkBones[i].name;
                        // A boot/high-heel sole does not bend like the barefoot
                        // source walk. Preserve toe rest while footwear is worn.
                        if(name.Contains("Toe")&&(rig.longBoots||rig.footwearKind==CharacterRig.FootwearKind.AnkleBoot))continue;
                        // Keep the retargeted shoulder, forearm, wrist and fingers
                        // together; partial arm weighting changes their relative roll.
                        bool arm=name.Contains("Arm")||name.Contains("Shoulder")||name.Contains("Hand")||name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little")||name.Contains("Thumb");
                        float blend=arm?Ease(t,1.5f,2.6f)*(1-Ease(t,4.2f,5.2f)):weight;
                        walkBones[i].localRotation=Quaternion.Slerp(walkBones[i].localRotation,walk.Rotation(i,phase),blend);
                    }
                    rig.hips.localPosition=Vector3.Lerp(rig.hipRestPosition,walk.Hip(phase),weight);

                }
                if(!walk)for(int side=0;side<2;side++)
                {
                    var elbow=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
                    var axis=(hand.position-elbow.position).normalized;
                    var inward=Vector3.ProjectOnPlane(rig.transform.right*(side==0?-1:1),axis).normalized;
                    var palm=Vector3.ProjectOnPlane(Hands.Palm(side,hand.rotation),axis).normalized;
                    // Correct forearm roll as a chain, so the palms face the
                    // thighs and the wrists remain aligned with the forearms.
                    elbow.rotation=Quaternion.FromToRotation(palm,inward)*elbow.rotation;
                    hand.rotation=Hands.Rotation(side,inward,axis);
                    Hands.Fingers(side,HomecomingHandPose.Hold.Rest,0);
                }
                // The walk clip already contains ankle roll. Driving a worn
                // foot from the calf forced the rigid sole to pitch with every
                // knee swing, and floor correction then bounced the whole body.
                KeepAboveFloor();
                return;
            }
            t-=1;
            if(rig.longBoots || longDress || rig.footwearKind==CharacterRig.FootwearKind.AnkleBoot) { SampleLongBoots(t); return; }
            Vector3 root=Vector3.Lerp(V(-1.25f,0,1.02f),V(-1.25f,0,1.47f),Ease(t,7,9));
            root=Vector3.Lerp(root,V(-1.25f,0,1.135f),Ease(t,23,26));
            float seated=Ease(t,7,9)*(1-Ease(t,23,26));
            rig.transform.localPosition=root;
            rig.transform.localRotation=Quaternion.identity;
            rig.hips.localPosition=rig.HipPosition(seated);
            // Seat contact: pelvis stays above the cushion; lean precedes standing.
            float lReach=Ease(t,10,11.4f)*(1-Ease(t,14.4f,15.2f));
            float rReach=Ease(t,16,17.4f)*(1-Ease(t,20.4f,21.2f));
            float reach=lReach+rReach;
            float lPlace=Ease(t,12.8f,14.2f)*(1-Ease(t,14.4f,15.2f));
            float rPlace=Ease(t,18.8f,20.2f)*(1-Ease(t,20.4f,21.2f));
            rig.hips.position+=Vector3.right*.25f*(lPlace-rPlace);
            float lean=12*Ease(t,7,8)*(1-Ease(t,8,9))+45*reach+65*(lPlace+rPlace)+16*Ease(t,22,23)*(1-Ease(t,24,26));
            rig.Pose(rig.spine,V(-lean,0,0));
            rig.Pose(rig.head,V(-7*reach,-10*Ease(t,25,27),0));
            rig.SetSkirtPose(seated);
            if(!lf||!rf||!lh||!rh) return;
            Vector3 left=rig.transform.TransformPoint(V(.105f,.10f,-.035f));
            Vector3 right=rig.transform.TransformPoint(V(-.105f,.10f,-.035f));
            {
                left=Vector3.Lerp(rig.transform.TransformPoint(V(.105f,.10f,-.035f)),V(-1.145f,.10f,1.10f),Ease(t,7,9));
                right=Vector3.Lerp(rig.transform.TransformPoint(V(-.105f,.10f,-.035f)),V(-1.355f,.10f,1.10f),Ease(t,7,9));
                // Lift one heel toward the reaching hand; the other foot remains planted.
                left+=V(.04f,.42f,-.055f)*Ease(t,10,11.4f)*(1-Ease(t,12.8f,13.8f));
                right+=V(-.04f,.42f,-.055f)*Ease(t,16,17.4f)*(1-Ease(t,18.8f,19.8f));
                left+=Vector3.right*.23f*Ease(t,13,14.5f)*(1-Ease(t,15.2f,16.2f));
                right-=Vector3.right*.23f*Ease(t,19,20.5f)*(1-Ease(t,21,22));
            }
            Solve(rig.leftThigh,rig.leftCalf,lf,left,rig.hips.position+V(.13f,-.015f,-1));
            Solve(rig.rightThigh,rig.rightCalf,rf,right,rig.hips.position+V(-.13f,-.015f,-1));
            lf.rotation=leftFootRest; rf.rotation=rightFootRest;
            KeepAboveFloor();
            PoseHands(t,seated,lReach,rReach,lean,0);
        }
        void SampleLongBoots(float t)
        {
            float sit=Ease(t,7,9)*(1-Ease(t,23,26));
            rig.SetSkirtPose(sit);
            rig.transform.localPosition=Vector3.Lerp(V(-1.25f,0,1.02f),V(-1.25f,0,1.47f),Ease(t,7,9));
            rig.transform.localPosition=Vector3.Lerp(rig.transform.localPosition,V(-1.25f,0,1.135f),Ease(t,23,26));
            rig.transform.localRotation=Quaternion.identity;rig.hips.localPosition=rig.HipPosition(sit);
            float l=Ease(t,10,11.4f)*(1-Ease(t,14.4f,15.2f));
            float r=Ease(t,16,17.4f)*(1-Ease(t,20.4f,21.2f));
            float lp=Ease(t,rig.longBoots?13.05f:12.3f,rig.longBoots?14.2f:13.1f)*(1-Ease(t,14.4f,15.2f));
            float rp=Ease(t,rig.longBoots?19.05f:18.3f,rig.longBoots?20.2f:19.1f)*(1-Ease(t,20.4f,21.2f));
            rig.hips.position+=Vector3.right*(Shoes.AnatomicalExtraction?.18f:.25f)*(lp-rp);
            rig.hips.position+=Vector3.back*(rig.longBoots?BootHipForward:.12f)*(l+r);
            // Anticipate the downward pull with a modest, continuous torso reach.
            // The old emergency solver changed the torso after the grip began.
            float lean=(dlcBoots?34:rig.openToeFootwear?30:rig.longBoots?BootLean:28)*(l+r)+(dlcBoots?12:rig.openToeFootwear?14:rig.longBoots?8:10)*(lp+rp)+12*Ease(t,7,8)*(1-Ease(t,8,9))+17*Ease(t,22.4f,23.4f)*(1-Ease(t,24,26));
            rig.Pose(rig.spine,V(-lean+.35f*Mathf.Sin(t*1.1f),1.5f*(l-r),2*(lp-rp)));
            rig.Pose(rig.head,V(-3*(l+r),-4*(l-r),-1.5f*(l-r)));
            foreach(var pose in rig.restPoses)
            {
                if(pose.bone.name=="J_Bip_L_Shoulder")rig.Pose(pose.bone,V(0,20,-6)*Mathf.Max(l,.75f*r));
                if(pose.bone.name=="J_Bip_R_Shoulder")rig.Pose(pose.bone,V(0,-20,6)*Mathf.Max(r,.75f*l));
            }
            Vector3 left=Vector3.Lerp(rig.transform.TransformPoint(V(.105f,.10f,-.035f)),V(-1.145f,.10f,1.10f),sit);
            Vector3 right=Vector3.Lerp(rig.transform.TransformPoint(V(-.105f,.10f,-.035f)),V(-1.355f,.10f,1.10f),sit);
            float ll=Ease(t,10,11.4f)*(1-Ease(t,13.35f,14.4f));
            float rl=Ease(t,16,17.4f)*(1-Ease(t,19.35f,20.4f));
            float lift=dlcBoots?.30f:rig.openToeFootwear?.42f:longDress?(rig.longBoots?(Shoes.AnatomicalExtraction?.29f:.14f):.32f):rig.longBoots?BootLegLift:.40f;
            // The free hand lifts and holds the hem before the shoe hand reaches.
            // Release the fabric once the shaft clears the foot, before leaning
            // to place the shoe. Holding it at lap height through that lean
            // drove a lifted panel into the chest and face.
            float gatherLeft=Ease(t,9.15f,10)*(1-Ease(t,13.1f,14));
            float gatherRight=Ease(t,15.15f,16)*(1-Ease(t,19.1f,20));
            rig.SetDressLift(gatherLeft,gatherRight);
            // Raise the ankle and present the shaft diagonally, providing an
            // actual exit corridor above the floor before applying pull force.
            float presentation=dlcBoots?-.16f:rig.openToeFootwear?-.18f:rig.longBoots?BootLegBack:.10f;
            left+=V(.045f,lift,presentation)*ll;right+=V(-.045f,lift,presentation)*rl;
            if(rig.longBoots&&Shoes.AnatomicalExtraction)
            {
                // Withdraw the leg towards the body while the hand controls
                // the cuff. The captured boot frame no longer follows the leg.
                // Withdraw towards the actual captured opening. World -Z here
                // pushed the foot deeper into the shaft and sideways through it.
                float withdrawal=dlcBoots?.22f:rig.openToeFootwear?.26f:.18f;
                left+=Shoes.ExtractionAxis(0)*withdrawal*Ease(t,12,13.05f)*(1-Ease(t,13.35f,14.4f));
                right+=Shoes.ExtractionAxis(1)*withdrawal*Ease(t,18,19.05f)*(1-Ease(t,19.35f,20.4f));
            }
            float leftBare=Ease(t,Shoes.Cleared(0),14.4f),rightBare=Ease(t,Shoes.Cleared(1),20.4f);
            if(barefoot[0]!=null)
            {
                float leftHeight=barefoot[0].AnkleHeight(flatFootRest[0]),rightHeight=barefoot[1].AnkleHeight(flatFootRest[1]);
                left.y+=(leftHeight-.10f)*leftBare;right.y+=(rightHeight-.10f)*rightBare;
                rig.hips.position+=Vector3.up*((leftHeight+rightHeight)*.5f-.10f)*Mathf.Min(leftBare,rightBare)*(1-sit);
            }
            // A raised ankle can pass through a front-only pole's direction.
            // Give the working knee an upward hint before lifting the leg so
            // the bend plane cannot switch to the opposite folded solution.
            float kneeHint=dlcBoots?.55f:.35f;
            SolveLeg(rig.leftThigh,rig.leftCalf,lf,left,rig.hips.position+V(.16f,-.02f+kneeHint*ll,-1),0);
            SolveLeg(rig.rightThigh,rig.rightCalf,rf,right,rig.hips.position+V(-.16f,-.02f+kneeHint*rl,-1),1);
            if(rig.longBoots){AlignBoot(lf,rig.leftCalf,leftFootRest);AlignBoot(rf,rig.rightCalf,rightFootRest);}
            else{lf.rotation=leftFootRest;rf.rotation=rightFootRest;}
            // Point the toes through the shaft as the boot is pulled away; the boot
            // follows the calf, so the ankle can flex independently inside its opening.
            if(barefoot[0]!=null)
            {
                float pointLeft=rig.longBoots?Ease(t,Shoes.AnatomicalExtraction?11.35f:12.2f,Shoes.AnatomicalExtraction?11.95f:12.65f)*(1-Ease(t,Shoes.Cleared(0),14.2f)):0;
                float pointRight=rig.longBoots?Ease(t,Shoes.AnatomicalExtraction?17.35f:18.2f,Shoes.AnatomicalExtraction?17.95f:18.65f)*(1-Ease(t,Shoes.Cleared(1),20.2f)):0;
                if(Shoes.AnatomicalExtraction)
                {
                    // The heel counter limits the ankle while the boot is worn.
                    // Complete the toe point as the heel leaves the shoe, rather
                    // than forcing the full plantar flexion through its back wall.
                    float heelRelease=rig.openToeFootwear?.04f:.10f;
                    float shaftEntry=rig.openToeFootwear?.32f:.90f;
                    pointLeft=(.15f*Ease(t,11.35f,11.95f)+.85f*Ease(t,12+heelRelease,12+shaftEntry))*(1-Ease(t,Shoes.Cleared(0),14.2f));
                    pointRight=(.15f*Ease(t,17.35f,17.95f)+.85f*Ease(t,18+heelRelease,18+shaftEntry))*(1-Ease(t,Shoes.Cleared(1),20.2f));
                }
                var leftFlat=Quaternion.Slerp(lf.rotation,flatFootRest[0],Mathf.Max(leftBare,pointLeft));
                var rightFlat=Quaternion.Slerp(rf.rotation,flatFootRest[1],Mathf.Max(rightBare,pointRight));
                var leftDirection=(lf.position-rig.leftCalf.position).normalized;
                var rightDirection=(rf.position-rig.rightCalf.position).normalized;
                if(Shoes.AnatomicalExtraction&&t>=HomecomingShoeGrip.Detach(0))leftDirection=Vector3.Slerp(-Shoes.ExtractionAxis(0),leftDirection,Ease(t,Shoes.Cleared(0),14.2f));
                if(Shoes.AnatomicalExtraction&&t>=HomecomingShoeGrip.Detach(1))rightDirection=Vector3.Slerp(-Shoes.ExtractionAxis(1),rightDirection,Ease(t,Shoes.Cleared(1),20.2f));
                lf.rotation=barefoot[0].Point(leftFlat,leftDirection,pointLeft);
                rf.rotation=barefoot[1].Point(rightFlat,rightDirection,pointRight);
                barefoot[0].PlantToes(leftBare);barefoot[1].PlantToes(rightBare);
            }
            KeepAboveFloor();
            PoseHands(t,sit,l,r,lean,lp-rp);
        }
        void MeasureThighClearance()
        {
            var baked=new Mesh();
            for(int side=0;side<2;side++)
            {
                var thigh=side==0?rig.leftThigh:rig.rightThigh;var knee=side==0?rig.leftCalf:rig.rightCalf;
                var axis=(knee.position-thigh.position).normalized;var center=Vector3.Lerp(thigh.position,knee.position,.62f);
                var front=Vector3.ProjectOnPlane(-rig.transform.forward,axis).normalized;float radius=.08f;
                foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(skin==rig.skirt||skin.gameObject==rig.leftShoe||skin.gameObject==rig.rightShoe)continue;
                    var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;
                    if(weights.Length==0)continue;
                    int a=System.Array.IndexOf(bones,thigh),b=System.Array.IndexOf(bones,knee);if(a<0||b<0)continue;
                    skin.BakeMesh(baked);var vertices=baked.vertices;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];float own=0;
                        if(w.boneIndex0==a||w.boneIndex0==b)own+=w.weight0;if(w.boneIndex1==a||w.boneIndex1==b)own+=w.weight1;
                        if(w.boneIndex2==a||w.boneIndex2==b)own+=w.weight2;if(w.boneIndex3==a||w.boneIndex3==b)own+=w.weight3;
                        if(own<.5f)continue;
                        var delta=skin.transform.TransformPoint(vertices[i])-center;
                        if(Mathf.Abs(Vector3.Dot(delta,axis))<.045f)radius=Mathf.Max(radius,Vector3.Dot(delta,front));
                    }
                }
                // Finger pads sit above the garment while the soft curl settles.
                thighClearance[side]=Mathf.Clamp(radius,.08f,.16f)+.038f;
            }
            if(Application.isPlaying)Object.Destroy(baked);else Object.DestroyImmediate(baked);
        }
        Vector3 IdleWrist(int side)
        {
            var upper=side==0?rig.leftArm:rig.rightArm;var elbow=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
            float length=Vector3.Distance(upper.position,elbow.position)+Vector3.Distance(elbow.position,hand.position);
            var outward=rig.transform.right*(side==0?1:-1);
            // Author the relaxed silhouette, rather than shortening the arm and
            // letting a generic grasp hint put its wrist behind the hip.
            float outside=side==0?.12f:.13f,forward=side==0?.075f:.085f;
            // Finger clearance follows the costume's width, not just the
            // shared skeleton. Capture the profile before cloth deformation.
            if(wideGown){outside+=.025f;forward+=.025f;}
            float span=length*.985f;
            float drop=Mathf.Sqrt(Mathf.Max(.01f,span*span-outside*outside-forward*forward));
            return upper.position+Vector3.down*drop+outward*outside-rig.transform.forward*forward;
        }
        void PoseHands(float t,float sit,float leftReach,float rightReach,float lean,float lateral)
        {
            var targets=handTargets;var rotations=handRotations;
            var modes=handModes;var fingers=fingerAmounts;
            var fabric=fabricReach;var support=supportReach;
            for(int side=0;side<2;side++){fabric[side]=0;support[side]=0;}
            if(longDress)
            {
                // Touch the fabric before the lift begins, keep contact while
                // lowering it, then open the pinch before bringing the hand back.
                fabric[0]=Ease(t,14.7f,15.15f)*(1-Ease(t,20,20.65f));
                fabric[1]=Ease(t,8.7f,9.15f)*(1-Ease(t,14,14.65f));
            }
            else
            {
                // The other hand stabilizes the knee, not the same boot cuff.
                support[0]=Ease(t,16.3f,17.15f)*(1-Ease(t,19.35f,20.25f));
                support[1]=Ease(t,10.3f,11.15f)*(1-Ease(t,13.35f,14.25f));
            }
            for(int side=0;side<2;side++)
            {
                var thigh=side==0?rig.leftThigh:rig.rightThigh;var knee=side==0?rig.leftCalf:rig.rightCalf;
                Vector3 downThigh=(knee.position-thigh.position).normalized;
                var palm=Vector3.ProjectOnPlane(Vector3.down,downThigh).normalized;
                if(palm.sqrMagnitude<.01f)palm=rig.transform.forward;
                var restTurn=Hands.Rotation(side,palm,downThigh);
                rotations[side]=Quaternion.Slerp(Hands.RelaxedRotation(side,rig.transform.rotation),restTurn,sit);
                var restPoint=Vector3.Lerp(thigh.position,knee.position,.62f)-palm*thighClearance[side];
                targets[side]=Vector3.Lerp(IdleWrist(side),Hands.Wrist(side,HomecomingHandPose.Hold.Rest,restPoint,restTurn),sit);
                float reach=side==0?leftReach:rightReach;
                var bootTurn=Hands.BootRotation(side,Shoes.Rotation(side,t));
                Shoes.SetHandRotation(side,bootTurn);
                targets[side]=Vector3.Lerp(targets[side],Shoes.WristTarget(side,t),reach);
                targets[side]+=rig.transform.right*(side==0?1:-1)*.02f*Mathf.Sin(Mathf.PI*reach);
                rotations[side]=Quaternion.Slerp(rotations[side],bootTurn,reach);
                float start=HomecomingShoeGrip.Start(side);
                fingers[side]=Ease(t,start+1.35f,start+1.95f)*(1-Ease(t,start+4.4f,start+5.1f));
                bootFingerAmounts[side]=fingers[side];
                modes[side]=HomecomingHandPose.Hold.Boot;
                if(support[side]>0)
                {
                    var workingKnee=side==0?rig.rightCalf:rig.leftCalf;
                    var inward=Vector3.down;
                    var workingFoot=side==0?rf:lf;
                    var supportDirection=Vector3.ProjectOnPlane(workingFoot.position-workingKnee.position,inward).normalized;
                    if(supportDirection.sqrMagnitude<.01f)supportDirection=-rig.transform.forward;
                    var turn=Hands.Rotation(side,inward,supportDirection);
                    var point=workingKnee.position+Vector3.up*.035f+rig.transform.right*(side==0?.025f:-.025f);
                    targets[side]=Vector3.Lerp(targets[side],Hands.Wrist(side,HomecomingHandPose.Hold.Support,point,turn),support[side]);
                    rotations[side]=Quaternion.Slerp(rotations[side],turn,support[side]);
                    modes[side]=HomecomingHandPose.Hold.Support;fingers[side]=support[side];
                }
                if(fabric[side]>0)
                {
                    var turn=Hands.Rotation(side,rig.transform.forward,Vector3.down);
                    var point=rig.DressHoldPoint(1-side);
                    targets[side]=Vector3.Lerp(targets[side],Hands.Wrist(side,HomecomingHandPose.Hold.Fabric,point,turn),fabric[side]);
                    rotations[side]=Quaternion.Slerp(rotations[side],turn,fabric[side]);
                    float startLift=side==0?15.15f:9.15f;
                    fingers[side]=Ease(t,startLift-.3f,startLift)*(1-Ease(t,startLift+4.85f,startLift+5.5f));
                    modes[side]=HomecomingHandPose.Hold.Fabric;
                }
                float rising=Ease(t,22.3f,23)*(1-Ease(t,24.6f,25.6f));
                if(rising>0)
                {
                    var turn=Hands.Rotation(side,palm,downThigh);
                    var point=Vector3.Lerp(thigh.position,knee.position,.42f)-palm*thighClearance[side];
                    targets[side]=Vector3.Lerp(targets[side],Hands.Wrist(side,HomecomingHandPose.Hold.Support,point,turn),rising);
                    rotations[side]=Quaternion.Slerp(rotations[side],turn,rising);
                    modes[side]=HomecomingHandPose.Hold.Support;support[side]=rising;fingers[side]=rising*.55f;
                }
            }
            float leftLength=Vector3.Distance(rig.leftArm.position,rig.leftElbow.position)+Vector3.Distance(rig.leftElbow.position,lh.position);
            float rightLength=Vector3.Distance(rig.rightArm.position,rig.rightElbow.position)+Vector3.Distance(rig.rightElbow.position,rh.position);
            bool Beyond()=>Vector3.Distance(rig.leftArm.position,targets[0])>leftLength-.008f||Vector3.Distance(rig.rightArm.position,targets[1])>rightLength-.008f;
            float effort=Mathf.Max(Mathf.Max(leftReach,rightReach),Mathf.Max(fabric[0],fabric[1]));
            // These two fitted outfits have a designed reaching pose and a
            // clavicle contribution at final contact. A binary fallback lean
            // jumped from that pose to its angle limit when the idle arm's
            // target became unreachable, abruptly moving both shoulders.
            if(!dlcBoots&&!rig.openToeFootwear&&effort>.001f&&Beyond())
            {
                float low=lean,high=rig.longBoots?45:85;
                for(int attempt=0;attempt<12;attempt++)
                {
                    float middle=(low+high)*.5f;rig.Pose(rig.spine,V(-middle,0,2*lateral));
                    if(Beyond())low=middle;else high=middle;
                }
                rig.Pose(rig.spine,V(-Mathf.Lerp(lean,high,Mathf.SmoothStep(0,1,effort)),0,2*lateral));
            }
            for(int side=0;side<2;side++)
            {
                var upper=side==0?rig.leftArm:rig.rightArm;var elbow=side==0?rig.leftElbow:rig.rightElbow;var hand=Hands.Hand(side);
                float standing=1-sit;
                float forearm=Vector3.Distance(elbow.position,hand.position);
                // Choose the elbow plane that lets the hand continue the forearm,
                // instead of forcing a sideways elbow and folding the wrist back.
                var direction=(targets[side]-upper.position).normalized;
                var lateralPlane=Vector3.ProjectOnPlane(rig.transform.right*(side==0?1:-1)-rig.transform.forward*.25f,direction).normalized;
                var desiredBend=Vector3.ProjectOnPlane(targets[side]-Hands.Direction(side,rotations[side])*forearm-upper.position,direction);
                // Elbows stay below the shoulders. Finger direction alone can
                // select a raised-elbow pose when the cuff is close to the torso.
                var hanging=Vector3.down*.24f+rig.transform.right*(side==0?.10f:-.10f)-rig.transform.forward*.12f;
                // Supporting a raised knee needs a lateral elbow, so the palm
                // can lie down without the forearm forcing the fingers upward.
                var supporting=rig.transform.right*(side==0?.24f:-.24f)-Vector3.up*.04f-rig.transform.forward*.06f;
                hanging=Vector3.Lerp(hanging,supporting,support[side]);
                desiredBend=Vector3.ProjectOnPlane(hanging,direction)+desiredBend*.35f;
                // In a hanging arm the elbow is slightly behind the wrist, so
                // the forearm comes forward. The shoe-reaching plane faces the
                // opposite way and must fade out as the character stands.
                var relaxedHint=rig.transform.forward*.14f+rig.transform.right*(side==0?.015f:-.015f);
                desiredBend=Vector3.Lerp(desiredBend,Vector3.ProjectOnPlane(relaxedHint,direction),standing);
                float outward=Vector3.Dot(desiredBend,lateralPlane);
                // Keep the elbow on the body's outer side when the preferred
                // plane passes close to the straight-arm singularity.
                float clearance=Mathf.Lerp(.085f,.012f,standing);
                float margin=Mathf.Sqrt(outward*outward+clearance*clearance);
                var pole=upper.position+desiredBend+lateralPlane*(margin-outward);
                var bend=Vector3.ProjectOnPlane(pole-upper.position,direction).normalized;
                if(elbowsInitialized)
                {
                    var previous=Vector3.ProjectOnPlane(elbowBend[side],direction).normalized;
                    if(previous.sqrMagnitude>.1f)bend=Vector3.RotateTowards(previous,bend,Mathf.Max(0,handStep)*Mathf.Deg2Rad*180,0);
                }
                elbowBend[side]=bend;
                SolveArm(upper,elbow,hand,targets[side],upper.position+bend);
                var turn=rotations[side];
                var relaxedDirection=(hand.position-elbow.position).normalized;
                var relaxedPalm=Vector3.ProjectOnPlane(rig.transform.right*(side==0?-1:1),relaxedDirection).normalized;
                turn=Quaternion.Slerp(turn,Hands.Rotation(side,relaxedPalm,relaxedDirection),standing);
                var contact=targets[side]-Hands.Wrist(side,modes[side],Vector3.zero,turn);
                // Keep the finger contact while limiting wrist extension. This
                // matters for low shoes and for the hand returning to the lap.
                for(int attempt=0;attempt<3;attempt++)
                {
                    var forearmDirection=(hand.position-elbow.position).normalized;
                    var forward=Hands.Direction(side,turn);
                    if(Vector3.Angle(forearmDirection,forward)<=55)break;
                    // Use the calibrated palm normal, not the imported bone axis.
                    turn=Hands.LimitWrist(side,turn,forearmDirection,55);
                    targets[side]=Hands.Wrist(side,modes[side],contact,turn);
                    SolveArm(upper,elbow,hand,targets[side],upper.position+bend);
                }
                // Limit recovery speed when a constrained wrist leaves the cuff
                // or hem. Preserve the contact anchor as the palm turns.
                if(elbowsInitialized)
                {
                    turn=Quaternion.RotateTowards(previousHandTurn[side],turn,Mathf.Max(0,handStep)*360);
                    targets[side]=Hands.Wrist(side,modes[side],contact,turn);
                    SolveArm(upper,elbow,hand,targets[side],upper.position+bend);
                }
                previousHandTurn[side]=turn;
                if(modes[side]==HomecomingHandPose.Hold.Boot&&fingers[side]>.999f)Shoes.SetHandRotation(side,turn);
                hand.rotation=turn;
                Hands.Fingers(side,HomecomingHandPose.Hold.Boot,bootFingerAmounts[side]);
                if(support[side]>0)Hands.Overlay(side,HomecomingHandPose.Hold.Support,1,support[side]);
                if(fabric[side]>0)Hands.Overlay(side,HomecomingHandPose.Hold.Fabric,fingers[side],fabric[side]);
            }
            elbowsInitialized=true;
        }
        void SolveArm(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            var origin=upper.position;var originalUpper=lower.position-origin;var originalLower=end.position-lower.position;
            float a=originalUpper.magnitude,b=originalLower.magnitude;
            var direction=(target-origin).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.002f,a+b-.002f);
            var bend=Vector3.ProjectOnPlane(pole-origin,direction).normalized;
            int side=upper==rig.leftArm?0:1;
            if(elbowsInitialized&&priorDirection[side].sqrMagnitude>.5f&&priorBend[side].sqrMagnitude>.5f)
            {
                // Parallel transport the previous elbow plane to the new reach
                // direction. Simple projection can flip when a wrist adjustment
                // moves that direction through the pole near a bent arm.
                var transported=Quaternion.FromToRotation(priorDirection[side],direction)*priorBend[side];
                transported=Vector3.ProjectOnPlane(transported,direction).normalized;
                bend=Vector3.RotateTowards(transported,bend,Mathf.Max(0,handStep)*Mathf.Deg2Rad*180,0);
            }
            solvedDirection[side]=direction;solvedBend[side]=bend;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var joint=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var finish=origin+direction*distance;
            var plane=Vector3.Cross(joint-origin,finish-joint).normalized;
            // A full two-axis frame defines forearm roll even when the upper arm
            // points almost opposite its bind direction. FromToRotation alone
            // left that roll undefined and caused visible elbow flips.
            // Bone roll is calibrated once from the bind pose. Rebuilding it
            // from almost collinear neutral segments each frame causes flips.
            upper.rotation=Quaternion.LookRotation(joint-origin,plane)*upperArmFrames[side];
            lower.rotation=Quaternion.LookRotation(finish-joint,plane)*forearmFrames[side];
        }
        void AlignBoot(Transform foot,Transform calf,Quaternion basis)
        {
            foot.rotation=calf.rotation*Quaternion.Inverse(calf==rig.leftCalf?leftCalfRest:rightCalfRest)*basis;
        }
        void KeepAboveFloor()
        {
            if(leftSole==null) return;
            var left=sampleAction>=HomecomingShoeGrip.Detach(0)&&leftStockingSole!=null?leftStockingSole:leftSole;
            var right=sampleAction>=HomecomingShoeGrip.Detach(1)&&rightStockingSole!=null?rightStockingSole:rightSole;
            float minimum=Mathf.Min(left.Minimum(),right.Minimum());
            if(minimum<.012f) rig.hips.position+=Vector3.up*(.012f-minimum);
        }
        Quaternion RestRotation(Transform bone)
        {
            Quaternion result=Quaternion.identity;
            for(var current=bone;current&&current!=rig.transform;current=current.parent)
            {
                Quaternion rotation=current.localRotation;
                foreach(var rest in rig.restPoses) if(rest.bone==current) { rotation=rest.rotation; break; }
                result=rotation*result;
            }
            return result;
        }
        void StandPose()
        {
            if(!lf||!rf||!lh||!rh) return;
            var root=rig.transform;
            Solve(rig.leftThigh,rig.leftCalf,lf,root.TransformPoint(V(.105f,.10f,-.035f)),root.TransformPoint(V(.13f,.8f,-1)));
            Solve(rig.rightThigh,rig.rightCalf,rf,root.TransformPoint(V(-.105f,.10f,-.035f)),root.TransformPoint(V(-.13f,.8f,-1)));
            lf.rotation=root.rotation*leftFootRest; rf.rotation=root.rotation*rightFootRest;
            PoseHands(-1,0,0,0,0,0);
        }
        static float Travel(float t,float duration)
        {
            const float ramp=.5f;
            float elapsed=Mathf.Clamp(t-1.5f,0,duration);
            float distance;
            if(elapsed<ramp) distance=.5f*(elapsed-ramp/Mathf.PI*Mathf.Sin(Mathf.PI*elapsed/ramp));
            else if(elapsed>duration-ramp)
            {
                float remaining=duration-elapsed;
                distance=duration-ramp-.5f*(remaining-ramp/Mathf.PI*Mathf.Sin(Mathf.PI*remaining/ramp));
            }
            else distance=elapsed-ramp*.5f;
            return distance/(duration-ramp);
        }
        void BuildRoute()
        {
            var points=new[]{V(-1.45f,0,4.95f),V(-1.45f,0,2.50f),V(-2.25f,0,2.35f),V(-2.25f,0,.98f),V(-1.25f,0,1.02f)};
            for(int i=0;i<route.Length;i++)
            {
                float value=i*(points.Length-1f)/(route.Length-1);
                int segment=Mathf.Min(points.Length-2,Mathf.FloorToInt(value)); float u=value-segment;
                Vector3 a=points[Mathf.Max(0,segment-1)],b=points[segment],c=points[segment+1],d=points[Mathf.Min(points.Length-1,segment+2)];
                route[i]=.5f*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
                if(i>0) distances[i]=distances[i-1]+Vector3.Distance(route[i],route[i-1]);
            }
            routeLength=distances[distances.Length-1];
        }
        Vector3 Route(float distance)
        {
            distance=Mathf.Clamp(distance,0,routeLength);
            for(int i=1;i<route.Length;i++) if(distances[i]>=distance) return Vector3.Lerp(route[i-1],route[i],Mathf.InverseLerp(distances[i-1],distances[i],distance));
            return route[route.Length-1];
        }
        void SolveLeg(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole,int side)
        {
            var origin=upper.position;
            float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,end.position);
            var direction=(target-origin).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.002f,a+b-.002f);
            var bend=Vector3.ProjectOnPlane(pole-origin,direction).normalized;
            if(legContinuity&&priorLegBend[side].sqrMagnitude>.1f)
            {
                var transported=Quaternion.FromToRotation(priorLegDirection[side],direction)*priorLegBend[side];
                bend=Vector3.RotateTowards(transported,bend,Mathf.Max(0,handStep)*Mathf.Deg2Rad*120,0);
                bend=Vector3.ProjectOnPlane(bend,direction).normalized;
            }
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var joint=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var finish=origin+direction*distance;var plane=Vector3.Cross(joint-origin,finish-joint).normalized;
            upper.rotation=Quaternion.LookRotation(joint-origin,plane)*thighFrames[side];
            lower.rotation=Quaternion.LookRotation(finish-joint,plane)*calfFrames[side];
        }
        public static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            Vector3 origin=upper.position;
            float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,end.position);
            Vector3 direction=(target-origin).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.002f,a+b-.002f);
            Vector3 bend=Vector3.ProjectOnPlane(pole-origin,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            Vector3 joint=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-origin,joint-origin)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,origin+direction*distance-lower.position)*lower.rotation;
        }
    }
}
