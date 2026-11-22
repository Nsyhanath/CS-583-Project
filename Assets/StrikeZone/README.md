# Strike Zone – Title Screen (Unity 6)

## Set up (one time)
1. In the Unity menu bar choose **Strike Zone > Build Title Screen Scene**.
2. If Unity asks to import **TMP Essential Resources**, click **Import**. The scene builds once the import finishes.
   (If nothing happens after the import, run the menu item again.)
3. Open `Assets/Scenes/TitleScreen.unity` (it opens automatically) and press **Play**.

Running the menu item again rebuilds the scene from scratch, so any edits you made to that scene are lost.

## What's in here
| Path | What it is |
|---|---|
| `Resources/StrikeZoneBackground.png` | The bowling-alley background (3840×2160) |
| `Scripts/TitleScreenController.cs` | Menu logic, keyboard/gamepad navigation, toast messages |
| `Scripts/MenuButton.cs` | Per-button settings: message, scene to load, hover/selected look |
| `Scripts/RollingBall.cs` | Rolls the ball down the middle lane in perspective |
| `Scripts/TitleBob.cs`, `Scripts/Blink.cs` | Title bobbing and "Press Enter" blinking |
| `Editor/StrikeZoneSetup.cs` | The menu item that builds the scene and sprites |

## Hooking up your game
Select a menu button (for example **Title Screen > Menu > PLAY**) and, in its **Menu Button** component,
set **Scene To Load** to your gameplay scene's name. Add that scene to **File > Build Profiles > Scene List**.
Until then, the button shows its **Message** instead.

## Optional: the chunky title font
The web version uses the free Google font **Bungee**. To match it, download Bungee from Google Fonts,
drop the `.ttf` into Unity, right-click it, and choose **Create > TextMeshPro > Font Asset**. Then assign
that font to the `Strike` and `Zone` texts.
