# Building the plug-in

## Prerequisites

1. A legitimate local installation of Vampire Crawlers for Windows (Unity 6000.0.62 IL2CPP).
2. BepInEx 6 IL2CPP for Windows x64: `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip` from https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+5b766a3.zip
3. Unity 6000.0.62 base libraries for BepInEx interop generation: https://unity.bepinex.dev/libraries/6000.0.62.zip
4. A C# compiler compatible with .NET Framework 4.7.2 / modern C# (the release was built with Roslyn `csc.exe`).

## Prepare references

1. Extract BepInEx into the game directory.
2. Provide the matching Unity base libraries to BepInEx according to its IL2CPP setup instructions.
3. Launch the game once and close it after BepInEx has generated `BepInEx/interop`.
4. Collect all valid `.dll` files from:
   - `BepInEx/core`
   - `BepInEx/interop`
   - BepInEx's bundled `dotnet` directory

The game-specific assemblies referenced by the source, such as `Pancake.dll`, are generated from the reviewer's own lawful local game copy during this step. They are not distributed in this repository.

## Compile

Compile `src/VampireCrawlersArabic.cs` as a library against the reference assemblies collected above. Example Roslyn command shape:

```powershell
csc.exe /nologo /target:library /langversion:latest /nostdlib+ /out:VampireCrawlersArabic.dll /r:<each reference DLL> src\VampireCrawlersArabic.cs
```

Install the resulting DLL beside the following two runtime files in:

`BepInEx/plugins/VampireCrawlersArabic/`

- `VampireCrawlersArabic.dll`
- `strings-ar.json` from `translations/strings-ar.json`
- `Arabic Pixel 2.ttf` obtained from its source at https://fontstruct.com/fontstructions/show/1425456, together with the notices in `licenses/`

Start the game and select **English** in its language setting. The plug-in replaces the loaded English string-table values in memory with the Arabic translations.

## External projects

- BepInEx source and licence: https://github.com/BepInEx/BepInEx
- RTLTMPro is used through the game runtime; no separate copy is shipped here.
- Arabic Pixel 2 is CC0; its attribution and modification notes are included in `licenses/`.
