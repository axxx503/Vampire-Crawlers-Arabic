# Vampire Crawlers Arabic Localization — source review

This repository contains the complete source material for the Nexus Mods file **Vampire Crawlers Arabic 0.1.9**.

The mod is a local BepInEx 6 IL2CPP plug-in for the Steam Windows version of *Vampire Crawlers*. It translates game string-table entries into Arabic at runtime and applies RTL text shaping, the bundled Arabic Pixel 2 font, and number/punctuation alignment for Arabic UI.

## What is included

- `src/VampireCrawlersArabic.cs` — complete production C# source of the plug-in DLL shipped in version 0.1.9.
- `translations/strings-ar.json` — the 2,271 runtime Arabic translation entries loaded by the plug-in.
- `translations/translation-source.json` — editable English-to-Arabic translation source data.
- `licenses/` — Arabic Pixel 2 font attribution, licence, and modification notes.
- `BUILD.md` — build requirements and reproducible build procedure.
- `RELEASE.md` — the exact file and hashes being reviewed.

## What is deliberately not included

This repository contains no game executable, game assets, save data, Steam files, generated game interop assemblies, BepInEx binaries, or compiled plug-in DLL. Those are either proprietary game files or third-party dependencies obtainable from their official sources.

## Behaviour and security scope

The plug-in runs only inside Vampire Crawlers through BepInEx. It reads its adjacent `strings-ar.json`, patches in-memory localization and TMPro text rendering, and creates a font asset from the adjacent Arabic Pixel 2 font file.

It does **not** download files, make network requests, collect telemetry, launch processes, change save files, or patch game files on disk.

The repository is provided specifically to support an automated-quarantine review of the Nexus Mods upload.
