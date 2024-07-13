using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public class iOSPBXProjectModifier
{
    [PostProcessBuildAttribute]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        // Modify Unity generated Xcode project to enable Unity as a library
        if (target == BuildTarget.iOS)
        {
            // Read project
            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);

            // Get main and framework target guids
            string unityMainTargetGuid = project.GetUnityMainTargetGuid();
            string unityFrameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            // Set NativeCallProxy plugin header visibility to public
            string pluginHeaderGuid = project.FindFileGuidByProjectPath("Libraries/Plugins/iOS/NativeCallProxy.h");
            project.AddPublicHeaderToBuild(unityFrameworkTargetGuid, pluginHeaderGuid);

            // Change data directory target membership to framework only
            string dataDirectoryGuid = project.FindFileGuidByProjectPath("Data");
            project.RemoveFileFromBuild(unityMainTargetGuid, dataDirectoryGuid);
            project.AddFileToBuild(unityFrameworkTargetGuid, dataDirectoryGuid);

            // Overwrite project
            project.WriteToFile(projectPath);
        }
    }
}