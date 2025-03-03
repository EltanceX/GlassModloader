using GlassLoader;
using HarmonyLib;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace GlassLoader
{
    // HarmonyX Patch（使用 Transpiler 替换 Log.Information）
    public static class Patch_Log
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method &&
                    method.Name == "Information" && method.DeclaringType == GModsManager.ClassAssemblys["Engine"].GetType("Engine.Log"))
                {
                    GLog.Info($"IL: {codes[i]}");
                    // 替换方法
                    codes[i].operand = typeof(Patch_Log).GetMethod(nameof(CustomLog), BindingFlags.Static | BindingFlags.Public);
                }
            }
            return codes;
        }

        public static void CustomLog(string message)
        {
            Console.WriteLine("[Program -> Initialize]：" + message);
        }
    }
    /// <summary>
    /// IL Modify
    /// </summary>
    public static class Patch_EntityLoad
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var cpt = typeof(List<>).MakeGenericType(typeof(KeyValuePair<,>).MakeGenericType(typeof(int), GModsManager.ClassTypes["GameEntitySystem.Component"]));
            for (int i = 0; i < codes.Count; i++)
            {
                //if (codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo method &&
                //    method.Name == "Information" && method.DeclaringType == GModsManager.ClassAssemblys["Engine"].GetType("Engine.Log"))
                //{
                //    GLog.Info($"<EntityLoad> IL: {codes[i]}");
                //    // 替换方法
                //    codes[i].operand = typeof(Patch_Log).GetMethod(nameof(CustomLog), BindingFlags.Static | BindingFlags.Public);
                //}
                if (codes[i].opcode == OpCodes.Newobj && codes[i].operand is ConstructorInfo ctor && ctor.DeclaringType == cpt)
                {
                    //Debugger.Break();
                    //list 局部索引
                    int listVarIndex = -1;
                    for (int j = i + 1; j < codes.Count; j++)
                    {

                        if (codes[j].opcode == OpCodes.Stloc) // stloc <index>
                        {
                            listVarIndex = (int)codes[j].operand;
                            break;
                        }
                        else if (codes[j].opcode == OpCodes.Stloc_0) // stloc_0, stloc_1, stloc_2, stloc_3
                        {
                            listVarIndex = 0; // Stloc_0 -> index 0
                            break;
                        }
                        else if (codes[j].opcode == OpCodes.Stloc_1)
                        {
                            listVarIndex = 1; // Stloc_1 -> index 1
                            break;
                        }
                        else if (codes[j].opcode == OpCodes.Stloc_2)
                        {
                            listVarIndex = 2; // Stloc_2 -> index 2
                            break;
                        }
                        else if (codes[j].opcode == OpCodes.Stloc_3)
                        {
                            listVarIndex = 3; // Stloc_3 -> index 3
                            break;
                        }
                    }
                    int thisVarIndex = 0; // this构造里通常索引为0

                    if (listVarIndex >= 0)
                    {
                        var loadThis = new CodeInstruction(OpCodes.Ldarg_0); // this
                        var loadList = new CodeInstruction(OpCodes.Ldloc, listVarIndex);
                        var valuesDictionary = new CodeInstruction(OpCodes.Ldarg_2);
                        //var loadList = new CodeInstruction(OpCodes.Ldloc_1);
                        var callA = new CodeInstruction(OpCodes.Call, typeof(GamePatch).GetMethod("HandleOnEntityCtor"));

                        // stloc后插入
                        codes.Insert(i + 2, loadThis);
                        codes.Insert(i + 3, loadList);
                        codes.Insert(i + 4, valuesDictionary);
                        codes.Insert(i + 5, callA); // 调用A
                    }
                    //Debugger.Break();
                    break;
                }
            }
            return codes;
        }

        public static void CustomLog(string message)
        {
            Console.WriteLine("[Program -> Initialize]：" + message);
        }
    }
    public static class OfficialEditionPatch
    {

        /// <summary>
        /// 处理资源加载，加载模组自定义资源
        /// </summary>
        /// <returns>是否中断原版资源加载</returns>
        public static bool HandleRootGet(string name, bool throwIfNotFound, ref object __result)
        {
            //if (name == "Fonts/Pericles32")
            //if (name.Contains("Pericles"))
            //{
            //    //Debugger.Break();

            //    var FontTex = ContentLoader.GetDirect("GlassFont/PericlesTexture");
            //    var FontLst = ContentLoader.GetDirect("GlassFont/Pericles");
            //    if (FontLst.Content == null)
            //    {
            //        var BmpFont = ContentLoader.GetBitmapFont(FontTex, FontLst);
            //        FontLst.Content = BmpFont;
            //    }
            //    __result = FontLst.Content;
            //    return false;
            //}

            //模组资源事件
            object[] param = new object[] { name, __result };
            GModsManager.SpreadEventsToAllMods("HandleRootGet", param);
            object ModResult = param[1];
            if (ModResult != null)
            {
                __result = ModResult;
                return false;
            }

            //自定义资源
            if (ContentLoader.GlobalDescriptions.Keys.Contains(name))
            {
                //Debugger.Break();

                //GLog.Debug($"Assets {name} is taking effect.");
                object ModAsset = null;
                try { ModAsset = ContentLoader.Get(name); }
                catch (Exception ex) { GLog.Error(ex); GLog.Warn($"An error occurred while loading Mod Asstes: {name}, GML will attempt to use Original Function..."); }
                if (ModAsset != null)
                {
                    __result = ModAsset;
                    return false;
                }
            }
            return true;
        }

        //public static bool HandleLabelTextSet(ref string value, object __instance)
        //{
        //    //Stopwatch sw = Stopwatch.StartNew();
        //    //sw.Start();
        //    //var origin = ModAccessUtil.GetFieldOnceSlow(__instance.GetType(), __instance, "m_text");
        //    //GLog.Info("ori" + origin);
        //    //sw.Stop();
        //    //GLog.Info($"语句执行耗时(微秒): {sw.Elapsed.Microseconds}");
        //    //GLog.Info($"语句执行耗时(毫秒): {ps.runningTime}");
        //    return true;
        //}


        public static bool FirstContentInitialize = true;
        public static void ContentCache_SetContentDescription(string name, object contentDescription)
        {
            //Debugger.Break();
            GLog.Info($"- ContentPatch: {name} {contentDescription.GetType().GetField("TypeName").GetValue(contentDescription)}");
            //FileStream fs = new FileStream("C:\\Users\\EltanceX\\Desktop\\Blocks.png", FileMode.Open);
            //MemoryStream ms = new MemoryStream();
            //fs.CopyTo(ms);
            //fs.Close();
            //fs.Dispose();

            //Type ContentDescription = GModsManager.ClassTypes["Engine.Content.ContentCache.ContentDescription"];
            //Type ContentStream = GModsManager.ClassTypes["Engine.Content.ContentStream"];
            //MethodInfo SetContentDescription = GModsManager.MethodInfos["SetContentDescription"];

            //object description = Activator.CreateInstance(ContentDescription);
            //object contentStreamInstance = Activator.CreateInstance(ContentStream, () => ms);
            //Type contentStreamType = ContentStream.GetType();
            //Type t = description.GetType();

            //string AssetsName = "Textures/GML/Test";
            //t.GetField("Name").SetValue(description, "Textures/GML/Test");
            //t.GetField("TypeName").SetValue(description, "Engine.Graphics.Texture2D");
            //t.GetField("Stream").SetValue(description, contentStreamInstance);
            //t.GetField("Position").SetValue(description, 0L);
            //t.GetField("BytesCount").SetValue(description, ms.Length);

            //SetContentDescription.Invoke(null, [AssetsName, description]);
            //Cannot get pad?
            //contentStreamType.GetField("Pad").SetValue(contentStreamType, null);
        }
    }
    public static class APIPatch
    {
        public static void HandleLoadEntities(object entityDataList,object entityList, object __instance)
        {
            GModsManager.SpreadEventsToAllMods("OnAfterLoadEntities", __instance, entityDataList, entityList);

        }
    }
    public static class GamePatch
    {
        public static void HandleCreateEntity(object valuesDictionary, object __instance, ref object __result)
        {
            GModsManager.SpreadEventsToAllMods("OnAfterCreateEntity", __instance, valuesDictionary, __result);
        }
        public static void HandleLoadEntities(object entityDataList, object __instance, ref object __result)
        {
            GModsManager.SpreadEventsToAllMods("OnAfterLoadEntities", __instance, entityDataList, __result);

        }
        public static void HandleGetAppDirectory(ref object __result)
        {
            //修改入口dll路径
            var loc = Glass.CurrentVersion.directoryInfo.FullName;
            __result = loc;
        }
        public static void HandleProjectCtor(object gameDatabase, object projectData, object __instance)
        {
            //Debugger.Break();
            GModsManager.SpreadEventsToAllMods("OnAfterProjectCtor", gameDatabase, projectData, __instance);

        }
        /// <summary>
        /// API Version
        /// </summary>
        public static void HandleEntityLoad(object entityDataList, object entityList, object __instance)
        {
            Debugger.Break();
            GModsManager.SpreadEventsToAllMods("OnEntityLoad", entityDataList, entityList, __instance);
        }
        /// <summary>
        /// 2.4 Version
        /// </summary>
        public static void HandleEntityLoad2(object entityDataList, object __instance)
        {
            //Debugger.Break();
            GModsManager.SpreadEventsToAllMods("OnEntityLoad", entityDataList, null, __instance);
        }
        public static void HandleOnEntityCtor(object entity, object componentList, object valuesDictionary)
        {
            //Debugger.Break();
            //GLog.Info(entity);
            //GLog.Info(componentList);
            GModsManager.SpreadEventsToAllMods("OnEntityCtor", entity, componentList, valuesDictionary);
        }
        public static void SpreadOnAfterEntityCtor(object __instance, object project, object valuesDictionary)
        {
            //__instance: Entity
            GModsManager.SpreadEventsToAllMods("OnAfterEntityCtor", __instance, project, valuesDictionary);
        }
        public static bool WindowTitleSet(ref string value)
        {
            GLog.Info($"Window Title Modification Behavior intercepted: {value}");
            if (value.ToLower().Contains("gml")) return true;
            return false;
        }
        public static void GMLLoading()
        {
            GModsManager.SpreadEventsToAllModsWithUUID("OnBeforeGameLoading", typeof(GModsManager), typeof(GUtil));
        }
        public static void EntryPrefix()
        {
            GLog.Debug("SurvivalCraft Entry Preload");

            return;
        }
        public static void AfterEntryPoint()
        {
            Console.WriteLine("AfterEntryPoint");
        }
        public static void GameInit()
        {
            GModsManager.SpreadEventsToAllModsWithUUID("GML_Main", Glass.CurrentVersion.AssemblyVersion.FileVersion ?? "Unknown");
        }

        [Obsolete]
        public static bool PrefixTest()
        {
            return false; // 返回 false 阻止原方法执行
        }

    }
}
