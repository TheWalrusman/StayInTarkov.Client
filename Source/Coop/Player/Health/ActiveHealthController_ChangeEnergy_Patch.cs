﻿using EFT.HealthSystem;
using StayInTarkov.Coop.Matchmaker;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace StayInTarkov.Coop.Player.Health
{
    internal class ActiveHealthController_ChangeEnergy_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(ActiveHealthController);

        public override string MethodName => "ChangeEnergy";

        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
        }

        [PatchPostfix]
        public static void PatchPostfix(ActiveHealthController __instance, float value)
        {
            var player = __instance.Player as CoopPlayer;
            if (player == null || !player.IsYourPlayer && (!MatchmakerAcceptPatches.IsServer && !player.IsAI))
                return;

            player.HealthPacket.HasEnergyChange = true;
            player.HealthPacket.EnergyChangeValue = value;
            player.HealthPacket.ToggleSend();
        }


        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
        {

        }
    }
}