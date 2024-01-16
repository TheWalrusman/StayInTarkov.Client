using EFT;
using EFT.HealthSystem;
using StayInTarkov.Coop.Players;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace StayInTarkov.Coop.Player.Health
{
    internal class ActiveHealthController_DestroyBodyPart_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(PlayerHealthController);

        public override string MethodName => "DestroyBodyPart";

        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
        }

        [PatchPostfix]
        public static void PatchPostfix(PlayerHealthController __instance, EBodyPart bodyPart, EDamageType damageType)
        {
            var botPlayer = __instance.Player as CoopBot;
            if (botPlayer != null)
            {
                botPlayer.HealthPacket.HasBodyPartDestroyInfo = true;
                botPlayer.HealthPacket.DestroyBodyPartPacket = new()
                {
                    BodyPartType = bodyPart,
                    DamageType = damageType
                };
                botPlayer.HealthPacket.ToggleSend();
                return;
            }

            var player = __instance.Player as CoopPlayer;
            if (player == null || !player.IsYourPlayer)
                return;

            player.HealthPacket.HasBodyPartDestroyInfo = true;
            player.HealthPacket.DestroyBodyPartPacket = new()
            {
                BodyPartType = bodyPart,
                DamageType = damageType
            };
            player.HealthPacket.ToggleSend();
        }


        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
        {

        }
    }
}
