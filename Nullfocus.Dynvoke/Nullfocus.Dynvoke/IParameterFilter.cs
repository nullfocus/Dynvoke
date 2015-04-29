using System;

namespace Nullfocus.Dynvoke
{
    public interface IParameterFilter
    {
        /// <summary>
        /// Used to process method parameters from Dynvoke methods when building targets and caching parameters
        /// Any changes made here need to be reflected in ProcessParameter, to change the value and type back to what it expects
        /// </summary>
        /// <param name="paramName">Parameter name.</param>
        /// <param name="paramType">Parameter type.</param>
        void ProcessDefinition (ref string paramName, ref Type paramType);

        /// <summary>
        /// Used to process the actual parameters passed, switch them out with required values before calling the method
        /// </summary>
        /// <param name="paramName">Parameter name.</param>
        /// <param name="paramValue">Parameter value.</param>
        void ProccessParameter (ref string paramName, ref object paramValue);
    }
}
