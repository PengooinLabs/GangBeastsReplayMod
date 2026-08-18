using Il2CppFemur;
using UnityEngine;
using Il2CppGB.Game;
using static PengooinLabs.ReplayMod.Replay;
using static PengooinLabs.ReplayMod.Types;
using Il2Cpp;
using Il2CppGB.Core;
using UnityEngine.Networking;
using Il2CppGB.Game.Critters;
using Il2CppAudio;
using UnityEngine.Audio;

namespace PengooinLabs.ReplayMod
{
    public class Loader
    {
        private string mapName = "";
        private Dictionary<CaptureGroupId, List<Frame>> frameGroups = new();
        private List<Actor> spawnedActors = new();
        private List<ActorInfo> lateActors = new();
        private List<ActorInfo> actorInfos = new();
        private Dictionary<Actor, string> actorNames = new();
        private List<BirdActor> replayBirds = new();
        private List<Glass.ShardContainerData> shardContainersData = new();
        private Dictionary<string, ItemState> staticItemStates = new();

        public Loader()
        {
            patch();
        }

        // functions to get data after loading

        public Dictionary<string, ItemState> getStaticItemStates() { return staticItemStates; }
        public string getMapName() { return mapName; }
        public List<Actor> getActors() { return spawnedActors; }
        public Dictionary<CaptureGroupId, List<Frame>> getFrameGroups() { return frameGroups; }
        public Dictionary<Actor, string> getActorNames() { return actorNames; }
        public List<BirdActor> getBirds() { return replayBirds; }

