using Il2CppInterop.Runtime;
using Il2CppCoreNet.StateSync.Syncs;
using System.Runtime.InteropServices;
using UnityEngine;
using Il2CppFemur;
using static PengooinLabs.ReplayMod.Replay;
using static PengooinLabs.ReplayMod.Types;
using static PengooinLabs.ReplayMod.TransformCache;
using static PengooinLabs.ReplayMod.Tools;
using Il2CppGB.Game.Critters;
using Il2Cpp;
using System.Text.RegularExpressions;
using Il2CppAudio;
using Il2CppCoreNet.Components.Client;
using Il2CppCoreNet.Messaging.Messages;
using UnityEngine.Audio;
using UnityEngine.Networking;

namespace PengooinLabs.ReplayMod
{
    public class Recorder
    {
        private List<CaptureGroup> captureGroups = new();
        private Dictionary<CaptureGroupId,CaptureGroup> captureGroupHash = new();
        public bool updateActorTransforms = true;
        public Recorder(string filePath)
        {
            // create binaryWriter for replay data to be written to disk
            var bufferedStream = new BufferedStream(File.Open(filePath, FileMode.Create));
            writer = new BinaryWriter(bufferedStream);

            // create a capture group for the actors. they are captured at 120fps maximum
            // and rate limited by the screen's refresh rate
            
            var actorGroup = new CaptureGroup() { id = 0, rate = 1f / 120f };
            captureGroups.Add(actorGroup);

            actorGroup.update = (CaptureGroup group) =>
            {
                // only update if we have to
                if (!updateActorTransforms) return;
                updateActorTransforms = false;

                group.observed.Clear();
                int n = 0;
                foreach (var actor in recordingActors) observeActor(actor, n++);
            };

            // create a seaparate group for the environment, captured at 30fps
            var environmentCaptureGroup = new CaptureGroup() { id = CaptureGroupId.Environment, rate = 1f / 30f };
            captureGroups.Add(environmentCaptureGroup);

            foreach (var group in captureGroups) captureGroupHash[group.id] = group;
        }

        // point in time we started recording, becomes virtualTime = 0 in the replay
        private float recordStartTime = -1;
        
        // counter to help setting frame.index
        public int nextFrameIndex = 0;

        // indexes/maps for transform- and sound clip name, avoiding reundant data
        private Dictionary<string, int> transformKeyToIndex = new();
        private Dictionary<string, int> soundNameToIndex = new();

        // tracks whether a certain gameObject was active last frame
        private Dictionary<string, bool> lastTransformActiveStates = new();

        // regexes for shards
        public Regex rShards = new Regex(@"/Shard#\d+");
        public Regex rNodes = new Regex(@"Nodes");

        // incrementing ids for certain stuff
        private int nextTransformKeyIndex = 0;
        private int nextSoundId = 0;

        // writing to disk
        private BinaryWriter? writer = null;

        // captured frames
        private List<Frame> frames = new();

        // audio clips played within 
        private List<_3dAudioClip> thisFrame3dAudio = new();
        private List<_2dAudioClip> thisFrame2dAudio = new();

        // check in OnUpdate after new actors (wave ai) was created
        private Dictionary<Actor, bool> onupdate_toAddActors = new();

        // list of actors being recorded
        private List<Actor> recordingActors = new();

        // list of birds being recorded (for blinks)
        private List<BirdActor>? recordingBirds = null;

        // information about initial playrs (human controlled)
        private List<ActorInfo> actorInfos = new();

        // list of actors added later on (wave actors)
        private List<Actor> lateActors = new();

        // collect a played 2d sound
        public void pushSound2d(_2dAudioClip clp)
        {
            if (recordStartTime == -1) return;
            float time = Time.time - recordStartTime;
            clp.time = time;
            thisFrame2dAudio.Add(clp);
        }

        // collect a played 3d sound
        public void pushSound3d(_3dAudioClip clp)
        {
            if (recordStartTime == -1) return;
            float time = Time.time - recordStartTime;
            clp.time = time;
            thisFrame3dAudio.Add(clp);
        }

