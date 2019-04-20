using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AppDomainAlternative;
using Common;

namespace HostApp
{
    public static class HostProgram
    {
        public static async Task Main()
        {
            const bool host = true;

            //get the location for the client exe
            var clientAppLocation = typeof(HostProgram).Assembly.Location.Replace("HostApp", "ClientApp");

            var currentDomain = Domains.Current;

            //start the client exe
            var childDomain = currentDomain.AddChildDomain(new ProcessStartInfo("dotnet", $"\"{clientAppLocation}\""));

            //create a shared instance between these processes
            //the instance will be hosted on this process and the client will proxy it calls to the instance to this process
            var chatRoom = (ChatRoom)await childDomain.Channels.CreateInstance(typeof(ChatRoom).GetConstructors().First(), host, "Interprocess Communication Chat").ConfigureAwait(false);

            await chatRoom.SendMessage(host, "Client are you there?").ConfigureAwait(false);

            Console.ReadLine();
        }
    }
}