        // load replay from file
        public bool loadFromFile(string absFilePath)
        {
            try
            {
                string path = absFilePath;

                byte[] data = File.ReadAllBytes(path);

                using var memoryStream = new MemoryStream(data);

                var reader = new BinaryReader(memoryStream);

                actorInfos.Clear();

                int REPLAYCONTENTS;

                try
                {
                    REPLAYCONTENTS = reader.ReadInt32();
                    mapName = reader.ReadString();

                    actorInfos.Clear();
                    shardContainersData.Clear();
                    lateActors.Clear();

                    // read initial actor count

                    int actorInfoCount = reader.ReadInt32();

                    // peek what's coming next
                    while (reader.PeekChar() == (byte)INDICATORS.ACTORINFO)
                    {
                        // remove indicator
                        reader.ReadByte();
                        // read actor info
                        ActorInfo recActor = binaryReadActorInfo(reader, REPLAYCONTENTS);
                        // collect actor info
                        actorInfos.Add(recActor);
                    }
                }
                catch (Exception e)
                {
                    logError("Exception loading replay (block 1): " + e);
                    return false;
                }

                // maps int to transform key
                Dictionary<int, string> transformIndexToKey = new();

                // maps int to sound clip name
                Dictionary<int, string> soundIndexToKey = new();

                // stores last known vectors for copying
                Dictionary<string, Vector3> lastVectors = new();

                bool INVALID_DATA = false;

                frameGroups.Clear();

                // frame last containing blink values
                Frame? lastFrameWithActorBlinks = null;
                Frame? lastFrameWithBirdBlinks = null;
                Frame? lastFrameWithActorStates = null;

                try
                {
                    // read until end of stream
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        // read indicator of what's coming next
                        byte indicator = reader.ReadByte();

                        // safety check
                        if (
                            indicator != (byte)INDICATORS.FRAME &&
                            indicator != (byte)INDICATORS.SHARDCONTAINER &&
                            indicator != (byte)INDICATORS.ACTORINFO
                        )
                        {
                            logError("expected a frame or shardcontainer indicator, but datatype is " + indicator);
                            INVALID_DATA = true;
                            throw new Exception("Invalid Data");
                        }

                        // shard container data

                        if (indicator == (byte)INDICATORS.SHARDCONTAINER)
                        {
                            // read the data
                            var shardContainerData = Binary.ReadShardContainer(reader);
                            // collect it for later
                            shardContainersData.Add(shardContainerData);
                        }

                        // actor info - this is a late actor then (waves)

                        else if (indicator == (byte)INDICATORS.ACTORINFO)
                        {
                            // read the actor info
                            var actorInfo = binaryReadActorInfo(reader, REPLAYCONTENTS);
                            // collect it for later
                            lateActors.Add(actorInfo);
                        }

                        // frame data

                        else if (indicator == (byte)INDICATORS.FRAME)
                        {
                            // create and collect new frame object
                            Frame frame = new Frame();

                            // read series id
                            frame.groupId = (CaptureGroupId)reader.ReadByte();

                            // add to series
                            if (!frameGroups.ContainsKey(frame.groupId)) frameGroups[frame.groupId] = new();
                            frame.index = frameGroups[frame.groupId].Count;
                            // do not add the frame here yet since data might be incomplete

                            // read frame timestamp
                            frame.time = reader.ReadSingle();

                            // read actor blink values of the current frame

                            if (frame.groupId == CaptureGroupId.Environment)
                            {
                                // read actor state count
                                var actorStateCount = reader.ReadByte();
                                
                                if (actorStateCount > 0)
                                {
                                    frame.actorStates = new();
                                    for (int i = 0; i < actorStateCount; i++)
                                    {
                                        frame.actorStates.Add(reader.ReadByte());
                                    }
                                    lastFrameWithActorStates = frame;
                                }
                                else
                                {
                                    // reuse actor states from last frame
                                    if (lastFrameWithActorStates == null) throw new Exception("Can't link actorStates because there's no lastFrameWithActorStates");
                                    frame.actorStates = lastFrameWithActorStates.actorStates!;
                                }

                                // read actor blink count
                                var blinkCount = reader.ReadByte();

                                if (blinkCount > 0)
                                {
                                    // read blink values
                                    frame.actorBlinks = new();
                                    for (int i = 0; i < blinkCount; i++)
                                    {
                                        frame.actorBlinks.Add(reader.ReadByte());
                                    }
                                    lastFrameWithActorBlinks = frame;
                                }
                                else
                                {
                                    // reuse blinks from last frame
                                    if (lastFrameWithActorBlinks == null) throw new Exception("Can't link actorBlinks because there's no lastFrameWithActorBlinks");
                                    frame.actorBlinks = lastFrameWithActorBlinks.actorBlinks;
                                }


                                // same with bird blinks

                                var birdBlinkCount = reader.ReadByte();

                                if (birdBlinkCount > 0)
                                {
                                    // read blink values
                                    frame.birdBlinks = new();
                                    for (int i = 0; i < birdBlinkCount; i++)
                                    {
                                        frame.birdBlinks.Add(reader.ReadByte());
                                    }
                                    lastFrameWithBirdBlinks = frame;
                                }
                                else
                                {
                                    // reuse blinks from last frame
                                    if (lastFrameWithBirdBlinks != null)
                                    {
                                        // throwing only makes sense if there are supposed to be birds.
                                        // throw new Exception("Can't link birdBlinks because there's no lastBirdBlinkFrame");
                                        frame.birdBlinks = lastFrameWithBirdBlinks.birdBlinks;
                                    }
                                }

                            }
                            // read item states as long as they're coming in

                            while (reader.BaseStream.Position < reader.BaseStream.Length)
                            {
                                // peek what's coming next
                                var peek = reader.PeekChar();

                                if (
                                    (peek != (byte)INDICATORS.TRANSFORMKEY) &&
                                    (peek != (byte)INDICATORS.SOUNDKEY) &&
                                    (peek != (byte)INDICATORS.ITEM_STATE) &&
                                    (peek != (byte)INDICATORS.SOUND3D) &&
                                    (peek != (byte)INDICATORS.SOUND2D)
                                )
                                {
                                    // frame end reached
                                    break;
                                }
                                else
                                {
                                    // read indicator
                                    byte indicator2 = reader.ReadByte();

                                    // process transform key
                                    // this is to avoid storing string values redundantly

                                    if (indicator2 == (byte)INDICATORS.TRANSFORMKEY)
                                    {
                                        // save transform key mapping
                                        int keyIndex = reader.ReadInt32();
                                        string key = reader.ReadString();
                                        transformIndexToKey[keyIndex] = key;
                                    }

                                    else if (indicator2 == (byte)INDICATORS.SOUNDKEY)
                                    {
                                        // save sound key mapping
                                        int keyIndex = reader.ReadInt32();
                                        string key = reader.ReadString();
                                        soundIndexToKey[keyIndex] = key;
                                    }

                                    else if (indicator2 == (byte)INDICATORS.SOUND3D)
                                    {
                                        // read 3d sound
                                        int soundIndex = reader.ReadInt32();
                                        var clipName = soundIndexToKey[soundIndex];
                                        var clip3d = Binary.ReadSound3d(reader, clipName);
                                        // store it in the frame
                                        if (frame.sounds3d == null) frame.sounds3d = new();
                                        frame.sounds3d.Add(clip3d);
                                    }

                                    else if (indicator2 == (byte)INDICATORS.SOUND2D)
                                    {
                                        // read 2d sound
                                        int soundIndex = reader.ReadInt32();
                                        var clipName = soundIndexToKey[soundIndex];
                                        var clip2d = Binary.ReadSound2d(reader, clipName);
                                        // store it in the frame
                                        if (frame.sounds2d == null) frame.sounds2d = new();
                                        frame.sounds2d.Add(clip2d);
                                    }

                                    else if (indicator2 == (byte)INDICATORS.ITEM_STATE)
                                    {
                                        // read item state
                                        int keyIndex2 = reader.ReadInt32();
                                        if (!transformIndexToKey.ContainsKey(keyIndex2)) throw new Exception("Missing key name of keyIndex " + keyIndex2.ToString());
                                        string key2 = transformIndexToKey[keyIndex2];
                                        // create new item state
                                        ItemState itemState = new ItemState();

                                        // read bitmap of what data is included
                                        ushort contents = reader.ReadUInt16();
                                        // read whether item is active
                                        itemState.active = (contents & (ushort)CONTAINS.ACTIVE) > 0;

                                        if ((contents & (ushort)CONTAINS.POSITION) > 0)
                                        {
                                            // read item position
                                            itemState.pos = Binary.ReadVector3(reader);
                                            // keep it as last position for future duplication
                                            lastVectors[key2 + "_position"] = itemState.pos;
                                        }
                                        else
                                        {
                                            // position is not included (= did not change),
                                            // copy last known position
                                            itemState.pos = lastVectors[key2 + "_position"];
                                        }

                                        // same with scale

                                        if ((contents & (ushort)CONTAINS.LOCALSCALE) > 0)
                                        {
                                            itemState.lscale = Binary.ReadVector3(reader);
                                            lastVectors[key2 + "_localScale"] = itemState.lscale;
                                        }
                                        else
                                        {
                                            itemState.lscale = lastVectors[key2 + "_localScale"];
                                        }

                                        // same with rotation

                                        if ((contents & (ushort)CONTAINS.ROTATION) > 0)
                                        {
                                            itemState.rot = Binary.ReadVector3(reader);
                                            lastVectors[key2 + "_rotation"] = itemState.rot;
                                        }
                                        else
                                        {
                                            itemState.rot = lastVectors[key2 + "_rotation"];
                                        }

                                        frame.states[key2] = itemState;
                                    }
                                    else
                                    {
                                        logError("invalid datatype read, expected keymap or itemstate. datatype value is " + indicator2.ToString());
                                        INVALID_DATA = true;
                                        throw new Exception("Invalid Data");
                                    }
                                }
                            }

                            frameGroups[frame.groupId].Add(frame);
                        }
                    }
                }
                catch (Exception err)
                {
                    logError("Exception reading frames: " + err);
                    // TODO improve processing of incomplete data
                }

                if (INVALID_DATA)
                {
                    throw new Exception("Invalid Data");
                }

                // frameSeries are created here. we have to pad the item states
                // in each series

                // fix bug where costume acts weird when actor dies. the
                // data is contained in series 0 (actor frames)
                fixDeathBug(frameGroups[0]);

                // add data of late actors (wave actors) to frames
                // earlier then when the actor first appeared, since at
                // the time that data couldn't be recorded yet.
                fixLateActors(frameGroups[0]);

                staticItemStates.Clear();
                foreach (var entry in frameGroups)
                {
                    var id = entry.Key;
                    var frames = entry.Value;
                    extractStaticItems(frames);
                }

                // make sure data for every transform is present in every frame
                foreach (var frames in frameGroups.Values)
                {
                    padFrameData(frames);
                }

            }
            catch (Exception err)
             {
                 logError("exception in loadReplay: " + err);
                 return false;
             }

