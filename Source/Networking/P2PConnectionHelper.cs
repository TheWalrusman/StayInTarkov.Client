using BepInEx.Logging;
using EFT;
using LiteNetLib.Utils;
using StayInTarkov.Configuration;
using StayInTarkov.Coop;
using StayInTarkov.Coop.Matchmaker;
using STUN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace StayInTarkov.Networking
{
    public enum NatTraversalMethod
    {
        NatPunch,
        Upnp,
        PortForward
    }

    public enum NatTraversalStatus
    {
        Idle,
        InProgress,
        Completed,
        Error
    }
    
    public class P2PConnectionHelper
    {
        public LiteNetLib.NetManager NetManager;
        public WebSocket WebSocket { get; set; }

        private TaskCompletionSource<IPEndPoint> PunchCompletionResult = new TaskCompletionSource<IPEndPoint>();

        private static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("P2P Connection Helper");

        public P2PConnectionHelper(LiteNetLib.NetManager netManager) 
        {
            NetManager = netManager;
        }

        public void Connect()
        {
            var wsUrl = $"{StayInTarkovHelperConstants.GetREALWSURL()}:6972/{MatchmakerAcceptPatches.Profile.ProfileId}?";

            WebSocket = new WebSocket(wsUrl);
            WebSocket.WaitTime = TimeSpan.FromMinutes(1);
            WebSocket.EmitOnPing = true;
            WebSocket.Connect();

            WebSocket.OnError += WebSocket_OnError;
            WebSocket.OnMessage += WebSocket_OnMessage;
        }

        private void WebSocket_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Logger.LogInfo("WebSocket Error:" + e.Message);
            WebSocket.Close();
        }

        private void WebSocket_OnMessage(object sender, WebSocketSharp.MessageEventArgs e)
        {
            if (e == null)
                return;

            if (string.IsNullOrEmpty(e.Data))
                return;

            ProcessMessage(e.Data);
        }

        public Task<IPEndPoint> PunchNATRequest()
        {
            PunchCompletionResult = new TaskCompletionSource<IPEndPoint>();
            
            // perform STUN query to open a public endpoint and punch it later.
            var stunEndPoint = GetSTUNEndpoint();

            if(stunEndPoint != null)
            {
                // punch:serverId:profileId:publicIp:publicPort
                string punchPacket = $"punch:{MatchmakerAcceptPatches.GetGroupId()}:{MatchmakerAcceptPatches.Profile.ProfileId}:{stunEndPoint.Address.ToString()}:{stunEndPoint.Port.ToString()}";

                WebSocket.Send(punchPacket);

                return PunchCompletionResult.Task;
            }

            return null;
        }

        public void GenerateAndSendEndpoints()
        {
            // NAT Punching
            var stunEndPoint = GetSTUNEndpoint();

            // UPNP Mapping
            // TODO: add a upnp mapping library and use it to map a port using upnp
            var upnpEndpoint = new IPEndPoint(IPAddress.Parse("12.12.12.12"), 1234);

            // Port Forwarding
            // TODO: use a different way to obtain external ip in case STUN query fails
            var portForwardingEndPoint = new IPEndPoint(stunEndPoint.Address, PluginConfigSettings.Instance.CoopSettings.SITUDPPort);

            // server_endpoints:serverId:stunIp:stunPort:upnpIp:upnpPort:portforwardingIp:portforwardingPort
            string serverEndpointsPacket = $"server_endpoints:{MatchmakerAcceptPatches.GetGroupId()}:{stunEndPoint.Address.ToString()}:{stunEndPoint.Port.ToString()}:{upnpEndpoint.Address.ToString()}:{upnpEndpoint.Port}:{portForwardingEndPoint.Address.ToString()}:{portForwardingEndPoint.Port}";

            WebSocket.Send(serverEndpointsPacket);
        }

        public IPEndPoint GetSTUNEndpoint()
        {
            // we dont need to use SITUDPPort as the local port, but this works for now.
            if (STUNHelper.Query(PluginConfigSettings.Instance.CoopSettings.SITUDPPort, out STUNQueryResult stunQueryResult))
            {
                return stunQueryResult.PublicEndPoint;
            }

            return null;
        }

        private void ProcessMessage(string message) 
        {
            Logger.LogInfo("received message: " + message);

            var messageSplit = message.Split(':');

            if (messageSplit[0] == "punch")
            {
                var ipToPunch = messageSplit[1];
                var portToPunch = messageSplit[2];

                var endPoint = new IPEndPoint(IPAddress.Parse(ipToPunch), int.Parse(portToPunch));

                PunchNAT(endPoint);
                PunchCompletionResult.TrySetResult(endPoint);
            }
        }

        private void PunchNAT(IPEndPoint endPoint)
        {
            Logger.LogInfo($"Punching: {endPoint.Address.ToString()}:{endPoint.Port}");

            // bogus punch data
            NetDataWriter resp = new NetDataWriter();
            resp.Put(9999);

            // send a couple of packets to punch a hole
            for (int i = 0; i < 10; i++)
            {
                NetManager.SendUnconnectedMessage(resp, endPoint);
            }
        }
    }
}
