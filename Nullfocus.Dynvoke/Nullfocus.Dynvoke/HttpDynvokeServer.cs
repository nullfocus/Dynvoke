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

        private static readonly DynvokeResponse BAD_REQUEST = new DynvokeResponse (400, "Bad Request");

        private Dynvoke dynvoke = null;
        private DynvokeResponse GENERATED_JS = null;
        private Thread listenThread = null;
        private HttpListener listener = null;
        private CountdownEvent requestProcessingCounter = new CountdownEvent (1);

        public HttpDynvokeServer (string ipOrHostname, int port, string customNamespace) : this (ipOrHostname, port, customNamespace, null)
        {
        }

        public HttpDynvokeServer (string ipOrHostname, int port, string customNamespace, IParameterFilter filter)
        { 
            dynvoke = new Dynvoke (filter);

            JavascriptEndpointGenerator gen = new JavascriptEndpointGenerator (dynvoke);
            string generatedJS = gen.GenerateJavascript (customNamespace);
            GENERATED_JS = new DynvokeResponse (200, generatedJS, "application/javascript");

            listener = new HttpListener ();
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            listener.Prefixes.Add ("http://" + ipOrHostname + ":" + port + "/");

            this.listenThread = new Thread (new ParameterizedThreadStart (ListenerThread));
        }

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

        private void HandleRequest (HttpListenerRequest request, HttpListenerResponse response)
        {
            DynvokeResponse dynResp = null;

            string[] parts = request.Url.AbsolutePath.Split (new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (request.Url.AbsolutePath.StartsWith ("/generated.js")) {
                dynResp = GENERATED_JS;

            } else if (parts.Length != 2) {
                dynResp = BAD_REQUEST;

            } else {
                DynvokeRequest dynReq = new DynvokeRequest ();

                dynReq.Controller = parts [0];
                dynReq.Action = parts [1];
                dynReq.RequestBody = Uri.UnescapeDataString (new StreamReader (request.InputStream, request.ContentEncoding).ReadToEnd ());

                dynResp = dynvoke.HandleRequest (dynReq);
            }

            response.StatusCode = dynResp.StatusCode;
            response.ContentType = dynResp.ContentType;

            using (StreamWriter writer = new StreamWriter (response.OutputStream))
                writer.Write (dynResp.ResponseBody);
        }
    }
}

