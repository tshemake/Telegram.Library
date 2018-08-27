using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Net.Core.Auth;
using Telegram.Net.Core.MTProto;
using Telegram.Net.Core.Network;
using Telegram.Net.Core.Requests;
using Telegram.Net.Core.Utils;
using MD5 = System.Security.Cryptography.MD5;

namespace Telegram.Net.Core
{
    class DcOptionsCollection
    {
        private readonly Dictionary<int, DcOptionConstructor> dcOptions;

        public bool isEmpty => dcOptions.Count == 0;

        public DcOptionsCollection(List<DcOption> dcOptions)
        {
            this.dcOptions = dcOptions.Cast<DcOptionConstructor>().ToDictionary(dc => dc.id, dc => dc);
        }

        public DcOptionConstructor GetDc(int dcId)
        {
            return dcOptions[dcId];
        }
    }

    public class TelegramClient : IDisposable
    {
        private static readonly int s_apiLayer = 23;
        private static readonly int s_connectionReinitializationTimeoutSeconds = 8;

#if DEBUG
        private static readonly string s_defaultMTProtoServerAddress = "149.154.167.40";
        private static readonly int s_defaultMTProtoServerPort = 443;
#else
        private static readonly string s_defaultMTProtoServerAddress = "149.154.167.50";
        private static readonly int s_defaultMTProtoServerPort = 443;
#endif

        private MtProtoSender _protoSender;

        private readonly IDeviceInfoService _deviceInfo;

        private readonly string _apiHash;
        private readonly int _apiId;
        private readonly Session _session;

        public bool IsConnected { get; set; }
        private bool _isClosed;

        private ConfigConstructor _configuration;
        private DcOptionsCollection _dcOptions;

        public UserSelfConstructor AuthenticatedUser => _session.user.As<UserSelfConstructor>();

        public event EventHandler<ConnectionStateEventArgs> ConnectionStateChanged;
        public event EventHandler<Updates> UpdateMessage;
        public event EventHandler AuthenticationCanceled;

        public TelegramClient(int apiId, string apiHash, string serverAddress, int serverPort, IDeviceInfoService deviceInfo, ISessionStore store)
        {
            if (apiId == 0)
                throw new ArgumentException("API_ID is invalid", nameof(apiId));

            if (string.IsNullOrEmpty(apiHash))
                throw new ArgumentException("API_HASH is invalid", nameof(apiHash));

            this._apiHash = apiHash;
            this._apiId = apiId;
            this._deviceInfo = deviceInfo;

            serverAddress = string.IsNullOrWhiteSpace(serverAddress) ? s_defaultMTProtoServerAddress : serverAddress;
            serverPort = serverPort == 0 ? s_defaultMTProtoServerPort : serverPort;

            _session = Session.TryLoadOrCreateNew(serverAddress, serverPort, store);
        }

        public async Task<bool> Start()
        {
            try
            {
                await ReconnectImpl();
                return true;
            }
            catch (Exception)
            {
                StartReconnecting().IgnoreAwait();
                return false;
            }
        }
        public async Task SendRpcRequest(MtProtoRequest request, bool throwOnError = true)
        {
            if (_isClosed)
                throw new ObjectDisposedException("TelegramClient is closed");

            await _protoSender.Send(request);

            // handle errors that can be fixed without user interaction
            if (request.Error == RpcRequestError.IncorrectServerSalt)
            {
                // assuming that salt was already updated by underlying layer
                Debug.WriteLine("IncorrectServerSalt. Resolving by resending message");

                request.ResetError();
                await _protoSender.Send(request);
            }

            if (request.Error == RpcRequestError.MessageSeqNoTooLow)
            {
                Debug.WriteLine("MessageSeqNoTooLow. Resoliving by resetting session and resending message");
                _session.Reset();

                request.ResetError();
                await _protoSender.Send(request);
            }

            if (request.Error == RpcRequestError.Unauthorized)
            {
                Debug.WriteLine("Invalid authorization");

                _session.ResetAuth();
                OnAuthenticationCanceled();
            }

            _session.Save();

            // escalate
            if (throwOnError)
            {
                request.ThrowIfHasError();
            }
        }

        private void OnUserAuthenticated(User user, int sessionExpiration)
        {
            _session.user = user;
            _session.sessionExpires = sessionExpiration;

            _session.Save();
        }
        private void OnProtoSenderBroken(object sender, EventArgs e)
        {
            StartReconnecting().IgnoreAwait(); // no await
        }

        private async Task ReconnectImpl()
        {
            await CloseProto();

            Debug.WriteLine("Creating new transport..");
            if (_session.authKey == null)
            {
                Step3_Response result = await Authenticator.Authenticate(_session.serverAddress, _session.port);

                _session.authKey = result.authKey;
                _session.timeOffset = result.timeOffset;
                _session.salt = result.serverSalt;
            }

            _protoSender = new MtProtoSender(_session);

            Subscribe();
            _protoSender.Start();

            var request = new InitConnectionAndGetConfigRequest(s_apiLayer, _apiId, _deviceInfo);
            await SendRpcRequest(request);

            _configuration = request.config;
            _dcOptions = new DcOptionsCollection(request.config.dcOptions);

            OnConnectionStateChanged(ConnectionStateEventArgs.Connected());
        }
        private async Task StartReconnecting()
        {
            while (!_isClosed)
            {
                try
                {
                    await ReconnectImpl();
                    break;
                }
                catch (Exception ex)
                {
                    _protoSender?.Dispose();

                    Debug.WriteLine($"Failed to initialize connection: {ex.Message}");
                    Debug.WriteLine($"Retrying in {s_connectionReinitializationTimeoutSeconds} seconds..");

                    OnConnectionStateChanged(ConnectionStateEventArgs.Disconnected(s_connectionReinitializationTimeoutSeconds));
                    await Task.Delay(TimeSpan.FromSeconds(s_connectionReinitializationTimeoutSeconds));
                }
            }
        }