            // replay loaded successfully
            return true;
        }

        private void extractStaticItems(List<Frame> frames)
        {
            // find items appearing only once in the replay,
            // extract them so we don't have to duplicate all the data

            Dictionary<string, int> counts = new();
            Dictionary<string, Dictionary<string, ItemState>> appearsIn = new();

            foreach (var frame in frames)
            {
                foreach (var entry in frame.states)
                {
                    var key = entry.Key;
                    if (!counts.ContainsKey(key)) counts[key] = 0;
                    counts[key]++;
                    appearsIn[key] = frame.states;
                }
            }

            foreach (var entry in counts)
            {
                var key = entry.Key;
                var count = entry.Value;
                if (count == 1)
                {
                    staticItemStates[key] = appearsIn[key][key];
                    appearsIn[key].Remove(key);
                }
            }
        }

        private void fixLateActors(List<Frame> actorFrames)
        {
            var allKeys = getAllTransformKeysInFrames(actorFrames);

            // for late (wave) actors, create an inactive itemState for their
            // transforms in frame 0. the actors will be created early but hidden,
            // making further special treatment obsolete. 

            foreach (var entry in allKeys)
            {
                var key = entry.Key;
                if (key.StartsWith("Actor#") && !actorFrames[0].states.ContainsKey(key))
                {
                    var firstState = getFirstState(actorFrames, key)!;
                    var initialState = new ItemState();
                    initialState.pos = firstState.pos;
                    initialState.rot = firstState.rot;
                    initialState.lscale = firstState.lscale;
                    initialState.active = false;
                    actorFrames[0].states[key] = initialState;
                }
            }
        }

