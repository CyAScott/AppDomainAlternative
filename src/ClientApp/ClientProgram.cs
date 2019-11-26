using System;
using System.Threading.Tasks;
using AppDomainAlternative;
using Common;

namespace ClientApp
{
    public static class ClientProgram
    {
        public static async Task Main()
        {
            if (Domains.Current.Channels == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Host not found!");
                return;
            }

            const bool host = false;

            var chatRoomInstanceInfo = await Domains.Current.Channels.GetInstanceOf<ChatRoom>(filter: (isHost, instance) => isHost == host).ConfigureAwait(false);

            await chatRoomInstanceInfo.Instance.SendMessage(host, "Hello server.").ConfigureAwait(false);

            Console.ReadLine();
        }
    }
}
