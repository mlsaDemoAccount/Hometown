# Hometown reference screens

Open `SixScreensPreview.unity` and press Play. The bottom row switches between all six screens. Upload opens New Memory, Save returns to the memory list, and View Diary opens the diary. The preview is separate from the existing game scenes.

## In your game

Drag `Prefabs/SixScreensUI.prefab` into a scene with an EventSystem. Its six child canvases begin inactive. Activate the desired child to show it; call `ReferenceScreen.Close()` for the animated exit. The linked suite has all destination references populated. The individual screen prefabs are also available; assign their Destinations array in enum order if using them separately.

Each prefab uses its supplied 1920 x 1080 reference screen as the exact visible artwork. The artwork scales within the safe area without stretching. Interactive buttons, navigation areas, text input, and audio sliders are mapped over the matching positions. This preserves the precise appearance of the approved designs; changing visible text or individual artwork requires updating the corresponding image in `ReferenceScreens`.

## Behavior

- Opening and closing fades/scales use unscaled time, so menus animate while the game is paused.
- Buttons animate on hover/press and play the existing click audio. Character and reward elements have idle motion.
- Layout scales to fit the screen safe area, including tablet and ultrawide displays. Portrait retains the full landscape composition at a smaller size; it does not reflow into a portrait form.
- Audio sliders control master volume and the existing SoundManager music/SFX settings. Values persist in PlayerPrefs.
- Memory descriptions are validated and the latest description is saved locally. This is a UI sample store, not a complete photo diary database.
- `onCaptureRequested` and `onUploadRequested` must be connected to the game's platform photo service. Return a sprite through `SetPhoto`. Photos are not uploaded or persisted by this UI package.
- Wire `onAccept` to demolition of the selected building and `onCollect` to the reward system. These UI events intentionally do not delete buildings or grant economy rewards themselves.
- Wire `onHome` and `onCategories` to the game's existing navigation. Settings and memory navigation are linked in the suite.

`Validation.txt` records the completed missing-script, button-binding, and audio-reference checks. Runtime interaction and visual checks should also be performed on the target devices.
