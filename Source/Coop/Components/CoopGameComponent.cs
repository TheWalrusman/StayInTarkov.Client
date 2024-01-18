﻿using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using StayInTarkov.Configuration;
using StayInTarkov.Coop.Components;
using StayInTarkov.Coop.Matchmaker;
using StayInTarkov.Coop.Player;
using StayInTarkov.Coop.Players;
using StayInTarkov.Coop.Web;
using StayInTarkov.Core.Player;
using StayInTarkov.Memory;
using StayInTarkov.Networking;
using StayInTarkov.Networking.Packets;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Rect = UnityEngine.Rect;

namespace StayInTarkov.Coop
{
    /// <summary>
    /// Coop Game Component is the User 1-2-1 communication to the Server. This can be seen as an extension component to CoopGame.
    /// </summary>
    public class CoopGameComponent : MonoBehaviour
    {
        #region Fields/Properties        
        public Dictionary<string, WorldInteractiveObject> ListOfInteractiveObjects { get; private set; } = [];
        private AkiBackendCommunication RequestingObj { get; set; }
        public SITConfig SITConfig { get; private set; } = new SITConfig();
        public string ServerId { get; set; } = null;
        public long Timestamp { get; set; } = 0;
        public EFT.Player OwnPlayer { get; set; }

        /// <summary>
        /// ProfileId to Player instance
        /// </summary>
        public ConcurrentDictionary<string, CoopPlayer> Players { get; } = new();

        //public EFT.Player[] PlayerUsers
        public IEnumerable<EFT.Player> PlayerUsers
        {
            get
            {

                if (Players == null)
                    yield return null;

                var keys = Players.Keys.Where(x => x.StartsWith("pmc")).ToArray();
                foreach (var key in keys)
                    yield return Players[key];


            }
        }

        public EFT.Player[] PlayerBots
        {
            get
            {
                if (LocalGameInstance is CoopGame coopGame)
                {
                    if (MatchmakerAcceptPatches.IsClient || coopGame.Bots.Count == 0)
                        return Players.Values.Where(x => !x.ProfileId.StartsWith("pmc")).ToArray();

                    return coopGame.Bots.Values.ToArray();
                }

                return null;
            }
        }

        /// <summary>
        /// This is all the spawned players via the spawning process. Not anyone else.
        /// </summary>
        public Dictionary<string, CoopPlayer> SpawnedPlayers { get; private set; } = new();

        BepInEx.Logging.ManualLogSource Logger { get; set; }
        public ConcurrentDictionary<string, ESpawnState> PlayersToSpawn { get; private set; } = new();
        public ConcurrentDictionary<string, Dictionary<string, object>> PlayersToSpawnPacket { get; private set; } = new();
        public Dictionary<string, Profile> PlayersToSpawnProfiles { get; private set; } = new();
        public ConcurrentDictionary<string, Vector3> PlayersToSpawnPositions { get; private set; } = new();

        public List<EFT.LocalPlayer> SpawnedPlayersToFinalize { get; private set; } = new();
        public CoopPlayer MyPlayer => Singleton<GameWorld>.Instance.MainPlayer as CoopPlayer;

        public BlockingCollection<Dictionary<string, object>> ActionPackets => ActionPacketHandler.ActionPackets;

        private Dictionary<string, object>[] m_CharactersJson { get; set; }
        private List<string> queuedProfileIds = [];
        private Queue<SpawnObject> spawnQueue = new(50);

        public class SpawnObject(Profile profile, Vector3 position, bool isAlive)
        {
            public Profile Profile { get; set; } = profile;
            public Vector3 Position { get; set; } = position;
            public bool IsAlive { get; set; } = isAlive;
        }

        public bool RunAsyncTasks { get; set; } = true;

        float screenScale = 1.0f;

        Camera GameCamera { get; set; }

        public ActionPacketHandlerComponent ActionPacketHandler { get; } = CoopPatches.CoopGameComponentParent.GetOrAddComponent<ActionPacketHandlerComponent>();

        #endregion

        #region Public Voids

        public static CoopGameComponent GetCoopGameComponent()
        {
            if (CoopPatches.CoopGameComponentParent == null)
                return null;

            var coopGameComponent = CoopPatches.CoopGameComponentParent.GetComponent<CoopGameComponent>();
            if (coopGameComponent != null)
                return coopGameComponent;

            return null;
        }

        public static bool TryGetCoopGameComponent(out CoopGameComponent coopGameComponent)
        {
            coopGameComponent = GetCoopGameComponent();
            return coopGameComponent != null;
        }

        public static string GetServerId()
        {
            var coopGC = GetCoopGameComponent();
            if (coopGC == null)
                return null;

            return coopGC.ServerId;
        }
        #endregion

        #region Unity Component Methods

        /// <summary>
        /// Unity Component Awake Method
        /// </summary>
        void Awake()
        {
            // ----------------------------------------------------
            // Create a BepInEx Logger for CoopGameComponent
            Logger = BepInEx.Logging.Logger.CreateLogSource("CoopGameComponent");
            Logger.LogDebug("CoopGameComponent:Awake");

            SITCheck();
        }

        /// <summary>
        /// Check the StayInTarkov assembly hasn't been messed with.
        /// </summary>
        void SITCheck()
        {
            // Check the StayInTarkov assembly hasn't been messed with.
            SITCheckConfirmed[0] = StayInTarkovHelperConstants
                .SITTypes
                .Any(x => x.Name ==
                Encoding.UTF8.GetString(new byte[] { 0x4c, 0x65, 0x67, 0x61, 0x6c, 0x47, 0x61, 0x6d, 0x65, 0x43, 0x68, 0x65, 0x63, 0x6b }))
                ? (byte)0x1 : (byte)0x0;
        }



