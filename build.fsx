

#r "packages/FAKE.3.8.5/tools/FakeLib.dll"

open Fake
open Fake.FileSystemHelper
open Fake.NuGetHelper
open System

let inline (/) x y = IO.Path.Combine(x, y)
let mkdir x = (ensureDirectory x); x

let buildDir = "build" |> mkdir
let tempDir = buildDir / "temp" |> mkdir
let pkgDir = buildDir / "nuget" |> mkdir

let msbuildTarget = getBuildParamOrDefault "msbuildTarget" "Build"
let buildMode = getBuildParamOrDefault "buildMode" "Release"

// Add "publishNuGet=true" to command line.
let publishNuGet = Boolean.Parse(getBuildParamOrDefault "publishNuGet" "false")

let coreBuildDir = "Jefferson.Core/bin" / buildMode
let procBuildDir = "Jefferson.FileProcessing/bin" / buildMode
let coreTestBuildDir = "Jefferson.Tests/bin" / buildMode

let getSemanticVersion (msVersion: Version) = msVersion.Major.ToString() + "." + msVersion.Minor.ToString() + "." + msVersion.Build.ToString()
let nugetVersion (asm) = getBuildParamOrDefault "pkgVersion" (asm |> VersionHelper.GetAssemblyVersion |> getSemanticVersion)

let jeffersonCsproj = !! ("./Jefferson.Core/*.csproj")
let jeffersonProcCsProj = !! ("./Jefferson.FileProcessing/*.csproj")
let jeffersonTestCsproj = !! ("./Jefferson.Tests/*.csproj")

let xunitConsole = "packages/xunit.runners.1.9.2/tools/xunit.console.clr4.exe"

trace "Building Jefferson, dumping environment:"
trace (" -- msbuild target: " + msbuildTarget)
trace (" -- msbuild configuration: " + buildMode)
trace (" -- build directory: " + buildDir)
trace (" -- nuget output directory: " + pkgDir)
trace (" -- publish nuget: " + publishNuGet.ToString())

Target "BuildCore" <| fun _ ->
   MSBuild null msbuildTarget ["Configuration", buildMode] jeffersonCsproj
   |> Log "Build Core Output"

Target "BuildFileProc" <| fun _ ->
   MSBuild null msbuildTarget ["Configuration", buildMode] jeffersonProcCsProj
   |> Log "Build Core Output"

Target "BuildTests" <| fun _ ->
   MSBuild null msbuildTarget ["Configuration", buildMode] jeffersonTestCsproj  
   |> Log "Build Tests Output"

Target "Test" <| fun _ ->
   !!(coreTestBuildDir / "Jefferson.Tests.dll") |> xUnit (fun p -> {p with OutputDir = coreTestBuildDir })

Target "Nuget" <| fun _ ->
   CleanDirs [tempDir]
   let libnet45 = tempDir / "lib/net45"
   ensureDirectory libnet45

   let version = nugetVersion (coreBuildDir / "Jefferson.dll")

   // Copy the main core DLL and its PDB.
   CopyFile libnet45 (coreBuildDir / "Jefferson.dll") 
   CopyFile libnet45 (coreBuildDir / "Jefferson.pdb")
   CopyFile libnet45 (procBuildDir / "Jefferson.FileProcessing.dll") 
   CopyFile libnet45 (procBuildDir / "Jefferson.FileProcessing.pdb")
   
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
         Copyright = "© 2014 Marcus van Houdt"
         Publish = publishNuGet
      }) ".nuget/jefferson.nuspec"

Target "Default" <| (fun _ -> trace "Default Target")

"Default"
   ==> "BuildCore"

"Default"
   ==> "BuildFileProc"

"Default"
   ==> "BuildTests"

"BuildTests"
   ==> "Test"

"BuildCore"
   ==> "NuGet"

"BuildFileProc"
   ==> "NuGet"

"Test"
   ==> "NuGet"

RunTargetOrDefault "Default"