        private static Dictionary<string, bool> getAllTransformKeysInFrames(List<Frame> frames)
        {
            Dictionary<string, bool> result = new();
            for (int f = 0; f < frames.Count; f++)
            {
                var frame = frames[f];
                foreach (var entry in frame.states)
                {
                    result[entry.Key] = true;
                }
            }
            return result;
        }

        // get the first state of a key available in recorded data

        private ItemState? getFirstState(List<Frame> inFrames, string key)
        {
            for (int i = 0; i < inFrames.Count; i++)
            {
                var frame = inFrames[i];
                if (frame.states.ContainsKey(key))
                {
                    return frame.states[key];
                }
            }
            return null;
        }

        // prepare the map/scene for the replay
        public void setupMapForReplay()
        {
            // freeze time during setup
            setTimeScale(0);

            // setup birds and glass
            replayBirds = indexBirds(true);

            // default camera setting
            var camConfig = new float[] { 65f, 0f, 12f };

            // some maps have different rotation
            var mapName = Game.getSceneName();

            if (mapName == "ring")
            {
                camConfig[1] = 180f;
            }
            else if (mapName == "billboard")
            {
                camConfig[1] = 90f;
            }

            // apply camera position instantly
            Replay.camera.setUD(camConfig[0], true);
            Replay.camera.setLR(camConfig[1], true);
            Replay.camera.setDistance(camConfig[2], true);


            // remove the regular actor(s) the scene was started with
            destroyExistingActors();


            // create dummy actors to use in the replay

            int nextControllerId = GameMode_Waves.AI_CONTROLLER_STARTIDEX;

            var allActorList = new List<ActorInfo>(actorInfos);
            allActorList.AddRange(lateActors);
            actorNames.Clear();


            for (int i = 0; i < allActorList.Count; i++)
            {
                ActorInfo info = allActorList[i];

                // get first known state of actor in replay
                var actorKey = "Actor#" + i;

                // the actor root transform position usually only appears once and
                // sits in the static transform states

                ItemState? firstState = null;

                if (staticItemStates.ContainsKey(actorKey))
                {
                    firstState = staticItemStates[actorKey];
                }
                else
                {
                    firstState = getFirstState(frameGroups[(byte)CaptureGroupId.Actor], actorKey)!;
                }

                if (firstState == null)
                {
                    throw new Exception("Failed to find first state of " + actorKey);
                }



                // compose actor spawn options
                Game.SpawnActorOpt spawnOpt = new Game.SpawnActorOpt();
                spawnOpt.name = "Actor#" + i;
                spawnOpt.primaryColor = info.primaryColor;
                spawnOpt.costumeColor = info.CostumeColor;
                spawnOpt.costumeIdsWithColors = info.costumeIdsWithColors;
                spawnOpt.controllerId = nextControllerId++;
                spawnOpt.position = firstState.pos;
                spawnOpt.rotation = Quaternion.Euler(firstState.rot);

                // spawn new actor
                var actor = Game.spawnActor(spawnOpt);

                actorNames[actor] = Types.getSpeakingNameOfColor(info.primaryColor);
                spawnedActors.Add(actor);

                // must be active or costume will not be applied
                actor.gameObject.SetActive(true);

            }

            // wait until actor setup process completed
            waitForActorsReadyTimeout = 5f;
            Replay.nextSetupStep = replaySetup_waitForActorsReady;
        }

