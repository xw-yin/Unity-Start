using UnityEngine;

namespace Homecoming
{
    // Transfer the currently bent, skinned boot to the hand without a mesh jump.
    // Once the foot is clear, ease the empty boot back to its resting shape.
    public sealed class HomecomingBootSnapshot : MonoBehaviour
    {
        Mesh mesh,template,sampledBody;
        Vector3[] captured,normals,restNormals,points;
        float previous=-1,previousFold=-1;
        [System.Serializable] class Shape { public int vertexCount;public float[] delta; }
        [System.Serializable] class ContactKey { public float phase;public float[] delta; }
        [System.Serializable] class ContactClip { public int vertexCount;public float[] baseVertices;public int[] vertices;public ContactKey[] keys; }
        ContactClip contactClip;float previousContact=-1;
        Vector3[] authoredDelta;
        public Vector3[] Points=>points;
        public void ResetCapture()
        {
            if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);mesh=null;}
            if(template)GetComponent<MeshFilter>().sharedMesh=template;
            previous=previousFold=previousContact=-1;authoredDelta=null;contactClip=null;
        }
        public void UpdateShape(SkinnedMeshRenderer skin,Quaternion rotation,Vector3 cuff,Vector3 localCuff,Vector3[] relaxed,float amount,float fold=0,float contactPhase=0)
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
                var sourcePoints=skin.sharedMesh.vertices;
                var character=skin.GetComponentInParent<CharacterRig>().name.Replace("(Clone)","").Trim();
                var contact=Resources.Load<TextAsset>("Homecoming/Poses/BootContact_"+character+"_"+skin.sharedMesh.name);
                if(contact)
                {
                    contactClip=JsonUtility.FromJson<ContactClip>(contact.text);
                    if(contactClip.vertexCount!=sourcePoints.Length||contactClip.baseVertices.Length!=sourcePoints.Length*3)throw new System.InvalidOperationException("Boot contact source topology changed");
                    for(int i=0;i<sourcePoints.Length;i++)if((sourcePoints[i]-new Vector3(contactClip.baseVertices[i*3],contactClip.baseVertices[i*3+1],contactClip.baseVertices[i*3+2])).sqrMagnitude>1e-12f)throw new System.InvalidOperationException("Boot contact source geometry changed");
                }

            }
            bool sameShape=Mathf.Abs(amount-previous)<.000001f&&Mathf.Abs(fold-previousFold)<.000001f&&Mathf.Abs(contactPhase-previousContact)<.000001f;
            if(sameShape)return;
            previous=amount;previousFold=fold;previousContact=contactPhase;var blendedNormals=new Vector3[points.Length];
            float ankle=.14f,height=Mathf.Max(.1f,localCuff.y-ankle);
            Vector3 FoldOffset(Vector3 p)
            {
                float h=Mathf.Clamp01((p.y-ankle)/height),envelope=h*(1-h);
                return new Vector3(Mathf.Sign(localCuff.x)*.06f*fold*h*h,-Mathf.Min(.16f,height*.4f)*fold*Mathf.SmoothStep(0,1,h),.012f*fold*Mathf.Sin(h*Mathf.PI*6)*envelope);
            }
            var gripOffset=FoldOffset(localCuff);
            // The detached prop keeps the captured boot in its own space.
            // Driving its shaft with a calf that has already left the boot
            // stretches the leather into a U and can appear to remove the leg.
            int gripIndex=0;
            if(authoredDelta!=null)
                for(int i=1;i<relaxed.Length;i++)if((relaxed[i]-localCuff).sqrMagnitude<(relaxed[gripIndex]-localCuff).sqrMagnitude)gripIndex=i;
            for(int i=0;i<points.Length;i++)
            {
                var p=Vector3.Lerp(captured[i],relaxed[i],amount);
                points[i]=authoredDelta!=null?p+(authoredDelta[i]-authoredDelta[gripIndex])*fold:p+FoldOffset(p)-gripOffset;
                var n=Vector3.Lerp(normals[i],restNormals[i],amount).normalized;
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
            if(contactClip!=null)
            {
                int next=1;while(next<contactClip.keys.Length-1&&contactClip.keys[next].phase<contactPhase)next++;
                var a=contactClip.keys[next-1];var b=contactClip.keys[next];
                float blend=Mathf.InverseLerp(a.phase,b.phase,contactPhase);
                for(int i=0;i<contactClip.vertices.Length;i++)
                {
                    int offset=i*3;
                    var first=new Vector3(a.delta[offset],a.delta[offset+1],a.delta[offset+2]);
                    var second=new Vector3(b.delta[offset],b.delta[offset+1],b.delta[offset+2]);
                    points[contactClip.vertices[i]]+=Vector3.Lerp(first,second,blend);
                }
            }
            mesh.vertices=points;
            if(authoredDelta!=null||contactClip!=null)mesh.RecalculateNormals();else mesh.normals=blendedNormals;
            mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
        public float FootClearance(SkinnedMeshRenderer body,int[] indices,int[] rim,Vector3 localCuff)
        {
            if(!sampledBody)sampledBody=new Mesh{hideFlags=HideFlags.DontSave};
            body.BakeMesh(sampledBody);var vertices=sampledBody.vertices;
            var cuff=transform.TransformPoint(localCuff);float minimum=float.MaxValue;
            foreach(int i in indices)minimum=Mathf.Min(minimum,Vector3.Dot(body.transform.TransformPoint(vertices[i])-cuff,transform.up));
            float lip=float.MinValue;foreach(int i in rim)lip=Mathf.Max(lip,points[i].y);
            return minimum-(lip-localCuff.y);
        }
        void OnDestroy(){foreach(var owned in new[]{mesh,sampledBody})if(owned){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}}
    }
}
