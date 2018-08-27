using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
            var cancellationToken = default(CancellationToken);

            // packet length
            var packetLengthBytes = new byte[4];

            var readLenghtBytes = await TcpService.Read(packetLengthBytes, 0, 4, cancellationToken).ConfigureAwait(false);
            if (readLenghtBytes != 4)
            {
                throw new InvalidOperationException("Couldn't read the packet length");
            }

            int packetLength = BitConverter.ToInt32(packetLengthBytes, 0);

            // seq
            var seqBytes = new byte[4];
            var readSeqBytes = await TcpService.Read(seqBytes, 0, 4, cancellationToken).ConfigureAwait(false);

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
            var readBodyBytes = await TcpService.Read(bodyBytes, 0, neededToRead, cancellationToken).ConfigureAwait(false);
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
            var readCrcBytes = await TcpService.Read(crcBytes, 0, 4, cancellationToken).ConfigureAwait(false);
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

        public Task Send(byte[] packet, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<bool>();

            var tcpMessage = new TcpMessage(_messageSequenceNumber, packet, tcs, cancellationToken);
            PushToQueue(tcpMessage);

            return tcs.Task;
        }

        private void PushToQueue(TcpMessage message)
        {
            _messageQueue.Enqueue(message);
            SendAllMessagesFromQueue().ConfigureAwait(false);
        }

        private async Task SendAllMessagesFromQueue()
        {
            await _semaphoreSlim.WaitAsync().ContinueWith(
                async _ =>
                {
                    if (!_messageQueue.IsEmpty)
                    {
                        try
                        {
                            await SendFromQueue().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("Failed to send confim message", ex);
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
                }).ConfigureAwait(false);
        }

        private async Task SendFromQueue()
        {
            while (!_messageQueue.IsEmpty)
            {
                _messageQueue.TryDequeue(out var item);
                TcpMessage message = item;

                try
                {
                    await SendMessage(message).ConfigureAwait(false);
                    message.TaskCompletionSource.SetResult(true);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Failed to process the message", e);

                    message.TaskCompletionSource.SetException(e);
                }
            }
        }

        public async Task SendMessage(TcpMessage tcpMessage)
        {
            var mesSeqNo = _messageSequenceNumber++;

            Debug.Write($"Sending message with seq_no {mesSeqNo}");

            var encodedMessage = tcpMessage.Encode();

            await TcpService.Send(encodedMessage, tcpMessage.CancellationToken).ConfigureAwait(false);
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
