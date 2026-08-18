using Il2CppGB.UI.Menu;
using UnityEngine;
using Il2CppFemur;
using MelonLoader;
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using MelonLoader.Utils;
using Il2CppGB.Game.Critters;
using static PengooinLabs.ReplayMod.Types;
using static MelonLoader.MelonLogger;

[assembly: MelonInfo(typeof(PengooinLabs.ReplayMod.Replay), "ReplayMod", "1.0", "PengooinLabs")]

namespace PengooinLabs.ReplayMod
{
    public class Replay : MelonMod
    {
        private static string _VERSION = "1.0";
        private static string helpString = "Help";
        public static string VERSION { get { return _VERSION; } }

        public static string PROJECT_PAGE_URL = "https://github.com/PengooinLabs/GangBeastsReplayMod";

        public static Recorder? recorder = null;
        public static Loader? loader = null;
        public static Player? player = null;
        public static ReplayCamera camera = new ReplayCamera();

        public static float fasterSpeedMultiplier = 5f;
        public static bool MANUALPLAY = false;
        public static RectOffset zeroRectOffset = new RectOffset(0, 0, 0, 0);

        public static float? _forceTimeScale = null;
        public static float? forceTimeScale { get { return _forceTimeScale; } }

        public static int labelFontSize = 16;
        
        public enum ModState { Idle, Recording, LoadingReplay, PlayingReplay }

        // current mod state
        public static ModState modState = ModState.Idle;
        
        public static List<string> replayFilenames = new();

        // transform cache supplying unique transform names
        public static TransformCache tc = new TransformCache();

        // configurable values

        private static MelonPreferences_Category cfg = MelonPreferences.CreateCategory("ReplayMod");

        // hide the UI when pressing f12 (= steam screenshot)
        public static MelonPreferences_Entry<bool> cfg_hideUiOnScreenShot = cfg.CreateEntry<bool>("hide-ui-on-screenshot", true);

        // whether menus are supposed to be shown

        public static bool wantMenuOpen(Menu menu)
        {
            return !wantMenusOpen.ContainsKey(menu) ? false : wantMenusOpen[menu];
        }

        public static MelonPreferences_Entry<bool> cfg_isFirstStart_menu = cfg.CreateEntry<bool>("is-first-start-menu", true);
        public static MelonPreferences_Entry<bool> cfg_isFirstStart_player = cfg.CreateEntry<bool>("is-first-start-player", true);

        // what matches to record
        public static MelonPreferences_Entry<bool> cfg_recordLocalGames = cfg.CreateEntry<bool>("record-local-games", true);
        public static MelonPreferences_Entry<bool> cfg_recordOnlineGames = cfg.CreateEntry<bool>("record-online-games", false);

        // whether to switch camera to nearest alive play if the focused player dies
        public static MelonPreferences_Entry<bool> cfg_switchCameraOnActorDeath = cfg.CreateEntry<bool>("switch-camera-on-player-death", true);

        // use 360° cam or not
        public static MelonPreferences_Entry<CameraMode> cfg_cameraMode = cfg.CreateEntry<CameraMode>("camera-mode", CameraMode.SURROUND);

        // overwrite active state of gameObjects each frame (not explorer friendly) or not (explorer friendly)
        public static MelonPreferences_Entry<bool> cfg_disableActiveStateEnforcement = cfg.CreateEntry<bool>("disable-active-state-enforcement", false);

        // hide the position and speed sliders? this is not saved
        public static bool cfg_hideReplayControls_Value = false;

        // disable left drag (for example for unity explorer)
        public static MelonPreferences_Entry<bool> cfg_disableRightDrag = cfg.CreateEntry<bool>("disable-right-mouse", false);
        public static MelonPreferences_Entry<bool> cfg_leftMouseAltMode = cfg.CreateEntry<bool>("left-click-and-wheel-alt-mode", false);
        public static MelonPreferences_Entry<bool> cfg_disableKeys = cfg.CreateEntry<bool>("disable-keys", false);

        // axis inversion options
        public static MelonPreferences_Entry<AXIS_INVERSION> cfg_invertGamepadAxes = cfg.CreateEntry<AXIS_INVERSION>("invert-gamepad-axes", AXIS_INVERSION.NO);
        public static MelonPreferences_Entry<AXIS_INVERSION> cfg_invertMouseAxes = cfg.CreateEntry<AXIS_INVERSION>("invert-mouse-x-axes", AXIS_INVERSION.NO);
        
