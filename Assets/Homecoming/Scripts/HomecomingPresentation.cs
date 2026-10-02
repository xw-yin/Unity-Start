using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Homecoming
{
    public static class HomecomingPresentation
    {
        public static void Configure(Camera camera,Transform root,Transform character)
        {
            Application.targetFrameRate=60;
            QualitySettings.skinWeights=SkinWeights.FourBones;
            QualitySettings.realtimeReflectionProbes=true;
            QualitySettings.anisotropicFiltering=AnisotropicFiltering.ForceEnable;
            camera.allowHDR=true; camera.allowMSAA=true; camera.allowDynamicResolution=false;
            camera.farClipPlane=35;
            var data=camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.None; // The pipeline uses 4x MSAA.
            data.renderShadows=true;
            var volumeObject=new GameObject("室内色彩与高光"); volumeObject.transform.SetParent(root,false);
            var volume=volumeObject.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=10;
            var profile=Resources.Load<VolumeProfile>("Homecoming/IndoorLighting");
            volume.sharedProfile=profile?profile:CreateProfile();
            // Capture the furnished room once; exclude the moving character from the reflection.
            foreach(var part in character.GetComponentsInChildren<Transform>(true)) part.gameObject.layer=8;
            var probeObject=new GameObject("室内环境反射"); probeObject.transform.SetParent(root,false);
            probeObject.transform.localPosition=new Vector3(0,1.4f,1);
            var probe=probeObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Realtime; probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution=128; probe.size=new Vector3(8,3.2f,7);
            probe.boxProjection=true; probe.intensity=.65f; probe.cullingMask=~(1<<8);
            probe.clearFlags=ReflectionProbeClearFlags.SolidColor; probe.backgroundColor=new Color(.23f,.25f,.28f);
            if(Application.isPlaying) probe.RenderProbe();
        }
        public static VolumeProfile CreateProfile()
        {
            var profile=ScriptableObject.CreateInstance<VolumeProfile>(); profile.name="IndoorLighting";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var color=profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(.1f); color.contrast.Override(8); color.saturation.Override(-4);
            var bloom=profile.Add<Bloom>(true);
            bloom.threshold.Override(1.2f); bloom.intensity.Override(.12f); bloom.scatter.Override(.45f);
            return profile;
        }
    }
}
