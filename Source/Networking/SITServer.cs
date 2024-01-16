﻿using Comfort.Common;
using EFT;
using EFT.Weather;
using LiteNetLib;
using LiteNetLib.Utils;
using StayInTarkov.Configuration;
using StayInTarkov.Coop;
using StayInTarkov.Coop.Players;
using StayInTarkov.Networking.Packets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Open.Nat;
using static StayInTarkov.Networking.SITSerialization;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using EFT.UI;
using UnityEngine.UIElements;

/* 
* This code has been written by Lacyway (https://github.com/Lacyway) for the SIT Project (https://github.com/stayintarkov/StayInTarkov.Client). 
* You are free to re-use this in your own project, but out of respect please leave credit where it's due according to the MIT License.
*/

namespace StayInTarkov.Networking
{
    public class SITServer : MonoBehaviour, INetEventListener, INetLogger
    {
        private LiteNetLib.NetManager _netServer;
        public NetPacketProcessor _packetProcessor = new();
        private NetDataWriter _dataWriter = new();
        public CoopPlayer MyPlayer => Singleton<GameWorld>.Instance.MainPlayer as CoopPlayer;
        public ConcurrentDictionary<string, CoopPlayer> Players => CoopGameComponent.Players;
        public List<string> PlayersMissing = [];
        public string MyExternalIP { get; private set; } = NetUtils.GetLocalIp(LocalAddrType.IPv4);
        private int Port => PluginConfigSettings.Instance.CoopSettings.SITGamePlayPort;
        private CoopGameComponent CoopGameComponent { get; set; }
        public LiteNetLib.NetManager NetServer
        {
            get
            {
                return _netServer;
            }
        }

        public async void Start()
        {
            NetDebug.Logger = this;

            _packetProcessor.RegisterNestedType(Vector3Utils.Serialize, Vector3Utils.Deserialize);
            _packetProcessor.RegisterNestedType(Vector2Utils.Serialize, Vector2Utils.Deserialize);
            _packetProcessor.RegisterNestedType(PhysicalUtils.Serialize, PhysicalUtils.Deserialize);

            _packetProcessor.SubscribeNetSerializable<PlayerStatePacket, NetPeer>(OnPlayerStatePacketReceived);
            _packetProcessor.SubscribeNetSerializable<GameTimerPacket, NetPeer>(OnGameTimerPacketReceived);
            _packetProcessor.SubscribeNetSerializable<WeatherPacket, NetPeer>(OnWeatherPacketReceived);
            _packetProcessor.SubscribeNetSerializable<WeaponPacket, NetPeer>(OnWeaponPacketReceived);
            _packetProcessor.SubscribeNetSerializable<HealthPacket, NetPeer>(OnHealthPacketReceived);
            _packetProcessor.SubscribeNetSerializable<InventoryPacket, NetPeer>(OnInventoryPacketReceived);
            _packetProcessor.SubscribeNetSerializable<CommonPlayerPacket, NetPeer>(OnCommonPlayerPacketReceived);
            _packetProcessor.SubscribeNetSerializable<AllCharacterRequestPacket, NetPeer>(OnAllCharacterRequestPacketReceived);
            _packetProcessor.SubscribeNetSerializable<InformationPacket, NetPeer>(OnInformationPacketReceived);

            _netServer = new LiteNetLib.NetManager(this)
            {
                BroadcastReceiveEnabled = true,
                UpdateTime = 15,
                AutoRecycle = true,
                IPv6Enabled = false
            };

            if (PluginConfigSettings.Instance.CoopSettings.UseUPnP)
            {
                bool upnpFailed = false;

                await Task.Run(async () =>
                {
                    try
                    {
                        var discoverer = new NatDiscoverer();
                        var cts = new CancellationTokenSource(10000);
                        var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
                        var extIp = await device.GetExternalIPAsync();
                        MyExternalIP = extIp.MapToIPv4().ToString();

                        await device.CreatePortMapAsync(new Mapping(Protocol.Udp, Port, Port, 300, "SIT UDP"));
                    }
                    catch (System.Exception ex)
                    {
                        ConsoleScreen.LogError($"Error when attempting to map UPnP. Make sure the selected port is not already open! Error message: {ex.Message}");
                        upnpFailed = true;
                    }
                });

                if (upnpFailed)
                    Singleton<PreloaderUI>.Instance.ShowErrorScreen("Network Error", "UPnP mapping failed. Make sure the selected port is not already open!");
            }
            else
            {
                try
                {
                    var discoverer = new NatDiscoverer();
                    var cts = new CancellationTokenSource(10000);
                    var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
                    var extIp = await device.GetExternalIPAsync();
                    MyExternalIP = extIp.MapToIPv4().ToString();
                }
                catch (System.Exception ex)
                {
                    Singleton<PreloaderUI>.Instance.ShowErrorScreen("Network Error", "Error when trying to receive IP automatically. Make sure you are behind a NAT!");
                }
            }

            _netServer.Start(Port);

            ConsoleScreen.Log("Started SITServer");
            NotificationManagerClass.DisplayMessageNotification($"Server started on address {MyExternalIP}, port {_netServer.LocalPort}.",
                EFT.Communications.ENotificationDurationType.Default, EFT.Communications.ENotificationIconType.EntryPoint);

            Dictionary<string, object> packet = new()
            {
                {
                    "m",
                    "SetIpAndPort"
                },
                {
                    "serverId",
                    CoopGameComponent.GetServerId()
                },
                {
                    "ip",
                    MyExternalIP
                },
                {
                    "port",
                    Port.ToString()
                }
            };
            AkiBackendCommunication.Instance.PostJson("/coop/server/update", packet.ToJson());
        }

