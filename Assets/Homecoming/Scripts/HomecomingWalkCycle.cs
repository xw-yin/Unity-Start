using UnityEngine;

namespace Homecoming
{
    public class HomecomingWalkCycle : ScriptableObject
    {
        public string source;
        public float duration,strideDistance;
        public Vector3 forward;
        public string[] bones;
        public Quaternion[] rotations;
        public Vector3[] hipPositions;
        public int frames;
        public Quaternion Rotation(int bone,float phase)
        {
            float frame=Mathf.Repeat(phase,1)*frames;
            int a=Mathf.FloorToInt(frame),b=(a+1)%frames;
            return Quaternion.Slerp(rotations[a*bones.Length+bone],rotations[b*bones.Length+bone],frame-a);
        }
        public Vector3 Hip(float phase)
        {
            float frame=Mathf.Repeat(phase,1)*frames;
            int a=Mathf.FloorToInt(frame),b=(a+1)%frames;
            return Vector3.Lerp(hipPositions[a],hipPositions[b],frame-a);
        }
    }
}
