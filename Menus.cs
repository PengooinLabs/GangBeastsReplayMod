using UnityEngine;
using static PengooinLabs.ReplayMod.Replay;
using static PengooinLabs.ReplayMod.Types;
using static PengooinLabs.ReplayMod.UI;

namespace PengooinLabs.ReplayMod
{
    public class Menus
    {
        private static int lastReplayCount = 0;
        public static int pageIndex = -1;
        public static Rect replayOptionsMenuRect = new Rect();
        
        private static GUIStyle? _projectLinkStyle = null;
        public static GUIStyle projectLinkStyle
        {
            get
            {
                if (_projectLinkStyle == null)
                {
                    _projectLinkStyle = new GUIStyle(GUI.skin.button);
                    _projectLinkStyle.fontSize = Vars.projectLinkFontSize;
                    _projectLinkStyle.normal.textColor = Colors.projectLinkTextColor;
                    _projectLinkStyle.hover.textColor = Colors.projectLinkHoverColor;
                    _projectLinkStyle.alignment = TextAnchor.MiddleCenter;
                    _projectLinkStyle.padding = new RectOffset(10, 10, 10, 10);
                    _projectLinkStyle.normal.background = null;
                    _projectLinkStyle.hover.background = null;
                    _projectLinkStyle.active.background = null;
                }

                if (UI.isRepaintStep())
                {
                    _projectLinkStyle.normal.background ??= UI.getTexture2D(Colors.transparent);
                    _projectLinkStyle.hover.background ??= UI.getTexture2D(Colors.transparent);
                }

                return _projectLinkStyle;
            }
        }