        private List<Frame> getStateFrames(bool isStart)
        {
            // create a new frame
            List<Frame> frames = new();

            // time to set on each frame series
            var frameTime = Time.time - recordStartTime;

            // note: the frequencies can drift over time due to floating point
            // errors, but it's irrelevant.

            for (int g=0; g<captureGroups.Count; g++)
            {
                var group = captureGroups[g];
                group.accumulator += Time.deltaTime;

                if (isStart || group.accumulator >= group.rate)
                {
                    // create a frame for this series
                    // index is set externally
                    Frame frame = new Frame();
                    frame.groupId = group.id;
                    frame.time = frameTime;
                    frame.index = getNextFrameIndex(frame.groupId);
                    
                    while (group.accumulator >= group.rate) group.accumulator -= group.rate;

                    Dictionary<TransformInfo, bool> destroyedTransforms = new();

                    if (group.update != null) group.update(group);

                    var observedTransforms = group.observed;

                    foreach (var entry in observedTransforms)
                    {
                        var key = entry.Key;
                        var info = entry.Value;
                        if (info.transform == null)
                        {
                            // mark it for removal after finishing the loop
                            destroyedTransforms[info] = true;

                            // if a transform / gameObject was removed, we have to add a
                            // deactivated-state to the recording, or else the object
                            // will never get updated again and remain in its last
                            // recorded state during replay.

                            var removedState = new ItemState();
                            removedState.pos = Vector3.zero;
                            removedState.rot = Quaternion.identity.eulerAngles;
                            removedState.lscale = Vector3.one;
                            removedState.active = false;
                            frame.states[info.key] = removedState;
                        }
                        else
                        {
                            var state = getTransformState(info.transform);
                            // if the key is registered as a broken shard key, force inactive state,
                            // so it won't stick around in the replay. the game does this differently.
                            if (brokenShards.ContainsKey(info.key)) state.active = false;
                            frame.states[key] = state;
                        }
                    }

                    // remove destroyed transforms from observed transforms
                    foreach (var entry in destroyedTransforms) observedTransforms.Remove(entry.Key.key);

                    if (group.id == CaptureGroupId.Environment)
                    {
                        // add actors data

                        var actorBlinks = new List<byte>();

                        for (int i = 0; i < recordingActors.Count; i++)
                        {
                            // blink
                            actorBlinks.Add(Game.getActorBlink(recordingActors[i]));
                        }

                        var actorStates = new List<byte>();

                        for (int i = 0; i < recordingActors.Count; i++)
                        {
                            // actorState
                            actorStates.Add((byte)recordingActors[i].actorState);
                        }



                        if (lastFrameWithActorStates == null || bytesChanged(actorStates, lastFrameWithActorStates.actorStates!))
                        {
                            // include changed actorStates
                            frame.actorStates = actorStates;
                            lastFrameWithActorStates = frame;
                        }

                        // add actor blinks if they changed

                        if (lastFrameWithActorBlinks == null || bytesChanged(actorBlinks, lastFrameWithActorBlinks.actorBlinks!))
                        {
                            frame.actorBlinks = actorBlinks;
                            lastFrameWithActorBlinks = frame;
                        }

                        if (birdBlinksDirty)
                        {
                            // include bird blinks
                            frame.birdBlinks = new();
                            frame.birdBlinks.AddRange(birdBlinks);
                            birdBlinksDirty = false;
                        }
                    }
                    else if (group.id == CaptureGroupId.Actor)
                    {
                        // add sounds that were played since last frame

                        if (thisFrame3dAudio.Count > 0)
                        {
                            frame.sounds3d = thisFrame3dAudio;
                            thisFrame3dAudio = new();
                        }

                        if (thisFrame2dAudio.Count > 0)
                        {
                            frame.sounds2d = thisFrame2dAudio;
                            thisFrame2dAudio = new();
                        }
                    }

                    frames.Add(frame);
                }
            }

            return frames;
        }

        public static bool bytesChanged(List<byte> a, List<byte> b)
        {
            if (a.Count != b.Count) return true;
            for (int i=0; i< a.Count; i++)
            {
                if (a[i] != b[i]) return true;
            }
            return false;
        }

        public Frame? lastFrameWithActorBlinks;
        public Frame? lastFrameWithActorStates;


        private Dictionary<string, ItemState> lastPushedItemStates = new();

        private Dictionary<CaptureGroupId, int> _nextFrameIndexes = new();

        public int peekNextFrameIndex(CaptureGroupId captureGroupId)
        {
            if (!_nextFrameIndexes.ContainsKey(captureGroupId)) return 0;
            return _nextFrameIndexes[captureGroupId];
        }

        public int getNextFrameIndex(CaptureGroupId captureGroupId)
        {
            if (!_nextFrameIndexes.ContainsKey(captureGroupId)) _nextFrameIndexes[captureGroupId] = 0;
            return _nextFrameIndexes[captureGroupId]++;
        }