        public float waitForActorsReadyTimeout = 0f;

        private void replaySetup_waitForActorsReady()
        {
            // an actor is ready when primaryColor was assigned. costume parts are spawned then.
            if (spawnedActors.Find(actor => Tools.isGrayColor(actor.primaryColor)) != null)
            {
                waitForActorsReadyTimeout -= Time.unscaledDeltaTime;
                if (waitForActorsReadyTimeout <= 0)
                {
                    // something went wrong. this can happen if the round was exited early.
                    // abort loading and return to menu
                    Replay.abortLoading();
                }
                return;
            }

            for (int j = 0; j < spawnedActors.Count; j++)
            {
                var actor = spawnedActors[j];
                var _name = actor.gameObject.name;
                var key = _name; // = Actor#N


                if (!frameGroups[(byte)CaptureGroupId.Actor][0].states.ContainsKey(key))
                {
                    // deactivate actors not present in first frame (wave actors)
                    actor.gameObject.SetActive(false);
                }
            }

            // spawn soccer ball on alley map
            if (mapName == "alley") spawnBall();

            // synchronize map state to replay map state
            preventDestruction = false;
            synchronizeMap();

            // trickery to fix some things
            fixStickyness();

            disableInterferences();

            // refresh transform cache
            tc.refresh();

            if (mapName == "subway")
            {
                // index Subway Loop sounds so their positions will be updated
                Game.getSubwayLoopAudioSourceTransforms();
            }

            Replay.replaySetup_createPlayer();
        }

