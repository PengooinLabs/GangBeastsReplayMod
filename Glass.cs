using Il2Cpp;
using UnityEngine;

// takes care of the reconstruction of glass shards

namespace PengooinLabs.ReplayMod
{
    public class Glass
    {
        public class MeshData
        {
            // data needed to reconstruct shards
            public int[] triangles;
            public int[][] submeshIndices;
            public Vector2[] uv;
            public Vector3[] normals;
            public Vector3[] vertices;
        }

        // data structure for a window containing multiple shards

        public class ShardContainerData
        {
            public string key = "";
            public MeshData[] meshData;
        }

        // find all shard containers in the scene
        public static List<GameObject> getShardContainers()
        {
            // example names: GlassA_fractured, GlassB_fractured, GlassC_fractured
            // they contain shard gameObjects having a number as a name. those shard
            // gameObjects contain a transform for positioning and a MeshFilter defining
            // its shape.
                
            Dictionary<GameObject, bool> result = new();

            var fractureShards = Resources.FindObjectsOfTypeAll<FractureShard>();
            for (int i = 0; i < fractureShards.Length; i++)
            {
                var parent = fractureShards[i].gameObject.transform.parent;
                if (parent != null) result[parent.gameObject] = true;
            }

            // on incinerator, the doors sometimes have no shards, so the glass containers
            // won't be found in the upper block. we have to add them manually if needed

            if (Game.getSceneName() == "incinerator")
            {
                var doorGlass = Tools.getTransforms(new()
                {
                    "Scene/Geometry/Door (Windowed)/door_windowed (1)/door_frame_200cm/door_windowed/door_windowed_glassLower_fractured",
                    "Scene/Geometry/Door (Windowed)/door_windowed (1)/door_frame_200cm/door_windowed/door_windowed_glassUpper_fractured",
                    "Scene/Geometry/Door (Windowed)/door_windowed (2)/door_frame_200cm/door_windowed/door_windowed_glassLower_fractured",
                    "Scene/Geometry/Door (Windowed)/door_windowed (2)/door_frame_200cm/door_windowed/door_windowed_glassUpper_fractured",
                }, false);

                foreach (var container in doorGlass)
                {
                    if (!result.ContainsKey(container.gameObject))
                    {
                        result[container.gameObject] = true;
                    }
                }
            }

            return result.Keys.ToList();
        }

        public static void indexGlass()
        {
            var shardContainers = getShardContainers();
            
            foreach (var container in shardContainers)
            {
                int n = 0;

                var shardTransforms = Tools.getTransformChildren(container.transform);

                foreach (var shardTransform in shardTransforms)
                {
                    // record shards with fixed key Shard#N
                    Replay.tc.setFixedSingleTransformKey(shardTransform, "Shard#" + (n++));
                }
            }
        }

        // reconstructs a shard shape
        private static void synchronizeShardMeshData(GameObject shardObject, MeshData data)
        {
            // reconstruct a shard shape.
            var meshFilter = shardObject.GetComponent<MeshFilter>();
            var mesh = meshFilter.mesh;
            mesh.normals = data.normals;
            mesh.triangles = data.triangles;
            mesh.uv = data.uv;
            mesh.vertices = data.vertices;
            mesh.subMeshCount = data.submeshIndices.Length;
            for (int i = 0; i < data.submeshIndices.Length; i++)
            {
                mesh.SetTriangles(data.submeshIndices[i], i);
            }
            meshFilter.mesh.RecalculateNormals();
            meshFilter.mesh.RecalculateBounds();
            meshFilter.mesh.RecalculateTangents();
        }

        // make sure there's the same amount of shards in a container as in the recorded map
        private static void synchronizeGlassContainer(GameObject shardContainer, int count, GameObject? cloneShard)
        {
            // destroy superfluous shards
            while (shardContainer.transform.childCount > count)
            {
                UnityEngine.Object.DestroyImmediate(shardContainer.transform.GetChild(0).gameObject);
            }

            // create missing shards
            while (shardContainer.transform.childCount < count)
            {
                if (cloneShard == null) return; // safety
                GameObject newShard = UnityEngine.Object.Instantiate(cloneShard);
                newShard.transform.SetParent(shardContainer.transform, false);
                newShard.SetActive(true);
            }

            // rename/renumber all shards

            var childTransforms = Tools.getTransformChildren(shardContainer.transform);
            for (var i=0; i<childTransforms.Count; i++)
            {
                childTransforms[i].name = "Shard#" + i;
            }
        }

        public static void synchronizeGlass(Glass.ShardContainerData data, GameObject? cloneShard)
        {
            // find the referenced container by key
            var transformKey = data.key;
            var container = getShardContainers().Find(c => Replay.tc.getTransformInfo(c.transform).key == transformKey);
            if (container == null)
            {
                Replay.logError("failed to find shard container " + data.key + "!");
                return;
            }

            // how many shards there are
            var shardCount = data.meshData.Length;

            // assert needed amount of shard objects
            synchronizeGlassContainer(container, shardCount, cloneShard);

            // set mesh data of all shards
            for (int i = 0; i < container.transform.childCount; i++)
            {
                var shard = container.transform.GetChild(i).gameObject;
                synchronizeShardMeshData(shard, data.meshData[i]);
                shard.name = "Shard#" + i;
            }
        }
    }
}
