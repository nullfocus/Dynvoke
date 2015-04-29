using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Logging;

namespace Nullfocus.Dynvoke
{
    public class DynvokeRequest
    {
        public string Controller { get; set; }

        public string Action { get; set; }

        public string RequestBody { get; set; }

        public DynvokeRequest ()
        {
        }

        public DynvokeRequest (string controller, string action, string requestBody)
        {
            this.Controller = controller;
            this.Action = action;
            this.RequestBody = requestBody;
        }
    }

    public class DynvokeResponse
    {
        public int StatusCode { get; set; }

        public string ContentType { get; set; }

        public string ResponseBody { get; set; }

        public DynvokeResponse () : this (200, "", "text/html")
        {
        }

        public DynvokeResponse (int statusCode, string responseBody) : this (statusCode, responseBody, "text/html")
        {
        }

        public DynvokeResponse (int statusCode, string responseBody, string contentType)
        {
            this.StatusCode = statusCode;
            this.ResponseBody = responseBody;
            this.ContentType = contentType;
        }
    }

    public class Dynvoke
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger ();

        private static readonly DynvokeResponse NOT_FOUND = new DynvokeResponse (404, "Not Found");
        private static readonly DynvokeResponse BAD_ARGUMENTS = new DynvokeResponse (400, "Arguments need to be wrapped in a JSON object");
        private static readonly DynvokeResponse MISSING_ARGUMENTS = new DynvokeResponse (400, "Missing arguments");
        private static readonly DynvokeResponse SERVER_ERROR = new DynvokeResponse (500, "Server Error");

        private Dictionary<string, DynvokeTarget> Targets = new Dictionary<string, DynvokeTarget> ();
        private List<DynvokeObject> DynvokeObjects = new List<DynvokeObject> ();

        private IParameterFilter ParameterFilter { get; set; }

        public IReadOnlyList<DynvokeTarget> AllTargets { get { return Targets.Values.ToList (); } }

        public IReadOnlyList<DynvokeObject> AllObjects { get { return DynvokeObjects; } }


        public Dynvoke () : this(null)
        {            
        }

        public Dynvoke (IParameterFilter filter)
        {
            this.ParameterFilter = filter;

            FindTargets ();
        }

        public DynvokeResponse HandleRequest (DynvokeRequest request)
        {
            try {
                DateTime requestStart = DateTime.Now;

                string json = request.RequestBody;

                DynvokeMethod method = Get (request.Controller, request.Action);

                PrepTarget (method, json);

                if (method == null)
                    return NOT_FOUND;

                if (!method.ReadyToCall)
                    return MISSING_ARGUMENTS;            
            
                object returnObj = method.Call ();

                string outputStr = "";

                if (returnObj != null)
                    outputStr = JsonConvert.SerializeObject (returnObj);

                Log.Debug ("Called method, returned: " + outputStr);
                Log.Debug ("Total response time in [" + DateTime.Now.Subtract (requestStart).TotalMilliseconds + "] ms");

                return new DynvokeResponse (200, outputStr, "application/json");

            } catch (Exception e) {
                Log.Error ("Exception thrown while handling request!\n" + e.ToString ());
                return SERVER_ERROR;
            }
        }

        private void FindTargets ()
        {
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
                    List<string> ParamOrder = new List<string> ();

                    ParameterInfo[] methodParams = method.GetParameters ();

                    if (methodParams.Length == 0)
                        Log.Debug ("    no parameters");

                    foreach (ParameterInfo parameter in methodParams) {
                        string paramName = parameter.Name.ToLower ();
                        Type paramType = parameter.ParameterType;

                        if (ParameterFilter != null)
                            ParameterFilter.ProcessDefinition (ref paramName, ref paramType);

                        Log.Debug ("    [" + paramName + "] (" + paramType.Name + ")");

                        if (null != parameter.ParameterType.GetCustomAttribute<DynvokeObjectAttribute> (true) &&
                        !parameterDynObjs.Contains (parameter.ParameterType))
                            parameterDynObjs.Add (parameter.ParameterType);

                        ParamOrder.Add (paramName);
                        Parameters.Add (paramName, paramType);
                    }                            

                    DynvokeTarget target = new DynvokeTarget () {
                        Method = method,
                        ParamOrder = ParamOrder,
                        Paramters = Parameters,

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

        private DynvokeMethod Get (string classname, string methodname)
        {
            string key = GetKey (classname, methodname);

            if (!Targets.ContainsKey (key))
                return null;

            DynvokeMethod method = new DynvokeMethod (Targets [key], ParameterFilter);

            return method;
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

        private static void PrepTarget (DynvokeMethod method, string json)
        {
            if (!string.IsNullOrEmpty (json)) {
                JObject jsonObj = JObject.Parse (json);

                if (jsonObj.Type != JTokenType.Object) {
                    Log.Error ("Could not parse json request!");
                    return;
                }

                foreach (JProperty child in jsonObj.Children<JProperty>()) {
                    string paramName = child.Name;

                    Type paramType = null;

                    if (method.Paramters.TryGetValue (paramName, out paramType)) {
                        object value = child.Value.ToObject (paramType);

                        Log.Debug ("  [" + paramName + "] = [" + value + "]");

                        method.SetParameter (paramName, value);
                    } else
                        Log.Debug ("Unused property [" + paramName + "]");
                }
            }
        }

        #endregion
    }
}