        private void writeFrames(bool isStart)
        {
            Dictionary<Transform, bool> writtenHash = new();

            List<Frame> stateFrames = getStateFrames(isStart);

            var binaryWriter = writer!;

            foreach (var frame in stateFrames)
            {
                // write frame indicator
                binaryWriter.Write((byte)INDICATORS.FRAME);

                // write series id
                binaryWriter.Write((byte)frame.groupId);

                // write frame virtual time
                binaryWriter.Write(frame.time);

                // actor blinks are included in environment frames
                if (frame.groupId == CaptureGroupId.Environment)
                {
                    // write count of following blink values

                    // write actor states
                    if (frame.actorStates == null)
                    {
                        binaryWriter.Write((byte)0);
                    }
                    else
                    {
                        binaryWriter.Write((byte)frame.actorStates.Count);
                        for (int i = 0; i < frame.actorStates.Count; i++)
                        {
                            binaryWriter.Write(frame.actorStates[i]);
                        }
                    }


                    byte blinkCount = 0;

                    // write blink values if present
                    if (frame.actorBlinks != null)
                    {

                        blinkCount = (byte)frame.actorBlinks.Count; //  frame.actorBlinks == null ? (byte)0 : (byte)frame.actorBlinks.Count;
                        binaryWriter.Write(blinkCount);

                        // write blink values
                        for (int i = 0; i < blinkCount; i++)
                        {
                            binaryWriter.Write(frame.actorBlinks[i]);
                        }
                    } else
                    {
                        binaryWriter.Write(blinkCount);
                    }



                    byte birdBlinkCount = frame.birdBlinks == null ? (byte)0 : (byte)frame.birdBlinks.Count;
                    binaryWriter.Write(birdBlinkCount);

                    // write blink values if present
                    if (frame.birdBlinks != null)
                    {
                        // write blink values
                        for (int i = 0; i < birdBlinkCount; i++)
                        {
                            binaryWriter.Write(frame.birdBlinks[i]);
                        }
                    }
                }

                // write transform states

                foreach (KeyValuePair<string, ItemState> item in frame.states)
                {
                    string key = item.Key;
                    ItemState itemState = item.Value;

                    // check if the key appeared before
                    bool isNewKey = !transformKeyToIndex.ContainsKey(key);
                    int keyIndex;
                    if (isNewKey)
                    {
                        // register the key
                        keyIndex = nextTransformKeyIndex++;
                        transformKeyToIndex[key] = keyIndex;
                    }

                    // get the int for that key
                    keyIndex = transformKeyToIndex[key];

                    if (isNewKey)
                    {
                        // write transform key mapping
                        binaryWriter.Write((byte)INDICATORS.TRANSFORMKEY);
                        binaryWriter.Write(keyIndex);
                        binaryWriter.Write(key);
                    }

                    // flags describing what data changed and thus will be written to file
                    bool withTransformPosition = true;
                    bool withTransformLocalScale = true;
                    bool withTransformRotation = true;

                    // compare position, rotation etc (current vs previous states)

                    // check if the active state changed

                    bool activeStateChanged = lastTransformActiveStates.ContainsKey(key) ?
                        (lastTransformActiveStates[key] != itemState.active) : true;

                    if (lastPushedItemStates.ContainsKey(key))
                    {
                        // include position,rotation,scale only if the item is active and the values changed,
                        // or if the active state has changed
                        var prevState = lastPushedItemStates[key];
                        withTransformPosition   = activeStateChanged || (itemState.active && itemState.pos != prevState.pos);
                        withTransformLocalScale = activeStateChanged || (itemState.active && itemState.lscale != prevState.lscale);
                        withTransformRotation   = activeStateChanged || (itemState.active && itemState.rot != prevState.rot);
                    }

                    if (!itemState.active && frame.groupId == CaptureGroupId.Actor)
                    {
                        // if an actor transform becomes inactive, that's the death bug.
                        // we have to refresh the actors next frame to get the new transforms.
                        updateActorTransforms = true;
                    }

                    if (activeStateChanged)
                    {
                        // preserve state for later
                        lastTransformActiveStates[key] = itemState.active;
                    }

                    lastPushedItemStates[key] = itemState;

                    // info of what's included/changed

                    ushort contents = 0;
                    contents |= (ushort)(!withTransformPosition ? 0 : CONTAINS.POSITION);
                    contents |= (ushort)(!withTransformLocalScale ? 0 : CONTAINS.LOCALSCALE);
                    contents |= (ushort)(!withTransformRotation ? 0 : CONTAINS.ROTATION);

                    // only write data if anything has changed

                    if (contents != 0 || activeStateChanged)
                    {

                        // write item state indicator
                        binaryWriter.Write((byte)INDICATORS.ITEM_STATE);

                        // write transform key index
                        binaryWriter.Write(keyIndex);

                        // set "active" bit in contents (don't do this earlier)
                        if (itemState.active) contents |= (ushort)CONTAINS.ACTIVE;

                        // write contents descriptor
                        binaryWriter.Write(contents);

                        // write position if changed
                        if (withTransformPosition) Binary.WriteVector3(binaryWriter, itemState.pos);

                        // write localScale if changed
                        if (withTransformLocalScale) Binary.WriteVector3(binaryWriter, itemState.lscale);

                        // write rotation if changed
                        if (withTransformRotation) Binary.WriteVector3(binaryWriter, itemState.rot);
                    }
                }

                // write 3d audio clips that were played

                if (frame.sounds3d != null)
                {
                    foreach (var clp in frame.sounds3d)
                    {
                        // include key mapping if it's a new key (see above)
                        if (!soundNameToIndex.ContainsKey(clp.name))
                        {
                            int newSoundId = nextSoundId++;
                            soundNameToIndex[clp.name] = newSoundId;
                            binaryWriter.Write((byte)INDICATORS.SOUNDKEY);
                            binaryWriter.Write(newSoundId);
                            binaryWriter.Write(clp.name);
                        }

                        // include the actual sound information
                        binaryWriter.Write((byte)INDICATORS.SOUND3D);
                        int soundIndex = soundNameToIndex[clp.name];
                        binaryWriter.Write(soundIndex);
                        Binary.WriteSound3d(binaryWriter, clp);
                    }
                }

                // same for 2d audioclips

                if (frame.sounds2d != null)
                {
                    foreach (var clp in frame.sounds2d)
                    {
                        if (!soundNameToIndex.ContainsKey(clp.name))
                        {
                            int newSoundId = nextSoundId++;
                            soundNameToIndex[clp.name] = newSoundId;
                            binaryWriter.Write((byte)INDICATORS.SOUNDKEY);
                            binaryWriter.Write(newSoundId);
                            binaryWriter.Write(clp.name);
                        }

                        binaryWriter.Write((byte)INDICATORS.SOUND2D);
                        int soundIndex = soundNameToIndex[clp.name];
                        binaryWriter.Write(soundIndex);
                        Binary.WriteSound2d(binaryWriter, clp);
                    }
                }
            }
        }

