using UnityEngine;

namespace Homecoming
{
    // Body geometry covered by clothing is masked, including during extraction.
    // This is per renderer: each stocking follows its own boot's actual pose.
    public sealed class HomecomingBootCoverage : MonoBehaviour
    {
        readonly SkinnedMeshRenderer[] stockings=new SkinnedMeshRenderer[2];
        readonly Bounds[] bounds=new Bounds[2];
        readonly SkinnedMeshRenderer[] laces=new SkinnedMeshRenderer[2];
        readonly bool[] originalBoots=new bool[2];
        MaterialPropertyBlock block;
        bool initialized;
        public void Apply(CharacterRig rig,HomecomingShoeGrip grip,Transform proxies,float action)
        {
            if(block==null)block=new MaterialPropertyBlock();
            if(!initialized)
            {
                for(int i=0;i<2;i++)
                    stockings[i]=rig.hosiery!=null&&rig.hosiery.Length>i?rig.hosiery[i]:null;
                for(int i=0;i<2;i++)
                {
                    bounds[i]=(i==0?rig.leftGroundShoe:rig.rightGroundShoe).GetComponent<MeshFilter>().sharedMesh.bounds;
                    originalBoots[i]=(i==0?rig.leftShoe:rig.rightShoe).GetComponent<SkinnedMeshRenderer>().sharedMesh.name.StartsWith("OriginalYenneferBoot");
                    if(originalBoots[i])foreach(var surface in rig.GetComponentsInChildren<SkinnedMeshRenderer>())if(surface.name=="YenneferOriginalLace"+(i==0?"Left":"Right"))laces[i]=surface;
                }
                initialized=true;
            }
            for(int i=0;i<2;i++)
            {
                if(!stockings[i])continue;
                if(rig.dressedLegs!=null&&rig.dressedLegs.Length==2&&rig.dressedLegs[i])
                {
                    bool detached=action>=HomecomingShoeGrip.Detach(i);
                    stockings[i].enabled=detached;if(laces[i])laces[i].enabled=detached;
                    rig.dressedLegs[i].enabled=!detached;rig.dressedHosiery[i].enabled=!detached;
                }
                Matrix4x4 matrix;
                if(action>=HomecomingShoeGrip.Detach(i)&&proxies)matrix=proxies.GetChild(i).worldToLocalMatrix;
                else matrix=grip.AttachedMatrix(i).inverse;
                // The original barefoot mesh includes toes outside the narrower
                // dressed shoe shell. Hide the enclosed sock, without changing
                // the source leg topology; keep the cuff height exact.
                var currentBounds=proxies&&action>=HomecomingShoeGrip.Detach(i)?proxies.GetChild(i).GetComponent<MeshFilter>().sharedMesh.bounds:bounds[i];
                var padding=new Vector3(.04f,originalBoots[i]?.035f:.004f,.09f);
                stockings[i].GetPropertyBlock(block);block.SetMatrix("_BootWorldToLocal",matrix);
                var minimum=currentBounds.min-padding;
                // Triss' alternate costume has a shaft and open foot straps.
                // Retain the original foot through those openings, masking only
                // the enclosed shaft instead of erasing the whole foot in a box.
                if(rig.openToeFootwear)minimum.y=currentBounds.min.y+.145f;
                block.SetVector("_BootBoundsMin",minimum);block.SetVector("_BootBoundsMax",currentBounds.max+padding);
                block.SetFloat("_OpenFootSoleMask",rig.openToeFootwear?1:0);
                block.SetFloat("_BootSoleMax",currentBounds.min.y+.145f);
                // Detaching transfers the boot to the hand while the foot is
                // still inside. Keep coverage during the actual extraction.
                block.SetFloat("_BootMask",action>=grip.Cleared(i)?0:1);
                block.SetFloat("_BootRestMask",originalBoots[i]&&action<HomecomingShoeGrip.Detach(i)?1:0);stockings[i].SetPropertyBlock(block);
                if(laces[i])laces[i].SetPropertyBlock(block);
                if(rig.dressedLegs!=null&&rig.dressedLegs.Length==2&&rig.dressedLegs[i])
                {
                    rig.dressedLegs[i].SetPropertyBlock(block);rig.dressedHosiery[i].SetPropertyBlock(block);
                }
            }
        }
    }
}