        /// <summary>
        /// Unity Component Start Method
        /// </summary>
        async void Start()
        {
            Logger.LogDebug("CoopGameComponent:Start");

            // Get Reference to own Player
            OwnPlayer = (LocalPlayer)Singleton<GameWorld>.Instance.MainPlayer;

            // Add own Player to Players list
            Players.TryAdd(OwnPlayer.ProfileId, OwnPlayer as CoopPlayer);

            // Instantiate the Requesting Object for Aki Communication
            RequestingObj = AkiBackendCommunication.GetRequestInstance(false, Logger);

            // Request SIT Config
            await RequestingObj.PostJsonAsync<SITConfig>("/SIT/Config", "{}").ContinueWith(x =>
            {
                if (x.IsCanceled || x.IsFaulted)
                {
                    SITConfig = new SITConfig();
                    Logger.LogError("SIT Config Failed!");
                }
                else
                {
                    SITConfig = x.Result;
                    Logger.LogDebug("SIT Config received Successfully!");
                    Logger.LogDebug(SITConfig.ToJson());

                }
            });

            // Run an immediate call to get characters in the server
            if (MatchmakerAcceptPatches.IsClient)
            {
                ReadFromServerCharacters();
            }

            // Get a Result of Characters within an interval loop
            if (MatchmakerAcceptPatches.IsClient)
            {
                _ = Task.Run(() => ReadFromServerCharactersLoop());
            }

            // Run any methods you wish every second
            StartCoroutine(EverySecondCoroutine());

            StartCoroutine(ProcessSpawnQueue());

            // Start the SIT Garbage Collector
            _ = Task.Run(() => PeriodicEnableDisableGC());

            // Get a List of Interactive Objects (this is a slow method), so run once here to maintain a reference
            WorldInteractiveObject[] interactiveObjects = FindObjectsOfType<WorldInteractiveObject>();
            foreach (WorldInteractiveObject interactiveObject in interactiveObjects)
            {
                ListOfInteractiveObjects.Add(interactiveObject.Id, interactiveObject);
            }

            // Enable the Coop Patches
            CoopPatches.EnableDisablePatches();

            // Send My Player to Aki, so that other clients know about me
            Player_Init_Coop_Patch.SendPlayerDataToServer((LocalPlayer)Singleton<GameWorld>.Instance.RegisteredPlayers.First(x => x.IsYourPlayer));

        }

        /// <summary>
        /// Last stored memory allocation from the SIT Garbage Collector
        /// </summary>
        private long? _SITGCLastMemory;

        /// <summary>
        /// This clears out the RAM usage very effectively.
        /// </summary>
        /// <returns></returns>
        private async Task PeriodicEnableDisableGC()
        {
            var coopGame = LocalGameInstance as CoopGame;
            if (coopGame == null)
                return;

            int counter = 0;
            await Task.Run(async () =>
            {
                do
                {
                    await Task.Delay(1000);

                    counter++;

                    var memory = GC.GetTotalMemory(false);
                    if (!_SITGCLastMemory.HasValue)
                        _SITGCLastMemory = memory;

                    long memoryThreshold = PluginConfigSettings.Instance.AdvancedSettings.SITGCMemoryThreshold;

                    if (_SITGCLastMemory.HasValue && memory > _SITGCLastMemory.Value + (memoryThreshold * 1024 * 1024))
                    {
                        Logger.LogDebug($"Current Memory Allocated:{memory / 1024 / 1024}mb");
                        _SITGCLastMemory = memory;
                        Stopwatch sw = Stopwatch.StartNew();

                        GCHelpers.EnableGC();
                        if (PluginConfigSettings.Instance.AdvancedSettings.SITGCAggressiveClean)
                        {
                            GCHelpers.ClearGarbage(true, PluginConfigSettings.Instance.AdvancedSettings.SITGCClearAssets);
                        }
                        else
                        {
                            GC.GetTotalMemory(true);
                            GCHelpers.DisableGC(true);
                        }

                        var freedMemory = GC.GetTotalMemory(false);
                        Logger.LogDebug($"Freed {(freedMemory > 0 ? (freedMemory / 1024 / 1024) : 0)}mb in memory");
                        Logger.LogDebug($"Garbage Collection took {sw.ElapsedMilliseconds}ms");
                        sw.Stop();
                        sw = null;

                    }

                } while (RunAsyncTasks && PluginConfigSettings.Instance.AdvancedSettings.UseSITGarbageCollector);
            });
        }

        /// <summary>
        /// This is a simple coroutine to allow methods to run every second.
        /// </summary>
        /// <returns></returns>
        private IEnumerator EverySecondCoroutine()
        {
            var waitSeconds = new WaitForSeconds(1.0f);
            var coopGame = LocalGameInstance as CoopGame;
            if (coopGame == null)
                yield return null;

            while (RunAsyncTasks)
            {
                yield return waitSeconds;

                var playersToExtract = new List<string>();
                foreach (var exfilPlayer in coopGame.ExtractingPlayers)
                {
                    var exfilTime = new TimeSpan(0, 0, (int)exfilPlayer.Value.Item1);
                    var timeInExfil = new TimeSpan(DateTime.Now.Ticks - exfilPlayer.Value.Item2);
                    if (timeInExfil >= exfilTime)
                    {
                        if (!playersToExtract.Contains(exfilPlayer.Key))
                        {
                            Logger.LogDebug(exfilPlayer.Key + " should extract");
                            playersToExtract.Add(exfilPlayer.Key);
                        }
                    }
                    else
                    {
                        Logger.LogDebug(exfilPlayer.Key + " extracting " + timeInExfil);

                    }
                }

                foreach (var player in playersToExtract)
                {
                    coopGame.ExtractingPlayers.Remove(player);
                    coopGame.ExtractedPlayers.Add(player);
                }

                var world = Singleton<GameWorld>.Instance;

                // Hide extracted Players
                foreach (var profileId in coopGame.ExtractedPlayers)
                {
                    var player = world.RegisteredPlayers.Find(x => x.ProfileId == profileId) as EFT.Player;
                    if (player == null)
                        continue;

                    if (!ExtractedProfilesSent.Contains(profileId))
                    {
                        ExtractedProfilesSent.Add(profileId);
                        AkiBackendCommunicationCoop.PostLocalPlayerData(player
                            , new Dictionary<string, object>() { { "m", "Extraction" }, { "Extracted", true } }
                            );
                    }

                    if (player.ActiveHealthController != null)
                    {
                        if (!player.ActiveHealthController.MetabolismDisabled)
                        {
                            player.ActiveHealthController.AddDamageMultiplier(0);
                            player.ActiveHealthController.SetDamageCoeff(0);
                            player.ActiveHealthController.DisableMetabolism();
                            player.ActiveHealthController.PauseAllEffects();

                            player.SwitchRenderer(false);

                            // TODO: Currently. Destroying your own Player just breaks the game and it appears to be "frozen". Need to learn a new way to do a FreeCam!
                            if (Singleton<GameWorld>.Instance.MainPlayer.ProfileId != profileId)
                                GameObject.Destroy(player);
                        }
                    }
                }
            }
        }

        private HashSet<string> ExtractedProfilesSent = new();

