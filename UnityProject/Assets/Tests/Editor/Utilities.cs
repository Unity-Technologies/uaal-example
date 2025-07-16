using System;
using System.Text;
using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.Build;
using System.Diagnostics;
using UnityEditor.SceneManagement;
using System.Text.RegularExpressions;

namespace UAAL.EditorTests
{
    public static class Utilities
    {
        internal class BuildProjectResult
        {
            internal int ExitCode { get; }
            internal string StdOut { get; }
            internal string StdError { get; }

            internal BuildProjectResult(int exitCode, string stdOut, string stdErr)
            {
                ExitCode = exitCode;
                StdOut = stdOut;
                StdError = stdErr;
            }

            public override string ToString()
            {
                return $"Stdout:\n'{StdOut}'\nStderr\n:'{StdError}'\nExitCode:'{ExitCode}'";
            }
        }


#if UNITY_6000_3_OR_NEWER
        private static string SevenZipPath => EditorApplication.sevenZipPath;
#else
        private static string SevenZipPath => Path.Combine(EditorApplication.applicationContentsPath, "Tools",
            Application.platform == RuntimePlatform.WindowsEditor ? "7z.exe" : "7za");
#endif

        public static void BuildProject(string location, bool cleanDirectoryOrFile, out string warnings, bool openScene = false)
        {
            if (cleanDirectoryOrFile)
                CleanPath(location);

            var options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None;
            Log($"Building project to: {location}");
            Log(@$"Build parameters:
- Build Options: {options}
- Application Entry: {PlayerSettings.Android.applicationEntry}
- Optimized Frame Pacing: {PlayerSettings.Android.optimizedFramePacing}
- Scripting Backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}
- Strip Engine Code: {PlayerSettings.stripEngineCode}
- Architectures: {PlayerSettings.Android.targetArchitectures}
");
            var defaultScene = "Assets/Scenes/SampleScene.unity";
            if (openScene)
                EditorSceneManager.OpenScene(defaultScene);
            var result = BuildPipeline.BuildPlayer(new string[] { defaultScene }, location, BuildTarget.Android, options);

            var idx = 0;
            var warningSummary = new StringBuilder();
            foreach (var s in result.steps)
            {
                foreach (var m in s.messages)
                {
                    if (m.type != LogType.Warning)
                        continue;
                    warningSummary.AppendLine($"Warning {idx}:");
                    warningSummary.AppendLine(m.content);
                    idx++;
                }
            }
            warnings = warningSummary.ToString();
            Assert.That(result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, "Build procedure failed!");
        }

        public static void BuildProject(string location, bool cleanDirectoryOrFile, bool openScene = false)
        {
            BuildProject(location, cleanDirectoryOrFile, out var warnings, openScene);
        }

        private static string GetJVMArgs(string gradleProjectPath)
        {
            // This value can be get from AndroidJavaTools.PreferredHeapSizeForJVM, but AndroidJavaTools is not accessible
            // Instead get it from gradle.properties

            var gradleProperties = Path.Combine(gradleProjectPath, "gradle.properties");
            if (!File.Exists(gradleProperties))
                return string.Empty;
            var contents = File.ReadAllText(gradleProperties);
            var regex = new Regex(@"org\.gradle\.jvmargs=(?<jvmargs>\S+)");
            var result = regex.Match(contents);
            if (result.Success)
                return result.Groups["jvmargs"].Value;
            return string.Empty;
        }

        internal static BuildProjectResult BuildGradleProject(string workingDirectory)
        {
            var java = Path.Combine(UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath, "bin", "java");
            var gradleLauncherJarPath = GetGradleLauncherJar(Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "Tools"));
            if (Application.platform == RuntimePlatform.WindowsEditor)
                java += ".exe";
            Log($"Building gradle project in '{workingDirectory}' (Check editor.log for output)");
            Log($"Java Binary: {java}");
            Log($"Gradle Launcher: {gradleLauncherJarPath}");
            var gradleTask = "assembleDebug";
            Log($"Gradle Task: {gradleTask}");

