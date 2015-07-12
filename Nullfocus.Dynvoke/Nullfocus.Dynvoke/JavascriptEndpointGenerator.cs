using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Nullfocus.Dynvoke
{
    public static class JavascriptEndpointGenerator
    {
        public static string GenerateJavascript(Dynvoke dynvoke, string customNamespace)
        {
            StringBuilder output = new StringBuilder();
            StringBuilder angularOut = new StringBuilder();

            Dictionary<string, List<DynvokeTarget>> targetsByController = new Dictionary<string, List<DynvokeTarget>>();

            foreach (DynvokeTarget target in dynvoke.AllTargets)
            {
                if (!targetsByController.ContainsKey(target.ControllerName))
                    targetsByController.Add(target.ControllerName, new List<DynvokeTarget>());

                targetsByController[target.ControllerName].Add(target);
            }

            output.Append(string.Format(namespaceStr, customNamespace));

            foreach (DynvokeObject dynObj in dynvoke.AllObjects)
            {
                string className = dynObj.Name;
                string parameters = string.Join(", ", dynObj.PropertyNamesAndTypes.Keys);
                string properties = string.Join("\n    ", dynObj.PropertyNamesAndTypes.Select(k => k.Key + " = " + k.Key + "; //" + k.Value));

                output.Append(string.Format(classStr, customNamespace, className, parameters, properties));
            }

            foreach (string controllerName in targetsByController.Keys)
            {
                output.Append(string.Format(controllerStr, customNamespace, controllerName));

                angularOut.Append(string.Format(angularObject, customNamespace, controllerName));

                foreach (DynvokeTarget target in targetsByController[controllerName])
                {
                    string parameters = string.Join(", ", target.ParameterNames);
                    parameters += (parameters.Length > 0 ? ", " : "");

                    string endpoint = "/" + customNamespace + "/" + controllerName + "/" + target.ActionName;
                    string properties = string.Join(", ", target.ParameterNames.Select<string, string>(p => p + ":" + p));

                    output.Append(string.Format(ajaxStr, customNamespace, controllerName, target.ActionName, parameters, endpoint, properties));

                    angularOut.Append(string.Format(angularHttp, customNamespace, controllerName, target.ActionName, parameters, endpoint, properties));
                }
            }

            output.Append(string.Format(angularService, customNamespace, angularOut.ToString()));

            return output.ToString();
        }


        #region Javascript strings

        //0=base namespace
        private static string namespaceStr = @"
var {0} = {{}};
{0}.models = {{}};
";
        //0=base namespace
        //1=classname
        //2=parameters
        //3=properties ie. this.blah = blah; for each parameter
        private static string classStr = @"
{0}.models.{1}  = function({2}){{
    {3}
}};
";

        //0=base namespace
        //1=controller
        private static string controllerStr = @"
{0}.{1} = {{}};
";

        //0=base namespace
        //1=controller
        //2=action
        //3=params
        //4=endpoint + /controller/action/
        //5=properties + params
        private static string ajaxStr = @"
{0}.{1}.{2} = function({3}successFunc, failureFunc){{
    var xmlhttp = new XMLHttpRequest();

    if(successFunc){{
        xmlhttp.addEventListener(""load"", function(evt){{
            successFunc(JSON.parse(evt.responseText));
        }}, false);
    }}

    if(failureFunc){{
        xmlhttp.addEventListener(""error"", function(){{
            failureFunc();
        }}, false);  
    }} 

    xmlhttp.open(""POST"", ""{4}?"" + (new Date()).getTime()); //add time string to bypass caching...
    xmlhttp.setRequestHeader(""Content-Type"", ""application/json;charset=UTF-8"");
    xmlhttp.send(JSON.stringify({{ {5} }}));
}};
";


        private static string angularService = @"
{0}.NgService = function ($http){{
    var svc = {{}};
{1}
    
    return svc;
}};
";
        private static string angularObject = @"
    svc.{1} = {{}};
";


        private static string angularHttp = @"
    svc.{1}.{2} = function({3}successFunc, failureFunc){{
        var post = $http.post('{4}', {{ {5} }});
        
        if(successFunc)
            post.success(function(data) {{
                successFunc(data);
            }});
        
        if(failureFunc)
            post.error(function() {{
                failureFunc();
            }});
    }}
";

        #endregion
    }
}

