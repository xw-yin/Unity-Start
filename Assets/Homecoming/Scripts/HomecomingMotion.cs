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
        public HomecomingShoeGrip Shoes { get; private set; }
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
                foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(skin.name=="左腿连续丝袜")leftStockingSole=new Sole(skin);
                    if(skin.name=="右腿连续丝袜")rightStockingSole=new Sole(skin);
                }
            }
            walk=Resources.Load<HomecomingWalkCycle>("Homecoming/WalkCycle");
            if(walk) { walkBones=new Transform[walk.bones.Length];
                var all=rig.GetComponentsInChildren<Transform>(true);
                for(int i=0;i<walkBones.Length;i++) foreach(var bone in all) if(bone.name==walk.bones[i]) { walkBones[i]=bone; break; }
            }
            BuildRoute();
            Shoes=new HomecomingShoeGrip(rig);
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
            sampleAction=t-1;
            rig.Neutral();
            rig.SetDressLift(0,0);
            if(t<8)
            {
                rig.SetSkirtPose(0);
                float distance=routeLength*Travel(t);
                rig.transform.localPosition=Route(distance);
                var direction=Route(Mathf.Min(routeLength,distance+.04f))-Route(Mathf.Max(0,distance-.04f));
                Quaternion heading=direction.sqrMagnitude>.000001f ? Quaternion.LookRotation(direction)*Quaternion.Euler(0,180,0) : Quaternion.identity;
                rig.transform.localRotation=Quaternion.Slerp(heading,Quaternion.identity,Ease(t,6.9f,8));
                rig.hips.localPosition=rig.hipRestPosition;
                StandPose();
                float weight=Ease(t,1.5f,2)*(1-Ease(t,6.6f,8));
                if(walk)
                {
                    float phase=distance/walk.strideDistance;
                    for(int i=0;i<walkBones.Length;i++) if(walkBones[i]) walkBones[i].localRotation=Quaternion.Slerp(walkBones[i].localRotation,walk.Rotation(i,phase),weight);
                    rig.hips.localPosition=Vector3.Lerp(rig.hipRestPosition,walk.Hip(phase),weight);
                    // Carry the handbag without the full free-arm swing from the source.
                    foreach(var rest in rig.restPoses) if(rest.bone==rig.rightArm) rig.rightArm.localRotation=Quaternion.Slerp(rest.rotation,rig.rightArm.localRotation,1-.55f*weight);
                }
                if(rig.longBoots) { AlignBoot(lf,rig.leftCalf,leftFootRest); AlignBoot(rf,rig.rightCalf,rightFootRest); }
                KeepAboveFloor();
                return;
            }
            t-=1;
            if(rig.longBoots) { SampleLongBoots(t); return; }
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
            Vector3 lIdle=rig.transform.position+V(.26f,.69f,-.04f);
            Vector3 rIdle=rig.transform.position+V(-.26f,.69f,-.04f);
            Vector3 lKnee=rig.leftCalf.position+V(.025f,.09f,-.055f);
            Vector3 rKnee=rig.rightCalf.position+V(-.025f,.09f,-.055f);
            Vector3 lTarget=Vector3.Lerp(lIdle,lKnee,seated);
            Vector3 rTarget=Vector3.Lerp(rIdle,rKnee,seated);
            lTarget=Vector3.Lerp(lTarget,Shoes.WristTarget(0,t),lReach);
            rTarget=Vector3.Lerp(rTarget,Shoes.WristTarget(1,t),rReach);
            Solve(rig.leftArm,rig.leftElbow,lh,lTarget,rig.hips.position+V(.65f,.08f,-.30f));
            Solve(rig.rightArm,rig.rightElbow,rh,rTarget,rig.hips.position+V(-.65f,.08f,-.30f));
            GripHand(lh,true,lReach,Shoes.Rotation(0,t)); GripHand(rh,false,rReach,Shoes.Rotation(1,t));
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
            float lp=Ease(t,13.05f,14.2f)*(1-Ease(t,14.4f,15.2f));
            float rp=Ease(t,19.05f,20.2f)*(1-Ease(t,20.4f,21.2f));
            rig.hips.position+=Vector3.right*.25f*(lp-rp);
            rig.hips.position+=Vector3.back*BootHipForward*(l+r);
            float lean=BootLean*(l+r)+8*(lp+rp)+12*Ease(t,7,8)*(1-Ease(t,8,9))+12*Ease(t,22,23)*(1-Ease(t,24,26));
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
            float lift=longDress?.14f:BootLegLift;
            rig.SetDressLift(ll,rl);
            left+=V(.07f,lift,BootLegBack)*ll;right+=V(-.07f,lift,BootLegBack)*rl;
            Solve(rig.leftThigh,rig.leftCalf,lf,left,rig.hips.position+V(.16f,-.02f,-1));
            Solve(rig.rightThigh,rig.rightCalf,rf,right,rig.hips.position+V(-.16f,-.02f,-1));
            AlignBoot(lf,rig.leftCalf,leftFootRest);AlignBoot(rf,rig.rightCalf,rightFootRest);
            // Point the toes through the shaft as the boot is pulled away; the boot
            // follows the calf, so the ankle can flex independently inside its opening.
            lf.rotation*=Quaternion.Euler(65*Ease(t,12.20f,12.65f)*(1-Ease(t,13.15f,14.4f)),0,0);
            rf.rotation*=Quaternion.Euler(65*Ease(t,18.20f,18.65f)*(1-Ease(t,19.15f,20.4f)),0,0);
            lf.rotation=Quaternion.Slerp(lf.rotation,leftFootRest,Ease(t,13.35f,14.4f));
            rf.rotation=Quaternion.Slerp(rf.rotation,rightFootRest,Ease(t,19.35f,20.4f));
            KeepAboveFloor();
            Vector3 li=Vector3.Lerp(rig.transform.TransformPoint(V(.26f,.69f,-.04f)),rig.leftCalf.position+V(.025f,.055f,.035f),sit);
            Vector3 ri=Vector3.Lerp(rig.transform.TransformPoint(V(-.26f,.69f,-.04f)),rig.rightCalf.position+V(-.025f,.055f,.035f),sit);
            // The reach follows a small arc; the wrist settles before the fingers close.
            Vector3 lt=Vector3.Lerp(li,Shoes.WristTarget(0,t),l)+V(.025f,.045f,-.035f)*Mathf.Sin(Mathf.PI*l);
            Vector3 rt=Vector3.Lerp(ri,Shoes.WristTarget(1,t),r)+V(-.025f,.045f,-.035f)*Mathf.Sin(Mathf.PI*r);
            float helpLeft=Ease(t,17,17.7f)*(1-Ease(t,18.4f,19.1f));
            float helpRight=Ease(t,11,11.7f)*(1-Ease(t,12.4f,13.1f));
            lt=Vector3.Lerp(lt,Shoes.WristTarget(1,t)+V(.06f,0,0),helpLeft);
            rt=Vector3.Lerp(rt,Shoes.WristTarget(0,t)+V(-.06f,0,0),helpRight);
            if(longDress)
            {
                // The free hand gathers the front hem while the other hand
                // removes the boot, rather than reaching through the skirt.
                lt=Vector3.Lerp(lt,rig.rightCalf.position+V(-.02f,.10f,-.20f),r*(1-helpLeft));
                rt=Vector3.Lerp(rt,rig.leftCalf.position+V(.02f,.10f,-.20f),l*(1-helpRight));
            }
            Solve(rig.leftArm,rig.leftElbow,lh,lt,rig.leftArm.position+V(.55f,-.30f,-.1f));
            Solve(rig.rightArm,rig.rightElbow,rh,rt,rig.rightArm.position+V(-.55f,-.30f,-.1f));
            lh.rotation=Quaternion.Slerp(lh.rotation,Quaternion.FromToRotation(lh.TransformDirection(Vector3.left),Vector3.back)*lh.rotation,sit*(1-Mathf.Max(l,helpLeft)));
            rh.rotation=Quaternion.Slerp(rh.rotation,Quaternion.FromToRotation(rh.TransformDirection(Vector3.right),Vector3.back)*rh.rotation,sit*(1-Mathf.Max(r,helpRight)));
            BootHand(lh,true,Mathf.Max(l,helpLeft),Quaternion.Slerp(Shoes.Rotation(0,t),Shoes.Rotation(1,t),helpLeft),t,0,helpLeft);
            BootHand(rh,false,Mathf.Max(r,helpRight),Quaternion.Slerp(Shoes.Rotation(1,t),Shoes.Rotation(0,t),helpRight),t,1,helpRight);
        }
        void BootHand(Transform hand,bool left,float reach,Quaternion boot,float t,int side,float help)
        {
            Vector3 finger=left?Vector3.left:Vector3.right;
            var desired=Quaternion.FromToRotation(finger,boot*Vector3.down);
            // Establish an absolute orientation instead of compounding the boot rotation.
            hand.rotation=Quaternion.Slerp(hand.rotation,desired,reach);
            float start=HomecomingShoeGrip.Start(side);
            float grip=Ease(t,start+1.35f,start+1.95f)*(1-Ease(t,start+4.4f,start+4.85f));
            grip=Mathf.Max(grip,help*.8f);
            foreach(var pose in rig.restPoses)
            {
                if(!pose.bone||pose.bone==hand||!pose.bone.IsChildOf(hand)||!pose.bone.name.StartsWith("J_Bip_"))continue;
                string name=pose.bone.name;
                float curl=name.Contains("Thumb")?18:name.EndsWith("1")?28:name.EndsWith("2")?52:32;
                float delay=name.Contains("Index")?0:name.Contains("Middle")?.06f:name.Contains("Ring")?.12f:.18f;
                float closed=Mathf.SmoothStep(0,1,Mathf.Clamp01((grip-delay)/(1-delay)));
                pose.bone.localRotation=pose.rotation*Quaternion.Euler(0,(left?1:-1)*curl*closed,0);
            }
        }
        void AlignBoot(Transform foot,Transform calf,Quaternion basis)
        {
            foot.rotation=calf.rotation*Quaternion.Inverse(calf==rig.leftCalf?leftCalfRest:rightCalfRest)*basis;
        }
        void GripHand(Transform hand,bool left,float amount,Quaternion turn)
        {
            var axis=left?Vector3.left:Vector3.right;
            var down=turn*Quaternion.FromToRotation(hand.TransformDirection(axis),Vector3.down)*hand.rotation;
            hand.rotation=Quaternion.Slerp(hand.rotation,down,amount);
            foreach(var pose in rig.restPoses)
            {
                if(!pose.bone||!pose.bone.IsChildOf(hand)||pose.bone==hand) continue;
                string name=pose.bone.name;
                if(!name.StartsWith("J_Bip_")) continue;
                if(name.Contains("Thumb")) continue;
                float curl=name.EndsWith("1")?22:name.EndsWith("2")?38:24;
                pose.bone.localRotation=pose.rotation*Quaternion.Euler(0,(left?1:-1)*curl*amount,0);
            }
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
            Solve(rig.leftArm,rig.leftElbow,lh,root.TransformPoint(V(.26f,.69f,-.04f)),root.TransformPoint(V(.65f,.98f,-.30f)));
            Solve(rig.rightArm,rig.rightElbow,rh,root.TransformPoint(V(-.26f,.69f,-.04f)),root.TransformPoint(V(-.65f,.98f,-.30f)));
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
