## Integrating Unity as a library into standard Android app
This document explains how to include Unity as a Library into standard Android application through Activity. You can read more about [Unity as a Library](https://docs.unity3d.com/6000.0/Documentation/Manual/UnityasaLibrary.html).

**Requirements:**
- Android Studio Hedgehog (2023.1.1) or later
- Unity version 2023.1.7f1, 2023.2.0b3 or later

[Note] For Unity versions from 2019.3.0b4 to 2022.2.0a17 use [19LTS-21LTS branch](https://github.com/Unity-Technologies/uaal-example/tree/uaal-example/19LTS-21LTS). For Unity versions from 2022.2.0a17 to 2023.1.0a16 use [22LTS branch](https://github.com/Unity-Technologies/uaal-example/tree/uaal-example/22LTS). For Unity versions from 2023.1.7f1 to 2023.2.0b3 use [23LTS branch](https://github.com/Unity-Technologies/uaal-example/tree/uaal-example/23LTS).

**1. Get source**
- Clone or Download GitHub repo [uaal-example](https://github.com/Unity-Technologies/uaal-example). It includes:
  <br><img src="images/android/rootFolderStructure.png">
  - Unityproject - this is a simple demo project made with Unity which will be exported for the standard Android application.
  - MainApp.androidlib - Inside Unityproject, you can find MainApp.androidlib, which is [Android Library Project](https://docs.unity3d.com/6000.0/Documentation/Manual/android-library-project-import.html) and has a simple UI, MainUnityActivity and MainUnityGameActivity.

**2. Generate Gradle project for Android platform**
- Open UnityProject in Unity Editor
- Go to Build Settings window (Menu / File / Build Settings)
  - Select and switch to Android Platform
- Go to Player Settings window (click Player Settings button at the bottom left corner of Build Settings or use Edit / Project Settings menu and choose Player tab on the left)
  - In Other Settings -> Configuration section -> choose targeted architectures
    <br><img src="images/android/selectArchitectures.png">
  - In Other Settings -> Configuration section -> select both Activity and GameActivity as Application Entry Point
    <br><img src="images/android/ApplicationEntryPoint.png" width='600px'>
- Go to Project tab and select MainApp under Assets
  - In Inspector tab -> Select platforms for plugin -> select Android
  - In Inspector tab -> Platform settings -> Select dependent module -> choose None
    <br><img src="images/android/androidlibImportSettings.png" width='700px'>
- Go back to Build Settings window
  - Select option “Export Project” 
    <br><img src="images/android/exportProject.png" width='400px'>
  - Export UnityProject to a folder, the folder structure should look like this
    <br><img src="images/android/exportedProjectFolder.png" width='400px'>

**3. Prepare to build in Android Studio**
- Open the exported project in Android Studio. The launcher folder will not be visible if you select Android from the Project menu (UnityProject/Assets/Editor/AndroidGradleProjectModifier.cs removes launcher/build.gradle)
    <br><img src="images/android/exportedProjectInAndroidStudio.png">
- Open build.gradle(Module: MainApp.androidlib) file
  - In the file take a look at android{defaultConfig{ndk{ block and make sure abiFilters match the architectures you selected in Unity editor before exporting the project. The filter must match architectures in Unity editor exactly. If Unity exports only ARMv7 architecture, but the filter includes arm64-v8a, the application will crash on ARM64 devices. Check for valid abiFilters values in the [official Android documentation](https://developer.android.com/ndk/guides/abis#sa).
  <br><img src="images/android/buildGradleAppAbiFilters.png">
- Click Sync Now to do a project sync since Gradle files have been modified
  <img src="images/android/syncGradle.png">

## Project is ready
Everything is ready to build, run and debug:
<br><img src="images/android/buildOnDevice.png" width='500px'>
<br>If everything succeeded, at this point you should be able to run NativeAndroidApp:

Main Activity | Unity Activity or GameActivity
------------------------ | -------------------------
<img src="images/android/appNativeSS.png" width='750px' height='800px'> | <img src="images/android/appUnitySS.png" height='800px'>
Main Activity | Unity is loaded and is running in a separate Activity. Light grey buttons in the middle are added from the MainUnityActivity or MainUnityGameActivity implemented in NativeAndroidApp

## Notes
- If the console displays error messages like the image below, open Package Manager and remove or install Ugui or Unity UI package depending on the version of Unity Editor.
  <br><img src="images/android/packageErrors.png" width='750px'>
  <br><img src="images/android/openPackageManager.png" width='750px'>
  <br><img src="images/android/uguiPackages.png" width='750px'>
  <br><img src="images/android/unityUIPackages.png" width='750px'>
- If the Build Output in Android Studio displays error messages like the image below, open SDK Manager and remove the build tool and then install it again.
  <br><img src="images/android/buildToolErrors.png" width='800px'>
  <br><img src="images/android/sdkManager1.png" width='450px'>
  <br><img src="images/android/sdkManager2.png" width='700px'> 
- Unity is running in another process android:process=":Unity" (AndroidManifest.xml at app module)
- In step 2, if you select only Activity or GameActivy as Application Entry Point, MainUnityActivity.java or MainUnityGameActivity.java file must be deleted.
  - When only Activity is checked as ApplicationEntry Point
    <br><img src="images/android/selectActivity.png" width='600px'>
    <br><img src="images/android/deleteMainUnityGameActivity.png">
  - When only GameActivity is checked as ApplicationEntry Point
    <br><img src="images/android/selectGameActivity.png" width='600px'>
    <br><img src="images/android/deleteMainUnityActivity.png">
  
- (Optional) We found some Android 7.* devices set frontOfTask to wrong state for activities, as a result when finishing/quitting Unity activity whole task goes to background instead of bringing back Main activity. Next workaround keeps expected behavior: add the below code to MainUnityActivity.java or UnityPlayerGameActivity.java or both in NativeAndroidApp
  ```
  @Override public void onUnityPlayerQuitted() { SharedClass.showMainActivity(""); finish(); }
  ```
