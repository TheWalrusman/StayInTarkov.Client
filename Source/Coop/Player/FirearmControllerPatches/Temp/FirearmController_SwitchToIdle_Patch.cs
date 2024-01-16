//using StayInTarkov.Coop.Players;
//using System;
//using System.Collections.Generic;
//using System.Reflection;

//namespace StayInTarkov.Coop.Player.FirearmControllerPatches
//{
//    internal class FirearmController_SwitchToIdle_Patch : ModuleReplicationPatch
//    {
//        public override Type InstanceType => typeof(EFT.Player.FirearmController.GClass1529);
//        public override string MethodName => "SwitchToIdle";

//        [PatchPostfix]
//        public static void Postfix(EFT.Player ____player)
//        {
//            var botPlayer = ____player as CoopBot;
//            if (botPlayer != null)
//            {
//                botPlayer.WeaponPacket.SwitchToIdle = true;
//                botPlayer.WeaponPacket.ToggleSend();
//                return;
//            }

//            var player = ____player as CoopPlayer;
//            if (player == null || !player.IsYourPlayer)
//                return;

//            player.WeaponPacket.SwitchToIdle = true;
//            player.WeaponPacket.ToggleSend();
//        }

//        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
//        {
//            throw new NotImplementedException();
//        }

//        protected override MethodBase GetTargetMethod()
//        {
//            return ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
//        }
//    }
//}
