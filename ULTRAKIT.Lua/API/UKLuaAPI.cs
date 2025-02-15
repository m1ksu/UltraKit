﻿using HarmonyLib;
using MoonSharp.Interpreter;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using ULTRAKIT.Lua.API.Abstract;
using ULTRAKIT.Lua.API.Proxies;
using ULTRAKIT.Lua.Attributes;
using ULTRAKIT.Lua.Components;
using UnityEngine;
using MoonSharp.Interpreter.Serialization;

namespace ULTRAKIT.Lua.API
{
    //[HarmonyPatch(typeof(DynValue))]
    //public static class DynValuePatch
    //{
    //    [HarmonyPatch(nameof(DynValue.IsNotNil))]
    //    [HarmonyPrefix]
    //    public static bool IsNotNilPrefix(DynValue __instance, ref bool __result)
    //    {
    //        if(__instance.Type == DataType.UserData && __instance.UserData.Object == null)
    //        {
    //            __result = false;
    //            return false;
    //        }

    //        return true;
    //    }

    //    [HarmonyPatch(nameof(DynValue.IsNil))]
    //    [HarmonyPrefix]
    //    public static bool IsNilPrefix(DynValue __instance, ref bool __result)
    //    {

    //        if (__instance.Type == DataType.UserData && __instance.UserData. == null)
    //        {
    //            UserData
    //            Debug.Log($"{__instance.UserData.Descriptor.Name} IS APPARENTLY NULL");
    //            __result = true;
    //            return false;
    //        }

    //        return true;
    //    }

    //    [HarmonyPatch(nameof(DynValue.Equals))]
    //    [HarmonyPrefix]
    //    public static bool EqualsPrefix(DynValue __instance, ref bool __result, object obj)
    //    {
    //        var other = obj as DynValue;
    //        if(obj == null)
    //        {
    //            return true;
    //        }

    //        if(__instance.Type == DataType.UserData && other.Type == DataType.Nil)
    //        {
    //            Debug.Log("NO WAY HOW DID IT ACTUALLY WORK HOLY SHIT");
    //            __result = __instance.UserData == null;
    //            return false;
    //        }

    //        return true;
    //    }
    //}

    public static class UKLuaAPI
    {
        public static Dictionary<string, UKStatic> luaStatics = new Dictionary<string, UKStatic>();
        public static List<Type> luaStructs = new List<Type>();
        public static Action<UKScriptRuntime> constructMethods, destructMethods, updateMethods;
        public static Harmony harmony = new Harmony("ULTRAKIT.Lua");

