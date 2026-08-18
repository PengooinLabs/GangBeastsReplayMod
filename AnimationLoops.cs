using UnityEngine;

namespace PengooinLabs.ReplayMod
{
    public class PositionLoop
    {
        public Vector3 current = Vector3.zero;
        public Vector3 target = Vector3.zero;
        public float speed = 3f;

        public float minThreshold = 0.2f;

        public void tick(float deltaTime)
        {
            if (
                target.x != current.x ||
                target.y != current.y ||
                target.z != current.z
            )
            {
                float fraction = speed * deltaTime;
                
                float curX = current.x;
                float curY = current.y;
                float curZ = current.z;

                float targetX = target.x;
                float targetY = target.y;
                float targetZ = target.z;

                float deltaX = targetX - curX;
                float deltaY = targetY - curY;
                float deltaZ = targetZ - curZ;

                if (deltaX < 0)
                {
                    if (deltaX > -minThreshold) deltaX = -minThreshold;
                }
                else if (deltaX > 0)
                {
                    if (deltaX < minThreshold) deltaX = minThreshold;
                }

                if (deltaY < 0)
                {
                    if (deltaY > -minThreshold) deltaY = -minThreshold;
                }
                else if (deltaY > 0)
                {
                    if (deltaY < minThreshold) deltaY = minThreshold;
                }

                if (deltaZ < 0)
                {
                    if (deltaZ > -minThreshold) deltaZ = -minThreshold;
                }
                else if (deltaZ > 0)
                {
                    if (deltaZ < minThreshold) deltaZ = minThreshold;
                }

                if (curX != targetX)
                {
                    bool xWasSmaller = curX < targetX;
                    curX += deltaX * fraction;
                    if (xWasSmaller) {
                        if (curX > targetX) curX = targetX;
                    } else {
                        if (curX < targetX) curX = targetX;
                    }
                }

                if (curY != targetY)
                {
                    bool yWasSmaller = curY < targetY;
                    curY += deltaY * fraction;
                    if (yWasSmaller) {
                        if (curY > targetY) curY = targetY;
                    } else {
                        if (curY < targetY) curY = targetY;
                    }
                }

                if (curZ != targetZ)
                {
                    bool zWasSmaller = curZ < targetZ;
                    curZ += deltaZ * fraction;
                    if (zWasSmaller) {
                        if (curZ > targetZ) curZ = targetZ;
                    } else {
                        if (curZ < targetZ) curZ = targetZ;
                    }
                }

                current.x = curX;
                current.y = curY;
                current.z = curZ;
            }
        }
    }

    public class RotationLoop
    {
        public Vector3 currentEulers = Vector3.zero;
        public Vector3 targetEulers = Vector3.zero;

        // degrees per second
        public float speed = 90f;

        private float clampAngle(float angle)
        {
            angle %= 360;
            if (angle < 0) angle += 360;
            return angle;
        }

        private float getShortRotation(float from, float to)
        {
            from = clampAngle(from);
            to = clampAngle(to);

            if (to > from)
            {
                if (to - from <= 180) return to - from;
                return -(360 - to + from);
            }

            if (from - to <= 180) return -(from - to);
            return 360 - from + to;
        }

        public void tick(float deltaT)
        {
            if (
                currentEulers.x != targetEulers.x ||
                currentEulers.y != targetEulers.y ||
                currentEulers.z != targetEulers.z
            )
            {
                float curX = currentEulers.x;
                float curY = currentEulers.y;
                float curZ = currentEulers.z;

                float targetX = targetEulers.x;
                float targetY = targetEulers.y;
                float targetZ = targetEulers.z;

                float deltaX = getShortRotation(curX, targetX);
                float deltaY = getShortRotation(curY, targetY);
                float deltaZ = getShortRotation(curZ, targetZ);

                float fraction = speed * deltaT;

                if (curX != targetX)
                {
                    bool xWasSmaller = curX < targetX;
                    curX += deltaX * fraction;
                    if (xWasSmaller)
                    {
                        if (curX > targetX) curX = targetX;
                    }
                    else
                    {
                        if (curX < targetX) curX = targetX;
                    }
                }

                if (curY != targetY)
                {
                    bool yWasSmaller = curY < targetY;
                    curY += deltaY * fraction;
                    if (yWasSmaller)
                    {
                        if (curY > targetY) curY = targetY;
                    }
                    else
                    {
                        if (curY < targetY) curY = targetY;
                    }
                }

                if (curZ != targetZ)
                {
                    bool zWasSmaller = curZ < targetZ;
                    curZ += deltaZ * fraction;
                    if (zWasSmaller)
                    {
                        if (curZ > targetZ) curZ = targetZ;
                    }
                    else
                    {
                        if (curZ < targetZ) curZ = targetZ;
                    }
                }

                currentEulers.x = clampAngle(curX);
                currentEulers.y = clampAngle(curY);
                currentEulers.z = clampAngle(curZ);
            }
        }
    }
}
