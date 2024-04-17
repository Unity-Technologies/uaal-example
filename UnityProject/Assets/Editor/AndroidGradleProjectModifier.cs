// Remove this, once Unity reexposes Configuration Manager
#define DISABLE_PROJECT_FILES_MODIFIER
#if UNITY_ANDROID
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Unity.Android.Gradle.Manifest;
using UnityEditor.Android;
using UnityEngine;

#if DISABLE_PROJECT_FILES_MODIFIER
class MyCustomBuildProcessor : IPostGenerateGradleAndroidProject
{
    public int callbackOrder { get { return 0; } }

    private void Log(string message)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
    }

    private void PatchManifest(string path)
    {
        Log($"Patching '{path}'");
        var doc = XDocument.Load(path);
        var toRemove = new List<XElement>();
        XNamespace android = "http://schemas.android.com/apk/res/android";

        foreach (var activity in doc.Descendants("activity"))
        {
            var isUnityActivity = false;
            foreach (var metaData in activity.Elements("meta-data"))
            {
                var name = metaData.Attribute(android + "name");
                var value = metaData.Attribute(android + "value");
                if (name == null || value == null)
                    continue;
                if (name.Value == "unityplayer.UnityActivity" && value.Value == "true")
                {
                    isUnityActivity = true;
                    break;
                }
            }
            if (isUnityActivity)
                toRemove.Add(activity);
        }

        var modified = false;
        foreach (var activity in toRemove)
        {
            modified = true;
            activity.Remove();
        }

        if (modified)
            doc.Save(path);
    }

    private void PatchSettingsGradle(string path)
    {
        Log($"Patching '{path}'");
        var lines = File.ReadAllLines(path);
        var modified = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("include ':launcher'"))
            {
                lines[i] = "//" + lines[i];
                modified = true;
                break;
            }
        }
        if (modified)
            File.WriteAllLines(path, lines);

    }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        var manifestPath = Path.GetFullPath(Path.Combine(path, "src/main/AndroidManifest.xml"));
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"Failed to find '{manifestPath}'");
        PatchManifest(manifestPath);


        var settingsGradle = Path.GetFullPath(Path.Combine(path, "../settings.gradle"));
        if (!File.Exists(settingsGradle))
            throw new FileNotFoundException($"Failed to find '{settingsGradle}'");
        PatchSettingsGradle(settingsGradle);
    }
}
#else
public class AndroidGradleProjectModifier : AndroidProjectFilesModifier
{
    public override void OnModifyAndroidProjectFiles(AndroidProjectFiles projectFiles)
    {
        // Remove entries to launcher
        projectFiles.LauncherBuildGradle.SetRaw(string.Empty);
        foreach (var include in projectFiles.GradleSettings.IncludeList.GetElements())
        {
            if (include.GetRaw().Contains("launcher"))
            {
                include.Remove();
                break;
            }
        }

        // Remove launchable activities in unityLibrary
        var manifest = projectFiles.UnityLibraryManifest.Manifest;
        foreach (var item in manifest.GetActivitiesWithLauncherIntent())
        {
            foreach (var intent in item.IntentFilterList)
                intent.Remove();
        }
    }
}
#endif
#endif