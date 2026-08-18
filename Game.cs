using static PengooinLabs.ReplayMod.TransformCache;
using static PengooinLabs.ReplayMod.Types;
using System.Reflection;
using Il2Cpp;
using Il2CppFemur;
using Il2CppGB.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Il2CppAudio;
using Il2CppCoreNet.Contexts;
using Il2CppGB.Config;
using Il2CppGB.Platform.Lobby;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppGB.Game;
using Il2CppCoreNet.Model;
using Il2CppCoreNet.Objects;
using Il2CppCostumes;
using Il2CppGB.Networking.Objects;
using Il2CppGB.Networking.Utils;
using UnityEngine.Networking;
using Il2CppGB.UI.Menu;
using Il2CppGB.Game.Critters;
using Il2CppGB.UI.Beasts;
using Il2CppGB.UI.Lobby;

namespace PengooinLabs.ReplayMod
{
    public partial class Game
    {
        // track whether we're in menu, game loading, or in a fight scene
        public enum SceneType { Menu, Loading, Fight };

        private static SceneType _sceneType = SceneType.Loading;
        public static SceneType sceneType { get { return _sceneType; } }

        // initialization

        public static void init()
        {
            // track if we're in-menu/loading/in-game
            LoadingHooks.onShowLoadingScreen += () => { _sceneType = Game.SceneType.Loading; };
            LoadingHooks.onHideLoadingScreen += (string sceneName) => { updateSceneType(sceneName); };
            updateSceneType(Game.getSceneName());
        }

        private static void updateSceneType(string sceneName)
        {
            if (sceneName == Global.MENU_SCENE_NAME.ToLower())
            {
                _sceneType = Game.SceneType.Menu;
            }
            else if (sceneName == "_bootscene")
            {
                _sceneType = Game.SceneType.Loading;
            }
            else
            {
                _sceneType = Game.SceneType.Fight;
            }
        }

        public static string getSceneName()
        {
            return SceneManager.GetActiveScene().name.ToLower();
        }

        public static void startGame(string mapName)
        {
            var rotationConfig = GBConfigLoader.CreateRotationConfig(
                new Il2CppStringArray(new string[] { mapName }),
                Il2CppGB.Gamemodes.GameModeEnum.Melee,
                1,       // win count
                false,   // randomize map order
                30 * 3600 // time limit
            );

            var lobbyManager = LobbyManager.Instance;
            lobbyManager.LobbyStates.CurrentState = (LobbyState.State.Ready | LobbyState.State.InGame);
            lobbyManager.LobbyStates.UpdateLobbyState();
            lobbyManager.LocalBeasts.SetupNetMemberContext(false);
            NetMemberContext.LocalHostedGame = true;

            // use regular loading screen routine
            MonoSingleton<Global>.Instance.LevelLoadSystem.ShowLoadingScreen(1f, (Action)delegate {
                MonoSingleton<Global>.Instance.UNetManager.LaunchHost();
                GameManagerNew.Instance.ChangeRotationConfig(rotationConfig, 0);
            }, -1f, false);
        }

        // read actor's costume config
        public static ushort[] getActorCostumeIdsWithColors(Actor actor)
        {
            return actor.CostumeCompRef.SaveEntry.ExtractCostumeItemIDs(true);
        }

        public class SpawnActorOpt
        {
            public string name = "Actor";
            public Color primaryColor = Color.gray;
            public Color costumeColor = Color.gray;
            public ushort[] costumeIdsWithColors = new ushort[0];
            public int controllerId = 10;
            public Vector3 position = Vector3.zero;
            public Quaternion rotation = Quaternion.identity;
            public bool active = true;
        }

