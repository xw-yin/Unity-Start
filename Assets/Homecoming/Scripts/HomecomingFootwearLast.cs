using System;
using System.Linq;
using UnityEngine;

namespace Homecoming
{
    // Preserve the original barefoot mesh; only its worn high-heel pose is fitted.
    public sealed class HomecomingFootwearLast : MonoBehaviour
    {
        [Serializable] class Shape { public string character;public int vertexCount;public float[] baseVertices,delta,normalDelta,weights;public int[] boneIndices;public string[] boneNames; }
        SkinnedMeshRenderer skin;
        Mesh mesh,sampledMesh;
        readonly System.Collections.Generic.List<Vector3> samplePoints=new System.Collections.Generic.List<Vector3>();
        int shape=-1;
        BoneWeight[] originalWeights,fittedWeights,mixedWeights;
        readonly int[] indices=new int[8];readonly float[] values=new float[8];
        float previous=-1;
        public float RequiredPointAngle {get;internal set;}
        public void Initialize(string prefix="FootLast_")
        {
            if(skin)return;skin=GetComponent<SkinnedMeshRenderer>();
            var source=Resources.Load<TextAsset>("Homecoming/Poses/"+prefix+skin.sharedMesh.name);
            if(!source)return;
            var data=JsonUtility.FromJson<Shape>(source.text);
            if(!string.IsNullOrEmpty(data.character)&&data.character!=GetComponentInParent<CharacterRig>().name.Replace("(Clone)","").Trim())throw new InvalidOperationException("Footwear fit belongs to a different costume");
            if(data.vertexCount!=skin.sharedMesh.vertexCount||data.delta==null||data.normalDelta==null||data.delta.Length!=data.vertexCount*3||data.normalDelta.Length!=data.vertexCount*3)throw new InvalidOperationException("Foot last no longer matches original mesh");
            if(data.baseVertices!=null&&data.baseVertices.Length>0)
            {
                var basis=skin.sharedMesh.vertices;
                if(data.baseVertices.Length!=basis.Length*3)throw new InvalidOperationException("Footwear source topology changed");
                for(int i=0;i<basis.Length;i++)if((basis[i]-new Vector3(data.baseVertices[i*3],data.baseVertices[i*3+1],data.baseVertices[i*3+2])).sqrMagnitude>1e-12f)throw new InvalidOperationException("Footwear source geometry changed; re-export the fit");
            }
            mesh=Instantiate(skin.sharedMesh);mesh.name=skin.sharedMesh.name;mesh.hideFlags=HideFlags.DontSave;
            var positions=new Vector3[data.vertexCount];var normals=new Vector3[data.vertexCount];
            for(int i=0;i<positions.Length;i++)
            {
                positions[i]=new Vector3(data.delta[i*3],data.delta[i*3+1],data.delta[i*3+2]);
                normals[i]=new Vector3(data.normalDelta[i*3],data.normalDelta[i*3+1],data.normalDelta[i*3+2]);
            }
            mesh.AddBlendShapeFrame("WornHighHeel",100,positions,normals,null);shape=mesh.GetBlendShapeIndex("WornHighHeel");skin.sharedMesh=mesh;
            originalWeights=mesh.boneWeights;fittedWeights=new BoneWeight[originalWeights.Length];mixedWeights=new BoneWeight[originalWeights.Length];
            if(data.boneNames==null||data.boneIndices==null||data.weights==null||data.boneIndices.Length!=data.vertexCount*4||data.weights.Length!=data.vertexCount*4)throw new InvalidOperationException("Re-export fitted calf weights from Blender");
            var names=skin.bones.Select((b,i)=>(b.name,i)).ToDictionary(p=>p.name,p=>p.i);
            int Map(int offset){if(data.weights[offset]<=0)return 0;if(names.TryGetValue(data.boneNames[data.boneIndices[offset]],out int index))return index;throw new InvalidOperationException("Fitted foot bone missing; re-export after bone trimming");}
            for(int i=0;i<fittedWeights.Length;i++)
            {
                int p=i*4;fittedWeights[i]=new BoneWeight{boneIndex0=Map(p),boneIndex1=Map(p+1),boneIndex2=Map(p+2),boneIndex3=Map(p+3),weight0=data.weights[p],weight1=data.weights[p+1],weight2=data.weights[p+2],weight3=data.weights[p+3]};
            }
        }
        public void Set(float weight)
        {
            Initialize();if(shape<0)return;weight=Mathf.Clamp01(weight);
            if(Mathf.Approximately(weight,previous))return;previous=weight;
            skin.SetBlendShapeWeight(shape,weight*100);
            if(weight<=0){mesh.boneWeights=originalWeights;return;}
            if(weight>=1){mesh.boneWeights=fittedWeights;return;}
            for(int vertex=0;vertex<mixedWeights.Length;vertex++)
            {
                int count=0;
                var a=originalWeights[vertex];var b=fittedWeights[vertex];float inverse=1-weight;
                AddWeight(a.boneIndex0,a.weight0*inverse,ref count);AddWeight(a.boneIndex1,a.weight1*inverse,ref count);AddWeight(a.boneIndex2,a.weight2*inverse,ref count);AddWeight(a.boneIndex3,a.weight3*inverse,ref count);
                AddWeight(b.boneIndex0,b.weight0*weight,ref count);AddWeight(b.boneIndex1,b.weight1*weight,ref count);AddWeight(b.boneIndex2,b.weight2*weight,ref count);AddWeight(b.boneIndex3,b.weight3*weight,ref count);
                for(int j=0;j<Mathf.Min(4,count);j++)for(int k=j+1;k<count;k++)if(values[k]>values[j]){(values[j],values[k])=(values[k],values[j]);(indices[j],indices[k])=(indices[k],indices[j]);}
                float total=0;for(int j=0;j<Mathf.Min(4,count);j++)total+=values[j];
                mixedWeights[vertex]=new BoneWeight{boneIndex0=indices[0],boneIndex1=count>1?indices[1]:0,boneIndex2=count>2?indices[2]:0,boneIndex3=count>3?indices[3]:0,weight0=values[0]/total,weight1=count>1?values[1]/total:0,weight2=count>2?values[2]/total:0,weight3=count>3?values[3]/total:0};
            }
            mesh.boneWeights=mixedWeights;
        }
        void AddWeight(int bone,float value,ref int count)
        {
            if(value<=0)return;for(int j=0;j<count;j++)if(indices[j]==bone){values[j]+=value;return;}
            indices[count]=bone;values[count++]=value;
        }
        public Vector3 SampleFootAxis(int[] heel,int[] ball)
        {
            if(!sampledMesh)sampledMesh=new Mesh{hideFlags=HideFlags.DontSave};
            skin.BakeMesh(sampledMesh);sampledMesh.GetVertices(samplePoints);
            Vector3 Average(int[] ids){var sum=Vector3.zero;foreach(int id in ids)sum+=samplePoints[id];return sum/ids.Length;}
            return skin.transform.TransformVector(Average(ball)-Average(heel));
        }
        void OnDestroy()
        {
            foreach(var owned in new[]{mesh,sampledMesh})if(owned){if(Application.isPlaying)Destroy(owned);else DestroyImmediate(owned);}
        }
    }
}
