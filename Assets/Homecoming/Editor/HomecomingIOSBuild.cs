using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace Homecoming.Editor
{
    public static class HomecomingIOSBuild
    {
        static string Argument(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return fallback;
        }

        public static void Build()
        {
            const string scene = "Assets/Homecoming/Scenes/Homecoming.unity";
            const string output = "build/iOS";
            if (!File.Exists(scene) || !Resources.Load<GameObject>("Homecoming/YenneferDLC"))
                throw new BuildFailedException("Homecoming scene or saved YenneferDLC prefab is missing.");
            PlayerSettings.companyName = "Xinwang";
            PlayerSettings.productName = "Homecoming";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, Argument("-iosBundleID", "com.xwyin.homecoming"));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.iOS.buildNumber = Argument("-iosBuildNumber", "1");
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleDeveloperTeamID = Argument("-iosTeamID");
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            bool unsigned = Argument("-iosUnsigned", "false") == "true";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = output,
                target = BuildTarget.iOS, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("iOS export failed: " + report.summary.result);
#if UNITY_IOS
            var path = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject(); project.ReadFromFile(path);
            string app = project.GetUnityMainTargetGuid();
            string framework = project.GetUnityFrameworkTargetGuid();
            if (unsigned)
            {
                foreach (string target in new[] { app, framework })
                {
                    project.SetBuildProperty(target, "CODE_SIGNING_ALLOWED", "NO");
                    project.SetBuildProperty(target, "CODE_SIGNING_REQUIRED", "NO");
                    project.SetBuildProperty(target, "CODE_SIGN_IDENTITY", "");
                    project.SetBuildProperty(target, "DEVELOPMENT_TEAM", "");
                    project.SetBuildProperty(target, "PROVISIONING_PROFILE_SPECIFIER", "");
                }
                project.WriteToFile(path);
                Debug.Log("HOMECOMING UNSIGNED IOS XCODE EXPORT SUCCEEDED: " + output);
                return;
            }
            project.SetBuildProperty(app, "DEVELOPMENT_TEAM", Argument("-iosTeamID"));
            project.SetBuildProperty(app, "CODE_SIGN_STYLE", "Manual");
            project.SetBuildProperty(app, "PROVISIONING_PROFILE_SPECIFIER", Argument("-iosProfileUUID"));
            project.SetBuildProperty(app, "CODE_SIGN_IDENTITY", Argument("-iosIdentity", "Apple Distribution"));
            // The application profile must not be assigned to UnityFramework.
            project.SetBuildProperty(framework, "CODE_SIGN_STYLE", "Manual");
            project.SetBuildProperty(framework, "DEVELOPMENT_TEAM", Argument("-iosTeamID"));
            project.SetBuildProperty(framework, "CODE_SIGN_IDENTITY", Argument("-iosIdentity", "Apple Distribution"));
            project.SetBuildProperty(framework, "PROVISIONING_PROFILE_SPECIFIER", "");
            project.WriteToFile(path);
#endif
            Debug.Log("HOMECOMING IOS XCODE EXPORT SUCCEEDED: " + output);
        }
    }
}
