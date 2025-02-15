﻿using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ULTRAKIT.Data.ScriptableObjects.Registry;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ULTRAKIT.Loader.Registries
{

    public static class UKContentSpawnableExtensions
    {
        public static SpawnableObject GetAsSpawnable(this UKContentSpawnable item)
        {
            SpawnableObject a = new SpawnableObject();

            a.gameObject = item.Prefab;
            foreach (var c in a.gameObject.GetComponentsInChildren<Renderer>(true))
            {
                c.material.shader = Shader.Find(c.material.shader.name);
            }

            a.armOffset = Vector3.zero;
            a.armRotationOffset = Vector3.zero;
            a.menuOffset = Vector3.zero;
            a.backgroundColor = Color.white;

            a.gridIcon = item.Icon;

            a.objectName = item.Name;

            a.strategy = "";

            a.enemyType = EnemyType.MinosPrime;
            a.type = "Custom Spawnable";
            a.preview = new GameObject();
            GameObject.DontDestroyOnLoad(a.preview);


            return a;
        }
    }

    [HarmonyPatch(typeof(DebugArm))]
    public static class DebugArmPatch
    {
        public static InputAction dotAct = new InputAction("dot"), comAct = new InputAction("comma");
        static DebugArmPatch()
        {
            dotAct.AddBinding("<Keyboard>/period");
            dotAct.started += (ctx) =>
            {
                curIndex += 1;
            };
            comAct.AddBinding("<Keyboard>/comma");
            comAct.performed += (ctx) =>
            {
                curIndex -= 1;
            };
        }


        public static Dictionary<SpawnMenu, Addon> menus = new Dictionary<SpawnMenu, Addon>();
        static int _curIndex;

        public static int curIndex
        {
            get
            {
                return _curIndex;
            }

            set
            {
                var menuList = menus.Keys.ToList();
                var prevIndex = curIndex;
                _curIndex = (int)Mathf.Repeat(value, menus.Count);

                var oldMenu = menuList[prevIndex];
                var newMenu = menuList[curIndex];

                if (oldMenu.gameObject.activeInHierarchy)
                {
                    oldMenu.gameObject.SetActive(false);
                    newMenu.gameObject.SetActive(true);
                }
            }
        }


        [HarmonyPatch("Awake")]
        [HarmonyPrefix]
        public static bool AwakePrefix(DebugArm __instance)
        {
            menus.Clear();

            dotAct.Enable();
            comAct.Enable();

            __instance.SetPrivate("menu", MonoSingleton<HUDOptions>.Instance.GetComponentInChildren<SpawnMenu>(true));
            var initMenu = __instance.GetPrivate<SpawnMenu>("menu");
            initMenu.arm = __instance;
            menus.Add(initMenu, null);



            foreach (var addon in AddonLoader.addons)
            {
                if (addon.GetAll<UKContentSpawnable>().Count == 0)
                {
                    continue;
                }

                var go = GameObject.Instantiate(initMenu.gameObject, initMenu.transform.parent);
                go.SetActive(false);

                // New menu instantiation
                var newMenu = go.GetComponent<SpawnMenu>();
                newMenu.arm = __instance;

                menus.Add(newMenu, addon);
            }

            __instance.SetPrivate("menu", menus.Keys.ToList()[curIndex]);

            return false;
        }

        [HarmonyPatch("OnDestroy")]
        [HarmonyPrefix]
        public static void DestroyPrefix()
        {
            dotAct.Disable();
            comAct.Disable();
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool UpdatePrefix(DebugArm __instance, AudioSource ___jabSound, Animator ___armAnimator, SpawnableObject ___currentObject, LayerMask ___raycastLayers, SpawnMenu ___menu)
        {


            if (Time.timeScale == 0f)
            {
                return false;
            }
            if (MonoSingleton<InputManager>.Instance.InputSource.Fire1.WasPerformedThisFrame)
            {
                if (___menu.gameObject.activeSelf || ___currentObject == null)
                {
                    return false;
                }

                __instance.StopAllCoroutines();
                __instance.StartCoroutine("HandClosedAnimationThingy");

                ___jabSound.Play();
                ___armAnimator.SetTrigger(Animator.StringToHash("Punch"));
                RaycastHit raycastHit;
                if (Physics.Raycast(__instance.cameraCtrl.transform.position, __instance.cameraCtrl.transform.forward, out raycastHit, 50f, ___raycastLayers))
                {
                    var goreZone = __instance.GetType().GetMethod("GetGoreZone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(__instance, new object[] { }) as GoreZone;
                    var go = GameObject.Instantiate<GameObject>(___currentObject.gameObject, raycastHit.point, Quaternion.identity, goreZone.transform);
                    go.transform.forward = -MonoSingleton<NewMovement>.Instance.transform.forward;
                }
            }
            if (MonoSingleton<InputManager>.Instance.InputSource.Fire2.WasPerformedThisFrame)
            {

                ___menu.gameObject.SetActive(true);
                MonoSingleton<OptionsManager>.Instance.Freeze();
            }

            __instance.SetPrivate("menu", menus.Keys.ToList()[curIndex]);

            return false;
        }
    }

    [HarmonyPatch(typeof(SpawnMenu))]
    public static class SpawnMenuPatch
    {
        [HarmonyPatch("Awake")]
        [HarmonyPrefix]
        public static bool AwakePrefix(SpawnMenu __instance)
        {
            if (DebugArmPatch.menus[__instance] == null)
            {
                // This is the default menu, leave as-is
                return true;
            }
            else
            {
                // Setup
                var addon = DebugArmPatch.menus[__instance];
                var content = addon.GetAll<UKContentSpawnable>();

                var secRef = __instance.GetPrivate<SpawnMenuSectionReference>("sectionReference");
                secRef.gameObject.SetActive(false);

                foreach (Transform child in secRef.transform.parent)
                {
                    if (child != secRef.transform)
                    {
                        GameObject.Destroy(child.gameObject);
                    }
                }

                // Addon title
                var addonNameSec = GameObject.Instantiate(secRef, secRef.transform.parent);
                addonNameSec.sectionName.text = addon.Data?.ModName ?? "UNNAMED ADDON";
                addonNameSec.sectionName.alignment = TextAnchor.MiddleCenter;
                addonNameSec.sectionName.fontSize = 45;
                addonNameSec.gameObject.SetActive(true);
                addonNameSec.button.gameObject.SetActive(false);

                // New section
                var newSec = GameObject.Instantiate(secRef, secRef.transform.parent);
                newSec.sectionName.enabled = false;

                foreach (var spawnable in content)
                {
                    GameObject bgo = GameObject.Instantiate(newSec.button.gameObject, newSec.grid.transform, false);
                    var b = bgo.GetComponent<Button>();

                    if (spawnable.Icon == null)
                    {
                        b.transform.Find("Background").gameObject.SetActive(false);
                        var text = b.GetComponentInChildren<Text>(true);
                        text.text = spawnable.Name;
                        text.gameObject.SetActive(true);
                    }
                    else
                    {
                        var bg = b.transform.Find("Background");
                        bg.Find("Foreground").GetComponent<Image>().sprite = spawnable.Icon;
                        bg.GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f);
                    }

                    b.onClick.AddListener(delegate
                    {
                        __instance.arm.PreviewObject(spawnable.GetAsSpawnable());
                        __instance.gameObject.SetActive(false);
                    });

                }
                newSec.gameObject.SetActive(true);
                newSec.button.gameObject.SetActive(false);


                // Skip regular awake call
                return false;
            }
        }
    }
}
