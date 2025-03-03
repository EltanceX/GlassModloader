using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AccessLib
{
    public class ModAccessUtil
    {
        /// <summary>
        /// 创建快速普通读取器
        /// </summary>
        /// <typeparam name="T">目标类</typeparam>
        /// <typeparam name="TOut">目标变量类型</typeparam>
        /// <param name="fieldNames">路径</param>
        /// <returns>读取函数</returns>
        public static Func<T, TOut> CreateNestedGetter<T, TOut>(params string[] fieldNames)
        {
            ParameterExpression param = Expression.Parameter(typeof(T), "instance");
            Expression body = param;

            foreach (string fieldName in fieldNames)
            {
                var fieldInfo = body.Type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public) ?? body.Type.GetRuntimeFields().Where(x => x.FieldType.Name == fieldName).FirstOrDefault();
                //?? body.Type.GetRuntimeProperties().Where(x => x.FieldType.Name == fieldName).FirstOrDefault();
                //var fieldInfoRuntime = body.Type.GetRuntimeFields().Where(x=>x.FieldType.Name == fieldName).FirstOrDefault();
                if (fieldInfo == null)
                    throw new Exception($"Field {fieldName} not found!");
                body = Expression.Field(body, fieldInfo);
            }

            return Expression.Lambda<Func<T, TOut>>(body, param).Compile();
        }
        /// <summary>
        /// 创建快速通用读取器
        /// </summary>
        /// <typeparam name="TOut">目标变量类型</typeparam>
        /// <param name="type">目标类</param>
        /// <param name="fieldNames">路径</param>
        /// <returns>读取函数</returns>
        public static Func<object, TOut> CreateNestedGetter<TOut>(Type type, params string[] fieldPath)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            Expression body = instanceParam.Type == typeof(object) ? Expression.Convert(instanceParam, type) : instanceParam;
            bool isStatic = false;

            //解析路径
            for (int i = 0; i < fieldPath.Length; i++)
            {
                string name = fieldPath[i];

                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
                if (field == null) throw new Exception($"Field {name} not found!");
                isStatic = field.IsStatic;

                body = isStatic ? Expression.Field(null, field) : Expression.Field(body, field);
                type = field.FieldType;
            }

            return Expression.Lambda<Func<object, TOut>>(Expression.Convert(body, typeof(TOut)), instanceParam).Compile();
        }
        /// <summary>
        /// 创建快速通用写入器
        /// </summary>
        /// <typeparam name="TIn">写入变量类型</typeparam>
        /// <param name="type">目标类</param>
        /// <param name="fieldNames">路径</param>
        /// <returns>写入函数</returns>
        public static Action<object, TIn> CreateNestedSetter<TIn>(Type type, params string[] fieldPath)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression valueParam = Expression.Parameter(typeof(TIn), "value");
            Expression body = Expression.Convert(instanceParam, type);
            bool isStatic = false;
            FieldInfo field = null;

            //解析路径
            for (int i = 0; i < fieldPath.Length; i++)
            {
                string name = fieldPath[i];

                field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (field == null) throw new Exception($"Field {name} not found!");
                isStatic = field.IsStatic;

                body = isStatic ? Expression.Field(null, field) : Expression.Field(body, field);
                type = field.FieldType;
            }

            //静态字段赋值
            BinaryExpression assign = Expression.Assign(body, Expression.Convert(valueParam, type));
            return Expression.Lambda<Action<object, TIn>>(assign, instanceParam, valueParam).Compile();
        }
        /// <summary>
        /// 创建快速普通写入器
        /// </summary>
        /// <typeparam name="T">目标类</typeparam>
        /// <typeparam name="TIn">写入变量类型</typeparam>
        /// <param name="fieldNames">路径</param>
        /// <returns>写入函数</returns>
        public static Action<T, TOut> CreateNestedSetter<T, TOut>(params string[] fieldNames)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(T), "instance");
            ParameterExpression valueParam = Expression.Parameter(typeof(TOut), "value");

            Expression body = instanceParam;
            Type currentType = typeof(T);
            FieldInfo fieldInfo = null;

            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldInfo = currentType.GetField(fieldNames[i], BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (fieldInfo == null)
                    throw new Exception($"Field {fieldNames[i]} not found!");

                if (i < fieldNames.Length - 1)
                {
                    body = Expression.Field(body, fieldInfo);
                    currentType = fieldInfo.FieldType;
                }
            }

            //生成赋值表达式
            MemberExpression fieldAccess = Expression.Field(body, fieldInfo);
            BinaryExpression assignExpr = Expression.Assign(fieldAccess, valueParam);

            return Expression.Lambda<Action<T, TOut>>(assignExpr, instanceParam, valueParam).Compile();
        }


        //public static Dictionary<(Type,Type,string), Delegate> NestedMethodCallerCache = new Dictionary<(Type, Type, string), Delegate>();
        //public static Func<T, object[], TOut> MethodCallerAuto<T, TOut>(params string[] methodPath)
        //{
        //    var key = (typeof(T), typeof(TOut), string.Join(".", methodPath));
        //    if (!NestedMethodCallerCache.TryGetValue(key, out Delegate existing))
        //    {
        //        var func = CreateNestedMethodCaller<T, TOut>(methodPath);
        //        NestedMethodCallerCache[key] = func;
        //        return func;
        //    }

        //    return (Func<T, object[], TOut>)existing;
        //}

        /// <summary>
        /// 创建快速普通函数调用器
        /// </summary>
        /// <typeparam name="T">目标类</typeparam>
        /// <typeparam name="TOut">返回值类型</typeparam>
        /// <param name="fieldNames">路径</param>
        /// <returns>目标函数调用</returns>
        public static Func<T, object[], TOut> CreateNestedMethodCaller<T, TOut>(params string[] methodPath)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(T), "instance");
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");

            Expression body = instanceParam;
            Type currentType = typeof(T);
            MethodInfo methodInfo = null;

            for (int i = 0; i < methodPath.Length; i++)
            {
                string name = methodPath[i];

                if (i < methodPath.Length - 1)
                {
                    //FieldInfo field = currentType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo field = currentType.GetField(name);
                    if (field == null) throw new Exception($"Field {name} not found!");
                    body = Expression.Field(body, field);
                    currentType = field.FieldType;
                }
                else //最后一个是函数
                {
                    //methodInfo = currentType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);
                    methodInfo = currentType.GetMethod(name) ?? currentType.GetRuntimeMethods().Where(x => x.Name == name).FirstOrDefault();
                    if (methodInfo == null) throw new Exception($"Method {name} not found!");
                }
            }

            //解析方法参数
            ParameterInfo[] parameters = methodInfo.GetParameters();
            Expression[] callArgs = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                // args[i] 转换为正确的参数类型
                callArgs[i] = Expression.Convert(
                    Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                    parameters[i].ParameterType
                );
            }

            //调用
            MethodCallExpression call = Expression.Call(body, methodInfo, callArgs);

            //无返回值情况
            Expression finalExpr = typeof(TOut) == typeof(object) && methodInfo.ReturnType == typeof(void)
                ? Expression.Block(call, Expression.Default(typeof(object)))
                : (Expression)call;

            return Expression.Lambda<Func<T, object[], TOut>>(finalExpr, instanceParam, argsParam).Compile();
        }

        /// <summary>
        /// 创建快速通用函数调用器
        /// </summary>
        /// <typeparam name="TOut">返回值类型</typeparam>
        /// <param name="type">目标类</param>
        /// <param name="fieldNames">路径</param>
        /// <returns>目标函数调用</returns>
        public static Func<object, object[], TOut> CreateNestedMethodCaller<TOut>(Type type, params string[] methodPath)
        {
            ParameterExpression instanceParam = Expression.Parameter(typeof(object), "instance");
            ParameterExpression argsParam = Expression.Parameter(typeof(object[]), "args");

            Expression body = Expression.Convert(instanceParam, type);
            MethodInfo methodInfo = null;
            bool isStatic = false;

            //解析路径
            for (int i = 0; i < methodPath.Length; i++)
            {
                string name = methodPath[i];

                if (i < methodPath.Length - 1)
                {
                    FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null) throw new Exception($"Field {name} not found!");
                    body = Expression.Field(body, field);
                    type = field.FieldType;
                }
                else //最后一个是函数
                {
                    methodInfo = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (methodInfo == null) throw new Exception($"Method {name} not found!");
                    isStatic = methodInfo.IsStatic;
                }
            }

            //解析方法参数
            ParameterInfo[] parameters = methodInfo.GetParameters();
            Expression[] callArgs = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                callArgs[i] = Expression.Convert(
                    Expression.ArrayIndex(argsParam, Expression.Constant(i)),
                    parameters[i].ParameterType
                );
            }

            //静态方法处理
            Expression call;
            if (isStatic)
            {
                call = Expression.Call(methodInfo, callArgs);
            }
            else
            {
                call = Expression.Call(body, methodInfo, callArgs);
            }

            //处理void
            Expression finalExpr = typeof(TOut) == typeof(object) && methodInfo.ReturnType == typeof(void)
                ? Expression.Block(call, Expression.Default(typeof(object)))
                : (Expression)call;

            return Expression.Lambda<Func<object, object[], TOut>>(finalExpr, instanceParam, argsParam).Compile();
        }


        public static Action<T, TIn> CreateSetterMethod<T, TIn>(string propertyName)
        {
            var type = typeof(T);
            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");

            var setterMethod = propertyInfo.SetMethod;

            var targetParam = Expression.Parameter(typeof(T), "target");
            var valueParam = Expression.Parameter(typeof(TIn), "value");

            //创建设置属性的表达式
            var setterExpression = Expression.Call(targetParam, setterMethod, valueParam);

            //编译并返回一个接受目标对象和int值的lambda
            return Expression.Lambda<Action<T, TIn>>(setterExpression, targetParam, valueParam).Compile();
        }
        public static Func<T, TOut> CreateGetterMethod<T, TOut>(string propertyName)
        {
            var type = typeof(T);
            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");
            var getterMethod = propertyInfo.GetMethod;

            //创建一个目标对象参数表达式
            var targetParam = Expression.Parameter(typeof(T), "target");

            //创建获取属性的表达式
            var getterExpression = Expression.Call(targetParam, getterMethod);

            return Expression.Lambda<Func<T, TOut>>(getterExpression, targetParam).Compile();
        }




















        // ONCE & SLOW Method
        public static void SetFieldOnceSlow<T, TIn>(T target, string fieldName, TIn value)
        {
            FieldInfo fieldInfo = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            fieldInfo.SetValue(target, value);
        }
        public static TOut GetFieldOnceSlow<T, TOut>(T target, string fieldName)
        {
            FieldInfo fieldInfo = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            TOut value = (TOut)fieldInfo.GetValue(target);
            return value;
        }
        public static void CallSetterOnceSlow<T, TIn>(T target, string propertyName, TIn value)
        {
            var type = typeof(T);
            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");
            var setterMethod = propertyInfo.SetMethod;
            setterMethod.Invoke(target, [value]);
        }
        public static TOut CallGetterOnceSlow<T, TOut>(T target, string propertyName)
        {
            var type = typeof(T);
            var propertyInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");
            var getterMethod = propertyInfo.GetMethod;
            return (TOut)getterMethod.Invoke(target, null);
        }

        public static TOut CallMethodOnceSlow<T, TOut>(T target, string methodName, params object[] paramaters)
        {
            var type = typeof(T);
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetRuntimeMethods().Where(x => x.Name == methodName).FirstOrDefault();
            return (TOut)method.Invoke(target, paramaters);
        }
        public static TOut CallMethodOnceSlow<T, TOut>(T target, string methodName, int paramatersCount, params object[] paramaters)
        {
            var type = typeof(T);
            var method = type.GetRuntimeMethods().Where(x => x.Name == methodName && x.GetParameters().Length == paramatersCount).FirstOrDefault();
            return (TOut)method.Invoke(target, paramaters);
        }



        public static void SetFieldOnceSlow(Type type, object target, string fieldName, object value)
        {
            FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            fieldInfo.SetValue(target, value);
        }
        public static object GetFieldOnceSlow(Type type, object target, string fieldName)
        {
            FieldInfo fieldInfo = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            object value = fieldInfo.GetValue(target);
            return value;
        }
        public static void CallSetterOnceSlow(Type target, object targetObject, string propertyName, object value)
        {
            var propertyInfo = target.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");
            var setterMethod = propertyInfo.SetMethod;
            setterMethod.Invoke(targetObject, [value]);
        }
        public static object CallGetterOnceSlow(Type target, object targetObject, string propertyName)
        {
            var propertyInfo = target.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (propertyInfo == null) throw new Exception($"Property {propertyName} not found!");
            var getterMethod = propertyInfo.GetMethod;
            return getterMethod.Invoke(targetObject, null);
        }
        public static object CallMethodOnceSlow(Type target, object targetObject, string methodName, params object[] paramaters)
        {
            var method = target.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic) ?? target.GetRuntimeMethods().Where(x => x.Name == methodName).FirstOrDefault();
            return method.Invoke(targetObject, paramaters);
        }
        public static object CallMethodOnceSlow(Type target, object targetObject, string methodName, int paramatersCount, params object[] paramaters)
        {
            var method = target.GetRuntimeMethods().Where(x => x.Name == methodName && x.GetParameters().Length == paramatersCount).FirstOrDefault();
            return method.Invoke(targetObject, paramaters);
        }

    }
}