        private List<ActorInfo> createRecordedActorsInfo(List<Actor> actors)
        {
            List<ActorInfo> actorsInfo = new();
            for (int i = 0; i < actors.Count; i++)
            {
                actorsInfo.Add(createRecordedActorInfo(actors[i]));
            }
            return actorsInfo;
        }

        private ActorInfo createRecordedActorInfo(Actor actor)
        {
            ActorInfo recordedActor = new ActorInfo();
            recordedActor.primaryColor = actor.primaryColor;
            recordedActor.CostumeColor = actor.CostumeColor;
            recordedActor.costumeIdsWithColors = Game.getActorCostumeIdsWithColors(actor); // .CostumeCompRef.SaveEntry.ExtractCostumeItemIDs(true);
            return recordedActor;
        }

        public void newActorAppeared(Actor actor)
        {
            // save the actor reference of actors to check for readiness in OnUpdate()
            onupdate_toAddActors[actor] = true;
        }

        public bool isActorReadyToRecord(Actor actor)
        {
            // actor must not be gray colored
            return !Tools.isGrayColor(actor.primaryColor);
        }

        public void unpatch()
        {
            if (harmony != null) harmony.UnpatchSelf();
            harmony = null;
        }

        private bool stopped = false;
        public void stop()
        {
            if (stopped) return;
            stopped = true;
            unpatch();
            if (writer != null) writer.Close();
        }

        public void binaryWriteActorInfo(BinaryWriter writer, ActorInfo info)
        {
            // write actor info indicator
            writer.Write((byte)INDICATORS.ACTORINFO);

            // write primary color
            writer.Write(info.primaryColor.r);
            writer.Write(info.primaryColor.g);
            writer.Write(info.primaryColor.b);

            // write costume color
            writer.Write(info.CostumeColor.r);
            writer.Write(info.CostumeColor.g);
            writer.Write(info.CostumeColor.b);

            // write costume parts

            // write amount of space required to save costume info
            writer.Write(info.costumeIdsWithColors.Length);

            // write costume ids and colors
            for (int j = 0; j < info.costumeIdsWithColors.Length; j++)
            {
                writer.Write(info.costumeIdsWithColors[j]);
            }
        }

        public static HarmonyLib.Harmony? harmony;

        private static void patch()
        {
            harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(RecorderPatch), null);
            var method = typeof(AudioController).GetMethods().Where(m => m.Name == "Play3DAt" && m.GetParameters().Length == 15).ToList().First();
            var postfix = new HarmonyLib.HarmonyMethod(typeof(Recorder).GetMethod("handlePlay3DAt"));
            if (harmony.Patch(method, null, postfix, null) == null)
            {
                logError("failed to patch Play3DAt");
            }
        }

