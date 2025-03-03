//@2025 EltanceX
//annularwind@outlook.com

using HarmonyLib;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace GlassLoader;


public class Glass
{
    public static VersionInfo CurrentVersion = null;
    public static List<ModInstance> ModList = new List<ModInstance>();
    public static Harmony GHarmony = new Harmony("com.glass.modloader");
    public static int[] GMLVersion = new int[] { 0, 1 };
    public static bool ExecuteWithoutPatch = false;

    //[STAThread] // 确保 WinForms 运行在单线程单元 (STA) 模式下
    static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine(@"
   ________                __                    __         
  / ____/ /___ ___________/ /   ____  ____ _____/ /__  _____
 / / __/ / __ `/ ___/ ___/ /   / __ \/ __ `/ __  / _ \/ ___/
/ /_/ / / /_/ (__  |__  ) /___/ /_/ / /_/ / /_/ /  __/ /    
\____/_/\__,_/____/____/_____/\____/\__,_/\__,_/\___/_/     
                                                            ");
        Console.ResetColor();
        GLog.Info($"Glass ModLoader: Version {GMLVersion[0]}.{GMLVersion[1]}");
        GLog.Info("Supported Survivalcraft Game Version: 2.4.0.0");
        GLog.Info("EltanceX [annularwind@outlook.com]");
        GLog.Info("");


        FileManager.Initialize();
        GModsManager.Initialize();
        GLog.Display("LogLevel: " + GLog.GLogLevel);
        //return;

        // WinForms线程
        Thread uiThread = new Thread(LoadingForm.CreateForm);
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();




        if (FileManager.VersionsDirectories.Count == 0)
        {
            GLog.Fatal("No Game Versions Found.");
            GUtil.ErrorTerminate();
        }
        CurrentVersion = FileManager.VersionsDirectories[0];
        if (GlobalConfig.TargetVesrion != null)
        {
            var target = FileManager.VersionsDirectories.Where(x => x.directoryInfo.Name == GlobalConfig.TargetVesrion).FirstOrDefault();
            if (target == null)
            {
                GLog.Warn($"Required Version '{GlobalConfig.TargetVesrion}'({FileManager.ConfigPath}) not found in Path:{FileManager.VersionsPath}");
                GLog.Warn($"Automatically selecting the FIRST executable version...");
            }
            else
            {
                CurrentVersion = target;
            }
        }
        //CurrentVersion = VersionGame;

        Assembly game;
        Assembly engine;
        Assembly gameEntitySystem = null;
        try
        {
            //if (Path.Exists(GamePathAPI))
            //    game = Assembly.LoadFrom(GamePathAPI);
            FileManager.EnterVersionPath(FileManager.VersionsPath + "\\" + CurrentVersion.directoryInfo.Name);
            game = Assembly.LoadFrom(CurrentVersion.ExecutableGame.ToString());
            CurrentVersion.assembly = game;

            GModsManager.ClassAssemblys.Add("Game", game);
            engine = Assembly.LoadFrom(FileManager.EnginePath);
            GModsManager.ClassAssemblys.Add("Engine", engine);
            //var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (Path.Exists("EntitySystem.Android.Net.dll")) gameEntitySystem = Assembly.LoadFrom("EntitySystem.Android.Net.dll");
            if (Path.Exists("EntitySystem.dll")) gameEntitySystem = Assembly.LoadFrom("EntitySystem.dll");

            LoadingForm.UpdateLoadingData("Loading Game program...");
        }
        catch (Exception ex)
        {
            GLog.Error(ex);
            LoadingForm.UpdateLoadingData("Loading Filed: " + ex.Message);
            GUtil.ErrorTerminate();
            return;
        }

        LoadingForm.UpdateLoadingData("Getting Game Version...");
        //FileVersionInfo fileInfo = FileVersionInfo.GetVersionInfo(GamePathRuntime);
        //GameVersion = fileInfo.FileVersion ?? GameVersion;
        GLog.Info($"Game Version: {CurrentVersion.AssemblyVersion.FileVersion}");



        //加载模组
        LoadingForm.UpdateLoadingData("Adding Mods...");
        GModsManager.LoadModsToAssembly(FileManager.AbsoluteModsPath, ModList);
        GModsManager.ModList = ModList;
        GamePatch.GMLLoading();




        Type program = game.GetType("Game.Program");
        Type engine_Window = engine.GetType("Engine.Window");
        Type engine_Storage = engine.GetType("Engine.Storage");
        //var a = game.GetTypes().Where(x=>x.Name.Contains(""));
        Type gameEntitySystem_Entity = gameEntitySystem.GetType("GameEntitySystem.Entity");
        Type gameEntitySystem_Project = gameEntitySystem.GetType("GameEntitySystem.Project");
        Type gameEntitySystem_Component = gameEntitySystem.GetType("GameEntitySystem.Component");

        GUtil.NullVerification(engine_Window);
        GUtil.NullVerification(engine_Storage);

        GModsManager.ClassTypes.Add("Game.Program", program);
        GModsManager.ClassTypes.Add("Engine.Window", engine_Window);
        GModsManager.ClassTypes.Add("GameEntitySystem.Entity", gameEntitySystem_Entity);
        GModsManager.ClassTypes.Add("GameEntitySystem.Project", gameEntitySystem_Project);
        GModsManager.ClassTypes.Add("GameEntitySystem.Component", gameEntitySystem_Component);
        if (program == null || gameEntitySystem_Entity == null || gameEntitySystem_Project == null || gameEntitySystem_Component == null)
        {
            GLog.Error("Failed to find Game/Engine Entry");
            GUtil.ErrorTerminate();
            return;
        }


        MethodInfo GameEntry_Method = program.GetMethod("EntryPoint") ?? program.GetMethod("Main");
        if (ExecuteWithoutPatch) goto entry;
        MethodInfo GameInit_Method = program.GetRuntimeMethod("Initialize", Type.EmptyTypes) ?? program.GetRuntimeMethods().Where(mi => mi.Name == "Initialize").FirstOrDefault();
        //MethodInfo Entity_InteralLoad = gameEntitySystem_Entity.GetRuntimeMethods().Where(x => x.Name == "InternalLoadEntity").FirstOrDefault();
        //MethodInfo Project_EntityLoad = gameEntitySystem_Project.GetRuntimeMethods().Where(x => x.Name == "LoadEntities").FirstOrDefault();
        MethodInfo Project_EntityLoad = GUtil.GetNonPublicMethod(gameEntitySystem_Project, "LoadEntities");
        MethodInfo Project_CreateEntity = GUtil.GetMethodByName(gameEntitySystem_Project, "CreateEntity");
        MethodInfo Project_LoadEntities = GUtil.GetMethodByName(gameEntitySystem_Project, "LoadEntities");
        ConstructorInfo Project_Ctor = GUtil.GetFirstConstructor(gameEntitySystem_Project);
        MethodInfo GetAppDirectory_Method = GUtil.GetNonPublicMethod(engine_Storage, "GetAppDirectory");
        MethodInfo GetDataDirectory_Method = GUtil.GetNonPublicMethod(engine_Storage, "GetDataDirectory");

        GUtil.NullVerification(GameEntry_Method, true);
        GUtil.NullVerification(GameInit_Method);
        GUtil.NullVerification(Project_EntityLoad);
        GUtil.NullVerification(GetAppDirectory_Method);
        GUtil.NullVerification(GetDataDirectory_Method);
        GUtil.NullVerification(Project_Ctor);









        LoadingForm.UpdateLoadingData("Injecting init method...");
        //游戏初始化
        GModsManager.TryPatch(GHarmony, GameInit_Method, GUtil.GetMethodByName(typeof(GamePatch), nameof(GamePatch.GameInit)), printError: true);

        GModsManager.TryPatch(GHarmony, GetAppDirectory_Method, postfix: GUtil.GetMethodByName(typeof(GamePatch), nameof(GamePatch.HandleGetAppDirectory)), printError: true);


        PatchWindowTitle(GHarmony, engine_Window);

        LoadingForm.UpdateLoadingData("Injecting Game Entry Hooks...");
        //harmony0.Patch(GameEntry_Method, GUtil.GetMethodByName(typeof(GamePatch), "EntryPrefix"));
        //游戏入口前缀
        GModsManager.TryPatch(GHarmony, GameEntry_Method, GUtil.GetMethodByName(typeof(GamePatch), nameof(GamePatch.EntryPrefix)), printError: true);

        LoadingForm.UpdateLoadingData("Injecting Entity Hooks...");
        PatchEntityLoad(GHarmony, Project_EntityLoad);


        //MethodInfo MainMethod2 = program.GetMethod("EntryPoint");

        //初始化日志
        PatchInitLog(GHarmony, GameInit_Method);

        //Entity构造
        PatchEntityCotr(GHarmony, gameEntitySystem_Entity);

        //原版资源加载器
        PatchContent(GHarmony);

        PatchProject(GHarmony, Project_Ctor, Project_CreateEntity, Project_LoadEntities);




        LoadingForm.UpdateLoadingData("Finished");
        Thread.Sleep(300);
        LoadingForm.LoadingFinished();
    entry:
        GameEntry_Method?.Invoke(null, null);

        GLog.Info("Process End.");
        //GLog.Info("Press any key to exit.");
        //Console.ReadKey();
    }