            var gradleFilePath = Path.Combine(workingDirectory, "build.gradle");
            var args = string.Join(" ",
                new[]
                {
                "-classpath",
                $"\"{gradleLauncherJarPath}\"",
                "org.gradle.launcher.GradleMain",
                "-b",
                $"\"{gradleFilePath}\"",
                "--no-daemon"
                });

            var jvmArgs = GetJVMArgs(workingDirectory);
            if (!string.IsNullOrEmpty(jvmArgs))
                args += $" \"-Dorg.gradle.jvmargs={jvmArgs}\"";

            args += $" \"{gradleTask}\"";

            Process process = new Process();
            process.StartInfo.FileName = java;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.CreateNoWindow = true;
            var output = new StringBuilder();
            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                }
            });

            var error = new StringBuilder();
            process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                }
            });

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            var result = new BuildProjectResult(process.ExitCode, output.ToString(), error.ToString());


            Console.WriteLine($"Exit Code: {result.ExitCode}\nStandard Output:\n{result.StdOut}\nStandard Error:\n{result.StdError}");
            Assert.AreEqual(0, result.ExitCode, $"Gradle Build failed:\n{result.StdError}");

            return result;
        }

        private static string GetGradleLauncherJar(string androidBuildTools)
        {
            string gradleDir = Path.Combine(androidBuildTools, "gradle");
            string libDir = Path.Combine(gradleDir, "lib");
            var launcherFiles = Directory.GetFiles(libDir, "gradle-launcher-*.jar");
            if (launcherFiles.Length != 1)
                throw new Exception("Failed to find gradle-launcher in " + libDir);
            return launcherFiles[0];
        }

        public static void Log(string message)
        {
            UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, message);
        }

        public static void AssertFileOrDirectoryExistance(string path, bool shouldExist)
        {
            if (shouldExist)
                Assert.IsTrue(File.Exists(path) || Directory.Exists(path), $"Expected '{path}' to exist");
            else
                Assert.IsFalse(File.Exists(path) || Directory.Exists(path), $"Expected '{path}' to NOT exist");
        }

        public static void CleanPath(string path)
        {
            const int kTries = 3;
            Exception lastException = null;
            for (int i = 0; i < kTries; i++)
            {
                try
                {
                    lastException = null;

                    if (Directory.Exists(path))
                    {
                        Console.WriteLine($"Deleting directory '{path}', try {i}");
                        Directory.Delete(path, true);
                    }

                    if (File.Exists(path))
                    {
                        Console.WriteLine($"Deleting file '{path}', try {i}");
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            if (lastException != null)
                throw lastException;
        }

        public static string Unzip(string workingDirectory, string packageName, string extractionFolder = null)
        {
            if (string.IsNullOrEmpty(packageName))
                throw new ArgumentException("A valid packageName has to be provided for unzipping.");
            var output = string.IsNullOrEmpty(extractionFolder) ?
                Path.Combine(workingDirectory, "unzipped") :
                Path.Combine(workingDirectory, extractionFolder);
            Utilities.CleanPath(output);

            var processArguments = $"x -o{output} {packageName}";
            var exitCode = RunCommand(SevenZipPath, processArguments, out var commandOutput);
            if (exitCode != 0)
                throw new Exception($"Failed to unzip '{packageName}', check the console for errors");

            return output;
        }

        public static int RunCommand(string executable, string arguments, out string output)
        {
            UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"{executable} {arguments}");

            static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine, StringBuilder output)
            {
                if (outLine.Data == null)
                    return;
                lock (output)
                {
                    output.AppendLine(outLine.Data);
                }
                UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, outLine.Data);
            }
            var process = new Process();
            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            var commandOutput = new StringBuilder();
            process.OutputDataReceived += new DataReceivedEventHandler((obj, evtArgs) => OutputHandler(obj, evtArgs, commandOutput));
            process.ErrorDataReceived += new DataReceivedEventHandler((obj, evtArgs) => OutputHandler(obj, evtArgs, commandOutput));

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            output = commandOutput.ToString();
            return process.ExitCode;
        }
    }
}