        public static void handlePlay3DAt(
              AudioController __instance,
              AudioClip clip,
              Vector3 location,
              Transform? target = null,
              VolumeLevels.SoundType soundType = VolumeLevels.SoundType.SFX,
              Action? onComplete = null,
              bool loop = false,
              float volume = 1f,
              float pitch = 1f,
              float delay = 0f,
              float minDistance = 1f,
              float maxDistance = 500f,
              float dopplerLevel = 0f,
              AudioMixerGroup? mixerGroup = null,
              AudioController.PooledAudioSource? pooledSource = null,
              float spatialBlendOverride = 1f
        ) {
            if (clip == null) return;
            if (Replay.recorder == null) return;

            if (clip.name == "Subway Loop") return;

            var clp = new _3dAudioClip();
            clp.name = clip.name;
            clp.posX = location.x;
            clp.posY = location.y;
            clp.posZ = location.z;
            clp.soundType = (int)soundType;
            clp.loop = loop;
            clp.volume = volume;
            clp.pitch = pitch;
            clp.delay = delay;
            clp.minDistance = minDistance;
            clp.maxDistance = maxDistance;
            clp.dopplerLevel = dopplerLevel;
            clp.spatialBlendOverride = spatialBlendOverride;
            Replay.recorder.pushSound3d(clp);
        }

        public void start()
        {
            patch();
            tc.clear();

            nextFrameIndex = 0;
            
            Glass.indexGlass();

            // rename birds GameObjects and collect them for tracking
            recordingBirds = indexBirds(false);

            birdBlinks.Clear();
            for (int i = 0; i < recordingBirds.Count; i++) birdBlinks.Add(100);
            birdBlinksDirty = true;

            // keep a list of actors we will record
            for (int i = 0; i < Actor._ActorCache.Count; i++)
            {
                var actor = Actor._ActorCache[i];

                // put local actors first
                if (actor.IsLocal) {
                    recordingActors.Insert(0, actor);
                } else {
                    recordingActors.Add(actor);
                }
            }

            // create information about the actors
            actorInfos = createRecordedActorsInfo(recordingActors);

            // create list of transforms to observe
            createObservedTransformsList();

            writer.Write(0);

            // write map name
            writer.Write(Game.getSceneName());

            // write count of actor infos
            writer.Write(actorInfos.Count);

            // write info of each actor
            for (int i = 0; i < actorInfos.Count; i++)
            {
                ActorInfo info = actorInfos[i];
                binaryWriteActorInfo(writer, info);
            }

            // write information about shards

            var shardContainers = Glass.getShardContainers();
            for (int i = 0; i < shardContainers.Count; i++)
            {
                binaryWriteShardContainer(writer, shardContainers[i]);
            }

            // empty frames array
            frames.Clear();
        }

        // get state of a single transform
        private ItemState getTransformState(Transform transform)
        {
            ItemState s = new ItemState();
            s.pos = transform.position;
            s.rot = transform.rotation.eulerAngles;
            s.lscale = transform.localScale;
            s.active = transform.gameObject.active;
            return s;
        }

        public void lateUpdate()
        {
            bool isStart = false;

            if (recordStartTime == -1)
            {
                isStart = true;
                recordStartTime = Time.time;
            }

            // check if there are late actors to add (happens in waves mode)

            if (onupdate_toAddActors.Count > 0)
            {
                // list of processed actors (will be removed later)
                var processedActors = new Dictionary<Actor, bool>();

                foreach (var entry in onupdate_toAddActors)
                {
                    var actor = entry.Key;
                    
                    // skip actor if not ready to record yet
                    if (!isActorReadyToRecord(actor)) continue;

                    // assign an index to the actor
                    var actorIndex = recordingActors.Count;

                    // actor was processed and will be removed from the list of
                    // to check actors further down
                    processedActors[actor] = true;

                    // actor came in late, i.e. was not present when the recording started
                    lateActors.Add(actor);

                    // add actor to list of all recorded actors so they will be tracked
                    recordingActors.Add(actor);

                    observeActor(actor, actorIndex);

                    // assemble information about the actor (color, colstume etc)
                    var actorInfo = createRecordedActorInfo(actor);

                    // add it to the list of actor information
                    actorInfos.Add(actorInfo);
                    binaryWriteActorInfo(writer, actorInfo);
                }
                    
                // remove processed actors
                foreach (var entry in processedActors) onupdate_toAddActors.Remove(entry.Key);
            }

            writeFrames(isStart);
        }

        public void observeActor(Actor actor, int idx)
        {
            // actor can be null if a wave actor was destroyed but a nulled
            // reference is still sitting in the replayActors list
            if (actor == null) return;
            
            // use key Actor#N in recording
            tc.setFixedSingleTransformKey(actor.transform, "Actor#" + idx);

            // get actor's relevant transforms
            var actorTransforms = Replay.getActorTransforms(actor, true);

            // add transforms to transform cache
            tc.addTransforms(actorTransforms);
            
            // register actor transforms in capture group
            var group = captureGroups[(int)CaptureGroupId.Actor];

            for (int i = 0; i < actorTransforms.Count; i++)
            {
                var info = Replay.tc.getTransformInfo(actorTransforms[i]);
                group.observed[info.key] = info;
            }
        }

