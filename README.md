# SymphonyRecomp — Android Dual Screen

An experimental dual-screen extension of the Android port of **Castlevania:
Symphony of the Night**. The game runs on the main screen; the second Android
screen shows the explored map, player position, equipment, inventory and relics.

This derivative project is not an official Konami release and is not endorsed
by or affiliated with sergiomanzur, BlackLabelHQ or the SOTN Decomp project.
Please report issues with this extension here rather than to those projects.

## Credits and provenance

- **[sergiomanzur/SymphonyRecomp](https://github.com/sergiomanzur/SymphonyRecomp)**:
  the Android port, menus, touch controls, controller support, disc importer and
  foundation of this extension. Base: `05bfad3fa18203477456440083165d8471683238`.
- **[BlackLabelHQ/SymphonyRecomp](https://github.com/BlackLabelHQ/SymphonyRecomp)**:
  the original recompilation, patches, game integration, widescreen and
  quality-of-life features.
- **flaffy and [RecompOne](https://github.com/BlackLabelHQ/RecompOne)**:
  the PlayStation static recompiler and runtime. The Android runtime fork is
  [sergiomanzur/RecompOne](https://github.com/sergiomanzur/RecompOne), pinned to
  `275a1271415f97924dcfd38a721bc7407f6baa9a`.
- **The SOTN Decomp community**: reverse-engineering research underlying the
  original projects' function names and knowledge of the game.
- **Konami and KCET**: the original game and characters.

The Android base README is preserved in [UPSTREAM_README.md](UPSTREAM_README.md).
Original contributor credits are also retained in the source and build scripts.
The Android base and this extension were developed with AI assistance. Human-only
statements in preserved upstream files describe the original upstream work.

## Changes in this extension

- A companion panel with **MAP**, **EQUIPMENT** and **RELICS** tabs.
- Explored map cells and the current player position.
- Current equipment, carried items and obtained relics from the running game.
- EQUIPMENT, inventory choices and RELICS can be browsed while gameplay continues.
  Select a slot/item or relic below; only **Equip**, **Enable Relic** or **Disable Relic**
  opens the original menu and pauses for review. **Confirm Equipment** or **Confirm Toggle**
  lets the game apply the change; confirmation or cancellation closes that pause
  automatically and keeps the current lower-screen tab. A pause you opened manually
  with START stays under your control.
- English companion menus with cobalt panels, silver frames, serif typography,
  gold headings and a selection cursor, plus a compact castle/level/HP header.
- 4:3 gameplay fills the active height while retaining both sides of the picture.
- 16:9 renders additional stage content and recalculates the field of view for
  the active height; it does not stretch a finished 4:3 picture sideways.
- Native menus, the original map, file selection and FMV retain their framing;
  FMV keeps its original aspect ratio. Framing updates automatically after loading
  and when switching between gameplay and native menus.
- Decorative borders remain configurable from the display menu.
- Android file-picker save-state import/export, validation and a backup before
  replacing an existing slot.

Version: `0.22-framing-menu`, Android version code `22`.

## Downloads and installation guides

**[Download version 0.22](https://github.com/pettersdark/SymphonyRecomp-DUAL/releases/tag/v0.22)**

- [ARM64 APK for Android handhelds](https://github.com/pettersdark/SymphonyRecomp-DUAL/releases/download/v0.22/SymphonyRecomp-DUAL-0.22-arm64.apk)
- [English illustrated installation guide (0.21 edition)](https://github.com/pettersdark/SymphonyRecomp-DUAL/releases/download/v0.21/SymphonyRecomp-DUAL-0.21-Installation-EN.pdf)
- [Guía ilustrada en español (edición 0.21)](https://github.com/pettersdark/SymphonyRecomp-DUAL/releases/download/v0.21/SymphonyRecomp-DUAL-0.21-Installation-ES.pdf)

The 0.22 installation ZIP includes both Android architectures and bilingual
quick instructions for a fresh installation. The illustrated 0.21 guides remain
linked above; their installation steps still apply. See the 0.22 release notes
for the new framing behavior and menu appearance. Supply your own USA CUE/BIN disc.

## Fresh installation

Supply your own legally obtained North American PlayStation disc dump,
**SLUS-00067**. This repository does not include a disc image, prepared save states,
generated game-code files or private signing keys.

Install the ARM64 APK, open the app and use **Choose disc files** to select the
CUE and every BIN referenced by it. Wait for the import. At the title screen,
press START and create a new game in an empty file. No prepared save is needed.

Enable the lower screen in the handheld firmware, then use:

**MENU → Display → Second screen → Map, equipment and relics → On**

The status should show **Connected**. Choose **Aspect ratio → 4:3 original**
to retain the complete active picture inside a 4:3 area, or **16:9 widescreen**
for an extended stage view. Gameplay fills the active height in both modes.
Choose borders under **Display → Decorative borders**.

## Build from source

Install Git, .NET 10 SDK with its Android workload, a JDK and the Android SDK.

```sh
git clone --recurse-submodules https://github.com/pettersdark/SymphonyRecomp-DUAL.git
cd SymphonyRecomp-DUAL
dotnet workload install android
```

Apply the runtime extension once on a fresh clone:

```sh
git -C RecompOne apply --check ../local-patches/recompone-dual-screen.patch
git -C RecompOne apply ../local-patches/recompone-dual-screen.patch
```

The patch preserves the local changes to the pinned RecompOne submodule.
It may already be applied in the original development checkout.

Put your own CUE and its referenced BIN files in `disc/`. Configuration expects
`disc/Castlevania - Symphony of the Night (USA).cue`; the CUE must reference the
actual BIN filenames. Generate the game code locally:

```sh
dotnet run --project RecompOne/RecompOne.Recompiler -c Release -p:TargetFrameworks=net10.0 -- config/sotn.json
dotnet publish RecompOne.SoTN.Android.csproj -c Release -r android-arm64 -p:RuntimeIdentifiers=android-arm64
```

Build one architecture at a time. For an x64 Android emulator, replace
`android-arm64` with `android-x64`. Configure your local SDK/JDK paths and your
own signing key as required by .NET Android.

## Validation and limitations

The 0.22 native equipment menu was checked from a fresh installation and
**New Game**, with no state loads: names, icons, statistics and item descriptions
rendered correctly in both aspect selections. Earlier incomplete-menu reports
came from a test setup using a different old disc and incomplete graphics output;
the clean check required no APK change. See [0.22 validation](docs/VALIDATION-0.22.md).


The x64 app has been exercised in an Android emulator with a separate virtual
display, including map updates, equipment/relic navigation and file-picker
save-state import/export. ARM64 builds successfully; physical dual-screen
handheld validation is still pending.

- Firmware must expose the lower panel as a presentation-compatible Android display.
- Physical controller mapping, audible sound, sustained performance and extended
  play still require testing on the console.
- A pending XA audio/CD issue can generate repeated out-of-range reads and logs,
  potentially affecting performance.
- Some native libraries emit 16 KB page-alignment warnings during ARM64 builds.
- Keep normal in-game saves alongside save states.

## Licenses

This project does **not** claim a blanket MIT license. RecompOne includes its
own MIT license, which must be retained. No clear repository-wide license was
identified in the reviewed SymphonyRecomp base or bundled mods. Preserve their
credits and notices; this README grants no rights over their code or game assets.

Dependencies retain their own terms. ImageSharp 3.1.12 uses the Six Labors Split
License; fonts and other libraries have their own notices. Rights to the original
game remain with their respective holders.

## CI workflows

The inherited workflows are manual-only in this repository. They depend on
upstream-specific private disc-source credentials and are not configured as an
automatic build or release service for this extension. Local builds use your
own disc as described above.
