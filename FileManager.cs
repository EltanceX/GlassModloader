using GlassLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GlassLoader
{
    public enum VersionTypes
    {
        Unknown,
        OriginalEdition,
        APIEdition
    }
    public class VersionInfo
    {
        public DirectoryInfo directoryInfo;
        public FileInfo fileInfo;
        public FileInfo ExecutableGame;
        public FileVersionInfo AssemblyVersion;
        public VersionTypes VersionType = VersionTypes.Unknown;
        public Assembly assembly;
        public VersionInfo(DirectoryInfo directoryInfo)
        {
            this.directoryInfo = directoryInfo;
            bool dllExists = Path.Exists(directoryInfo + "\\Survivalcraft.dll");
            bool exeExists = Path.Exists(directoryInfo + "\\Survivalcraft.exe");
            if (!dllExists && !exeExists)
            {
                throw new Exception($"Game Executable File Not Found! [{directoryInfo.Name}].\n Please check Naming Conventions: Survivalcraft.exe / Survivalcraft.dll");
            }
            string fileName = "Survivalcraft" + (dllExists ? ".dll" : ".exe");
            fileInfo = new FileInfo(directoryInfo + "\\" + fileName);
            if (!fileInfo.Exists)
            {
                throw new Exception($"Game Executable File Not Found! [{fileInfo.Name}].\n Please check Naming Conventions: Survivalcraft.exe / Survivalcraft.dll");
            }
            ExecutableGame = fileInfo;
            FileVersionInfo fileVerInfo = FileVersionInfo.GetVersionInfo(fileInfo.ToString());
            AssemblyVersion = fileVerInfo;
        }
    }
    public class GlobalConfig
    {
        public static string TargetVesrion = null;
        public static int LogLevel = 0;
    }

    public class FileManager
    {
        public static string CurrentDirectory = "";
        public static string DefaultDirectory = "";
        public static List<VersionInfo> VersionsDirectories = new List<VersionInfo>();
        public static string VersionsPath = "versions";
        public static string AssetsPath = "GML\\Assets";
        public static string ModAssetsPath = "ModAssets";
        public static string ModsPath = "GML\\Mods";
        public static string TempPath = "GML\\Temp";
        public static string GMLLogoPath = "GML\\Assets\\GML_LOGO.png";
        public static string GMLIconPath = "GML\\Assets\\SurvivalcraftGML.ico";
        public static string EnginePath = "Engine.dll";
        public static string ConfigPath = "GMLConfig.json";

        public static string AbsoluteModsPath { get { return DefaultDirectory + "\\" + ModsPath; } }
        public static string AbsoluteAssetsPath { get { return DefaultDirectory + "\\" + AssetsPath; } }
        public static string AbsoluteGMLIconPath { get { return DefaultDirectory + "\\" + GMLIconPath; } }
        public static string AbsoluteGMLLogoPath { get { return DefaultDirectory + "\\" + GMLLogoPath; } }

        public static string GMLPath(string relativePath)
        {
            return DefaultDirectory + "\\" + relativePath;
        }

        public static void Initialize()
        {
            FileManager.RecoverFiles();
            bool isAssetsIntegrity = FileManager.CheckAssetsIntegrity();

            CurrentDirectory = Directory.GetCurrentDirectory();
            DefaultDirectory = CurrentDirectory;
            new DirectoryInfo($"{CurrentDirectory}\\{VersionsPath}").Create();
            LoadConfig();
            GetVersions(VersionsDirectories);
        }

        public static Dictionary<string, List<FileInfo>> GetAssetsPath(ModInstance mod)
        {
            var Assets = new Dictionary<string, List<FileInfo>>();
            string assetsPath = Path.Combine(AbsoluteModsPath, mod.ModDirectory.Name, ModAssetsPath);
            DirectoryInfo AssetsDir = new DirectoryInfo(assetsPath);
            if (!AssetsDir.Exists) return Assets;

            void GetPath(DirectoryInfo dir, string FormatPath = "")
            {
                var files = dir.GetFiles();
                var directories = dir.GetDirectories();
                foreach (var file in files)
                {
                    string AssetsPath = FormatPath + file.Name.Substring(0, file.Name.Length - file.Extension.Length);
                    if (Assets.Keys.Contains(AssetsPath))
                    {
                        Assets[AssetsPath].Add(file);
                        continue;
                    }
                    Assets.Add(AssetsPath, new List<FileInfo>() { file });
                }
                foreach (var ChildDirectory in directories)
                {
                    GetPath(ChildDirectory, FormatPath == string.Empty ? ChildDirectory.Name + "/" : FormatPath + ChildDirectory.Name + "/");
                }
            }
            GetPath(AssetsDir);

            return Assets;
        }

        /// <summary>
        /// 获取模组配置
        /// </summary>
        /// <param name="modDirectory">模组文件夹</param>
        /// <returns>无文件或格式错误返回null</returns>
        public static ModConfig? LoadModConfig(DirectoryInfo modDirectory)
        {
            string manifestPath = modDirectory.FullName + "\\" + "manifest.json";
            var modConfig = new ModConfig();
            if (!File.Exists(manifestPath))
            {
                GLog.Warn($"Mod {modDirectory.Name} Doesn't contain any manifest file !");
                return null;
            }
            string originJsonText = File.ReadAllText(manifestPath);
            string JsonText = GUtil.RemoveJsonComments(originJsonText);
            JsonDocument json = null;
            JsonElement root;
            try
            {
                json = JsonDocument.Parse(JsonText);
                root = json.RootElement;
            }
            catch (Exception e)
            {
                GLog.Error(e);
                GLog.Error("Incorrect Manifest JSON Format !");
                return null;
            }

            try
            {
                modConfig.Priority = root.GetProperty("Priority").GetInt32();
            }
            catch (Exception e) { GLog.Warn($"Item Priority Not Found in manifest.json[{modDirectory.Name}]!"); }

            try
            {
                JsonElement TargetGameVersion = root.GetProperty("TargetGameVersion");
                string? Version = TargetGameVersion.GetProperty("Version").GetString();
                bool StrictMatch = TargetGameVersion.GetProperty("StrictMatch").GetBoolean();
                modConfig.TargetGameVersion = Version;
                modConfig.GameVersionStrictMode = StrictMatch;
            }
            catch(Exception e) { }

            try
            {
                JsonElement TargetGameDirectory = root.GetProperty("TargetGameDirectory");
                string? Name = TargetGameDirectory.GetProperty("Name").GetString();
                bool StrictMatch = TargetGameDirectory.GetProperty("StrictMatch").GetBoolean();
                modConfig.TargetGameDirectory = Name;
                modConfig.GameDirectoryStrictMode = StrictMatch;
            }
            catch (Exception e) { }

            try
            {
                modConfig.Author = root.GetProperty("Author").GetString();
            }
            catch (Exception e) { }

            try
            {
                JsonElement RuntimeCompilation = root.GetProperty("RuntimeCompilation");
                bool RuntimeCompilationEnabled = RuntimeCompilation.GetProperty("enabled").GetBoolean();
                if (RuntimeCompilationEnabled)
                {
                    JsonElement.ArrayEnumerator dependencies = RuntimeCompilation.GetProperty("dependencies").EnumerateArray();
                    List<string> Dependencies = new List<string>();
                    List<string> DependenciesShared = new List<string>();
                    foreach (var dependency in dependencies)
                    {
                        Dependencies.Add(dependency.GetString());
                    }
                    JsonElement.ArrayEnumerator dependencies_shared = RuntimeCompilation.GetProperty("dependencies_shared").EnumerateArray();
                    foreach (var dependency_shared in dependencies_shared)
                    {
                        DependenciesShared.Add(dependency_shared.GetString());
                    }
                    modConfig.Dependencies = Dependencies;
                    modConfig.DependenciesShared = DependenciesShared;
                    modConfig.RuntimeCompileEnabled = RuntimeCompilationEnabled;
                }
            }
            catch (Exception e)
            {
                GLog.Warn(e);
                GLog.Warn($"Loading Item RuntimeCompilation Error! at: manifest.json[{modDirectory.Name}]");
            }
            return modConfig;

        }
        public static void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                GLog.Warn("Config file missing !");
                return;
            }
            string jsontext = File.ReadAllText(ConfigPath, Encoding.UTF8);
            jsontext = GUtil.RemoveJsonComments(jsontext);
            JsonDocument json = null;
            JsonElement root;
            try
            {
                json = JsonDocument.Parse(jsontext);
                root = json.RootElement;
            }
            catch (Exception e)
            {
                GLog.Error(e);
                GLog.Error("Incorrect Config JSON Format !");
                return;
            }

            try
            {
                int LogLevel = root.GetProperty("LogLevel").GetInt32()!;
                GlobalConfig.LogLevel = LogLevel;
                GLog.GLogLevel = (GLogType)LogLevel;
            }
            catch (Exception ex) { GLog.Warn("Json Element 'LogLevel' Not Found !"); }

            try
            {
                string TargetVersion = root.GetProperty("TargetVersion").GetString()!;
                GlobalConfig.TargetVesrion = TargetVersion;
            }
            catch (Exception ex) { GLog.Warn("Json Element 'TargetVersion' Not Found !"); }

            try
            {
                JsonElement versionArray = root.GetProperty("GMLVersion");
                foreach (JsonElement version in versionArray.EnumerateArray())
                {
                    int versionNumber = version.GetInt32();
                }
            }
            catch (Exception ex) { GLog.Warn("Json Element 'GMLVersion' Not Found !"); }
        }
        public static void EnterVersionPath(string relativePath)
        {
            var a = Assembly.GetEntryAssembly()?.Location;
            string pathAfter = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            Directory.SetCurrentDirectory(pathAfter);
            CurrentDirectory = pathAfter;
        }
        public static void GetVersionDirectoryByName()
        {

        }
        public static void GetVersions(List<VersionInfo> versionsDirectories)
        {
            versionsDirectories.Clear();
            var dirs = new DirectoryInfo($"{CurrentDirectory}\\{VersionsPath}").GetDirectories();
            foreach (DirectoryInfo dir in dirs)
            {
                GLog.Info($"Loading Version: [{dir.Name}]");
                try
                {
                    versionsDirectories.Add(new VersionInfo(dir));
                }
                catch (Exception ex)
                {
                    GLog.Error(ex);
                    GLog.Error($"Failed to Load Version: [{dir.Name}]");
                }
            }
        }
        public static void RecoverFiles(string assetsPath = null, string modsPath = null, string tempPath = null)
        {
            assetsPath ??= AssetsPath;
            modsPath ??= ModsPath;
            tempPath ??= TempPath;
            DirectoryInfo Assets = new DirectoryInfo(assetsPath);
            Assets.Create();
            //FileInfo[] files = Assets.GetFiles();
            //DirectoryInfo[] dics = Assets.GetDirectories();
            DirectoryInfo Mods = new DirectoryInfo(modsPath);
            Mods.Create();

            DirectoryInfo TempDir = new DirectoryInfo(tempPath);
            TempDir.Create();
        }
        public static bool CheckAssetsIntegrity(string assetsPath = null)
        {
            assetsPath ??= AssetsPath;
            GLog.Info("Check Assets File integrity...");
            Dictionary<string, bool> PresetFiles = new Dictionary<string, bool>();
            PresetFiles.Add("GML_LOGO.png", false);
            PresetFiles.Add("SurvivalcraftGML.ico", false);


            DirectoryInfo Assets = new DirectoryInfo(AssetsPath);
            FileInfo[] files = Assets.GetFiles();
            foreach (FileInfo file in files)
            {
                try
                {
                    PresetFiles[file.Name] = true;
                }
                catch { }
            }
            foreach (var file in PresetFiles)
            {
                if (!file.Value)
                {
                    GLog.Error($"Missing File: {AssetsPath}\\{file.Key}");
                    return false;
                }
            }
            return true;
        }
    }

}
