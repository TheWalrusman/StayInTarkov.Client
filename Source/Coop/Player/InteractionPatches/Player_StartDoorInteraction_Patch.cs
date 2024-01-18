//using EFT;
//using EFT.Interactive;
//using EFT.InventoryLogic;
//using StayInTarkov.Coop.Players;
//using StayInTarkov.Networking;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;

//namespace StayInTarkov.Coop.Player.InteractionPatches
//{
//    internal class Player_StartDoorInteraction_Patch : ModuleReplicationPatch
//    {
//        public override Type InstanceType => typeof(MovementState);

//        public override string MethodName => "vmethod_0";

//        public static List<string> CallLocally = new();

//        protected override MethodBase GetTargetMethod()
//        {
//            return ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
//        }

//        [PatchPostfix]
//        public static void PatchPostfix(MovementState __instance, WorldInteractiveObject interactive, InteractionResult interactionResult, Action callback)
//        {
//            bool isKey = false;
//            string keyItemId = "";
//            string keyItemTemplateId = "";
//            GridItemAddressDescriptor addressDescriptor = null;
//            bool keySuccess = false;

//            if (interactive.Id == null)
//                return;

//            var botPlayer = GetPlayerByMovementState(__instance) as CoopBot;
//            if (botPlayer != null)
//            {
//                if (interactionResult is KeyInteractionResult keyInteractionResult)
//                {
//                    isKey = true;
//                    KeyComponent key = keyInteractionResult.Key;

//                    keyItemId = key.Item.Id;
//                    keyItemTemplateId = key.Item.TemplateId;

//                    if (key.Template.MaximumNumberOfUsage > 0 && key.NumberOfUsages + 1 >= key.Template.MaximumNumberOfUsage)
//                        callback();

//                    ItemAddress itemAddress = keyInteractionResult.DiscardResult != null ? keyInteractionResult.From : key.Item.Parent;
//                    if (itemAddress is GridItemAddress grid)
//                    {
//                        GridItemAddressDescriptor gridItemAddressDescriptor = new();
//                        gridItemAddressDescriptor.Container = new();
//                        gridItemAddressDescriptor.Container.ContainerId = grid.Container.ID;
//                        gridItemAddressDescriptor.Container.ParentId = grid.Container.ParentItem?.Id;
//                        gridItemAddressDescriptor.LocationInGrid = grid.LocationInGrid;
//                        addressDescriptor = gridItemAddressDescriptor;
//                    }

//                    keySuccess = keyInteractionResult.Succeed;
//                }

//                botPlayer.CommonPlayerPacket.HasWorldInteractionPacket = true;
//                if (isKey)
//                {
//                    botPlayer.CommonPlayerPacket.WorldInteractionPacket = new()
//                    {
//                        IsStart = true,
//                        InteractiveId = interactive.Id,
//                        InteractionType = interactionResult.InteractionType,
//                        HasKey = true,
//                        KeyItemId = keyItemId,
//                        KeyItemTemplateId = keyItemTemplateId,
//                        GridItemAddressDescriptor = addressDescriptor,
//                        KeySuccess = keySuccess
//                    };
//                }
//                else
//                {
//                    botPlayer.CommonPlayerPacket.WorldInteractionPacket = new()
//                    {
//                        IsStart = true,
//                        InteractiveId = interactive.Id,
//                        InteractionType = interactionResult.InteractionType,
//                        HasKey = false
//                    };
//                }
//                EFT.UI.ConsoleScreen.Log($"Sending WorldInteractionPacket on {interactive.Id}");
//                botPlayer.CommonPlayerPacket.ToggleSend();
//                return;
//            }

//            var player = GetPlayerByMovementState(__instance) as CoopPlayer;
//            if (player == null || !player.IsYourPlayer)
//                return;

//            if (interactionResult is KeyInteractionResult keyInteractionResult2)
//            {
//                isKey = true;
//                KeyComponent key = keyInteractionResult2.Key;

//                keyItemId = key.Item.Id;
//                keyItemTemplateId = key.Item.TemplateId;

//                if (key.Template.MaximumNumberOfUsage > 0 && key.NumberOfUsages + 1 >= key.Template.MaximumNumberOfUsage)
//                    callback();

//                ItemAddress itemAddress = keyInteractionResult2.DiscardResult != null ? keyInteractionResult2.From : key.Item.Parent;
//                if (itemAddress is GridItemAddress grid)
//                {
//                    GridItemAddressDescriptor gridItemAddressDescriptor = new();
//                    gridItemAddressDescriptor.Container = new();
//                    gridItemAddressDescriptor.Container.ContainerId = grid.Container.ID;
//                    gridItemAddressDescriptor.Container.ParentId = grid.Container.ParentItem?.Id;
//                    gridItemAddressDescriptor.LocationInGrid = grid.LocationInGrid;
//                    addressDescriptor = gridItemAddressDescriptor;
//                }

//                keySuccess = keyInteractionResult2.Succeed;
//            }

//            player.CommonPlayerPacket.HasWorldInteractionPacket = true;
//            if (isKey)
//            {
//                player.CommonPlayerPacket.WorldInteractionPacket = new()
//                {
//                    IsStart = true,
//                    InteractiveId = interactive.Id,
//                    InteractionType = interactionResult.InteractionType,
//                    HasKey = true,
//                    KeyItemId = keyItemId,
//                    KeyItemTemplateId = keyItemTemplateId,
//                    GridItemAddressDescriptor = addressDescriptor,
//                    KeySuccess = keySuccess
//                };
//            }
//            else
//            {
//                player.CommonPlayerPacket.WorldInteractionPacket = new()
//                {
//                    IsStart = true,
//                    InteractiveId = interactive.Id,
//                    InteractionType = interactionResult.InteractionType,
//                    HasKey = false
//                };
//            }
//            player.CommonPlayerPacket.ToggleSend();
//        }

//        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
//        {
            
//        }

//        public static EFT.Player GetPlayerByMovementState(MovementState movementState)
//        {
//            GameWorld world = Comfort.Common.Singleton<GameWorld>.Instance;
//            if (world != null)
//                foreach (var player in world.AllAlivePlayersList)
//                    if (player.CurrentManagedState == movementState)
//                        return player;

//            return null;
//        }
//    }
//}