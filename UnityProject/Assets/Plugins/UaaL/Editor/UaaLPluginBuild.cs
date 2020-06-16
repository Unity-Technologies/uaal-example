using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

using UnityEditor.Build;

namespace UaaL {

public class UaaLPluginBuild : IPreprocessBuildWithReport
{	

    public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport report)
    {    	
    	UaaLPluginGenerator.GenerateCode();
        Debug.Log("UaaLPlugin Done generating UaaLPlugin files.");
    }

    public int callbackOrder { get { return 0; } }

    [PostProcessBuild]
    private static void PostprocessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
            return;

        var pbxPath = Path.Combine(buildPath, "Unity-iPhone.xcodeproj", "project.pbxproj");
        var pbxProject = new PBXProject();
        pbxProject.ReadFromFile(pbxPath);

        var frameworkGUID = pbxProject.GetUnityFrameworkTargetGuid();
        var dataGUID = pbxProject.FindFileGuidByRealPath("Data");
        pbxProject.AddFileToBuild(frameworkGUID, dataGUID);

        var headerGUID = pbxProject.FindFileGuidByProjectPath("Libraries/" + UaaLPluginGenerator.iOSObjcPluginsHeaderFilePath);
        pbxProject.AddPublicHeaderToBuild(frameworkGUID, headerGUID);

        pbxProject.WriteToFile(pbxPath);
    }
}

}