using EFT.InventoryLogic;
using EFT;
using StayInTarkov.Coop.Players;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace StayInTarkov.Coop.Player.FirearmControllerPatches
{
    internal class FirearmController_InitiateShot_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(EFT.Player.FirearmController);
        public override string MethodName => "InitiateShot";

        [PatchPostfix]
        public static void Postfix(EFT.Player.FirearmController __instance, EFT.Player ____player, IWeapon weapon, BulletClass ammo, Vector3 shotPosition, Vector3 shotDirection, Vector3 fireportPosition, int chamberIndex, float overheat)
        {
            EShotType shotType = new();

            var botPlayer = ____player as CoopBot;
            if (botPlayer != null)
            {
                switch (weapon.MalfState.State)
                {
                    case Weapon.EMalfunctionState.None:
                        shotType = EShotType.RegularShot;
                        break;
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
                    IsPrimaryActive = (weapon == __instance.Item),
                    ShotType = shotType,
                    AmmoAfterShot = weapon.GetCurrentMagazineCount(),
                    ShotPosition = shotPosition,
                    ShotDirection = shotDirection,
                    FireportPosition = fireportPosition,
                    ChamberIndex = chamberIndex,
                    Overheat = overheat,
                    UnderbarrelShot = weapon.IsUnderbarrelWeapon,
                    AmmoTemplate = ammo.AmmoTemplate._id
                };
                botPlayer.WeaponPacket.ToggleSend();
                return;
            }

            var player = ____player as CoopPlayer;
            if (player == null || !player.IsYourPlayer)
                return;

            switch (weapon.MalfState.State)
            {
                case Weapon.EMalfunctionState.None:
                    shotType = EShotType.RegularShot;
                    break;
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
                IsPrimaryActive = (weapon == __instance.Item),
                ShotType = shotType,
                AmmoAfterShot = weapon.GetCurrentMagazineCount(),
                ShotPosition = shotPosition,
                ShotDirection = shotDirection,
                FireportPosition = fireportPosition,
                ChamberIndex = chamberIndex,
                Overheat = overheat,
                UnderbarrelShot = weapon.IsUnderbarrelWeapon,
                AmmoTemplate = ammo.AmmoTemplate._id
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
