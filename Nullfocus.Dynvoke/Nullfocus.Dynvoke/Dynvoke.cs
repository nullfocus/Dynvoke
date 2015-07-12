using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Common.Logging;

namespace Nullfocus.Dynvoke
{
    public delegate object ParamReplacment(object value);
    public delegate object ParamInjection();

    public interface ParamProvider
    {
        bool TryGetParam(string name, Type type, out object value);
    }

    public class Dynvoke
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private bool initialized = false;

        private Dictionary<string, DynvokeTarget> Targets = new Dictionary<string, DynvokeTarget> ();
        private List<DynvokeObject> DynvokeObjects = new List<DynvokeObject> ();
                
        public IReadOnlyList<DynvokeTarget> AllTargets { get { return Targets.Values.ToList (); } }

        public IReadOnlyList<DynvokeObject> AllObjects { get { return DynvokeObjects; } }
        
        private DynvokeMethod Get(string classname, string methodname)
        {
            string key = GetKey(classname, methodname);

            if (!Targets.ContainsKey(key))
                return null;

            DynvokeMethod method = new DynvokeMethod(Targets[key]);

            return method;
        }

        private class DictParamProvider : ParamProvider
        {
            private Dictionary<string, object> dict = null;

            public DictParamProvider(Dictionary<string, object> dict)
            {
                this.dict = dict;
            }

            public bool TryGetParam(string name, Type type, out object value)
            {
                name = name.ToLower();

                if (dict.TryGetValue(name, out value))
                {
                    if(value == null || value.GetType() == type)
                        return true;
                }

                value = null;

                return false;
            }
        }

        public DynvokeMethod PrepTarget(string classname, string methodname, Dictionary<string, object> parameters)
        {
            return PrepTarget(classname, methodname, new DictParamProvider(parameters));
        }

        public DynvokeMethod PrepTarget(string classname, string methodname, ParamProvider provider)
        {
            DynvokeMethod method = Get(classname, methodname);

            if (method == null)
                return null;

            foreach (KeyValuePair<string, Type> entry in method.Parameters)
            {
                object value = null;

                if (provider.TryGetParam(entry.Key, entry.Value, out value))
                {
                    if (ParamReplacersExternal.ContainsKey(entry.Key) && ParamReplacersExternal[entry.Key].ExternalType == entry.Value)
                    {
                        ParamReplacmentContainer replCont = ParamReplacersExternal[entry.Key];

                        object newvalue = replCont.Replacer(value);

                        Log.Debug("  Replacement: [" + replCont.InternalParamName + "] = (" + replCont.InternalType + ") [" + newvalue + "]");

                        method.SetParameter(replCont.InternalParamName, newvalue);
                    }
                    else
                    {
                        Log.Debug("  Parameter:   [" + entry.Key + "] = (" + entry.Value + ") [" + value + "]");

                        method.SetParameter(entry.Key, value);
                    }
                }                
            }

            foreach(KeyValuePair<string, Type> entry in method.InjectedParameters){
                object value = null;

                if (ParamInjectors.ContainsKey(entry.Key) && ParamInjectors[entry.Key].Type == entry.Value)
                {
                    ParamInjectionContainer injCont = ParamInjectors[entry.Key];

                    value = injCont.Injector();

                    Log.Debug("  Injected:    [" + entry.Key + "] = (" + entry.Value + ") [" + value + "]");

                    method.SetParameter(entry.Key, value);
                }
            }

            return method;
        }

        public void FindTargets ()
        {
            if (!initialized)
                initialized = true;
            else
                return;

            Log.Debug ("Finding Dynvoke targets...");

            List<Type> attributedClasses = new List<Type> ();
            List<Type> parameterDynObjs = new List<Type> ();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (Type type in assembly.GetTypes()) {
                    if (null != type.GetCustomAttribute<DynvokeClassAttribute> (true)) {
                        if (!type.IsAbstract || !type.IsSealed) {
                            Log.Warn ("Skipping [" + type.FullName + "] because it isn't static!");

                            continue; 
                        }

                        attributedClasses.Add (type);
                    }
                }
            }

