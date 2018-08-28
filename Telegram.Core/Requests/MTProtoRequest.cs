using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Telegram.Net.Core.MTProto;
using Telegram.Net.Core.Network.Exceptions;

namespace Telegram.Net.Core.Requests
{
    public abstract class MtProtoRequest
    {
        protected abstract uint requestCode { get; }

        protected MtProtoRequest()
        {
            Sent = false;
        }

        public RpcRequestError Error { get; private set; }
        public string ErrorMessage { get; private set; }

        public long MessageId { get; set; }
        public int Sequence { get; set; }

        public bool Dirty { get; set; }

        public bool Sent { get; private set; }
        public DateTime SendTime { get; private set; }
        public bool ConfirmReceived { get; set; }
        public abstract void OnSend(BinaryWriter writer);
        public abstract void OnResponse(BinaryReader reader);
        public virtual void OnError(int errorCode, string errorMessage)
        {
            Error = (RpcRequestError)errorCode;
            ErrorMessage = errorMessage;
        }

        public virtual bool isContentMessage => true;

        public virtual void OnSendSuccess()
        {
            SendTime = DateTime.Now;
            Sent = true;
        }

        public virtual void OnConfirm()
        {
            ConfirmReceived = true;
        }

        public bool NeedResend => Dirty || (isContentMessage && !ConfirmReceived && DateTime.Now - SendTime > TimeSpan.FromSeconds(3));

        public void ResetError()
        {
            Error = RpcRequestError.None;
            ErrorMessage = null;
        }

        public void ThrowIfHasError()
        {
            if (Error != RpcRequestError.None)
            {
                HandleRpcError();
            }
        }

        private static int ExtractNumber(string line)
        {
            return int.Parse(Regex.Match(line, @"\d+").Value, System.Globalization.NumberStyles.Number);
        }

        private void HandleRpcError()
        {
            // rpc_error

            Debug.WriteLine($"Throwing exception for request {GetType().Name} because of error {Error} - {ErrorMessage}");

            switch (ErrorMessage)
            {
                case var floodMessage when floodMessage.StartsWith("FLOOD_WAIT_"):
                    var seconds = ExtractNumber(floodMessage);
                    throw new FloodWaitException(TimeSpan.FromSeconds(seconds));

                case var phoneMigrate when phoneMigrate.StartsWith("PHONE_MIGRATE_"):
                    var phoneMigrateDcIdx = ExtractNumber(phoneMigrate);
                    throw new PhoneMigrationException(phoneMigrateDcIdx);

                case var fileMigrate when fileMigrate.StartsWith("FILE_MIGRATE_"):
                    var fileMigrateDcIdx = ExtractNumber(fileMigrate);
                    throw new FileMigrationException(fileMigrateDcIdx);

                case var userMigrate when userMigrate.StartsWith("USER_MIGRATE_"):
                    var userMigrateDcIdx = ExtractNumber(userMigrate);
                    throw new UserMigrationException(userMigrateDcIdx);

                case "PHONE_CODE_INVALID":
                    throw new PhoneCodeInvalidException("The numeric code used to authenticate does not match the numeric code sent by SMS/Telegram");

                case "SESSION_PASSWORD_NEEDED":
                    throw new CloudPasswordNeededException("This Account has Cloud Password !");

                case "AUTH_RESTART":
                    throw new AuthRestartException();

                default:
                    throw new TelegramReqestException(Error, ErrorMessage);
            }
        }
    }
}
