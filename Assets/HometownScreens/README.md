# Hometown reference screens

Open `SixScreensPreview.unity` and press Play. The bottom row switches between all six screens. Upload opens New Memory, Save returns to the memory list, and View Diary opens the diary. The preview is separate from the existing game scenes.

## In your game

Drag `Prefabs/SixScreensUI.prefab` into a scene with an EventSystem. Its six child canvases begin inactive. Activate the desired child to show it; call `ReferenceScreen.Close()` for the animated exit. The linked suite has all destination references populated. The individual screen prefabs are also available; assign their Destinations array in enum order if using them separately.

Each prefab contains editable UI objects rather than a full-screen reference image. The shared TopPanel and RightPanel are copied from `Assets/Scenes/Gameplay.unity` (including its gameplay-prefab overrides and added HUD elements), preserving their sprite assignments, child layouts, Image settings, and TMP text. Screen-specific icons and decorative buttons use Simple Images with Preserve Aspect; resizable panels use the bordered RoundedPanel sprite as Sliced Images. Photos keep their source aspect ratio. Titles, captions, button labels, and the description field use TextMeshPro.

The CanvasScaler follows Gameplay.unity (Scale With Screen Size, 1080 x 1920, width match). A separate safe-area design root fits the 1600 x 900 landscape composition uniformly. The animation root is separate from the fitting root, so opening animations cannot override aspect fitting. These are overlay prefabs: the game scene remains visible behind the dimmer, and the standalone preview uses its scene background. ReferenceScreens contains design references only and is not rendered by these prefabs.

Audio sliders have real tracks, linked fill RectTransforms, and live percentage labels. Memory text, photo previews, and level labels are connected to ReferenceScreen instead of being baked into artwork. The sample memory images use the existing family-photo sprite in UiImages/Elements.

`Tools > Hometown > Validate and Render Six Screen UI` checks sprite modes and button/field bindings, then renders every prefab at 16:9, 4:3, and 21:9 into `Temp/HometownUIReview`. `Tools > Hometown > Rebuild Six Screen UI` recreates the six prefab layouts; it overwrites manual child-layout edits, so use it only when intentionally regenerating the screens. It preserves root/controller identities used by SixScreensUI and the preview scene.

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
