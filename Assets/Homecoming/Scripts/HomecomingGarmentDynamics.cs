using System.Collections.Generic;
using UnityEngine;

namespace Homecoming
{
    // Painted REDcloth freedom, welded sewing seams, inertial particles and
    // edge constraints. The source asset and all pinned vertices stay intact.
    public sealed class HomecomingGarmentDynamics : MonoBehaviour
    {
        [SerializeField] float[] maximumDistance;
        [SerializeField] bool seatedHipCompensation;
        SkinnedMeshRenderer skin;
        Mesh source, dynamicMesh, importedMesh, authoredMesh;
        Vector3[] rest, output, goal, originalGoal, previousGoal, position, velocity, stepOrigin, posedRest;
        Vector3[][] shapeDelta;
        float[] shapeWeights;
        Matrix4x4[] boneMatrices, inverseSkin, bindposes;
        BoneWeight[] weights;
        Transform[] bones;
        int[] particle, representative;
        float[] limits,stepLimits;
        bool[] pelvisVertices;
        struct Edge { public int a,b; public float length; }
        Edge[] edges;
        float lastTime=-1;
        public Mesh SourceMesh=>source;
        public Mesh ImportedMesh=>importedMesh;
        public float MaximumDisplacement { get; private set; }
        public float MaximumPinnedDisplacement { get; private set; }
        public int FreeParticles { get; private set; }
        public void Configure(float[] freedom) { maximumDistance=freedom; }
        public void ConfigureSeatedHipCompensation(bool enabled) { seatedHipCompensation=enabled; }

        // Keep the imported skinning for walking. The lap blendshape was authored
        // in pelvis space, so compensate for leg skinning only while seated.
        public Vector3 PoseTarget(CharacterRig rig,Vector3 point,float restHeight,BoneWeight weight,Vector3 skinned)
        {
            if(!seatedHipCompensation)return skinned;
            var renderer=skin?skin:GetComponent<SkinnedMeshRenderer>();
            var mesh=source?source:renderer.sharedMesh;
            int sitting=mesh.GetBlendShapeIndex("Sitting");
            float amount=sitting<0?0:renderer.GetBlendShapeWeight(sitting)*.01f;
            if(amount<=0)return skinned;
            var rigBones=bones??renderer.bones;
            if(!NeedsPelvis(restHeight,weight,rigBones))return skinned;
            int hip=System.Array.IndexOf(rigBones,rig.hips);
            if(hip<0)throw new System.InvalidOperationException("The gown has no pelvis binding");
            var binds=bindposes??mesh.bindposes;
            var hipPoint=(rig.hips.localToWorldMatrix*binds[hip]).MultiplyPoint3x4(point);
            return Vector3.Lerp(skinned,hipPoint,amount);
        }
        static bool Helper(Transform bone,float weight)=>weight>0&&(bone.name.Contains("dress")||bone.name=="W3_Root");
        static bool NeedsPelvis(float height,BoneWeight weight,Transform[] rigBones)=>height<.94f||
            Helper(rigBones[weight.boneIndex0],weight.weight0)||Helper(rigBones[weight.boneIndex1],weight.weight1)||
            Helper(rigBones[weight.boneIndex2],weight.weight2)||Helper(rigBones[weight.boneIndex3],weight.weight3);

