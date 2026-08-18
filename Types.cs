using Il2CppAudio;
using UnityEngine;
using static PengooinLabs.ReplayMod.TransformCache;

namespace PengooinLabs.ReplayMod
{
    public class Types
    {
        public enum MenuState { Unknown, LoadingReplay, PlayingReplay, Menu };

        public enum Menu { None, MenuHelp, MenuFileList, ReplayHelp, ReplayOptions, ReplayFilelist }

        public static Dictionary<string, string> colorToSpeakingName = new() {
            { "RGBA(0.033, 0.523, 0.847, 1.000)", "Blue"    },
            { "RGBA(0.667, 0.401, 0.275, 1.000)", "Brown"   },
            { "RGBA(0.219, 0.588, 0.219, 1.000)", "Green"   },
            { "RGBA(0.560, 0.745, 0.190, 1.000)", "Lime"    },
            { "RGBA(0.706, 0.208, 0.489, 1.000)", "Magenta" },
            { "RGBA(0.980, 0.470, 0.211, 1.000)", "Orange"  },
            { "RGBA(1.000, 0.569, 0.686, 1.000)", "Pink"    },
            { "RGBA(0.613, 0.336, 0.745, 1.000)", "Purple"  },
            { "RGBA(0.824, 0.048, 0.048, 1.000)", "Red"     },
            { "RGBA(0.745, 0.592, 0.482, 1.000)", "Tan"     },
            { "RGBA(0.138, 0.784, 0.784, 1.000)", "Teal"    },
            { "RGBA(0.902, 0.710, 0.000, 1.000)", "Yellow"  },
        };

        public static string getSpeakingNameOfColor(string colorString)
        {
            if (!colorToSpeakingName.ContainsKey(colorString))
            {
                return "";
            }
            return colorToSpeakingName[colorString];
        }

        public static string getSpeakingNameOfColor(Color color)
        {
            return getSpeakingNameOfColor(color.ToString());
        }

        public class ActorInfo
        {
            public Color primaryColor = Color.gray;
            public Color CostumeColor = Color.gray;
            public ushort[] costumeIdsWithColors = new ushort[0];
        }

        public class Frame
        {
            public CaptureGroupId groupId = 0;
            public float time;
            public Dictionary<string, ItemState> states = new Dictionary<string, ItemState>();
            public int index;
            public int aIndex = -1;
            public List<byte>? actorBlinks = null;
            public List<byte>? birdBlinks = null;
            public List<_3dAudioClip>? sounds3d;
            public List<_2dAudioClip>? sounds2d;
            public List<byte>? actorStates = null;
        }

        // state of a single item
        public class ItemState
        {
            public Vector3 pos = Vector3.zero;
            public Vector3 lscale = Vector3.zero;
            public Vector3 rot = Vector3.zero;
            public bool active = false;
        }

        public enum CameraMode
        {
            DISABLED = 0,
            SURROUND = 1,
        }

        // indicators for what's coming next in replay data
        public enum INDICATORS : byte
        {
            ACTORINFO = 0,
            FRAME = 1,

            // inside frame:
            ITEM_STATE = 2,
            TRANSFORMKEY = 3,

            // separately again
            SHARDCONTAINER = 4,
            SOUNDKEY = 5,
            SOUND2D = 6,
            SOUND3D = 7,

            BIRDBLINKS = 8,
        }

        // indicators what data is included for a single item
        public enum CONTAINS
        {
            POSITION = 1,
            ACTIVE = 2,
            LOCALSCALE = 4,
            ROTATION = 8,
        }

        public class _3dAudioClip
        {
            public float time = 0;
            public string name = "";
            public float posX = 0f;
            public float posY = 0f;
            public float posZ = 0f;
            public int soundType = (int)VolumeLevels.SoundType.SFX;
            public bool loop = false;
            public float volume = 1f;
            public float pitch = 1f;
            public float delay = 0f;
            public float minDistance = 1f;
            public float maxDistance = 500f;
            public float dopplerLevel = 0f;
            public float spatialBlendOverride = 1f;
        }

        public class _2dAudioClip
        {
            public float time = 0;
            public string name = "";
            public int soundType = (int)VolumeLevels.SoundType.SFX;
            public bool loop = false;
            public float volume = 1f;
            public float pitch = 1f;
            public float delay = 0f;
        }

        public enum Accuracy
        {
            FPS30 = 30,
            FPS60 = 60,
            FPS120 = 120
        }

        public enum CaptureGroupId : byte
        {
            Actor = 0,
            Environment = 1,
            INTERMEDIATE = 255
        }

        public class CaptureGroup
        {
            // unique series id, 0 = master
            public CaptureGroupId id = 0;

            // this holds the keys and transform infos to all transforms whose
            // state will be captured. can be static or get updated using
            // updateTransformInfos() callback
            public Dictionary<string, TransformInfo> observed = new();

            // the rate (seconds) at which the transform states will be captured
            public float rate = 0f;

            // helper to determine when it's time to capture
            public float accumulator = 0f;

            // if set, will be called before transformInfos are read for
            // comparison. used to update actor transforms each frame
            // until we found a workaround for the costume stringing effects
            // when an actor dies.
            public Action<CaptureGroup>? update;
        }

        public enum AXIS_INVERSION { NO,X,Y,XY }
        public enum RenderOn { Update,LateUpdate }
        public enum DragButton { Left = 1, Right = 2, Time = 1, Camera = 2 }
        public enum DragDisable { No = 0, Left = 1, Right = 2, Both = 3 }
        
    }
}
