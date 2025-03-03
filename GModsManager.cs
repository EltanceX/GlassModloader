using HarmonyLib;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GlassLoader
{
    public class GModsManager
    {
        public static Dictionary<string, Assembly> ClassAssemblys = new Dictionary<string, Assembly>();
        public static Dictionary<string, Type> ClassTypes = new Dictionary<string, Type>();
        public static Dictionary<string, MethodInfo> MethodInfos = new Dictionary<string, MethodInfo>();
        public static List<ModInstance> ModList;

        //public static List<string> EventIdentifiers = new List<string>() {
        //    "OnAfterProjectCtor", "OnEntityLoad", "OnEntityAdded", "OnBeforeGameLoading", "GML_Main", "OnGameInitializeBegin", "OnGameLoadingStart"
        //};

        public static void Initialize()
        {
            //foreach (var identifier in EventIdentifiers)
            //{
            //    Listeners.Add(identifier, new List<ModListener>());
            //}
        }
        public static ModInstance? GetModInstance(string UUID)
        {
            return Glass.ModList.Where(x => x.UUID == UUID).FirstOrDefault();
        }
        public static void RemoveUserHook(string ID)
        {
            Harmony.UnpatchID(ID);
        }

        /// <summary>
        /// 检查模组配置与版本
        /// </summary>
        /// <returns>是否继续加载</returns>
        public static bool MatchVersion(VersionInfo version, ModConfig modConfig, DirectoryInfo ModDirectory)
        {
            if (modConfig.TargetGameVersion != null && modConfig.TargetGameVersion != string.Empty)
            {
                if (version.AssemblyVersion.FileVersion != modConfig.TargetGameVersion)
                {
                    string message = $"Required Game Version Not Matched! Target:[{modConfig.TargetGameVersion}] Loaded:[{version.AssemblyVersion.FileVersion}] Mod:[{ModDirectory.Name}]";
                    if (modConfig.GameVersionStrictMode)
                    {
                        GLog.Error(message);
                        return false;
                    }
                    else GLog.Warn(message);
                }
            }
            if (modConfig.TargetGameDirectory != null && modConfig.TargetGameDirectory != string.Empty)
            {
                if (ModDirectory.Name != modConfig.TargetGameDirectory)
                {
                    string message = $"Required Game Directory Name Not Matched! Target:[{modConfig.TargetGameDirectory}] Loaded:[{version.directoryInfo.Name}] Mod:[{ModDirectory.Name}]";
                    if (modConfig.GameDirectoryStrictMode)
                    {
                        GLog.Error(message);
                        return false;
                    }
                    else GLog.Warn(message);
                }
            }
            return true;
        }
        public static void LoadModsToAssembly(string ModPath, List<ModInstance> modList)
        {
            DirectoryInfo modPathDirectoryInfo = new DirectoryInfo(ModPath);
            DirectoryInfo[] modDirectories;
            try
            {
                //Dictionary<DirectoryInfo, (List<FileInfo>, List<FileInfo>)> modInfo = new Dictionary<DirectoryInfo, (List<FileInfo>, List<FileInfo>)>();
                List<ModInstance> ModInstances = new List<ModInstance>();

                modDirectories = modPathDirectoryInfo.GetDirectories();
                foreach (var modDir in modDirectories)
                {
                    ModConfig modConfig = FileManager.LoadModConfig(modDir);
                    //if (modConfig == null) GLog.Warn("Failed to find config file for mod: " + modDir.Name);
                    modConfig ??= new ModConfig();
                    if (!modConfig.Enabled) continue;
                    bool Loadable = MatchVersion(Glass.CurrentVersion, modConfig, modDir);
                    if (!Loadable)
                    {
                        GLog.Error($"The current version information does not meet the module requirements, mod [{modDir.Name}] failed to load!");
                        continue;
                    }

                    ModInstance modInstance = new ModInstance()
                    {
                        ModDirectory = modDir,
                        Config = modConfig
                    };

                    List<FileInfo> cs = new List<FileInfo>();
                    List<FileInfo> dll = new List<FileInfo>();
                    foreach (FileInfo mod in modDir.GetFiles())
                    {

                        if (mod.Extension.ToLower() == ".dll") dll.Add(mod);
                        if (mod.Extension.ToLower() == ".cs") cs.Add(mod);
                        //if (mod.Extension.ToLower() != ".dll") continue;
                        ////string assemblyPath = ModPath + "\\" + mod.Name;
                        ////绝对路径
                        ////string assemblyPath = mod.DirectoryName + "\\" + mod.Name;
                        //string assemblyPath = mod.ToString();
                        //try
                        //{
                        //    Assembly assembly = FileManager.LoadModAssembly(modDir, mod);
                        //    Assembly modassembly = Assembly.LoadFrom(assemblyPath);
                        //    modList.Add(new ModInstance(modassembly));
                        //}
                        //catch (Exception ex)
                        //{
                        //    GLog.Error("An Error Occurred while loading mod: " + assemblyPath);
                        //    GLog.Error(ex);
                        //    continue;
                        //}
                    }
                    modInstance.Cs = cs;
                    modInstance.Dll = dll;
                    ModInstances.Add(modInstance);
                }

                ModInstances.Sort((a, b) => a.Config.Priority.CompareTo(b.Config.Priority));

                foreach (ModInstance modInstance in ModInstances)
                {
                    var ModConfig = modInstance.Config;
                    if (ModConfig == null || !ModConfig.RuntimeCompileEnabled)
                    {
                        GLog.Info($"Loading Mod: {modInstance.ModDirectory.Name} [UUID: {modInstance.UUID}]");
                        Assembly LastAssembly = null;
                        foreach (FileInfo mod in modInstance.Dll)
                        {
                            string assemblyPath = mod.ToString();
                            try
                            {
                                Assembly ModAssembly = Assembly.LoadFrom(assemblyPath);
                                LastAssembly = ModAssembly;
                                //ModInstance ins = new ModInstance(ModAssembly);
                                //var ModMain = ModAssembly.GetTypes();
                                var isMainAssembly = ModAssembly.GetTypes().Where(x => x.Name == "EventListeners").FirstOrDefault();
                                if (isMainAssembly != null) modInstance.assembly = ModAssembly;
                            }
                            catch (Exception ex)
                            {
                                GLog.Error("An Error Occurred while loading mod: " + assemblyPath);
                                GLog.Error(ex);
                                continue;
                            }
                        }
                        if (modInstance.assembly == null) modInstance.assembly = LastAssembly;
                        modInstance.LoadListeners();
                        modList.Add(modInstance);
                        //string assemblyPath = modFile.ToString();
                        //return Assembly.LoadFrom(assemblyPath);
                        continue;
                    }

                    GLog.Info($"Runtime Compiling: {modInstance.ModDirectory.Name} [UUID: {modInstance.UUID}]");
                    LoadingForm.UpdateLoadingData($"Runtime Compiling: {modInstance.ModDirectory.Name} [UUID: {modInstance.UUID}]", 0);

                    try
                    {
                        //运行时编译
                        var sharedDeps = RuntimeCompiler.GetSharedDependencies(ModConfig.DependenciesShared);
                        var localDeps = RuntimeCompiler.GetLocalDependencies(ModConfig.Dependencies);
                        var sources = RuntimeCompiler.GetCsSources(modInstance.Cs);

                        List<MetadataReference> deps = new List<MetadataReference>();
                        deps.AddRange(sharedDeps);
                        deps.AddRange(localDeps);
                        Assembly ModAssemblyGen = RuntimeCompiler.CompileToAssembly(sources, deps, $"RuntimeMod_{modInstance.ModDirectory.Name}");
                        if (ModAssemblyGen == null)
                        {
                            GLog.Error($"RuntimeCompiler Error Occurred while loading mod: {modInstance.ModDirectory.Name}");
                            return;
                        }
                        modInstance.assembly = ModAssemblyGen;
                        modInstance.LoadListeners();
                        modList.Add(modInstance);

                    }
                    catch (Exception ex)
                    {
                        GLog.Error(ex);
                        GLog.Error($"RuntimeCompiler Error Occurred while loading mod: {modInstance.ModDirectory.Name}");
                        return;
                    }

                }
            }
            catch (Exception ex)
            {
                GLog.Error(ex);
                GLog.Error("Fail to get mods directory.");
                GUtil.ErrorTerminate();
            }

        }

        public static void SpreadEventsToAllMods(string targetModMethod, params object[] param)
        {
            foreach (ModInstance mod in Glass.ModList)
            {
                var Listeners = mod.Listeners;
                if (Listeners.ContainsKey(targetModMethod))
                {
                    var listeners = Listeners[targetModMethod];
                    foreach (ModListener listener in listeners)
                    {
                        try
                        {
                            if (listener.ParamLength == param.Length)
                                listener.Listener.Invoke(null, param);
                            else
                            {
                                var MatchedLengthParam = param.Take(listener.ParamLength).ToArray<object>();
                                listener.Listener.Invoke(null, MatchedLengthParam);
                            }
                        }
                        catch (Exception ex)
                        {
                            GLog.Warn(ex);
                            GLog.Warn($"Error while spreading events: {targetModMethod}");
                        }
                    }
                }
            }
            //return null;
        }
        public static void SpreadEventsToAllModsWithUUID(string targetModMethod, params object[] param)
        {
            foreach (ModInstance mod in Glass.ModList)
            {
                var Listeners = mod.Listeners;
                if (Listeners.ContainsKey(targetModMethod))
                {
                    var listeners = Listeners[targetModMethod];
                    foreach (ModListener listener in listeners)
                    {
                        try
                        {
                            object[] concat = new object[param.Length + 1];
                            concat[0] = mod.UUID;
                            //Array.Copy(a, 0, concat, 0, a.Length);
                            Array.Copy(param, 0, concat, 1, param.Length);
                            //return listener.Listener.Invoke(null, concat);

                            if (listener.ParamLength == concat.Length)
                                listener.Listener.Invoke(null, concat);
                            else
                            {
                                var MatchedLengthParam = concat.Take(listener.ParamLength).ToArray<object>();
                                listener.Listener.Invoke(null, MatchedLengthParam);
                            }
                        }
                        catch (Exception ex)
                        {
                            GLog.Warn(ex);
                            GLog.Warn($"Error while spreading events: {targetModMethod}");
                        }
                    }
                }
            }
            //return null;
        }
        public static MethodInfo BasicPatch(MethodInfo gameMethod, MethodInfo patchMethod, Harmony harmony = null)
        {
            return (harmony ?? new Harmony(patchMethod.Name)).Patch(gameMethod, new HarmonyMethod(patchMethod));
        }
        public static MethodInfo? TryPatch(Harmony harmony, MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null,
            MethodInfo ilmanipulator = null, bool printError = false)
        {
            HarmonyMethod harmonyPrefix = prefix != null ? new HarmonyMethod(prefix) : null;
            HarmonyMethod harmonyPostfix = postfix != null ? new HarmonyMethod(postfix) : null;
            HarmonyMethod harmonyTranspiler = transpiler != null ? new HarmonyMethod(transpiler) : null;
            HarmonyMethod harmonyFinalizer = finalizer != null ? new HarmonyMethod(finalizer) : null;
            HarmonyMethod harmonyILManipulator = ilmanipulator != null ? new HarmonyMethod(ilmanipulator) : null;
            try
            {
                return harmony.Patch(original, harmonyPrefix, harmonyPostfix, harmonyTranspiler, harmonyFinalizer, harmonyILManipulator);
            }
            catch (Exception ex)
            {
                if (printError)
                {
                    GLog.Warn(ex);
                    GLog.Warn($"Patch [{original.Name}] Failed. Function will lose efficacy");
                }
                try
                {
                    var unpatch = prefix ?? postfix ?? transpiler ?? finalizer ?? ilmanipulator;
                    harmony.Unpatch(original, unpatch);
                }
                catch (Exception ex2)
                {
                    GLog.Fatal(new Exception($"Automatic Unpatch [{original.Name}] Failed."));
                    GUtil.ErrorTerminate();
                }
            }
            return null;
        }
    }
}
