using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nullfocus.Dynvoke
{
    public class DynvokeTarget
    {
        public MethodInfo Method { get; set; }

        public Type Returns { get { return Method.ReturnType; } }

        public IReadOnlyList<string> InteralParamOrder { get; set; }

        public IReadOnlyDictionary<string, Type> ExternalParameters { get; set; }

        public IReadOnlyDictionary<string, Type> InjectedParameters { get; set; }

        public string ControllerName { get; set; }

        public string ActionName { get; set; }

        public IEnumerable<string> ParameterNames { get { return ExternalParameters.Keys; } }
    }
}
