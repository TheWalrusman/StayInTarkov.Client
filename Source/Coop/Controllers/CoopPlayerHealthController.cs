using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using System;
using UnityEngine;

namespace StayInTarkov.Coop
{
    internal sealed class CoopPlayerHealthController : AbstractHealth
    {
        public CoopPlayerHealthController(byte[] serializedState, InventoryController inventory, SkillManager skills) : base(serializedState, inventory, skills)
        {

        }

        public override bool ApplyItem(Item item, EBodyPart bodyPart, float? amount = null)
        {
            throw new NotImplementedException();
        }

        public override void CancelApplyingItem()
        {
            throw new NotImplementedException();
        }
    }
}