        void OnDestroy()
        {
            StayInTarkovHelperConstants.Logger.LogDebug($"CoopGameComponent:OnDestroy");

            if (Players != null)
            {
                foreach (var pl in Players)
                {
                    if (pl.Value == null)
                        continue;

                    if (pl.Value.TryGetComponent<PlayerReplicatedComponent>(out var prc))
                    {
                        GameObject.DestroyImmediate(prc);
                    }
                }
            }
            Players.Clear();
            PlayersToSpawnProfiles.Clear();
            PlayersToSpawnPositions.Clear();
            PlayersToSpawnPacket.Clear();
            RunAsyncTasks = false;
            StopCoroutine(ProcessServerCharacters());
            StopCoroutine(EverySecondCoroutine());

            CoopPatches.EnableDisablePatches();
        }

        TimeSpan LateUpdateSpan = TimeSpan.Zero;
        Stopwatch swActionPackets { get; } = new Stopwatch();
        bool PerformanceCheck_ActionPackets { get; set; } = false;
        public bool RequestQuitGame { get; set; }

        /// <summary>
        /// The state your character or game is in to Quit.
        /// </summary>
        public enum EQuitState
        {
            NONE = -1,
            YouAreDead,
            YouAreDeadAsHost,
            YouAreDeadAsClient,
            YourTeamIsDead,
            YourTeamHasExtracted,
            YouHaveExtractedOnlyAsHost,
            YouHaveExtractedOnlyAsClient
        }

        public EQuitState GetQuitState()
        {
            var quitState = EQuitState.NONE;

            if (!Singleton<ISITGame>.Instantiated)
                return quitState;

            var coopGame = Singleton<ISITGame>.Instance;
            if (coopGame == null)
                return quitState;

            if (Players == null)
                return quitState;

            if (PlayerUsers == null)
                return quitState;

            if (coopGame.ExtractedPlayers == null)
                return quitState;

            var numberOfPlayersDead = PlayerUsers.Count(x => !x.HealthController.IsAlive);
            var numberOfPlayersAlive = PlayerUsers.Count(x => x.HealthController.IsAlive);
            var numberOfPlayersExtracted = coopGame.ExtractedPlayers.Count;

            var world = Singleton<GameWorld>.Instance;

            // You are playing with a team
            if (PlayerUsers.Count() > 1)
            {
                // All Player's in the Raid are dead
                if (PlayerUsers.Count() == numberOfPlayersDead)
                {
                    quitState = EQuitState.YourTeamIsDead;
                }
                else if (!world.MainPlayer.HealthController.IsAlive)
                {
                    if (MatchmakerAcceptPatches.IsClient)
                        quitState = EQuitState.YouAreDeadAsClient;
                    else if (MatchmakerAcceptPatches.IsServer)
                        quitState = EQuitState.YouAreDeadAsHost;
                }
            }
            else if (PlayerUsers.Any(x => !x.HealthController.IsAlive))
            {
                quitState = EQuitState.YouAreDead;
            }

            // -------------------------
            // Extractions
            if (coopGame.ExtractedPlayers.Contains(world.MainPlayer.ProfileId))
            {
                if (MatchmakerAcceptPatches.IsClient)
                    quitState = EQuitState.YouHaveExtractedOnlyAsClient;
                else if (MatchmakerAcceptPatches.IsServer)
                    quitState = EQuitState.YouHaveExtractedOnlyAsHost;
            }

            if (numberOfPlayersAlive == numberOfPlayersExtracted || PlayerUsers.Count() == numberOfPlayersExtracted)
            {
                quitState = EQuitState.YourTeamHasExtracted;
            }
            return quitState;
        }

        /// <summary>
        /// This handles the ways of exiting the active game session
        /// </summary>
        void ProcessQuitting()
        {
            var quitState = GetQuitState();

            if (Input.GetKeyDown(KeyCode.F8) && quitState != EQuitState.NONE && !RequestQuitGame)
            {
                RequestQuitGame = true;

                // If you are the server / host
                if (MatchmakerAcceptPatches.IsServer)
                {
                    // A host needs to wait for the team to extract or die!
                    if ((MyPlayer.Server.NetServer.ConnectedPeersCount > 0) && (quitState == EQuitState.YouAreDeadAsHost || quitState == EQuitState.YouHaveExtractedOnlyAsHost))
                    {
                        NotificationManagerClass.DisplayWarningNotification("HOSTING: You cannot exit the game until all clients have extracted or died.");
                        RequestQuitGame = false;
                        return;
                    }
                    else
                    {
                        Singleton<ISITGame>.Instance.Stop(Singleton<GameWorld>.Instance.MainPlayer.ProfileId,
                            Singleton<ISITGame>.Instance.MyExitStatus,
                            Singleton<ISITGame>.Instance.MyExitLocation, 0);
                    }
                }
                else
                {
                    Singleton<ISITGame>.Instance.Stop(Singleton<GameWorld>.Instance.MainPlayer.ProfileId,
                        Singleton<ISITGame>.Instance.MyExitStatus,
                        Singleton<ISITGame>.Instance.MyExitLocation, 0);
                }
                return;
            }
        }

        /// <summary>
        /// This handles the possibility the server has stopped / disconnected and exits your player out of the game
        /// </summary>
        void ProcessServerHasStopped()
        {
            if (ServerHasStopped && !ServerHasStoppedActioned)
            {
                ServerHasStoppedActioned = true;
                try
                {
                    LocalGameInstance.Stop(Singleton<GameWorld>.Instance.MainPlayer.ProfileId, Singleton<ISITGame>.Instance.MyExitStatus, Singleton<ISITGame>.Instance.MyExitLocation, 0);
                }
                catch { }
                return;
            }
        }

        void Update()
        {
            GameCamera = Camera.current;

            if (!Singleton<ISITGame>.Instantiated)
                return;

            ProcessQuitting();
            ProcessServerHasStopped();

            if (ActionPackets == null)
                return;

            if (Players == null)
                return;

            if (Singleton<GameWorld>.Instance == null)
                return;

            if (RequestingObj == null)
                return;

            if (SpawnedPlayersToFinalize == null)
                return;

            List<LocalPlayer> SpawnedPlayersToRemoveFromFinalizer = [];
            foreach (var p in SpawnedPlayersToFinalize)
            {
                SetWeaponInHandsOfNewPlayer(p, () =>
                {

                    SpawnedPlayersToRemoveFromFinalizer.Add(p);
                });
            }
            foreach (var p in SpawnedPlayersToRemoveFromFinalizer)
            {
                SpawnedPlayersToFinalize.Remove(p);
            }

            // In game ping system.
            if (Singleton<FrameMeasurer>.Instantiated)
            {
                FrameMeasurer instance = Singleton<FrameMeasurer>.Instance;
                instance.PlayerRTT = ServerPing;
                instance.ServerFixedUpdateTime = ServerPing;
                instance.ServerTime = ServerPing;
            }

            if (Singleton<PreloaderUI>.Instantiated && SITCheckConfirmed[0] == 0 && SITCheckConfirmed[1] == 0)
            {
                SITCheckConfirmed[1] = 1;
                Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen("", StayInTarkovPlugin.IllegalMessage, ErrorScreen.EButtonType.QuitButton, 60, () => { Application.Quit(); }, () => { Application.Quit(); });
            }
        }

