using System;
using System.Linq;
using UnityEngine;

namespace Homecoming
{
    // Heel and ball contacts are measured from the original anatomical surface.
    // A minimum-height check alone can leave a barefoot character on tiptoe.
    public sealed class HomecomingFootContact
    {
        readonly Transform foot,toe;
        readonly Vector3 heel,ball,tip;
        // Extraction point cap: passive ankle plantar flexion tops out near
        // 50 degrees, and the old 58-degree cap bent the foot past anatomy
        // (before PlantToes added its own toe curl on top).
        const float MaxPoint=40;
        readonly int[] heelVertices,ballVertices;
        public HomecomingFootContact(SkinnedMeshRenderer skin,Transform joint,Transform toe)
        {
            foot=joint;this.toe=toe;var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
            var bindings=mesh.bindposes;
            var matrices=skin.bones.Select((b,i)=>b.localToWorldMatrix*bindings[i]).ToArray();
            var forward=Vector3.ProjectOnPlane(toe.position-foot.position,Vector3.up).normalized;
            var allWorld=vertices.Select((v,i)=>
            {
                var w=weights[i];return matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
            }).ToArray();
            var indices=Enumerable.Range(0,allWorld.Length).Where(i=>allWorld[i].y<foot.position.y+.012f).ToArray();
            var world=indices.Select(i=>allWorld[i]).ToArray();
            if(world.Length<20||forward.sqrMagnitude<.5f)throw new InvalidOperationException("Original foot contact surface is incomplete");
            float low=world.Min(p=>Vector3.Dot(p-foot.position,forward)),high=world.Max(p=>Vector3.Dot(p-foot.position,forward));
            int[] Contacts(float a,float b)
            {
                var region=indices.Where(i=>Mathf.InverseLerp(low,high,Vector3.Dot(allWorld[i]-foot.position,forward))>=a&&Mathf.InverseLerp(low,high,Vector3.Dot(allWorld[i]-foot.position,forward))<=b).OrderBy(i=>allWorld[i].y).ToArray();
                int count=Mathf.Max(1,Mathf.CeilToInt(region.Length*.15f));
                return region.Take(count).ToArray();
            }
            Vector3 Contact(int[] ids)=>foot.InverseTransformPoint(ids.Aggregate(Vector3.zero,(sum,i)=>sum+allWorld[i])/ids.Length);
            heelVertices=Contacts(0,.25f);ballVertices=Contacts(.60f,.86f);
            heel=Contact(heelVertices);ball=Contact(ballVertices);
            tip=toe.InverseTransformPoint(foot.TransformPoint(Contact(Contacts(.88f,1))));
        }
        public Quaternion Flat(Quaternion rotation)
        {
            var axis=rotation*(ball-heel);var horizontal=Vector3.ProjectOnPlane(axis,Vector3.up);
            return horizontal.sqrMagnitude>.000001f?Quaternion.FromToRotation(axis,horizontal)*rotation:rotation;
        }
        public Quaternion Point(Quaternion flat,Vector3 direction,float amount)
        {
            var aligned=Quaternion.FromToRotation(flat*(ball-heel),direction)*flat;
            return Quaternion.Slerp(flat,Quaternion.RotateTowards(flat,aligned,MaxPoint),amount);
        }
        public Quaternion PointFitted(Quaternion basis,Vector3 direction,float amount,HomecomingFootwearLast fit)
        {
            if(amount<=0)return basis;
            foot.rotation=basis;
            // A high-heel corrective already bends the source sole. Align that
            // actual rendered sole, rather than adding the barefoot angle again.
            var sole=fit.SampleFootAxis(heelVertices,ballVertices);
            fit.RequiredPointAngle=Vector3.Angle(sole,direction);
            var aligned=Quaternion.FromToRotation(sole,direction)*basis;
            return Quaternion.Slerp(basis,Quaternion.RotateTowards(basis,aligned,MaxPoint),amount);
        }
        public float AnkleHeight(Quaternion rotation)=>.012f-Mathf.Min((rotation*heel).y,(rotation*ball).y);
        public void PlantToes(float amount)
        {
            if(amount<=0)return;
            var current=toe.TransformPoint(tip);var desired=current;desired.y=Mathf.Max(current.y,.014f);
            var turn=Quaternion.FromToRotation(current-toe.position,desired-toe.position)*toe.rotation;
            toe.rotation=Quaternion.Slerp(toe.rotation,Quaternion.RotateTowards(toe.rotation,turn,35),amount);
        }
        public float HeelHeight=>foot.TransformPoint(heel).y;
        public float BallHeight=>foot.TransformPoint(ball).y;
    }
}
