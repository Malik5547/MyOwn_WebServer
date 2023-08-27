using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Reflection;

using Clifton.Extensions;


namespace MyWebServer
{
    public static class Server
    {

        public enum ServerError
        {
            OK,
            ExpiredSession,
            NotAuthorized,
            FileNotFound,
            PageNotFound,
            ServerError,
            UnknownType,
            ValidationError
        }
        public static int expirationTimeSeconds = 30;

        public static Func<ServerError, string> onError;

        public static int maxSimultaneousConnections = 10;
        private static Semaphore sem = new Semaphore(maxSimultaneousConnections, maxSimultaneousConnections);
        private static HttpListener listener;
        private static Router router = new Router();
        public static Action<Session, HttpListenerContext> onRequest;
        private static SessionManager sessionManager = new SessionManager();

        public static Func<Session, string, string> postProcess = DefaultPostProcess;
        public static string validationTokenScript = "<%AntiForgeryToken%>";
        public static string validationTokenName = "__CSRFToken__";

        // Methods
        public static void Start(string websitePath)
        {
            router.WebsitePath = websitePath;
            List<IPAddress> localhostIPs = GetLocalHostIPs();
            listener = InitializeListener(localhostIPs);
            Start(listener);
        }

        public static void AddRoute(Route route)
        {
            router.AddRoute(route);
        }

        private static List<IPAddress> GetLocalHostIPs()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            List<IPAddress> ret = host.AddressList.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).ToList();

            return ret;
        }

        private static HttpListener InitializeListener(List<IPAddress> localhostIPs)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");

            return listener;
        }

        private static void Start(HttpListener listener)
        {
            listener.Start();
            Task.Run(() => RunServer(listener));
        }

        private static void RunServer(HttpListener listener)
        {
            while (true)
            {
                sem.WaitOne();
                StartConnectionListener(listener);
            }
        }

        private static async void StartConnectionListener(HttpListener listener)
        {
            ResponsePacket resp = new ResponsePacket();

            // Wait for a connection. Return to caller while we wait.
            HttpListenerContext context = await listener.GetContextAsync();
            Session session = sessionManager.GetSession(context.Request.RemoteEndPoint);
            onRequest.IfNotNull(r => r(session, context));

            sem.Release();

            // Log request
            Log(context.Request);

            HttpListenerRequest request = context.Request;
            try
            {
                string path = request.RawUrl.LeftOf("?");
                string verb = request.HttpMethod;
                string parms = request.RawUrl.RightOf("?");
                Dictionary<string, string> kvParams = GetKeyValues(parms);

                string data = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
                GetKeyValues(data, kvParams);
                Log(kvParams);

                resp = router.Route(session, verb, path, kvParams);

                session.UpdateLastConnectionTime();

                if (resp.Error != ServerError.OK)
                {
                    resp.Redirect = onError(resp.Error);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                resp = new ResponsePacket() { Redirect = onError(ServerError.ServerError) };
            }

            // Response
            Respond(request, context.Response, resp);
        }

        public static string GetWebsitePath()
        {
            string websitePath = Assembly.GetExecutingAssembly().Location;
            websitePath = websitePath.LeftOfRightmostOf("\\").LeftOfRightmostOf("\\").LeftOfRightmostOf("\\") + "\\Website\\";

            return websitePath;
        }

        /// <summary>
        /// Return a ResponsePacket with the specified URL and an optional (singular) parameter.
        /// </summary>
        public static ResponsePacket Redirect(string url, string parm = null)
        {
            ResponsePacket ret = new ResponsePacket() { Redirect = url };
            parm.IfNotNull((p) => ret.Redirect += "?" + p);

            return ret;
        }

        private static void Log(HttpListenerRequest request)
        {
            Console.WriteLine(request.RemoteEndPoint + " " + request.HttpMethod + " /" + request.Url.AbsoluteUri.RightOf('/', 2));
        }

        private static void Log(Dictionary<string, string> kv)
        {
            kv.ForEach(kvp => Console.WriteLine(kvp.Key + " : " + kvp.Value));
        }

        private static Dictionary<string, string> GetKeyValues(string data, Dictionary<string, string> kv = null)
        {
            kv.IfNull(() => kv = new Dictionary<string, string>());
            data.If(d => d.Length > 0, (d) => d.Split('&').ForEach(keyValue => kv[keyValue.LeftOf('=')] = keyValue.RightOf('=')));

            return kv;
        }

        private static void Respond(HttpListenerRequest request, HttpListenerResponse response, ResponsePacket resp)
        {
            if (string.IsNullOrEmpty(resp.Redirect))
            {
                // No redirecting 

                if (resp.Data != null)
                {
                    response.ContentType = resp.ContentType;
                    response.ContentLength64 = resp.Data.Length;
                    response.OutputStream.Write(resp.Data, 0, resp.Data.Length);
                    response.ContentEncoding = resp.Encoding;
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Redirect;

                string pageDomain = request.Url.Host;

                if (request.Url.Port != 80)
                {
                    pageDomain += ":" + request.Url.Port;
                }


                response.Redirect("http://" + pageDomain + resp.Redirect);
            }

            response.OutputStream.Close();
        }

        private static string DefaultPostProcess(Session session, string html)
        {
            string ret = html.Replace(validationTokenScript,
              "<input name='" +
              validationTokenName +
              "' type='hidden' value='" +
              session.Objects[validationTokenName].ToString() +
              " id='#__csrf__'" +
              "/>");

            return ret;
        }
    }
}