        private void Subscribe()
        {
            Unsubscribe();

            _protoSender.Broken += OnProtoSenderBroken;
            _protoSender.UpdateMessage += OnUpdateMessage;
        }
        private void Unsubscribe()
        {
            if (_protoSender != null)
            {
                _protoSender.Broken -= OnProtoSenderBroken;
                _protoSender.UpdateMessage -= OnUpdateMessage;
            }
        }

        private async Task<MtProtoSender> CreateProto(Session protoSession)
        {
            Debug.WriteLine("Creating new transport..");
            if (protoSession.authKey == null)
            {
                var authResult = await Authenticator.Authenticate(protoSession.serverAddress, protoSession.port);

                protoSession.authKey = authResult.authKey;
                protoSession.timeOffset = authResult.timeOffset;
                protoSession.salt = authResult.serverSalt;
            }

            var proto = new MtProtoSender(protoSession, true);

            var initRequest = new InitConnectionAndGetConfigRequest(s_apiLayer, _apiId, _deviceInfo);
            await proto.Send(initRequest);

            return proto;
        }

        private async Task SendRpcRequestInSeparateSession(int dcId, MtProtoRequest request)
        {
            var dc = _dcOptions.GetDc(dcId);
            var newSession = Session.TryLoadOrCreateNew(dc.ipAddress, dc.port);
            if (dcId == _configuration.thisDc) // same dc
            {
                newSession.authKey = _session.authKey;
                newSession.salt = _session.salt;
                newSession.timeOffset = _session.timeOffset;
            }

            using (var proto = await CreateProto(newSession))
            {
                if (dcId != _configuration.thisDc)
                {
                    var exportAuthRequest = new AuthExportAuthorizationRequest(dcId);
                    await SendRpcRequest(exportAuthRequest);
                    var exportedAuth = exportAuthRequest.exportedAuthorization.Cast<AuthExportedAuthorizationConstructor>();

                    var importAuthRequest = new AuthImportAuthorizationRequest(exportedAuth.id, exportedAuth.bytes);
                    await proto.Send(importAuthRequest);
                }

                await proto.Send(request);
                request.ThrowIfHasError();
            }
        }

        #region Auth

        public bool IsUserAuthorized { get => _session.user != null; }

        //auth.checkPhone#6fe51dfb phone_number:string = auth.CheckedPhone;
        /// <summary>
        /// Returns information on whether the passed phone number was registered.
        /// </summary>
        /// <param name="phoneNumber">Phone number in the international format</param>
        /// <returns>The method returns an auth.CheckedPhone type object with information on whether an account with such a phone number has already been registered, as well as whether invitations were sent to this number (using the auth.sendInvites method).</returns>
        public async Task<AuthCheckedPhoneConstructor> CheckPhone(string phoneNumber)
        {
            var authCheckPhoneRequest = new AuthCheckPhoneRequest(phoneNumber);
            await SendRpcRequest(authCheckPhoneRequest);

            // only single implementation available
            return (AuthCheckedPhoneConstructor)authCheckPhoneRequest.checkedPhone;
        }

        //auth.sendCode#768d5f4d phone_number:string sms_type:int api_id:int api_hash:string lang_code:string = auth.SentCode;
        /// <summary>
        /// Sends an confirmation code message to the specified phone number via SMS.
        /// </summary>
        /// <param name="phoneNumber">Phone number in international format</param>
        /// <param name="tokenDestination">Message text type. Possible values:
        /// 0 - message contains a numerical code
        /// 1 (deprecated) - message contains a link { app_name }://{code}
        /// 5 - message sent via Telegram instead of SMS(the (auth.sentAppCode) constructor may be returned in this case)</param>
        /// <param name="langCode">Code for the language used on a client, ISO 639-1 standard.  Parameter added in layer 5.</param>
        /// <returns>The method returns an auth.SentCode object with information on the message sent.</returns>
        public async Task<AuthSentCode> SendCode(string phoneNumber, VerificationCodeDeliveryType tokenDestination, string langCode = "en")
        {
            var request = new AuthSendCodeRequest(phoneNumber, (int)tokenDestination, _apiId, _apiHash, langCode);
            await SendRpcRequest(request, false);

            if (request.Error == RpcRequestError.MigrateDataCenter)
            {
                if (request.ErrorMessage.StartsWith("PHONE_MIGRATE_") ||
                    request.ErrorMessage.StartsWith("NETWORK_MIGRATE_") ||
                    request.ErrorMessage.StartsWith("USER_MIGRATE_"))
                {
                    Debug.WriteLine($"Trying to resolve error: {request.ErrorMessage}.");

                    var dcIdStr = Regex.Match(request.ErrorMessage, @"\d+").Value;
                    var dcId = int.Parse(dcIdStr);

                    // close
                    await CloseProto();

                    // set new dc options
                    var dcOpt = _dcOptions.GetDc(dcId);

                    _session.authKey = null;
                    _session.serverAddress = dcOpt.ipAddress;
                    _session.port = dcOpt.port;

                    try
                    {
                        Debug.WriteLine($"Reconnecting to dc {dcId} - {dcOpt.ipAddress}");
                        await ReconnectImpl();
                    }
                    catch (Exception)
                    {
                        StartReconnecting().IgnoreAwait();
                        throw;
                    }

                    // try one more time
                    request.ResetError();
                    await SendRpcRequest(request);
                }
            }

            request.ThrowIfHasError();
            return request.sentCode;
        }

