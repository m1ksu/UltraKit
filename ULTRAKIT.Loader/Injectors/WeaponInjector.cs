﻿using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using ULTRAKIT.Data.Components;
using ULTRAKIT.Data.ScriptableObjects.Registry;
using ULTRAKIT.Lua.Components;
using UnityEngine;

namespace ULTRAKIT.Loader.Registries
{
    [HarmonyPatch(typeof(GunSetter), "ResetWeapons")]
    public class GunSetterPatch
    {
        public static List<List<GameObject>> modSlots = new List<List<GameObject>>();
        public static Dictionary<GameObject, bool> equippedDict = new Dictionary<GameObject, bool>();

        static void Postfix(GunSetter __instance)
        {
            foreach (var slot in modSlots)
            {
                foreach (var item in slot)
                {
                    if (item)
                    {
                        GameObject.Destroy(item);
                    }
                }

                if (__instance.gunc?.slots?.Contains(slot) ?? false)
                {
                    __instance.gunc.slots.Remove(slot);
                }
            }
            modSlots.Clear();
            foreach (var pair in AddonLoader.registry)
            {
                foreach (var weap in pair.Key.GetAll<UKContentWeapon>())
                {


                    // check if equipped
                    var slot = new List<GameObject>();

                    int i = 0;
                    foreach (var variant in weap.Variants)
                    {
                        if (!equippedDict.ContainsKey(variant))
                        {
                            equippedDict.Add(variant, true);
                        }

                        if (!equippedDict[variant])
                        {
                            continue;
                        }


                        var go = GameObject.Instantiate(variant, __instance.transform);
                        go.SetActive(false);


                        foreach (var c in go.GetComponentsInChildren<Renderer>(true))
                        {
                            Debug.Log(c.gameObject.name);
                            c.gameObject.layer = LayerMask.NameToLayer("AlwaysOnTop");

                            var glow = c.gameObject.GetComponent<UKGlow>();

                            if (glow)
                            {
                                c.material.shader = Shader.Find("psx/railgun");
                                c.material.SetFloat("_EmissivePosition", 5);
                                c.material.SetFloat("_EmissiveStrength", glow.glowIntensity);
                                c.material.SetColor("_EmissiveColor", glow.glowColor);
                            }
                            else
                            {
                                c.material.shader = Shader.Find(c.material.shader.name);
                            }
                        }

                        var wi = go.AddComponent<WeaponIcon>();
                        wi.weaponIcon = weap.Icon;
                        wi.glowIcon = weap.Icon;
                        wi.variationColor = i;
                        i++;

                        slot.Add(go);

                        var field = typeof(GunControl).GetField("weaponFreshnesses", BindingFlags.NonPublic | BindingFlags.Instance);
                        List<float> freshnessList = field.GetValue(__instance.gunc) as List<float>;
                        freshnessList.Add(10);
                        field.SetValue(__instance.gunc, freshnessList);


                        __instance.gunc.allWeapons.Add(go);

                        UKScriptRuntime.Create(pair.Key.Data, go);
                    }

                    __instance.gunc.slots.Add(slot);

                    modSlots.Add(slot);
                }
            }
        }
    }

    [HarmonyPatch(typeof(GunControl))]
    class GunControlPatch
    {
        [HarmonyPatch("Start")]
        [HarmonyPrefix]
        static void StartPrefix(GunControl __instance)
        {
            // Important to avoid semi-permanently breaking weapon script lol
            if (PlayerPrefs.GetInt("CurSlo", 1) > __instance.slots.Count)
            {
                PlayerPrefs.SetInt("CurSlo", 1);
            }
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(GunControl __instance)
        {
            if (MonoSingleton<InputManager>.Instance.InputSource.Slot6.WasPerformedThisFrame && __instance.slots[5]?.Count > 0)
            {
                __instance.SwitchWeapon(6);
            }

            if (MonoSingleton<InputManager>.Instance.InputSource.Slot7.WasPerformedThisFrame && __instance.slots[6]?.Count > 0)
            {
                __instance.SwitchWeapon(7);
            }

            if (MonoSingleton<InputManager>.Instance.InputSource.Slot8.WasPerformedThisFrame && __instance.slots[7]?.Count > 0)
            {
                __instance.SwitchWeapon(8);
            }
        }
    }

}