        private void binaryWriteShardContainer(BinaryWriter w, GameObject shardContainer)
        {

            var shardTransforms = Tools.getTransformChildren(shardContainer.transform);
            List<GameObject> shardObjects = shardTransforms.ConvertAll<GameObject>(t => t.gameObject);

            // write shard-ontainer-indicator
            w.Write((byte)INDICATORS.SHARDCONTAINER);

            // write key of the shard container
            var key = tc.getTransformInfo(shardContainer.transform).key;
            w.Write(key);

            // write amount of shards
            w.Write(shardObjects.Count);

            // write shard shape data
            for (int i = 0; i < shardObjects.Count; i++)
            {
                binaryWriteShard(w, shardObjects[i]);
            }
        }

        private void binaryWriteShard(BinaryWriter w, GameObject shard)
        {
            var meshFilter = shard.GetComponent<MeshFilter>();
            var mesh = meshFilter.mesh;

            // write normals data
            w.Write((int)mesh.normals.Count);
            ReadOnlySpan<byte> normalsAsBytes = MemoryMarshal.AsBytes(mesh.normals.AsSpan());
            w.Write(normalsAsBytes);

            // write vertices data
            w.Write((int)mesh.vertices.Count);
            ReadOnlySpan<byte> verticesAsBytes = MemoryMarshal.AsBytes(mesh.vertices.AsSpan());
            w.Write(verticesAsBytes);

            // write uv data
            w.Write((int)mesh.uv.Count);
            ReadOnlySpan<byte> uvAsBytes = MemoryMarshal.AsBytes(mesh.uv.AsSpan());
            w.Write(uvAsBytes);

            // write triangles data
            w.Write((int)mesh.triangles.Count);
            ReadOnlySpan<byte> trianglesAsBytes = MemoryMarshal.AsBytes(mesh.triangles.AsSpan());
            w.Write(trianglesAsBytes);

            // write submesh count
            w.Write((int)mesh.subMeshCount);

            // write submesh triangle data
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var subMeshTriangles = mesh.GetTriangles(i);
                w.Write((int)subMeshTriangles.Count);
                ReadOnlySpan<byte> asBytes = MemoryMarshal.AsBytes(subMeshTriangles.AsSpan());
                w.Write(asBytes);
            }
        }

        public Regex tankPropsRegex = new Regex(@"^Tank [ABCDE]/Props/");
        public Regex tankPropsBonesRegex = new Regex(@"^Tank [ABCDE]/Props/.*_bone");
        public Regex birdRootRegex = new Regex(@"^.*/Bird#\d+$");
        public Regex birdMovementNodeRegex = new Regex(@"/Bird Movement Node");
        public Regex disabledSharksRegex = new Regex(@"^Sharks/Shark \(Sleeping\) \([23456]\)[/$]");

        public void createObservedTransformsList()
        {
            // finds all transforms to be observed and submits them to a capture rate group,
            // which will be checked in different intervals

            foreach (var group in captureGroups) group.observed.Clear(); // safety


            // observe all actors
            // has to be done first, so the actors' root keys are substituted correctly
            int n = 0;
            foreach (var actor in recordingActors) observeActor(actor, n++);

            // place all environment in a single, lower cpature rate series
            var observedTransforms = captureGroupHash[CaptureGroupId.Environment].observed;

            var mapName = Game.getSceneName();

            var withTransformSync = UnityEngine.Object.FindObjectsOfType<TransformSync>(true);
            var interactables = UnityEngine.Object.FindObjectsOfType<InteractableObject>(true);
            var fractures = UnityEngine.Object.FindObjectsOfType<Fracture>(true);
            var fractureShards = UnityEngine.Object.FindObjectsOfType<FractureShard>(true);

            // these are capable of being synced by the server so we'll track them
            foreach (var m in withTransformSync)
            {
                var t = m.gameObject.transform;
                var info = tc.getTransformInfo(t);
                observedTransforms[info.key] = info;
            }

            // another type of transforms to track
            foreach (var m in interactables)
            {
                var t = m.gameObject.transform;
                var info = tc.getTransformInfo(t);
                observedTransforms[info.key] = info;
            }

            // these are the intact window parts that are getting disabled when the window breaks
            foreach (var m in fractures)
            {
                var t = m.gameObject.transform;
                var info = tc.getTransformInfo(t);
                observedTransforms[info.key] = info;
            }

            // map specific trackings

            if (mapName == "incinerator")
            {
                // the curtains next to the conveyor belts
                var ts = getTransforms(new List<string>() { "/Scene/Geometry/curtain", "/Scene/Geometry/curtain (1)" }, true);
                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            else if (mapName == "buoy")
            {
                // ice floes
                var ts = Tools.findTransformsOfComponents("/Ice", new List<Il2CppSystem.Type>()
                {
                    Il2CppType.Of<MeshRenderer>(),
                    Il2CppType.Of<SkinnedMeshRenderer>()
                });

                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            else if (mapName == "crane")
            {
                var ts = Tools.findTransformsOfComponents("/Crane", new List<Il2CppSystem.Type>()
                {
                    Il2CppType.Of<MeshRenderer>(),
                    Il2CppType.Of<SkinnedMeshRenderer>()
                });

                // var ts = crane.GetComponentsInChildren<MeshRenderer>().ToList().ConvertAll<Transform>(m => m.gameObject.transform);
                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }


            else if (mapName == "aquarium")
            {

                // fish tanks
                var ts = getTransforms(new List<string>() { "/Tank A", "/Tank B", "/Tank C", "/Tank E", }, true);
                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            else if (mapName == "rooftop")
            {
                // the turning fans

                var ts = getTransforms(new List<string>() {
                    "/Rooftop/Vents/rooftop_ventBase/rooftop_ventFan",
                    "/Rooftop/Vents/rooftop_ventBase (1)/rooftop_ventFan",
                    "/Rooftop/Vents/rooftop_ventBase (2)/rooftop_ventFan",
                    "/Rooftop/Vents/rooftop_ventBase (3)/rooftop_ventFan",
                }, false);

                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            else if (mapName == "trucks")
            {
                // wheels and drivers

                var ts = getTransforms(new List<string>() {
                    "/Root/Truck (1)/Truck/ragdoll/colliders",
                    "/Root/Truck (2)/Truck/ragdoll/colliders",
                    "/Root/Truck (3)/Truck/ragdoll/colliders",
                    "/Root/Truck (1)/Truck/truck_steeringWheel",
                    "/Root/Truck (2)/Truck/truck_steeringWheel",
                    "/Root/Truck (3)/Truck/truck_steeringWheel",
                }, true);

                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            else if (mapName == "trawler")
            {
                var ts = getTransforms(new List<string>() {
                    "/Trawler/Trawler_Wheel",
                }, false);

                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms[info.key] = info;
                }
            }

            // observe shards

            foreach (var t in fractureShards)
            {
                var info = tc.getTransformInfo(t.transform);
                var key = info.key;
                observedTransforms[key] = info;
            }

            // map excludes

            if (mapName == "towers")
            {
                // exclude irrelevant background stuff
                var keys = observedTransforms.Keys.ToList();
                foreach (var key in keys)
                {
                    if (key.StartsWith("GameObject/") || key == "GameObject")
                    {
                        observedTransforms.Remove(key);
                    }
                }
            }
            else if (mapName == "crane")
            {
                var keys = observedTransforms.Keys.ToList();
                foreach (var key in keys)
                {
                    // remove everything in the background for performance reasons
                    if (key.StartsWith("Background"))
                    {
                        observedTransforms.Remove(key);
                    }
                }
            }

            // global excludes

            var keys2 = observedTransforms.Keys.ToList();
            foreach (var key in keys2)
            {
                if (
                    key.StartsWith("NameBar") || // orphan namebar
                    key.StartsWith("Global(Clone)") ||
                    (key.StartsWith("Managers") && !key.StartsWith("Managers/Trains")) || // for trains on subway
                    key.StartsWith("Actor#") // actors are observed in a different capture group
                ) {
                    observedTransforms.Remove(key);
                }
            }

            // include parents of selected items

            var keys3 = observedTransforms.Keys.ToList();
            foreach (var key in keys3)
            {
                var info = observedTransforms[key];
                var t = info.transform.parent;
                while (t != null)
                {
                    var parentInfo = tc.getTransformInfo(t);
                    observedTransforms[parentInfo.key] = parentInfo;
                    t = t.parent;
                }
            }

            // hard excludes

            // remove bird root transforms as they never move.
            // do it after including parents, because they're
            // getting included there, but we don't need them,
            // they're always active (saves about 20 checks)

            if (mapName == "rooftop" || mapName == "crane")
            {
                var keys = observedTransforms.Keys.ToList();
                foreach (var key in keys)
                {
                    // remove everything in the background for performance reasons
                    if (
                        key == "Critters - Birds" ||
                        key == "Critters - Birds/Group1" ||
                        key == "Critters - Birds/Group2" ||
                        birdRootRegex.IsMatch(key) ||
                        birdMovementNodeRegex.IsMatch(key)
                    )
                    {
                        observedTransforms.Remove(key);
                    }
                }
            }

            if (mapName == "girders")
            {
                // bogus stuff flying around
                var ts = getTransforms(new List<string>() { "/World/Scene Root/Static/Rubble Chute" }, true);
                foreach (var t in ts)
                {
                    var info = tc.getTransformInfo(t);
                    observedTransforms.Remove(info.key);
                }
            }

            else if (mapName == "buoy")
            {
                var keys = observedTransforms.Keys.ToList();
                foreach (var key in keys)
                {
                    if (disabledSharksRegex.IsMatch(key))
                    {
                        observedTransforms.Remove(key);
                    }
                }
            }

            if (mapName == "subway")
            {
                // index the looped subway audio clips. their playing AudioSources are
                // moving on the map alongside the trains.

                var infos = Game.getSubwayLoopAudioSourceTransforms();

                foreach (var info in infos)
                {
                    observedTransforms[info.key] = info;
                }
            }
        }

        public Dictionary<string, bool> brokenShards = new();

        public void shardBroke(FractureShard shard)
        {
            var info = tc.getTransformInfo(shard.gameObject.transform);
            brokenShards[info.key] = true;
        }

        // bird blinks management

        // unknown how to detect the exact blink value as of now,
        // we only know when the birds are commanded to open and
        // close their eyes. we store this as a 0 or 100 percentage
        // value and modify it on load so the transition is smooth.

        private List<byte> birdBlinks = new();
        private bool birdBlinksDirty = false;

        public int getBirdIndex(BirdActor bird)
        {
            return recordingBirds == null ? -1 : recordingBirds.IndexOf(bird);
        }

        public void birdBlinking(BirdActor bird, byte blink)
        {
            // this can happen before recording starts, ignore it, that' all 0 values
            if (recordingBirds == null) return;

            var idx = getBirdIndex(bird);
            if (idx == -1)
            {
                return;
            }

            if (birdBlinks[idx] != blink)
            {
                birdBlinks[idx] = blink;
                // include blinks in next frame
                birdBlinksDirty = true;
            }
        }

        public class RecorderPatch
        {
            // track new actors appearing (wave actors) to submit them to the recorder

            [HarmonyLib.HarmonyPatch(typeof(Actor), "Awake")]
            [HarmonyLib.HarmonyPostfix]

            public static void Actor_Awake(Actor __instance)
            {
                Replay.recorder?.newActorAppeared(__instance);
            }

            [HarmonyLib.HarmonyPatch(typeof(BirdActor), "BlinkOpen")]
            [HarmonyLib.HarmonyPostfix]
            public static void BirdActor_BlinkOpen(BirdActor __instance)
            {
                Replay.recorder?.birdBlinking(__instance, 100);
            }

            [HarmonyLib.HarmonyPatch(typeof(BirdActor), "BlinkClose")]
            [HarmonyLib.HarmonyPostfix]
            public static void BirdActor_BlinkClose(BirdActor __instance)
            {
                // detection when a shard breaks
                Replay.recorder?.birdBlinking(__instance, 0);
            }


            [HarmonyLib.HarmonyPatch(typeof(FractureShard), "FinalizeCleanUp")]
            [HarmonyLib.HarmonyPostfix]
            public static void FractureShard_FinalizeCleanUp(FractureShard __instance)
            {
                // detection when a shard breaks
                Replay.recorder?.shardBroke(__instance);
            }

            [HarmonyLib.HarmonyPatch(typeof(NetClientExit), "OnExit")]
            [HarmonyLib.HarmonyPostfix]
            public static void NetClientExit_OnExit(NetClientExit __instance, NetExitMessage message, NetworkConnection conn)
            {
                Replay.recorder?.stop();
            }

            [HarmonyLib.HarmonyPatch(typeof(AudioController), "Play2D")]
            [HarmonyLib.HarmonyPostfix]
            public static void AudioController_Play2D(
                AudioController __instance,
                UnityEngine.AudioClip clip,
                VolumeLevels.SoundType soundType = VolumeLevels.SoundType.SFX,
                Action? onComplete = null,
                bool loop = false,
                float volume = 1f,
                float pitch = 1f,
                float delay = 0f,
                AudioMixerGroup? mixerGroup = null,
                AudioController.PooledAudioSource? pooledSource = null
            )
            {
                if (Replay.recorder == null) return;
                if (clip.name == "GB menu blip") return;
                var clp = new _2dAudioClip();
                clp.name = clip.name;
                clp.soundType = (int)soundType;
                clp.loop = loop;
                clp.volume = volume;
                clp.pitch = pitch;
                clp.delay = delay;
                Replay.recorder.pushSound2d(clp);
            }
        }


    }
}
