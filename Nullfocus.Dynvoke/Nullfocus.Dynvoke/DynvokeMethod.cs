﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;

namespace Nullfocus.Dynvoke
{
    public class DynvokeMethod
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger ();

        private DynvokeTarget Target { get; set; }
        
        private bool _ReadyToCall = false;
        private Dictionary<string, object> ReadyParams = new Dictionary<string, object> ();

        public DynvokeMethod (DynvokeTarget target)
        {
            this.Target = target;            
        }

        public IReadOnlyDictionary<string, Type> Parameters { get { return Target.ExternalParameters; } }
        public IReadOnlyDictionary<string, Type> InjectedParameters { get { return Target.InjectedParameters; } }

        public Type ReturnType { get { return Target.Method.ReturnType; } }

        public bool ReadyToCall {
            get {
                if (!_ReadyToCall) {
                    foreach (string key in this.Target.InteralParamOrder) {
                        if (!ReadyParams.ContainsKey (key)) {
                            _ReadyToCall = false;
                            return _ReadyToCall;
                        }
                    }
                }

                _ReadyToCall = true;
                return _ReadyToCall;
            }
        }

        public void SetParameter (string name, object value)
        {
            ReadyParams.Add (name, value);
        }

        public object Call ()
        {
            if (!ReadyToCall)
                throw new ArgumentException ("Not ready to call, missing parameters!!");

            List<object> invokeParams = new List<object> ();

            for (int i = 0; i < this.Target.InteralParamOrder.Count (); i++) {
                string paramName = this.Target.InteralParamOrder [i];
                object paramVal = ReadyParams [paramName];
                
                invokeParams.Add (paramVal);
            }

            DateTime methodInvokeStart = DateTime.Now;
            
            object returnObj = this.Target.Method.Invoke (null, invokeParams.ToArray ());

            Log.Debug ("Total method time in [" + DateTime.Now.Subtract (methodInvokeStart).TotalMilliseconds + "] ms");

            return returnObj;
        }
    }
}
