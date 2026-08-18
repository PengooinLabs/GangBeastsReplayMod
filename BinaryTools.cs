using UnityEngine;
using static PengooinLabs.ReplayMod.Types;

namespace PengooinLabs.ReplayMod
{
    public class Binary
    {
        public static void WriteVector3(BinaryWriter binaryWriter, Vector3 vector3)
        {
            binaryWriter.Write(vector3.x);
            binaryWriter.Write(vector3.y);
            binaryWriter.Write(vector3.z);
        }

        public static void WriteVector2(BinaryWriter binaryWriter, Vector2 vector2)
        {
            binaryWriter.Write(vector2.x);
            binaryWriter.Write(vector2.y);
        }

        public static Vector3 ReadVector3(BinaryReader binaryReader)
        {
            return new Vector3(
                binaryReader.ReadSingle(),
                binaryReader.ReadSingle(),
                binaryReader.ReadSingle()
            );
        }

        public static _2dAudioClip ReadSound2d(BinaryReader r, string clipName)
        {
            var clp = new _2dAudioClip();
            clp.time = r.ReadSingle();
            clp.name = clipName;
            clp.soundType = r.ReadInt32();
            clp.loop = r.ReadBoolean();
            clp.volume = r.ReadSingle();
            clp.pitch = r.ReadSingle();
            clp.delay = r.ReadSingle();
            return clp;
        }

        public static void WriteSound2d(BinaryWriter w, _2dAudioClip clp)
        {
            w.Write(clp.time);
            // clp.name was set earlier
            w.Write(clp.soundType);
            w.Write(clp.loop);
            w.Write(clp.volume);
            w.Write(clp.pitch);
            w.Write(clp.delay);
        }

        public static _3dAudioClip ReadSound3d(BinaryReader binaryReader, string clipName)
        {
            var clp = new _3dAudioClip();
            clp.name = clipName;
            clp.time = binaryReader.ReadSingle();
            clp.posX = binaryReader.ReadSingle();
            clp.posY = binaryReader.ReadSingle();
            clp.posZ = binaryReader.ReadSingle();
            clp.soundType = (int)binaryReader.ReadInt32();
            clp.loop = binaryReader.ReadBoolean();
            clp.volume = binaryReader.ReadSingle();
            clp.pitch = binaryReader.ReadSingle();
            clp.delay = binaryReader.ReadSingle();
            clp.minDistance = binaryReader.ReadSingle();
            clp.maxDistance = binaryReader.ReadSingle();
            clp.dopplerLevel = binaryReader.ReadSingle();
            clp.spatialBlendOverride = binaryReader.ReadSingle();
            return clp;
        }

        public static void WriteSound3d(BinaryWriter w, _3dAudioClip clp)
        {
            w.Write(clp.time);
            w.Write(clp.posX);
            w.Write(clp.posY);
            w.Write(clp.posZ);
            w.Write(clp.soundType);
            w.Write(clp.loop);
            w.Write(clp.volume);
            w.Write(clp.pitch);
            w.Write(clp.delay);
            w.Write(clp.minDistance);
            w.Write(clp.maxDistance);
            w.Write(clp.dopplerLevel);
            w.Write(clp.spatialBlendOverride);
        }

        public static Vector3 ReadVector2(BinaryReader binaryReader)
        {
            return new Vector2(
                binaryReader.ReadSingle(),
                binaryReader.ReadSingle()
            );
        }

        public static Glass.ShardContainerData ReadShardContainer(BinaryReader r)
        {
            var container = new Glass.ShardContainerData();
            container.key = r.ReadString();
            int shardCount = r.ReadInt32();
            container.meshData = new Glass.MeshData[shardCount];
            for (int i = 0; i < shardCount; i++)
            {
                container.meshData[i] = ReadShard(r);
            }
            return container;
        }

        public static Glass.MeshData ReadShard(BinaryReader r)
        {
            
            // read normals
            int normalsCount = r.ReadInt32();
            Vector3[] normals = new Vector3[normalsCount];
            for (int i = 0; i < normalsCount; i++)
            {
                normals[i] = ReadVector3(r);
            }

            // read vertices
            int verticesCount = r.ReadInt32();
            Vector3[] vertices = new Vector3[verticesCount];
            for (int i = 0; i < verticesCount; i++)
            {
                vertices[i] = Binary.ReadVector3(r);
            }

            // read uvs
            int uvCount = r.ReadInt32();
            Vector2[] uv = new Vector2[uvCount];
            for (int i = 0; i < uvCount; i++)
            {
                uv[i] = ReadVector2(r);
            }

            // read triangles
            int triangleCount = r.ReadInt32();
            int[] triangles = new int[triangleCount];
            for (int i = 0; i < triangleCount; i++)
            {
                triangles[i] = r.ReadInt32();
            }

            // read submeshes
            int subMeshCount = r.ReadInt32();
            int[][] subMeshIndices = new int[subMeshCount][];
            for (int i = 0; i < subMeshCount; i++)
            {
                int trianglesCount = r.ReadInt32();
                subMeshIndices[i] = new int[trianglesCount];

                for (int j = 0; j < trianglesCount; j++)
                {
                    subMeshIndices[i][j] = r.ReadInt32();
                }
            }

            // compose mesh data
            var meshData = new Glass.MeshData();
            meshData.triangles = triangles;
            meshData.submeshIndices = subMeshIndices;
            meshData.uv = uv;
            meshData.normals = normals;
            meshData.vertices = vertices;

            return meshData;
        }
    }
}
