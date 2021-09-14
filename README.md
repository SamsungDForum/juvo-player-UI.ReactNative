# JuvoPlayer UI.ReactNative

## Introduction

ReactNative UI for JuvoPlayer 2.x

## Prerequesits
Detailed information on Tizen .NET application development can be found on Samsung Developers website
https://developer.samsung.com/smarttv/develop/tizen-net-tv.html

ReactNative UI for JuvoPlayer 2.x is based on React Native Tizen Dotnet implementation
https://github.com/Samsung/react-native-tizen-dotnet

In order to build ReactNative UI application, following packages are required:

- Nodejs https://nodejs.org/en/download/
  ***
  Important!

  Node.js versions higher than 12.10 were affected by regular expression syntax issue https://github.com/facebook/react-native/issues/26598 
  
  It impacts React Native Tizen Dotnet dependencies update (npm or yarn command). If it happens, downgrading to Nodejs version 12.10 will help.
  ***
- Yarn https://yarnpkg.com/en/
  
## Setup
- Verify node.js / yarn installation.
  This can be done by calling *yarn -v* / *node -v* in shell
  ```
  PS C:\> node -v
  v12.10.0
  PS C:\> yarn -v
  1.22.10
  ```

- Dependecies installation
  Installing dependencies is done by executing *yarn* command. This needs to be called from *JuvoReactNative* subdirectory
  ```
  PS C:\juvo-player-UI.ReactNative\JuvoReactNative> yarn
  yarn install v1.22.10
  [1/4] Resolving packages...
  [2/4] Fetching packages...
  info fsevents@2.1.3: The platform "win32" is incompatible with this module.
  info "fsevents@2.1.3" is an optional dependency and failed compatibility check. Excluding it from installation.
  [3/4] Linking dependencies...
  warning " > react-native@0.42.3" has incorrect peer dependency "react@~15.4.1".
  [4/4] Building fresh packages...
  Done in 79.98s.
  PS C:\juvo-player-UI.ReactNative\JuvoReactNative>
  ```

## Configuring ReactNative UI build.
ReactNative UI application can be build in debug and release mode. Debug builds are split into
- JS Debug
  This is a defult configuration for ReactNative UI repo. It caters for remote debugging of JS part of code. Supports live, hot reloading and web inspector support.
- C# Debug
  Configuration allowing debugging of C# Native part of code through Visual Studio or Visual Studio Code.
  
*DEBUGJS* preprocessor directive controls debug type. When defined in project, JS bundle will be downloaded from React Native bundling server. Otherwise, JS bundle will be taken from tpk package.

JS Debug / Release Configuration is controlled via *\JuvoReactNative\package.json* file. Set mode to "Debug" or "Release" according to needs.
```
// package.json
{
  // ...
  "config": {
    // ...
    "mode": "Debug"
  },
// ...
}
```
https://github.com/Samsung/react-native-tizen-dotnet/#debug

## Building ReactNative UI
Hybrid nature of React Tizen Dotnet application may require creation of react bundle. Created bundles are placed in *JuvoReactNative\Tizen\shared\res\assets\assets* directory. To create a bundle, go into *JuvoReactNative* subdirectory and execute *yarn bundle* command.
```
PS C:\juvo-player-UI.ReactNative\JuvoReactNative> yarn bundle
yarn run v1.22.10
$ react-native-tizen bundle
[Mon Sep 13 2021 20:52:37 GMT+0200 (GMT+02:00)][Util][INFO]: App current path : C:\juvo-player-UI.ReactNative\JuvoReactNative\
[Mon Sep 13 2021 20:52:37 GMT+0200 (GMT+02:00)][Util][INFO]: App Name : JuvoReactNative
[Mon Sep 13 2021 20:52:37 GMT+0200 (GMT+02:00)][Util][INFO]: Loading config file from : C:\juvo-player-UI.ReactNative\JuvoReactNative\package.json
[Mon Sep 13 2021 20:52:37 GMT+0200 (GMT+02:00)][Bundle][INFO]: Output Bundle Path: C:\juvo-player-UI.ReactNative\JuvoReactNative\Tizen\shared\res
[Mon Sep 13 2021 20:52:37 GMT+0200 (GMT+02:00)][Bundle][INFO]: React Native will Bundle file with Platfrom: tizen
Scanning 693 folders for symlinks in C:\juvo-player-UI.ReactNative\JuvoReactNative\node_modules (77ms)
Scanning 693 folders for symlinks in C:\juvo-player-UI.ReactNative\JuvoReactNative\node_modules (71ms)
Loading dependency graph, done.
bundle: start
bundle: finish
bundle: Writing bundle output to: C:\juvo-player-UI.ReactNative\JuvoReactNative\Tizen\shared\res\index.tizen.bundle
bundle: Done writing bundle output
bundle: Copying 7 asset files
bundle: Done copying assets
Done in 37.32s.
PS C:\juvo-player-UI.ReactNative\JuvoReactNative>
```

