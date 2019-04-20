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
            const bool host = false;

            var chatRoomInstanceInfo = await Domains.Current.Channels.GetInstanceOf<ChatRoom>(filter: (isHost, instance) => isHost == host).ConfigureAwait(false);

            await chatRoomInstanceInfo.Instance.SendMessage(host, "Hello server.").ConfigureAwait(false);

            Console.ReadLine();
        }
    }
}
