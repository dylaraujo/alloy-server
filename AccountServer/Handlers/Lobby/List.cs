#region

using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Common.Messaging;

#endregion

namespace AccountServer.Handlers.Lobby;

public class List : RequestHandler {
    public override string Path => "/lobby/list";

    public override Task<string> Handle(string ip, NameValueCollection query) {
        var servers = IpcServer.Worlds.Values
            .OrderBy(w => w.Name)
            .Select(w => new XElement("Server",
                new XElement("Name", w.Name),
                new XElement("DNS", w.Address),
                new XElement("Port", w.Port),
                new XElement("Players", w.PlayerCount),
                new XElement("MaxPlayers", w.MaxPlayers),
                new XElement("AdminOnly", w.AdminOnly ? "true" : "false")));

        return Task.FromResult(new XElement("Servers", servers).ToString());
    }
}
