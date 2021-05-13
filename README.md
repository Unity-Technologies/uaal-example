# _Unity as a Library_ integration example for Android, iOS, and tvOS #

Many mobile app developers prefer to build using native platform technologies:
Java/Kotlin on Android, Objective-C/Swift on iOS. In some scenarios, they want
to include features powered by Unity such as 3D/2D Real Time Rendering, AR
experience, interaction with 3D models, and 2D mini games.

Starting with Unity 2019.3.0a2, Unity introduced a new feature to use _Unity as
a Library_ in native apps. This integrates the Unity runtime components and
content into their own separate platform project. A new tool called the Unity
Runtime Library exposes controls to manage when and how to load/activate/unload
this separate project from within the native application.

**Warning**

Using Unity as a Library **requires you to have experience with developing for
native platform technologies** such as Java or Kotlin on Android, Objective-C
or Swift on iOS, or Win32/UWP on Windows. You need to be familiar with the
structure of the project, language features and specific platform configuration
options (like user permissions, for example).


**Limitations**

While we tested many scenarios for Unity as a library hosted by a native app,
Unity no longer controls the lifecycle of the runtime. We cannot guarantee
it'll work in all possible use cases.
For example:
- Unity as a Library supports rendering only full screen, rendering on a part
of the screen isn’t supported.
- Loading more than one instance of the Unity runtime isn’t supported.
- You may need to adapt 3rd party Plug-ins (native or managed) to work properly
- Overhead of having Unity in unloaded state is: 90Mb for Android and 110Mb for iOS

**How it works**

The build process, overall, is still the same. However, each time you click
'build' Unity will generate not 1 but _2_ iOS Xcode projects or _2_ Android
Gradle modules:
1. A library part (`Unity-iPhone` or `unityLibrary`)
   - Includes all of the Unity-specific source and plugins
   - Built into an iOS Framework or an Android Archive (AAR) 
2. A thin launcher part (`launcher`)
   - Depends on the library part
   - Includes app representation data
   - Built into the full app (IPA or APK/AAB file)

We have step by step explanations on how to build and include the
[iOS](docs/ios.md) / [Android](docs/android.md)
library parts into your pre-existing application.

