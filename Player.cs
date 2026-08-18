using System.Reflection;
using static PengooinLabs.ReplayMod.Replay;
using static PengooinLabs.ReplayMod.Types;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppFemur;
using Il2CppGB.Game.Critters;
using Il2CppAudio;
using Il2Cpp;
using Il2CppGB.Core;
using Il2CppCinemachine;
using Il2CppGB.Stages.Train;
using Il2CppGB.Stages.Vents;
using Il2CppGB;
using Il2CppGB.Game;
using Il2CppGB.UI.Menu;
using Il2CppCoatsink.Common;

namespace PengooinLabs.ReplayMod
{
    public class Player
    {
        public Player(
            Dictionary<CaptureGroupId, List<Frame>> frameGroups,
            List<Actor> replayActors,
            Dictionary<Actor, string> actorNames,
            List<BirdActor> birds,
            Dictionary<string, ItemState> staticItemStates
        )
        {
            this.frameGroups = frameGroups;
            this.replayActors = replayActors;
            this.actorNames = actorNames;
            this.replayBirds = birds;
            this.staticItemStates = staticItemStates;
        }

        private Dictionary<string, ItemState> staticItemStates;
        public bool started = false;

        public void start()
        {
            started = true;
            setPlaySpeed(idlePlaySpeed);
            if (Replay.cfg_isFirstStart_player.Value)
            {
                Replay.cfg_isFirstStart_player.Value = false;
                Replay.toggleWantMenuOpen(Menu.ReplayHelp, true);
            }
        }

        private HarmonyLib.Harmony? harmony;

        private void unpatch()
        {
            harmony!.UnpatchSelf();
            harmony = null;
        }

        private bool stopped = false;

        public void stop()
        {
            if (stopped) return;
            stopped = true;
            unpatch();
        }

        public float totalCursorXDelta = 0f;
        private float fixedDeltaTime = 1f / 30f;
        private int? _stickSpeed = 0;
        public int? stickSpeed { get { return _stickSpeed; } }

        public void update_checkUserInput()
        {
            update_drag(DragButton.Left, handleDragStart, handleDragMove, handleDragEnd);
            update_drag(DragButton.Right, handleDragStart, handleDragMove, handleDragEnd);

            // ------------------------------
            // check next/prev player buttons
            // ------------------------------

            if (Replay.cfg_cameraMode.Value == CameraMode.SURROUND)
            {
                if (Controls.wasPrevActorButtonPressed()) focusPrevActor();
                if (Controls.wasNextActorButtonPressed()) focusNextActor();
                if (Controls.wasToggleCameraTargetButtonPressed())
                {
                    Replay.toggleCameraTargeting();
                }
            }

            if (!Replay.cfg_disableKeys.Value)
            {
                if (Controls.wasSpeedIncreaseKeyPressed())
                {
                    increasePlaySpeed();
                }
                else if (Controls.wasSpeedDecreaseKeyPressed())
                {
                    decreasePlaySpeed();
                }
            }

            // ------------------
            // check pause button
            // ------------------

            if (Controls.wasPauseButtonPressed())
            {
                // toggle play/pause
                if (idlePlaySpeed == 0 && lastSliderSpeed == 0) lastSliderSpeed = 100;
                idlePlaySpeed = idlePlaySpeed == 0 ? lastSliderSpeed : 0;
            }

            // --------------------
            // check mouse movement
            // --------------------

            if (mouseAction == MouseAction.RotationControl)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                totalCursorXDelta += mouseDelta.x;

                if (mouseDelta.x != 0)
                {
                    float multiplier = Replay.cfg_mouse_sensitivity_x.Value / -1000f;
                    if (isMouseXInverted()) multiplier *= -1;
                    var degrees = mouseDelta.x * multiplier;
                    camera.setLR(camera.getLR() + degrees, false);
                }

                if (mouseDelta.y != 0)
                {
                    if (!Mouse.current.leftButton.isPressed)
                    {
                        float multiplier = Replay.cfg_mouse_sensitivity_y.Value / -1000f;
                        if (isMouseYInverted()) multiplier *= -1;
                        var degrees = mouseDelta.y * multiplier;
                        camera.setUD(camera.getUD() + degrees, false);
                    }
                }
            }
            else if (mouseAction == MouseAction.TimeControl)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                totalCursorXDelta += mouseDelta.x;

                float speedup = Mouse.current.rightButton.isPressed ? 5f : 1f;
                virtualTime += mouseDelta.x * speedup / 200f;
            }

            // -----------------
            // check mouse wheel
            // -----------------

            Vector2 wheelDelta = Controls.getMouseWheel();

