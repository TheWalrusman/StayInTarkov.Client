﻿using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using LiteNetLib.Utils;
using StayInTarkov.Coop;
using StayInTarkov.Coop.NetworkPacket;
using EFT;
using Comfort.Common;

namespace StayInTarkov.Networking
{
    public class SITClient : MonoBehaviour, INetEventListener
    {
        private LiteNetLib.NetManager _netClient;
        public CoopPlayer Player { get; set; }

        public NetDataWriter _dataWriter = new();

        public void Start()
        {
            _netClient = new LiteNetLib.NetManager(this)
            {
                UnconnectedMessagesEnabled = true,
                UpdateTime = 15,
                NatPunchEnabled = true
            };
            _netClient.Start();
            _netClient.Connect("78.72.78.93", 5000, "sit.core");
        }

        void Update()
        {
            _netClient.PollEvents();

            var peer = _netClient.FirstPeer;
            if (peer != null && peer.ConnectionState == ConnectionState.Connected)
            {
                //Basic lerp
                //_lerpTime += Time.deltaTime / Time.fixedDeltaTime;
            }
            else
            {
                _netClient.SendBroadcast(new byte[] { 1 }, 5000);
            }
        }

        void OnDestroy()
        {
            if (_netClient != null)
                _netClient.Stop();
        }

        public void SendData(NetDataWriter dataWriter, DeliveryMethod deliveryMethod)
        {
            _netClient.FirstPeer.Send(dataWriter, deliveryMethod);
        }

        public void OnPeerConnected(NetPeer peer)
        {
            EFT.UI.ConsoleScreen.Log("[CLIENT] We connected to " + peer.EndPoint);
            NotificationManagerClass.DisplayMessageNotification($"Connected to server {peer.EndPoint}.",
                EFT.Communications.ENotificationDurationType.Default, EFT.Communications.ENotificationIconType.Friend);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
        {
            EFT.UI.ConsoleScreen.Log("[CLIENT] We received error " + socketErrorCode);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            PlayerStatePacket pSP = new();
            pSP.Deserialize(reader);

            var playerToApply = Singleton<GameWorld>.Instance.allAlivePlayersByID.Where(x => x.Key == pSP.ProfileId).FirstOrDefault().Value as CoopPlayer;
            if (playerToApply != default && playerToApply != null && !playerToApply.IsYourPlayer)
            {
                playerToApply.NewState = pSP;
            }
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            if (messageType == UnconnectedMessageType.BasicMessage && _netClient.ConnectedPeersCount == 0 && reader.GetInt() == 1)
            {
                EFT.UI.ConsoleScreen.Log("[CLIENT] Received discovery response. Connecting to: " + remoteEndPoint);
                _netClient.Connect(remoteEndPoint, "sit.core");
            }
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {

        }

        public void OnConnectionRequest(LiteNetLib.ConnectionRequest request)
        {

        }

        public void OnPeerDisconnected(NetPeer peer, LiteNetLib.DisconnectInfo disconnectInfo)
        {
            EFT.UI.ConsoleScreen.Log("[CLIENT] We disconnected because " + disconnectInfo.Reason);
        }
    }
}