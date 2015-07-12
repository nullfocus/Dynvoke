using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Common.Logging;

namespace Nullfocus.Dynvoke
{
    public class HttpDynvokeRequest
    {
        public string Controller { get; set; }

        public string Action { get; set; }

        public string RequestBody { get; set; }

        public HttpDynvokeRequest()
        {
        }

        public HttpDynvokeRequest(string controller, string action, string requestBody)
        {
            this.Controller = controller;
            this.Action = action;
            this.RequestBody = requestBody;
        }
    }

    public class HttpDynvokeResponse
    {
        public int StatusCode { get; set; }

        public string ContentType { get; set; }

        public string ResponseBody { get; set; }

        public HttpDynvokeResponse()
            : this(200, "", "text/html")
        {
        }

        public HttpDynvokeResponse(int statusCode, string responseBody)
            : this(statusCode, responseBody, "text/html")
        {
        }

        public HttpDynvokeResponse(int statusCode, string responseBody, string contentType)
        {
            this.StatusCode = statusCode;
            this.ResponseBody = responseBody;
            this.ContentType = contentType;
        }
    }

    public class JsonParamProvider : ParamProvider
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        private Dictionary<string, JProperty> jsonChildren = new Dictionary<string, JProperty>();

        public JsonParamProvider(string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                JObject jsonObj = JObject.Parse(json);

                if (jsonObj.Type != JTokenType.Object)
                {
                    Log.Error("Could not parse json request!");
                    return;
                }

                foreach (JProperty child in jsonObj.Children<JProperty>())                
                    jsonChildren.Add(child.Name, child);
            }
        }

        public bool TryGetParam(string name, Type type, out object value)
        {
            JProperty child = null;

            if (jsonChildren.TryGetValue(name, out child))
            {
                value = child.Value.ToObject(type);
                return true;
            }

            value = null;

            return false;
        }
    }

    public class HttpDynvoke
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        public Dynvoke Dynvoke { get; set; }

        private static readonly HttpDynvokeResponse NOT_FOUND = new HttpDynvokeResponse(404, "Not Found");
        private static readonly HttpDynvokeResponse BAD_ARGUMENTS = new HttpDynvokeResponse(400, "Arguments need to be wrapped in a JSON object");
        private static readonly HttpDynvokeResponse MISSING_ARGUMENTS = new HttpDynvokeResponse(400, "Missing arguments");
        private static readonly HttpDynvokeResponse SERVER_ERROR = new HttpDynvokeResponse(500, "Server Error");

        public HttpDynvoke() : this(new Dynvoke()) { }

        public HttpDynvoke(Dynvoke dynvoke)
        {
            this.Dynvoke = dynvoke;
        }

        public void FindTargets() { this.Dynvoke.FindTargets(); }

        public HttpDynvokeResponse HandleRequest(HttpDynvokeRequest request)
        {
            DateTime requestStart = DateTime.Now;

            try
            {
                string json = request.RequestBody;

                JsonParamProvider jsonParams = new JsonParamProvider(json);

                DynvokeMethod method = this.Dynvoke.PrepTarget(request.Controller, request.Action, jsonParams);

                if (method == null)
                    return NOT_FOUND;

                if (!method.ReadyToCall)
                    return MISSING_ARGUMENTS;

                object returnObj = method.Call();
                string outputStr = "";

                if (method.ReturnType != typeof(void))
                {
                    outputStr = JsonConvert.SerializeObject(returnObj);
                    Log.Debug("Called method, returned: [" + outputStr + "]");
                }
                else
                {
                    Log.Debug("Called method, void return");
                }

                return new HttpDynvokeResponse(200, outputStr, "application/json");

            }
            catch (Exception e)
            {
                Log.Error("Exception thrown while handling request!\n" + e.ToString());
                return SERVER_ERROR;
            }
            finally
            {
                Log.Debug("Total response time in [" + DateTime.Now.Subtract(requestStart).TotalMilliseconds + "] ms");
            }
        }
    }
}
