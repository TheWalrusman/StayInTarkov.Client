using Comfort.Common;
using EFT;
using EFT.Interactive;
using StayInTarkov.Coop.Players;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StayInTarkov.Coop.World
{
    internal class LootableContainer_Interact_Patch : ModulePatch
    {
        public static Type InstanceType => typeof(LootableContainer);

        public static string MethodName => "LootableContainer_Interact";

        protected override MethodBase GetTargetMethod()
        {
            return ReflectionHelpers.GetAllMethodsForType(InstanceType).FirstOrDefault(x => x.Name == "Interact" && x.GetParameters().Length == 1 && x.GetParameters()[0].Name == "interactionResult");
        }

        static ConcurrentBag<long> ProcessedCalls = new();

        protected static bool HasProcessed(Dictionary<string, object> dict)
        {
            var timestamp = long.Parse(dict["t"].ToString());

            if (!ProcessedCalls.Contains(timestamp))
            {
                ProcessedCalls.Add(timestamp);
                return false;
            }

            return true;
        }

        //[PatchPrefix]
        //public static bool Prefix(LootableContainer __instance)
        //{
        //    return false;
        //}

        [PatchPostfix]
        public static void Postfix(LootableContainer __instance, InteractionResult interactionResult)
        {
            if (__instance.Id == null)
                return;

            var player = Singleton<GameWorld>.Instance.MainPlayer as CoopPlayer;
            if (player == null)
                return;

            player.CommonPlayerPacket.HasContainerInteractionPacket = true;
            player.CommonPlayerPacket.ContainerInteractionPacket = new()
            {
                InteractiveId = __instance.Id,
                InteractionType = interactionResult.InteractionType
            };
            player.CommonPlayerPacket.ToggleSend();
        }

        public static void Replicated(Dictionary<string, object> packet)
        {

        }
    }
}