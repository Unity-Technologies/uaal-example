using UnityEditor.Android;

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