        // camera zoom speed
        public static MelonPreferences_Entry<int> cfg_camDistSpeed = cfg.CreateEntry<int>("gamepad-camera-distance-speed", 20);

        // mouse rotation sensitivity
        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_x = cfg.CreateEntry<int>("mouse-sensitivity-x", 180);
        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_y = cfg.CreateEntry<int>("mouse-sensitivity-y", 125);

        public static MelonPreferences_Entry<int> cfg_mousewheel_zoom_sensitivity = cfg.CreateEntry<int>("mousewheel-zoom-sensitivity", 80);

        // how much to modify the playSpeed per notch of the mousewheel
        public static MelonPreferences_Entry<int> cfg_mousewheel_speed_step = cfg.CreateEntry<int>("mousewheel-speed-step", 10);
        
        // camera up/down rotation speed
        public static MelonPreferences_Entry<int> cfg_camUdSpeed = cfg.CreateEntry<int>("gamepad-camera-up-down-Speed", 90);
        public static MelonPreferences_Entry<int> cfg_camUdSpeed_min = cfg.CreateEntry<int>("gamepad-camera-up-down-speed-min", 20);
        public static MelonPreferences_Entry<int> cfg_camUdSpeed_max = cfg.CreateEntry<int>("gamepad-camera-up-down-speed-max", 180);

        // camera left/right rotation speed
        public static MelonPreferences_Entry<int> cfg_camLrSpeed = cfg.CreateEntry<int>("gamepad-camera-left-right-speed", 315);
        public static MelonPreferences_Entry<int> cfg_camLrSpeed_min = cfg.CreateEntry<int>("gamepad-camera-left-right-speed-min", 90);
        public static MelonPreferences_Entry<int> cfg_camLrSpeed_max = cfg.CreateEntry<int>("gamepad-camera-left-right-speed-max", 360);

        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_x_min = cfg.CreateEntry<int>("mouse-sensitivity-x-min", 10);
        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_x_max = cfg.CreateEntry<int>("mouse-sensitivity-x-max", 249);
        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_y_min = cfg.CreateEntry<int>("mouse-sensitivity-y-min", 10);
        public static MelonPreferences_Entry<int> cfg_mouse_sensitivity_y_max = cfg.CreateEntry<int>("mouse-sensitivity-y-max", 249);

        public static MelonPreferences_Entry<ReplayCamera.CameraTarget> cfg_camera_target = cfg.CreateEntry<ReplayCamera.CameraTarget>("camera-target", ReplayCamera.CameraTarget.Helper);

        public static MelonPreferences_Entry<string> cfg_replayFilesPath = cfg.CreateEntry<string>("replay-files-path", "");

        public static Types.RenderOn cfg_renderOn = RenderOn.Update;

        // static 
        public static Instance StaticLoggerInstance;
        public override void OnInitializeMelon()
        {
            Replay.StaticLoggerInstance = LoggerInstance;
            checkVersionTimeout = 10f;
            setModState(ModState.Idle);
        }

        public override void OnApplicationQuit()
        {
            stopRecording();
        }

        public static bool gotGameVersion = false;
        public static float checkVersionTimeout = -1f;
        public static bool abortMod = false;

        public static void checkVersion()
        {
            checkVersionTimeout -= Time.deltaTime;
            if (checkVersionTimeout <= 0)
            {
                Replay.logError("Failed to determine game version. Mod disabled.");
                abortMod = true;
                return;
            }
            
            try
            {
                var version = Game.getVersion();
                gotGameVersion = true;
                if (version.StartsWith("1.28.1687.0 "))
                {
                    init();
                }
                else
                {
                    Replay.logError("Unknown game version detected. Please wait for update.");
                }
            }
            catch { }
        }

        private static bool initialized = false;

        public static void init() {

            initialized = true;
            
            LoadingHooks.init();

            LoadingHooks.onShowLoadingScreen += () =>
            {
                // abort everything once loading screen shows
                tc.clear();
                stopRecording();
                stopReplay();
                excludeActorTransforms.Clear();
            };

            LoadingHooks.onLoadingScreenHidden += handle_LoadingScreenHidden;

            // prepare hash from list of actor transformkeys to exclude
            foreach (var key in excludeActorTransformsKeylist) excludeActorTransformKeysHash[key] = true;

            // initialize game component
            Game.init();
            
            Tools.onSubstFixedUpdate += substFixedUpdate;
        }