    public static void PatchContent(Harmony harmony)
    {
        Assembly engine = GModsManager.ClassAssemblys["Engine"];
        Assembly game = GModsManager.ClassAssemblys["Game"];
        if (engine == null) throw new Exception("Engine cannot be null!");
        if (game == null) throw new Exception("Game cannot be null!");

        Type ContentCache = engine.GetType("Engine.Content.ContentCache");
        CurrentVersion.VersionType = ContentCache == null ? VersionTypes.APIEdition : VersionTypes.OriginalEdition;
        if (CurrentVersion.VersionType == VersionTypes.APIEdition)
        {
            GLog.Warn("GML Assets Loader is Only Supported for Origin Edition!");
            return;
        }
        //harmony.Patch()
        if (ContentCache != null)
        {
            GModsManager.ClassTypes.Add("Engine.Content.ContentCache", ContentCache);

            Type ContentDescription = ContentCache.GetNestedType("ContentDescription", BindingFlags.NonPublic | BindingFlags.Public);
            if (ContentDescription != null) GModsManager.ClassTypes.Add("Engine.Content.ContentCache.ContentDescription", ContentDescription);



            MethodInfo RootGetMethod = ContentCache.GetMethod("Get", 0, new Type[] { typeof(string), typeof(bool) });
            if (RootGetMethod != null) GModsManager.MethodInfos.Add("Engine.Content.ContentCache.Get[0.string.bool]", RootGetMethod);

            Type ContentStream = engine.GetType("Engine.Content.ContentStream");
            if (ContentStream != null) GModsManager.ClassTypes.Add("Engine.Content.ContentStream", ContentStream);

            //MethodInfo SetContentDescription = GUtil.GetNonPublicMethod(ContentCache, "SetContentDescription");
            //if (SetContentDescription != null) GModsManager.MethodInfos.Add("SetContentDescription", SetContentDescription);
            //harmony.Patch(
            //    SetContentDescription,
            //    prefix: typeof(OfficialEditionPatch).GetMethod("ContentCache_SetContentDescription")
            //);

            harmony.Patch(
                RootGetMethod,
                prefix: typeof(OfficialEditionPatch).GetMethod(nameof(OfficialEditionPatch.HandleRootGet))
            );

            //Type LabelWidget = game.GetType("Game.LabelWidget");
            //PropertyInfo LabelWidget_Text = LabelWidget.GetProperty("Text");
            //MethodInfo LabelWidget_TextSet = LabelWidget_Text.SetMethod;
            //harmony.Patch(
            //    LabelWidget_TextSet,
            //    prefix: typeof(OfficialEditionPatch).GetMethod("HandleLabelTextSet")
            //);


            ContentLoader.Initialize();
        }
    }

