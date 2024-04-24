using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace UAAL.EditorTests
{
    public class BasicTests : EditorTestBase
    {
        [SetUp]
        public void BasicTestsSetUp()
        {
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        }

        [TestCase(ScriptingImplementation.IL2CPP)]
        [TestCase(ScriptingImplementation.Mono2x)]
        public void GradleProjectWithAllActivitiesBuildsSuccesfully(ScriptingImplementation scriptingImplementation)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, scriptingImplementation);
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity | AndroidApplicationEntry.GameActivity;
            var location = GenerateBuildLocation($"BasicUAAL{scriptingImplementation}");
            Utilities.BuildProject(location, true);
            Utilities.BuildGradleProject(location);
        }
    }
}