        //auth.sendCall#3c51564 phone_number:string phone_code_hash:string = Bool;
        /// <summary>
        /// Makes a voice call to the passed phone number. A robot will repeat the confirmation code from a previously sent SMS message.
        /// </summary>
        /// <param name="phoneNumber">Phone number in the international format</param>
        /// <param name="phoneCodeHash">SMS-message ID</param>
        /// <returns></returns>
        public async Task<bool> SendCall(string phoneNumber, string phoneCodeHash)
        {
            var request = new AuthSendCallRequest(phoneNumber, phoneCodeHash);
            await SendRpcRequest(request);

            return request.callSent;
        }

        //auth.signUp#1b067634 phone_number:string phone_code_hash:string phone_code:string first_name:string last_name:string = auth.Authorization;
        /// <summary>
        /// Registers a validated phone number in the system.
        /// </summary>
        /// <param name="phoneNumber">Phone number in the international format</param>
        /// <param name="phoneCodeHash">SMS-message ID</param>
        /// <param name="phoneCode">Valid numerical code from the SMS-message</param>
        /// <param name="firstName">New user first name</param>
        /// <param name="lastName">New user last name</param>
        /// <returns>Returns an auth.Authorization object with information about the new authorization.</returns>
        public async Task<AuthAuthorizationConstructor> SignUp(string phoneNumber, string phoneCodeHash, string phoneCode, string firstName, string lastName)
        {
            var request = new AuthSignUpRequest(phoneNumber, phoneCodeHash, phoneCode, firstName, lastName);
            await SendRpcRequest(request);

            // only single implementation available
            var authorization = (AuthAuthorizationConstructor)request.authorization;
            OnUserAuthenticated(authorization.user, authorization.expires);

            return authorization;
        }

        //auth.signIn#bcd51581 phone_number:string phone_code_hash:string phone_code:string = auth.Authorization;
        /// <summary>
        /// Signs in a user with a validated phone number.
        /// </summary>
        /// <param name="phoneNumber">Phone number in the international format</param>
        /// <param name="phoneCodeHash">SMS-message ID</param>
        /// <param name="code">Valid numerical code from the SMS-message</param>
        /// <returns>Returns an auth.Authorization object with information on the new authorization.</returns>
        public async Task<AuthAuthorizationConstructor> SignIn(string phoneNumber, string phoneCodeHash, string code)
        {
            var request = new AuthSignInRequest(phoneNumber, phoneCodeHash, code);
            await SendRpcRequest(request);

            // only single implementation available
            var authorization = (AuthAuthorizationConstructor)request.authorization;
            OnUserAuthenticated(authorization.user, authorization.expires);

            return authorization;
        }

        // auth.logOut#5717da40 = Bool;
        /// <summary>
        /// Logs out the user.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> LogOut()
        {
            var request = new AuthLogOutRequest();
            await SendRpcRequest(request);

            return request.IsLogOut;
        }

        // auth.resetAuthorizations#9fab0d1a = Bool;
        /// <summary>
        /// Terminates all user's authorized sessions except for the current one. After calling this method it is necessary to reregister the current device using the method account.registerDevice
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ResetAuthorization()
        {
            var request = new AuthResetAuthorizationRequest();
            await SendRpcRequest(request);

            return request.IsDone;
        }

        // auth.sendInvites#771c1d97 phone_numbers:Vector<string> message:string = Bool;
        // auth.exportAuthorization#e5bfffcd dc_id:int = auth.ExportedAuthorization;
        // auth.importAuthorization#e3ef9613 id:int bytes:bytes = auth.Authorization;
        // auth.bindTempAuthKey#cdd42a05 perm_auth_key_id:long nonce:long expires_at:int encrypted_message:bytes = Bool;

        //auth.sendSms#da9f3e8 phone_number:string phone_code_hash:string = Bool;
        /// <summary>
        /// Forces sending an SMS message to the specified phone number. Use this method if auth.sentAppCode was returned as a response to auth.sendCode, but the user can't reach the device with Telegram.
        /// </summary>
        /// <param name="phoneNumber">Phone number in international format</param>
        /// <param name="phoneCodeHash">SMS-message ID</param>
        /// <returns></returns>
        public async Task<bool> SendSms(string phoneNumber, string phoneCodeHash)
        {
            var request = new AuthSendSmsRequest(phoneNumber, phoneCodeHash);
            await SendRpcRequest(request);

            return request.smsSent;
        }