        public static void substFixedUpdate(float deltaTime)
        {
            // we run timers at fixed rate because in OnUpdate
            // deltaTime can be high after a long operation.
            // substituted fixedUpdate always calls with a
            // fixed interval of 1/30s

            Tools.tickTimers(deltaTime);

            if (modState == ModState.LoadingReplay)
            {
                if (nextSetupStep != null) nextSetupStep();
                return;
            }

            if (player != null && player.started) player.fixedUpdate();
        }

        private static void handle_LoadingScreenHidden(string mapName)
        {
            if (modState == ModState.LoadingReplay)
            {
                // we were loading a replay, setup the map now
                loader!.setupMapForReplay();
            }
            else
            {
                if (Game.sceneType == Game.SceneType.Fight)
                {
                    // regular game, maybe we'll record it

                    // check if it's a local or online game
                    bool isLocal = NetworkServer.active;
                    bool isOnline = !isLocal;

                    // determine if we're supposed to record it
                    bool doRecord = (
                        (isLocal && recordLocalGames()) ||
                        (isOnline && recordOnlineGames())
                    );

                    // start recording if conditions match
                    if (doRecord) Tools.setTimeout("startRecording", 0f, startRecording);
                }
                else
                {
                    // entered menu
                    if (cfg_isFirstStart_menu.Value)
                    {
                        cfg_isFirstStart_menu.Value = false;
                        Replay.toggleWantMenuOpen(Menu.MenuHelp, true);
                    }

                    if (loadReplayOnEnteringMenu != null)
                    {
                        var replay = loadReplayOnEnteringMenu;
                        loadReplayOnEnteringMenu = null;
                        loadReplay(replay);
                    }
                }
            }
        }

        // actor transforms with these keys don't require tracking:

        private static List<string> excludeActorTransformsKeylist = new() {
            "colliders/actor_spring_helper/actor_ball_proxy",
            "colliders/actor_spring_helper",
            "colliders/actor_leftHand_collider",
            "colliders/actor_rightHand_collider",
            "colliders/actor_head_collider/helper_voiceBox",
            "colliders/actor_rightForarm_collider/actor_rightHand_helper/actor_rightThumbBase_helper",
            "colliders/actor_rightForarm_collider/actor_rightHand_helper/actor_rightFingersBase_helper/actor_rightFingersTip_helper",
            "colliders/actor_rightForarm_collider/actor_rightHand_helper/actor_rightFingersBase_helper",
            "colliders/actor_rightForarm_collider/actor_rightHand_helper/actor_rightThumbBase_helper/actor_rightThumbTip_helper",
            "colliders/actor_rightForarm_collider/actor_rightHand_helper",
            "colliders/actor_leftForarm_collider/actor_leftHand_helper/actor_leftThumbBase_helper/actor_leftThumbTip_helper",
            "colliders/actor_leftForarm_collider/actor_leftHand_helper/actor_leftThumbBase_helper",
            "colliders/actor_leftForarm_collider/actor_leftHand_helper/actor_leftFingersBase_helper/actor_leftFingersTip_helper",
            "colliders/actor_leftForarm_collider/actor_leftHand_helper/actor_leftFingersBase_helper",
            "colliders/actor_leftForarm_collider/actor_leftHand_helper",
            "colliders/actor_leftFoot_helper",
            "colliders/actor_rightFoot_helper",
            "colliders/actor_ball_collider",
            "colliders/actor_rightThigh_collider/actor_rightLeg_offset",
            "colliders/actor_rightThigh_collider/actor_rightLeg_collider",
            "colliders/actor_leftThigh_collider/actor_leftLeg_offset",
            "colliders/actor_leftThigh_collider/actor_leftLeg_collider",
            "colliders/actor_stomach_collider",
            "colliders/actor_crotch_collider",
            "colliders",
        };

        private static Dictionary<string, bool> excludeActorTransformKeysHash = new();

        private static Dictionary<Transform, bool> excludeActorTransforms = new();

