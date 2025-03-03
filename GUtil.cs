using Microsoft.CodeAnalysis;
using MonoMod.Logs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GlassLoader
{
    public enum GLogType
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL
    }

    public class GUtil
    {
        public class PerformanceStatistic
        {
            public double startTime;
            public double runningTime;
            public PerformanceStatistic()
            {
                this.startTime = GUtil.GetTime();
            }
            public double end()
            {
                return runningTime = GUtil.GetTime() - startTime;
            }
        }
        public static double GetTime()
        {
            DateTime dateTime = DateTime.Now;
            DateTime startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
            return (dateTime - startTime).TotalMilliseconds;

        }
        public static string UUID()
        {
            return Guid.NewGuid().ToString();
        }
        // 移除JSON注释
        public static string RemoveJsonComments(string json)
        {
            // //单行注释
            json = Regex.Replace(json, @"//.*", "");
            // /* */多行注释
            json = Regex.Replace(json, @"/\*[\s\S]*?\*/", "");
            return json.Trim();
        }
        public static string GetDateTime()
        {
            var date = DateTime.Now;
            return string.Format("{0}.{1}", date.ToString("yyyy/MM/dd HH:mm:ss"), date.Millisecond.ToString().PadRight(3, '0'));
        }
        public static string GetTimeString()
        {
            var date = DateTime.Now;
            return date.ToString("HH:mm:ss.fff");
        }
        public static double GetMillionSecond()
        {
            DateTime dateTime = DateTime.Now;
            DateTime startTime = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
            return (dateTime - startTime).TotalMilliseconds;
        }
        public static void ErrorTerminate()
        {
            GLog.Error("A Fatal Error occurred, GML TERMINATED.");
            GLog.Error("Press ANY key to end.");
            Console.ReadKey();
            Environment.Exit(0);
        }
        public static Assembly FindAssembly(string assemblyName)
        {
            // 获取当前已加载的所有程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // 查找指定名称的程序集
            return assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
        }

        public static MethodInfo? GetMethodByName(Type classType, string methodName, BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public)
        {
            return classType.GetMethod(methodName, bindingFlags) ?? classType.GetRuntimeMethods().Where(x => x.Name == methodName).FirstOrDefault();
        }
        public static MethodInfo? GetNonPublicMethod(Type classType, string methodName, BindingFlags bindingFlags = BindingFlags.NonPublic)
        {
            //var a = classType.GetMethod(methodName, bindingFlags);
            //var b = classType.GetMethods();
            return classType.GetMethod(methodName, bindingFlags) ?? classType.GetRuntimeMethods().Where(x => x.Name == methodName).FirstOrDefault();
        }
        public static ConstructorInfo? GetFirstConstructor(Type classType)
        {
            return classType.GetConstructors().Where(_ => true).FirstOrDefault();
        }
        public static bool NullVerification(object? obj, bool exit = false)
        {
            if (obj == null)
            {
                GLog.Error(new Exception($"Object {obj} cannot be null, Unexpected error my be caused."));
                if (exit)
                {
                    ErrorTerminate();
                }
            }
            return obj == null;
        }

    }
    public class GLog
    {
        public static GLogType GLogLevel = GLogType.DEBUG;
        public static void Info(object obj)
        {
            Output(obj, GLogType.INFO);
        }
        public static void Display(object obj)
        {
            Output(obj, GLogType.INFO, ForceDisplay: true);
        }
        public static void Debug(object obj)
        {
            Output(obj, GLogType.DEBUG);
        }
        public static void Warn(object obj)
        {
            Output(obj, GLogType.WARN, ConsoleColor.DarkYellow);
        }
        public static void Error(object obj)
        {
            Debugger.Break();
            Output(obj, GLogType.ERROR, ConsoleColor.DarkRed);
        }
        public static void Fatal(object obj)
        {
            Output(obj, GLogType.FATAL, ConsoleColor.DarkRed);
        }
        private static void Output(object obj, GLogType logType, ConsoleColor color = ConsoleColor.White, bool ForceDisplay = false)
        {
            if (!ForceDisplay && logType < GLogLevel) return;
            string str = string.Format("[{0}][{1}] ", logType.ToString(), GUtil.GetDateTime());
            if (obj == null)
            {
                str = "null";
            }
            else str += obj.ToString();
            Console.ForegroundColor = color;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.DarkGray;
        }
    }
}
