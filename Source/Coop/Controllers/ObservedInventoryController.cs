using Comfort.Common;
using EFT;
using StayInTarkov.Coop.Players;
using UnityEngine;

namespace StayInTarkov.Coop.Controllers
{
    public class ObservedInventoryController(EFT.Player player, Profile profile, bool examined) : EFT.Player.PlayerInventoryController(player, profile, examined)
    {

        public override void StrictCheckMagazine(MagazineClass magazine, bool status, int skill = 0, bool notify = false, bool useOperation = true)
        {
            // Do nothing
        }

        public override void OnAmmoLoadedCall(int count)
        {
            // Do nothing
        }

        public override void OnAmmoUnloadedCall(int count)
        {
            // Do nothing
        }

        public override void OnMagazineCheckCall()
        {
            // Do nothing
        }

        public override bool IsInventoryBlocked()
        {
            return false;
        }

        public override void StartSearchingAction(GItem1 item)
        {
            // TODO: Add networking
            base.StartSearchingAction(item);
        }

        public override void StopSearchingAction(GItem1 item)
        {
            // TODO: Add networking
            base.StopSearchingAction(item);
        }

        public class ObservedInventoryOperationHandler()
        {
            public void OperationCallback(IResult executeResult)
            {
                if (!executeResult.Succeed)
                {
                    EFT.UI.ConsoleScreen.LogError($"{coopPlayer.ProfileId}: Operation ID {operation.Id} failed: {operation}\nError: {executeResult.Error}");
                    Debug.Log($"{coopPlayer.ProfileId}: Operation ID {operation.Id} failed: {operation}\nError: {executeResult.Error}");
                }
            }

            public AbstractInventoryOperation operation;
            public CoopPlayer coopPlayer;
        }

    }
}
