

#r "packages/FAKE.3.8.5/tools/FakeLib.dll"

open Fake
open Fake.FileSystemHelper
open System

let inline (/) x y = IO.Path.Combine(x, y)
let mkdir x = (ensureDirectory x); x

let buildDir = "build" |> mkdir
let tempDir = buildDir / "temp" |> mkdir
let pkgDir = buildDir / "nuget" |> mkdir

let msbuildTarget = getBuildParamOrDefault "msbuildTarget" "Build"
let buildMode = getBuildParamOrDefault "buildMode" "Release"

let coreBuildDir = "Jefferson.Core/bin" / buildMode

let getSemanticVersion (msVersion: Version) = msVersion.Major.ToString() + "." + msVersion.Minor.ToString() + "." + msVersion.Build.ToString()
let nugetVersion (asm) = getBuildParamOrDefault "pkgVersion" (asm |> VersionHelper.GetAssemblyVersion |> getSemanticVersion)

trace "Building Jefferson, dumping environment:"
trace (" -- msbuild target: " + msbuildTarget)
trace (" -- msbuild configuration: " + buildMode)
trace (" -- build directory: " + buildDir)
trace (" -- nuget output directory: " + pkgDir)


Target "Nuget" <| fun _ ->
   CleanDirs [tempDir]
   let libnet45 = tempDir / "lib/net45"
   ensureDirectory libnet45

   // Read version from settings.xml.
   let version = nugetVersion (coreBuildDir / "Jefferson.dll")

   // Copy the main core DLL and its PDB.
   CopyFile libnet45 (coreBuildDir / "Jefferson.dll") 
   CopyFile libnet45 (coreBuildDir / "Jefferson.dll")
   
   // Create package.
   NuGet (fun pkg ->
      {pkg with
         ToolPath = ".nuget/nuget.exe"
         Authors = ["Marcus van Houdt"]  
         Project = "Jefferson"
         Description = "Jefferson Expression and Template Parser"
         OutputPath = pkgDir
         Summary = "Provides simple expression and template parsing and compiling"
         WorkingDir = tempDir
         Version = version
         Dependencies = [] //  NuGetHelper.getDependencies (libCoreDir / "packages.config")
         // AccessKey = getBuildParamOrDefault "nugetkey" ""
         Publish = false
      }) ".nuget/jefferson.nuspec"

