using System;
using System.Collections.Generic;
using System.Reflection;

namespace Nullfocus.Dynvoke
{
    public class DynvokeTarget
    {
        public MethodInfo Method { get; set; }

        public Type Returns { get { return Method.ReturnType; } }

        public IReadOnlyList<string> ParamOrder { get; set; }

        public IReadOnlyDictionary<string, Type> Paramters { get; set; }

        public string ControllerName { get; set; }

        public string ActionName { get; set; }

        public IEnumerable<string> ParameterNames { get { return Paramters.Keys; } }
    }
}