        public static void drawReplayOptions()
        {
            // means replay-options are visible

            var menuArea = new Rect(
                Vars.menuOffsetLeft,
                Screen.height - bottomBarHeight - replayOptionsMenuRect.height - Vars.menuOffsetBottom,
                replayOptionsMenuRect.width,
                replayOptionsMenuRect.height
            );

            GUILayout.BeginArea(menuArea);
            GUILayout.BeginVertical(backgroundStyle, GUILayout.Width(Vars.menuWidth));

            // --------------
            // replay options
            // --------------

            UI.section("Replay options");

            var perNotch = (int)UI.SliderOption("Mousewheel speed step (ct)", Replay.cfg_mousewheel_speed_step.Value, 5, 20, 0);
            if (perNotch != Replay.cfg_mousewheel_speed_step.Value) Replay.cfg_mousewheel_speed_step.Value = (int)perNotch;

            if (UI.Option("Hide UI when screenshotting via F12", Replay.cfg_hideUiOnScreenShot.Value ? "Yes" : "No"))
            {
                Replay.cfg_hideUiOnScreenShot.Value = !Replay.cfg_hideUiOnScreenShot.Value;
            }
            var cameraModeString = Replay.cfg_cameraMode.Value == CameraMode.SURROUND ? "360°" : "Disabled";

            if (UI.Option("Camera mode:", cameraModeString))
            {
                cfg_cameraMode.Value = Tools.cycleOption<CameraMode>(new() { CameraMode.SURROUND, CameraMode.DISABLED }, cfg_cameraMode.Value);
            }

            if (Replay.cfg_cameraMode.Value == CameraMode.SURROUND)
            {
                var cameraTargetStr =
                    cfg_camera_target.Value == ReplayCamera.CameraTarget.Chest ? "Chest" :
                    cfg_camera_target.Value == ReplayCamera.CameraTarget.Head ? "Head" : "Smooth";

                if (UI.Option("Camera targeting:", cameraTargetStr))
                {
                    Replay.toggleCameraTargeting();
                }

                if (UI.Option("Switch camera target when player dies:", Replay.cfg_switchCameraOnActorDeath.Value ? "Yes" : "No"))
                {
                    Replay.cfg_switchCameraOnActorDeath.Value = !Replay.cfg_switchCameraOnActorDeath.Value;
                }

                // -------------
                // mouse options
                // -------------

                var mouseInvStr = cfg_invertMouseAxes.Value == AXIS_INVERSION.NO ? "None" :
                    cfg_invertMouseAxes.Value == AXIS_INVERSION.X ? "X-Axis" :
                    cfg_invertMouseAxes.Value == AXIS_INVERSION.Y ? "Y-Axis" : "Both";

                if (UI.Option("Invert mouse rotation axes:", mouseInvStr))
                {
                    cfg_invertMouseAxes.Value = Tools.cycleOption<AXIS_INVERSION>(new() {
                        AXIS_INVERSION.NO,
                        AXIS_INVERSION.X,
                        AXIS_INVERSION.Y,
                        AXIS_INVERSION.XY
                    }, cfg_invertMouseAxes.Value);
                }

                var mouseSensitivityX = (int)UI.SliderOption("Mouse rotation sensitivity (X)", Replay.cfg_mouse_sensitivity_x.Value, Replay.cfg_mouse_sensitivity_x_min.Value, Replay.cfg_mouse_sensitivity_x_max.Value, 0);
                if (mouseSensitivityX != Replay.cfg_mouse_sensitivity_x.Value) Replay.cfg_mouse_sensitivity_x.Value = mouseSensitivityX;

                var mouseSensitivityY = (int)UI.SliderOption("Mouse rotation sensitivity (Y)", Replay.cfg_mouse_sensitivity_y.Value, Replay.cfg_mouse_sensitivity_y_min.Value, Replay.cfg_mouse_sensitivity_y_max.Value, 0);
                if (mouseSensitivityX != Replay.cfg_mouse_sensitivity_y.Value) Replay.cfg_mouse_sensitivity_y.Value = mouseSensitivityY;

                var mouseSensitivityWheel = (int)UI.SliderOption("Mousewheel zoom sensitivity", Replay.cfg_mousewheel_zoom_sensitivity.Value, 20, 140, 0);
                if (mouseSensitivityWheel != Replay.cfg_mousewheel_zoom_sensitivity.Value) Replay.cfg_mousewheel_zoom_sensitivity.Value = mouseSensitivityWheel;

                // ---------------
                // gamepad options
                // ---------------

                var gamepadInvStr = cfg_invertGamepadAxes.Value == AXIS_INVERSION.NO ? "None" :
                    cfg_invertGamepadAxes.Value == AXIS_INVERSION.X ? "X-Axis" :
                    cfg_invertGamepadAxes.Value == AXIS_INVERSION.Y ? "Y-Axis" : "Both";

                if (UI.Option("Invert gamepad rotation axes:", gamepadInvStr))
                {
                    cfg_invertGamepadAxes.Value = Tools.cycleOption(new()
                    {
                        AXIS_INVERSION.NO,
                        AXIS_INVERSION.X,
                        AXIS_INVERSION.Y,
                        AXIS_INVERSION.XY,
                    }, cfg_invertGamepadAxes.Value);
                }

                var camLr = (int)UI.SliderOption("Gamepad left/right max rotation (°/s)", Replay.cfg_camLrSpeed.Value, Replay.cfg_camLrSpeed_min.Value, Replay.cfg_camLrSpeed_max.Value, 0);
                if (camLr != Replay.cfg_camLrSpeed.Value) Replay.cfg_camLrSpeed.Value = camLr;

                var camUd = (int)UI.SliderOption("Gamepad up/down max rotation (°/s)", Replay.cfg_camUdSpeed.Value, Replay.cfg_camUdSpeed_min.Value, Replay.cfg_camUdSpeed_max.Value, 0);
                if (camUd != Replay.cfg_camUdSpeed.Value) Replay.cfg_camUdSpeed.Value = camUd;

                var camDist = (int)UI.SliderOption("Gamepad max zoom speed (m/s)", Replay.cfg_camDistSpeed.Value, 5, 70, 0);
                if (camDist != Replay.cfg_camDistSpeed.Value) Replay.cfg_camDistSpeed.Value = camDist;
            }

            UI.section("Unity Explorer compatibility options");
            
            if (UI.Option("Disable active-state enforcement:", Replay.cfg_disableActiveStateEnforcement.Value ? "Yes" : "No"))
            {
                Replay.cfg_disableActiveStateEnforcement.Value = !Replay.cfg_disableActiveStateEnforcement.Value;
            }

            var disableRightDragStr = Replay.cfg_disableRightDrag.Value ? "Yes" : "No";
            if (UI.Option("Disable right click/drag:", disableRightDragStr))
            {
                Replay.cfg_disableRightDrag.Value = !Replay.cfg_disableRightDrag.Value;
            }

            var leftMouseAltMode = Replay.cfg_leftMouseAltMode.Value ? "Yes" : "No";
            if (UI.Option("Left mouse/wheel alt mode (hold ctrl/only when rotating):", leftMouseAltMode))
            {
                Replay.cfg_leftMouseAltMode.Value = !Replay.cfg_leftMouseAltMode.Value;
            }

            var disableKeys = Replay.cfg_disableKeys.Value ? "Yes" : "No";
            if (UI.Option("Disable keyboard key handling (except Ctrl key):", disableKeys))
            {
                Replay.cfg_disableKeys.Value = !Replay.cfg_disableKeys.Value;
            }


            UI.projectLink("Visit project page for more info");

            if (UI.closeButton()) toggleWantMenuOpen(Menu.ReplayOptions, false);

            GUILayout.EndVertical();

            // save menu rect to exclude the area from click/drag
            if (UI.isRepaintStep())
            {
                var mRect = Tools.getLastGuiRect();
                if (mRect != null) replayOptionsMenuRect = (Rect)mRect;
            }

            GUILayout.EndArea();
        }