        public static List<Transform> getActorTransforms(Actor actor, bool firstFrame)
        {
            // we need everything below /colliders and the camera helper target
            var collidersRoot = Tools.getDirectChildTansform(actor.gameObject.transform, "colliders")!;
            List<Transform> list = collidersRoot.GetComponentsInChildren<Transform>().ToList();

            // filter out unneeded ones
            list = list.Where(t =>
            {
                if (!excludeActorTransforms.ContainsKey(t))
                {
                    var key = tc.getTransformInfo(t).key;
                    var idx = key.IndexOf('/');
                    key = key.Substring(idx + 1);
                    excludeActorTransforms[t] = excludeActorTransformKeysHash.ContainsKey(key);
                }
                return !excludeActorTransforms[t];
            }).ToList();

            list.Add(actor.bodyHandeler.CameraTarget.PartTransform);
            if (firstFrame) list.Add(actor.gameObject.transform); // needed for initial position
            return list;
        }

        public override void OnLateUpdate()
        {
            if (modState == ModState.Recording)
            {
                try
                {
                    recorder!.lateUpdate();
                }
                catch(Exception err)
                {
                    logError("Error during recording, stopping: " + err);
                    stopRecording();
                }
            }
            else if (modState == ModState.PlayingReplay)
            {
                // only replay in OnLateUpdate if the game is not paused,
                // or the pause screen will start jittering
                var play = !MANUALPLAY && cfg_renderOn == RenderOn.LateUpdate && !Game.paused();
                player!.lateUpdate(Time.unscaledDeltaTime, play);
            }
        }

        public static Action? nextSetupStep = null;

        public override void OnUpdate()
        {
            if (abortMod) return;
            if (!gotGameVersion)
            {
                checkVersion();
                return;
            }

            // must call this for subst_fixedUpdate to get called
            Tools.update();
            
            // run next setup step if present

            if (modState == ModState.LoadingReplay) return;

            if (modState == ModState.PlayingReplay)
            {
                var play = !MANUALPLAY && (Game.paused() || cfg_renderOn == RenderOn.Update);
                player!.update(Time.unscaledDeltaTime, play);
            }

            update_checkUserInput();
        }

        private static void update_checkUserInput()
        {
            if (Keyboard.current != null && !Replay.cfg_disableKeys.Value && Keyboard.current.f12Key.wasPressedThisFrame && cfg_hideUiOnScreenShot.Value)
            {
                hideGuiFor = 1f;
            }
        }

        public static float hideGuiFor = 0f;

        // start replay (gets called as last setup step)
        public static void replaySetup_createPlayer()
        {

            // create replay player and hand it the loaded data

            player = new Player(
                loader!.getFrameGroups(),
                loader.getActors(),
                loader.getActorNames(),
                loader.getBirds(),
                loader.getStaticItemStates()
            );

            loader.destroy();
            loader = null;
            player.patch();
            disableInterferences();
            nextSetupStep = null;

            if (cfg_cameraMode.Value == CameraMode.SURROUND)
            {
                Replay.camera.setFocusedActor(player.getActors()[0]);
            }
            
            // this hides the black screen
            setModState(ModState.PlayingReplay);
            
            // there is a weird problem which causes a late jump of unscaledDeltaTime by
            // a second or so (probably the duration of a previously running long running operation),
            // which is not in sync with the real time. cause unknown, we work around it by using
            // a short delay, since it occurs within the first 100-200ms (might be machine dependent)
            Tools.setTimeout("player.start()", 0.2f, () => { player!.start(); });
        }

        public static void abortLoading()
        {
            nextSetupStep = null;
            loader?.destroy();
            loader = null;
            setModState(ModState.Idle);
            returnToMenu();
        }

        public static void returnToMenu()
        {
            PauseManager.instance.OnReturnToMenu();
        }

        // stop replay
        public static void stopReplay()
        {
            if (modState != ModState.PlayingReplay) return;
            player!.stop();
            player = null;
            setTimeScale(null);
            setModState(ModState.Idle);
            currentReplayFilePath = "";
        }

        public static string getRecordingsPath()
        {
            string recordingsFolder = cfg_replayFilesPath.Value != "" ? cfg_replayFilesPath.Value : Path.Combine(MelonEnvironment.UserDataDirectory, "ReplayMod", "replays");
            Directory.CreateDirectory(recordingsFolder);
            return recordingsFolder;
        }

