using UnityEngine;

namespace Homecoming
{
    // Transfer the currently bent, skinned boot to the hand without a mesh jump.
    // Once the foot is clear, ease the empty boot back to its resting shape.
    public sealed class HomecomingBootSnapshot : MonoBehaviour
    {
        Mesh mesh,template;
        Vector3[] captured,normals,restNormals,points;
        float previous=-1,previousFold=-1;
        [System.Serializable] class Shape { public int vertexCount;public float[] delta; }
        Vector3[] authoredDelta;
        Vector3[] sourcePoints,sourceNormals;BoneWeight[] sourceWeights;Transform[] sourceBones;
        Matrix4x4[] bindings,frozenMatrices,currentMatrices;bool[] frozenFoot,usedBones;
        Quaternion captureInverse;Vector3 captureOrigin;int cuffIndex;
        public Vector3[] Points=>points;
        public void ResetCapture()
        {
            if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);mesh=null;}
            if(template)GetComponent<MeshFilter>().sharedMesh=template;
            previous=previousFold=-1;authoredDelta=null;
        }
        public void UpdateShape(SkinnedMeshRenderer skin,Quaternion rotation,Vector3 cuff,Vector3 localCuff,Vector3[] relaxed,float amount,float fold=0)
        {
            if(!mesh)
            {
                var filter=GetComponent<MeshFilter>();if(!template)template=filter.sharedMesh;restNormals=template.normals;
                mesh=new Mesh{name="Held original boot",hideFlags=HideFlags.DontSave};skin.BakeMesh(mesh);
                captured=mesh.vertices;normals=mesh.normals;points=new Vector3[captured.Length];
                var shapeAsset=Resources.Load<TextAsset>("Homecoming/Poses/BootShape_"+skin.sharedMesh.name);
                if(shapeAsset)
                {
                    var shape=JsonUtility.FromJson<Shape>(shapeAsset.text);
                    if(shape.vertexCount!=captured.Length||shape.delta.Length!=captured.Length*3)throw new System.InvalidOperationException("Authored boot shape no longer matches source topology");
                    authoredDelta=new Vector3[captured.Length];
                    for(int i=0;i<captured.Length;i++)authoredDelta[i]=new Vector3(shape.delta[i*3],shape.delta[i*3+1],shape.delta[i*3+2]);
                }
                var inverse=Quaternion.Inverse(rotation);var origin=cuff-rotation*localCuff;
                for(int i=0;i<captured.Length;i++)
                {
                    captured[i]=inverse*(skin.transform.TransformPoint(captured[i])-origin);
                    normals[i]=(inverse*skin.transform.TransformDirection(normals[i])).normalized;
                }
                filter.sharedMesh=mesh;
                sourcePoints=skin.sharedMesh.vertices;sourceNormals=skin.sharedMesh.normals;sourceWeights=skin.sharedMesh.boneWeights;sourceBones=skin.bones;bindings=skin.sharedMesh.bindposes;
                frozenMatrices=new Matrix4x4[sourceBones.Length];currentMatrices=new Matrix4x4[sourceBones.Length];frozenFoot=new bool[sourceBones.Length];usedBones=new bool[sourceBones.Length];
                foreach(var w in sourceWeights){if(w.weight0>0)usedBones[w.boneIndex0]=true;if(w.weight1>0)usedBones[w.boneIndex1]=true;if(w.weight2>0)usedBones[w.boneIndex2]=true;if(w.weight3>0)usedBones[w.boneIndex3]=true;}
                for(int bone=0;bone<sourceBones.Length;bone++)
                {
                    frozenMatrices[bone]=sourceBones[bone].localToWorldMatrix*bindings[bone];
                    for(var parent=sourceBones[bone];parent;parent=parent.parent)
                        if(parent.name=="J_Bip_L_Foot"||parent.name=="J_Bip_R_Foot"){frozenFoot[bone]=true;break;}
                }
                captureInverse=inverse;captureOrigin=origin;cuffIndex=0;
                for(int i=1;i<relaxed.Length;i++)if((relaxed[i]-localCuff).sqrMagnitude<(relaxed[cuffIndex]-localCuff).sqrMagnitude)cuffIndex=i;
            }
            bool sameShape=Mathf.Abs(amount-previous)<.000001f&&Mathf.Abs(fold-previousFold)<.000001f;
            if(amount>=.999999f&&sameShape)return;
            bool changed=false;
            for(int bone=0;bone<sourceBones.Length;bone++)if(usedBones[bone])
            {
                var next=frozenFoot[bone]?frozenMatrices[bone]:sourceBones[bone].localToWorldMatrix*bindings[bone];
                if(next!=currentMatrices[bone])changed=true;currentMatrices[bone]=next;
            }
            if(sameShape&&!changed)return;
            previous=amount;previousFold=fold;var blendedNormals=new Vector3[points.Length];
            float ankle=.14f,height=Mathf.Max(.1f,localCuff.y-ankle);
            Vector3 FoldOffset(Vector3 p)
            {
                float h=Mathf.Clamp01((p.y-ankle)/height),envelope=h*(1-h);
                return new Vector3(Mathf.Sign(localCuff.x)*.06f*fold*h*h,-Mathf.Min(.16f,height*.4f)*fold*Mathf.SmoothStep(0,1,h),.012f*fold*Mathf.Sin(h*Mathf.PI*6)*envelope);
            }
            var gripOffset=FoldOffset(localCuff);
            // Keep the hard toe/heel frame from transfer, while the soft shaft
            // follows the calf through extraction. Freezing the whole bent boot
            // left its shaft behind when the knee moved and caused calf clipping.
            Vector3 Live(int i)
            {
                var w=sourceWeights[i];var p=sourcePoints[i];
                var world=currentMatrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+currentMatrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+currentMatrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+currentMatrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                return captureInverse*(world-captureOrigin);
            }
            var shaftAnchor=Live(cuffIndex)-localCuff;
            Vector3 LiveNormal(int i)
            {
                var w=sourceWeights[i];var n=sourceNormals[i];
                return (captureInverse*(currentMatrices[w.boneIndex0].MultiplyVector(n)*w.weight0+currentMatrices[w.boneIndex1].MultiplyVector(n)*w.weight1+currentMatrices[w.boneIndex2].MultiplyVector(n)*w.weight2+currentMatrices[w.boneIndex3].MultiplyVector(n)*w.weight3)).normalized;
            }
            int gripIndex=0;
            if(authoredDelta!=null)
                for(int i=1;i<relaxed.Length;i++)if((relaxed[i]-localCuff).sqrMagnitude<(relaxed[gripIndex]-localCuff).sqrMagnitude)gripIndex=i;
            for(int i=0;i<points.Length;i++)
            {
                var p=Vector3.Lerp(Live(i)-shaftAnchor,relaxed[i],amount);
                points[i]=authoredDelta!=null?p+(authoredDelta[i]-authoredDelta[gripIndex])*fold:p+FoldOffset(p)-gripOffset;
                var n=Vector3.Lerp(LiveNormal(i),restNormals[i],amount).normalized;
                float h=Mathf.Clamp01((p.y-ankle)/height);
                if(authoredDelta==null&&p.y>ankle&&p.y<localCuff.y)
                {
                    float dx=Mathf.Sign(localCuff.x)*.12f*fold*h/height;
                    float dy=-Mathf.Min(.16f,height*.4f)*fold*6*h*(1-h)/height;
                    float dz=.012f*fold*(Mathf.PI*6*Mathf.Cos(h*Mathf.PI*6)*h*(1-h)+Mathf.Sin(h*Mathf.PI*6)*(1-2*h))/height;
                    n.y=(n.y-dx*n.x-dz*n.z)/Mathf.Max(.1f,1+dy);
                }
                blendedNormals[i]=n.normalized;
            }
            mesh.vertices=points;
            if(authoredDelta!=null)mesh.RecalculateNormals();else mesh.normals=blendedNormals;
            mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
