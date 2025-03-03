using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace GlassLoader
{
    public class ModConfig
    {
        public bool RuntimeCompileEnabled = false;
        public List<string> Dependencies = new List<string>();
        public List<string> DependenciesShared = new List<string>();
        public int Priority = 1;
        public string Author = "Unknown";
        public bool Enabled = true;
        public string TargetGameVersion = null;
        public string TargetGameDirectory = null;
        public bool GameVersionStrictMode = false;
        public bool GameDirectoryStrictMode = false;
        public ModConfig()
        {

        }
    }
    public class ModListener
    {
        public MethodInfo Listener;
        public string Name;
        public int ParamLength;
        public ModListener(MethodInfo listener, string Name = null)
        {
            if (Name == null) Name = Guid.NewGuid().ToString();
            Listener = listener;
            ParamLength = listener.GetParameters().Length;
        }
    }
    public class ModPatchInfo
    {
        public string ID;
        public MethodBase Origin;
        public List<MethodInfo> Patches = new List<MethodInfo>();
        public ModPatchInfo(string ID, MethodBase origin, List<MethodInfo> patches)
        {
            this.ID = ID;
            this.Origin = origin;
            this.Patches = patches;
        }

    }
    public class ModInstance : IDisposable
    {
        public Assembly assembly;
        public Harmony harmony;
        public string UUID;
        public Type EventListeners;
        public ModConfig Config;
        public List<FileInfo> Dll = new List<FileInfo>();
        public List<FileInfo> Cs = new List<FileInfo>();
        public DirectoryInfo ModDirectory;
        public Dictionary<string, List<ModListener>> Listeners = new Dictionary<string, List<ModListener>>();
        public Dictionary<string, ModPatchInfo> ModPatches = new Dictionary<string, ModPatchInfo>();
        public void AddListener(string identifier, ModListener listener)
        {
            if (Listeners.ContainsKey(identifier)) Listeners[identifier].Add(listener);
            else Listeners.Add(identifier, new List<ModListener>() { listener });
        }
        public void RemoveListener(string identifier, ModListener listener)
        {
            foreach (var item in Listeners)
            {
                foreach (ModListener value in item.Value)
                {
                    if (value.Equals(listener)) item.Value.Remove(value);
                }
                if (item.Value.Count == 0) Listeners.Remove(item.Key);
            }
        }
        public void RemoveListener(string identifier, MethodInfo listener)
        {
            foreach (var item in Listeners)
            {
                foreach (ModListener value in item.Value)
                {
                    if (value.Listener.Equals(listener)) item.Value.Remove(value);
                }
                if (item.Value.Count == 0) Listeners.Remove(item.Key);
            }
        }
        public void RemoveListener(string identifier, string modListenerName)
        {
            foreach (var item in Listeners)
            {
                foreach (ModListener value in item.Value)
                {
                    if (value.Name == modListenerName) item.Value.Remove(value);
                }
                if (item.Value.Count == 0) Listeners.Remove(item.Key);
            }
        }
        public ModInstance(Assembly assembly = null, string UUID = null)
        {
            if (UUID == null) this.UUID = Guid.NewGuid().ToString();
            this.assembly = assembly;
            harmony = new Harmony($"com.GlassMod.{UUID}");
            //LoadListeners();
        }
        public void LoadListeners()
        {
            if (assembly == null)
            {
                var ex = new Exception("Mod Assembly equals null! Listeners loading failure");
                GLog.Warn(ex);
                return;
                throw ex;
            }
            EventListeners = assembly.GetTypes().Where(x => x.Name == "EventListeners").FirstOrDefault();
            if (EventListeners == null) GLog.Warn("EventListeners Not Found in Mod: " + assembly.GetName());
            else
            {
                var methods = EventListeners.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (MethodInfo method in methods)
                {
                    try
                    {
                        if (Listeners.Keys.Contains(method.Name)) Listeners[method.Name].Add(new ModListener(method));
                        else Listeners.Add(method.Name, new List<ModListener> { new ModListener(method) });
                    }
                    catch (Exception ex)
                    {
                        GLog.Warn(ex);
                        continue;
                    }
                }
            }
        }
        public void RemoveGlobalPatch(string HarmonyID)
        {
            Harmony.UnpatchID(HarmonyID);
        }
        public void UnPatch(MethodBase original, MethodInfo patch)
        {
            harmony.Unpatch(original: original, patch: patch);
        }
        public void UnpatchSelfAll()
        {
            harmony.UnpatchSelf();
        }
        public void UnPatch(ModPatchInfo patch)
        {
            foreach (var methods in patch.Patches)
            {
                harmony.Unpatch(patch.Origin, methods);
            }
            if(ModPatches.Keys.Contains(patch.ID)) ModPatches.Remove(patch.ID);
        }
        public void UnPatch(string PatchID)
        {
            ModPatchInfo patch;
            if (ModPatches.Keys.Contains(PatchID))
            {
                patch = ModPatches[PatchID];
            }
            else
            {
                throw new Exception($"PatchID [{PatchID}] Not Found!");
            }
            foreach (var methods in patch.Patches)
            {
                harmony.Unpatch(patch.Origin, methods);
            }
            ModPatches.Remove(PatchID);
        }


        public ModPatchInfo? Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null,
            MethodInfo ilmanipulator = null, bool ThrowOnError = true)
        {
            HarmonyMethod harmonyPrefix = prefix != null ? new HarmonyMethod(prefix) : null;
            HarmonyMethod harmonyPostfix = postfix != null ? new HarmonyMethod(postfix) : null;
            HarmonyMethod harmonyTranspiler = transpiler != null ? new HarmonyMethod(transpiler) : null;
            HarmonyMethod harmonyFinalizer = finalizer != null ? new HarmonyMethod(finalizer) : null;
            HarmonyMethod harmonyILManipulator = ilmanipulator != null ? new HarmonyMethod(ilmanipulator) : null;
            try
            {
                harmony.Patch(original, harmonyPrefix, harmonyPostfix, harmonyTranspiler, harmonyFinalizer, harmonyILManipulator);
                string PatchID = Guid.NewGuid().ToString();
                List<MethodInfo> ModPatches = new List<MethodInfo>();
                if (prefix != null) ModPatches.Add(prefix);
                if (postfix != null) ModPatches.Add(postfix);
                if (transpiler != null) ModPatches.Add(transpiler);
                if (finalizer != null) ModPatches.Add(finalizer);
                if (ilmanipulator != null) ModPatches.Add(ilmanipulator);
                ModPatchInfo modPatchInfo = new ModPatchInfo(PatchID, original, ModPatches);
                return modPatchInfo;
            }
            catch (Exception ex)
            {
                if (ThrowOnError)
                {
                    throw ex;
                }
                try
                {
                    var unpatch = prefix ?? postfix ?? transpiler ?? finalizer ?? ilmanipulator;
                    harmony.Unpatch(original, unpatch);
                }
                catch (Exception ex2)
                {
                    GLog.Error(new Exception($"Automatic Unpatch [{original.Name}] Failed."));
                    //GUtil.ErrorTerminate();
                    try
                    {
                        harmony.UnpatchSelf();
                    }
                    catch (Exception ex3)
                    {
                        GLog.Fatal("Unpatch Self Failed! Unexpected Error may occurr!");
                        throw ex3;

                    }
                }
            }
            return null;
        }

        public void Dispose()
        {
            harmony?.UnpatchSelf();

        }
    }
}