        ///<summary> 
        /// Registers UserData, fills in constructMethods and deconstructMethods to be called when a script is destroyed or created, and registers all proxies
        ///</summary>
        public static void Initialize()
        {
            harmony.PatchAll();

            RegisterUnityStruct<Vector3>();
            RegisterUnityStruct<Vector2>();
            RegisterUnityStruct<Quaternion>();
            RegisterUnityStruct<Color>();
            RegisterUnityStruct<Mathf>();
            RegisterUnityStruct<Bounds>();
            RegisterUnityStruct<ParticleSystem.EmissionModule>();

            RegisterUnityStruct<SpriteDrawMode>();
            RegisterUnityStruct<SpriteTileMode>();
            RegisterUnityStruct<SpriteMaskInteraction>();
            RegisterUnityStruct<SpriteSortPoint>();

            UserData.RegisterType<Collision>();
            UserData.RegisterType<ParticleSystem>();
            UserData.RegisterType<SpriteRenderer>();
            UserData.RegisterType<TrailRenderer>();
            UserData.RegisterType<MeshRenderer>();
            UserData.RegisterType<LineRenderer>();
            UserData.RegisterType<StyleHUD>();
            UserData.RegisterType<RenderTexture>();

            // TODO: evaluate if we should expose these as a whole
            UserData.RegisterType<Camera>();
            UserData.RegisterType<Texture2D>();
            UserData.RegisterType<Sprite>();

            // Register all types with MoonsharpUserData attribute
            UserData.RegisterAssembly();

            // Register all types extending UKStatic
            var staticsToInitialize = AttributeHelper.GetDerivedTypes(typeof(UKStatic));
            foreach (var staticType in staticsToInitialize)
            {
                var inst = (UKStatic)Activator.CreateInstance(staticType);
                
                if(inst.name == null)
                {
                    Debug.LogError($"HECKTECK FORGOT TO GIVE {staticType.Name} A STATIC NAME, GO SHOUT AT HIM ON DISCORD RIGHT NOW");
                    continue;
                }

                luaStatics.Add(inst.name, inst);
            }


            foreach (var inst in luaStatics.Values)
            {
                UserData.RegisterType(inst.GetType());

                // Register all methods with UKScriptConstructor attribute
                foreach (var method in AttributeHelper.GetMethodsWith<UKScriptConstructor>(inst.GetType(), BindingFlags.NonPublic | BindingFlags.Instance).Keys)
                {
                    constructMethods += method.CreateDelegate(typeof(Action<UKScriptRuntime>), inst) as Action<UKScriptRuntime>;
                }

                // Register all methods with UKScriptUpdater attributes
                foreach (var method in AttributeHelper.GetMethodsWith<UKScriptUpdater>(inst.GetType(), BindingFlags.NonPublic | BindingFlags.Instance).Keys)
                {
                    updateMethods += method.CreateDelegate(typeof(Action<UKScriptRuntime>), inst) as Action<UKScriptRuntime>;
                }

                // Register all methods with UKScriptDestructor attributes
                foreach (var method in AttributeHelper.GetMethodsWith<UKScriptDestructor>(inst.GetType(), BindingFlags.NonPublic | BindingFlags.Instance).Keys)
                {
                    destructMethods += method.CreateDelegate(typeof(Action<UKScriptRuntime>), inst) as Action<UKScriptRuntime>;
                }
            }

            var regMethod = typeof(UKLuaAPI).GetMethod(nameof(RegisterProxyType), BindingFlags.Public | BindingFlags.Static);
            foreach (var type in AttributeHelper.GetDerivedTypes(typeof(UKProxy<>)))
            {
                var targetType = type.BaseType.GetGenericArguments()[0];

                var regMethodGen = regMethod.MakeGenericMethod(type, targetType);
                regMethodGen.Invoke(null, new object[] { });
            }
        }

        /// <summary>
        /// Registers a default unity struct (these cannot be proxied effectively as they are value types)
        /// </summary>
        /// <typeparam name="T">The struct to be registered</typeparam>
        public static void RegisterUnityStruct<T>()
            where T : struct
        {
            UserData.RegisterType<T>();
            luaStructs.Add(typeof(T));
        }

        /// <summary>
        /// A wrapper function to allow calls to UserData.RegisterProxy with runtime types.
        /// </summary>
        /// <typeparam name="TProxy">The proxy type to be registered</typeparam>
        /// <typeparam name="TTarget">The type that the proxy is targeting</typeparam>
        public static void RegisterProxyType<TProxy, TTarget>()
            where TProxy : UKProxy<TTarget>
            where TTarget : class
        {
            UserData.RegisterProxyType<TProxy, TTarget>((target) => (TProxy)Activator.CreateInstance(typeof(TProxy), target));
        }

        ///<summary>
        /// Accepts a UKScriptRuntime and calls constructMethods on it, allowing all the API modules to access the scripts on construction for whatever reason 
        ///</summary>
        public static void ConstructScript(UKScriptRuntime c)
        {
            // Globals
            c.runtime.Options.DebugPrint = (Action<string>)Debug.Log;
            c.runtime.Globals["gameObject"] = c.gameObject;
            c.runtime.Globals["transform"] = c.transform;

            // Statics
            foreach (var pair in luaStatics)
            {
                c.runtime.Globals[pair.Key] = pair.Value;
            }

            // Structs
            foreach(var structType in luaStructs)
            {
                c.runtime.Globals[structType.Name] = structType;
            }

            constructMethods?.Invoke(c);
        }

        ///<summary>
        /// Accepts a UKScriptRuntime and calls deconstructMethods on it, allowing all the API modules to access the scripts on destroy for whatever reason 
        ///</summary>
        public static void DestructScript(UKScriptRuntime c)
        {
            destructMethods?.Invoke(c);
        }

        ///<summary>
        /// Accepts a UKScriptRuntime and calls updateMethods on it, allowing all the API modules to access the scripts on update for whatever reason 
        ///</summary>
        public static void UpdateScript(UKScriptRuntime c)
        {
            updateMethods?.Invoke(c);
        }
    }
}
