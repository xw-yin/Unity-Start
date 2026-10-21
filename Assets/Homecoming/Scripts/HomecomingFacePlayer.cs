using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Homecoming
{
    // Authored game float curves drive the original additive mimic poses. Only
    // facial transforms are owned here; the body/head pose remains with the story.
    public sealed class HomecomingFacePlayer
    {
        readonly HomecomingFaceLibrary data;
        readonly Transform[] bones;
        readonly Quaternion[] restTurns,turns;
        readonly Vector3[] offsets;
        readonly float[] weights,neutralWeights,happyWeights;
        readonly Dictionary<string,int> poseIndices,boneIndices;
        readonly int[][] bindings;
        readonly HomecomingFaceLibrary.Clip neutral,happy;
        public float MaximumOffset {get;private set;}
        public float MaximumTurn {get;private set;}
        public HomecomingFacePlayer(CharacterRig rig)
        {
            data=rig.faceLibrary;
            var transforms=rig.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            bones=data.bones.Select(n=>transforms.TryGetValue(n,out var bone)?bone:null).ToArray();
            if(bones.Any(b=>!b))throw new InvalidOperationException("The original face rig has missing bones");
            restTurns=bones.Select(b=>rig.restPoses.FirstOrDefault(p=>p.bone==b).rotation).ToArray();
            for(int i=0;i<restTurns.Length;i++)if(Quaternion.Dot(restTurns[i],restTurns[i])<.5f)restTurns[i]=bones[i].localRotation;
            offsets=new Vector3[bones.Length];turns=new Quaternion[bones.Length];
            weights=new float[data.poses.Length];neutralWeights=new float[weights.Length];happyWeights=new float[weights.Length];
            poseIndices=data.poses.Select((p,i)=>new{p.name,i}).ToDictionary(p=>p.name,p=>p.i);
            boneIndices=data.bones.Select((n,i)=>new{n,i}).ToDictionary(p=>p.n,p=>p.i);
            bindings=data.poses.Select(p=>p.transforms.Select(t=>boneIndices[t.bone]).ToArray()).ToArray();
            neutral=data.clips.First(c=>c.name=="neutral_anim_combined_face");happy=data.clips.First(c=>c.name=="happy_anim_combined_face");
        }
        public void Reset()
        {
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=data.restPositions[i];bones[i].localRotation=restTurns[i];}
        }
        public void Sample(float time,float contentment)
        {
            ReadClip(neutral,time,neutralWeights);ReadClip(happy,time,happyWeights);
            // The authored blink remains at full amplitude while mood changes.
            // Interpolating unrelated blink phases would create half-closed lids.
            for(int i=0;i<weights.Length;i++)weights[i]=data.poses[i].name.StartsWith("eyelids_")?neutralWeights[i]:Mathf.Lerp(neutralWeights[i],happyWeights[i],Mathf.Clamp01(contentment));
            ApplyWeights();
        }
        public void PreviewPose(string name,float amount)
        {
            PreviewPoses((name,amount));
        }
        public void PreviewPoses(params (string name,float amount)[] poses)
        {
            Array.Clear(weights,0,weights.Length);
            foreach(var pose in poses){if(!poseIndices.TryGetValue(pose.name,out var index))throw new ArgumentException("Unknown original face pose "+pose.name);weights[index]=pose.amount;}
            ApplyWeights();
        }
        public float Weight(string name)=>poseIndices.TryGetValue(name,out var i)?weights[i]:0;
        void ReadClip(HomecomingFaceLibrary.Clip clip,float time,float[] target)
        {
            Array.Clear(target,0,target.Length);
            // Play the original interval in order, crossfading its last half
            // second to the beginning so loop seams are deterministic on replay.
            float blend=Mathf.Min(.5f,clip.duration*.05f),period=clip.duration-blend;
            float t=Mathf.Repeat(Mathf.Max(0,time),period),fade=Mathf.SmoothStep(0,1,Mathf.InverseLerp(period-blend,period,t));
            foreach(var track in clip.tracks)
            {
                if(!poseIndices.TryGetValue(track.name,out var index))continue;
                float value=At(track.values,t+blend,clip.duration);
                if(fade>0)value=Mathf.Lerp(value,At(track.values,t-period+blend,clip.duration),fade);
                target[index]=Mathf.Clamp01(value);
            }
        }
        static float At(float[] values,float time,float duration)
        {
            if(values.Length==1)return values[0];
            float frame=Mathf.Clamp01(time/duration)*(values.Length-1);int low=Mathf.FloorToInt(frame);
            return Mathf.Lerp(values[low],values[Mathf.Min(low+1,values.Length-1)],frame-low);
        }
        void ApplyWeights()
        {
            Array.Clear(offsets,0,offsets.Length);for(int i=0;i<turns.Length;i++)turns[i]=Quaternion.identity;
            for(int p=0;p<weights.Length;p++)
            {
                float amount=weights[p];if(amount<=.00001f)continue;
                var deltas=data.poses[p].transforms;
                for(int t=0;t<deltas.Length;t++)
                {
                    int bone=bindings[p][t];offsets[bone]+=deltas[t].position*amount;
                    turns[bone]*=Quaternion.SlerpUnclamped(Quaternion.identity,deltas[t].rotation,amount);
                }
            }
            MaximumOffset=0;MaximumTurn=0;
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=data.restPositions[i]+offsets[i];bones[i].localRotation=restTurns[i]*turns[i];
                MaximumOffset=Mathf.Max(MaximumOffset,offsets[i].magnitude);MaximumTurn=Mathf.Max(MaximumTurn,Quaternion.Angle(Quaternion.identity,turns[i]));
            }
        }
    }
}
