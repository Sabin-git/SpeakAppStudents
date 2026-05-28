#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class iOSBuildPostProcess
{
    [PostProcessBuild(1)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;

        // Required for SpeechRecognizer microphone capture
        root.SetString(
            "NSMicrophoneUsageDescription",
            "Microphone access is required to analyse your speech during presentations."
        );

        // Required by Google Cardboard XR for head tracking via camera
        root.SetString(
            "NSCameraUsageDescription",
            "Camera access is required for VR head tracking."
        );

        plist.WriteToFile(plistPath);

        UnityEngine.Debug.Log("[iOSBuildPostProcess] Info.plist updated with microphone and camera permissions.");
    }
}
#endif
