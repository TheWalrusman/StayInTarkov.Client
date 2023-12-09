using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using JetBrains.Annotations;
using SIT.Core.Coop.NetworkPacket;
using StayInTarkov.Coop.NetworkPacket;
using StayInTarkov.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace StayInTarkov.Coop
{
    internal sealed class CoopInventoryController
        // At this point in time. PlayerOwnerInventoryController is required to fix Malfunction and Discard errors. This class needs to be replaced with PlayerInventoryController.
        : EFT.Player.PlayerOwnerInventoryController, ICoopInventoryController
    {
        ManualLogSource BepInLogger { get; set; }

        public HashSet<string> AlreadySent = new();

        private EFT.Player Player { get; set; }


        public CoopInventoryController(EFT.Player Player, Profile profile, bool examined) : base(Player, profile, examined)
        {
            BepInLogger = BepInEx.Logging.Logger.CreateLogSource(nameof(CoopInventoryController));
			this.Player = Player;
        }

        public override void SubtractFromDiscardLimits(Item rootItem, IEnumerable<ItemsCount> destroyedItems)
        {
        }

        public override void InProcess(ItemController executor, Item item, ItemAddress to, bool succeed, IOperation1 operation, Callback callback)
        {
            BepInLogger.LogInfo($"InProcess [executor]");

            // Taken from EFT.Player.PlayerInventoryController
            if (!succeed)
            {
                callback.Succeed();
                return;
            }
            base.InProcess(executor, item, to, succeed, operation, callback);
        }

        public override void OutProcess(Item item, ItemAddress from, ItemAddress to, IOperation1 operation, Callback callback)
        {
            BepInLogger.LogInfo($"OutProcess [item]");
			base.OutProcess(item, from, to, operation, callback);

		}

		public override void OutProcess(ItemController executor, Item item, ItemAddress from, ItemAddress to, IOperation1 operation, Callback callback)
		{
			BepInLogger.LogInfo($"OutProcess [executor]");

			base.OutProcess(executor, item, from, to, operation, callback);	
		}


		public Dictionary<string, (AbstractInventoryOperation, Action)> OperationCallbacks { get; } = new();
		public HashSet<string> SentExecutions { get; } = new();

        public override void Execute(AbstractInventoryOperation operation, [CanBeNull] Callback callback)
        {
            BepInLogger.LogInfo($"Execute");
            BepInLogger.LogInfo($"{operation}");

            if (callback == null)
            {
                callback = delegate
                {
                };
            }
            //EOperationStatus? localOperationStatus = null;
            if (!vmethod_0(operation))
            {
                operation.Dispose();
                callback.Fail("LOCAL: hands controller can't perform this operation");
                return;
            }
            //EOperationStatus? serverOperationStatus;
            //base.Execute(operation, callback);

            var json = SendExecute(operation);
			if(json == null)
				return;

            OperationCallbacks.Add(json, (operation, new Action(() => {

                    BepInLogger.LogInfo("ActionCallback");
                    operation.vmethod_0(delegate (IResult executeResult)
                    {
                        BepInLogger.LogInfo($"operation.vmethod_0 : {executeResult}");
                        if (executeResult.Succeed)
                        {
                            ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", CommandStatus.Succeed);
                            RaiseInvEvents(operation, CommandStatus.Succeed);
                            operation.Dispose();
                        }
                        else
                        {
                            ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", CommandStatus.Failed);
                        }
                        operation.Dispose();

                    }, false);



            }
            )
            ));

        }


        private string SendExecute(AbstractInventoryOperation operation)
		{
			string json = null;
            BepInLogger.LogInfo($"SendExecute");
            BepInLogger.LogInfo($"{operation.GetType()}");
            BepInLogger.LogInfo($"{operation}");

            //ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", CommandStatus.Begin);
            RaiseInvEvents(operation, CommandStatus.Begin);

            if (operation is MoveInternalOperation moveOperation)
            {
                MoveOperationDescriptor moveOperationDescriptor = new MoveOperationDescriptor();
                // From Packet
                Dictionary<string, object> fromPacket = new();
                ItemAddressHelpers.ConvertItemAddressToDescriptor(moveOperation.From
                    , ref fromPacket
                    , out var gridItemAddressDescriptorFrom
                    , out var slotItemAddressDescriptorFrom
                    , out var stackSlotItemAddressDescriptorFrom);

                moveOperationDescriptor.From = gridItemAddressDescriptorFrom != null ? gridItemAddressDescriptorFrom
                    : slotItemAddressDescriptorFrom != null ? slotItemAddressDescriptorFrom
                    : stackSlotItemAddressDescriptorFrom;

                // To Packet
                Dictionary<string, object> toPacket = new();
                ItemAddressHelpers.ConvertItemAddressToDescriptor(moveOperation.To
                    , ref toPacket
                    , out var gridItemAddressDescriptorTo
                    , out var slotItemAddressDescriptorTo
                    , out var stackSlotItemAddressDescriptorTo);

                moveOperationDescriptor.To = gridItemAddressDescriptorTo != null ? gridItemAddressDescriptorTo 
                    : slotItemAddressDescriptorTo != null ? slotItemAddressDescriptorTo
                    : stackSlotItemAddressDescriptorTo;

                moveOperationDescriptor.OperationId = moveOperation.Id;
                moveOperationDescriptor.ItemId = moveOperation.Item.Id;

                var isToAddressPlayerInventoryAddress = false;
                // There doesn't seem to be a tester for this?
                try
                {
                    isToAddressPlayerInventoryAddress = Player.Inventory.IsEquipmentAddress(this.ToItemAddress(moveOperationDescriptor.To));
                }
                catch(Exception)
                {

                }
                BepInLogger.LogInfo($"isToAddressEquipmentAddress:{isToAddressPlayerInventoryAddress}");

                var moveOpJson = moveOperationDescriptor.SITToJson();
                MoveOperationPacket moveOperationPacket 
                    = new MoveOperationPacket(
                        Player.ProfileId
                        , moveOperation.Item.Id
                        , moveOperation.Item.TemplateId
                        , moveOperation.To.GetType().ToString()
                        , moveOperation.From != null ? moveOperation.From.GetType().ToString() : null
                        , null
                        );
                moveOperationPacket.MoveOpJson = moveOpJson;

				json = moveOperationPacket.SITToJson();
              
            }
			// Throw/Discard operation
			else if (operation is MoveInternalOperation2 throwOperation)
			{
                ThrowOperationDescriptor throwOperationDescriptor = new ThrowOperationDescriptor();

                throwOperationDescriptor.OperationId = throwOperation.Id;
                throwOperationDescriptor.ItemId = throwOperation.Item.Id;

                var moveOpJson = throwOperationDescriptor.SITToJson();
                MoveOperationPacket moveOperationPacket = new MoveOperationPacket(Player.ProfileId, throwOperation.Item.Id, throwOperation.Item.TemplateId, null, null);
				moveOperationPacket.Method = "ThrowOperation";
                moveOperationPacket.MoveOpJson = moveOpJson;

                json = moveOperationPacket.SITToJson();
            }
            else if (operation is FoldOperation foldOperation)
            {
                var foDescriptor = OperationToDescriptorHelpers.FromFoldOperation(foldOperation);
                using MemoryStream memoryStream = new();
                using BinaryWriter binaryWriter = new(memoryStream);
                binaryWriter.WritePolymorph(foDescriptor);

                Dictionary<string, object> dictionary = new()
                {
                    { "i", operation.Id },
                    { "profileId", Player.ProfileId },
                    { "d", memoryStream.ToArray().SITToJson() },
                    { "ct", operation.GetType().Name },
                    { "m", "PolymorphInventoryOperation" }
                };

                json = dictionary.SITToJson();

            }
            else 
            {
                var oneitemoperation = operation as IOneItemOperation;
                if (oneitemoperation != null)
                {
                    BepInLogger.LogInfo("SendExecute:IOneItemOperation");

                    MoveOperationDescriptor moveOperationDescriptor = new MoveOperationDescriptor();
                    // From Packet
                    Dictionary<string, object> fromPacket = new();
                    ItemAddressHelpers.ConvertItemAddressToDescriptor(oneitemoperation.From1
                        , ref fromPacket
                        , out var gridItemAddressDescriptorFrom
                        , out var slotItemAddressDescriptorFrom
                        , out var stackSlotItemAddressDescriptorFrom);

                    moveOperationDescriptor.From = gridItemAddressDescriptorFrom != null ? gridItemAddressDescriptorFrom
                        : slotItemAddressDescriptorFrom != null ? slotItemAddressDescriptorFrom
                        : stackSlotItemAddressDescriptorFrom;

                    // To Packet
                    Dictionary<string, object> toPacket = new();
                    ItemAddressHelpers.ConvertItemAddressToDescriptor(oneitemoperation.To1
                        , ref toPacket
                        , out var gridItemAddressDescriptorTo
                        , out var slotItemAddressDescriptorTo
                        , out var stackSlotItemAddressDescriptorTo);

                    moveOperationDescriptor.To = gridItemAddressDescriptorTo != null ? gridItemAddressDescriptorTo
                        : slotItemAddressDescriptorTo != null ? slotItemAddressDescriptorTo
                        : stackSlotItemAddressDescriptorTo;

                    moveOperationDescriptor.OperationId = operation.Id;
                    moveOperationDescriptor.ItemId = oneitemoperation.Item1.Id;

                    var moveOpJson = moveOperationDescriptor.SITToJson();

                    MoveOperationPacket moveOperationPacket = new MoveOperationPacket(
                        Player.ProfileId
                        , oneitemoperation.Item1.Id
                        , oneitemoperation.Item1.TemplateId
                        , oneitemoperation.To1 != null ? oneitemoperation.To1.GetType().ToString() : null
                        , oneitemoperation.From1 != null ? oneitemoperation.From1.GetType().ToString() : null
                        , oneitemoperation.GetType().FullName);
                    moveOperationPacket.MoveOpJson = moveOpJson;

                    json = moveOperationPacket.SITToJson();
                }
            }

            if (json == null)
                return null;

			if (OperationCallbacks.ContainsKey(json))
				return null;

			if (SentExecutions.Contains(json)) 
				return null;

            AkiBackendCommunication.Instance.PostDownWebSocketImmediately(json);
            SentExecutions.Add(json);
            return json;
        }

        public void CancelExecute(string packetJson)
        {
            BepInLogger.LogError($"CancelExecute");
            BepInLogger.LogInfo($"{packetJson}");
            if (OperationCallbacks.ContainsKey(packetJson))
            {
                OperationCallbacks[packetJson].Item1.vmethod_0(delegate (IResult result)
                {
                    ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(OperationCallbacks[packetJson].Item1, "commandStatus_0", CommandStatus.Succeed);
                });
                OperationCallbacks[packetJson].Item2();
                OperationCallbacks.Remove(packetJson);
            }
        }

        public void ReceiveExecute(AbstractInventoryOperation operation, string packetJson)
        {
            BepInLogger.LogInfo($"ReceiveExecute");
            BepInLogger.LogInfo($"{packetJson}");

            if (operation == null)
                return;

            BepInLogger.LogInfo($"{operation}");

            //ReceivedOperationPacket = operation;
            //ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", CommandStatus.Begin);
            if (OperationCallbacks.ContainsKey(packetJson))
            {
                BepInLogger.LogInfo($"Using OperationCallbacks!");

                //OperationCallbacks[packetJson].Item1.vmethod_0(delegate (IResult result)
                //{
                //    //ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(OperationCallbacks[packetJson].Item1, "commandStatus_0", CommandStatus.Succeed);
                //});
                RaiseInvEvents(operation, CommandStatus.Succeed);
                RaiseInvEvents(OperationCallbacks[packetJson].Item1, CommandStatus.Succeed);
                OperationCallbacks[packetJson].Item2();
                //OperationCallbacks[packetJson].Item1.vmethod_0((IResult result) => { RaiseInvEvents(operation, CommandStatus.Succeed); }, true);
                OperationCallbacks.Remove(packetJson);
            }
            else
            {
                operation.vmethod_0(delegate (IResult result)
                {
                    ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", CommandStatus.Succeed);
                });

            }
        }

        void RaiseInvEvents(object operation, CommandStatus status)
        {
            if (operation == null) 
                return;

            ReflectionHelpers.SetFieldOrPropertyFromInstance<CommandStatus>(operation, "commandStatus_0", status);

            //var baseInvOp = (BaseInventoryOperation)ReflectionHelpers.GetFieldFromTypeByFieldType(operation.GetType(), typeof(BaseInventoryOperation))?.GetValue(operation);
            //if (baseInvOp != null)
            //{
            //    baseInvOp.RaiseEvents(status);
            //}
            //var oneItemOp = (OneItemOperation)ReflectionHelpers.GetFieldFromTypeByFieldType(operation.GetType(), typeof(OneItemOperation))?.GetValue(operation);
            //if (oneItemOp != null)
            //{
            //    oneItemOp.RaiseEvents(status);
            //}
        }

        public override bool vmethod_0(AbstractInventoryOperation operation)
        {
            return true;
        }

        public void ReceiveDoOperation(AbstractDescriptor1 descriptor)
        {
			var invOp = Player.ToInventoryOperation(descriptor);
			BepInLogger.LogInfo("ReceiveDoOperation");
            BepInLogger.LogInfo(invOp);
            base.vmethod_0(invOp.Value);

		}

        public override Task<IResult> LoadMagazine(BulletClass sourceAmmo, MagazineClass magazine, int loadCount, bool ignoreRestrictions)
        {
            //BepInLogger.LogInfo("LoadMagazine");
            return base.LoadMagazine(sourceAmmo, magazine, loadCount, ignoreRestrictions);
        }

        public override Task<IResult> UnloadMagazine(MagazineClass magazine)
        {
            Task<IResult> result;
            //ItemControllerHandler_Move_Patch.DisableForPlayer.Add(Profile.ProfileId);

            BepInLogger.LogInfo("UnloadMagazine");
            ItemPlayerPacket unloadMagazinePacket = new(Profile.ProfileId, magazine.Id, magazine.TemplateId, "PlayerInventoryController_UnloadMagazine");
            var serialized = unloadMagazinePacket.Serialize();

            //if (AlreadySent.Contains(serialized))
            {
                result = base.UnloadMagazine(magazine);
                //ItemControllerHandler_Move_Patch.DisableForPlayer.Remove(Profile.ProfileId);
            }

            //AlreadySent.Add(serialized);

            AkiBackendCommunication.Instance.SendDataToPool(serialized);
            result = base.UnloadMagazine(magazine);
            //ItemControllerHandler_Move_Patch.DisableForPlayer.Remove(Profile.ProfileId);
            return result;
        }

        Random randomGenThrowNumber = new Random();

        public override void ThrowItem(Item item, IEnumerable<ItemsCount> destroyedItems, Callback callback = null, bool downDirection = false)
        {
            //BepInLogger.LogInfo("ThrowItem");
            //destroyedItems = new List<ItemsCount>();
            //base.ThrowItem(item, destroyedItems, callback, downDirection);
            Execute(new MoveInternalOperation2(ushort_0++, this, item, destroyedItems, Player, downDirection), callback);
        }

        public override SOperationResult3<bool> TryThrowItem(Item item, Callback callback = null, bool silent = false)
        {
            return base.TryThrowItem(item, callback, silent);
        }

        public void ReceiveUnloadMagazineFromServer(ItemPlayerPacket unloadMagazinePacket)
        {
            BepInLogger.LogInfo("ReceiveUnloadMagazineFromServer");
            if (ItemFinder.TryFindItem(unloadMagazinePacket.ItemId, out Item magazine))
            {
                //ItemControllerHandler_Move_Patch.DisableForPlayer.Add(unloadMagazinePacket.ProfileId);
                base.UnloadMagazine((MagazineClass)magazine);
                //ItemControllerHandler_Move_Patch.DisableForPlayer.Remove(unloadMagazinePacket.ProfileId);

            }
        }

        public static bool IsDiscardLimitsFine(Dictionary<string, int> DiscardLimits)
        {
            return DiscardLimits != null
                && DiscardLimits.Count > 0
                && DiscardLimits.ContainsKey("5449016a4bdc2d6f028b456f") // Roubles, Value: 20000
                && DiscardLimits.ContainsKey("5696686a4bdc2da3298b456a") // Dollars, Value: 0
                && DiscardLimits.ContainsKey("569668774bdc2da2298b4568") // Euros, Value: 0
                && DiscardLimits.ContainsKey("5448be9a4bdc2dfd2f8b456a") // RGD-5 Grenade, Value: 20
                && DiscardLimits.ContainsKey("5710c24ad2720bc3458b45a3") // F-1 Grenade, Value: 20
                && DiscardLimits.ContainsKey(DogtagComponent.BearDogtagsTemplate) // Value: 0
                && DiscardLimits.ContainsKey(DogtagComponent.UsecDogtagsTemplate); // Value: 0
        }

    }

    public interface ICoopInventoryController
    {
        public void ReceiveUnloadMagazineFromServer(ItemPlayerPacket unloadMagazinePacket);
    }
}