        private static float replayItemHeight = 0;
        private static float navButtonHeight = 0;
        private static float closeButtonHeight = 0;

        public static Rect fileListRect = new Rect();
        public static void drawFileList(Menu menu)
        {
            if (replayItemHeight == 0) replayItemHeight = Tools.getStyleLineHeight(UI.replayItemStyle) + 4;
            if (navButtonHeight == 0) navButtonHeight = Tools.getStyleLineHeight(UI.navButtonStyle) + 16;
            if (closeButtonHeight == 0) closeButtonHeight = Tools.getStyleLineHeight(UI.closeButtonStyle) + 20;

            if (Replay.modState != ModState.PlayingReplay && Game.isLobbyCountdownActive()) return;

            // means we're showing the replay file list
            float _availableHeight = Screen.height - bottomBarHeight - Vars.menuOffsetTop - Vars.menuOffsetBottom;
            float availableHeight = _availableHeight;
            availableHeight -= closeButtonHeight;
            
            var menuArea = new Rect(
                Vars.menuOffsetLeft,
                Screen.height - bottomBarHeight - fileListRect.height - Vars.menuOffsetBottom,
                Vars.menuWidth,
                fileListRect.height
            );
            
            GUILayout.BeginArea(menuArea);
            GUILayout.BeginVertical(backgroundStyle, GUILayout.Width(Vars.menuWidth));

            UI.section("Replay files");
            availableHeight -= UI.sectionHeight;

            if (Replay.modState != ModState.PlayingReplay && !Game.isSinglePlayerSelected())
            {
                GUILayout.Label("Select a single player to show recorded matches", UI.hintStyle);
                if (UI.closeButton()) toggleWantMenuOpen(menu, false);
            }
            else if (Replay.replayFilenames.Count == 0)
            {
                GUILayout.Label("There are no recorded matches yet.", UI.hintStyle);
                if (UI.closeButton()) toggleWantMenuOpen(menu, false);
            }
            else
            {
                // go to last page if amount of replays has changed, i.e.
                // we recorded new ones
                if (Replay.replayFilenames.Count != lastReplayCount)
                {
                    lastReplayCount = Replay.replayFilenames.Count;
                    pageIndex = -1;
                }

                // draw file items
                
                // how many fit on screen
                var drawNavButtons = false;

                if (Replay.replayFilenames.Count * replayItemHeight > availableHeight)
                {
                    // they don't all fit on a single page, we need prev/next buttons
                    drawNavButtons = true;
                    availableHeight -= navButtonHeight;
                }

                int pageSize = ((int)(availableHeight / replayItemHeight));
                int pageCount = (int)Math.Ceiling((double)((float)Replay.replayFilenames.Count / (float)pageSize));

                if (pageIndex == -1)
                {
                    pageIndex = pageCount-1;
                }
                else if (pageIndex > pageCount-1)
                {
                    pageIndex = pageCount-1;
                }
                else if (pageIndex < 0)
                {
                    pageIndex = 0;
                }

                var start = Replay.replayFilenames.Count - ((pageCount - pageIndex) * pageSize);
                var end = start + pageSize;
                if (start < 0) start = 0;

                bool gotOne = false;

                for (int i = start; i < end && i < Replay.replayFilenames.Count; i++)
                {
                    gotOne = true;

                    if (Replay.replayFilenames[i] == Replay.lastLoadedReplay)
                    {
                        replayItemStyle.normal.textColor = Color.white;
                    }
                    else
                    {
                        replayItemStyle.normal.textColor = Color.gray;
                    }
                    if (GUILayout.Button(Replay.replayFilenames[i], UI.replayItemStyle, GUILayout.Height(replayItemHeight)))
                    {
                        var replayName = Replay.replayFilenames[i];
                        Tools.setTimeout("loadReplay", 0, () => { Replay.loadReplay(replayName); });
                        return;
                    }
                    
                    availableHeight -= replayItemHeight;
                }

                if (!gotOne && Replay.replayFilenames.Count > 0) pageIndex--;

                if (drawNavButtons)
                {
                    GUILayout.BeginHorizontal();

                    if (pageIndex == 0) {
                        navButtonStyle.normal.textColor = Colors.transparent;
                        navButtonStyle.hover.textColor = Colors.transparent;
                    }
                    else
                    {
                        navButtonStyle.normal.textColor = Color.gray;
                        navButtonStyle.hover.textColor = Colors.gold;
                    }

                    if (GUILayout.Button("◀ Previous page", navButtonStyle, GUILayout.Height(navButtonHeight)) && pageIndex > 0)
                    {
                        pageIndex--;
                    }
                    bool canGoNext = pageIndex < pageCount - 1;

                    if (canGoNext)
                    {
                        navButtonStyle.normal.textColor = Color.gray;
                        navButtonStyle.hover.textColor = Colors.gold;
                    }
                    else
                    {
                        navButtonStyle.normal.textColor = Colors.transparent;
                        navButtonStyle.hover.textColor = Colors.transparent;
                    }

                    if (GUILayout.Button("Next page ▶", navButtonStyle, GUILayout.Height(navButtonHeight)) && canGoNext)
                    {
                        pageIndex++;
                    }
                    GUILayout.EndHorizontal();
                }

                if (UI.closeButton()) toggleWantMenuOpen(menu, false);
            }

            GUILayout.EndVertical();

            if (UI.isRepaintStep())
            {
                var mRect = Tools.getLastGuiRect();
                if (mRect != null) fileListRect = (Rect)mRect;
            }

            GUILayout.EndArea();
        }