        private static void startRecording()
        {
            // safety check
            if (modState != ModState.Idle) return;

            // set new replay state
            setModState(ModState.Recording);

            // compose filename

            string isodate = DateTime.Now.ToString("yyyy-MM-dd__HH-mm-ss");

            string filePath = Path.Combine(
                getRecordingsPath(),
                isodate + "__" + (NetworkServer.active ? "local" : "online") + "-" + Game.getSceneName() + "-" + Actor._ActorCache.Count + "p.replay"
            );

            // create recorder and start recording to file
            recorder = new Recorder(filePath);
            recorder.start();

            // set replayFilesDirty flag so the files list will be updated next time it's displayed
            replayFilesDirty = true;
        }


        public static void logError(string text)
        {
            Replay.StaticLoggerInstance.Msg("error: " + text);
        }

        public static void setModState(ModState modState)
        {
            Replay.modState = modState;
            if (modState == ModState.Idle)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static void stopRecording()
        {
            if (modState != ModState.Recording) return; // safety
            recorder!.stop();
            recorder = null;
            setModState(ModState.Idle);
        }

        public static void setTimeScale(float? scale)
        {
            if (scale == null)
            {
                Time.timeScale = 1f;
                _forceTimeScale = null;
            }
            else
            {
                Time.timeScale = (float)scale;
                _forceTimeScale = scale;
            }
        }

        public static float guiPaddingRight = 10;
        public static int bottomBarHeight = 40;

        // styles
        private static GUIStyle? _labelStyle = null;
        private static GUIStyle? _boldLabelStyle = null;
        private static GUIStyle? _clickableLabelStyle = null;

        public static GUIStyle labelStyle
        {
            get
            {
                if (_labelStyle == null)
                {
                    _labelStyle = new GUIStyle();
                    _labelStyle.alignment = TextAnchor.MiddleCenter;
                    _labelStyle.fontSize = labelFontSize;
                    _labelStyle.normal.textColor = Color.white;
                }
                return _labelStyle;
            }
        }

        public static GUIStyle clickableLabelStyle
        {
            get
            {
                if (_clickableLabelStyle == null)
                {
                    _clickableLabelStyle = new GUIStyle(labelStyle);
                    _clickableLabelStyle.hover.textColor = Colors.clickableLabelColor;
                }
                return _clickableLabelStyle;
            }
        }

        public static GUIStyle boldLabelStyle
        {
            get
            {
                if (_boldLabelStyle == null)
                {
                    _boldLabelStyle = new GUIStyle(labelStyle);
                    _boldLabelStyle.fontStyle= FontStyle.Bold;
                }
                return _boldLabelStyle;
            }
        }

        private static bool replayFilesDirty = true;

        public override void OnGUI()
        {
            if (!initialized) return;
            if (Camera.main == null) return;

            if (hideGuiFor > 0)
            {
                hideGuiFor -= Time.unscaledDeltaTime;
                if (hideGuiFor <= 0) hideGuiFor = 0;
                return;
            }

            if (centerMessageText != null)
            {
                centerMessageTimeout -= Time.unscaledDeltaTime;
                if (centerMessageTimeout <= 0)
                {
                    centerMessageTimeout = 0;
                    centerMessageText = null;
                }
                else
                {
                    UI.drawCenteredText(centerMessageText);
                }
            }

            var screen = MenuState.Unknown;

            if (modState == ModState.LoadingReplay)
            {
                screen = MenuState.LoadingReplay;
                if (Game.isInLocalGameMenu())
                {
                    // menu = 1;
                    UI.drawCenteredText("Loading replay...");
                }
                else if (Game.sceneType == Game.SceneType.Fight)
                {
                    UI.blackBgCenteredMessage("Almost ready!");
                }
            }
            else if (modState == ModState.PlayingReplay)
            {
                if (wantMenuOpen(Menu.ReplayOptions)) Menus.drawReplayOptions();
                Replay.player?.onGui();
                screen = MenuState.PlayingReplay;
            }
            else if (modState == ModState.Idle)
            {
                if (Game.sceneType == Game.SceneType.Menu)
                {
                    screen = MenuState.Menu;

                    if (Game.isInLocalGameMenu())
                    {
                        if (loadReplayOnEnteringMenu == null && Replay.wantMenuOpen(Menu.MenuFileList))
                        {
                            if (replayFilesDirty)
                            {
                                replayFilesDirty = false;
                                readRecordedFilenames();
                            }
                            Menus.drawFileList(Menu.MenuFileList);
                        }
                    }

                    GUILayout.BeginArea(new Rect(0, Screen.height - bottomBarHeight, Screen.width, bottomBarHeight));
                    GUILayout.BeginHorizontal();

                    if (Game.isInOnlineGameMenu() || Game.isInLocalGameMenu())
                    {

                        UI.GUILayoutOutlineLabel("   ReplayMod " + Replay.VERSION, boldLabelStyle, Color.white, Color.black, 1, bottomBarHeight);

                        var recordingEnabled = (Game.isInLocalGameMenu() && recordLocalGames()) || (Game.isInOnlineGameMenu() && recordOnlineGames());
                        var matchType = Game.isInLocalGameMenu() ? "local" : "online";

                        UI.GUILayoutOutlineLabel("  |  ", labelStyle, Color.white, Color.black, 1, bottomBarHeight);

                        if (UI.GUILayoutOutlineButton("helpButton", Replay.helpString, clickableLabelStyle, Replay.wantMenuOpen(Menu.MenuHelp) ? Colors.burgerActiveColor : Color.white, Colors.burgerActiveColor, Color.black, 1, bottomBarHeight)) {
                            Replay.toggleWantMenuOpen(Menu.MenuHelp);
                        }

                        if (Game.isInLocalGameMenu())
                        {
                            UI.GUILayoutOutlineLabel("  |  ", labelStyle, Color.white, Color.black, 1, bottomBarHeight);
                            if (UI.GUILayoutOutlineButton("loadReplayButton", "Load replay", clickableLabelStyle, Replay.wantMenuOpen(Menu.MenuFileList) ? Colors.burgerActiveColor : Color.white, Colors.burgerActiveColor, Color.black, 1, bottomBarHeight))
                            {
                                replayFilesDirty = true;
                                toggleWantMenuOpen(Menu.MenuFileList);
                            }
                        }

                        UI.GUILayoutOutlineLabel("  |  ", labelStyle, Color.white, Color.black, 1, bottomBarHeight);
                        if (UI.GUILayoutOutlineButton("recordToggle", "Record " + matchType + " matches: ", labelStyle, Color.white, Colors.burgerActiveColor, Color.black, 1, bottomBarHeight))
                        {
                            // toggle recording respective game type
                            if (matchType == "local")
                            {
                                Replay.cfg_recordLocalGames.Value = !Replay.cfg_recordLocalGames.Value;
                            }
                            else
                            {
                                Replay.cfg_recordOnlineGames.Value = !Replay.cfg_recordOnlineGames.Value;
                            }
                        }

                        UI.GUILayoutOutlineLabel(recordingEnabled ? "Yes" : "No", labelStyle, recordingEnabled ? Color.green : Colors.noRed, Color.black, 1, bottomBarHeight);

                    }
                    else
                    {
                        UI.GUILayoutOutlineLabel("   ReplayMod " + Replay.VERSION, boldLabelStyle, Color.white, Color.black, 1, bottomBarHeight);
                        UI.GUILayoutOutlineLabel(" by PengooinLabs", labelStyle, Color.white, Color.black, 1, bottomBarHeight);
                        UI.GUILayoutOutlineLabel("  |  ", labelStyle, Color.white, Color.black, 1, bottomBarHeight);
                        if (UI.GUILayoutOutlineButton("showHelp", Replay.helpString, clickableLabelStyle, wantMenuOpen(Menu.MenuHelp) ? Colors.burgerActiveColor : Color.white, Colors.clickableLabelColor, Color.black, 1, bottomBarHeight)) Replay.toggleWantMenuOpen(Menu.MenuHelp);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndArea();
                }
            }

            if (screen == MenuState.Menu && Replay.wantMenuOpen(Menu.MenuHelp))
            {
                Menus.drawHelp(Menu.MenuHelp, "ReplayMod Help", menuHelpText, null, false);
            }
            else if (screen == MenuState.PlayingReplay)
            {
                if (Replay.wantMenuOpen(Menu.ReplayHelp))
                {
                    Menus.drawHelp(Menu.ReplayHelp, "Replay help", replayHelpText, null, true);
                }
                else if (Replay.wantMenuOpen(Menu.ReplayFilelist))
                {
                    Menus.drawFileList(Menu.ReplayFilelist);
                }
            }
        }

        private static string menuHelpText = String.Join('\n', new String[] {
            "- Go to Local or Online game menu and enable recording at the bottom of the screen.",
            "- Play as usual. The match will be recorded in the background.",
            "- Go to LOCAL Game menu, click 'Load replay' at the bottom of the screen to open the replay list.",
            "- Click on a replay to load.",
            "- Click '?' in the player UI for player help.",
            "- Note: Replay files are stored in " + getRecordingsPath()
        });

        private static string replayHelpText = String.Join('\n', new String[] {
            "# UI controls",
            "",
            "- Set playspeed or -position using the sliders at the bottom.",
            "- Click ▶ to toggle play/pause.",
            "- Click ☰ to configure controls and options.",
            "- Click 'Load' to toggle the replay filelist / load another replay.",
            "- Click bottom left corner button to hide/show player bar.",
            "",
            "# Mouse controls",
            "",
            "- Hold right mouse button and move mouse to rotate camera",
            "  + Optionally hold left mousebutton to lock rotating to left/right.",
            "- Use mousewheel to zoom in and out.",
            "- Left-click near or on a player to focus the player (also wave bots)",
            "- Hold left mouse button, then click right to activate time control," +
            "  then move mouse left/right to go back/forward in time.",
            "- Hold Control key and use mousewheel to regulate playspeed.",
            "",
            "# Keyboard controls",
            "",
            "- Press A/D to switch to the next player on the left/right.",
            "- Press W/S to increase/decrease play speed.",
            "- Press Space key to toggle play/pause.",
            "- Press C key to toggle camera targeting.",
            "",
            "# Gamepad controls",
            "",
            "- Use right analog stick to rotate camera.",
            "- Move left analog stick up/down to zoom in/out.",
            "- Hold left trigger (LT) and move left analog stick left/right to go slow motion (-1x..1x speed)",
            "  + Additionally hold right trigger (RT) to go faster (-5x..5x speed).",
            "- Press WEST button to toggle play/pause.",
            "- Press EAST button to toggle camera targeting.",
        });
        public static bool recordOnlineGames()
        {
            return cfg_recordOnlineGames.Value;
        }

        public static bool recordLocalGames()
        {
            return cfg_recordLocalGames.Value;
        }

        public static Dictionary<Menu, bool> wantMenusOpen = new();

        public static void toggleWantMenuOpen(Menu whatMenu, bool? toggle = null)
        {

            if (toggle == null)
            {
                wantMenusOpen[whatMenu] = !wantMenuOpen(whatMenu);
                toggle = wantMenusOpen[whatMenu];
            }
            else
            {
                wantMenusOpen[whatMenu] = (bool)toggle;
            }

            if ((bool)toggle)
            {
                // deactivate menus that would overlap otherwise

                if (whatMenu == Menu.ReplayOptions)
                {
                    toggleWantMenuOpen(Menu.ReplayHelp, false);
                    toggleWantMenuOpen(Menu.ReplayFilelist, false);
                }
                else if (whatMenu == Menu.ReplayHelp)
                {
                    toggleWantMenuOpen(Menu.ReplayOptions, false);
                    toggleWantMenuOpen(Menu.ReplayFilelist, false);
                }
                else if (whatMenu == Menu.ReplayFilelist)
                {
                    replayFilesDirty = true;
                    toggleWantMenuOpen(Menu.ReplayHelp, false);
                    toggleWantMenuOpen(Menu.ReplayOptions, false);
                }
                else if (whatMenu == Menu.MenuFileList)
                {
                    replayFilesDirty = true;
                    toggleWantMenuOpen(Menu.MenuHelp, false);
                }
                else if (whatMenu == Menu.MenuHelp)
                {
                    toggleWantMenuOpen(Menu.MenuFileList, false);
                }

            }
        }

        // find .replay files on disk
        private static void readRecordedFilenames()
        {
            // string mapName = Game.getSceneName();
            var absPaths = Directory.GetFiles(getRecordingsPath(), "*.replay").ToList();

            List<string> files = new();
            for (int i = 0; i < absPaths.Count; i++)
            {
                var filename = Path.GetFileName(absPaths[i]);
                files.Add(filename);
            }

            
            files.Sort(Tools.filenameSorter);
            replayFilenames = files;
        }

        private static string currentReplayFilePath = "";

        public static string lastLoadedReplay = "";
        private static string? loadReplayOnEnteringMenu = null;
        public static void loadReplay(string filename)
        {
            if (modState == ModState.PlayingReplay)
            {
                loadReplayOnEnteringMenu = filename;
                toggleWantMenuOpen(Menu.ReplayFilelist, false);
                stopReplay();
                returnToMenu();
                return;
            }

            if (modState != ModState.Idle) return;

            lastLoadedReplay = filename;
            setModState(ModState.LoadingReplay);

            // use Loader to parse replay file
            loader = new Loader();
            
            currentReplayFilePath = Path.Combine(getRecordingsPath(), filename);

            // give ui time to update, since it will block during load.
            // putting this in a coroutine didn't help for some reason

            Tools.setTimeout("continueLoading", 0f, () =>
            {
                var loadResult = loader.loadFromFile(currentReplayFilePath);

                if (loadResult == false)
                {
                    showCenterMessage("Failed to load replay", 4f);
                    currentReplayFilePath = "";
                    loader?.destroy();
                    loader = null;
                    setModState(ModState.Idle);
                    return;
                }

                // we'll continue when the next loading screen is hidden
                Game.startGame(loader.getMapName());
            });
        }

        public static List<BirdActor> indexBirds(bool playing)
        {
            var birds = Resources.FindObjectsOfTypeAll<BirdActor>().ToList();
            for (int i = 0; i < birds.Count; i++)
            {
                if (playing)
                {
                    // rename randomly-named bird objects so they match the keys in the recording
                    birds[i].gameObject.name = "Bird#" + i;
                }
                else
                {
                    // record birds with keys Bird#N
                    tc.setFixedSingleTransformKey(birds[i].gameObject.transform, "Bird#" + i);
                }
            }
            birds.Sort(Tools.birdSorter);
            return birds;
        }

        public static void setTransformState(Transform transform, ItemState state, bool forceState)
        {
            var key = tc.getTransformInfo(transform).key;
            
            transform.position = state.pos;
            transform.localScale = state.lscale;
            transform.rotation = Quaternion.Euler(state.rot);

            // force active state only if explorerFriendly flag is not set
            if (state.active != transform.gameObject.activeSelf)
            {
                if (forceState || !Replay.cfg_disableActiveStateEnforcement.Value)
                {
                    transform.gameObject.SetActive(state.active);
                }
            }
        }

        public static bool isReplayActive()
        {
            return modState == ModState.PlayingReplay;
        }


        private static Dictionary<string, bool> badAnimators = new() {
            { "Crane", true },
            { "crane@rig", true }
        };

        public static void disableInterferences()
        {
            // this is needed on crane!
            var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var animator in animators)
            {
                if (badAnimators.ContainsKey(animator.name))
                {
                    UnityEngine.Object.Destroy(animator);
                }
            }

            // actor colliders are getting enabled again for some reason,
            // same for other objects over time. actor collision handling
            // is NOPed, unknown how to keep all colliders disabled.
            disableColliders();

            var rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rigidbody in rigidbodies)
            {
                if (!rigidbody.isKinematic || rigidbody.useGravity)
                {
                    rigidbody.isKinematic = true;
                    rigidbody.useGravity = false;
                }
            }

        }

        public static int disableColliders()
        {
            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }
            return colliders.Count;
        }

        public static void toggleCameraTargeting()
        {
            cfg_camera_target.Value = Tools.cycleOption<ReplayCamera.CameraTarget>(new() {
                ReplayCamera.CameraTarget.Helper,
                ReplayCamera.CameraTarget.Chest,
                ReplayCamera.CameraTarget.Head
            }, cfg_camera_target.Value);
        }

        public static string? centerMessageText = null;
        public static float centerMessageTimeout = 0f;
        public static void showCenterMessage(string text, float timeout)
        {
            centerMessageText = text;
            centerMessageTimeout = timeout;
        }

    }
}
