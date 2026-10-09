using UnityEngine;

namespace Homecoming
{
    // Transfer the currently bent, skinned boot to the hand without a mesh jump.
    // Once the foot is clear, ease the empty boot back to its resting shape.
    public sealed class HomecomingBootSnapshot : MonoBehaviour
    {
        Mesh mesh;
        Vector3[] captured,normals,restNormals,points;
        float previous=-1,previousFold=-1;
        public Vector3[] Points=>points;
        public void UpdateShape(SkinnedMeshRenderer skin,Quaternion rotation,Vector3 cuff,Vector3 localCuff,Vector3[] relaxed,float amount,float fold=0)
        {
            if(!mesh)
            {
                var filter=GetComponent<MeshFilter>();restNormals=filter.sharedMesh.normals;
                mesh=new Mesh{name="Held original boot",hideFlags=HideFlags.DontSave};skin.BakeMesh(mesh);
                captured=mesh.vertices;normals=mesh.normals;points=new Vector3[captured.Length];
                var inverse=Quaternion.Inverse(rotation);var origin=cuff-rotation*localCuff;
                for(int i=0;i<captured.Length;i++)
                {
                    captured[i]=inverse*(skin.transform.TransformPoint(captured[i])-origin);
                    normals[i]=(inverse*skin.transform.TransformDirection(normals[i])).normalized;
                }
                filter.sharedMesh=mesh;
            }
            if(Mathf.Abs(amount-previous)<.000001f&&Mathf.Abs(fold-previousFold)<.000001f)return;
            previous=amount;previousFold=fold;var blendedNormals=new Vector3[points.Length];
            float ankle=.14f,height=Mathf.Max(.1f,localCuff.y-ankle);
            Vector3 FoldOffset(Vector3 p)
            {
                float h=Mathf.Clamp01((p.y-ankle)/height),envelope=h*(1-h);
                return new Vector3(Mathf.Sign(localCuff.x)*.06f*fold*h*h,-Mathf.Min(.16f,height*.4f)*fold*Mathf.SmoothStep(0,1,h),.012f*fold*Mathf.Sin(h*Mathf.PI*6)*envelope);
            }
            var gripOffset=FoldOffset(localCuff);
            for(int i=0;i<points.Length;i++)
            {
                var p=Vector3.Lerp(captured[i],relaxed[i],amount);
                points[i]=p+FoldOffset(p)-gripOffset;
                var n=Vector3.Lerp(normals[i],restNormals[i],amount).normalized;
                float h=Mathf.Clamp01((p.y-ankle)/height);
                if(p.y>ankle&&p.y<localCuff.y)
                {
                    float dx=Mathf.Sign(localCuff.x)*.12f*fold*h/height;
                    float dy=-Mathf.Min(.16f,height*.4f)*fold*6*h*(1-h)/height;
                    float dz=.012f*fold*(Mathf.PI*6*Mathf.Cos(h*Mathf.PI*6)*h*(1-h)+Mathf.Sin(h*Mathf.PI*6)*(1-2*h))/height;
                    n.y=(n.y-dx*n.x-dz*n.z)/Mathf.Max(.1f,1+dy);
                }
                blendedNormals[i]=n.normalized;
            }
            mesh.vertices=points;mesh.normals=blendedNormals;mesh.RecalculateBounds();mesh.RecalculateTangents();
        }
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