***
Bundling Notes:

Directory*JuvoReactNative\Tizen\shared\res\assets\assets*

This directory is redundant and can be safely removed reducing final tpk package size.
https://github.com/Samsung/react-native-tizen-dotnet/issues/30#issue-533804121

*yarn bundle --dev* command 

https://github.com/Samsung/react-native-tizen-dotnet/#usage
has no effect on bundling. JS code will remain "ugly" as... is. Caused by bundling script bug. "Ugliness" will be reflected in errors reported in JS Debug.
***

### JS Debug
1. Assure *package.json* mode is set to *Debug*
2. Assure C# build has *DEBUGJS* defined
3. Build C# Project

### C# Debug
1. Assure *package.json* mode is set to *Debug*
2. Assure C# build does not have *DEBUGJS* defined
3. Bundle JS package
4. Build C# Project

## Running ReactNative UI
In C# Debug or Release mode, no further actions are required.

JS Debug mode requires ReactNative packaging server to run, listening on port 8081. Server needs to run from *JuvoReactNative* subdirectory using *npm run server* command.
```
PS C:juvo-player-UI.ReactNative\JuvoReactNative> npm run server

> JuvoReactNative@1.5.5 server C:\juvo-player-UI.ReactNative\JuvoReactNative
> node node_modules/react-native/local-cli/cli.js start

Scanning 693 folders for symlinks in C:\juvo-player-UI.ReactNative\JuvoReactNative\node_modules (100ms)
 ┌────────────────────────────────────────────────────────────────────────────┐
 │  Running packager on port 8081.                                            │
 │                                                                            │
 │  Keep this packager running while developing on any JS projects. Feel      │
 │  free to close this tab and run your own packager instance if you          │
 │  prefer.                                                                   │
 │                                                                            │
 │  https://github.com/facebook/react-native                                  │
 │                                                                            │
 └────────────────────────────────────────────────────────────────────────────┘
Looking for JS files in
   C:\work\juvo-player-UI.ReactNative\JuvoReactNative

Loading dependency graph...
React packager ready.

Loading dependency graph, done.
```

Launch of newly installed application wil present React Native error screen

TODO ADD SCREEN PICTURE HEERE

Pressing RED button on RC will bring up remote debug configuration screen

TODO ADD SCREEN PICTURE HERE

Enter ReactNative packaging server ip.
If all went well, reloading an application will result in JS code being pulled from remote server.

## Tips
- When using C# Debug mode, it is recommended to manually uninstall an application prior to execution. Otherwise introduced changes may not be visible.
- Avoid re-launching application from IDE in JS debug mode. Such re-installs an application loosing configuration entered at initial run. If there are no changes to C# Native part of code, application can be run TV menu.
- There are 3 dlog channels associated with React applications
  - RN - React Native engine logs.
  - RNJS - JS side React Native logs. Includes console logs.
  - JuvoRN - C# Native ReactNative UI logs. 
  ***
  Logging is not available on production devices. May be available in Emulator.
  ***