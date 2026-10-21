using System;
using UnityEngine;

namespace Homecoming
{
    public sealed class HomecomingFaceLibrary : ScriptableObject
    {
        [Serializable] public class Delta {public string bone;public Vector3 position;public Quaternion rotation;}
        [Serializable] public class Pose {public string name;public Delta[] transforms;}
        [Serializable] public class Track {public string name;public float[] values;}
        [Serializable] public class Clip {public string name;public float duration,fps;public int frames;public Track[] tracks;}
        public string poseSource,poseSHA256,clipSource,clipSHA256;
        public string[] bones,ignoredSourceBones,ignoredTracks;
        public Vector3[] restPositions;
        public Pose[] poses;
        public Clip[] clips;
    }
}