        #endregion

        #region Account

        // TODO
        // account.registerDevice#446c712c token_type:int token:string device_model:string system_version:string app_version:string app_sandbox:Bool lang_code:string = Bool;
        // account.unregisterDevice#65c55b40 token_type:int token:string = Bool;
        // account.updateNotifySettings#84be5b93 peer:InputNotifyPeer settings:InputPeerNotifySettings = Bool;
        // account.getNotifySettings#12b3ad31 peer:InputNotifyPeer = PeerNotifySettings;
        // account.resetNotifySettings#db7e1747 = Bool;
        // account.updateProfile#f0888d68 first_name:string last_name:string = User;

        // account.updateStatus#6628562c offline:Bool = Bool;
        /// <summary>
        /// Updates online user status.
        /// </summary>
        /// <param name="offline">If (boolTrue) is transmitted, user status will change to (userStatusOffline).</param>
        /// <returns></returns>
        public async Task<bool> UpdateStatus(bool offline)
        {
            var request = new UpdateStatusRequest(offline);
            await SendRpcRequest(request);

            return request.IsUserStatusOffline;
        }

        // account.getWallPapers#c04cfac2 = Vector<WallPaper>;
        // account.reportPeer#ae189d5f peer:InputPeer reason:ReportReason = Bool;

        // account.checkUsername#2714d86c username:string = Bool;
        /// <summary>
        /// Validates a username and checks availability.
        /// </summary>
        /// <param name="username">username. Accepted characters: A-z(case-insensitive), 0-9 and underscores. Length: 5-32 characters.</param>
        /// <returns>Return Bool result on whether the passed username can be used.</returns>
        public async Task<bool> CheckUsername(string username)
        {
            var request = new CheckUserNameAvailabilityRequest(username);
            await SendRpcRequest(request);

            return request.isAvailable;
        }

        // account.updateUsername#3e0bdd7c username:string = User;
        // account.getPrivacy#dadbc950 key:InputPrivacyKey = account.PrivacyRules;
        // account.setPrivacy#c9f81ce8 key:InputPrivacyKey rules:Vector<InputPrivacyRule> = account.PrivacyRules;
        // account.deleteAccount#418d4e0b reason:string = Bool;
        // account.getAccountTTL#8fc711d = AccountDaysTTL;
        // account.setAccountTTL#2442485e ttl:AccountDaysTTL = Bool;
        // account.sendChangePhoneCode#a407a8f4 phone_number:string = account.SentChangePhoneCode;
        // account.changePhone#70c32edb phone_number:string phone_code_hash:string phone_code:string = User;
        // account.updateDeviceLocked#38df3532 period:int = Bool;

        #endregion

        #region Users

        // users.getUsers#d91a548 id:Vector<InputUser> = Vector<User>;
        /// <summary>
        /// Returns basic user info according to their identifiers.
        /// </summary>
        /// <param name="ids">List of user identifiers</param>
        /// <returns>Returns basic user info according to their identifiers.</returns>
        public async Task<List<User>> GetUsers(List<InputUser> ids)
        {
            var request = new GetUsersRequest(ids);
            await SendRpcRequest(request);

            return request.users;
        }

        // users.getFullUser#ca30a5b1 id:InputUser = UserFull;
        /// <summary>
        /// Returns extended user info by ID.
        /// </summary>
        /// <param name="user">User ID</param>
        /// <returns>Returns extended user info by ID.</returns>
        public async Task<UserFullConstructor> GetFullUser(InputUser user)
        {
            var request = new GetFullUserRequest(user);
            await SendRpcRequest(request);

            // only single implementation available
            return (UserFullConstructor)request.userFull;
        }

        #endregion

        #region Contacts

        // TODO
        // contacts.getStatuses#c4a353ee = Vector<ContactStatus>;
        public async Task<List<ContactStatus>> GetStatuses()
        {
            var request = new GetContactStatusesRequest();
            await SendRpcRequest(request);

            return request.ContactStatuses;
        }

        // contacts.getContacts#22c6aa08 hash:string = contacts.Contacts;
        /// <summary>
        /// Returns the current user's contact list.
        /// </summary>
        /// <param name="alreadyLoadedContactsIds"></param>
        /// <returns>Returns the current user's contact list.</returns>
        public async Task<ContactsContacts> GetContacts(IEnumerable<int> alreadyLoadedContactsIds = null)
        {
            var request = new GetContactsRequest(alreadyLoadedContactsIds);
            await SendRpcRequest(request);

            return request.contacts;
        }

        // contacts.importContacts#da30b32d contacts:Vector<InputContact> replace:Bool = contacts.ImportedContacts;
        /// <summary>
        /// Imports contacts: saves a full list on the server, adds already registered contacts to the contact list, returns added contacts and their info.
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="replace">If (boolTrue) is transmitted, the version existing on the server will be completely overwritten when the contact list is saved. Otherwise, new entries will be added to the list.</param>
        /// <returns></returns>
        public async Task<ContactsImportedContactsConstructor> ImportContactByPhoneNumber(string phoneNumber, string firstName, string lastName, bool replace = true)
        {
            var inputContact = new InputPhoneContactConstructor(0, phoneNumber, firstName, lastName);
            var request = new ImportContactsRequest(new List<InputContact> { inputContact }, replace);
            await SendRpcRequest(request);

            // only single implementation available
            return (ContactsImportedContactsConstructor)request.importedContacts;
        }

