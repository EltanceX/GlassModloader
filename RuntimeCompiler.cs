using System;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace GlassLoader
{
    public class RuntimeCompiler
    {
        public static List<MetadataReference> GetSharedDependencies(List<string> deps)
        {
            var dependencies = new List<MetadataReference>();

            //引用程序集
            string coreLib = typeof(object).Assembly.Location; // System.Private.CoreLib.dll
            //string systemRuntime = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll");
            //string systemConsole = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Console.dll");
            dependencies.Add(MetadataReference.CreateFromFile(coreLib));
            foreach (var dep in deps)
            {
                try
                {
                    string libPath = Path.Combine(Path.GetDirectoryName(coreLib)!, dep);
                    dependencies.Add(MetadataReference.CreateFromFile(libPath));
                }
                catch (Exception ex)
                {
                    GLog.Error(ex);
                    GLog.Error($"An error happened while loading shared dependencies: {dep}");
                    continue;
                }
            }

            return dependencies;
        }
        public static List<MetadataReference> GetLocalDependencies(List<string> deps)
        {
            var dependencies = new List<MetadataReference>();

            //引用程序集
            foreach (var dep in deps)
            {
                try
                {
                    string libPath = dep.Replace("${ROOT}", FileManager.DefaultDirectory);
                    libPath = libPath.Replace("${GAMEVERSION}", Glass.CurrentVersion.directoryInfo.Name);
                    dependencies.Add(MetadataReference.CreateFromFile(libPath));
                }
                catch (Exception ex)
                {
                    GLog.Error(ex);
                    GLog.Error($"An error happened while loading local dependencies: {dep}");
                    continue;
                }
            }

            return dependencies;
        }
        public static Dictionary<string, string> GetCsSources(List<FileInfo> fileInfos)
        {
            Dictionary<string, string> sources = new Dictionary<string, string>();

            foreach (var fileInfo in fileInfos)
            {
                string code = File.ReadAllText(fileInfo.FullName);
                sources.Add(fileInfo.Name, code);
            }

            return sources;
        }
        public static Assembly? CompileToAssembly(Dictionary<string, string> sources, List<MetadataReference> references, string LibraryName = null)
        {
            LibraryName ??= "RuntimeMod_" + Guid.NewGuid().ToString();

            //解析语法树
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var source in sources.Values)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(source));
            }

            //引用程序集
            //    string coreLib = typeof(object).Assembly.Location; // System.Private.CoreLib.dll
            //    string systemRuntime = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Runtime.dll");
            //    string systemConsole = Path.Combine(Path.GetDirectoryName(coreLib)!, "System.Console.dll");

            //    var references = new List<MetadataReference>
            //{
            //    MetadataReference.CreateFromFile(coreLib),
            //    MetadataReference.CreateFromFile(systemRuntime),
            //    MetadataReference.CreateFromFile(systemConsole)
            //};

            //编译选项
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName: LibraryName,
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );
            GLog.Info($"Compiling: {LibraryName}");

            using (var dllStream = new MemoryStream())
            {
                var emitResult = compilation.Emit(dllStream);

                if (!emitResult.Success)
                {
                    GLog.Error("Compile Failed :");
                    foreach (var diagnostic in emitResult.Diagnostics)
                    {
                        GLog.Error(diagnostic.ToString());
                    }
                    return null;
                }

                GLog.Info($"Compile success: {LibraryName}");

                //从内存加载程序集
                dllStream.Seek(0, SeekOrigin.Begin);
                byte[] assemblyBytes = dllStream.ToArray();
                Assembly assembly = Assembly.Load(assemblyBytes);
                return assembly;
            }
        }
    }
}