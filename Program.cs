using System;
using System.Text;

using MyWebServer;

namespace ConsoleWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting server...");
            Server.onError = ErrorHandler;

            // Always authorize the request
            Server.onRequest = (session, context) =>
              {
                  session.Authorized = true;
                  session.UpdateLastConnectionTime();
              };

            // register route handlers:
            Server.AddRoute(new Route()
            {
                Verb = Router.POST,
                Path = "/demo/redirect",
                Handler = new AuthenticatedRouteHandler(RedirectMe)
            });
            Server.AddRoute(new Route()
            {
                Verb = Router.POST,
                Path = "/demo/ajax",
                Handler = new AnonymousRouteHandler(AjaxResponder)
            });

            Server.Start("./Website");
            Console.WriteLine("Press any key to stop server");
            Console.ReadKey();
        }

        public static ResponsePacket RedirectMe(Session session, Dictionary<string, string> parms)
        {
            return Server.Redirect("/demo/clicked");
        }

        public static ResponsePacket AjaxResponder(Session session, Dictionary<string, string> parms)
        {
            string data = "You said " + parms["number"];
            ResponsePacket ret = new ResponsePacket() { Data = Encoding.UTF8.GetBytes(data), ContentType = "text" };

            return ret;
        }

        public static string ErrorHandler(Server.ServerError error)
        {
            string ret = null;

            switch (error)
            {
                case Server.ServerError.ExpiredSession:
                    ret = "/ErrorPages/expiredSession.html";
                    break;
                case Server.ServerError.FileNotFound:
                    ret = "/ErrorPages/fileNotFound.html";
                    break;
                case Server.ServerError.NotAuthorized:
                    ret = "/ErrorPages/notAuthorized.html";
                    break;
                case Server.ServerError.PageNotFound:
                    ret = "/ErrorPages/pageNotFound.html";
                    break;
                case Server.ServerError.ServerError:
                    ret = "/ErrorPages/serverError.html";
                    break;
                case Server.ServerError.UnknownType:
                    ret = "/ErrorPages/unknownType.html";
                    break;
            }

            return ret;
        }
    }
}