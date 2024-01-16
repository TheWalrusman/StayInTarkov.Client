using EFT.InventoryLogic;
using EFT;
using StayInTarkov.Coop.Players;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StayInTarkov.Coop.Player.FirearmControllerPatches
{
    internal class FirearmController_ShotMisfired_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(EFT.Player.FirearmController);
        public override string MethodName => "ShotMisfired";

        [PatchPostfix]
        public static void Postfix(EFT.Player.FirearmController __instance, EFT.Player ____player, BulletClass ammo, Weapon.EMalfunctionState malfunctionState, float overheat)
        {
            EShotType shotType = new();

            var botPlayer = ____player as CoopBot;
            if (botPlayer != null)
            {
                switch (malfunctionState)
                {
                    case Weapon.EMalfunctionState.Misfire:
                        shotType = EShotType.Misfire;
                        break;
                    case Weapon.EMalfunctionState.Jam:
                        shotType = EShotType.JamedShot;
                        break;
                    case Weapon.EMalfunctionState.HardSlide:
                        shotType = EShotType.HardSlidedShot;
                        break;
                    case Weapon.EMalfunctionState.SoftSlide:
                        shotType = EShotType.SoftSlidedShot;
                        break;
                    case Weapon.EMalfunctionState.Feed:
                        shotType = EShotType.Feed;
                        break;
                }

                botPlayer.WeaponPacket.HasShotInfo = true;
                botPlayer.WeaponPacket.ShotInfoPacket = new()
                {
                    IsPrimaryActive = true,
                    ShotType = shotType,
                    AmmoAfterShot = __instance.Item.GetCurrentMagazineCount(),
                    Overheat = overheat
                };
                botPlayer.WeaponPacket.ToggleSend();
                return;
            }

            var player = ____player as CoopPlayer;
            if (player == null || !player.IsYourPlayer)
                return;

            switch (malfunctionState)
            {
                case Weapon.EMalfunctionState.Misfire:
                    shotType = EShotType.Misfire;
                    break;
                case Weapon.EMalfunctionState.Jam:
                    shotType = EShotType.JamedShot;
                    break;
                case Weapon.EMalfunctionState.HardSlide:
                    shotType = EShotType.HardSlidedShot;
                    break;
                case Weapon.EMalfunctionState.SoftSlide:
                    shotType = EShotType.SoftSlidedShot;
                    break;
                case Weapon.EMalfunctionState.Feed:
                    shotType = EShotType.Feed;
                    break;
            }

            player.WeaponPacket.HasShotInfo = true;
            player.WeaponPacket.ShotInfoPacket = new()
            {
                IsPrimaryActive = true,
                ShotType = shotType,
                AmmoAfterShot = __instance.Item.GetCurrentMagazineCount(),
                Overheat = overheat
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
