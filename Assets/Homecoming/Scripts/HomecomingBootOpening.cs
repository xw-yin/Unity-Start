using System;
using UnityEngine;

namespace Homecoming
{
    // The original upper loosens before it is transferred; its sole and source
    // asset stay unchanged. The held contact clip starts from the same shape.
    public sealed class HomecomingBootOpening : MonoBehaviour
    {
        [Serializable] class Clip { public int vertexCount;public float[] baseVertices,opening;public int[] vertices; }
        SkinnedMeshRenderer skin;Mesh mesh;int shape=-1;
        public void Initialize()
        {
            if(skin)return;skin=GetComponent<SkinnedMeshRenderer>();
            var character=GetComponentInParent<CharacterRig>().name.Replace("(Clone)","").Trim();
            var asset=Resources.Load<TextAsset>("Homecoming/Poses/BootContact_"+character+"_"+skin.sharedMesh.name);
            var basis=skin.sharedMesh.vertices;
            bool softDlcBoot=skin.sharedMesh.name.StartsWith("OriginalYenneferBootDLC");
            if(!asset&&!softDlcBoot)return;
            var delta=new Vector3[basis.Length];
            if(asset)
            {
                var clip=JsonUtility.FromJson<Clip>(asset.text);
                if(clip.vertexCount!=basis.Length||clip.baseVertices.Length!=basis.Length*3||clip.opening.Length!=clip.vertices.Length*3)throw new InvalidOperationException("Boot opening topology changed; re-author contact");
                for(int i=0;i<basis.Length;i++)if((basis[i]-new Vector3(clip.baseVertices[i*3],clip.baseVertices[i*3+1],clip.baseVertices[i*3+2])).sqrMagnitude>1e-12f)throw new InvalidOperationException("Boot opening source geometry changed");
                for(int i=0;i<clip.vertices.Length;i++)delta[clip.vertices[i]]=new Vector3(clip.opening[i*3],clip.opening[i*3+1],clip.opening[i*3+2]);
            }
            else
            {
                // The retail DLC boot is soft leather. No authored contact clip
                // is shipped for it, so gently relax only the upper shaft while
                // the heel and toe remain completely untouched.
                var bounds=skin.sharedMesh.bounds;float top=bounds.max.y;
                float start=top-Mathf.Min(.22f,bounds.size.y*.34f);
                var centre=Vector3.zero;int count=0;
                for(int i=0;i<basis.Length;i++)if(basis[i].y>=top-.025f){centre.x+=basis[i].x;centre.z+=basis[i].z;count++;}
                if(count>0)
                {
                    centre/=count;
                    for(int i=0;i<basis.Length;i++)
                    {
                        float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,top,basis[i].y));
                        var radial=new Vector3(basis[i].x-centre.x,0,basis[i].z-centre.z);
                        delta[i]=radial*(.12f*weight);
                    }
                }
            }
            mesh=Instantiate(skin.sharedMesh);mesh.name=skin.sharedMesh.name;mesh.hideFlags=HideFlags.DontSave;
            mesh.AddBlendShapeFrame("LoosenForRemoval",100,delta,null,null);shape=mesh.GetBlendShapeIndex("LoosenForRemoval");skin.sharedMesh=mesh;
        }
        public void Set(float amount){Initialize();if(shape>=0)skin.SetBlendShapeWeight(shape,Mathf.Clamp01(amount)*100);}
        void OnDestroy(){if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