        // contacts.deleteContact#8e953744 id:InputUser = contacts.Link;
        /// <summary>
        /// Deletes a contact from the list.
        /// </summary>
        /// <param name="user"></param>
        /// <returns>The method returns a contacts.Link object containing the new link value for this contact.</returns>
        public async Task<ContactsLink> DeleteContact(InputUser user)
        {
            var request = new DeleteContactRequest(user);
            await SendRpcRequest(request);

            return request.contactsLink;
        }

        // contacts.deleteContacts#59ab389e id:Vector<InputUser> = Bool;
        /// <summary>
        /// Deletes several contacts from the list.
        /// </summary>
        /// <param name="users">User ID list</param>
        /// <returns></returns>
        public async Task<bool> DeleteContacts(List<InputUser> users)
        {
            var request = new DeleteContactsRequest(users);
            await SendRpcRequest(request);

            return request.Done;
        }

        // contacts.block#332b49fc id:InputUser = Bool;
        // contacts.unblock#e54100bd id:InputUser = Bool;
        // contacts.getBlocked#f57c350f offset:int limit:int = contacts.Blocked;
        // contacts.exportCard#84e53737 = Vector<int>;
        // contacts.importCard#4fe196fe export_card:Vector<int> = User;
        // contacts.search#11f812d8 q:string limit:int = contacts.Found;

        // contacts.resolveUsername#bf0131c username:string = User;
        public async Task<User> ResolveUsername(string username)
        {
            var request = new ResolveUsernameRequest(username);
            await SendRpcRequest(request);

            return request.user;
        }

        #endregion

        #region Messages

        // messages.getMessages#4222fa74 id:Vector<int> = messages.Messages;

        // messages.getDialogs#eccf1df6 offset:int max_id:int limit:int = messages.Dialogs;
        /// <summary>
        /// Returns the current user dialog list.
        /// </summary>
        /// <param name="offset">Number of list elements to be skipped</param>
        /// <param name="limit">Number of list elements to be returned</param>
        /// <param name="maxId">If a positive value was transmitted sent, the method will return only dialogs with IDs less than the set one</param>
        /// <returns></returns>
        public async Task<MessagesDialogs> GetDialogs(int offset, int limit, int maxId = 0)
        {
            var request = new GetDialogsRequest(offset, maxId, limit);
            await SendRpcRequest(request);

            return request.messagesDialogs;
        }

        // messages.getHistory#92a1df2f peer:InputPeer offset:int max_id:int limit:int = messages.Messages;
        /// <summary>
        /// Returns message history for a chat.
        /// </summary>
        /// <param name="inputPeer">Target user or group</param>
        /// <param name="offset">Number of list elements to be skipped. As of Layer 15 this value is added to the one that was calculated from max_id.Negative values are also accepted.</param>
        /// <param name="limit">Number of list elements to be returned</param>
        /// <param name="maxId">If a positive value was transferred, the method will return only messages with IDs less than max_id</param>
        /// <returns>Returns message history for a chat.</returns>
        public async Task<MessagesMessages> GetHistory(InputPeer inputPeer, int offset, int limit, int maxId = -1)
        {
            var request = new GetHistoryRequest(inputPeer, offset, maxId, limit);
            await SendRpcRequest(request);

            return request.messages;
        }

        public async Task<MessagesMessages> GetHistoryForContact(int userId, int offset = 0, int limit = int.MaxValue, int maxId = -1)
        {
            return await GetHistory(new InputPeerContactConstructor(userId), offset, limit, maxId);
        }
        public async Task<MessagesMessages> GetHistoryForForeignContact(int userId, long accessHash, int offset = 0, int limit = int.MaxValue, int maxId = -1)
        {
            return await GetHistory(new InputPeerForeignConstructor(userId, accessHash), offset, limit, maxId);
        }
        public async Task<MessagesMessages> GetHistoryForChat(int chatId, int offset = 0, int limit = int.MaxValue, int maxId = -1)
        {
            return await GetHistory(new InputPeerChatConstructor(chatId), offset, limit, maxId);
        }

        // messages.search#7e9f2ab peer:InputPeer q:string filter:MessagesFilter min_date:int max_date:int offset:int max_id:int limit:int = messages.Messages;

        // messages.readHistory#eed884c6 peer:InputPeer max_id:int offset:int read_contents:Bool = messages.AffectedHistory;
        /// <summary>
        /// Marks message history as read.
        /// </summary>
        /// <param name="inputPeer">Target user or group</param>
        /// <param name="offset">Value from (messages.affectedHistory) or 0</param>
        /// <param name="maxId">If a positive value is passed, only messages with identifiers less or equal than the given one will be read</param>
        /// <param name="readContents">(boolTrue) if the client doesn't support the new read status for multimedia. Parameter added in Layer 17.</param>
        /// <returns></returns>
        public async Task<MessagesAffectedHistory> ReadHistory(InputPeer inputPeer, int offset, int maxId = -1, bool readContents = true)
        {
            var request = new MarkHistoryAsReadRequest(inputPeer, offset, maxId, readContents);
            await SendRpcRequest(request);

            return request.affectedHistory;
        }

