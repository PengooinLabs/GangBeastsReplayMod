using Il2CppGB.Game.Critters;
using UnityEngine;
using UnityEngine.SceneManagement;
using static PengooinLabs.ReplayMod.Player;

namespace PengooinLabs.ReplayMod
{
    public class Tools
    {
        public static List<Transform> getRootTransforms()
        {
            return SceneManager.GetActiveScene().GetRootGameObjects().ToList().ConvertAll<Transform>(o => o.transform);
        }

        public static List<Transform> getTransformChildren(Transform t)
        {
            var children = new List<Transform>();
            for (int i = 0; i < t.childCount; i++)
            {
                children.Add(t.GetChild(i));
            }
            return children;
        }

        public static Transform? getDirectChildTansform(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        public static bool isGrayColor(Color c)
        {
            return c.r == c.g && c.g == c.b;
        }

        public static List<Transform> findTransformsOfComponents(string root, List<Il2CppSystem.Type> componentTypes)
        {
            var go = GameObject.Find(root);
            if (go == null)
            {
                Replay.logError("GameObject " + root + " not found");
                return new List<Transform>();
            }

            Dictionary<Transform, bool> hash = new();
            foreach (var componentType in componentTypes)
            {
                foreach (var component in go.GetComponentsInChildren(componentType)) {
                    hash[component.transform] = true;
                }
            }
            return hash.Keys.ToList();
        }

        public static List<Transform> getTransforms(List<string> paths, bool withChildren)
        {
            List<Transform> result = new();
            foreach (var path in paths)
            {
                var go = GameObject.Find(path);
                if (go != null)
                {
                    if (withChildren)
                    {
                        result.AddRange(go.GetComponentsInChildren<Transform>().ToList());

                    }
                    else
                    {
                        result.Add(go.transform);
                    }
                }
                else
                {
                    Replay.logError("transform " + path + " not found");
                }
            }
            return result;
        }

        // sort functions

        // sorts filenames for the replay file menu:
        public static FilenameSorter filenameSorter = new();
        public class FilenameSorter : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            }
        }


        public static BirdSorter birdSorter = new();
        public class BirdSorter : IComparer<BirdActor>
        {
            public int Compare(BirdActor a, BirdActor b)
            {
                var aKey = Replay.tc.getTransformInfo(a.transform).key;
                var bKey = Replay.tc.getTransformInfo(b.transform).key;
                return String.Compare(aKey, bKey);
            }
        }

        public static NextActorSorter nextActorSorter = new();
        public class NextActorSorter : IComparer<SortableNextActor>
        {
            public int Compare(SortableNextActor a, SortableNextActor b)
            {
                if (a.screenDistanceX < b.screenDistanceX) return -1;
                if (a.screenDistanceX > b.screenDistanceX) return 1;
                return 0;
            }
        }

        public static NextClosestActorSorter nextClosestActorSorter = new();
        public class NextClosestActorSorter : IComparer<SortableNextClosestActor>
        {
            public int Compare(SortableNextClosestActor a, SortableNextClosestActor b)
            {
                if (a.distance < b.distance) return -1;
                if (a.distance > b.distance) return 1;
                return 0;
            }
        }

        public static bool? isLastGuiRectHovered()
        {
            var lastRect = getLastGuiRect();
            if (lastRect == null) return null;
            return ((Rect)lastRect).Contains(Event.current.mousePosition); 
        }

        public static Rect? getLastGuiRect()
        {
            EventType eventType = Event.current.type;
            if (eventType != EventType.Layout && eventType != EventType.Used)
            {
                return GUILayoutGroup_GetLast(GUILayoutUtility.current.topLevel);
            }
            return null;
        }

        private static Rect GUILayoutGroup_GetLast(GUILayoutGroup _this)
        {
            bool flag = _this.m_Cursor == 0;
            Rect rect;
            if (flag)
            {
                bool flag2 = Event.current.type == EventType.Repaint;
                rect = GUILayoutEntry.kDummyRect;
            }
            else
            {
                bool flag3 = _this.m_Cursor <= _this.entries.Count;
                if (flag3)
                {
                    GUILayoutEntry guilayoutEntry = _this.entries[_this.m_Cursor - 1];
                    rect = guilayoutEntry.rect;
                }
                else
                {
                    rect = GUILayoutEntry.kDummyRect;
                }
            }
            return rect;
        }

        public static T cycleOption<T>(List<T> options, T current)
        {
            int index = options.IndexOf(current);
            if (index == -1) return options[0];
            return options[(index + 1) % options.Count];
        }

        public static bool isRectHovered(Rect rect)
        {
            return rect.Contains(Event.current.mousePosition);
        }

        public static bool isRectClicked(Rect rect)
        {
            return Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition);
        }

        public static float getStyleLineHeight(GUIStyle style)
        {
            return style.CalcSize(new GUIContent("X")).y;
        }

        // substitute mechanism for OnFixedUpdate when time is frozen

        // called at 30fps:
        public static event Action<float>? onSubstFixedUpdate;

        private static float fixedUpdateInterval = 1f / 30f;
        private static float timeAccumulator = 0f;

        public static void update()
        {
            timeAccumulator += Time.unscaledDeltaTime;
            if (timeAccumulator >= fixedUpdateInterval)
            {
                while (timeAccumulator >= fixedUpdateInterval)
                {
                    timeAccumulator -= fixedUpdateInterval;
                }
                if (onSubstFixedUpdate != null) onSubstFixedUpdate(fixedUpdateInterval);
            }
        }

        private class UTimer
        {
            public float timeLeft = 0;
            public Action callback;
            public UTimer(float timeLeft, Action callback)
            {
                this.timeLeft = timeLeft;
                this.callback = callback;
            }

            public bool tick(float deltaTime)
            {
                timeLeft -= deltaTime;
                if (timeLeft <= 0)
                {
                    return true;
                }
                return false;
            }
        }

        private static Dictionary<string, UTimer>? utimers = null;

        public static void setTimeout(string id, float t, Action action)
        {
            utimers ??= new();
            utimers[id] = new UTimer(t, action);
        }

        public static void tickTimers(float deltaTime)
        {
            if (utimers == null) return;

            List<string>? removeIds = null;

            foreach (var entry in utimers)
            {
                var utimer = entry.Value;
                if (utimer.tick(deltaTime))
                {
                    removeIds ??= new();
                    removeIds.Add(entry.Key);
                }
            }

            if (removeIds != null)
            {
                List<Action> callbacks = new();

                foreach (var id in removeIds)
                {
                    callbacks.Add(utimers[id].callback);
                    utimers.Remove(id);
                }

                if (utimers.Count == 0) utimers = null;
                foreach (var callback in callbacks) callback();
            }
        }

    }
}
