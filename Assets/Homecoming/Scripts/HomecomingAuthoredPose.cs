using System;
using System.Linq;
using UnityEngine;

namespace Homecoming
{
    // Local rotations exported from the editable Blender rig, after pose review.
    public sealed class HomecomingAuthoredPose
    {
        [Serializable] class Bone { public string name;public float[] rotation; }
        [Serializable] class Data { public string character,intent;public Bone[] bones; }
        readonly Transform[] bones;
        readonly Quaternion[] rotations;
        public HomecomingAuthoredPose(CharacterRig rig)
        {
            string character=rig.name.Replace("(Clone)","").Trim();
            var source=Resources.Load<TextAsset>("Homecoming/Poses/"+character);
            if(!source)return;
            var data=JsonUtility.FromJson<Data>(source.text);
            if(data.character!=character)throw new InvalidOperationException("Authored pose belongs to another character");
            var hierarchy=rig.GetComponentsInChildren<Transform>(true);
            bones=data.bones.Select(b=>hierarchy.Single(t=>t.name==b.name)).ToArray();
            rotations=data.bones.Select(b=>new Quaternion(b.rotation[0],b.rotation[1],b.rotation[2],b.rotation[3]).normalized).ToArray();
        }
        public bool Available=>bones!=null;
        public void Apply(float weight)
        {
            if(bones==null)return;
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,rotations[i],weight);
        }
    }
}
