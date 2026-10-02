using UnityEngine;

namespace Homecoming
{
    // Body geometry covered by clothing is masked, including during extraction.
    // This is per renderer: each stocking follows its own boot's actual pose.
    public sealed class HomecomingBootCoverage : MonoBehaviour
    {
        readonly SkinnedMeshRenderer[] stockings=new SkinnedMeshRenderer[2];
        readonly Bounds[] bounds=new Bounds[2];
        MaterialPropertyBlock block;
        bool initialized;
        public void Apply(CharacterRig rig,HomecomingShoeGrip grip,Transform proxies,float action)
        {
            if(block==null)block=new MaterialPropertyBlock();
            if(!initialized)
            {
                foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(skin.name=="左腿连续丝袜")stockings[0]=skin;
                    if(skin.name=="右腿连续丝袜")stockings[1]=skin;
                }
                for(int i=0;i<2;i++)bounds[i]=(i==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>().sharedMesh.bounds;
                initialized=true;
            }
            for(int i=0;i<2;i++)
            {
                if(!stockings[i])continue;
                Matrix4x4 matrix;
                if(action>=HomecomingShoeGrip.Detach(i)&&proxies)matrix=proxies.GetChild(i).worldToLocalMatrix;
                else matrix=grip.AttachedMatrix(i).inverse;
                var padding=new Vector3(.015f,.004f,.015f);
                stockings[i].GetPropertyBlock(block);block.SetMatrix("_BootWorldToLocal",matrix);
                block.SetVector("_BootBoundsMin",bounds[i].min-padding);block.SetVector("_BootBoundsMax",bounds[i].max+padding);
                block.SetFloat("_BootMask",1);stockings[i].SetPropertyBlock(block);
            }
        }
    }
}
