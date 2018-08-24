using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Models;

namespace Telegram
{
    class Program
    {
        static readonly ILogger s_logger = LoggerFactory.GetLogger<Program>();
        static Stopwatch s_stopWatch = new Stopwatch();

        static void Main(string[] args)
        {
            long ticksTime = 0;
            Client client = null;
            var username = string.Empty;
            var phoneNumber = string.Empty;
            var message = string.Empty;
            Contact contact = null;

            Init(out client);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        #region Вход
        static void Init(out Client client)
        {
            long ticksTime = 0;
            s_stopWatch.Start();
            client = new Client(new ConfigurationManager(), LoggerFactory.GetLogger<Client>());
            if (!client.IsUserAuthorized)
            {
                client.InitializeAndAuthenticateClientAsync().Wait();
            }
            s_stopWatch.Stop();
            s_stopWatch.Reset();
            s_logger.Info($"Authorized: {client.IsUserAuthorized}, {ticksTime} ticks.");
        }
        #endregion
    }
}
