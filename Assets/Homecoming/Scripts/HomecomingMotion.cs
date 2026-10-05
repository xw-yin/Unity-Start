using UnityEngine;

namespace Homecoming
{
    // Contact targets are in scene space. The skeleton is solved after the pelvis,
    // so imported bone axes never determine where a foot or a hand ends up.
    public sealed class HomecomingMotion
    {
        readonly CharacterRig rig;
        readonly Transform lf,rf,lh,rh;
        readonly Quaternion leftFootRest,rightFootRest;
        readonly Quaternion leftCalfRest,rightCalfRest;
        public const float Duration=27;
        const float BootLegLift=.36f,BootLegBack=.10f,BootLean=28,BootHipForward=.12f;
        readonly HomecomingWalkCycle walk;
        readonly Transform[] walkBones;
        readonly Vector3[] route=new Vector3[321];
        readonly float[] distances=new float[321];
        float routeLength;
        readonly Sole leftSole,rightSole;
        readonly Sole leftStockingSole,rightStockingSole;
        float sampleAction;
        readonly HomecomingClothDynamics cloth;
        readonly HomecomingGarmentDynamics[] garments;
        readonly bool longDress;
        readonly float[] thighClearance=new float[2];
        readonly Vector3[] handTargets=new Vector3[2];
        readonly Quaternion[] handRotations=new Quaternion[2];
        readonly HomecomingHandPose.Hold[] handModes=new HomecomingHandPose.Hold[2];
        readonly float[] fingerAmounts=new float[2],fabricReach=new float[2],supportReach=new float[2];
        readonly Vector3[] elbowBend=new Vector3[2];
        readonly Quaternion[] previousHandTurn=new Quaternion[2];
        float previousSample=-1,handStep;bool elbowsInitialized;
        public HomecomingShoeGrip Shoes { get; private set; }
        public HomecomingHandPose Hands { get; private set; }
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
            longDress=rig.skirt&&rig.skirt.sharedMesh.GetBlendShapeIndex("BootLeft")>=0;
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
            walk=Resources.Load<HomecomingWalkCycle>("Homecoming/WalkCycle");
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
            }
            if(lf) leftFootRest=RestRotation(lf);
            if(rf) rightFootRest=RestRotation(rf);
            leftCalfRest=RestRotation(rig.leftCalf);rightCalfRest=RestRotation(rig.rightCalf);
        }
        static float Ease(float t,float a,float b) { float u=Mathf.Clamp01((t-a)/(b-a)); return u*u*u*(u*(u*6-15)+10); }
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        public void Sample(float t)
        {
            SamplePose(t);
            if(cloth) cloth.Sample(rig,t);
            foreach(var garment in garments)garment.Sample(rig,t);
        }
        void SamplePose(float t)
        {
            handStep=t-previousSample;
            if(t<8||handStep<0||handStep>.2f)elbowsInitialized=false;
            previousSample=t;
            sampleAction=t-1;
            rig.Neutral();
            rig.SetDressLift(0,0);
            if(t<8)
            {
                rig.SetSkirtPose(0);
                float distance=routeLength*Travel(t);
                rig.transform.localPosition=Route(distance);
                var direction=Route(Mathf.Min(routeLength,distance+.55f))-Route(Mathf.Max(0,distance-.55f));
                Quaternion heading=direction.sqrMagnitude>.000001f ? Quaternion.LookRotation(direction)*Quaternion.Euler(0,180,0) : Quaternion.identity;
                rig.transform.localRotation=Quaternion.Slerp(heading,Quaternion.identity,Ease(t,6.9f,8));
                rig.hips.localPosition=rig.hipRestPosition;
                StandPose();
                float weight=Ease(t,1.5f,2)*(1-Ease(t,6.6f,8));
                if(walk)
                {
                    float phase=distance/walk.strideDistance;
                    for(int i=0;i<walkBones.Length;i++) if(walkBones[i])
                    {
                        string name=walkBones[i].name;
                        bool arm=name.Contains("Arm")||name.Contains("Shoulder")||name.Contains("Hand");
                        float blend=arm?.60f*Ease(t,1.5f,2.6f)*(1-Ease(t,6.2f,8)):weight;
                        walkBones[i].localRotation=Quaternion.Slerp(walkBones[i].localRotation,walk.Rotation(i,phase),blend);
                    }
                    rig.hips.localPosition=Vector3.Lerp(rig.hipRestPosition,walk.Hip(phase),weight);

                }
                Hands.Fingers(0,HomecomingHandPose.Hold.Rest,0);Hands.Fingers(1,HomecomingHandPose.Hold.Rest,0);
                if(rig.longBoots) { AlignBoot(lf,rig.leftCalf,leftFootRest); AlignBoot(rf,rig.rightCalf,rightFootRest); }
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
            rig.hips.position+=Vector3.right*.25f*(lp-rp);
            rig.hips.position+=Vector3.back*BootHipForward*(l+r);
            float lean=BootLean*(l+r)+(rig.longBoots?8:10)*(lp+rp)+12*Ease(t,7,8)*(1-Ease(t,8,9))+12*Ease(t,22,23)*(1-Ease(t,24,26));
            rig.Pose(rig.spine,V(-lean+.65f*Mathf.Sin(t*1.7f),0,2*(lp-rp)));
            rig.Pose(rig.head,V(8*(l+r),-5*(l-r),0));
            foreach(var pose in rig.restPoses)
            {
                if(pose.bone.name=="J_Bip_L_Shoulder")rig.Pose(pose.bone,V(0,20,-6)*Mathf.Max(l,.75f*r));
                if(pose.bone.name=="J_Bip_R_Shoulder")rig.Pose(pose.bone,V(0,-20,6)*Mathf.Max(r,.75f*l));
            }
            Vector3 left=Vector3.Lerp(rig.transform.TransformPoint(V(.105f,.10f,-.035f)),V(-1.145f,.10f,1.10f),sit);
            Vector3 right=Vector3.Lerp(rig.transform.TransformPoint(V(-.105f,.10f,-.035f)),V(-1.355f,.10f,1.10f),sit);
            float ll=Ease(t,10,11.4f)*(1-Ease(t,13.35f,14.4f));
            float rl=Ease(t,16,17.4f)*(1-Ease(t,19.35f,20.4f));
            float lift=longDress?(rig.longBoots?.14f:.32f):rig.longBoots?BootLegLift:.40f;
            // The free hand lifts and holds the hem before the shoe hand reaches.
            // Release the fabric once the shaft clears the foot, before leaning
            // to place the shoe. Holding it at lap height through that lean
            // drove a lifted panel into the chest and face.
            float gatherLeft=Ease(t,9.15f,10)*(1-Ease(t,13.1f,14));
            float gatherRight=Ease(t,15.15f,16)*(1-Ease(t,19.1f,20));
            rig.SetDressLift(gatherLeft,gatherRight);
            left+=V(.07f,lift,BootLegBack)*ll;right+=V(-.07f,lift,BootLegBack)*rl;
            Solve(rig.leftThigh,rig.leftCalf,lf,left,rig.hips.position+V(.16f,-.02f,-1));
            Solve(rig.rightThigh,rig.rightCalf,rf,right,rig.hips.position+V(-.16f,-.02f,-1));
            if(rig.longBoots){AlignBoot(lf,rig.leftCalf,leftFootRest);AlignBoot(rf,rig.rightCalf,rightFootRest);}
            else{lf.rotation=leftFootRest;rf.rotation=rightFootRest;}
            // Point the toes through the shaft as the boot is pulled away; the boot
            // follows the calf, so the ankle can flex independently inside its opening.
            if(rig.longBoots)
            {
                lf.rotation*=Quaternion.Euler(65*Ease(t,12.20f,12.65f)*(1-Ease(t,13.15f,14.4f)),0,0);
                rf.rotation*=Quaternion.Euler(65*Ease(t,18.20f,18.65f)*(1-Ease(t,19.15f,20.4f)),0,0);
            }
            lf.rotation=Quaternion.Slerp(lf.rotation,leftFootRest,Ease(t,13.35f,14.4f));
            rf.rotation=Quaternion.Slerp(rf.rotation,rightFootRest,Ease(t,19.35f,20.4f));
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
            return upper.position+Vector3.down*(length*.93f)+outward*.045f+rig.transform.forward*-.025f;
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
                fingers[side]=Ease(t,start+1.35f,start+1.95f)*(1-Ease(t,start+4.4f,start+4.85f))*reach;
                modes[side]=HomecomingHandPose.Hold.Boot;
                if(support[side]>0)
                {
                    var workingKnee=side==0?rig.rightCalf:rig.leftCalf;
                    var workingFoot=side==0?rf:lf;
                    var direction=(workingFoot.position-workingKnee.position).normalized;
                    var inward=Vector3.ProjectOnPlane(rig.transform.forward,direction).normalized;
                    var supportDirection=Vector3.ProjectOnPlane(workingKnee.position-(side==0?rig.leftArm:rig.rightArm).position,inward).normalized;
                    var turn=Hands.Rotation(side,inward,supportDirection);
                    var point=workingKnee.position-inward*.065f+Vector3.up*.10f+rig.transform.right*(side==0?.04f:-.04f);
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
                    fingers[side]=Ease(t,startLift-.1f,startLift+.4f)*(1-Ease(t,startLift+4.85f,startLift+5.5f));
                    modes[side]=HomecomingHandPose.Hold.Fabric;
                }
            }
            float leftLength=Vector3.Distance(rig.leftArm.position,rig.leftElbow.position)+Vector3.Distance(rig.leftElbow.position,lh.position);
            float rightLength=Vector3.Distance(rig.rightArm.position,rig.rightElbow.position)+Vector3.Distance(rig.rightElbow.position,rh.position);
            bool Beyond()=>Vector3.Distance(rig.leftArm.position,targets[0])>leftLength-.008f||Vector3.Distance(rig.rightArm.position,targets[1])>rightLength-.008f;
            float effort=Mathf.Max(Mathf.Max(leftReach,rightReach),Mathf.Max(fabric[0],fabric[1]));
            if(effort>.001f&&Beyond())
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
                float forearm=Vector3.Distance(elbow.position,hand.position);
                // Choose the elbow plane that lets the hand continue the forearm,
                // instead of forcing a sideways elbow and folding the wrist back.
                var direction=(targets[side]-upper.position).normalized;
                var lateralPlane=Vector3.ProjectOnPlane(rig.transform.right*(side==0?1:-1)-rig.transform.forward*.25f,direction).normalized;
                var desiredBend=Vector3.ProjectOnPlane(targets[side]-Hands.Direction(side,rotations[side])*forearm-upper.position,direction);
                // Elbows stay below the shoulders. Finger direction alone can
                // select a raised-elbow pose when the cuff is close to the torso.
                var hanging=Vector3.down*.28f+rig.transform.right*(side==0?.16f:-.16f)-rig.transform.forward*.06f;
                desiredBend=Vector3.ProjectOnPlane(hanging,direction)+desiredBend*.2f;
                float outward=Vector3.Dot(desiredBend,lateralPlane);
                // Keep the elbow on the body's outer side when the preferred
                // plane passes close to the straight-arm singularity.
                float margin=Mathf.Sqrt(outward*outward+.085f*.085f);
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
                hand.rotation=turn;Hands.Fingers(side,modes[side],fingers[side]);
            }
            elbowsInitialized=true;
        }
        static void SolveArm(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            var origin=upper.position;var originalUpper=lower.position-origin;var originalLower=end.position-lower.position;
            var upperRotation=upper.rotation;var lowerRotation=lower.rotation;
            var reference=Vector3.Cross(originalUpper,originalLower).normalized;
            if(reference.sqrMagnitude<.1f)reference=Vector3.ProjectOnPlane(upper.forward,originalUpper).normalized;
            float a=originalUpper.magnitude,b=originalLower.magnitude;
            var direction=(target-origin).normalized;
            float distance=Mathf.Clamp(Vector3.Distance(origin,target),Mathf.Abs(a-b)+.002f,a+b-.002f);
            var bend=Vector3.ProjectOnPlane(pole-origin,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var joint=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            var finish=origin+direction*distance;
            var plane=Vector3.Cross(joint-origin,finish-joint).normalized;
            // A full two-axis frame defines forearm roll even when the upper arm
            // points almost opposite its bind direction. FromToRotation alone
            // left that roll undefined and caused visible elbow flips.
            upper.rotation=Quaternion.LookRotation(joint-origin,plane)*Quaternion.Inverse(Quaternion.LookRotation(originalUpper,reference))*upperRotation;
            lower.rotation=Quaternion.LookRotation(finish-joint,plane)*Quaternion.Inverse(Quaternion.LookRotation(originalLower,reference))*lowerRotation;
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
        static float Travel(float t)
        {
            const float duration=5.8f,ramp=.5f;
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
