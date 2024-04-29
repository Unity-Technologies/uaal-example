using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.TestTools;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UAAL.EditorTests
{
    [RequirePlatformSupport(BuildTarget.Android)]
    public abstract class EditorTestBase
    {
        protected const string AssetsName = "Assets";

        protected string UnityProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        protected string Splitter => new string('=', 300);


        [OneTimeSetUp]
        public void OneTimeSetupBase()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.Unity.UAALTests");
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.androidBuildType = EditorUserBuildSettings.development ? AndroidBuildType.Development : AndroidBuildType.Release;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            PlayerSettings.stripEngineCode = false;
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.fullscreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetIl2CppStacktraceInformation(NamedBuildTarget.Android, Il2CppStacktraceInformation.MethodOnly);
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.resizableWindow = false;
        }

        [SetUp]
        public void SetUpCheck()
        {
            Console.WriteLine(Splitter);
            Console.WriteLine($"Started test '{TestContext.CurrentContext.Test.FullName}'");
            Console.WriteLine(Splitter);
        }

        [TearDown]
        public void CleanupCheck()
        {
            Console.WriteLine(Splitter);
            Console.WriteLine($"Finished test '{TestContext.CurrentContext.Test.FullName}'");
            Console.WriteLine(Splitter);
        }

        protected string GenerateBuildLocation(string name)
        {
            var location = Path.Combine(Path.GetTempPath(), name);
            Utilities.CleanPath(location);
            Directory.CreateDirectory(location);
            return location;
        }

        /// <summary>
        /// Creates asset file containing provided plain text content and its directory path if it does not exist
        /// </summary>
        /// <param name="relativePath">Relative asset path from (but not including) "Assets" folder</param>
        /// <param name="fileName">Asset file name</param>
        /// <param name="content">Asset content</param>
        /// <returns>Returns nothing</returns>
        protected static void CreateAssetWithTextContent(string relativePath, string fileName, string content)
        {
            CreateAssetFolder(relativePath);
            CreateAssetWithTextContent($"{relativePath}/{fileName}", content);
        }

        /// <summary>
        /// Creates asset file containing provided plain text content and saves it in existing directory
        /// </summary>
        /// <param name="relativePath">Relative asset path from (but not including) "Assets" folder or Packages folder. Provided path must exist</param>
        /// <param name="content">Asset content</param>
        /// <returns>Returns nothing</returns>
        protected static void CreateAssetWithTextContent(string relativePath, string content)
        {
            File.WriteAllText(GetAbsolutePath(relativePath), content);
        }

        /// <summary>
        /// Creates assets folder including all required parent folders
        /// </summary>
        /// <param name="relativePath">Relative asset path from (but not including) "Assets" folder or Packages folder</param>
        /// <returns>Returns nothing</returns>
        protected static void CreateAssetFolder(string relativePath)
        {
            Directory.CreateDirectory(GetAbsolutePath(relativePath));
        }

        /// <summary>
        /// Deletes assets file or folder and its meta data
        /// </summary>
        /// <param name="relativePath">Relative asset path from (but not including) "Assets" folder or Packages folder</param>
        /// <returns>Returns true if asset was found and deleted</returns>
        protected static bool DeleteAsset(string relativePath)
        {
            if (relativePath.StartsWith(AssetsName))
                return AssetDatabase.DeleteAsset(relativePath);
            return AssetDatabase.DeleteAsset($"{AssetsName}/{relativePath}");
        }

        protected static bool DeleteAssets(List<string> relativePaths)
        {
            var success = true;
            foreach (var path in relativePaths)
            {
                success &= DeleteAsset(path);
            }
            return success;
        }


        /// <summary>
        /// Creates absolute path from relative asset path
        /// </summary>
        /// <param name="relativePath">Relative asset path from (but not including) "Assets" folder or from Packages</param>
        /// <returns>Absolute asset path</returns>
        protected static string GetAbsolutePath(string relativePath)
        {
            return $"{Application.dataPath}/{relativePath}";
        }

    }
}
