---
title: .NET for Android Build Targets
description: "This document will list all supported targets in the .NET for Android build process."
ms.date: 04/11/2024
---

# Build targets

The following build targets are defined in .NET for Android projects.

## Build

Builds the source code within a project and all dependencies.

This target *does not* create an Android package (`.apk` file).
To create an Android package, use the [SignAndroidPackage](#signandroidpackage)
target, *or* set the
[`$(AndroidBuildApplicationPackage)](build-properties.md#androidbuildapplicationpackage)
property to True when building:

```shell
msbuild /p:AndroidBuildApplicationPackage=True App.sln
```

## BuildAndStartAotProfiling

Builds the app with an  embedded AOT profiler, sets the profiler TCP port to
[`$(AndroidAotProfilerPort)`](build-properties.md#androidaotprofilerport),
and starts the default activity.

The default TCP port is `9999`.

Added in Xamarin.Android 10.2.

## Clean

Removes all files generated by the build process.

## FinishAotProfiling

Must be called *after* the [BuildAndStartAotProfiling](#buildandstartaotprofiling)
target.

Collects the AOT profiler data from the device or emulator through the TCP port
[`$(AndroidAotProfilerPort)`](build-properties.md#androidaotprofilerport)
and writes them to
[`$(AndroidAotCustomProfilePath)`](build-properties.md#androidaotcustomprofilepath).

The default values for port and custom profile are `9999` and `custom.aprof`.

To pass additional options to `aprofutil`, set them in the
[`$(AProfUtilExtraOptions)`](build-properties.md#aprofutilextraoptions)
property.

This is equivalent to:

```shell
aprofutil $(AProfUtilExtraOptions) -s -v -f -p $(AndroidAotProfilerPort) -o "$(AndroidAotCustomProfilePath)"
```

Added in Xamarin.Android 10.2.

## GetAndroidDependencies

Creates the `@(AndroidDependency)` item group, which is used by the
[`InstallAndroidDependencies`](#installandroiddependencies) target to determine
which Android SDK packages to install.

## Install

[Creates, signs](#signandroidpackage), and installs the Android package onto
the default device or virtual device.

The [`$(AdbTarget)`](build-properties.md#adbtarget)
property specifies the Android target device the
Android package may be installed to or removed from.

```bash
# Install package onto emulator via -e
# Use `/Library/Frameworks/Mono.framework/Commands/msbuild` on OS X
MSBuild /t:Install ProjectName.csproj /p:AdbTarget=-e
```

## InstallAndroidDependencies

Calls the [`GetAndroidDependencies`](#getandroiddependencies) target, then installs
the Android SDK packages specified in the `@(AndroidDependency)` item group.

```dotnetcli
dotnet build -t:InstallAndroidDependencies -f net8.0-android "-p:AndroidSdkDirectory=<path to sdk>" "-p:JavaSdkDirectory=<path to java sdk>"
```

The `-f net8.0-android` is required as this target is a .NET for Android specific target. If you omit this argument
you will get the following error:

```
error MSB4057: The target "InstallAndroidDependencies" does not exist in the project.
```

The `AndroidSdkDirectory` and `JavaSdkDirectory` properties are required as we need to know where to install the required components. These directories can be empty or existing. Sdk components
will be installed on top on an existing sdk installation.

The [`$(AndroidManifestType)`](build-properties.md#androidmanifesttype)
MSBuild property controls which
[Visual Studio SDK Manager repository](/xamarin/android/get-started/installation/android-sdk?tabs=windows#repository-selection)
is used for package name and package version detection, and URLs to download.

## RunWithLogging

Runs the application with additional logging enabled.  Helpful when reporting or investigating an issue with
either the application or the runtime.  If successful, messages printed to the screen will show location
of the logcat file with the logged messages.

Properties which affect how the target works:

  * `/p:RunLogVerbose=true` enables even more verbose logging from MonoVM
  * `/p:RunLogDelayInMS=X` where `X` should be replaced with time in milliseconds to wait before writing the
    log output to file.  Defaults to `1000`.

## SignAndroidPackage

Creates and signs the Android package (`.apk`) file.

Use with `/p:Configuration=Release` to generate self-contained "Release" packages.

## StartAndroidActivity

Starts the default activity on the device or the running emulator.

To start a different activity, set the
[`$(AndroidLaunchActivity)`](build-properties.md#androidlaunchactivity)
property to the activity name.

This is equivalent to:

```shell
adb shell am start -S -n @PACKAGE_NAME@/$(AndroidLaunchActivity)
```

Added in Xamarin.Android 10.2.

## StopAndroidPackage

Completely stops the application package on the device or the running emulator.

This is equivalent to:

```shell
adb shell am force-stop @PACKAGE_NAME@
```

Added in Xamarin.Android 10.2.

## Uninstall

Uninstalls the Android package from the default device or virtual device.

The [`$(AdbTarget)`](build-properties.md#adbtarget)
property specifies the Android target device the
Android package may be installed to or removed from.

## UpdateAndroidResources

Updates the `Resource.designer.cs` file.

This target is usually called by the IDE when new resources are added to the project.
