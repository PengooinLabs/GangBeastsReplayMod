# Gang Beasts Replay Mod 

This mod adds **MATCH REPLAY** to [Gang Beasts](https://gangbeasts.game)!

Replay any match inside the game! Skip back and forward in time! Watch your greatest stunts in slow motion, action-movie style, using 360° camera! Create awesome screenshots and videos! Find out why the trawler exploded!

# Features

- **Camera control**: 360°/zoom camera in replay mode
- **Time control**: Go forward and back in time and playback at different speeds

# Requirements

- Disk space for the saved replay files: Depends on how much was going on on the map. Usually in the range of a few dozen MB, ~ 200MB max for a full match. Bottom line, a fraction of what a video file would require. Check help in the menu to see where replay files are stored (by default `UserData\ReplayMod\replays`). You can change the path after first running the game by editing the `[ReplayMod]`/`replay-files-path` entry in `UserData\MelonPreferences.cfg`.
- RAM: Consumption when loading a replay file varies depending on the size of the loaded data. 2GB spare RAM should do the trick.

# Installation

Note: This was developed for and has only been tested on Windows / Steam version, Gang Beasts v1.28!
 
- Download and install [MelonLoader](https://melonwiki.xyz) (developed on v0.7.3 beta)
- Download **ReplayMod.dll** from the [releases section](https://github.com/PengooinLabs/GangBeastsReplayMod/releases) and copy it to the `Gang Beasts/Mods` folder

# Usage

- Start the game.
- Go to Local or Online game menu and enable recording at the bottom of the screen.
- Play as usual. The match will be recorded in the background.
- Go to LOCAL Game menu, click 'Load replay' at the bottom of the screen to open the replay list.
- Click on a replay to load it and wait for the map to load and setup.
- Click '?' in the player UI for player help.

# Replay Controls

- UI Controls
  - Set playspeed or -position using the sliders at the bottom.
  - Click ▶ to toggle play/pause.
  - Click ☰ to configure controls and options.
  - Click 'Load' to toggle the replay filelist / load another replay.
  - Click the bottom left corner button to hide/show the player bar.

- Mouse controls
  - Hold right mouse button and move mouse to rotate camera, optionally hold left mousebutton to lock rotation to left/right
  - Use mousewheel to zoom in and out
  - Left-click near or on a player to focus the player (also wave bots)
  - Hold left mouse button, then click right to activate time control, then move mouse left/right to go back/forward in time
  - Hold Control key and use mousewheel to regulate playspeed.

- Keyboard controls
  - Press A/D to switch to the next player on the left/right.
  - Press W/S to increase/decrease play speed.
  - Press Space key to toggle play/pause.
  - Press C key to toggle camera targeting (see below)

- Gamepad controls
  - Use right analog stick to rotate camera
  - Move left analog stick up/down to zoom in/out
  - Press L/R keys to to switch to the next player on the left/right.
  - Hold left trigger (LT) and move left analog stick left/right to go slow motion (-1x..1x speed)
    + Additionally hold right trigger (RT) to go faster (-5x..5x speed).
  - Press WEST button to toggle play/pause.
  - Press EAST button to toggle camera targeting.

# Replay options

- **Speed modifier step**: By how much the playspeed/slider is modified when using mousewheel or W/S keys to modify playspeed
- **Hide UI when screenshotting via F12** : For clean Steam screenshots
- **Camera mode**: `360°` is the default replay camera. Right now the only other option is `Disabled`, which allows another mod to control the camera.
- **Camera targeting**: `Smooth` is best for videos, producing a smooth motion, but lagging behind the player during fast movements. `Head` and `Chest` will center the respective body part exactly in the screen center (good for close screenshots during fast movement).
- **Switch camera when player dies**: When set to `Yes`, the camera will automatically switch to the next closest player if the focused player dies. If you move forward and back in time, this might be undesired, so you can turn it off here.
- **Invert mouse rotation axes**: Mouse axis inversion when controlling the camera with right drag (`None`,`X-Axis`,`Y-Axis`,`Both`)
- **Mouse rotation sensitivity**: Mouse sensitivity when controlling the camera with right drag
- **Mousewheel zoom sensitivity**: How fast the camera will move closer/further away when using mousewheel
- **Invert gamepad rotation axes**: Axis inversion for right analog stick (`None`,`X-Axis`,`Y-Axis`,`Both`)
- **Gamepad left/right max rotation**: How fast the camera rotates left/right when using right analog stick
- **Gamepad left/right max rotation**: How fast the camera rotates up/down when using right analog stick
- **Gamepad max zoom speed**: How fast the camera will move closer/further away when using left analog stick

## Unity Explorer compatiblity options

- If you don't know what this means, leave everything at `No`
- **Disable active-state enforcement**: If set to `Yes`, the active state of GameObjects is not overwritten each frame, allowing toggling them on or off in Unity Explorer. It comes with side effects such as wave actors not showing up until you set it to `No` again briefly.
- **Disable right click/drag**: If it interferes with something, set to `Yes` to disable right mouse button handling.
- **Left mouse/wheel alt mode**: If set to `Yes`, you have to hold Ctrl key when using actions that activate using left mousebutton, and mousewheel will only zoom when in camera rotation mode.
- **Disable keyboard key handling**: If set to `Yes`, only Ctrl key will be handled, disabling Space for pause etc.

# Completion status

- You get the fights without the meta, i.e. there are no screen messages, balloon screens, player names
- Minor things like particle effects aren't showing up, some background animations are disabled
- There might be some sound issues with longer sound bites
- Possibly further yet undiscovered issues

# Building

- `apt install dotnet-sdk-8.0` or whatever your distribution command is for a net6 compatible setup
- `git clone https://github.com/PengooinLabs/GangBeastsReplayMod.git`
- Link `Il2CppAssemblies` and `net6` folders from Melonloader inside the folder or edit the paths in `ReplayMod.csproj` manually
- `dotnet build -c release` -> dll is at `bin/release/net6.0/ReplayMod.dll`

# Epilogue

We hope you have a lot of fun with this one and are looking forward to a new generation of Gang Beasts content!

GLHF, but use at your own risk!
