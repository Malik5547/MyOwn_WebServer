
using System.Text;

namespace MyWebServer
{
    public class ResponsePacket
    {
        public string Redirect { get; set; }
        public byte[] Data { get; set; }
        public string ContentType { get; set; }
        public Encoding Encoding { get; set; }

        public Server.ServerError Error { get; set; }
    }

    internal class ExtensionInfo
    {
        public string ContentType { get; set; }
        public Func<Session, string, string,  ExtensionInfo, ResponsePacket> Loader { get; set; }
    }

}
