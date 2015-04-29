namespace Nullfocus.Dynvoke
{
    /// <summary>
    /// Attribute for static classes to allow for automatic lookup and invokation of methods
    /// </summary>
    [System.AttributeUsage (System.AttributeTargets.Class)]
    public class DynvokeClassAttribute : System.Attribute
    {
        public string AltName { get; set; }

        public bool AttributedMethodsOnly { get; set; }

        public DynvokeClassAttribute ()
        {
            AltName = null;
            AttributedMethodsOnly = false;
        }
    }

    /// <summary>
    /// Attribute for methods with classes attributed with [DynvokeClass]
    /// </summary>
    [System.AttributeUsage (System.AttributeTargets.Method)]
    public class DynvokeMethodAttribute : System.Attribute
    {
        public string AltName { get; set; }

        public DynvokeMethodAttribute ()
        {
            AltName = null;
        }
    }

    /// <summary>
    /// Attribute for generation of helper classes in generated code
    /// Only those actually used in DynvokeMethods will be captured and generated!
    /// </summary>
    [System.AttributeUsage (System.AttributeTargets.Class)]
    public class DynvokeObjectAttribute : System.Attribute
    {
		
    }
}
