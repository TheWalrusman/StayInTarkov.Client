﻿using EFT;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using BepInEx.Logging;
using StayInTarkov;
using StayInTarkov.Coop;
using StayInTarkov.Coop.Web;

namespace SIT.Core.Coop.Player.FirearmControllerPatches
{
    public class FirearmController_ReloadWithAmmo_Patch : ModuleReplicationPatch
    {
        public override Type InstanceType => typeof(EFT.Player.FirearmController);
        public override string MethodName => "ReloadWithAmmo";

        private static HashSet<string> _Processed = new();

        protected override MethodBase GetTargetMethod()
        {
            var method = ReflectionHelpers.GetMethodForType(InstanceType, MethodName);
            return method;
        }

        private ManualLogSource GetLogger()
        {
            return GetLogger(typeof(FirearmController_ReloadWithAmmo_Patch));
        }

        public override void Enable()
        {
            _Processed.Clear();
            base.Enable();
        }

        [PatchPostfix]
        public static void PostPatch(EFT.Player ____player)
        {
            var player = ____player;
            if (player == null)
            {
                Logger.LogError("Unable to obtain Player variable from Firearm Controller!");
                return;
            }

            Dictionary<string, object> dictionary = new()
            {
                { "m", "ReloadWithAmmo" }
            };
            AkiBackendCommunicationCoop.PostLocalPlayerData(player, dictionary, true, out _, out var generatedData);
            _Processed.Add(generatedData.SITToJson());
        }



        public override void Replicated(EFT.Player player, Dictionary<string, object> dict)
        {
            GetLogger(typeof(FirearmController_ReloadWithAmmo_Patch)).LogDebug("Replicated");

            if (_Processed.Contains(dict.SITToJson()))
                return;

            _Processed.Add(dict.SITToJson());

            if (player.HandsController is EFT.Player.FirearmController firearmCont)
            {
                try
                {
                    firearmCont.FirearmsAnimator.Reload(true);
                }
                catch (Exception e)
                {
                    GetLogger().LogError(e);
                }
            }
        }


    }
}