        private static GUIStyle? _helpTextStyle = null;
        public static GUIStyle helpTextStyle
        {
            get
            {
                if (_helpTextStyle == null)
                {
                    _helpTextStyle = new GUIStyle();
                    _helpTextStyle.normal.textColor = Color.gray; //  Color.white;
                    _helpTextStyle.fontSize = Vars.menuFontSize;
                    _helpTextStyle.wordWrap = true;
                    _helpTextStyle.padding = new RectOffset(10, 10, 10, 10);
                }
                return _helpTextStyle;
            }
        }

        public static Rect helpRect = new Rect();
        public static void drawHelp(Menu whatMenu, string title, string helpText, string? projectLinkText, bool withBottomOffset)
        {
            
            var menuArea = new Rect(
                Vars.menuOffsetLeft,
                Screen.height - bottomBarHeight - helpRect.height - (withBottomOffset ? Vars.menuOffsetBottom : 0),
                Screen.width - Vars.menuOffsetLeft*2,
                helpRect.height
            );

            GUILayout.BeginArea(menuArea);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(backgroundStyle, GUILayout.ExpandWidth(false));
            UI.section(title);
            GUILayout.Label(helpText, helpTextStyle);

            if (projectLinkText != null) UI.projectLink(projectLinkText);
            if (UI.closeButton()) toggleWantMenuOpen(whatMenu, false);
            
            GUILayout.EndVertical();

            if (UI.isRepaintStep())
            {
                var mRect = Tools.getLastGuiRect();
                if (mRect != null) helpRect = (Rect)mRect;
            }


            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            
            GUILayout.EndArea();
        }
    }
}
