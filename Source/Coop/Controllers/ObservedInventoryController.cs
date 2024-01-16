using EFT;
using System;

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

    }
}