        void Initialize()
        {
            skin=GetComponent<SkinnedMeshRenderer>();importedMesh=skin.sharedMesh;
            authoredMesh=HomecomingGarmentPose.Create(GetComponentInParent<CharacterRig>(),importedMesh);
            source=authoredMesh?authoredMesh:importedMesh;
            rest=source.vertices;weights=source.boneWeights;bindposes=source.bindposes;output=new Vector3[rest.Length];particle=new int[rest.Length];
            posedRest=new Vector3[rest.Length];shapeDelta=new Vector3[source.blendShapeCount][];shapeWeights=new float[source.blendShapeCount];
            for(int shape=0;shape<shapeDelta.Length;shape++){shapeDelta[shape]=new Vector3[rest.Length];source.GetBlendShapeFrameVertices(shape,0,shapeDelta[shape],null,null);}
            var groups=new Dictionary<Vector3Int,int>();var reps=new List<int>();var freedom=new List<float>();
            for(int i=0;i<rest.Length;i++)
            {
                var key=Vector3Int.RoundToInt(rest[i]*100000);
                if(!groups.TryGetValue(key,out int id)) { id=groups.Count;groups.Add(key,id);reps.Add(i);freedom.Add(float.MaxValue); }
                particle[i]=id;
                float distance=maximumDistance!=null&&i<maximumDistance.Length?maximumDistance[i]:0;
                freedom[id]=Mathf.Min(freedom[id],distance<=.0001f?0:Mathf.Clamp(distance*.5f,.008f,.11f));
            }
            representative=reps.ToArray();limits=freedom.ToArray();stepLimits=new float[limits.Length];
            foreach(float limit in limits)if(limit>0)FreeParticles++;
            goal=new Vector3[groups.Count];previousGoal=new Vector3[groups.Count];position=new Vector3[groups.Count];velocity=new Vector3[groups.Count];stepOrigin=new Vector3[groups.Count];
            originalGoal=new Vector3[groups.Count];
            bones=skin.bones;inverseSkin=new Matrix4x4[groups.Count];boneMatrices=new Matrix4x4[bones.Length];
            pelvisVertices=new bool[rest.Length];
            if(seatedHipCompensation)for(int i=0;i<rest.Length;i++)pelvisVertices[i]=NeedsPelvis(rest[i].y,weights[i],bones);
            var set=new HashSet<long>();var links=new List<Edge>();var triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)for(int j=0;j<3;j++)
            {
                int a=particle[triangles[i+j]],b=particle[triangles[i+(j+1)%3]];
                if(a>b){int swap=a;a=b;b=swap;}if(a==b)continue;
                if(set.Add(((long)a<<32)|(uint)b))links.Add(new Edge{a=a,b=b,length=Vector3.Distance(rest[representative[a]],rest[representative[b]])});
            }
            edges=links.ToArray();dynamicMesh=Instantiate(source);dynamicMesh.name=source.name+" (simulated fabric)";dynamicMesh.MarkDynamic();
            var bounds=dynamicMesh.bounds;bounds.Expand(.3f);dynamicMesh.bounds=bounds;skin.sharedMesh=dynamicMesh;
        }
        public void Sample(CharacterRig rig,float time)
        {
            if(!dynamicMesh)Initialize();if(lastTime==time)return;
            for(int shape=0;shape<shapeWeights.Length;shape++)shapeWeights[shape]=skin.GetBlendShapeWeight(shape)/100;
            for(int i=0;i<rest.Length;i++){posedRest[i]=rest[i];for(int shape=0;shape<shapeDelta.Length;shape++)posedRest[i]+=shapeDelta[shape][i]*shapeWeights[shape];}
            for(int i=0;i<boneMatrices.Length;i++)boneMatrices[i]=bones[i].localToWorldMatrix*bindposes[i];
            int sitting=source.GetBlendShapeIndex("Sitting"),hip=System.Array.IndexOf(bones,rig.hips);
            float sitAmount=seatedHipCompensation&&sitting>=0?shapeWeights[sitting]:0;
            var pelvisMatrix=hip>=0?boneMatrices[hip]:Matrix4x4.identity;
            for(int i=0;i<goal.Length;i++)
            {
                int vertex=representative[i];var w=weights[vertex];var matrix=new Matrix4x4();
                for(int row=0;row<4;row++)for(int col=0;col<4;col++)
                    matrix[row,col]=boneMatrices[w.boneIndex0][row,col]*w.weight0+boneMatrices[w.boneIndex1][row,col]*w.weight1+boneMatrices[w.boneIndex2][row,col]*w.weight2+boneMatrices[w.boneIndex3][row,col]*w.weight3;
                originalGoal[i]=matrix.MultiplyPoint3x4(posedRest[vertex]);
                goal[i]=sitAmount>0&&pelvisVertices[vertex]?Vector3.Lerp(originalGoal[i],pelvisMatrix.MultiplyPoint3x4(posedRest[vertex]),sitAmount):originalGoal[i];inverseSkin[i]=matrix.inverse;
            }
            bool holding=source.GetBlendShapeIndex("BootLeft")>=0&&(rig.LeftDressLift>.01f||rig.RightDressLift>.01f);
            Vector3 leftHold=holding?rig.DressHoldPoint(0):Vector3.zero,rightHold=holding?rig.DressHoldPoint(1):Vector3.zero;
            for(int i=0;i<limits.Length;i++)
            {
                stepLimits[i]=limits[i];
                if(!holding)continue;
                float left=rig.LeftDressLift*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,.16f,Vector3.Distance(goal[i],leftHold))));
                float right=rig.RightDressLift*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.04f,.16f,Vector3.Distance(goal[i],rightHold))));
                stepLimits[i]*=1-.95f*Mathf.Max(left,right);
            }
            float dt=time-lastTime;bool reset=lastTime<0||dt<=0||dt>.25f;
            if(reset)for(int i=0;i<goal.Length;i++){position[i]=goal[i];velocity[i]=Vector3.zero;previousGoal[i]=goal[i];}
            else
            {
                int steps=Mathf.CeilToInt(dt*120);float h=dt/steps;
                for(int step=0;step<steps;step++)
                {
                    float fraction=(step+1f)/steps;
                    System.Array.Copy(position,stepOrigin,position.Length);
                    for(int i=0;i<goal.Length;i++)
                    {
                        var target=Vector3.Lerp(previousGoal[i],goal[i],fraction);
                        if(limits[i]==0){position[i]=target;continue;}
                        // Momentum comes from the previous world position; moving
                        // shoulders/waist drag the fabric through its sewn edges.
                        velocity[i]+=(65*(target-position[i])-9*velocity[i]+new Vector3(0,-.5f,0))*h;
                        var before=position[i];position[i]+=velocity[i]*h;
                        position[i]=target+Vector3.ClampMagnitude(position[i]-target,stepLimits[i]);
                        velocity[i]=(position[i]-before)/h;
                    }
                    for(int iteration=0;iteration<4;iteration++)
                    {
                        foreach(var edge in edges)
                        {
                            var delta=position[edge.b]-position[edge.a];float length=delta.magnitude;
                            if(length<.000001f)continue;
                            int va=representative[edge.a],vb=representative[edge.b];
                            float desired=shapeDelta.Length>0?Vector3.Distance(posedRest[va],posedRest[vb]):edge.length;
                            float a=limits[edge.a]>0?1:0,b=limits[edge.b]>0?1:0;if(a+b==0)continue;
                            var correction=delta*((length-desired)/length*.65f/(a+b));position[edge.a]+=correction*a;position[edge.b]-=correction*b;
                        }
                        for(int i=0;i<goal.Length;i++)
                        {
                            var target=Vector3.Lerp(previousGoal[i],goal[i],fraction);
                            if(limits[i]==0){position[i]=target;continue;}
                            PreventInward(ref position[i],target,rig.leftThigh.position,rig.leftCalf.position,.08f);
                            PreventInward(ref position[i],target,rig.rightThigh.position,rig.rightCalf.position,.08f);
                            position[i].y=Mathf.Max(.012f,position[i].y);
                            position[i]=target+Vector3.ClampMagnitude(position[i]-target,stepLimits[i]);
                        }
                    }
                    for(int i=0;i<goal.Length;i++)velocity[i]=limits[i]>0?(position[i]-stepOrigin[i])/h:Vector3.zero;
                }
            }
            MaximumDisplacement=0;MaximumPinnedDisplacement=0;
            for(int i=0;i<rest.Length;i++)
            {
                int id=particle[i];var offset=position[id]-goal[id];MaximumDisplacement=Mathf.Max(MaximumDisplacement,offset.magnitude);
                if(limits[id]==0)MaximumPinnedDisplacement=Mathf.Max(MaximumPinnedDisplacement,offset.magnitude);
                output[i]=rest[i]+inverseSkin[id].MultiplyVector(offset+(goal[id]-originalGoal[id]));
            }
            dynamicMesh.vertices=output;dynamicMesh.RecalculateNormals();
            System.Array.Copy(goal,previousGoal,goal.Length);lastTime=time;
        }
        static void PreventInward(ref Vector3 point,Vector3 baseline,Vector3 a,Vector3 b,float radius)
        {
            var axis=b-a;var center=a+axis*Mathf.Clamp01(Vector3.Dot(point-a,axis)/Mathf.Max(.00001f,axis.sqrMagnitude));
            if((point-center).sqrMagnitude>=radius*radius)return;
            var baseCenter=a+axis*Mathf.Clamp01(Vector3.Dot(baseline-a,axis)/Mathf.Max(.00001f,axis.sqrMagnitude));
            if(Vector3.Distance(point,center)<Vector3.Distance(baseline,baseCenter))point=baseline;
        }
        void OnDestroy()
        {
            if(skin&&skin.sharedMesh==dynamicMesh)skin.sharedMesh=importedMesh;
            if(dynamicMesh){if(Application.isPlaying)Destroy(dynamicMesh);else DestroyImmediate(dynamicMesh);}
            if(authoredMesh){if(Application.isPlaying)Destroy(authoredMesh);else DestroyImmediate(authoredMesh);}
        }
    }
}
