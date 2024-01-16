using EFT;
using Comfort.Common;
using System.Reflection;
using System.Collections;
using EFT.HealthSystem;

namespace StayInTarkov.AkiSupport.Singleplayer.Patches.RaidFix
{
    /// <summary>
    /// Credit: SPT-Aki team
    /// Link: https://dev.sp-tarkov.com/SPT-AKI/Modules/src/branch/master/project/Aki.SinglePlayer/Patches/RaidFix/TinnitusFixPatch.cs
    /// </summary>
    public class TinnitusFixPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BetterAudio).GetMethod("StartTinnitusEffect", BindingFlags.Instance | BindingFlags.Public);
        }

        // checks on invoke whether the player is stunned before allowing tinnitus
        [PatchPrefix]
        protected static bool PatchPrefix()
        {
            bool shouldInvoke = typeof(ActiveHealthController)
                .GetMethod("FindActiveEffect", BindingFlags.Instance | BindingFlags.Public)
                .MakeGenericMethod(typeof(ActiveHealthController)
                .GetNestedType("Stun", BindingFlags.Instance | BindingFlags.NonPublic))
                .Invoke(Singleton<GameWorld>.Instance.MainPlayer.ActiveHealthController, [EBodyPart.Common]) != null;

            return shouldInvoke;
        }

        // prevent null coroutine exceptions
        protected static IEnumerator CoroutinePassthrough()
        {
            yield return null;
            yield break;
        }
    }
}