        // messages.deleteHistory#f4f8fb61 peer:InputPeer offset:int = messages.AffectedHistory;

        // messages.deleteMessages#14f2dd0a id:Vector<int> = Vector<int>;
        /// <summary>
        /// Deletes messages by their identifiers.
        /// </summary>
        /// <param name="messageIdsToDelete">Message ID list</param>
        /// <returns>The method returns the list of successfully deleted messages in Vector<int>.</returns>
        public async Task<List<int>> DeleteMessages(List<int> messageIdsToDelete)
        {
            var request = new DeleteMessagesRequest(messageIdsToDelete);
            await SendRpcRequest(request);

            return request.deletedMessageIds;
        }

        // messages.receivedMessages#28abcb68 max_id:int = Vector<int>;
        /// <summary>
        /// Confirms receipt of messages by a client, cancels PUSH-notification sending.
        /// </summary>
        /// <param name="maxId">Maximum message ID available in a client.</param>
        /// <returns>The method returns the list of message IDs, for which PUSH-notifications in Vector<int> were cancelled.</returns>
        public async Task<List<int>> ReceivedMessages(int maxId)
        {
            var request = new ReceivedMessagesRequest(maxId);
            await SendRpcRequest(request);

            return request.PushNotificationsCanceledForIds;
        }

        // messages.setTyping#a3825e50 peer:InputPeer action:SendMessageAction = Bool;
        /// <summary>
        /// Sends a current user typing event (see SendMessageAction for all event types) to a conversation partner or group.
        /// </summary>
        /// <param name="inputPeer">Target user or group</param>
        /// <param name="action">Type of action. Parameter added in Layer 17.</param>
        /// <returns></returns>
        public async Task<bool> SetTyping(InputPeer inputPeer, SendMessageAction action)
        {
            var request = new SetTypingRequest(inputPeer, action);
            await SendRpcRequest(request);

            return request.state;
        }

        // messages.sendMessage#4cde0aab peer:InputPeer message:string random_id:long = messages.SentMessage;
        /// <summary>
        /// Sends a text message.
        /// </summary>
        /// <param name="inputPeer">User or chat where a message will be sent</param>
        /// <param name="message">Message text</param>
        /// <returns></returns>
        public async Task<SentMessage> SendMessage(InputPeer inputPeer, string message)
        {
            var request = new SendMessageRequest(inputPeer, message);
            await SendRpcRequest(request);

            return request.sentMessage;
        }
        public async Task<SentMessage> SendDirectMessage(int userId, string message)
        {
            var request = new SendMessageRequest(new InputPeerContactConstructor(userId), message);
            await SendRpcRequest(request);

            return request.sentMessage;
        }
        public async Task<SentMessage> SendChatMessage(int chatId, string message)
        {
            var request = new SendMessageRequest(new InputPeerChatConstructor(chatId), message);
            await SendRpcRequest(request);

            return request.sentMessage;
        }
        public async Task<SentMessage> SendMessageToForeignContact(int id, long accessHash, string message)
        {
            var request = new SendMessageRequest(new InputPeerForeignConstructor(id, accessHash), message);
            await SendRpcRequest(request);

            return request.sentMessage;
        }

        // messages.sendMedia#a3c85d76 peer:InputPeer media:InputMedia random_id:long = messages.StatedMessage;
        /// <summary>
        /// Sends a non-text message.
        /// </summary>
        /// <param name="inputPeer">User or group to receive the message</param>
        /// <param name="media">Message contents</param>
        /// <returns></returns>
        public async Task<MessagesStatedMessage> SendMediaMessage(InputPeer inputPeer, InputMedia media)
        {
            var request = new SendMediaRequest(inputPeer, media);
            await SendRpcRequest(request);

            return request.statedMessage;
        }

        // messages.forwardMessages#514cd10f peer:InputPeer id:Vector<int> = messages.StatedMessages;
        // messages.reportSpam#cf1592db peer:InputPeer = Bool;
        // messages.hideReportSpam#a8f1709b peer:InputPeer = Bool;
        // messages.getPeerSettings#3672e09c peer:InputPeer = PeerSettings;

        // messages.getChats#3c6aa187 id:Vector<int> = messages.Chats;

        // messages.getFullChat#3b831c66 chat_id:int = messages.ChatFull;
        /// <summary>
        /// Returns full chat info according to its ID.
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        /// <returns>Returns full chat info according to its ID.</returns>
        public async Task<ChatFull> GetFullChat(int chatId)
        {
            var request = new GetFullChatRequest(chatId);

            await SendRpcRequest(request);
            return request.chatFull;
        }

        // messages.editChatTitle#b4bc68b5 chat_id:int title:string = messages.StatedMessage;
        // messages.editChatPhoto#d881821d chat_id:int photo:InputChatPhoto = messages.StatedMessage;