        private void OnInformationPacketReceived(InformationPacket packet, NetPeer peer)
        {
            InformationPacket respondPackage = new(false)
            {
                NumberOfPlayers = _netServer.ConnectedPeersCount
            };

            _dataWriter.Reset();
            SendDataToPeer(peer, _dataWriter, ref respondPackage, DeliveryMethod.ReliableUnordered);
        }
        private void OnAllCharacterRequestPacketReceived(AllCharacterRequestPacket packet, NetPeer peer)
        {
            // This method needs to be refined. For some reason the ping-pong has to be run twice for it to work on the host?
            if (packet.IsRequest)
            {
                foreach (var player in CoopGameComponent.Players.Values)
                {
                    if (player.ProfileId == packet.ProfileId)
                        continue;

                    if (packet.Characters.Contains(player.ProfileId))
                        continue;

                    AllCharacterRequestPacket requestPacket = new(player.ProfileId)
                    {
                        IsRequest = false,
                        PlayerInfo = new()
                        {
                            Profile = player.Profile
                        },
                        IsAlive = player.ActiveHealthController.IsAlive,
                        Position = player.Transform.position
                    };
                    _dataWriter.Reset();
                    SendDataToPeer(peer, _dataWriter, ref requestPacket, DeliveryMethod.ReliableUnordered);
                }
            }
            if (!Players.ContainsKey(packet.ProfileId) && !PlayersMissing.Contains(packet.ProfileId))
            {
                PlayersMissing.Add(packet.ProfileId);
                ConsoleScreen.Log($"Requesting missing player from server.");
                AllCharacterRequestPacket requestPacket = new(MyPlayer.ProfileId);
                _dataWriter.Reset();
                SendDataToPeer(peer, _dataWriter, ref requestPacket, DeliveryMethod.ReliableUnordered);
            }
            if (!packet.IsRequest && PlayersMissing.Contains(packet.ProfileId))
            {
                ConsoleScreen.Log($"Received CharacterRequest from client: ProfileID: {packet.PlayerInfo.Profile.ProfileId}, Nickname: {packet.PlayerInfo.Profile.Nickname}");
                if (packet.ProfileId != MyPlayer.ProfileId)
                {
                    if (!CoopGameComponent.PlayersToSpawn.ContainsKey(packet.PlayerInfo.Profile.ProfileId))
                        CoopGameComponent.PlayersToSpawn.TryAdd(packet.PlayerInfo.Profile.ProfileId, ESpawnState.None);
                    if (!CoopGameComponent.PlayersToSpawnProfiles.ContainsKey(packet.PlayerInfo.Profile.ProfileId))
                        CoopGameComponent.PlayersToSpawnProfiles.Add(packet.PlayerInfo.Profile.ProfileId, packet.PlayerInfo.Profile);

                    CoopGameComponent.QueueProfile(packet.PlayerInfo.Profile, new Vector3(packet.Position.x, packet.Position.y + 0.5f, packet.Position.y), packet.IsAlive);
                    PlayersMissing.Remove(packet.ProfileId);
                }
            }
        }
        private void OnCommonPlayerPacketReceived(CommonPlayerPacket packet, NetPeer peer)
        {
            if (!Players.ContainsKey(packet.ProfileId))
                return;

            _dataWriter.Reset();
            SendDataToAll(_dataWriter, ref packet, DeliveryMethod.ReliableOrdered, peer);

            var playerToApply = Players[packet.ProfileId];
            if (playerToApply != default && playerToApply != null)
            {
                playerToApply.CommonPlayerPackets.Enqueue(packet);
            }
        }
        private void OnInventoryPacketReceived(InventoryPacket packet, NetPeer peer)
        {
            if (!Players.ContainsKey(packet.ProfileId))
                return;

            _dataWriter.Reset();
            SendDataToAll(_dataWriter, ref packet, DeliveryMethod.ReliableOrdered, peer);

            var playerToApply = Players[packet.ProfileId];
            if (playerToApply != default && playerToApply != null)
            {
                playerToApply.InventoryPackets.Enqueue(packet);
            }
        }
        private void OnHealthPacketReceived(HealthPacket packet, NetPeer peer)
        {
            if (!Players.ContainsKey(packet.ProfileId))
                return;

            _dataWriter.Reset();
            SendDataToAll(_dataWriter, ref packet, DeliveryMethod.ReliableOrdered, peer);

            var playerToApply = Players[packet.ProfileId];
            if (playerToApply != default && playerToApply != null)
            {
                playerToApply.HealthPackets.Enqueue(packet);
            }
        }
        private void OnWeaponPacketReceived(WeaponPacket packet, NetPeer peer)
        {
            if (!Players.ContainsKey(packet.ProfileId))
                return;

            _dataWriter.Reset();
            SendDataToAll(_dataWriter, ref packet, DeliveryMethod.ReliableOrdered, peer);

            var playerToApply = Players[packet.ProfileId];
            if (playerToApply != default && playerToApply != null && !playerToApply.IsYourPlayer)
            {
                playerToApply.FirearmPackets.Enqueue(packet);
            }
        }
        private void OnWeatherPacketReceived(WeatherPacket packet, NetPeer peer)
        {
            if (!packet.IsRequest)
                return;

            var weatherController = WeatherController.Instance;
            if (weatherController != null)
            {
                WeatherPacket weatherPacket = new();
                if (weatherController.CloudsController != null)
                    weatherPacket.CloudDensity = weatherController.CloudsController.Density;

                var weatherCurve = weatherController.WeatherCurve;
                if (weatherCurve != null)
                {
                    weatherPacket.Fog = weatherCurve.Fog;
                    weatherPacket.LightningThunderProbability = weatherCurve.LightningThunderProbability;
                    weatherPacket.Rain = weatherCurve.Rain;
                    weatherPacket.Temperature = weatherCurve.Temperature;
                    weatherPacket.WindX = weatherCurve.Wind.x;
                    weatherPacket.WindY = weatherCurve.Wind.y;
                    weatherPacket.TopWindX = weatherCurve.TopWind.x;
                    weatherPacket.TopWindY = weatherCurve.TopWind.y;
                }

                _dataWriter.Reset();
                SendDataToPeer(peer, _dataWriter, ref weatherPacket, DeliveryMethod.ReliableOrdered);
            }
        }
        private void OnGameTimerPacketReceived(GameTimerPacket packet, NetPeer peer)
        {
            if (!packet.IsRequest)
                return;

            var game = (CoopGame)Singleton<AbstractGame>.Instance;
            if (game != null)
            {
                GameTimerPacket gameTimerPacket = new(false, (game.GameTimer.SessionTime - game.GameTimer.PastTime).Value.Ticks);
                _dataWriter.Reset();
                SendDataToPeer(peer, _dataWriter, ref gameTimerPacket, DeliveryMethod.ReliableOrdered);
            }
            else
            {
                ConsoleScreen.Log("OnGameTimerPacketReceived: Game was null!");
            }
        }
        private void OnPlayerStatePacketReceived(PlayerStatePacket packet, NetPeer peer)
        {
            if (!Players.ContainsKey(packet.ProfileId))
                return;

            _dataWriter.Reset();
            SendDataToAll(_dataWriter, ref packet, DeliveryMethod.ReliableOrdered, peer);

            var playerToApply = Players[packet.ProfileId];
            if (playerToApply != default && playerToApply != null && !playerToApply.IsYourPlayer)
            {
                playerToApply.NewState = packet;
            }
        }