        byte[] SITCheckConfirmed { get; } = new byte[2] { 0, 0 };

        #endregion

        private async Task ReadFromServerCharactersLoop()
        {
            if (GetServerId() == null)
                return;


            while (RunAsyncTasks)
            {
                await Task.Delay(5000);

                if (Players == null)
                    continue;

                ReadFromServerCharacters();
            }
        }

        private void ReadFromServerCharacters()
        {
            //AllCharacterRequestPacket requestPacket = new(profileId: MyPlayer.ProfileId)
            //{
            //    CharactersAmount = Players.Count + queuedProfileIds.Count,
            //};
            //requestPacket.Characters = new string[requestPacket.CharactersAmount];
            //for (int i = 0; i < Players.Count; i++)
            //{
            //    requestPacket.Characters[i] = Players.ElementAt(i).Key;
            //}
            //for (int i = 0; i < queuedProfileIds.Count; i++)
            //{
            //    requestPacket.Characters[i] = Players.ElementAt(i).Key;
            //}
            //MyPlayer.Client.SendData(MyPlayer.Writer, ref requestPacket, LiteNetLib.DeliveryMethod.ReliableOrdered);
            AllCharacterRequestPacket requestPacket = new(profileId: MyPlayer.ProfileId)
            {
                CharactersAmount = Players.Count
            };
            requestPacket.Characters = new string[requestPacket.CharactersAmount];
            for (int i = 0; i < Players.Count; i++)
            {
                requestPacket.Characters[i] = Players.ElementAt(i).Key;
            }
            MyPlayer.Client.SendData(MyPlayer.Writer, ref requestPacket, LiteNetLib.DeliveryMethod.ReliableOrdered);
        }

        private IEnumerator ProcessServerCharacters()
        {
            var waitEndOfFrame = new WaitForEndOfFrame();

            if (GetServerId() == null)
                yield return waitEndOfFrame;

            var waitSeconds = new WaitForSeconds(0.5f);

            while (RunAsyncTasks)
            {
                yield return waitSeconds;
                foreach (var p in PlayersToSpawn)
                {
                    // If not showing drones. Check whether the "Player" has been registered, if they have, then ignore the drone
                    if (!PluginConfigSettings.Instance.CoopSettings.SETTING_DEBUGSpawnDronesOnServer)
                    {
                        if (Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.ProfileId == p.Key))
                        {
                            if (PlayersToSpawn.ContainsKey(p.Key))
                                PlayersToSpawn[p.Key] = ESpawnState.Ignore;

                            continue;
                        }

                        if (Players.Any(x => x.Key == p.Key))
                        {
                            if (PlayersToSpawn.ContainsKey(p.Key))
                                PlayersToSpawn[p.Key] = ESpawnState.Ignore;

                            continue;
                        }
                    }


                    if (PlayersToSpawn[p.Key] == ESpawnState.Ignore)
                        continue;

                    if (PlayersToSpawn[p.Key] == ESpawnState.Spawned)
                        continue;
                }


                yield return waitEndOfFrame;
            }
        }

        private async Task SpawnPlayer(SpawnObject spawnObject)
        {
            if (Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.ProfileId == spawnObject.Profile.ProfileId))
                return;

            if (Singleton<GameWorld>.Instance.AllAlivePlayersList.Any(x => x.ProfileId == spawnObject.Profile.ProfileId))
                return;

            int playerId = Players.Count + Singleton<GameWorld>.Instance.RegisteredPlayers.Count + 1;
            if (spawnObject.Profile == null)
            {
                Logger.LogError("CreatePhysicalOtherPlayerOrBot Profile is NULL!");
                queuedProfileIds.Remove(spawnObject.Profile.ProfileId);
                return;
            }

            IEnumerable<ResourceKey> allPrefabPaths = spawnObject.Profile.GetAllPrefabPaths();
            if (allPrefabPaths.Count() == 0)
            {
                Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{spawnObject.Profile.Info.Nickname}::PrefabPaths are empty!");
                PlayersToSpawn[spawnObject.Profile.ProfileId] = ESpawnState.Error;
                return;
            }

            await Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(PoolManager.PoolsCategory.Raid,
                PoolManager.AssemblyType.Local,
                allPrefabPaths.ToArray(),
                JobPriority.General).ContinueWith(x =>
            {
                if (x.IsCompleted)
                {
                    PlayersToSpawn[spawnObject.Profile.ProfileId] = ESpawnState.Spawning;
                    Logger.LogDebug($"CreatePhysicalOtherPlayerOrBot::{spawnObject.Profile.Info.Nickname}::Load Complete.");
                }
                else if (x.IsFaulted)
                {
                    Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{spawnObject.Profile.Info.Nickname}::Load Failed.");
                }
                else if (x.IsCanceled)
                {
                    Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{spawnObject.Profile.Info.Nickname}::Load Cancelled?");
                }
            });

            //if (PlayersToSpawn[spawnObject.Profile.ProfileId] == ESpawnState.Spawned)
            //{
            //    Logger.LogDebug($"CreatePhysicalOtherPlayerOrBot::{spawnObject.Profile.Info.Nickname}::Is already spawned");
            //    return;
            //}

            //PlayersToSpawn[spawnObject.Profile.ProfileId] = ESpawnState.Spawned;

            LocalPlayer otherPlayer = CreateLocalPlayer(spawnObject.Profile, spawnObject.Position, playerId);

            if (!spawnObject.IsAlive)
            {
                //Create corpse instead?
                otherPlayer.ActiveHealthController.Kill(EDamageType.Undefined);
            }

