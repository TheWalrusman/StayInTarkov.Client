using EFT.InventoryLogic;
using EFT;
using StayInTarkov.Coop.Players;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StayInTarkov.Coop.Player.FirearmControllerPatches
{
    internal class FirearmController_DryShot_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(EFT.Player.FirearmController);
        public override string MethodName => "DryShot";

        [PatchPostfix]
        public static void Postfix(EFT.Player.FirearmController __instance, EFT.Player ____player, int chamberIndex = 0, bool underbarrelShot = false)
        {
            var botPlayer = ____player as CoopBot;
            if (botPlayer != null)
            {
                botPlayer.WeaponPacket.HasShotInfo = true;
                botPlayer.WeaponPacket.ShotInfoPacket = new()
                {
                    IsPrimaryActive = true,
                    ShotType = EShotType.DryFire,
                    AmmoAfterShot = underbarrelShot ? 0 : __instance.Item.GetCurrentMagazineCount(),
                    ChamberIndex = chamberIndex,
                    UnderbarrelShot = underbarrelShot
                };
                botPlayer.WeaponPacket.ToggleSend();
                return;
            }

            var player = ____player as CoopPlayer;
            if (player == null || !player.IsYourPlayer)
                return;            

            player.WeaponPacket.HasShotInfo = true;
            player.WeaponPacket.ShotInfoPacket = new()
            {
                IsPrimaryActive = true,
                ShotType = EShotType.DryFire,
                AmmoAfterShot = underbarrelShot ? 0 : __instance.Item.GetCurrentMagazineCount(),
                ChamberIndex = chamberIndex,
                UnderbarrelShot = underbarrelShot
            };
            player.WeaponPacket.ToggleSend();
        }

        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }

        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
        }
    }
}