        private ActorInfo binaryReadActorInfo(BinaryReader reader, int contentFlags)
        {
            ActorInfo recActor = new ActorInfo();
            recActor.primaryColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            recActor.CostumeColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            int costumeIdCount = reader.ReadInt32();
            ushort[] costumeIdsWithColors = new ushort[costumeIdCount];
            for (int j = 0; j < costumeIdCount; j++) costumeIdsWithColors[j] = reader.ReadUInt16();
            recActor.costumeIdsWithColors = costumeIdsWithColors;
            return recActor;
        }

        private void destroyExistingActors()
        {
            // destroy actors generated by the game during map load
            List<Actor> toDestroyActors = new();
            foreach (var actor in Actor._ActorCache) toDestroyActors.Add(actor);
            foreach (var actor in toDestroyActors) UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }

        private void padFrameData(List<Frame> frames)
        {
            // duplicate item states present in a loaded frame series to
            // exist in every frame of the series

            Dictionary<string, ItemState> rollingItemStates = new();

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                foreach (var entry in frame.states)
                {
                    var key = entry.Key;
                    var state = entry.Value;
                    rollingItemStates[key] = state;
                }

                foreach (var entry in rollingItemStates)
                {
                    if (!frame.states.ContainsKey(entry.Key))
                    {
                        frame.states[entry.Key] = entry.Value;
                    }
                }
            }
        }

        private void fixDeathBug(List<Frame> actorFrames)
        {
            // when an actor dies, costume parts are reattached to
            // the actor and also appear at 0/0/0 for a single
            // frame. the latter causes string effects in interpolated
            // frames. we remove those undesired states to fix
            // the stringing (does not fix the parts being reattached
            // to the actor)

            // conditions:
            // - transform belongs to an actor
            // - position is 0/0/0
            // - rotation is 0/0/0
            // - item is inactive

            for (int i = 0; i < actorFrames.Count; i++)
            {
                var transformKeys = actorFrames[i].states.Keys;

                foreach (var key in transformKeys)
                {
                    var state = actorFrames[i].states[key];
                    if (
                        state.pos == Vector3.zero &&
                        state.rot == Quaternion.identity.eulerAngles &&
                        state.active == false &&
                        key.StartsWith("Actor#")
                    )
                    {
                        actorFrames[i].states.Remove(key);
                    }
                }
            }
        }

        // makes randomized things in the scene look like in the replay
        private void synchronizeMap()
        {
            var map = Game.getSceneName();

            // active state of ice chunks on buoy is shuffled each
            // time. disable all chunks initially, they will be
            // re-enabled when a frame state is applied. if we don't
            // do this, there will be too much ice.

            if (map == "buoy")
            {
                var ice = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(t => t.name.StartsWith("IceChunk_"));
                foreach (var i in ice) i.gameObject.SetActive(false);
            }

            else if (map == "subway")
            {
                // randomized destruction of cones and puddle was prevented in the
                // Destroy() hook earlier. we now remove the cones not present in the
                // replay data. same for the puddle.

                var keysInReplay = getAllTransformKeysInFrames(frameGroups[CaptureGroupId.Environment]);

                // note: do not use GameObject.Find() since it won't find inactive cones

                var coneObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(go => go.name.StartsWith("trafficCone (")).ToList();

                // there are 5 cones
                for (int i = 0; i < 5; i++)
                {
                    var cone = coneObjects.FirstOrDefault(go => go.name == "trafficCone (" + i + ")");

                    if (cone != null)
                    {
                        var key = tc.getTransformInfo(cone.transform).key;
                        if (!keysInReplay.ContainsKey(key))
                        {
                            // deactivate the cone
                            cone.SetActive(false);
                            // UnityEngine.Object.Destroy(cone);
                        }
                        else
                        {
                            // make sure the cone is active
                            cone.SetActive(true);
                        }
                    }
                }

                // deactivate the drip (unsupported)
                var drip = GameObject.Find("PuddleGroup/drip");
                if (drip != null) drip.SetActive(false);

                // show/hide puddle depending on whether it exist in the replay
                var puddle = GameObject.Find("PuddleGroup/puddle");
                if (puddle != null)
                {
                    if (!keysInReplay.ContainsKey(tc.getTransformInfo(puddle.transform).key))
                    {
                        // deactivate the puddle
                        puddle.SetActive(false);
                    }
                    else
                    {
                        // make sure puddle is active
                        puddle.SetActive(true);
                    }
                }
            }

            // synchronize glass shards

            if (shardContainersData.Count > 0)
            {
                // shard we can clone to get the required number of shards
                GameObject? cloneShard = UnityEngine.Object.FindObjectOfType<FractureShard>(true)?.gameObject;
                foreach (var shardContainerData in shardContainersData)
                {
                    Glass.synchronizeGlass(shardContainerData, cloneShard);
                }
            }
        }

        private void fixStickyness()
        {
            var map = Game.getSceneName();
            if (map == "crane")
            {
                // deactivating and reactivating the crane object fixes
                // some parts not being sticky and drifting off without
                // extra tracking.

                var crane = GameObject.Find("/Crane");
                crane.SetActive(false);
                crane.SetActive(true);
            }
            else if (map == "containers")
            {
                var crane = GameObject.Find("/Crane A");
                crane.SetActive(false);
                crane.SetActive(true);
                crane = GameObject.Find("/Crane B");
                crane.SetActive(false);
                crane.SetActive(true);
            }
        }

        // spawns the soccer ball
        private void spawnBall()
        {
            GameObject football = UnityEngine.Object.Instantiate<GameObject>(MonoSingleton<Global>.Instance.SceneLoader.FootballData.Football, Vector3.zero, Quaternion.identity);
            NetworkServer.Spawn(football);
        }

        private HarmonyLib.Harmony? harmony = null;

        public void patch()
        {
            if (harmony != null) return;

            harmony = HarmonyLib.Harmony.CreateAndPatchAll(typeof(LoaderPatch), null);

            var play3dAt = typeof(AudioController).GetMethods().Where(m => m.Name == "Play3DAt" && m.GetParameters().Length == 15).ToList().First();
            var prefix = new HarmonyLib.HarmonyMethod(typeof(Loader).GetMethod("handlePlay3DAt"));
            if (harmony.Patch(play3dAt, prefix, null, null) == null)
            {
                logError("failed to patch Play3DAt");
            }
        }

        private bool preventDestruction = true;

        public static class LoaderPatch {

            
            [HarmonyLib.HarmonyPatch(typeof(UnityEngine.Object), "Destroy", new Type[] { typeof(UnityEngine.Object) })]
            [HarmonyLib.HarmonyPrefix]
            public static bool Object_Destroy(UnityEngine.Object obj)
            {
                if (!Replay.loader!.preventDestruction) return true;

                // prevent destruction of traffic cones and puddle on subway
                if (
                    obj != null &&
                    (
                        obj.name.StartsWith("trafficCone") ||
                        obj.name.StartsWith("puddle")
                    )
                )
                {
                    return false;
                }
                return true;
            }

            [HarmonyLib.HarmonyPatch(typeof(Actor), "set_actorState")]
            [HarmonyLib.HarmonyPrefix]

            // prevent the game from setting the actor state which causes sounds to play
            public static bool Actor_set_actorState(Actor __instance, ref Actor.ActorState value)
            {
                if (Replay.modState == ModState.LoadingReplay) return false;
                return true;
            }
        }

        public static bool handlePlay3DAt(
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
        )
        {
            // for some reason the Final KO sounds are played sometimes when creating actors.
            // mute that sound when loading a replay as a workaround.

            if (Replay.modState == Replay.ModState.LoadingReplay && clip.name.StartsWith("Final KO "))
            {
                return false;
            }

            return true;
        }

        public void unpatch()
        {
            if (harmony == null) return;
            harmony.UnpatchSelf();
            harmony = null;
        }

        public void destroy()
        {
            unpatch();
        }
    }
}
