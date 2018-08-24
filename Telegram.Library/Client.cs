using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Net.Core;
using Telegram.Net.Core.MTProto;
using Telegram.Net.Core.Requests;
using TeleSharp.TL;

namespace Telegram
{
    public class Client : IDisposable
    {
        private readonly ILogger _logger;
        private TelegramClient _client;
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1);
        private ICollection<Models.Contact> _contacts = new List<Models.Contact>();
        private UpdatesState _lastInitialState;

        private IConfiguration ConfigurationManager { get; }

        public event EventHandler<Models.Message> UpdateMessage;

        private bool disposed = false;

        public Client(IConfiguration configurationManager, ILogger logger)
        {
            _logger = logger;
            ConfigurationManager = configurationManager;
            _client = GetTelegramClient();
            Subscribe();
        }

        private TelegramClient GetTelegramClient()
        {
            Contract.Ensures(Contract.Result<TelegramClient>() != default(TelegramClient));

            try
            {
                FileSessionStore store = new FileSessionStore { SessionUserId = ConfigurationManager.PhoneNumber };
                IDeviceInfoService device = new DeviceInfoService("PC", "Win 10.0", "1.0.0", "ru");
                return new TelegramClient(ConfigurationManager.ApiId, ConfigurationManager.ApiHash, ConfigurationManager.ServerHost, ConfigurationManager.ServerPort, device, store);
            }
            catch (Exception ex)
            {
                _logger.Error($"For connecting to telegram server use a VPN", ex);
                throw;
            }
        }