        // messages.addChatUser#2ee9ee9e chat_id:int user_id:InputUser fwd_limit:int = messages.StatedMessage;
        /// <summary>
        /// Adds a user to a chat and sends a service message on it.
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        /// <param name="user">User ID to be added</param>
        /// <param name="messagesToForward">Number of last messages to be forwarded</param>
        /// <returns></returns>
        public async Task<MessagesStatedMessage> AddChatUser(int chatId, InputUser user, int messagesToForward)
        {
            var request = new AddChatUserRequest(chatId, user, messagesToForward);
            await SendRpcRequest(request);

            return request.statedMessage;
        }

        // messages.deleteChatUser#c3c5cd23 chat_id:int user_id:InputUser = messages.StatedMessage;
        /// <summary>
        /// Deletes a user from a chat and sends a service message on it.
        /// </summary>
        /// <param name="chatId">Chat ID</param>
        /// <param name="userToDelete">User ID to be deleted</param>
        /// <returns>Returns a messages.StatedMessage object containing a service message sent during the action.</returns>
        public async Task<MessagesStatedMessage> DeleteChatUser(int chatId, InputUser userToDelete)
        {
            var request = new DeleteChatUserRequest(chatId, userToDelete);
            await SendRpcRequest(request);

            return request.statedMessage;
        }
        public async Task<MessagesStatedMessage> LeaveChat(int chatId)
        {
            var request = new DeleteChatUserRequest(chatId, new InputUserContactConstructor(AuthenticatedUser.id));
            await SendRpcRequest(request);

            return request.statedMessage;
        }

        // messages.createChat#419d9aee users:Vector<InputUser> title:string = messages.StatedMessage;
        /// <summary>
        /// Creates a new chat.
        /// </summary>
        /// <param name="title">List of user IDs to be invited</param>
        /// <param name="usersToInvite">Chat name</param>
        /// <returns>Returns a messages.StatedMessage object containing a service message sent sent during an action.</returns>
        public async Task<MessagesStatedMessage> CreateChat(string title, List<InputUser> usersToInvite)
        {
            var request = new CreateChatRequest(usersToInvite, title);
            await SendRpcRequest(request);

            return request.statedMessage;
        }

        // messages.forwardMessage#3f3f4f2 peer:InputPeer id:int random_id:long = messages.StatedMessage;
        // messages.getDhConfig#26cf8950 version:int random_length:int = messages.DhConfig;
        // messages.requestEncryption#f64daf43 user_id:InputUser random_id:int g_a:bytes = EncryptedChat;
        // messages.acceptEncryption#3dbc0415 peer:InputEncryptedChat g_b:bytes key_fingerprint:long = EncryptedChat;
        // messages.discardEncryption#edd923c5 chat_id:int = Bool;
        // messages.setEncryptedTyping#791451ed peer:InputEncryptedChat typing:Bool = Bool;
        // messages.readEncryptedHistory#7f4b690a peer:InputEncryptedChat max_date:int = Bool;
        // messages.sendEncrypted#a9776773 peer:InputEncryptedChat random_id:long data:bytes = messages.SentEncryptedMessage;
        // messages.sendEncryptedFile#9a901b66 peer:InputEncryptedChat random_id:long data:bytes file:InputEncryptedFile = messages.SentEncryptedMessage;
        // messages.sendEncryptedService#32d439a4 peer:InputEncryptedChat random_id:long data:bytes = messages.SentEncryptedMessage;
        // messages.receivedQueue#55a5bb66 max_qts:int = Vector<long>;
        // messages.readMessageContents#354b5bc2 id:Vector<int> = Vector<int>;
        // messages.getStickers#ae22e045 emoticon:string hash:string = messages.Stickers;
        // messages.getAllStickers#aa3bc868 hash:string = messages.AllStickers;

        #endregion

        #region Updates

        public async Task<UpdateReadMessagesConstructor> UpdateReadMessages(List<int> messages, int pts)
        {
            var request = new UpdateReadMessagesRequest(messages, pts);
            await SendRpcRequest(request);

            return (UpdateReadMessagesConstructor)request.eventsOccured;
        }

        // updates.getState#edd4882a = updates.State;
        /// <summary>
        /// Returns a current state of updates.
        /// </summary>
        /// <returns></returns>
        public async Task<UpdatesStateConstructor> GetUpdatesState()
        {
            var request = new GetUpdatesStateRequest();
            await SendRpcRequest(request);

            // only single implementation available
            return (UpdatesStateConstructor)request.updatesState;
        }

        // updates.getDifference#a041495 pts:int date:int qts:int = updates.Difference;
        /// <summary>
        /// Returns diffetence between the current state of updates and transmitted.
        /// </summary>
        /// <param name="pts">The most relevant value of parameter pts of (updates.state), (updateNewMessage), (updateReadMessages), (updateDeleteMessages) или (updateRestoreMessages)</param>
        /// <param name="date">The most relevant value of parameter date of (updates.state), (updateShort) или (updates)</param>
        /// <param name="qts">The most relevant value of parameter qts of (updates.state), (updateNewEncryptedMessage) -1 if end-to-end encryption is not supported. Parameter was added in eigth layer.</param>
        /// <returns></returns>
        public async Task<UpdatesDifference> GetUpdatesDifference(int pts, int date, int qts)
        {
            var request = new GetUpdatesDifferenceRequest(pts, date, qts);
            await SendRpcRequest(request);

            // only single implementation available
            return request.updatesDifference;
        }

        #endregion

