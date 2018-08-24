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
        static Client s_client = null;

        static void Main(string[] args)
        {
            #region Вход
            Do(AuthSendCodeAndSignIn, "auth.sendCode, auth.signIn");
            #endregion
            #region Список контактов
            //Do(ContactsGetContacts, "contacts.getContacts");
            #endregion
            #region Поиск пользователя по имени
            //Do(ContactsResolveUsername, "contacts.resolveUsername");
            #endregion
            #region Добавление в контакт
            //Do(ContactsImportContacts, "contacts.importContacts");
            #endregion
            #region Удалить из контактов
            //Do(ContactsDeleteContact, "contacts.deleteContact");
            #endregion
            #region Зарегестрирован в телеграмме?
            Do(AuthСheckPhone, "auth.checkPhone"); // На текущий момент всегда возвращает true
            #endregion

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void Do(Action action, string name)
        {
            long milliseconds = 0;
            try
            {
                s_stopWatch.Start();
                action();
                s_stopWatch.Stop();
                milliseconds = s_stopWatch.ElapsedMilliseconds;
                s_stopWatch.Reset();
            }
            finally
            {
                if (s_stopWatch.IsRunning)
                {
                    s_stopWatch.Stop();
                    milliseconds = s_stopWatch.ElapsedMilliseconds;
                    s_stopWatch.Reset();
                }
                s_logger.Info($"{name}: {milliseconds} milliseconds.");
            }
        }

        /// <summary>
        /// Вход
        /// </summary>
        static void AuthSendCodeAndSignIn()
        {
            s_client = new Client(new ConfigurationManager(), LoggerFactory.GetLogger<Client>());
            if (!s_client.IsUserAuthorized)
            {
                s_client.InitializeAndAuthenticateClientAsync().Wait();
            }
            Console.WriteLine($"Authorized:\n\t{s_client.IsUserAuthorized}.");
        }

        /// <summary>
        /// Список контактов
        /// </summary>
        static void ContactsGetContacts()
        {
            Console.WriteLine("Contacts:\n\t{0}", string.Join("\n\t", s_client.GetContactsAsync().Result));
        }

        /// <summary>
        /// Поиск пользователя по имени
        /// </summary>
        static void ContactsResolveUsername()
        {
            string username = "alexandr";
            Console.WriteLine("Contact by name '{0}':\n\t{1}", username, s_client.SearchUserByUserNameAsync(username).Result);
        }

        /// <summary>
        /// Добавить в контакт
        /// </summary>
        static void ContactsImportContacts()
        {
            string phoneNumber = "79999999999";
            string firstName = "First name";
            string lastName = "Last name";
            bool successfully = s_client.ImportContactByPhoneNumber(phoneNumber, firstName, lastName).Result;
            Console.WriteLine("Number {0} added to contact.", successfully ? "successfully" : "not");
        }

        /// <summary>
        /// Удалить из контактов
        /// </summary>
        static void ContactsDeleteContact()
        {
            string phoneNumber = "79999999999";
            bool successfully = false;
            Contact contact = s_client.GetContactByPhoneNumberAsync(phoneNumber).Result;
            if (contact != null)
            {
                successfully = s_client.DeleteContactAsync(contact).Result;
            }
            Console.WriteLine("Number {0} removed from contact.", successfully ? "successfully" : "not");
        }

        /// <summary>
        /// Зарегестрирован в телеграмме?
        /// </summary>
        static void AuthСheckPhone()
        {
            var phoneNumber = "79999999999";
            Console.WriteLine("Phone number '{0}' was {1}registered.", phoneNumber, s_client.IsPhoneNumberRegisteredAsync(phoneNumber).Result ? "" : "not ");
        }
    }
}
