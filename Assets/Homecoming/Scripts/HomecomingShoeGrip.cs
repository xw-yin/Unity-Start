using UnityEngine;

namespace Homecoming
{
    public sealed class HomecomingShoeGrip
    {
        readonly HomecomingHandPose palms;
        readonly Quaternion[] handRotation=new Quaternion[2];
        readonly bool[] hasHandRotation=new bool[2];
        readonly Transform[] feet=new Transform[2];
        readonly Transform[] calves=new Transform[2];
        readonly Vector3[] origins=new Vector3[2],heels=new Vector3[2];
        readonly Quaternion[] rest=new Quaternion[2];
        readonly Quaternion[] calfRest=new Quaternion[2];
        readonly bool longBoots;
        readonly bool ankleBoots;
        readonly Vector3[][] bootVertices=new Vector3[2][];
        public HomecomingShoeGrip(CharacterRig rig,HomecomingHandPose handPose)
        {
            palms=handPose;longBoots=rig.longBoots;
            ankleBoots=rig.footwearKind==CharacterRig.FootwearKind.AnkleBoot;
            calves[0]=rig.leftCalf;calves[1]=rig.rightCalf;
            calfRest[0]=calves[0].rotation;calfRest[1]=calves[1].rotation;
            foreach(var bone in rig.GetComponentsInChildren<Transform>(true))
            {
                if(bone.name=="J_Bip_L_Foot") feet[0]=bone;
                if(bone.name=="J_Bip_R_Foot") feet[1]=bone;
            }
            if(!rig.importedSource) return;
            var mesh=new Mesh();
            for(int side=0;side<2;side++)
            {
                var skin=(side==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>();
                skin.BakeMesh(mesh);
                var vertices=mesh.vertices; var bounds=new Bounds(skin.transform.TransformPoint(vertices[0]),Vector3.zero);
                foreach(var v in vertices) bounds.Encapsulate(skin.transform.TransformPoint(v));
                var origin=bounds.center; origin.y=bounds.min.y;
                origins[side]=feet[side].InverseTransformPoint(origin); rest[side]=feet[side].rotation;
                var ground=(side==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>().sharedMesh;
                bootVertices[side]=ground.vertices;
                var wanted=longBoots||ankleBoots?new Vector3(side==0?ground.bounds.max.x-.008f:ground.bounds.min.x+.008f,ground.bounds.max.y-.02f,ground.bounds.min.z+.055f):new Vector3(0,ground.bounds.max.y*.75f,ground.bounds.max.z-.018f);
                float distance=float.MaxValue;
                foreach(var v in ground.vertices)
                {
                    var delta=v-wanted; float score=delta.x*delta.x*3+delta.y*delta.y+delta.z*delta.z;
                    if(score<distance) { distance=score; heels[side]=v; }
                }
            }
            if(Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
        }
        public static float Start(int side)=>side==0?10:16;
        public static float Detach(int side)=>Start(side)+2;
        public float Cleared(int side)=>Start(side)+(longBoots?3.05f:2.8f);
        public static float Release(int side)=>Start(side)+4.4f;
        static float Ease(float t,float a,float b) { float u=Mathf.Clamp01((t-a)/(b-a)); return u*u*u*(u*(u*6-15)+10); }
        Quaternion AttachedRotation(int side)=>longBoots?calves[side].rotation*Quaternion.Inverse(calfRest[side]):feet[side].rotation*Quaternion.Inverse(rest[side]);
        public Vector3 HeelOnFoot(int side)=>feet[side].position+AttachedRotation(side)*(rest[side]*origins[side]+heels[side]);
        public Matrix4x4 AttachedMatrix(int side)=>Matrix4x4.TRS(HeelOnFoot(side)-AttachedRotation(side)*heels[side],AttachedRotation(side),Vector3.one);
        public Vector3 GroundOrigin(int side)=>longBoots
            ?new Vector3(side==0?-.64f:-1.86f,.012f,1.23f)
            :new Vector3(side==0?-.86f:-1.64f,.012f,1.08f);
        public Quaternion Rotation(int side,float action)=>Quaternion.Slerp(longBoots?AttachedRotation(side):Quaternion.identity,Quaternion.Euler(0,side==0?-90:90,0),Ease(action,Start(side)+(longBoots?3.05f:2.8f),Release(side)));
        public Vector3 GroundHeel(int side)=>GroundOrigin(side)+Quaternion.Euler(0,side==0?-90:90,0)*heels[side];
        public void SetHandRotation(int side,Quaternion rotation){handRotation[side]=rotation;hasHandRotation[side]=true;}
        Quaternion GripRotation(int side,float action)=>hasHandRotation[side]?handRotation[side]:palms.BootRotation(side,Rotation(side,action));
        public Vector3 WristTarget(int side,float action)
        {
            float start=Start(side);
            if(longBoots||ankleBoots)
            {
                var axis=AttachedRotation(side)*Vector3.up;
                float clearance=longBoots?.50f:.22f;
                float clearTime=longBoots?3.05f:2.8f;
                var cuff=HeelOnFoot(side)-axis*clearance*Ease(action,start+2,start+clearTime);
                cuff=Vector3.Lerp(cuff,GroundHeel(side),Ease(action,start+clearTime,Release(side)));
                // Carry the freed boot along a lifted arc before easing it down.
                float carry=Ease(action,start+clearTime,Release(side));
                cuff+=Vector3.up*.14f*Mathf.Sin(Mathf.PI*carry);
                if(action>=Detach(side))
                {
                    var turn=Rotation(side,action);float bottom=float.MaxValue;
                    foreach(var v in bootVertices[side]) bottom=Mathf.Min(bottom,(cuff+turn*(v-heels[side])).y);
                    if(bottom<.012f)cuff.y+=.012f-bottom;
                }
                return palms.Wrist(side,HomecomingHandPose.Hold.Boot,cuff,GripRotation(side,action));
            }
            var grip=HeelOnFoot(side)+new Vector3(0,-.025f,.17f)*Ease(action,start+2,start+2.8f);
            grip=Vector3.Lerp(grip,GroundHeel(side),Ease(action,start+2.8f,Release(side)));
            return palms.Wrist(side,HomecomingHandPose.Hold.Boot,grip,GripRotation(side,action));
        }
        public Vector3 HandContact(int side)=>palms.Contact(side,HomecomingHandPose.Hold.Boot);
        public void PlaceProxy(Transform proxy,int side,float action)
        {
            if(action<Release(side))
            {
                proxy.rotation=Rotation(side,action);
                proxy.position=HandContact(side)-proxy.rotation*heels[side];
            }
            else proxy.SetPositionAndRotation(GroundOrigin(side),Quaternion.Euler(0,side==0?-90:90,0));
        }
        public float ContactError(Transform proxy,int side)=>Vector3.Distance(proxy.TransformPoint(heels[side]),HandContact(side));
    }
}
