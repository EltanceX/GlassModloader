using GlassLoader;
using HarmonyLib;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GlassLoader
{
    public class GContentDescription
    {
        public Func<Stream> ContentFactory;
        public ModInstance ParentMod;
        public List<(FileInfo fileInfo, string suffix)> AssetsFileInfo = new();
        //public string Suffix;

        public object? Content;
        public GContentDescription(ModInstance parentMod, FileInfo fileInfo)
        {
            this.ParentMod = parentMod;
            this.AssetsFileInfo.Add((fileInfo, fileInfo.Extension));
            this.ContentFactory = () => File.OpenRead(fileInfo.FullName);
            //Suffix = fileInfo.Extension;
        }
        public GContentDescription(ModInstance parentMod, List<FileInfo> fileInfo)
        {
            this.ParentMod = parentMod;
            foreach (var file in fileInfo)
            {
                this.AssetsFileInfo.Add((file, file.Extension));
            }
            this.ContentFactory = () => File.OpenRead(fileInfo[0].FullName);
            //Suffix = fileInfo.Extension;
        }
        public void Merge(GContentDescription gContentDescription)
        {
            AssetsFileInfo = gContentDescription.AssetsFileInfo.Union(AssetsFileInfo).ToList();
        }
    }

    //待添加报错分析
    public class ContentLoader
    {
        //public static HashSet<Assembly> m_scannedAssemblies = new HashSet<Assembly>();
        //public static Dictionary<string, string> SuffixToTypename = new();
        //public static Dictionary<List<string>, string> SuffixListToTypename = new() {
        //    { new List<string>(){ ".png" }, "Engine.Graphics.Texture2D" }
        //};
        //public static Dictionary<string, object> ContentReadersByTypeName = new Dictionary<string, object>();
        public static Dictionary<string, GContentDescription> GlobalDescriptions = new();
        public static void Initialize()
        {
            //加载模组资源 (读取优先级高于原版资源)
            foreach (var mod in Glass.ModList)
            {
                var AssetsInfo = FileManager.GetAssetsPath(mod);
                foreach (var AssetData in AssetsInfo)
                {
                    var description = new GContentDescription(mod, AssetData.Value);
                    if (GlobalDescriptions.Keys.Contains(AssetData.Key))
                    {
                        GlobalDescriptions[AssetData.Key].Merge(description);
                    }
                    else GlobalDescriptions.TryAdd(AssetData.Key, description);
                    //if (!success)
                    //{
                    //    GLog.Warn("Assets with the same name is not allowed: " + AssetData.Key);
                    //    GLog.Warn("Attempting to override...");
                    //    GlobalDescriptions[AssetData.Key] = description;
                    //}
                }
            }


            //加载原版读取器
            //foreach (var item in SuffixListToTypename)
            //{
            //    foreach (var suffix in item.Key)
            //    {
            //        if (!SuffixToTypename.ContainsKey(suffix))
            //            SuffixToTypename.Add(suffix, item.Value);
            //    }
            //}

            //Type ContentCache = GModsManager.ClassTypes["Engine.Content.ContentCache"];
            //MethodInfo ScanAssembliesForContentReaders = GUtil.GetNonPublicMethod(ContentCache, "ScanAssembliesForContentReaders");
            //ScanAssembliesForContentReaders.Invoke(null, null);
            //FieldInfo ContentReaders = ContentCache.GetRuntimeFields().Where(x => x.Name == "m_contentReadersByTypeName").FirstOrDefault();
            //object value = ContentReaders.GetValue(ContentCache);
            //foreach (var content in value as dynamic)
            //{
            //    ContentReadersByTypeName.Add(content.Key, content.Value);
            //}

            //初始化引擎类
            Assembly engine = GModsManager.ClassAssemblys["Engine"];
            Type Texture2D = engine.GetType("Engine.Graphics.Texture2D");
            Type Png = engine.GetType("Engine.Media.Png");
            Type Image = engine.GetType("Engine.Media.Image");
            Type BitmapFont = engine.GetType("Engine.Media.BitmapFont");
            Type Glyph = BitmapFont.GetNestedType("Glyph");
            Type Vector2 = engine.GetType("Engine.Vector2");
            Type ShaderMacro = engine.GetType("Engine.Graphics.ShaderMacro");
            Type Shader = engine.GetType("Engine.Graphics.Shader");
            Type SoundBuffer = engine.GetType("Engine.Audio.SoundBuffer");
            Type SoundData = engine.GetType("Engine.Media.SoundData");
            Type Model = engine.GetType("Engine.Graphics.Model");
            GUtil.NullVerification(engine);
            GUtil.NullVerification(Texture2D);
            GUtil.NullVerification(Png);
            GUtil.NullVerification(BitmapFont);
            GUtil.NullVerification(Glyph);
            GUtil.NullVerification(Vector2);
            GUtil.NullVerification(ShaderMacro);
            GUtil.NullVerification(SoundBuffer);
            GUtil.NullVerification(SoundData);
            GUtil.NullVerification(Model);
            GModsManager.ClassTypes.TryAdd("Engine.Graphics.Texture2D", Texture2D);
            GModsManager.ClassTypes.TryAdd("Engine.Media.Png", Png);
            GModsManager.ClassTypes.TryAdd("Engine.Media.Image", Image);
            GModsManager.ClassTypes.TryAdd("Engine.Media.BitmapFont", BitmapFont);
            GModsManager.ClassTypes.TryAdd("Engine.Media.BitmapFont.Glyph", Glyph);
            GModsManager.ClassTypes.TryAdd("Engine.Vector2", Vector2);
            GModsManager.ClassTypes.TryAdd("Engine.Graphics.ShaderMacro", ShaderMacro);
            GModsManager.ClassTypes.TryAdd("Engine.Graphics.Shader", Shader);
            GModsManager.ClassTypes.TryAdd("Engine.Audio.SoundBuffer", SoundBuffer);
            GModsManager.ClassTypes.TryAdd("Engine.Media.SoundData", SoundData);
            GModsManager.ClassTypes.TryAdd("Engine.Graphics.Model", SoundData);

            MethodInfo Texture2DLoad = Texture2D.GetMethod("Load", 0, new Type[] { typeof(Stream), typeof(bool), typeof(int) }); //,false, 1
            MethodInfo Texture2DLoad2 = Texture2D.GetMethod("Load", 0, new Type[] { Image, typeof(int) });
            MethodInfo BitmapFontInitialize = GUtil.GetMethodByName(BitmapFont, "Initialize", BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo SoundBufferLoad = SoundBuffer.GetMethod("Load", 0, new Type[] { typeof(Stream) });
            MethodInfo SoundDataStream = SoundData.GetMethod("Stream", 0, new Type[] { typeof(Stream) });
            MethodInfo ModelLoad = Model.GetMethod("Load", 0, new Type[] { typeof(Stream), typeof(bool) });
            GUtil.NullVerification(Texture2DLoad);
            GUtil.NullVerification(Texture2DLoad2);
            GUtil.NullVerification(BitmapFontInitialize);
            GUtil.NullVerification(SoundBufferLoad);
            GUtil.NullVerification(SoundDataStream);
            GUtil.NullVerification(ModelLoad);
            GModsManager.MethodInfos.TryAdd("Texture2D.Load", Texture2DLoad);
            GModsManager.MethodInfos.TryAdd("Texture2D.Load2", Texture2DLoad2);
            GModsManager.MethodInfos.TryAdd("BitmapFont.Initialize", BitmapFontInitialize);
            GModsManager.MethodInfos.TryAdd("SoundBuffer.Load", SoundBufferLoad);
            GModsManager.MethodInfos.TryAdd("SoundData.Stream", SoundDataStream);
            GModsManager.MethodInfos.TryAdd("Model.Load", ModelLoad);
        }
        /// <summary>
        /// 获取资源描述
        /// </summary>
        /// <param name="FormatPath">资源路径</param>
        /// <returns>资源描述</returns>
        public static GContentDescription? GetDirect(string FormatPath)
        {
            return GlobalDescriptions.ContainsKey(FormatPath) ? GlobalDescriptions[FormatPath] : null;
        }
        /// <summary>
        /// 读取并加载资源
        /// </summary>
        /// <param name="FormatPath">资源路径</param>
        /// <returns>加载后的资源</returns>
        public static object? Get(string FormatPath)
        {
            if (GlobalDescriptions.ContainsKey(FormatPath))
            {
                var Description = GlobalDescriptions[FormatPath];
                if (Description.Content != null) return Description.Content;
                try
                {
                    LoadAssets(Description);
                }
                catch (Exception ex) { GLog.Error(ex); throw; }
                if (Description.Content != null) return Description.Content;

            }
            return null;
        }
        public static bool LoadAssets(GContentDescription description)
        {
            string suffix = description.AssetsFileInfo[0].suffix.ToLower();
            switch (suffix)
            {
                case ".png":
                    description.Content = GetTexture(description);
                    return true;
                case ".xml":
                    description.Content = GetXML(description);
                    return true;
                case ".txt":
                    description.Content = GetString(description);
                    return true;
                case ".psh":
                case ".vsh":
                    description.Content = GetShader(description);
                    return true;
                case ".wav":
                    description.Content = GetSoundBuffer(description);
                    return true;
                case ".ogg":
                    description.Content = GetSoundStreaming(description);
                    return true;
                case ".dae":
                    description.Content = GetDae(description);
                    return true;
                default:
                    GLog.Warn($"Not supported Asset Type: {suffix} [{description.AssetsFileInfo[0].fileInfo.Name}]");
                    break;
            }
            return false;
        }
        public static object GetDae(GContentDescription DaeDescription)
        {
            MethodInfo ModelLoad = GModsManager.MethodInfos["Model.Load"];
            Stream ContentStream = DaeDescription.ContentFactory();
            object Content = null;
            try
            {
                Content = ModelLoad.Invoke(null, [ContentStream, true]);
                ContentStream.Close();
                ContentStream.Dispose();
            }
            catch (Exception ex)
            {
                ContentStream.Close();
                ContentStream.Dispose();
                throw;
            }
            DaeDescription.Content = Content;
            return Content;
        }
        public static object GetSoundBuffer(GContentDescription SoundDescription)
        {
            //Type SoundBuffer = GModsManager.ClassTypes["Engine.Audio.SoundBuffer"];
            MethodInfo Load = GModsManager.MethodInfos["SoundBuffer.Load"];
            Stream ContentStream = SoundDescription.ContentFactory();
            object Content = null;
            try
            {
                Content = Load.Invoke(null, [ContentStream]);
                ContentStream.Close();
                ContentStream.Dispose();
            }
            catch (Exception ex)
            {
                ContentStream.Close();
                ContentStream.Dispose();
                throw;
            }
            SoundDescription.Content = Content;
            return Content;
        }
        public static object GetSoundStreaming(GContentDescription SoundDescription)
        {
            //Type SoundBuffer = GModsManager.ClassTypes["Engine.Audio.SoundBuffer"];
            //MethodInfo Load = GModsManager.MethodInfos["SoundBuffer.Load"];
            MethodInfo SoundDataStream = GModsManager.MethodInfos["SoundData.Stream"];
            Stream ContentStream = SoundDescription.ContentFactory();
            object Content = null;
            try
            {
                //Content = Load.Invoke(null, [ContentStream]);
                Content = SoundDataStream.Invoke(null, [ContentStream]);
                ContentStream.Close();
                ContentStream.Dispose();
            }
            catch (Exception ex)
            {
                ContentStream.Close();
                ContentStream.Dispose();
                throw;
            }
            SoundDescription.Content = Content;
            return Content;
        }
        public static object GetShader(GContentDescription ShaderDescription)
        {
            Type ShaderMacro = GModsManager.ClassTypes["Engine.Graphics.ShaderMacro"];
            Type Shader = GModsManager.ClassTypes["Engine.Graphics.Shader"];
            var shaderMacros = Array.CreateInstance(ShaderMacro, 0);
            if (ShaderDescription.AssetsFileInfo[0].fileInfo.Name.StartsWith("AlphaTested"))
            {
                shaderMacros = Array.CreateInstance(ShaderMacro, 1);
                shaderMacros.SetValue(Activator.CreateInstance(ShaderMacro, "ALPHATESTED"), 0);
            }
            int vshIndex = ShaderDescription.AssetsFileInfo[0].suffix.ToLower() == ".vsh" ? 0 : 1;
            string vsh = File.ReadAllText(ShaderDescription.AssetsFileInfo[vshIndex].fileInfo.FullName);
            string psh = File.ReadAllText(ShaderDescription.AssetsFileInfo[1 - vshIndex].fileInfo.FullName);

            var ShaderIns = Activator.CreateInstance(Shader, vsh, psh, shaderMacros);
            ShaderDescription.Content = ShaderIns;
            return ShaderIns;
            //return new Shader(new StreamReader(contents[0].Duplicate()).ReadToEnd(), new StreamReader(contents[1].Duplicate()).ReadToEnd(), shaderMacros);

        }
        /// <summary>
		/// 纹理图 (字体读取部分引用自API)
		/// </summary>
		/// <param name="TextureStream">图片文件的输入流</param>
		/// <param name="GlyphsStream">位图数据的输入流</param>
		public static object GetBitmapFont(GContentDescription TextureDescription, GContentDescription GlyphsDescription/*, dynamic customGlyphOffsetVec2 = null*/)
        {
            Stream TextureStream = TextureDescription.ContentFactory();
            Stream GlyphsStream = GlyphsDescription.ContentFactory();
            //return null;
            Type Texture2D = GModsManager.ClassTypes["Engine.Graphics.Texture2D"];
            MethodInfo Texture2DLoad = GModsManager.MethodInfos["Texture2D.Load"];
            MethodInfo Texture2DLoad2 = GModsManager.MethodInfos["Texture2D.Load2"];
            Type BitmapFont = GModsManager.ClassTypes["Engine.Media.BitmapFont"];
            Type Glyph = GModsManager.ClassTypes["Engine.Media.BitmapFont.Glyph"];
            MethodInfo BitmapFontInitialize = GModsManager.MethodInfos["BitmapFont.Initialize"];
            Type Vector2 = GModsManager.ClassTypes["Engine.Vector2"];
            Type Image = GModsManager.ClassTypes["Engine.Media.Image"];
            try
            {
                //object texture = Texture2DLoad.Invoke(null, [TextureStream, false, 1]);
                object texture = GetTexture(TextureDescription);


                //var aa = BitmapFont.GetConstructors();
                ConstructorInfo BmpFontCtor = BitmapFont.GetDeclaredConstructors().Where(x => x.GetParameters().Length == 0 && x.Name == ".ctor").FirstOrDefault();
                //var bb = BitmapFont.GetRuntimeMethods();

                //object bitmapFont = Activator.CreateInstance(BitmapFont, BindingFlags.NonPublic | BindingFlags.Instance);
                object bitmapFont = BmpFontCtor.Invoke(null);
                StreamReader streamReader = new(GlyphsStream);
                int num = int.Parse(streamReader.ReadLine());
                var array = Array.CreateInstance(Glyph, num);
                //var array = new Glyph[num];
                for (int i = 0; i < num; i++)
                {
                    string line = streamReader.ReadLine();
                    string[] arr = line.Split(new[] { (char)0x20, (char)0x09 }, StringSplitOptions.None);
                    if (arr.Length == 9)
                    {
                        string[] tmp = new string[8];
                        tmp[0] = " ";
                        for (int j = 2; j < arr.Length; j++)
                        {
                            tmp[j - 1] = arr[j];
                        }
                        arr = tmp;
                    }
                    char code = char.Parse(arr[0]);
                    //Vector2 texCoord = new(float.Parse(arr[1]), float.Parse(arr[2]));
                    //Vector2 texCoord2 = new(float.Parse(arr[3]), float.Parse(arr[4]));
                    //Vector2 offset = new(float.Parse(arr[5]), float.Parse(arr[6]));
                    object texCoord = Activator.CreateInstance(Vector2, float.Parse(arr[1]), float.Parse(arr[2]));
                    object texCoord2 = Activator.CreateInstance(Vector2, float.Parse(arr[3]), float.Parse(arr[4]));
                    object offset = Activator.CreateInstance(Vector2, float.Parse(arr[5]), float.Parse(arr[6]));
                    /*if (customGlyphOffsetVec2.HasValue)
                    {
                        offset += customGlyphOffsetVec2.Value;
                    }*/
                    float width = float.Parse(arr[7]);
                    //array[i] = new Glyph(code, texCoord, texCoord2, offset, width);
                    array.SetValue(Activator.CreateInstance(Glyph, code, texCoord, texCoord2, offset, width), i);
                }
                float glyphHeight = float.Parse(streamReader.ReadLine());
                string line2 = streamReader.ReadLine();
                string[] arr2 = line2.Split(new char[] { (char)0x20, (char)0x09 }, StringSplitOptions.None);
                //Vector2 spacing = new(float.Parse(arr2[0]), float.Parse(arr2[1]));
                object spacing = Activator.CreateInstance(Vector2, float.Parse(arr2[0]), float.Parse(arr2[1]));
                float scale = float.Parse(streamReader.ReadLine());
                char fallbackCode = char.Parse(streamReader.ReadLine());
                //bitmapFont.Initialize(texture, null, array, fallbackCode, glyphHeight, spacing, scale);
                BitmapFontInitialize.Invoke(bitmapFont, [texture, null, array, fallbackCode, glyphHeight, spacing, scale]);
                streamReader.Close();
                streamReader.Dispose();
                GlyphsStream.Close();
                GlyphsStream.Dispose();
                return bitmapFont;
            }
            catch (Exception e)
            {
                GLog.Error(e.Message);
                return null;
            }
        }
        public static object GetString(GContentDescription description)
        {
            Stream stream = description.ContentFactory();
            description.Content = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
            stream.Close();
            stream.Dispose();
            return description.Content;
        }
        public static object GetXML(GContentDescription description)
        {
            Stream stream = description.ContentFactory();
            description.Content = XElement.Load(stream);
            stream.Close();
            stream.Dispose();
            return description.Content;
        }
        public static object GetTexture(GContentDescription description)
        {
            Type Texture2D = GModsManager.ClassTypes["Engine.Graphics.Texture2D"];
            Type Png = GModsManager.ClassTypes["Engine.Media.Png"];
            Type Image = GModsManager.ClassTypes["Engine.Media.Image"];

            //注意此处(bool)不兼容api
            MethodInfo Texture2DLoad = GModsManager.MethodInfos["Texture2D.Load"];
            MethodInfo Texture2DLoad2 = GModsManager.MethodInfos["Texture2D.Load2"];
            MethodInfo PngLoad = GUtil.GetMethodByName(Png, "Load");

            Stream AssetStream = description.ContentFactory();
            //FileStream fs = new FileStream("C:\\Users\\EltanceX\\Desktop\\Blocks.png", FileMode.Open);
            //MemoryStream ms = new MemoryStream();
            //fs.CopyTo(ms);
            //fs.Close();
            //fs.Dispose();
            //ms.Position = 0;

            //string typename = SuffixToTypename[".png"];
            //Type ReaderClass = ContentReadersByTypeName[typename].GetType();
            //MethodInfo Reader = GUtil.GetMethodByName(ReaderClass, "Read");
            try
            {
                //Type cs = GModsManager.ClassTypes["Engine.Content.ContentStream"];
                //object o = Activator.CreateInstance(cs, () => ms);
                //object obj = Reader.Invoke(ContentReadersByTypeName[typename], [o, null, "NName"]);
                //object obj = Texture2DLoad.Invoke(null, [ms, false, 1]);\
                var img = PngLoad.Invoke(null, [AssetStream]);
                object obj = Texture2DLoad2.Invoke(null, [img, 1]);
                AssetStream.Close();
                AssetStream.Dispose();
                return obj;
            }
            catch (Exception ex)
            {
                GLog.Error(ex);
            }
            return null;
        }
    }
}
