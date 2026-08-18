using Il2CppFemur;
using Il2CppSuperGenius.Lib.Burst.Easing;
using UnityEngine;

namespace PengooinLabs.ReplayMod
{
    public class ReplayCamera
    {
        public ReplayCamera()
        {
            lrLoop.speed = 3f;
            udLoop.speed = 5f;
            distanceLoop.speed = 14f;
        }

        public enum CameraTarget { Helper, Chest, Head }

        private Actor? _focusedActor = null;
        public Actor? focusedActor { get { return _focusedActor; } }

        public void setFocusedActor(Actor actor)
        {
            if (actor != focusedActor)
            {
                var wasNull = focusedActor == null;
                _focusedActor = actor;
                if (!wasNull)
                {
                    // transition only from one actor to another. when the map starts,
                    // there is no currently focused player and we don't want a transition.
                    setCameraTransitionFrom(Camera.main.transform.position, Camera.main.transform.rotation);
                }
                else
                {
                    update(0);
                }
            }
        }

        public float getLR()
        {
            return lrLoop.targetEulers.y;
        }

        public float getUD()
        {
            return udLoop.targetEulers.x;
        }

        public void setLR(float lr, bool immediate)
        {
            lrLoop.targetEulers.y = clampLeftRight(lr);
            if (immediate) lrLoop.currentEulers.y = lrLoop.targetEulers.y;
        }

        public void setUD(float ud, bool immediate)
        {
            udLoop.targetEulers.x = clampUpDown(ud);
            if (immediate) udLoop.currentEulers.x = udLoop.targetEulers.x;
        }

        // set desired camera distance
        public void setDistance(float distance, bool immediate)
        {
            distanceLoop.target.x = clampDistance(distance);
            if (immediate) distanceLoop.current.x = distanceLoop.target.x;
        }

        public float getDistance()
        {
            return distanceLoop.target.x;
        }



        // driven manually
        public void update(float deltaTime)
        {
            lrLoop.tick(deltaTime);
            udLoop.tick(deltaTime);
            distanceLoop.tick(deltaTime);

            Actor? fallbackActor = focusedActor;
            Actor? toFocus = focusedActor;

            if (toFocus != null && toFocus.actorState == Actor.ActorState.Dead && Replay.cfg_switchCameraOnActorDeath.Value)
            {
                // actor died, have to find a new one
                if (Replay.player != null)
                {
                    toFocus = Replay.player.getNextClosestActor(toFocus);
                }
            }

            if (toFocus == null)
            {
                var replayActors = Replay.player!.getActors();
                // find new actor to focus
                for (int i = 0; i < replayActors.Count; i++)
                {
                    if (replayActors[i].actorState != Actor.ActorState.Dead)
                    {
                        toFocus = replayActors[i];
                        break;
                    }
                }
            }

            if (toFocus == null)
            {
                // go back to initial actor since no other was found
                toFocus = fallbackActor;
            }

            if (toFocus != null)
            {

                setFocusedActor(toFocus);

                var lookAtPos = Replay.cfg_camera_target.Value == CameraTarget.Chest ?
                    toFocus.bodyHandeler.Chest.PartTransform.position :
                    Replay.cfg_camera_target.Value == CameraTarget.Head ?
                    toFocus.bodyHandeler.Head.PartTransform.position :
                    toFocus.bodyHandeler.CameraTarget.PartTransform.position;

                if (cameraTransitionStartPos != null)
                {
                    if (transitionTimeLeft == -1)
                    {
                        transitionTimeLeft = 0.75f;
                        transitionTime = transitionTimeLeft;
                    }
                    else
                    {
                        transitionTimeLeft -= Time.unscaledDeltaTime;
                        if (transitionTimeLeft < 0) transitionTimeLeft = 0;
                    }

                    // lerp
                    float ratio = 1f - (transitionTimeLeft / transitionTime);
                    ratio = EaseFuctions.EaseInOutSine(0, 1, ratio);

                    if (ratio < 1)
                    {
                        var (camPos, camRot) = getLookatCameraPosition(lookAtPos);
                        var pos = Vector3.Lerp((Vector3)cameraTransitionStartPos, camPos, ratio);
                        var rot = Quaternion.Lerp((Quaternion)cameraTransitionStartRot!, camRot, ratio);
                        Camera.main.transform.position = pos;
                        Camera.main.transform.rotation = rot;
                        return;
                    }

                    // lerping ended
                    transitionTime = -1f;
                    transitionTimeLeft = -1f;
                    cameraTransitionStartPos = null;
                    cameraTransitionStartRot = null;
                }

                lookAt(lookAtPos);
            }
        }

        public static GameObject? helperGO = null;
        public static Transform? _helperTransform = null;

        public static Transform helperTransform
        {
            get
            {
                if (_helperTransform == null || helperGO == null)
                {
                    helperGO = new GameObject("helperTransform");
                    _helperTransform = helperGO.transform;
                }
                return _helperTransform;
            }
        }
       
        public (Vector3, Quaternion) getLookatCameraPosition(Vector3 lookAt)
        {
            float y = clampLeftRight(lrLoop.currentEulers.y);
            float x = clampUpDown(udLoop.currentEulers.x);
            float distance = clampDistance(distanceLoop.current.x);
            var pos = Quaternion.Euler(x, y, 0f) * Vector3.up * distance + lookAt;
            helperTransform.position = pos;
            if (Replay.cfg_camera_target.Value == CameraTarget.Helper) lookAt += new Vector3(0, 0.2f, 0);
            helperTransform.LookAt(lookAt);
            return (pos, helperTransform.rotation);
        }

        // set camera position
        private void lookAt(Vector3 lookAt)
        {
            var (camPos,camRot) = getLookatCameraPosition(lookAt);
            Camera.main.transform.position = camPos;
            Camera.main.transform.rotation = camRot;
        }

        public RotationLoop udLoop = new RotationLoop();
        public RotationLoop lrLoop = new RotationLoop();
        public PositionLoop distanceLoop = new PositionLoop();

        // transition from player to player
        public float transitionTimeLeft = -1f;
        public float transitionTime = -1f;
        public Vector3? cameraTransitionStartPos = null;
        public Quaternion? cameraTransitionStartRot = null;
        public float cameraTransitionTime = 0.6f; // TODO derive from distance?

        public void setCameraTransitionFrom(Vector3 position, Quaternion rotation)
        {
            cameraTransitionStartPos = position;
            cameraTransitionStartRot = rotation;
            transitionTimeLeft = -1f;
        }

        private static float minCamDistance = 2f;

        public static float clampDistance(float dist)
        {
            return dist >= minCamDistance ? dist : minCamDistance;
        }

        private static float clampLeftRight(float angle)
        {
            angle %= 360f;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        private static float clampUpDown(float angle)
        {
            // return ranges from 1 to 179 to avoid flipping control directions
            if (angle < 1f) return 1f;
            if (angle > 179f) return 179f;
            return angle;
        }
    }
}