            if (wheelDelta.y != 0)
            {
                if (Keyboard.current != null && Keyboard.current.ctrlKey.isPressed)
                {
                    // modify speed
                    // TODO might be problematic on some mice

                    if (wheelDelta.y > 0)
                    {
                        increasePlaySpeed();
                    }
                    else
                    {
                        decreasePlaySpeed();
                    }
                }
                else
                {
                    if (!cfg_leftMouseAltMode.Value || mouseAction == MouseAction.RotationControl)
                    {
                        float distance = wheelDelta.y / (Replay.cfg_mousewheel_zoom_sensitivity.Value * -1);
                        camera.setDistance(camera.getDistance() + distance, false);
                    }
                }
            }
        }

        public void increasePlaySpeed()
        {
            idlePlaySpeed += Replay.cfg_mousewheel_speed_step.Value;
            if (idlePlaySpeed > 100) idlePlaySpeed = 100;
            if (idlePlaySpeed > 0 && idlePlaySpeed < Replay.cfg_mousewheel_speed_step.Value) idlePlaySpeed = 0;
            lastSliderSpeed = idlePlaySpeed;
        }

        public void decreasePlaySpeed()
        {
            idlePlaySpeed -= Replay.cfg_mousewheel_speed_step.Value;
            if (idlePlaySpeed < -100) idlePlaySpeed = -100;
            if (idlePlaySpeed < 0 && idlePlaySpeed > -Replay.cfg_mousewheel_speed_step.Value) idlePlaySpeed = 0;
            lastSliderSpeed = idlePlaySpeed;
        }

        public void fixedUpdate()
        {
            if (!started) return;
            if (stopped) return;
            fixedUpdate_checkUserInput_setSpeed();
        }

        public void fixedUpdate_checkUserInput_setSpeed()
        {
            // rotation is modified in fixedUpdate and smoothened out by the rotation loop

            if (Game.paused())
            {
                // pause time, no further contol checks
                setPlaySpeed(0);
                return;
            }

            if (Gamepad.current != null && Replay.cfg_cameraMode.Value == CameraMode.SURROUND)
            {
                // ----------------
                // rotation control
                // ----------------

                var rotation = Gamepad.current.rightStick.ReadValue();

                if (rotation.x != 0)
                {
                    var x = rotation.x;
                    float mod = isGamepadXInverted() ? -fixedDeltaTime : fixedDeltaTime;
                    camera.setLR(camera.getLR() + (Replay.cfg_camLrSpeed.Value * -x * mod), false);
                }

                if (rotation.y != 0)
                {
                    var y = rotation.y;
                    float mod = isGamepadYInverted() ? -fixedDeltaTime : fixedDeltaTime;
                    camera.setUD(camera.getUD() + Replay.cfg_camUdSpeed.Value * -y * mod, false);
                }
            }

            if (mouseAction == MouseAction.TimeControl)
            {
                setPlaySpeed(0);
                return;
            }

            _stickSpeed = getAnalogStickPlaySpeed();

            if (Gamepad.current != null)
            {
                // --------------------------------
                // set speed from left analog stick?
                // --------------------------------

                if (stickSpeed != null)
                {
                    // if stick is being moved, use that speed
                    setPlaySpeed((int)stickSpeed);
                    return;
                }

                // -----
                // zoom?
                // -----

                // only possible if stickSpeed is null (LT not held)

                // zoom with left analog stick
                Vector2 ZOOM = Gamepad.current.leftStick.ReadValue();
                if (Math.Abs(ZOOM.y) > 0.2f)
                {
                    var dist = Replay.cfg_camDistSpeed.Value * ZOOM.y * fixedDeltaTime;
                    camera.setDistance(camera.getDistance() - dist, false);
                }
            }

            // use what's set as idle speed
            setPlaySpeed(idlePlaySpeed);
        }

        public void setPlaySpeed(int effectiveSpeed)
        {
            if (effectiveSpeed != lastEffectivePlaySpeed)
            {
                _lastEffectivePlaySpeed = effectiveSpeed;
                Replay.setTimeScale(effectiveSpeed >= 0 ? (effectiveSpeed / 100f) : 0);
            }
        }

        public float innerDeadzone = 0.05f;
        public float outerDeadzone = 0.05f;

        public int? getAnalogStickPlaySpeed()
        {
            if (Gamepad.current == null) return null;

            if (!Controls.isSpeedControlButtonDown()) return null;

            float x = Gamepad.current.leftStick.ReadValue().x;

            if (Math.Abs(x) > innerDeadzone)
            {
                x = x / (1f - outerDeadzone); // 10% outer deadzone
                if (x > 1f) { x = 1f; } else if (x < -1f) { x = -1f; }
                // multiply speed if modifier button is down
                if (Controls.isFasterSpeedButtonDown()) x *= Replay.fasterSpeedMultiplier;
                return (int)(x * 100);
            }

            return 0;
        }

        public bool isInSliderArea(Vector2 position)
        {
            if (Replay.cfg_hideReplayControls_Value)
            {
                return new Rect(
                    0,
                    Screen.height - bottomBarHeight,
                    hideButtonWidth,
                    bottomBarHeight
                ).Contains(new Vector2(position.x, Screen.height - position.y));
            }

            return !new Rect(
                0,
                0,
                Screen.width,
                Screen.height - Replay.bottomBarHeight
            ).Contains(new Vector2(position.x, Screen.height - position.y));
        }

        public bool isInMenuArea(Vector2 position)
        {
            if (Replay.wantMenuOpen(Menu.ReplayHelp) && Menus.helpRect.Contains(position)) return true;
            if (Replay.wantMenuOpen(Menu.ReplayOptions) && Menus.replayOptionsMenuRect.Contains(position)) return true;
            if (isInSliderArea(position)) return true;
            if (Replay.wantMenuOpen(Menu.ReplayFilelist) && Menus.fileListRect.Contains(position)) return true;
            return false;
        }

        public enum MouseAction { None, LeftClick, TimeControl, RotationControl }

        public MouseAction mouseAction = MouseAction.None;
        public MouseAction lastMouseAction = MouseAction.None;

        public void handleDragStart(DragButton button, Vector2 position)
        {
            // ignore all clicks in paused mode
            if (Game.paused())
            {
                return;
            }

            if (isInMenuArea(position)) return;

            // double mousedown should not haben, but if you tab out of the
            // game, it can.

            if (mouseButtonDown[button])
            {
                return;
            }


            // this reflects the current mouse state
            mouseButtonDown[button] = true;

            // note that the drag-threshold of this button hasn't been crossed yet
            mouseCrossedThreshold[button] = false;

            // not the drag start position for distance calculations
            dragStartPosition[button] = position;

            if (button == DragButton.Right && mouseAction == MouseAction.LeftClick)
            {
                // switch to time position control
                mouseAction = MouseAction.TimeControl;
                lastMouseAction = mouseAction;
            }

            if (mouseAction != MouseAction.None)
            {
                // then the button can only be treated as alt button.
                // this is checked somewhere else and we don't have
                // to do anything
                return;
            }

            if (button == DragButton.Left)
            {
                if (cfg_leftMouseAltMode.Value && (Keyboard.current == null || !Keyboard.current.ctrlKey.isPressed)) return;
                // until the threshold is crossed, releasing the
                // mouse will cause a leftclick
                mouseAction = MouseAction.LeftClick; // this is reset on mouseup
                lastMouseAction = mouseAction; // this stays until it's overwritten

                // we'll gather the horizontal movement delta over time
                totalCursorXDelta = 0;
            }
            else if (button == DragButton.Right)
            {
                // ignore if right drag is disabled in the options
                if (cfg_disableRightDrag.Value)
                {
                    return;
                }

                // if we're not using 360 camera, there is nothing to control
                if (Replay.cfg_cameraMode.Value != CameraMode.SURROUND)
                {
                    return;
                }

                // there is no right click atm, so pressing right mouse button will
                // immediately cause rotationcontrol.

                mouseAction = MouseAction.RotationControl;
                lastMouseAction = mouseAction;
            }
        }

        // drag at least 5px to be treated as drag and not click
        public float dragThreshold = 5f;

        public void handleDragMove(DragButton button, Vector2 position, Vector2 delta)
        {
            // not needed
        }

        public void handleDragEnd(DragButton button, Vector2 position, Vector2 delta)
        {
            mouseButtonDown[button] = false;
            dragEndPosition[button] = position;

            if (button == DragButton.Left)
            {

                if (mouseAction == MouseAction.LeftClick)
                {
                    // treat as left click -> focus a player
                    focusActorAtScreenPosition(position);
                    // reset mouseAction flag
                    mouseAction = MouseAction.None;
                }
                else if (mouseAction == MouseAction.TimeControl)
                {
                    // just reset the flag
                    mouseAction = MouseAction.None;
                }
            }
            else if (button == DragButton.Right)
            {
                if (mouseAction == MouseAction.RotationControl)
                {
                    mouseAction = MouseAction.None;
                }
            }
        }

        public void focusActorAtScreenPosition(Vector2 position)
        {
            // if an actor was clicked, focus them
            var actor = getActorAtScreenPosition(position, true);
            if (actor != null) Replay.camera.setFocusedActor(actor);
        }

        public void togglePlayPause()
        {
            if (idlePlaySpeed == 0 && lastSliderSpeed == 0) lastSliderSpeed = 100;
            idlePlaySpeed = idlePlaySpeed == 0 ? lastSliderSpeed : 0;
        }

        public void update(float deltaTime, bool playFrame)
        {
            if (stopped) return;
            if (!started) return;

            if (!Game.paused()) update_checkUserInput();

            if (playFrame)
            {
                playOurFrame(deltaTime);
                if (Replay.cfg_cameraMode.Value == CameraMode.SURROUND)
                {
                    Replay.camera.update(deltaTime);
                }
            }
        }

        private bool cursorLocked = false;

        public void lateUpdate(float deltaTime, bool playFrame)
        {
            if (playFrame)
            {
                playOurFrame(deltaTime);
                if (Replay.cfg_cameraMode.Value == CameraMode.SURROUND) Replay.camera.update(deltaTime);
            }

            bool maybeResetCursor = false;

            var lockCursor = false;

            // only ever hide cursor if the option is set, never hide it in paused mode,
            // must be controlling camera or time, plus further conditions
            if (!Game.paused() && (mouseAction == MouseAction.TimeControl || mouseAction == MouseAction.RotationControl))
            {
                // if menu is shown, mouse can only control rotation (i.e. right drag)
                lockCursor = true;
            }

            if (lockCursor != cursorLocked)
            {
                // cursor lock changed
                cursorLocked = lockCursor;
                if (!cursorLocked)
                {
                    // cursor lock was removed

                    // reset cursor to original position if we were controlling time
                    // using mouse, but didn't move the mouse for a significant distance
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    maybeResetCursor = true;
                }
            }

            // write the Cursor.visible and lockState values each frame if cursor is locked,
            // since it is set back all the time

            // lock cursor if gui is not visible so we can use the mouse
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (maybeResetCursor)
            {
                if (lastMouseAction == MouseAction.RotationControl)
                {
                    // reset cursor position
                    Vector2 pos = (Vector2)dragStartPosition[DragButton.Camera]!;
                    // pos += new Vector2(totalCursorXDelta, 0); // vertical axis only
                    Mouse.current.WarpCursorPosition(pos);
                }
                else if (lastMouseAction == MouseAction.TimeControl)
                {
                    // reset cursor position
                    Vector2 pos = (Vector2)dragStartPosition[DragButton.Time]!;
                    pos += new Vector2(totalCursorXDelta, 0); // vertical axis only
                    Mouse.current.WarpCursorPosition(pos);
                }
            }

            // something overwrites the name each frame (OnUpdate).
            // as a workaround, overwrite it again each frame OnLateUpdate()

            if (MonoSingleton<Global>.Instance.UIDirector.ShowNameBars)
            {
                foreach (var actor in replayActors)
                {
                    // might not be ready yet
                    try { actor.NameBar.CachedNameText.text = getNamebarNameForActor(actor); }
                    catch { }
                }
            }
        }

        public List<Actor> getActors() { return replayActors; }

        private int _lastEffectivePlaySpeed = 0;
        private int lastEffectivePlaySpeed { get { return _lastEffectivePlaySpeed; } }

        // if not set to -1, this will be used when analog stick is in neutral
        // position. toggle with jump button
        private int idlePlaySpeed = 100;
        private int lastSliderSpeed = 100;

        private List<Actor> replayActors = new();
        private List<BirdActor> replayBirds = new();
        private event Action<float, Frame, Frame>? afterPlayOurFrame = null;

        // names to be displayed in the actors' namebars
        private Dictionary<Actor, string> actorNames = new();

        // recorded frames are kept in groups with high/low capture rates
        private Dictionary<CaptureGroupId, List<Frame>> frameGroups = new();

        // what keyframe we played last
        private Frame? currentKeyframe = null;

        private float playSoundGraceTime = 0.5f;

        // the current virtual time (replay position)
        private float virtualTime = -1f;

        public void returnToStart()
        {
            virtualTime = 0;
        }

        // play the current replay frame
        private void playOurFrame(float realDeltaTime)
        {
            // on first call, set it to 0
            if (virtualTime == -1f)
            {
                virtualTime = 0;
            }
            else
            {
                // add passed time modified by playSpeed, if not in paused mode
                if (!Game.paused()) virtualTime += realDeltaTime * lastEffectivePlaySpeed / 100f;
            }

            // clamp virtualTime
            var frames = frameGroups[CaptureGroupId.Actor];
            if (frames.Count == 0 || virtualTime < 0f) virtualTime = 0;
            if (frames.Count > 0 && virtualTime > frames[^1].time) virtualTime = frames[^1].time;

            // get keyframe used for virtual time
            Frame? keyFrame = getFrameAtTime(frameGroups[CaptureGroupId.Actor], virtualTime);

            if (keyFrame == null) return; // safety

            var previousKeyFrame = currentKeyframe;

            bool keyframeChanged = false;

            // check for keyframe change            
            if (currentKeyframe != keyFrame)
            {
                keyframeChanged = true;
                currentKeyframe = keyFrame;
            }

            if (keyframeChanged)
            {
                // at high replay frame rates or low system performance, recorded frames might get skipped,
                // and their sounds arent't playing. to still play the sounds present in skipped frames,
                // we seek over a range of frames not checked yet.

                // we assume we can play at least 20fps
                bool catchUp = false;

                if (previousKeyFrame != null)
                {
                    var delta = keyFrame.time - previousKeyFrame.time;
                    catchUp = delta > 0 && delta < playSoundGraceTime;
                }

                // play sounds

                // condition: must be playing replay in forward direction.

                // play sounds referenced in a keyframe if it wasn't the
                // basis of the previous intermediate frame. prevents
                // repeated playing.

                if (catchUp)
                {
                    for (int k = previousKeyFrame!.index + 1; k <= keyFrame.index; k++)
                    {
                        var kf = frameGroups[CaptureGroupId.Actor][k];
                        playFrameSounds(kf);
                    }
                }
                else
                {
                    if (previousKeyFrame == null || keyFrame.index > previousKeyFrame.index)
                    {
                        playFrameSounds(keyFrame);
                    }
                }
            }

            // calculate intermediate frame
            Frame? intermediateFrame = getIntermediateFrame(virtualTime);

            // at the end of the replay, we won't get an intermediate
            // frame, and use the previous one, to lock the state to the
            // last possible frame. otherwise the state won't be updated
            // and the game will snap back to normal state.

            if (intermediateFrame == null)
            {
                if (lastIntermediateFrame == null) return;
                intermediateFrame = lastIntermediateFrame;
                virtualTime = intermediateFrame.time;
            }

            // keep reference of last intermediate frame for reuse
            lastIntermediateFrame = intermediateFrame;

            // play the frame / set its state
            playFrame(intermediateFrame);

            // callback after frame was played
            if (afterPlayOurFrame != null) afterPlayOurFrame(virtualTime, intermediateFrame, keyFrame);
        }

        private void playFrameSounds(Frame frame)
        {
            if (frame.sounds3d != null)
            {
                // play 3d sounds
                foreach (var clp in frame.sounds3d)
                {
                    clp.loop = false;
                    play3dAudioclip(clp);
                }
            }

            if (frame.sounds2d != null)
            {
                // play 2d sounds
                foreach (var clp in frame.sounds2d)
                {
                    clp.loop = false;
                    play2dAudioclip(clp);
                }
            }
        }

        private void playFrame(Frame intermediateFrame)
        {
            // apply frame state to game

            // blinks can be null at the end of the replay since the environment frame
            // group can end earlier than the actor frame group, and then it won't be
            // found at the ending timestamps, and thus not merged into the intermediate
            // frame.

            if (intermediateFrame.actorBlinks != null) setActorBlinks(intermediateFrame.actorBlinks);
            if (intermediateFrame.birdBlinks != null) setBirdBlinks(intermediateFrame.birdBlinks);
            setItemStates(intermediateFrame);
            if (intermediateFrame.actorStates != null) setActorStates(intermediateFrame.actorStates); // given
        }


        // set actor "blink" values

        private void setActorBlinks(List<byte> blinks)
        {
            // blinks can be < replayActors in waves mode, since actors
            // exist there since the beginning, but blinks for them only
            // come in later.

            for (int i = 0; i < blinks.Count; i++)
            {
                Actor actor = replayActors[i];
                Game.setActorBlink(actor, blinks[i]);
            }
        }

        private void setBirdBlinks(List<byte> blinks)
        {
            for (int i = 0; i < blinks.Count; i++)
            {
                if (i > replayBirds.Count - 1) break; // safety
                Game.setBirdBlink(replayBirds[i], blinks[i]);
            }
        }

        private void setItemStates(Frame intermediateFrame)
        {
            foreach (var entry in intermediateFrame.states)
            {
                var state = entry.Value;
                var info = tc.query(entry.Key);
                if (info != null)
                {
                    var key = info.key;
                    if (key.StartsWith("audioclip:"))
                    {
                        // save the last position to overwrite it after AudioController.Update()
                        audioclipPositions[info.transform] = state.pos;
                    }
                    Replay.setTransformState(info.transform, state, intermediateFrame.aIndex == 0);
                }
            }

            foreach (var entry in staticItemStates)
            {
                var info = tc.query(entry.Key);
                if (info != null)
                {
                    var key = info.key;
                    Replay.setTransformState(info.transform, staticItemStates[key], intermediateFrame.aIndex == 0);
                }
            }
        }

        private Dictionary<Transform, Vector3> audioclipPositions = new();

        public void setAudioClipsPosition()
        {
            foreach (var entry in audioclipPositions)
            {
                var transform = entry.Key;
                var position = entry.Value;
                transform.position = position;
            }
        }

        // reference to the last played intermediate frame
        private Frame? lastIntermediateFrame = null;
        private Frame? getIntermediateFrame(float virtualTime)
        {
            // if virtualTime didn't change, return previous intermediate frame
            if (lastIntermediateFrame != null && lastIntermediateFrame.time == virtualTime)
            {
                return lastIntermediateFrame;
            }

            // interpolate a state for current virtualtime, lerping positions and
            // rotations of all transforms from frame A to B

            var frames = frameGroups[CaptureGroupId.Actor];
            Frame? a = getFrameAtTime(frames, virtualTime);
            if (a == null) { return null; }

            Frame? b = a.index == frames.Count - 1 ? null : frames[a.index + 1];
            if (b == null) return null;

            var intermediate = getPartialIntermediateFrame(a, b, virtualTime, null);

            // add environment part
            // we only accept fully merged frames, so bail out if we don't find
            // a matching environment frame. can happen because the group can
            // end slightly earlier.

            var environmentFrames = frameGroups[CaptureGroupId.Environment];

            Frame? a2 = getFrameAtTime(environmentFrames, virtualTime);
            if (a2 == null) return null;

            Frame? b2 = a2.index == environmentFrames.Count - 1 ? null : environmentFrames[a2.index + 1];
            if (b2 == null) return null;

            getPartialIntermediateFrame(a2, b2, virtualTime, intermediate);

            // keep frame for potential reuse
            lastIntermediateFrame = intermediate;
            return intermediate;
        }

        private Frame? getPartialIntermediateFrame(Frame a, Frame b, float virtualTime, Frame? intermediate)
        {

            // create intermediate frame object if not given

            if (intermediate == null)
            {
                // happens when it's captureGroup 0
                intermediate = new Frame();
                intermediate.index = -1;
                intermediate.aIndex = a.index;
                intermediate.groupId = CaptureGroupId.INTERMEDIATE;
                intermediate.time = virtualTime;
            }

            // copy actorBlinks to the intermediate frame if it's included in this frame

            if (a.groupId == CaptureGroupId.Environment)
            {
                intermediate.actorBlinks = a.actorBlinks;
                if (a.birdBlinks != null) intermediate.birdBlinks = a.birdBlinks;
                intermediate.actorStates = a.actorStates;
            }
            // lerp all item states

            // ratio for lerp
            float r = (virtualTime - a.time) / (b.time - a.time);

            foreach (var entry in a.states)
            {
                string key = entry.Key;
                ItemState aState = entry.Value;
                if (!b.states.ContainsKey(key))
                {
                    // use state from A if not included in B
                    intermediate.states[key] = aState;
                    continue;
                }

                ItemState bState = b.states[key];

                // create intermediate ItemState
                var iState = intermediate.states[key] = new ItemState();
                iState.active = aState.active;

                var posDistance = Vector3.Distance(aState.pos, bState.pos);
                if (posDistance > 5f)
                {
                    // transform was "beamed". for example, when sausages on
                    // chutes are reset from the bottom to the top.
                    // interpolating this causes an unwanted effect.
                    iState.pos = aState.pos;
                }
                else
                {
                    // lerp position, rotation and scale
                    iState.pos = Vector3.Lerp(aState.pos, bState.pos, r);
                }
                iState.lscale = Vector3.Lerp(aState.lscale, bState.lscale, r);
                iState.rot = Quaternion.Lerp(Quaternion.Euler(aState.rot), Quaternion.Euler(bState.rot), r).eulerAngles;

            }

            return intermediate;
        }


        // binary search algorithm to find the frame for a given timestamp
        // in a series of frames

        private int findFrameIndexAtTime(List<Frame> frames, float targetTime)
        {
            int left = 0;
            int right = frames.Count - 1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                int result = checkFrame(frames[mid], targetTime);
                if (result == 0) return mid;
                if (result < 0)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            return -1;
        }

        private Dictionary<CaptureGroupId, Frame> lastLookedUpFrames = new();

        private Frame? getFrameAtTime(List<Frame> frames, float targetTime)
        {
            // check whether previously found frame still matches.
            // if not, whether the next frame matches.
            // if not, fallback to binary search

            if (frames.Count == 0) return null;
            var groupId = frames[0].groupId;

            if (lastLookedUpFrames.ContainsKey(groupId))
            {
                // check on last looked up
                var lastLookedUp = lastLookedUpFrames[groupId];
                var result = checkFrame(lastLookedUp, targetTime);
                if (result == 0) return lastLookedUp;

                if (result == 1)
                {
                    // look further right
                    var nextIdx = lastLookedUp.index + 1;
                    if (nextIdx <= frames.Count - 1)
                    {
                        var nextFrame = frames[nextIdx];
                        var nextResult = checkFrame(nextFrame, targetTime);
                        if (nextResult == 0)
                        {
                            lastLookedUpFrames[groupId] = nextFrame;
                            return nextFrame;
                        }
                    }
                }
                else if (result == -1)
                {
                    // look further left
                    var nextIdx = lastLookedUp.index - 1;
                    if (nextIdx >= 0)
                    {
                        var nextFrame = frames[nextIdx];
                        var nextResult = checkFrame(nextFrame, targetTime);
                        if (nextResult == 0)
                        {
                            lastLookedUpFrames[groupId] = nextFrame;
                            return nextFrame;
                        }
                    }

                }
                lastLookedUpFrames.Remove(groupId);
            }

            int idx = findFrameIndexAtTime(frames, targetTime);
            if (idx == -1) return null;
            lastLookedUpFrames[groupId] = frames[idx];

            return frames[idx];

        }

        private int checkFrame(Frame frame, float targetTime)
        {
            if (frame.time == targetTime) return 0; // found
            if (frame.time > targetTime) return -1; // go left
            var frames = frameGroups[frame.groupId];
            if (frame.index + 1 < frames.Count && frames[frame.index + 1].time > targetTime) return 0; // found
            return 1; // go right
        }

        private string getNamebarNameForActor(Actor actor)
        {
            if (actor == null) return ""; // safety
            if (actorNames.ContainsKey(actor)) return actorNames[actor];
            return "";
        }
        private bool isFocusableActor(Actor actor)
        {
            // must be active and alive
            return actor.actorState != Actor.ActorState.Dead && actor.gameObject.active;
        }

        private float tLastFocusAnotherActor = 0f;
        private List<Actor> lastFocusableActors = new();

        private void focusNextOrPrevActor(bool next)
        {
            if (Replay.camera.focusedActor == null)
            {
                var focusable = replayActors.Where(a => isFocusableActor(a)).ToList();
                if (focusable.Count > 0)
                {
                    Replay.camera.setFocusedActor(next ? focusable[0] : focusable[^1]);
                }
            }
            else
            {
                var focusable = new List<Actor>();

                // if actor switches are triggered within < 1s, use the order determined
                // when switching started

                if (lastFocusableActors == null || Time.unscaledTime - tLastFocusAnotherActor > 1f)
                {
                    focusable = replayActors.Where(a => isFocusableActor(a) || a == Replay.camera.focusedActor).ToList();
                    lastFocusableActors = focusable;
                    tLastFocusAnotherActor = Time.unscaledTime;
                }
                else
                {
                    focusable = lastFocusableActors;
                }
                
                Camera.main.orthographic = true;
                var refX = Camera.main.WorldToScreenPoint(Replay.camera.focusedActor.bodyHandeler.Chest.PartTransform.position).x;

                var sortable = focusable.ConvertAll<SortableNextActor>(a => new SortableNextActor()
                {
                    actor = a,
                    screenDistanceX = Camera.main.WorldToScreenPoint(a.bodyHandeler.Chest.PartTransform.position).x - refX
                });

                Camera.main.orthographic = false;

                sortable.Sort(Tools.nextActorSorter);
                focusable = sortable.ConvertAll<Actor>(s => s.actor);

                int oldIndex = focusable.IndexOf(Replay.camera.focusedActor);
                int index = oldIndex;
                while (true)
                {
                    if (next)
                    {
                        index++;
                        if (index > focusable.Count - 1) index = 0;
                    }
                    else
                    {
                        index--;
                        if (index < 0) index = focusable.Count - 1;
                    }

                    if (index == oldIndex) break; // none found
                    Replay.camera.setFocusedActor(focusable[index]);
                    break;
                }
            }
        }

        // focus previous actor
        private void focusPrevActor()
        {
            focusNextOrPrevActor(false);
        }

        // focus next actor
        private void focusNextActor()
        {
            focusNextOrPrevActor(true);
        }

        public Actor? getNextClosestActor(Actor oldActor)
        {
            var candidates = replayActors.Where(a => isFocusableActor(a) && a != oldActor).ToList();
            if (candidates.Count == 0) return null;

            var refPos = oldActor.bodyHandeler.Chest.PartTransform.position;
            var closest = candidates.ConvertAll<SortableNextClosestActor>(a => new SortableNextClosestActor()
            {
                actor = a,
                distance = Vector3.Distance(refPos, a.bodyHandeler.Chest.PartTransform.position)
            });
            closest.Sort(Tools.nextClosestActorSorter);
            return closest[0].actor;
        }

        public class SortableNextActor
        {
            public Actor actor;
            public float screenDistanceX;
        }

        public class SortableNextClosestActor
        {
            public Actor actor;
            public float distance;
        }

        // set current state of actors 
        private void setActorStates(List<byte> states)
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (replayActors[i].actorState != (Actor.ActorState)states[i])
                {
                    // make sure to set private field since we block setting
                    // the state via private property in our hooks
                    replayActors[i]._actorState = (Actor.ActorState)states[i];
                }
            }
        }

        public void patch()
        {
            if (harmony != null) return;

            harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(PlayerPatch), null);

            // disable some stuff that will interfere with replay otherwise.

            nopMethodsDuringReplay(

                new()
                {
                    "OnCollisionEnter",
                    "OnCollisionExit"
                },

                new()
                {
                    typeof (CollisionHandeler)
                }
            );

            nopMethodsDuringReplay(new() { "Tick", }, new() { typeof(wheelLights) } );

            nopMethodsDuringReplay(

                new() {
                    "Update",
                    "FixedUpdate",
                    "LateUpdate"
                },
                
                new() {
                    typeof(Actor),
                    typeof(StatusHandeler),

                    typeof(BirdActor),
                    typeof(FishActor),
                    typeof(SharkActor),
                    typeof(SharkNodeMover),

                    // aquarium
                    typeof(TentacleController),
                    typeof(TentacleMechanics),
                    typeof(TentacleSegmentManager),

                    // buoy
                    typeof(BuoyStageMechanics),
                    typeof(BuoyBell),

                    // crane
                    typeof(Crane),
                    // typeof(CraneController), // crashed
                    typeof(CranePlatformDestroyer),
                    typeof(Crane_RandomPointMover),
                

                    // gondola
                    typeof(MoveGondola),
                    typeof(Gondola_Wind),
                    typeof(Gondola_Cable),

                    // train
                    typeof(TrackMover),
                    typeof(TrackPool),

                    // trawler
                    typeof(Trawler_Crain),
                    typeof(Trawler_Mechanics),
                    typeof(TrawlerDoors),
                    // typeof(TrawlerLighthouse),
                
                    // trucks
                    typeof(Truck),
                    typeof(Truck1),

                    // wheel
                    typeof(WheelEscalation),
                    typeof(WheelAmbientAudio),
                    typeof(WheelRotator),
                    typeof(BurgerDwell),
                    typeof(wheelAxle),
                    typeof(WheelLight),
                    typeof(WheelRotator),

                
                    // vents
                    typeof(FanController),

                    typeof(Logic_Containers),
                    typeof(MovePlatform),
                    typeof(GamepLogic_Containers)
                }
            );

            // AudioController needs to run at real time

            useNormalTimeInUpdate(new List<Type>() {
                typeof(AudioController)
            });

            // patch time.deltaTime
            var get_deltaTime = typeof(UnityEngine.Time)!.GetProperty(nameof(UnityEngine.Time.deltaTime))!.GetGetMethod();
            harmony.Patch(get_deltaTime, new HarmonyLib.HarmonyMethod(typeof(Player).GetMethod("_deltaTimePatch")), null, null);


            // this sets the sharks' rigidbodies isKinematic = false, we have to prevent that
            nopMethodsDuringReplay(new() { "DelayedStart" }, new() { typeof(SharkActor) });
        }


        public bool useRealDeltaTime = false;

        public static bool _deltaTimePatch(ref float __result)
        {
            if (Replay.modState == Replay.ModState.PlayingReplay)
            {
                if (Replay.player!.useRealDeltaTime) __result = Time.unscaledDeltaTime;
                return false;
            }
            return true;
        }

        public static void _setNormalTime()
        {
            Replay.player!.useRealDeltaTime = true;
        }

        public static void _setZeroTime()
        {
            Replay.player!.useRealDeltaTime = false;
        }

        private void useNormalTimeInUpdate(List<Type> types)
        {
            foreach (var type in types)
            {
                var method = getMethods(type).Where(m => m.Name == "Update").FirstOrDefault();
                var prefix = new HarmonyLib.HarmonyMethod(typeof(Player).GetMethod("_setNormalTime"));
                var postfix = new HarmonyLib.HarmonyMethod(typeof(Player).GetMethod("_setZeroTime"));
                if (harmony!.Patch(method, prefix, postfix, null) == null)
                {
                    logError("failed to patch " + type.FullName + " Update");
                }
            }
        }

        public static bool _disabledDuringReplay()
        {
            if (Replay.modState == ModState.PlayingReplay) return false;
            if (Replay.modState == ModState.LoadingReplay) return false;
            return true;
        }
 
        private void nopMethodsDuringReplay(List<string> methodNames, List<Type> types)
        {
            var hash = new Dictionary<string,bool>();
            foreach (var methodName in methodNames) hash[methodName] = true;

            foreach (var type in types)
            {
                var methods = getMethods(type).Where(m => hash.ContainsKey(m.Name)).ToList();
                foreach (var method in methods)
                {
                    if (harmony!.Patch(method, new HarmonyLib.HarmonyMethod(typeof(Player).GetMethod("_disabledDuringReplay")), null, null) == null)
                    {
                        logError("failed to patch " + type.FullName + " " + method.Name);
                    }
                }
            }
        }

        private static List<MethodInfo> getMethods(Type type)
        {
            return type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly
            ).ToList();
        }

        public static Dictionary<string, AudioClip?>? cachedAudioClips = null;

        private static AudioClip? getAudioClip(string clipName)
        {
            if (cachedAudioClips == null)
            {
                cachedAudioClips = new();
                foreach (var clip in UnityEngine.Resources.FindObjectsOfTypeAll<AudioClip>())
                {
                    cachedAudioClips[clip.name] = clip;
                }
            }
            return cachedAudioClips.ContainsKey(clipName) ? cachedAudioClips[clipName] : null;
        }

        // play a 2d audio clip
        public static void play2dAudioclip(_2dAudioClip clp)
        {
            AudioClip? audioclip = getAudioClip(clp.name);
            if (audioclip == null) return;
            AudioController.Instance.Play2D(audioclip, (VolumeLevels.SoundType)clp.soundType, null, clp.loop, clp.volume, clp.pitch, clp.delay, null, null);
        }

        // play a 3d audio clip

        public static void play3dAudioclip(_3dAudioClip clp)
        {
            AudioClip? audioclip = getAudioClip(clp.name);
            if (audioclip == null) return;

            var position = new Vector3(clp.posX, clp.posY, clp.posZ);

            AudioController.Instance.Play3DAt(
                audioclip,
                position,
                null,
                (VolumeLevels.SoundType)clp.soundType,
                null,
                clp.loop,
                clp.volume,
                clp.pitch,
                clp.delay,
                clp.minDistance,
                clp.maxDistance,
                clp.dopplerLevel,
                null,
                null,
                clp.spatialBlendOverride
            );
        }

        private string formatTime(float seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return $"{time.Minutes:D2}:{time.Seconds:D2}";
        }

        private int hideButtonWidth = 16;

        // gui styles

        private GUIStyle? _playerLabelStyle = null;
        public GUIStyle playerLabelStyle
        {
            get
            {
                if (_playerLabelStyle == null) {
                    _playerLabelStyle = new GUIStyle(Replay.labelStyle);
                    _playerLabelStyle.alignment = TextAnchor.MiddleLeft;
                }
                return _playerLabelStyle;
            }
        }
        
        private static GUIStyle? _hideButtonStyle = null;
        
        private static GUIStyle hideButtonStyle
        {
            get
            {
                if (_hideButtonStyle == null)
                {
                    _hideButtonStyle = new GUIStyle();
                    _hideButtonStyle.alignment = TextAnchor.MiddleCenter;
                    _hideButtonStyle.fontStyle = FontStyle.Bold;
                    _hideButtonStyle.normal.textColor = Color.gray;
                    _hideButtonStyle.hover.textColor = Color.white;
                    _hideButtonStyle.active.textColor = Color.white;
                    _hideButtonStyle.normal.background = null;
                    _hideButtonStyle.hover.background = null;
                    _hideButtonStyle.active.background = null;
                    _hideButtonStyle.fontStyle = FontStyle.Bold;
                    _hideButtonStyle.padding = zeroRectOffset;
                    _hideButtonStyle.border = zeroRectOffset;
                    _hideButtonStyle.margin = zeroRectOffset;
                }

                if (UI.isRepaintStep())
                {
                    _hideButtonStyle.hover.background ??= UI.getTexture2D(Color.black);
                    _hideButtonStyle.active.background ??= UI.getTexture2D(Color.black);
                }

                return _hideButtonStyle;
            }
        }

        private static GUIStyle? _playerButtonStyle = null;
        private static GUIStyle playerButtonStyle
        {
            get
            {
                if (_playerButtonStyle == null)
                {
                    _playerButtonStyle = new GUIStyle();
                    _playerButtonStyle.fontSize = Vars.playerButtonsFontSize;
                    _playerButtonStyle.alignment = TextAnchor.MiddleCenter;
                    _playerButtonStyle.fontStyle = FontStyle.Bold;
                    _playerButtonStyle.normal.textColor = Color.gray;
                    _playerButtonStyle.hover.textColor = Color.white;
                    _playerButtonStyle.active.textColor = Color.white;
                    _playerButtonStyle.normal.background = null;
                    _playerButtonStyle.hover.background = null;
                    _playerButtonStyle.active.background = null;
                    _playerButtonStyle.border = zeroRectOffset;
                    _playerButtonStyle.margin = zeroRectOffset;
                    _playerButtonStyle.padding = zeroRectOffset;
                }

                if (UI.isRepaintStep())
                {
                    _playerButtonStyle.hover.background ??= UI.getTexture2D(Colors.transparent);
                    _playerButtonStyle.active.background ??= _playerButtonStyle.hover.background;
                }
                return _playerButtonStyle;
            }
        }

        private float qmWidth = 0f;
        private float burgerWidth = 0f;
        private float speedWidth = 0f;
        private float playWidth = 0f;
        private float positionWidth = 0f;
        private float footerWidth = 0f;
        private float loadWidth = 0;
        private string footerString = "ReplayMod " + Replay.VERSION;
        private string loadString = "Load";

        public void onGui()
        {
            if (qmWidth == 0) qmWidth = playerButtonStyle.CalcSize(new GUIContent("?")).x + 20f;
            if (burgerWidth == 0) burgerWidth = playerButtonStyle.CalcSize(new GUIContent("☰")).x + 20f;
            if (speedWidth == 0) speedWidth = playerLabelStyle.CalcSize(new GUIContent("-0.00x")).x + 10f;
            if (playWidth == 0) playWidth = playerButtonStyle.CalcSize(new GUIContent("▶")).x + 20f;
            if (positionWidth == 0) positionWidth = playerLabelStyle.CalcSize(new GUIContent(formatTime(0))).x + 10f;
            if (footerWidth == 0) footerWidth = boldLabelStyle.CalcSize(new GUIContent(footerString)).x + 20f;
            if (loadWidth == 0) loadWidth = playerButtonStyle.CalcSize(new GUIContent(loadString)).x + 20f;

            GUILayout.BeginArea(new Rect(0, Screen.height - Replay.bottomBarHeight, Screen.width, Replay.bottomBarHeight));

            GUILayout.FlexibleSpace();

            if (cfg_hideReplayControls_Value)
            {
                GUILayout.BeginVertical();
            }
            else
            {
                GUILayout.BeginVertical(UI.backgroundStyle);
            }

            // "bar"
            GUILayout.BeginHorizontal(GUILayout.Height(Replay.bottomBarHeight));

            // --------------------------
            // button which hides the bar
            // --------------------------

            GUILayoutOption[] options = new GUILayoutOption[] { GUILayout.Width(hideButtonWidth), GUILayout.Height(bottomBarHeight) };

            if (cfg_hideReplayControls_Value)
            {
                hideButtonStyle.hover.background = UI.getTexture2D(Color.black);
            }
            else
            {
                hideButtonStyle.hover.background = UI.getTexture2D(Colors.transparent);
            }
            if (GUILayout.Button(Replay.cfg_hideReplayControls_Value ? ">" : "<", hideButtonStyle, options))
            {
                Replay.cfg_hideReplayControls_Value = !Replay.cfg_hideReplayControls_Value;
                // hide menu too if it was open
                if (Replay.cfg_hideReplayControls_Value)
                {
                    toggleWantMenuOpen(Menu.ReplayHelp, false);
                    toggleWantMenuOpen(Menu.ReplayOptions, false);
                }
            }

            // -----------
            // menu button
            // -----------

            if (!Replay.cfg_hideReplayControls_Value)
            {
                playerButtonStyle.normal.textColor = Replay.wantMenuOpen(Menu.ReplayHelp) ? Colors.gold : Color.gray;
                playerButtonStyle.hover.textColor = Replay.wantMenuOpen(Menu.ReplayHelp) ? Colors.gold : Color.white;
                if (UI.horizontalButton("?", playerButtonStyle, qmWidth)) Replay.toggleWantMenuOpen(Menu.ReplayHelp);

                playerButtonStyle.normal.textColor = Replay.wantMenuOpen(Menu.ReplayOptions) ? Colors.gold : Color.gray;
                playerButtonStyle.hover.textColor = Color.white;
                if (UI.horizontalButton("☰", playerButtonStyle, burgerWidth)) toggleWantMenuOpen(Menu.ReplayOptions);

                playerButtonStyle.normal.textColor = Replay.wantMenuOpen(Menu.ReplayFilelist) ? Colors.gold : Color.gray;
                playerButtonStyle.hover.textColor = Replay.wantMenuOpen(Menu.ReplayFilelist) ? Colors.gold : Color.white;
                if (UI.horizontalButton(loadString, playerButtonStyle, loadWidth)) toggleWantMenuOpen(Menu.ReplayFilelist);

                // playspeed label
                string speedText = (lastEffectivePlaySpeed / 100f).ToString("F2") + "x";

                playerLabelStyle.normal.textColor = Color.white;
                UI.horizontalLabel(speedText, playerLabelStyle, speedWidth);

                // playspeed slider
                var sliderValue = mouseAction == MouseAction.TimeControl ? 0 : stickSpeed != null ? (float)stickSpeed : idlePlaySpeed;
                var saveValue = mouseAction != MouseAction.TimeControl && stickSpeed == null;
                float newSliderValue = UI.Slider(sliderValue, -100, 100, bottomBarHeight, 201);

                if (saveValue && newSliderValue != sliderValue)
                {
                    lastSliderSpeed = (int)newSliderValue;
                    idlePlaySpeed = (int)newSliderValue;
                }

                var isPaused = lastEffectivePlaySpeed == 0;
                playerButtonStyle.normal.textColor = isPaused ? Color.gray : Colors.playingGreenColor;
                playerButtonStyle.hover.textColor = isPaused ? Color.white : Colors.greenHoverColor;
                if (UI.horizontalButton("▶", playerButtonStyle, playWidth)) togglePlayPause();

                // position label
                string positionText = formatTime(virtualTime);
                playerLabelStyle.normal.textColor = Color.white;
                UI.horizontalLabel(positionText, playerLabelStyle, positionWidth);

                // positon slider
                float maxVirtualTime = frameGroups[0][^1].time;
                float newTime = UI.Slider(virtualTime, 0f, maxVirtualTime, bottomBarHeight);
                if (newTime != virtualTime) virtualTime = newTime;

                UI.horizontalLabel(footerString, Replay.boldLabelStyle, footerWidth);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // drag operations

        public Dictionary<DragButton, bool> mouseDown = new()
        {
            { DragButton.Left, true },
            { DragButton.Right, true }
        };

        public Dictionary<DragButton, Vector2?> dragStartPosition = new()
        {
            { DragButton.Left, null },
            { DragButton.Right, null }
        };

        public Dictionary<DragButton, Vector2?> dragEndPosition = new()
        {
            { DragButton.Left, null },
            { DragButton.Right, null }
        };

        public Dictionary<DragButton, bool> mouseButtonDown = new()
        {
            { DragButton.Left, false },
            { DragButton.Right, false }
        };

        public Dictionary<DragButton, Vector2> mouseStart = new() {
            { DragButton.Left, Vector2.zero },
            { DragButton.Right, Vector2.zero }
        };

        private Dictionary<DragButton, bool> mouseCrossedThreshold = new() {
            { DragButton.Left, false },
            { DragButton.Right, false }
        };

        public void update_drag(DragButton dragButton, Action<DragButton, Vector2> onStart, Action<DragButton, Vector2, Vector2> onMove, Action<DragButton, Vector2, Vector2> onEnd)
        {
            if (Mouse.current == null) return;

            var mouse = Mouse.current;
            var button = dragButton == DragButton.Left ? mouse.leftButton : mouse.rightButton;
            var isDown = button.isPressed;
            var wasDown = mouseDown[dragButton];

            if (isDown != wasDown)
            {
                mouseDown[dragButton] = isDown;
                if (isDown)
                {
                    // mousedown, store start position
                    mouseStart[dragButton] = mouse.position.ReadValue();
                    onStart(dragButton, mouseStart[dragButton]);
                }
                else
                {
                    // mouseup
                    var position = mouse.position.ReadValue();
                    var delta = position - mouseStart[dragButton];
                    onEnd(dragButton, position, delta);
                }
            }

            if (isDown)
            {
                // we're dragging
                var position = mouse.position.ReadValue();
                var delta = position - mouseStart[dragButton];
                onMove(dragButton, position, delta);
            }
        }

        public static class PlayerPatch
        {
            [HarmonyLib.HarmonyPatch(typeof(GameMode), "UseGameTimer")]
            [HarmonyLib.HarmonyPrefix]
            public static bool GameMode_UseGameTimer(GameMode __instance)
            {
                return false;
            }

            [HarmonyLib.HarmonyPatch(typeof(CinemachineBrain), "LateUpdate")]
            [HarmonyLib.HarmonyPrefix]
            public static bool CinemachineBrain_LateUpdate(CinemachineBrain __instance)
            {
                if (Replay.modState == Replay.ModState.PlayingReplay && Replay.cfg_cameraMode.Value != CameraMode.DISABLED)
                {
                    return false;
                }
                return true;
            }

            [HarmonyLib.HarmonyPatch(typeof(Il2CppAudio.AudioController), "Update")]
            [HarmonyLib.HarmonyPostfix]
            public static void AudioController_Update(Il2CppAudio.AudioController __instance)
            {
                Replay.player!.setAudioClipsPosition();
            }

            [HarmonyLib.HarmonyPatch(typeof(PauseManager), "OnRestartRound")]
            [HarmonyLib.HarmonyPrefix]
            public static bool PauseManager_OnRestartRound(PauseManager __instance)
            {
                // prevent this in replay mode since it messes things up.
                // go back to frame 0 instead.
                
                Replay.player!.returnToStart();
                __instance.SetPaused(false); // hide pause screen
                return false;
            }

            [HarmonyLib.HarmonyPatch(typeof(EffectsHandeler), "UpdateFaceState")]
            [HarmonyLib.HarmonyPrefix]
            public static bool EffectsHandeler_UpdateFaceState()
            {
                // we manage face state ourselves
                return false;
            }


            [HarmonyLib.HarmonyPatch(typeof(Actor), "set_actorState")]
            [HarmonyLib.HarmonyPrefix]

            // prevent the game from setting the actor state which causes sounds to play
            public static bool Actor_set_actorState(Actor __instance, ref Actor.ActorState value)
            {
                return false;
            }

            // take control of time speed

            [HarmonyLib.HarmonyPatch(typeof(TimeManager), "Update")]
            [HarmonyLib.HarmonyPrefix]
            public static bool TimeManager_Update()
            {
                // overwrite timeScale each time because it is reset each frame
                if (Replay.forceTimeScale != null)
                {
                    Time.timeScale = (float)Replay.forceTimeScale;
                    return false;
                }
                return true;
            }
        }

        public Actor? getActorAtScreenPosition(Vector2 position, bool nearestFallback)
        {
            // try a raycast first

            enableActorColliders();
            Ray ray = Camera.main.ScreenPointToRay(position);
            RaycastHit[] hits = Physics.RaycastAll(ray);
            disableActorColliders();

            foreach (RaycastHit hit in hits)
            {
                Actor? actor = hit.collider.GetComponentInParent<Actor>();
                if (actor != null && replayActors.Contains(actor))
                {
                    return actor;
                }
            }

            if (nearestFallback)
            {
                float nearestDistance = float.PositiveInfinity;
                Actor? nearestActor = null;

                // check positions of all actors on screen and return the one
                // that's closest to the clicked screen point

                foreach (var actor in replayActors)
                {
                    var actorScreenPos = Camera.main.WorldToScreenPoint(actor.bodyHandeler.Chest.PartTransform.position);
                    var distance = Vector2.Distance(new Vector2(actorScreenPos.x, actorScreenPos.y), position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestActor = actor;
                    }
                }

                return nearestActor;
            }

            return null;
        }

        private void enableActorColliders()
        {
            foreach (var actor in replayActors)
            {
                foreach (var collider in actor.gameObject.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = true;
                }
            }
        }

        private void disableActorColliders()
        {
            foreach (var actor in replayActors)
            {
                foreach (var collider in actor.gameObject.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
            }
        }

        public bool isGamepadYInverted()
        {
            return Replay.cfg_invertGamepadAxes.Value == AXIS_INVERSION.Y || Replay.cfg_invertGamepadAxes.Value == AXIS_INVERSION.XY;
        }

        public bool isGamepadXInverted()
        {
            return Replay.cfg_invertGamepadAxes.Value == AXIS_INVERSION.X || Replay.cfg_invertGamepadAxes.Value == AXIS_INVERSION.XY;
        }

        public bool isMouseXInverted()
        {
            return Replay.cfg_invertMouseAxes.Value == AXIS_INVERSION.X || Replay.cfg_invertMouseAxes.Value == AXIS_INVERSION.XY;
        }

        public bool isMouseYInverted()
        {
            return Replay.cfg_invertMouseAxes.Value == AXIS_INVERSION.Y || Replay.cfg_invertMouseAxes.Value == AXIS_INVERSION.XY;
        }


    }
}
