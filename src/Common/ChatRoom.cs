using System;
using System.Threading.Tasks;
using AppDomainAlternative;

namespace Common
{
    public class ChatRoom
    {
        public ChatRoom(string name)
        {
            Name = name;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Chat room \"{name}\" started for pid {Domains.Current.Process.Id}.");
        }

        public string Name { get; }

        public virtual Task SendMessage(bool isHost, string message)
        {
            Console.ForegroundColor = isHost ? ConsoleColor.Green : ConsoleColor.Yellow;
            Console.WriteLine($"{(isHost ? "Host" : "Client")}: {message}");
            Console.ForegroundColor = ConsoleColor.Gray;
            return Task.CompletedTask;
        }
    }
}