    public static void PatchWindowTitle(Harmony harmony, Type engine_Window)
    {
        MethodInfo EngineWindow_Method = null;
        try
        {
            EngineWindow_Method = engine_Window.GetProperty("TitlePrefix").GetSetMethod();
        }
        catch (Exception ex)
        {
            try { EngineWindow_Method = engine_Window.GetProperty("Title").GetSetMethod(); }
            catch (Exception ex2) { GLog.Error(ex2); }
        }
        if (EngineWindow_Method != null)
        {
            LoadingForm.UpdateLoadingData("Modifying Title Setter...");
            //窗口标题
            harmony.Patch(EngineWindow_Method, prefix: GUtil.GetMethodByName(typeof(GamePatch), nameof(GamePatch.WindowTitleSet)));
        }
    }
    public static void PatchInitLog(Harmony harmony, MethodInfo GameInit_Method)
    {
        //Log捕获
        try
        {
            LoadingForm.UpdateLoadingData("Handling Log Type...");
            //var harmonyX = new Harmony("com.glass.dynamic.loginformation");
            harmony.Patch(
                original: GameInit_Method,
                transpiler: new HarmonyMethod(typeof(Patch_Log).GetMethod(nameof(Patch_Log.Transpiler)))
            );
        }
        catch (Exception ex) { GLog.Error(ex); }
    }
    public static void PatchEntityCotr(Harmony harmony, Type gameEntitySystem_Entity)
    {
        try
        {
            var entityCtors = gameEntitySystem_Entity.GetConstructors();
            if (entityCtors.Length == 0) entityCtors = gameEntitySystem_Entity.GetDeclaredConstructors().ToArray();
            //var p = entityCtors[1].GetParameters();
            ConstructorInfo entityCtor = entityCtors.Where(item => item.GetParameters().Length == 2).FirstOrDefault();
            if (entityCtor == null) throw new Exception("Entity Constructor with 2 paramaters not found.");
            LoadingForm.UpdateLoadingData("Handling Log Type...");
            harmony.Patch(
                original: entityCtor,
                transpiler: new HarmonyMethod(typeof(Patch_EntityLoad).GetMethod(nameof(Patch_EntityLoad.Transpiler)))
            );
            harmony.Patch(
                original: entityCtor,
                postfix: typeof(GamePatch).GetMethod(nameof(GamePatch.SpreadOnAfterEntityCtor))
            );
        }
        catch (Exception ex) { GLog.Error(ex); }
    }
    public static void PatchEntityLoad(Harmony harmony, MethodInfo Project_EntityLoad)
    {
        MethodInfo patchMethodEntityLoad = GUtil.GetMethodByName(typeof(GamePatch), nameof(GamePatch.HandleEntityLoad));
        try
        {
            harmony.Patch(
                original: Project_EntityLoad,
                postfix: new HarmonyMethod(patchMethodEntityLoad)
            );
        }
        catch (Exception e)
        {
            harmony.Unpatch(Project_EntityLoad, patchMethodEntityLoad); // 不稳定?
            harmony.Patch(
                original: Project_EntityLoad,
                postfix: typeof(GamePatch).GetMethod(nameof(GamePatch.HandleEntityLoad2))
            );
        }
    }
    public static void PatchProject(Harmony harmony, ConstructorInfo Project_Ctor, MethodInfo Project_CreateEntity, MethodInfo Project_LoadEntities)
    {
        GModsManager.TryPatch(harmony,
            original: Project_Ctor,
            postfix: typeof(GamePatch).GetMethod(nameof(GamePatch.HandleProjectCtor)),
            printError: true
        );
        GModsManager.TryPatch(harmony,
            original: Project_CreateEntity,
            postfix: typeof(GamePatch).GetMethod(nameof(GamePatch.HandleCreateEntity)),
            printError: true
        );
        if (CurrentVersion.VersionType == VersionTypes.APIEdition)
        {
            GModsManager.TryPatch(harmony,
                original: Project_LoadEntities,
                postfix: typeof(APIPatch).GetMethod(nameof(APIPatch.HandleLoadEntities)),
                printError: true
            );
        }
        else
        {
            GModsManager.TryPatch(harmony,
                original: Project_LoadEntities,
                postfix: typeof(GamePatch).GetMethod(nameof(GamePatch.HandleLoadEntities)),
                printError: true
            );
        }
    }
}