        #region Photos

        // photos.updateProfilePhoto#eef579a0 id:InputPhoto crop:InputPhotoCrop = UserProfilePhoto;
        // photos.uploadProfilePhoto#d50f9c88 file:InputFile caption:string geo_point:InputGeoPoint crop:InputPhotoCrop = photos.Photo;
        // photos.deletePhotos#87cf7f2f id:Vector<InputPhoto> = Vector<long>;
        // photos.getUserPhotos#b7ee553c user_id:InputUser offset:int max_id:int limit:int = photos.Photos;

        #endregion

        #region Upload

        // upload.saveFilePart#b304a621 file_id:long file_part:int bytes:bytes = Bool;
        /// <summary>
        /// Saves a part of file for futher sending to one of the methods.
        /// </summary>
        /// <param name="fileId">Random file identifier created by the client</param>
        /// <param name="filePart">Numerical order of a part</param>
        /// <param name="bytes">Binary data, contend of a part</param>
        /// <returns></returns>
        public async Task<bool> SaveFilePart(long fileId, int filePart, byte[] bytes)
        {
            var request = new SaveFilePartRequest(fileId, filePart, bytes, 0, bytes.Length);
            await SendRpcRequest(request);

            return request.done;
        }
        public async Task<InputFileConstructor> UploadFile(string name, Stream content)
        {
            var buffer = new byte[65536];
            var fileId = BitConverter.ToInt64(Helpers.GenerateRandomBytes(8), 0);

            int partsCount = 0;
            int bytesRead;
            while ((bytesRead = content.Read(buffer, 0, buffer.Length)) > 0)
            {
                var request = new SaveFilePartRequest(fileId, partsCount, buffer, 0, bytesRead);
                await SendRpcRequest(request);

                partsCount++;

                if (request.done == false)
                    throw new InvalidOperationException($"Failed to upload file({name}) part: {partsCount})");
            }

            var md5Checksum = string.Empty;
            if (content.CanSeek)
            {
                content.Position = 0;

                using (var md5 = MD5.Create())
                {
                    var hash = md5.ComputeHash(content);
                    var hashResult = new StringBuilder(hash.Length * 2);

                    foreach (byte b in hash)
                        hashResult.Append(b.ToString("x2"));

                    md5Checksum = hashResult.ToString();
                }
            }

            return new InputFileConstructor(fileId, partsCount, name, md5Checksum);
        }

        // upload.getFile#e3a6cfb5 location:InputFileLocation offset:int limit:int = upload.File;
        /// <summary>
        /// Returns content of a whole file or its part.
        /// </summary>
        /// <param name="fileLocation">File location</param>
        /// <param name="offset">Number of bytes to be skipped</param>
        /// <param name="limit">Number of bytes to be returned</param>
        /// <returns>Returns content of a whole file or its part.</returns>
        public async Task<UploadFileConstructor> GetFile(FileLocationConstructor fileLocation, int offset, int limit)
        {
            var request = new GetFileRequest(new InputFileLocationConstructor(fileLocation.volumeId, fileLocation.localId, fileLocation.secret), offset, limit);
            await SendRpcRequestInSeparateSession(fileLocation.dcId, request);

            // only single implementation available
            return (UploadFileConstructor)request.file;
        }
        public async Task<UploadFileConstructor> GetFile(int dcId, InputFileLocation inputFileLocation, int offset, int limit)
        {
            var request = new GetFileRequest(inputFileLocation, offset, limit);
            await SendRpcRequestInSeparateSession(dcId, request);

            // only single implementation available
            return (UploadFileConstructor)request.file;
        }

        // upload.saveBigFilePart#de7b673d file_id:long file_part:int file_total_parts:int bytes:bytes = Bool;

        #endregion

        #region Help

        // help.getConfig#c4f9186b = Config;
        // help.getNearestDc#1fb33026 = NearestDc;
        // help.getAppUpdate#c812ac7e device_model:string system_version:string app_version:string lang_code:string = help.AppUpdate;
        // help.saveAppLog#6f02f748 events:Vector<InputAppEvent> = Bool;
        // help.getInviteText#a4a95186 lang_code:string = help.InviteText;
        // help.getSupport#9cdf08cd = help.Support;

        #endregion

        private void OnUpdateMessage(object sender, Updates e)
        {
            UpdateMessage?.Invoke(this, e);
        }
        private void OnAuthenticationCanceled()
        {
            Debug.WriteLine("Authentication was canceled");
            AuthenticationCanceled?.Invoke(this, EventArgs.Empty);
        }
        private void OnConnectionStateChanged(ConnectionStateEventArgs e)
        {
            IsConnected = e.isConnected;
            Debug.WriteLine($"Connection status: {(e.isConnected ? "connected" : "disconnected")}");
            ConnectionStateChanged?.Invoke(this, e);
        }

        private async Task CloseProto()
        {
            DisposeProto();
            if (_protoSender != null)
                await _protoSender.finishedListeningTask;
        }

        private void DisposeProto()
        {
            if (_protoSender != null)
            {
                Debug.WriteLine("Closing current transport");

                Unsubscribe();
                _protoSender.Dispose();
            }
        }

        public void Dispose()
        {
            _isClosed = true;
            DisposeProto();
        }
    }
}