            queuedProfileIds.Remove(spawnObject.Profile.ProfileId);
        }

        private IEnumerator ProcessSpawnQueue()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.5f);

                if (Singleton<AbstractGame>.Instantiated && (Singleton<AbstractGame>.Instance.Status == GameStatus.Starting || Singleton<AbstractGame>.Instance.Status == GameStatus.Started))
                {
                    if (spawnQueue.Count > 0)
                    {
                        Task spawnTask = SpawnPlayer(spawnQueue.Dequeue());
                        //yield return new WaitUntil(() => spawnTask.IsCompleted);                        
                    }
                    else
                    {
                        yield return new WaitForSeconds(2);
                    }
                }
                else
                {
                    yield return new WaitForSeconds(1);
                }
            }
        }

        private void ProcessPlayerBotSpawn2(string profileId, Vector3 newPosition, bool isBot, Profile profile, bool isAlive = true)
        {
            // If not showing drones. Check whether the "Player" has been registered, if they have, then ignore the drone
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_DEBUGSpawnDronesOnServer)
            {
                if (Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.ProfileId == profileId))
                {
                    if (PlayersToSpawn.ContainsKey(profileId))
                        PlayersToSpawn[profileId] = ESpawnState.Ignore;

                    return;
                }

                if (Players.Keys.Any(x => x == profileId))
                {
                    if (PlayersToSpawn.ContainsKey(profileId))
                        PlayersToSpawn[profileId] = ESpawnState.Ignore;

                    return;
                }
            }


            // If CreatePhysicalOtherPlayerOrBot has been done before. Then ignore the Deserialization section and continue.
            if (PlayersToSpawn.ContainsKey(profileId) && PlayersToSpawnProfiles.ContainsKey(profileId) && PlayersToSpawnProfiles[profileId] != null)
            {
                var isDead = !isAlive;
                CreatePhysicalOtherPlayerOrBot(PlayersToSpawnProfiles[profileId], newPosition, isDead);
                return;
            }

            if (PlayersToSpawnProfiles.ContainsKey(profileId))
                return;

            PlayersToSpawnProfiles.Add(profileId, null);

            Logger.LogDebug($"ProcessPlayerBotSpawn:{profileId}");

            if (profile != null)
            {
                //Logger.LogInfo("Obtained Profile");
                profile.Skills.StartClientMode();
                // Send to be loaded
                PlayersToSpawnProfiles[profileId] = profile;
            }
            else
            {
                Logger.LogError("Unable to Parse Profile");
                PlayersToSpawn[profileId] = ESpawnState.Error;
                return;
            }
        }

        public void QueueProfile(Profile profile, Vector3 position, bool isAlive = true)
        {
            if (Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.ProfileId == profile.ProfileId))
                return;

            if (Singleton<GameWorld>.Instance.AllAlivePlayersList.Any(x => x.ProfileId == profile.ProfileId))
                return;

            if (queuedProfileIds.Contains(profile.ProfileId))
                return;

            queuedProfileIds.Add(profile.ProfileId);
            ConsoleScreen.Log("Queueing profile.");
            spawnQueue.Enqueue(new SpawnObject(profile, position, isAlive));
        }

        private void CreatePhysicalOtherPlayerOrBot(Profile profile, Vector3 position, bool isDead = false)
        {
            try
            {
                // A final check to stop duplicate clones spawning on Server
                if (!PluginConfigSettings.Instance.CoopSettings.SETTING_DEBUGSpawnDronesOnServer)
                {
                    if (Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.ProfileId == profile.ProfileId))
                        return;


                    if (Singleton<GameWorld>.Instance.AllAlivePlayersList.Any(x => x.ProfileId == profile.ProfileId))
                        return;

                    if (Players.Keys.Any(x => x == profile.ProfileId))
                        return;
                }

                if (Players == null)
                {
                    Logger.LogError("Players is NULL!");
                    return;
                }

                int playerId = Players.Count + Singleton<GameWorld>.Instance.RegisteredPlayers.Count + 1;
                if (profile == null)
                {
                    Logger.LogError("CreatePhysicalOtherPlayerOrBot Profile is NULL!");
                    return;
                }

                PlayersToSpawn.TryAdd(profile.ProfileId, ESpawnState.None);
                if (PlayersToSpawn[profile.ProfileId] == ESpawnState.None)
                {
                    PlayersToSpawn[profile.ProfileId] = ESpawnState.Loading;
                    IEnumerable<ResourceKey> allPrefabPaths = profile.GetAllPrefabPaths();
                    if (allPrefabPaths.Count() == 0)
                    {
                        Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{profile.Info.Nickname}::PrefabPaths are empty!");
                        PlayersToSpawn[profile.ProfileId] = ESpawnState.Error;
                        return;
                    }

                    //Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(PoolManager.PoolsCategory.Raid, PoolManager.AssemblyType.Local,
                    //[ResourceBundleConstants.PLAYER_SPIRIT_RESOURCE_KEY], JobPriority.General);

                    Singleton<PoolManager>.Instance.LoadBundlesAndCreatePools(PoolManager.PoolsCategory.Raid, PoolManager.AssemblyType.Local, allPrefabPaths.ToArray(), JobPriority.General)
                        .ContinueWith(x =>
                        {
                            if (x.IsCompleted)
                            {
                                PlayersToSpawn[profile.ProfileId] = ESpawnState.Spawning;
                                Logger.LogDebug($"CreatePhysicalOtherPlayerOrBot::{profile.Info.Nickname}::Load Complete.");
                            }
                            else if (x.IsFaulted)
                            {
                                Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{profile.Info.Nickname}::Load Failed.");
                            }
                            else if (x.IsCanceled)
                            {
                                Logger.LogError($"CreatePhysicalOtherPlayerOrBot::{profile.Info.Nickname}::Load Cancelled?.");
                            }
                        });

                    return;
                }

                // ------------------------------------------------------------------
                // Its loading on the previous pass, ignore this one until its finished
                if (PlayersToSpawn[profile.ProfileId] == ESpawnState.Loading)
                {
                    return;
                }

                // ------------------------------------------------------------------
                // It has already spawned, we should never reach this point if Players check is working in previous step
                if (PlayersToSpawn[profile.ProfileId] == ESpawnState.Spawned)
                {
                    Logger.LogDebug($"CreatePhysicalOtherPlayerOrBot::{profile.Info.Nickname}::Is already spawned");
                    return;
                }

                // Move this here. Ensure that this is run before it attempts again on slow PCs
                PlayersToSpawn[profile.ProfileId] = ESpawnState.Spawned;

                // ------------------------------------------------------------------
                // Create Local Player drone
                LocalPlayer otherPlayer = CreateLocalPlayer(profile, position, playerId);
                // TODO: I would like to use the following, but it causes the drones to spawn without a weapon.
                //CreateLocalPlayerAsync(profile, position, playerId);

                if (isDead)
                {
                    // Logger.LogDebug($"{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff")}: CreatePhysicalOtherPlayerOrBot::Killing localPlayer with ID {playerId}");
                    otherPlayer.ActiveHealthController.Kill(EDamageType.Undefined);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.ToString());
            }

        }

        private LocalPlayer CreateLocalPlayer(Profile profile, Vector3 position, int playerId)
        {
            // If this is an actual PLAYER player that we're creating a drone for, when we set
            // aiControl to true then they'll automatically run voice lines (eg when throwing
            // a grenade) so we need to make sure it's set to FALSE for the drone version of them.
            var isAI = !profile.Id.StartsWith("pmc");

            // For actual bots, we can gain SIGNIFICANT clientside performance on the
            // non-host client by ENABLING aiControl for the bot. This has zero consequences
            // in terms of synchronization. No idea why having aiControl OFF is so expensive,
            // perhaps it's more accurate to think of it as an inverse bool of
            // "player controlled", where the engine has to enable a bunch of additional
            // logic when aiControl is turned off (in other words, for players)?

            if (!isAI)
            {
                var myPlayer = Singleton<GameWorld>.Instance.MainPlayer as CoopPlayer;
                position = new Vector3(myPlayer.Transform.position.x, myPlayer.Transform.position.y + 0.25f, myPlayer.Transform.position.z);
            }

            //var otherPlayer = LocalPlayer.Create(playerId
            var otherPlayer = ObservedCoopPlayer.CreateObservedPlayer(
                playerId,
                position,
                Quaternion.identity,
                "Player",
                "",
                EPointOfView.ThirdPerson,
                profile,
                isAI,
                EUpdateQueue.Update,
                EFT.Player.EUpdateMode.Manual,
                EFT.Player.EUpdateMode.Auto,
                BackendConfigManager.Config.CharacterController.ObservedPlayerMode,
                () => Singleton<SettingsManager>.Instance.Control.Settings.MouseSensitivity,
                () => Singleton<SettingsManager>.Instance.Control.Settings.MouseAimingSensitivity,
                FilterCustomizationClass.Default,
                null,
                false,
                true).Result;

            if (otherPlayer == null)
                return null;

            // ----------------------------------------------------------------------------------------------------
            // Add the player to the custom Players list
            if (!Players.ContainsKey(profile.ProfileId))
                Players.TryAdd(profile.ProfileId, otherPlayer as CoopPlayer);

            if (!Singleton<GameWorld>.Instance.RegisteredPlayers.Any(x => x.Profile.ProfileId == profile.ProfileId))
                Singleton<GameWorld>.Instance.RegisteredPlayers.Add(otherPlayer);

            if (!SpawnedPlayers.ContainsKey(profile.ProfileId))
                SpawnedPlayers.Add(profile.ProfileId, otherPlayer as ObservedCoopPlayer);

            // Create/Add PlayerReplicatedComponent to the LocalPlayer
            // This shouldn't be needed. Handled in CoopPlayer.Create code
            var prc = otherPlayer.GetOrAddComponent<PlayerReplicatedComponent>();
            prc.IsClientDrone = true;

            if (!MatchmakerAcceptPatches.IsClient)
            {
                if (otherPlayer.ProfileId.StartsWith("pmc"))
                {
                    if (LocalGameInstance != null)
                    {
                        var botController = (BotsController)ReflectionHelpers.GetFieldFromTypeByFieldType(typeof(BaseLocalGame<GamePlayerOwner>), typeof(BotsController)).GetValue(LocalGameInstance);
                        if (botController != null)
                        {
                            // Start Coroutine as botController might need a while to start sometimes...
                            StartCoroutine(AddClientToBotEnemies(botController, otherPlayer));
                        }
                    }
                }
            }

            if (isAI)
            {
                if (profile.Info.Side == EPlayerSide.Bear || profile.Info.Side == EPlayerSide.Usec)
                {
                    var backpackSlot = profile.Inventory.Equipment.GetSlot(EquipmentSlot.Backpack);
                    var backpack = backpackSlot.ContainedItem;
                    if (backpack != null)
                    {
                        Item[] items = backpack.GetAllItems()?.ToArray();
                        if (items != null)
                        {
                            for (int i = 0; i < items.Count(); i++)
                            {
                                Item item = items[i];
                                if (item == backpack)
                                    continue;

                                item.SpawnedInSession = true;
                            }
                        }
                    }
                }
            }
            else // Make Player PMC items are all not 'FiR'
            {
                Item[] items = profile.Inventory.AllPlayerItems?.ToArray();
                if (items != null)
                {
                    for (int i = 0; i < items.Count(); i++)
                    {
                        Item item = items[i];
                        item.SpawnedInSession = false;
                    }
                }
            }

            //if (!SpawnedPlayersToFinalize.Any(x => otherPlayer))
            //    SpawnedPlayersToFinalize.Add(otherPlayer);

            Logger.LogDebug($"CreateLocalPlayer::{profile.Info.Nickname}::Spawned.");

            SetWeaponInHandsOfNewPlayer(otherPlayer, () => { });

            return otherPlayer;
        }

        private IEnumerator AddClientToBotEnemies(BotsController botController, LocalPlayer playerToAdd)
        {
            yield return new WaitForSeconds(5);
            Logger.LogDebug($"Adding Client {playerToAdd.Profile.Nickname} to enemy list");
            botController.AddActivePLayer(playerToAdd);
            if (botController.BotSpawner != null)
            {
                for (int i = 0; i < botController.BotSpawner.PlayersCount; i++)
                {
                    if (botController.BotSpawner.GetPlayer(i) == playerToAdd)
                    {
                        Logger.LogDebug($"Verified that {playerToAdd.Profile.Nickname} was added to the enemy list.");
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to set up the New Player with the current weapon after spawning
        /// </summary>
        /// <param name="person"></param>
        public void SetWeaponInHandsOfNewPlayer(EFT.Player person, Action successCallback)
        {
            var equipment = person.Profile.Inventory.Equipment;
            if (equipment == null)
            {
                Logger.LogError($"SetWeaponInHandsOfNewPlayer: {person.Profile.ProfileId} has no Equipment!");
            }
            Item item = null;

            if (equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon).ContainedItem != null)
                item = equipment.GetSlot(EquipmentSlot.FirstPrimaryWeapon).ContainedItem;

            if (item == null && equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon).ContainedItem != null)
                item = equipment.GetSlot(EquipmentSlot.SecondPrimaryWeapon).ContainedItem;

            if (item == null && equipment.GetSlot(EquipmentSlot.Holster).ContainedItem != null)
                item = equipment.GetSlot(EquipmentSlot.Holster).ContainedItem;

            if (item == null && equipment.GetSlot(EquipmentSlot.Scabbard).ContainedItem != null)
                item = equipment.GetSlot(EquipmentSlot.Scabbard).ContainedItem;

            if (item == null)
            {
                Logger.LogError($"SetWeaponInHandsOfNewPlayer:Unable to find any weapon for {person.Profile.ProfileId}");
            }

            person.SetItemInHands(item, (IResult) =>
            {

                if (IResult.Failed == true)
                {
                    Logger.LogError($"SetWeaponInHandsOfNewPlayer:Unable to set item {item} in hands for {person.Profile.ProfileId}");
                }

                if (IResult.Succeed == true)
                {
                    if (successCallback != null)
                        successCallback();
                }

                if (person.TryGetItemInHands<Item>() != null)
                {
                    if (successCallback != null)
                        successCallback();
                }

            });
        }

        public ulong LocalIndex { get; set; }

        public float LocalTime => 0;

        public BaseLocalGame<GamePlayerOwner> LocalGameInstance { get; internal set; }

        int GuiX = 10;
        int GuiWidth = 400;

        //public const int PING_LIMIT_HIGH = 125;
        //public const int PING_LIMIT_MID = 100;

        public int ServerPing { get; set; } = 1;
        public ConcurrentQueue<int> ServerPingSmooth { get; } = new();

        //public bool HighPingMode { get; set; } = false;
        public bool ServerHasStopped { get; set; }
        private bool ServerHasStoppedActioned { get; set; }

        GUIStyle middleLabelStyle;
        GUIStyle middleLargeLabelStyle;
        GUIStyle normalLabelStyle;

        void OnGUI()
        {


            if (normalLabelStyle == null)
            {
                normalLabelStyle = new GUIStyle(GUI.skin.label);
                normalLabelStyle.fontSize = 16;
                normalLabelStyle.fontStyle = FontStyle.Bold;
            }
            if (middleLabelStyle == null)
            {
                middleLabelStyle = new GUIStyle(GUI.skin.label);
                middleLabelStyle.fontSize = 18;
                middleLabelStyle.fontStyle = FontStyle.Bold;
                middleLabelStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (middleLargeLabelStyle == null)
            {
                middleLargeLabelStyle = new GUIStyle(middleLabelStyle);
                middleLargeLabelStyle.fontSize = 24;
            }

            var rect = new Rect(GuiX, 5, GuiWidth, 100);

            rect.y = 5;
            GUI.Label(rect, $"SIT Coop: " + (MatchmakerAcceptPatches.IsClient ? "CLIENT" : "SERVER"));
            rect.y += 15;

            // PING ------
            if (MatchmakerAcceptPatches.IsClient && MyPlayer.Client != null)
            {
                GUI.contentColor = Color.white;
                GUI.contentColor = MyPlayer.Client.Ping >= AkiBackendCommunication.PING_LIMIT_HIGH ? Color.red : ServerPing >= AkiBackendCommunication.PING_LIMIT_MID ? Color.yellow : Color.green;
                GUI.Label(rect, $"Ping: {MyPlayer.Client.Ping}");
                rect.y += 15;
                GUI.contentColor = Color.white;
            }


            GUIStyle style = GUI.skin.label;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 13;

            var w = 0.5f; // proportional width (0..1)
            var h = 0.2f; // proportional height (0..1)
            var rectEndOfGameMessage = Rect.zero;
            rectEndOfGameMessage.x = (float)(Screen.width * (1 - w)) / 2;
            rectEndOfGameMessage.y = (float)(Screen.height * (1 - h)) / 2 + (Screen.height / 3);
            rectEndOfGameMessage.width = Screen.width * w;
            rectEndOfGameMessage.height = Screen.height * h;

            var numberOfPlayersDead = PlayerUsers.Count(x => !x.HealthController.IsAlive);


            if (LocalGameInstance == null)
                return;

            var coopGame = LocalGameInstance as CoopGame;
            if (coopGame == null)
                return;

            rect = DrawSITStats(rect, numberOfPlayersDead, coopGame);

            var quitState = GetQuitState();
            switch (quitState)
            {
                case EQuitState.YourTeamIsDead:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_TEAM_DEAD"], middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDead:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_SOLO"], middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDeadAsHost:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_HOST"], middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDeadAsClient:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_CLIENT"], middleLargeLabelStyle);
                    break;
                case EQuitState.YourTeamHasExtracted:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_TEAM_EXTRACTED"], middleLargeLabelStyle);
                    break;
                case EQuitState.YouHaveExtractedOnlyAsHost:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_EXTRACTED_HOST"], middleLargeLabelStyle);
                    break;
                case EQuitState.YouHaveExtractedOnlyAsClient:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_EXTRACTED_CLIENT"], middleLargeLabelStyle);
                    break;
            }

            //if(quitState != EQuitState.NONE)
            //{
            //    var rectEndOfGameButton = new Rect(rectEndOfGameMessage);
            //    rectEndOfGameButton.y += 15;
            //    if(GUI.Button(rectEndOfGameButton, "End Raid"))
            //    {

            //    }
            //}


            //OnGUI_DrawPlayerList(rect);
            OnGUI_DrawPlayerFriendlyTags(rect);
            //OnGUI_DrawPlayerEnemyTags(rect);

        }

        private Rect DrawSITStats(Rect rect, int numberOfPlayersDead, CoopGame coopGame)
        {
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_ShowSITStatistics)
                return rect;

            var numberOfPlayersAlive = PlayerUsers.Count(x => x.HealthController.IsAlive);
            // gathering extracted
            var numberOfPlayersExtracted = coopGame.ExtractedPlayers.Count;
            GUI.Label(rect, $"Players (Alive): {numberOfPlayersAlive}");
            rect.y += 15;
            GUI.Label(rect, $"Players (Dead): {numberOfPlayersDead}");
            rect.y += 15;
            GUI.Label(rect, $"Players (Extracted): {numberOfPlayersExtracted}");
            rect.y += 15;
            GUI.Label(rect, $"Bots: {PlayerBots.Length}");
            rect.y += 15;
            return rect;
        }

        private void OnGUI_DrawPlayerFriendlyTags(Rect rect)
        {
            if (SITConfig == null)
            {
                Logger.LogError("SITConfig is null?");
                return;
            }

            if (!SITConfig.showPlayerNameTags)
            {
                return;
            }

            if (FPSCamera.Instance == null)
                return;

            if (Players == null)
                return;

            if (PlayerUsers == null)
                return;

            if (Camera.current == null)
                return;

            if (!Singleton<GameWorld>.Instantiated)
                return;


            if (FPSCamera.Instance.SSAA != null && FPSCamera.Instance.SSAA.isActiveAndEnabled)
                screenScale = (float)FPSCamera.Instance.SSAA.GetOutputWidth() / (float)FPSCamera.Instance.SSAA.GetInputWidth();

            var ownPlayer = Singleton<GameWorld>.Instance.MainPlayer;
            if (ownPlayer == null)
                return;

            foreach (var pl in PlayerUsers)
            {
                if (pl == null)
                    continue;

                if (pl.HealthController == null)
                    continue;

                if (pl.IsYourPlayer && pl.HealthController.IsAlive)
                    continue;

                Vector3 aboveBotHeadPos = pl.PlayerBones.Pelvis.position + (Vector3.up * (pl.HealthController.IsAlive ? 1.1f : 0.3f));
                Vector3 screenPos = Camera.current.WorldToScreenPoint(aboveBotHeadPos);
                if (screenPos.z > 0)
                {
                    rect.x = (screenPos.x * screenScale) - (rect.width / 2);
                    rect.y = Screen.height - ((screenPos.y + rect.height / 2) * screenScale);

                    GUIStyle labelStyle = middleLabelStyle;
                    labelStyle.fontSize = 14;
                    float labelOpacity = 1;
                    float distanceToCenter = Vector3.Distance(screenPos, new Vector3(Screen.width, Screen.height, 0) / 2);

                    if (distanceToCenter < 100)
                    {
                        labelOpacity = distanceToCenter / 100;
                    }

                    if (ownPlayer.HandsController != null)
                    {
                        if (ownPlayer.HandsController.IsAiming)
                            labelOpacity *= 0.5f;
                    }

                    if (pl.HealthController.IsAlive)
                    {
                        var maxHealth = pl.HealthController.GetBodyPartHealth(EBodyPart.Common).Maximum;
                        var currentHealth = pl.HealthController.GetBodyPartHealth(EBodyPart.Common).Current / maxHealth;
                        labelStyle.normal.textColor = new Color(2.0f * (1 - currentHealth), 2.0f * currentHealth, 0, labelOpacity);
                    }
                    else
                    {
                        labelStyle.normal.textColor = new Color(255, 0, 0, labelOpacity);
                    }

                    var distanceFromCamera = Math.Round(Vector3.Distance(Camera.current.gameObject.transform.position, pl.Position));
                    GUI.Label(rect, $"{pl.Profile.Nickname} {distanceFromCamera}m", labelStyle);
                }
            }
        }

        private void OnGUI_DrawPlayerEnemyTags(UnityEngine.Rect rect)
        {
            if (SITConfig == null)
            {
                Logger.LogError("SITConfig is null?");
                return;
            }

            if (!SITConfig.showPlayerNameTagsForEnemies)
            {
                return;
            }

            if (FPSCamera.Instance == null)
                return;

            if (Players == null)
                return;

            if (PlayerUsers == null)
                return;

            if (Camera.current == null)
                return;

            if (!Singleton<GameWorld>.Instantiated)
                return;


            if (FPSCamera.Instance.SSAA != null && FPSCamera.Instance.SSAA.isActiveAndEnabled)
                screenScale = (float)FPSCamera.Instance.SSAA.GetOutputWidth() / (float)FPSCamera.Instance.SSAA.GetInputWidth();

            var ownPlayer = Singleton<GameWorld>.Instance.MainPlayer;
            if (ownPlayer == null)
                return;

            foreach (var pl in PlayerBots)
            {
                if (pl == null)
                    continue;

                if (pl.HealthController == null)
                    continue;

                if (!pl.HealthController.IsAlive)
                    continue;

                Vector3 aboveBotHeadPos = pl.Position + (Vector3.up * (pl.HealthController.IsAlive ? 1.5f : 0.5f));
                Vector3 screenPos = Camera.current.WorldToScreenPoint(aboveBotHeadPos);
                if (screenPos.z > 0)
                {
                    rect.x = (screenPos.x * screenScale) - (rect.width / 2);
                    rect.y = Screen.height - (screenPos.y * screenScale) - 15;

                    var distanceFromCamera = Math.Round(Vector3.Distance(Camera.current.gameObject.transform.position, pl.Position));
                    GUI.Label(rect, $"{pl.Profile.Nickname} {distanceFromCamera}m", middleLabelStyle);
                    rect.y += 15;
                    GUI.Label(rect, $"X", middleLabelStyle);
                }
            }
        }

        private void OnGUI_DrawPlayerList(UnityEngine.Rect rect)
        {
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_DEBUGShowPlayerList)
                return;

            rect.y += 15;

            if (PlayersToSpawn.Any(p => p.Value != ESpawnState.Spawned))
            {
                GUI.Label(rect, $"Spawning Players:");
                rect.y += 15;
                foreach (var p in PlayersToSpawn.Where(p => p.Value != ESpawnState.Spawned))
                {
                    GUI.Label(rect, $"{p.Key}:{p.Value}");
                    rect.y += 15;
                }
            }

            if (Singleton<GameWorld>.Instance != null)
            {
                var players = Singleton<GameWorld>.Instance.RegisteredPlayers.ToList();
                players.AddRange(Players.Values);
                players = players.Distinct(x => x.ProfileId).ToList();

                rect.y += 15;
                GUI.Label(rect, $"Players [{players.Count}]:");
                rect.y += 15;
                foreach (var p in players)
                {
                    GUI.Label(rect, $"{p.Profile.Nickname}:{(p.IsAI ? "AI" : "Player")}:{(p.HealthController.IsAlive ? "Alive" : "Dead")}");
                    rect.y += 15;
                }

                players.Clear();
                players = null;
            }
        }
    }

    public enum ESpawnState
    {
        None = 0,
        Loading = 1,
        Spawning = 2,
        Spawned = 3,
        Ignore = 98,
        Error = 99,
    }

    public class SITConfig
    {
        public bool showPlayerNameTags { get; set; }

        /// <summary>
        /// Doesn't do anything
        /// </summary>

        public bool showPlayerNameTagsOnlyWhenVisible { get; set; }

        public bool showPlayerNameTagsForEnemies { get; set; } = false;

        public bool useClientSideDamageModel { get; set; } = false;
    }


}
