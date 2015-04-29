using System.Collections.Generic;

namespace Nullfocus.Dynvoke
{
    public class DynvokeObject
    {
        public string Name { get; set; }

        public Dictionary<string, string> PropertyNamesAndTypes = new Dictionary<string, string> ();
    }
}
