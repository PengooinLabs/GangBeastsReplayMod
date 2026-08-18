using UnityEngine;

namespace PengooinLabs.ReplayMod
{
    // this indexes transforms by their path and assignes them a unique key,
    // takes duplicate path names into account and works around them.

    public class TransformCache
    {
        public class TransformInfo
        {
            public Transform transform;
            public string key;
            public byte detectedBy = 0;

            public TransformInfo(Transform transform, string key)
            {
                // key can be supplied manually (for example for audioclips), otherwise it will be composed
                this.transform = transform;
                this.key = key != "" ? key : String.Join('/', Replay.tc.GetTransformPath(transform));
            }
        }

        public void clear()
        {
            cachedSingleTransformKeys.Clear();
            cachedTransformInfos.Clear();
            cachedTransformInfos_string.Clear();
            cachedTransformPaths.Clear();
        }

        public Dictionary<Transform, List<string>> cachedTransformPaths = new();
        public Dictionary<Transform, string> cachedSingleTransformKeys = new();
        public Dictionary<Transform, TransformInfo> cachedTransformInfos = new();
        public Dictionary<string, TransformInfo> cachedTransformInfos_string = new();
     
        public String GetSingleTransformKey(Transform transform)
        {
            if (!cachedSingleTransformKeys.ContainsKey(transform))
            {
                var siblings = transform.parent != null ? Tools.getTransformChildren(transform.parent) : Tools.getRootTransforms();
                var sameName = siblings.Where(t => t.name == transform.name).ToList();

                if (sameName.Count > 1)
                {
                    for (int i = 0; i < sameName.Count; i++)
                    {
                        // might overwrite, but that doesn't matter
                        cachedSingleTransformKeys[sameName[i]] = transform.name + "#" + i;
                    }
                }
                else
                {
                    cachedSingleTransformKeys[transform] = transform.name;
                }
            }

            return cachedSingleTransformKeys[transform];
        }

        public void addTransforms(List<Transform> ts)
        {
            foreach (var t in ts) getTransformInfo(t);
        }

        public void setFixedSingleTransformKey(Transform transform, string key)
        {
            cachedSingleTransformKeys[transform] = key;
        }

        public static string GetRealPath(Transform transform)
        {
            List<string> parts = new List<string>();

            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        public TransformInfo addTransformWithKey(Transform transform, string key)
        {
            var info = new TransformInfo(transform, key);
            cachedTransformInfos[transform] = info;
            cachedTransformInfos_string[info.key] = info;
            return info;
        }

        public void refresh()
        {
            clear();
            var ts = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in ts) getTransformInfo(t);
        }

        public TransformInfo getTransformInfo(Transform transform)
        {
            if (cachedTransformInfos.ContainsKey(transform)) return cachedTransformInfos[transform];
            var info = new TransformInfo(transform, "");
            cachedTransformInfos[transform] = info;
            cachedTransformInfos_string[info.key] = info;
            return info;
        }

        public TransformInfo? query(string key)
        {
            if (cachedTransformInfos_string.ContainsKey(key))
            {
                var info = cachedTransformInfos_string[key];
                if (info.transform != null) return info;
                // remove nulled transforms from lookup
                cachedTransformInfos_string.Remove(key);
            }
            return null;
        }

        public List<string> GetTransformPath(Transform transform)
        {
            if (cachedTransformPaths.ContainsKey(transform)) return cachedTransformPaths[transform];

            List<string> path = new List<string>();

            Transform t = transform;
            while (t != null)
            {
                path.Insert(0, GetSingleTransformKey(t));
                t = t.parent;
            }

            cachedTransformPaths[transform] = path;
            return path;
        }
    }
}
