﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using System.Reflection;
using BattleTech;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace AmmoCookOff
{
    public class AmmoCookOff
    {
        internal static ModSettings Settings;
        public static string ModDirectory;
        public static void Init(string modDir, string modSettings)
        {
            var harmony = HarmonyInstance.Create("io.github.RealityMachina.AmmoCookoff");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            ModDirectory = Path.Combine(Path.GetDirectoryName(VersionManifestUtilities.MANIFEST_FILEPATH), @"..\..\..\Mods\AmmoCookOff");

            try
            {
                Settings = JsonConvert.DeserializeObject<ModSettings>(modSettings);
            }
            catch (Exception)
            {
                Settings = new ModSettings();
            }
        }

        internal class ModSettings
        {
            public float OverheatChance = 10;
            public float ShutdownHeatChance = 25;
            public bool UseHBSMercySetting = true;
        }


        [HarmonyPatch(typeof(Mech), "CheckForHeatDamage")]
        public static class Mech_CheckHeatDamage_Patch
        {
            public static void Postfix(Mech __instance, int stackID, string attackerID)
            {
                if (__instance.IsDead || (__instance.IsFlaggedForDeath && __instance.HasHandledDeath) || !__instance.IsOverheated && !__instance.IsShutDown)
                    return; //don't bother if they're dead or not overheating

                foreach (MechComponent mechComponent in __instance.allComponents)
                {
                    if (mechComponent as AmmunitionBox != null)
                    {
                        AmmunitionBox ammoBox = mechComponent as AmmunitionBox;

                        if (ammoBox != null)
                        {
                            int value = ammoBox.StatCollection.GetValue<int>("CurrentAmmo");
                            int capacity = ammoBox.ammunitionBoxDef.Capacity;
                            float num = value / (float)capacity;
                            if (num < 0.5f && Settings.UseHBSMercySetting)
                            {
                                return;
                            }
                            var rng = (new System.Random()).Next(100);
                            var rollToBeat = __instance.IsShutDown ? Settings.ShutdownHeatChance : Settings.OverheatChance; //if shut down, we use the Shutdown chance. Otherwise, the normal overheat chance.

                            
                            if (rng < rollToBeat) //things are exploding captain!
                            {
                                if (__instance.Combat.Constants.PilotingConstants.InjuryFromAmmoExplosion)
                                {
                                    Pilot pilot = __instance.GetPilot();
                                    if (pilot != null)
                                    {
                                        pilot.SetNeedsInjury(InjuryReason.AmmoExplosion);
                                    }
                                }
                                string text = string.Format("{0} EXPLOSION", ammoBox.Name);
                                ammoBox.parent.Combat.MessageCenter.PublishMessage(new FloatieMessage(ammoBox.parent.GUID, ammoBox.parent.GUID, text, FloatieMessage.MessageNature.CriticalHit));
                                //we make a fake hit info to apply the nuking
                                WeaponHitInfo hitInfo = new WeaponHitInfo(stackID, -1, -1, -1, string.Empty, string.Empty, -1, null, null, null, null, null, null, null, new AttackDirection[]
                                {
                                    AttackDirection.FromFront
                                }, null, null, null);
                                Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
                                __instance.NukeStructureLocation(hitInfo, ammoBox.Location, (ChassisLocations)ammoBox.Location, onUnitSphere, DamageType.Overheat);
                                ChassisLocations dependentLocation = MechStructureRules.GetDependentLocation((ChassisLocations)ammoBox.Location);
                                if (dependentLocation != ChassisLocations.None && !__instance.IsLocationDestroyed(dependentLocation))
                                {
                                    __instance.NukeStructureLocation(hitInfo, ammoBox.Location, dependentLocation, onUnitSphere, DamageType.Overheat);
                                }

                            }
                        }
                    }
                }
            }
        }
    }
}