        public void Awake()
        {
            CoopGameComponent = CoopPatches.CoopGameComponentParent.GetComponent<CoopGameComponent>();
            Singleton<SITServer>.Create(this);
        }

        void Update()
        {
            _netServer.PollEvents();
        }

        void OnDestroy()
        {
            NetDebug.Logger = null;
            if (_netServer != null)
                _netServer.Stop();
        }

        public void SendDataToAll<T>(NetDataWriter writer, ref T packet, DeliveryMethod deliveryMethod, NetPeer peer = null) where T : INetSerializable
        {
            _packetProcessor.WriteNetSerializable(writer, ref packet);
            if (peer != null)
                _netServer.SendToAll(writer, deliveryMethod, peer);
            else
                _netServer.SendToAll(writer, deliveryMethod);
        }

        public void SendDataToPeer<T>(NetPeer peer, NetDataWriter writer, ref T packet, DeliveryMethod deliveryMethod) where T : INetSerializable
        {
            _packetProcessor.WriteNetSerializable(writer, ref packet);
            peer.Send(writer, deliveryMethod);
        }

        public void OnPeerConnected(NetPeer peer)
        {
            NotificationManagerClass.DisplayMessageNotification($"Peer connected to server on port {peer.EndPoint.Port}.",
                EFT.Communications.ENotificationDurationType.Default, EFT.Communications.ENotificationIconType.Friend);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            ConsoleScreen.Log("[SERVER] error " + socketErrorCode);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            if (messageType == UnconnectedMessageType.Broadcast)
            {
                ConsoleScreen.Log("[SERVER] Received discovery request. Send discovery response");
                NetDataWriter resp = new NetDataWriter();
                resp.Put(1);
                _netServer.SendUnconnectedMessage(resp, remoteEndPoint);
            }
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(LiteNetLib.ConnectionRequest request)
        {
            request.AcceptIfKey("sit.core");
        }

        public void OnPeerDisconnected(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            ConsoleScreen.Log("[SERVER] peer disconnected " + peer.EndPoint + ", info: " + disconnectInfo.Reason);
        }

        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            Debug.LogFormat(str, args);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            _packetProcessor.ReadAllPackets(reader, peer);
        }
    }
}
