using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Core.Utils;

namespace Telegram.Net.Core.Network
{
    public class TcpTransport : ITcpTransport
    {
        private readonly ConcurrentQueue<TcpMessage> _messageQueue =
            new ConcurrentQueue<TcpMessage>();

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private int _messageSequenceNumber;

        public ITcpService TcpService { get; set; }

        public TcpTransport()
        {
            TcpService = new TcpService();
        }

        public async Task<TcpMessage> Receieve()
        {
            await AsyncHelper.RedirectToThreadPool();

            var cancellationToken = default(CancellationToken);

            // packet length
            var packetLengthBytes = new byte[4];

            var readLenghtBytes = await TcpService.Read(packetLengthBytes, 0, 4, cancellationToken);
            if (readLenghtBytes != 4)
            {
                throw new InvalidOperationException("Couldn't read the packet length");
            }

            int packetLength = BitConverter.ToInt32(packetLengthBytes, 0);

            // seq
            var seqBytes = new byte[4];
            var readSeqBytes = await TcpService.Read(seqBytes, 0, 4, cancellationToken);

            if (readSeqBytes != 4)
            {
                throw new InvalidOperationException("Couldn't read the sequence");
            }

            int mesSeqNo = BitConverter.ToInt32(seqBytes, 0);

            Debug.WriteLine($"Recieve message with seq_no {mesSeqNo}");

            // body
            if (packetLength < 12)
            {
                throw new InvalidOperationException("Invalid packet length");
            }

            var neededToRead = packetLength - 12;
            var bodyBytes = new byte[neededToRead];
            var readBodyBytes = await TcpService.Read(bodyBytes, 0, neededToRead, cancellationToken);
            if (readBodyBytes != neededToRead)
            {
                throw new InvalidOperationException("Couldn't read the crc");
            }

            byte[] rv = new byte[packetLengthBytes.Length + seqBytes.Length + bodyBytes.Length];

            Buffer.BlockCopy(packetLengthBytes, 0, rv, 0, packetLengthBytes.Length);
            Buffer.BlockCopy(seqBytes, 0, rv, packetLengthBytes.Length, seqBytes.Length);
            Buffer.BlockCopy(bodyBytes, 0, rv, packetLengthBytes.Length + seqBytes.Length, bodyBytes.Length);

            // crc
            var crcBytes = new byte[4];
            var readCrcBytes = await TcpService.Read(crcBytes, 0, 4, cancellationToken);
            if (readCrcBytes != 4)
            {
                throw new InvalidOperationException("Couldn't read the crc");
            }

            int checksum = BitConverter.ToInt32(crcBytes, 0);
            if (!IsValidChecksum(rv, checksum))
            {
                throw new InvalidOperationException("invalid checksum! skip");
            }

            return new TcpMessage(mesSeqNo, bodyBytes);
        }

        private static bool IsValidChecksum(byte[] block, int checksum)
        {
            var crc32 = new Ionic.Crc.CRC32();
            crc32.SlurpBlock(block, 0, block.Length);
            var validChecksum = crc32.Crc32Result;

            return checksum == validChecksum;
        }

        public async Task<Task<bool>> Send(byte[] packet, CancellationToken cancellationToken = default(CancellationToken))
        {
            await AsyncHelper.RedirectToThreadPool();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            TcpMessage tcpMessage = new TcpMessage(_messageSequenceNumber, packet, tcs, cancellationToken);
            await PushToQueue(tcpMessage);

            return tcs.Task;
        }

        private async Task PushToQueue(TcpMessage message)
        {
            await AsyncHelper.RedirectToThreadPool();

            _messageQueue.Enqueue(message);
            await SendAllMessagesFromQueue();
        }

        private async Task SendAllMessagesFromQueue()
        {
            await AsyncHelper.RedirectToThreadPool();

            await _semaphoreSlim.WaitAsync().ContinueWith(
                async _ =>
                {
                    if (!_messageQueue.IsEmpty)
                    {
                        try
                        {
                            await SendFromQueue();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to send confim message", ex);
                            throw;
                        }
                        finally
                        {
                            _semaphoreSlim.Release();
                        }
                    }
                    else
                    {
                        _semaphoreSlim.Release();
                    }
                });
        }

        private async Task SendFromQueue()
        {
            await AsyncHelper.RedirectToThreadPool();

            while (!_messageQueue.IsEmpty)
            {
                _messageQueue.TryDequeue(out var item);
                TcpMessage message = item;

                try
                {
                    await SendMessage(message);
                    message.TaskCompletionSource.SetResult(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Sending confirmation for messages failed", ex);

                    message.TaskCompletionSource.SetException(ex);
                }
            }
        }

        public async Task SendMessage(TcpMessage tcpMessage)
        {
            await AsyncHelper.RedirectToThreadPool();

            var mesSeqNo = _messageSequenceNumber++;

            Debug.WriteLine($"Sending message with seq_no {mesSeqNo}");

            var encodedMessage = tcpMessage.Encode();

            await TcpService.Send(encodedMessage, tcpMessage.CancellationToken);
        }

        public void Disconnect()
        {
            try
            {
                _messageSequenceNumber = 0;
                TcpService.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception on socket dispose: {ex}");
            }
        }

        #region IDisposable Members

        bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                TcpService.Dispose();
            }

            _disposed = true;
        }

        ~TcpTransport()
        {
            Dispose(false);
        }
        #endregion
    }
}
