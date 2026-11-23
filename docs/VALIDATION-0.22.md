# DUAL 0.22 validation

## Changes

Gameplay presents the active 207 lines of the 240-line PlayStation frame. The
4:3 mode retains the complete active width. Widescreen recalculates its rendered
field of view for that height and extends stage content, rather than pulling the
finished frame sideways. Presentation excludes RGB24 video and protects native
menus, native map and file selection. File selection shares a stage-buffer hook,
so native gameplay/menu/map state is checked too. A pre-presentation refresh
handles state changes occurring after that hook and after loading.

The lower panel uses cobalt surfaces, silver frames, serif text, gold section
headings and a selection cursor. Live browsing and native confirmation behavior
from 0.21 remain in place.

## Verification completed on 4 November 2026

- 37 cases exercised the production presentation method, including all aspect
  modes, native pause, file selection, native map, video and unloaded-game state.
- Equipment/relic regressions passed, including change-only pause, cancellation,
  native confirmation, stale selections and preservation of manual pauses.
- Final x64 APK checks covered automatic framing after a state load, 4:3 active
  height, real 16:9 extension, uncropped native review and automatic return to
  widescreen after cancellation.
- A separate new Android 15 x64 installation began with blank memory cards and
  no save states. New Game, intro, normal controller input through the Richter
  prologue and the native ending transition led to Alucard at the castle entrance.
  No state was loaded; no scene, position, inventory or player statistic was
  altered by the input/telemetry helper.
- Native root and equipment menus displayed the portrait, names, icons, stats,
  carried Potion and its item description. Both 4:3 and 16:9 aspect selections
  preserved native menu presentation.
- The earlier missing-text report was not reproduced in this clean run with the
  matching original USA disc. Earlier tests had used a different old disc with
  the same filenames/region and graphics settings showing incomplete output.
  Each factor's contribution to earlier artifacts was not isolated. This clean
  verification did not require a binary change.
- ARM64 and x64 source hashes match their checked build metadata. Both APKs use
  the same signing identity as 0.21 and pass ABI/archive checks. No disc image,
  saved game, signing key or temporary QA mod is included.

## Limits

This is emulator validation, not a physical ARM64 test of 0.22. Complete routes,
sustained handheld performance and complete FMV playback were not retested.
No performance improvement or widescreen correctness across every room is claimed.
Use an original USA SLUS-00067 image matching the recompilation; a region label
alone does not establish compatibility with modified images.
