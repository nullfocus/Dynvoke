using System;
using System.IO;
using System.Net;
using System.Threading;
using Common.Logging;

namespace Nullfocus.Dynvoke
{
    public class HttpDynvokeServer : IDisposable
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger ();

        private static readonly HttpDynvokeResponse BAD_REQUEST = new HttpDynvokeResponse(400, "Bad Request");

        public HttpDynvoke HttpDynvoke { get; set; }

        private string customNamespace = null;
        private HttpDynvokeResponse _GeneratedJS = null;
        private Thread listenThread = null;
        private HttpListener listener = null;
        private CountdownEvent requestProcessingCounter = new CountdownEvent (1);

        public HttpDynvokeServer(string ipOrHostname, int port, string customNamespace) : this(new HttpDynvoke(), ipOrHostname, port, customNamespace) { }

        public HttpDynvokeServer (HttpDynvoke httpDynvoke, string ipOrHostname, int port, string customNamespace)
        {
            HttpDynvoke = httpDynvoke;
            this.customNamespace = customNamespace;

            listener = new HttpListener ();
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Prefixes.Add ("http://" + ipOrHostname + ":" + port + "/");

            this.listenThread = new Thread (new ParameterizedThreadStart (ListenerThread));
        }

        public void FindTargets() { this.HttpDynvoke.Dynvoke.FindTargets(); }

        public void Start ()
        {
            listenThread.Start ();
        }

        public void Stop ()
        {
            listener.Stop ();

            //wait for all threads to complete handling requests...
            requestProcessingCounter.Signal ();
            requestProcessingCounter.Wait ();

            listener.Close ();
        }

        public void Dispose ()
        {
            this.Stop ();
        }

        private void ListenerThread (object s)
        {
            requestProcessingCounter.Reset ();
            listener.Start ();

            while (listener.IsListening) {
                try {
                    HttpListenerContext result = listener.GetContext ();
                    ThreadPool.QueueUserWorkItem (this.ProcessRequestWorkerThread, result);
                } catch (Exception e) {
                    if (e.GetType () == typeof(HttpListenerException)
                    && e.Message.Contains ("Listener closed")) //throws exception when stopped...
						return; //just ignore it
                }
            }
        }

        private void ProcessRequestWorkerThread (Object threadContext)
        {
            try {
                requestProcessingCounter.AddCount ();

                HttpListenerContext context = (HttpListenerContext)threadContext;

                Log.Debug ("Received request [" + context.Request.RawUrl + "]");

                HandleRequest (context.Request, context.Response);

                context.Response.Close ();
            } finally {
                requestProcessingCounter.Signal ();
            }
        }

        private HttpDynvokeResponse GeneratedJS
        {
            get
            {
                if (_GeneratedJS == null)
                {
                    string generatedJS = JavascriptEndpointGenerator.GenerateJavascript(this.HttpDynvoke.Dynvoke, this.customNamespace);
                    _GeneratedJS = new HttpDynvokeResponse(200, generatedJS, "application/javascript");
                }

                return _GeneratedJS;
            }
        }

        private void HandleRequest (HttpListenerRequest request, HttpListenerResponse response)
        {
            HttpDynvokeResponse dynResp = null;

            string[] parts = request.Url.AbsolutePath.Split (new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (request.Url.AbsolutePath.StartsWith ("/generated.js")) {
                dynResp = GeneratedJS;

            } else if (parts.Length != 2) {
                dynResp = BAD_REQUEST;

            } else {
                HttpDynvokeRequest dynReq = new HttpDynvokeRequest();

                dynReq.Controller = parts [0];
                dynReq.Action = parts [1];
                dynReq.RequestBody = Uri.UnescapeDataString (new StreamReader (request.InputStream, request.ContentEncoding).ReadToEnd ());

                dynResp = this.HttpDynvoke.HandleRequest(dynReq);
            }

            response.StatusCode = dynResp.StatusCode;
            response.ContentType = dynResp.ContentType;
            response.AddHeader("Content-Length", dynResp.ResponseBody.length);
            
            using (StreamWriter writer = new StreamWriter (response.OutputStream))
                writer.Write (dynResp.ResponseBody);
        }
    }
}