        public static Actor spawnActor(SpawnActorOpt opt)
        {
            // thanks to @HueSamai for figuring this out before

            // compose costume

            // create costumeSaveEntry holding the costume ids and colors and
            // its respective NetCostume. voice doesn't matter for replay.
            CostumeSaveEntry costumeSaveEntry = new CostumeSaveEntry(opt.costumeIdsWithColors, true);
            NetCostume netCostume = new NetCostume(costumeSaveEntry);
            netCostume.Voice = Actor.GetRandomVoice(false, true);

            int dummyGangId = 10;

            // create NetBeast
            NetBeast netBeast = new NetBeast(
                opt.controllerId,
                netCostume,
                opt.primaryColor,
                opt.costumeColor,
                dummyGangId,
                NetPlayer.PlayerType.AI,
                true // "dummy" (causes controlledBy = Animation)
            );

            netBeast.Alive = true;

            // create actor GameObject
            GameObject actorPrefab = MonoSingleton<Global>.Instance.SceneLoader.SpawnList.Spawnables[8].Item;
            GameObject actorGameObject = UnityEngine.Object.Instantiate<GameObject>(actorPrefab, opt.position, opt.rotation);
            actorGameObject.name = opt.name;
            netBeast.Instance = actorGameObject;

            // configure Actor object
            Actor actor = actorGameObject.GetComponent<Actor>();
            actor.ControlledBy = Actor.ControlledTypes.AI;
            actor.playerID = -1;
            actor.IsAI = true;
            actor.controllerID = netBeast.ControllerId;
            actor.gangID = dummyGangId;

            // update NetModel
            NetModel netModel = UnityEngine.Object.FindObjectOfType<NetModel>();
            netModel.Add<NetBeast>("NET_PLAYERS", netBeast);

            // other initialization stuff
            actor.DressBeast();
            NetworkServer.Spawn(actorGameObject);
            GBNetUtils.SetBeastsGang(netBeast);

            return actor;
        }

        public static bool paused()
        {
            return PauseManager.Instance.IsPaused;
        }

        public static List<TransformInfo> getSubwayLoopAudioSourceTransforms()
        {
            var ac = AudioController.Instance;
            var infos = new List<TransformInfo>();
            int n = -1;
            for (var i = 0; i < ac._pooledSources.Count; i++)
            {
                var source = ac._pooledSources[i];
                if (source != null && source.audioSource != null && source.audioSource.clip != null)
                {
                    if (source.audioSource.clip.name == "Subway Loop")
                    {
                        n++;
                        var audioSourceTransformKey = "audioclip:Subway Loop:" + n;
                        var info = Replay.tc.addTransformWithKey(source.audioSource.transform, audioSourceTransformKey);
                        infos.Add(info);
                    }
                }
            }
            return infos;
        }
        
        public static byte getActorBlink(Actor actor)
        {
            return (byte)(int)actor.effectsHandeler.blink;
        }

        public static void setActorBlink(Actor actor, float blink)
        {
            if (actor.effectsHandeler != null && actor.effectsHandeler.headMesh != null)
            {
                actor.effectsHandeler.headMesh.SetBlendShapeWeight(3, blink);
            }
        }

        public static void setBirdBlink(BirdActor bird, byte blink)
        {
            if (bird.skinnedMeshRenderer != null)
            {
                float weight = blink > 100f ? 100f : (float)blink;
                try
                {
                    bird.skinnedMeshRenderer.SetBlendShapeWeight(0, weight);
                } catch {
                    Replay.logError("exception setting weight " + weight);
                }
            }
        }

        public static string getVersion()
        {
            return GBConfigResourceLoader.ConfigData.GameVersion;
        }

        public static bool isInLocalGameMenu()
        {
            if (Game.sceneType != Game.SceneType.Menu) return false;
            return LobbyManager.Instance.LobbyStates.SelfState == LobbyState.Game.Local;
        }

        public static bool isInOnlineGameMenu()
        {
            if (Game.sceneType != Game.SceneType.Menu) return false;
            return LobbyManager.Instance.LobbyStates.SelfState == LobbyState.Game.Online;
        }

        public static bool isLobbyCountdownActive()
        {
            // TODO find out how to detect countdown
            return false;
        }

        public static bool isSinglePlayerSelected()
        {
            var designing = LobbyManager.Instance.LocalBeasts.TotalInState(BeastUtils.PlayerState.Designing);
            var ready = LobbyManager.Instance.LocalBeasts.TotalInState(BeastUtils.PlayerState.Ready);
            return designing + ready == 1;
        }

        public static void returnToMenu()
        {
            PauseManager.instance.OnReturnToMenu();
        }

    }
}
