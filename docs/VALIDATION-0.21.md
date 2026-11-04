# DUAL 0.21 validation

Equipment, inventory and relics now support live consultation on the lower screen.
Selecting an item or relic does not open a native menu. Only Equip, Enable Relic
or Disable Relic requests a paused native review. Confirmation uses the game's
own menu; confirmation or cancellation closes only a pause opened by that request.
A manual START pause stays under the player's control.

Verified in an Android 15 x64 emulator with a 1280x720 primary display and a
960x720 secondary Presentation, using a prepared Alucard test inventory:

- The game clock advances during browsing and selection and stops during review.
- Weapon and armor changes apply through the original menu and resume gameplay.
- A relic toggle applies through the native menu and resumes; cancelling leaves
  the active state unchanged. The current lower-screen tab is retained.
- Cancelling an equipment review leaves equipment unchanged.
- Manual pause ownership, secondary-display removal/reconnection and leaving
  for Android Home during review do not cause unintended item or relic changes.
- Deterministic regressions cover double confirmation, unavailable items,
  delayed opening, relic-state changes, native rejection, scene/prologue guards,
  cancellation before navigation and close retries.
- Both signed Android APKs match all ten checked companion source files in their
  build metadata. ABI and archive integrity checks pass. No disc, save, private
  signing key or temporary QA tool is packaged. Memory-card hashes did not change.

The installation manuals describe a fresh setup and use the current English menu
labels. Both language editions have six visually reviewed pages and contain no
credits, author metadata or external links.

Physical ARM64 handheld validation and complete game routes remain unverified.
Existing audio/CD reads outside the data track were observed; this change makes
no performance claim. The original native menu closing fade remains in place.