            foreach (Type attributedClass in attributedClasses) {
                string controllerName = GetControllerName (attributedClass);

                DynvokeClassAttribute classAttr = attributedClass.GetCustomAttribute<DynvokeClassAttribute> (true);

                Log.Debug ("Found class [" + attributedClass.FullName + "]");

                if (classAttr.AltName != null)
                    Log.Debug ("Using name [" + classAttr.AltName + "]");

                //static classes require static methods, dont look at object methods...
                foreach (MethodInfo method in attributedClass.GetMethods()) {
                    if (method.DeclaringType != attributedClass)
                        continue;

                    DynvokeMethodAttribute methodAttr = method.GetCustomAttribute<DynvokeMethodAttribute> (true);

                    if (classAttr.AttributedMethodsOnly) {
                        if (methodAttr == null)
                            continue;
                    } else {
                        if (!method.IsPublic && methodAttr == null)
                            continue;
                    }

                    Log.Debug ("  Method [" + method.Name + "]");

                    string actionName = GetActionName (method);
                    string key = GetKey (controllerName, actionName);                    

                    if (methodAttr != null && methodAttr.AltName != null)
                        Log.Debug ("  Using name [" + methodAttr.AltName + "]");

                    if (method.ReturnParameter.ParameterType == typeof(void))
                        Log.Debug ("    void return");
                    else
                        Log.Debug ("    returns [" + method.ReturnParameter.ParameterType.Name + "]");

                    Type returnType = method.ReturnParameter.ParameterType;

                    Dictionary<string, Type> Parameters = new Dictionary<string,Type> ();
                    Dictionary<string, Type> InjectedParameters = new Dictionary<string, Type>();
                    List<string> ParamOrder = new List<string> ();

                    ParameterInfo[] methodParams = method.GetParameters ();

                    if (methodParams.Length == 0)
                        Log.Debug ("    no parameters");

                    foreach (ParameterInfo parameter in methodParams) {
                        string paramName = parameter.Name.ToLower ();
                        Type paramType = parameter.ParameterType;

                        ParamOrder.Add(paramName);

                        if (this.ParamInjectors.ContainsKey(paramName) && this.ParamInjectors[paramName].Type == paramType)
                        {
                            Log.Debug("    [" + paramName + "] (" + paramType.Name + ") <- Injected");

                            InjectedParameters.Add(paramName, paramType);
                            continue;
                        }
                        
                        if (this.ParamReplacersInternal.ContainsKey(paramName) && this.ParamReplacersInternal[paramName].InternalType == paramType)
                        {
                            ParamReplacmentContainer replCont = this.ParamReplacersInternal[paramName];

                            Log.Debug("    [" + paramName + "] (" + paramType.Name + ") <- Replaced with [" + replCont.ExternalType + "] (" + replCont.ExternalParamName + ")");

                            paramName = replCont.ExternalParamName;
                            paramType = replCont.ExternalType;
                        }
                        else
                        {
                            Log.Debug("    [" + paramName + "] (" + paramType.Name + ")");
                        }

                        if (null != paramType.GetCustomAttribute<DynvokeObjectAttribute>(true) && !parameterDynObjs.Contains(paramType))
                            parameterDynObjs.Add(paramType);
                        
                        Parameters.Add (paramName, paramType);
                    }                            

                    DynvokeTarget target = new DynvokeTarget () {
                        Method = method,
                        InteralParamOrder = ParamOrder,
                        ExternalParameters = Parameters,
                        InjectedParameters = InjectedParameters,

                        ControllerName = controllerName,
                        ActionName = actionName
                    };

                    Targets.Add (key, target);                    
                }
            }

            foreach (Type paramDynObj in parameterDynObjs) {
                DynvokeObject dynObj = new DynvokeObject () {
                    Name = paramDynObj.Name
                };

                foreach (PropertyInfo prop in paramDynObj.GetProperties())
                    dynObj.PropertyNamesAndTypes.Add (prop.Name, prop.PropertyType.Name);

                this.DynvokeObjects.Add (dynObj);
            }

            Log.Debug ("Completed Dynvoke search");
        }
        
        #region private static helper methods

        private static string GetControllerName (Type classType)
        {
            DynvokeClassAttribute classAttr = classType.GetCustomAttribute<DynvokeClassAttribute> (true);

            string className = null;

            if (classAttr.AltName != null)
                className = classAttr.AltName;
            else
                className = classType.Name;

            return className.ToLower ();
        }

        private static string GetActionName (MethodInfo method)
        {
            DynvokeMethodAttribute methodAttr = method.GetCustomAttribute<DynvokeMethodAttribute> (true);

            string methodName = null;

            if (methodAttr != null && methodAttr.AltName != null)
                methodName = methodAttr.AltName;
            else
                methodName = method.Name;

            return methodName.ToLower ();
        }

        private static string GetKey (Type classType, MethodInfo method)
        {
            return GetKey (GetControllerName (classType), GetActionName (method));
        }

        private static string GetKey (string controllerName, string actionName)
        {
            return controllerName.ToLower () + "." + actionName.ToLower ();
        }

        #endregion

        #region Param Configuration

        private class ParamInjectionContainer
        {
            public string ParamName { get; set; }
            public Type Type { get; set; }
            public ParamInjection Injector { get; set; }
        }

        private class ParamReplacmentContainer
        {
            public string InternalParamName { get; set; }
            public Type InternalType { get; set; }
            public string ExternalParamName { get; set; }
            public Type ExternalType { get; set; }
            public ParamReplacment Replacer { get; set; }
        }

        private Dictionary<string, ParamInjectionContainer> ParamInjectors = new Dictionary<string, ParamInjectionContainer>();
        private Dictionary<string, ParamReplacmentContainer> ParamReplacersInternal = new Dictionary<string, ParamReplacmentContainer>();
        private Dictionary<string, ParamReplacmentContainer> ParamReplacersExternal = new Dictionary<string, ParamReplacmentContainer>();
        
        public void AddParamInjection(string paramName, Type type, ParamInjection paramInjection)
        {
            ParamInjectors.Add(paramName, new ParamInjectionContainer()
            {
                ParamName = paramName.ToLower(),
                Type = type,
                Injector = paramInjection
            });
        }

        public void AddParamReplacement(string internalParamName, Type internalType, string externalParamName, Type externalType, ParamReplacment paramReplacment)
        {
            ParamReplacmentContainer container = new ParamReplacmentContainer(){
                InternalParamName = internalParamName.ToLower(),
                InternalType = internalType,
                ExternalParamName = externalParamName.ToLower(),
                ExternalType = externalType,
                Replacer = paramReplacment
            };

            ParamReplacersInternal.Add(internalParamName, container);
            ParamReplacersExternal.Add(externalParamName, container);
        }

        #endregion
    }
}