        private void Subscribe()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
        }

        private async Task ConnectAsync() => await _client.Start();

        public async Task InitializeAndAuthenticateClientAsync()
        {
            await ConnectAsync();

            if (!IsUserAuthorized)
            {
                Console.WriteLine($"We've sent a code with an activation code to yout phone {ConfigurationManager.PhoneNumber}.");
                AuthSentCode codeRequest = await _client.SendCode(ConfigurationManager.PhoneNumber, VerificationCodeDeliveryType.NumericCodeViaTelegram);
                ConfigurationManager.PhoneCodeHash = codeRequest.phoneCodeHash;

                Console.WriteLine("Code: ");
                var code = Console.ReadLine();

                await _client.SignIn(ConfigurationManager.PhoneNumber, ConfigurationManager.PhoneCodeHash, code);
            }
        }

        public async Task<Models.MessagesDialog> GetDialogsAsync()
        {
            await ConnectAsync();

            int limit = 30;
            Models.MessagesDialog messagesDialog = new Models.MessagesDialog
            {
                Dialogs = new List<Models.Dialog>(),
                Chats = new List<Models.Chat>(),
                Messages = new List<Models.Message>(),
                Contacts = new List<Models.Contact>()
            };
            MessagesDialogs result = await _client.GetDialogs(0, limit);
            if (result is MessagesDialogsConstructor messagesDialogsConstructor)
            {
                messagesDialog.Dialogs.AddRange(messagesDialogsConstructor.dialogs.Select(d => (Models.Dialog)d));
                messagesDialog.Chats.AddRange(messagesDialogsConstructor.chats.Select(c => (Models.Chat)c));
                messagesDialog.Messages.AddRange(messagesDialogsConstructor.messages.Select(m => (Models.Message)m));
                messagesDialog.Contacts.AddRange(messagesDialogsConstructor.users.Select(u => (Models.Contact)u));
            }
            else if (result is MessagesDialogsSliceConstructor messagesDialogsSliceConstructor)
            {
                int count = messagesDialogsSliceConstructor.count;
                int total = limit;
                do
                {
                    messagesDialog.Dialogs.AddRange(messagesDialogsSliceConstructor.dialogs.Select(d => (Models.Dialog)d));
                    messagesDialog.Chats.AddRange(messagesDialogsSliceConstructor.chats.Select(c => (Models.Chat)c));
                    messagesDialog.Messages.AddRange(messagesDialogsSliceConstructor.messages.Select(m => (Models.Message)m));
                    messagesDialog.Contacts.AddRange(messagesDialogsSliceConstructor.users.Select(u => (Models.Contact)u));
                    result = await _client.GetDialogs(total, limit);
                } while (total < count);
            }

            return messagesDialog;
        }

        public async Task<ICollection<Models.Contact>> GetContactsAsync()
        {
            await ConnectAsync();

            ContactsContacts result = await _client.GetContacts();
            ContactsContactsConstructor userContacts = result.As<ContactsContactsConstructor>();
            _contacts = new List<Models.Contact>();
            foreach (User userContact in userContacts.users)
            {
                _contacts.Add((Models.Contact)userContact);
            }
            return _contacts;
        }

        private Models.Contact SearchContactInLocalCache(Func<Models.Contact, bool> predicate)
        {
            if (_contacts != null)
                return _contacts.FirstOrDefault(predicate);
            return null;
        }

        public async Task<Models.Contact> SearchUserByUserNameAsync(string userName)
        {
            string normalizedUserName = userName.TrimStart('@');
            Models.Contact contact = SearchContactInLocalCache(c => string.Equals(c.Username, normalizedUserName, StringComparison.CurrentCultureIgnoreCase));
            if (contact != null)
                return contact;

            await ConnectAsync();

            User resolveUser = await _client.ResolveUsername(normalizedUserName);
            return (Models.Contact)resolveUser;
        }

        public async Task<Models.Contact> GetContactByPhoneNumberAsync(string phoneNumber)
        {
            string normalizedPhoneNumber = phoneNumber.TrimStart('+');
            Models.Contact contact = SearchContactInLocalCache(c => c.Phone.Contains(normalizedPhoneNumber));
            if (contact != null)
                return contact;

            ICollection<Models.Contact> contacts = await GetContactsAsync();

            return contacts.FirstOrDefault(c => c.Phone.Contains(normalizedPhoneNumber));
        }

        public async Task<Models.SentMessage> SendMessageToContact(Models.Contact contact, string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            await ConnectAsync();

            SentMessage sentMessage = null;
            if (contact.IsForeign)
            {
                sentMessage = await _client.SendMessage(new InputPeerForeignConstructor(contact.Id, contact.AccessHash), message);
            }
            else
            {
               sentMessage = await _client.SendMessage(new InputPeerContactConstructor(contact.Id), message);
            }
            return (Models.SentMessage)sentMessage;
        }

        public async Task<bool> IsPhoneNumberRegisteredAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentNullException(nameof(phoneNumber));

            await ConnectAsync();

            string normalizedPhoneNumber = phoneNumber.TrimStart('+');

            AuthCheckedPhoneConstructor checkPhoneResult = await _client.CheckPhone(normalizedPhoneNumber);
            return checkPhoneResult.phoneRegistered;
        }

        public async Task<bool> IsAvailabilityUserNameAsync(string userName)
        {
            string normalizedUserName = userName.TrimStart('@');
            if (!IsValidUserName(normalizedUserName))
                return false;

            await ConnectAsync();

            return await _client.CheckUsername(normalizedUserName);
        }

        public async Task<bool> SetTypingActionAsync(Models.Contact contact)
        {
            await ConnectAsync();

            InputPeer peer = null;
            if (contact.IsForeign)
            {
                peer = new InputPeerForeignConstructor(contact.Id, contact.AccessHash);
            }
            else
            {
                peer = new InputPeerContactConstructor(contact.Id);
            }
            return await _client.SetTyping(peer, SendMessageAction.TypingAction);
        }

        public async Task<bool> SetCancelAllActionAsync(Models.Contact contact)
        {
            await ConnectAsync();

            InputPeer peer = null;
            if (contact.IsForeign)
            {
                peer = new InputPeerForeignConstructor(contact.Id, contact.AccessHash);
            }
            else
            {
                peer = new InputPeerContactConstructor(contact.Id);
            }
            return await _client.SetTyping(peer, SendMessageAction.CancelAction);
        }

        /// <summary>
        /// Gets message history for a chat.
        /// </summary>
        /// <param name="contact">User</param>
        /// <param name="offset">Number of list elements to be skipped. As of Layer 15 this value is added to the one that was calculated from max_id. Negative values are also accepted.</param>
        /// <param name="limit">Number of list elements to be returned</param>
        /// <param name="maxId">If a positive value was transferred, the method will return only messages with IDs less than max_id</param>
        /// <returns>Returns message history for a chat..</returns>
        public async Task<ICollection<Models.Message>> GetHistory(Models.Contact contact, int offset, int limit, int maxId = -1)
        {
            await ConnectAsync();

            InputPeer peer = null;
            if (contact.IsForeign)
            {
                peer = new InputPeerForeignConstructor(contact.Id, contact.AccessHash);
            }
            else
            {
                peer = new InputPeerContactConstructor(contact.Id);
            }

            MessagesMessages result = await _client.GetHistory(peer, offset, limit, maxId);
            MessagesMessagesConstructor history = result.As<MessagesMessagesConstructor>();

            ICollection<Models.Message> messages = new List<Models.Message>();
            foreach (Message historyMessage in history.messages)
            {
                MessageConstructor message = historyMessage.As<MessageConstructor>();
                if (message != null)
                {
                    messages.Add((Models.Message)message);
                }
            }
            return messages;
        }

        public async Task GetUpdatesAsync()
        {
            await ConnectAsync();

            if (_lastInitialState == null)
            {
                _lastInitialState = await _client.GetUpdatesState();
                return;
            }

            UpdatesDifference differenceUpdateResponse = await GetUpdatesDifferenceAsync(_lastInitialState);
            if (differenceUpdateResponse is UpdatesDifferenceConstructor differenceUpdate)
            {
                _lastInitialState = differenceUpdate.state;
                UpdatesStateConstructor updatesStateConstructor = _lastInitialState as UpdatesStateConstructor;
                _logger.Debug(string.Format("{0} Update.GetDifference result=[Seq={1} Date={2} Qts={3} Pts{4}]",
                    DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture), updatesStateConstructor.seq,
                    updatesStateConstructor.date, updatesStateConstructor.qts, updatesStateConstructor.pts));

                if (differenceUpdate.newMessages.Count > 0)
                {
                    IEnumerable<Models.Contact> users = differenceUpdate.users.Select(u => ((Models.Contact)u));
                    IEnumerable<Models.Message> messages = differenceUpdate.newMessages.Select(m => (Models.Message)m);
                    IDictionary<int, int> lastMessageIds = new Dictionary<int, int>();

                    foreach (Models.Message message in messages)
                    {
                        UpdateMessage(this, message);
                        lastMessageIds[message.FromId] = message.Id;
                    }
                    foreach (Models.Contact user in users)
                    {
                        if (lastMessageIds.ContainsKey(user.Id))
                        {
                            await MarkedMessagesAsReadAsync(user, 0, lastMessageIds[user.Id]);
                        }
                    }
                }
            }
            else if (differenceUpdateResponse is UpdatesDifferenceEmptyConstructor)
            {
            }
            else if (differenceUpdateResponse is UpdatesDifferenceSliceConstructor differenceSliceUpdate)
            {
            }
        }

        public async Task<UpdatesDifference> GetUpdatesDifferenceAsync(UpdatesState state)
        {
            UpdatesStateConstructor updatesStateConstructor = state as UpdatesStateConstructor;
            return await _client.GetUpdatesDifference(updatesStateConstructor.pts, updatesStateConstructor.date, updatesStateConstructor.qts);
        }

        /// <summary>
        /// Marks message history as read.
        /// </summary>
        /// <param name="contact">Target user</param>
        /// <param name="offset">Value from (messages.affectedHistory) or 0</param>
        /// <param name="maxId">If a positive value is passed, only messages with identifiers less or equal than the given one will be read</param>
        /// <returns>Object contains info on affected part of communication history with the user or in a chat.</returns>
        public async Task<MessagesAffectedHistory> MarkedMessagesAsReadAsync(Models.Contact contact, int offset, int maxId = -1)
        {
            InputPeer peer = null;

            if (contact.IsForeign)
            {
                peer = new InputPeerForeignConstructor(contact.Id, contact.AccessHash);
            }
            else
            {
                peer = new InputPeerContactConstructor(contact.Id);
            }

            return await _client.ReadHistory(peer, offset, maxId);
        }

        public async Task<List<int>> ReceivedMessagesAsync(int maxId)
        {
            return await _client.ReceivedMessages(maxId);
        }

        private void ConnectionUpdateMessage(object sender, Updates update)
        {
            Console.WriteLine(update);
        }


        /// <summary>
        /// Deletes messages by their identifiers.
        /// </summary>
        /// <param name="messageIdList">Message ID list</param>
        /// <returns>The method returns the list of successfully deleted messages in Task<List<int>>.</returns>
        public async Task<List<int>> DeleteMessagesAsync(params int[] messageIdList)
        {
            if (messageIdList.Length == 0)
                return new List<int>();

            await ConnectAsync();

            return await _client.DeleteMessages(new List<int>(messageIdList));
        }

        /// <summary>
        /// Info on succesfully imported contacts.
        /// </summary>
        /// <param name="phoneNumber">Phone number</param>
        /// <param name="firstName">First name the user assigned to himself</param>
        /// <param name="lastName">Last name the user assigned to himself</param>
        /// <returns></returns>
        public async Task<bool> ImportContactByPhoneNumber(string phoneNumber, string firstName, string lastName)
        {
            string normalizedPhoneNumber = phoneNumber.TrimStart('+');

            await ConnectAsync();

            ContactsImportedContactsConstructor importedConacts = await _client.ImportContactByPhoneNumber(normalizedPhoneNumber, firstName, lastName);
            return (importedConacts != null && importedConacts.importedContacts.Count == 1);
        }

        public async Task<bool> DeleteContactAsync(Models.Contact contact)
        {
            await ConnectAsync();

            bool deleteSuccessfully = false;
            try
            {
                ContactsLink contactsLink = await _client.DeleteContact(new InputUserContactConstructor(contact.Id));
                deleteSuccessfully = true;
            }
            catch (TelegramReqestException ex)
            {
                _logger.Error($"The contact with id `{contact.Id}` can not be found in contacts", ex);
            }
            return deleteSuccessfully;
        }

        public bool IsValidUserName(string userName) => Regex.IsMatch(userName, @"^[a-zA-Z][a-zA-Z0-9]{4,31}$");

        public bool IsUserAuthorized => _client.IsUserAuthorized;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Unsubscribe();
                    _client.Dispose();
                }
                disposed = true;
            }
        }

        ~Client()
        {
            Dispose(false);
        }
    }
}
