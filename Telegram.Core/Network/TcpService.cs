﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Core.Utils;
using Telegram.Net.Core.Network.Exceptions;
using Telegram.Net.Core.Settings;

namespace Telegram.Net.Core.Network
{
    public class TcpService : ITcpService
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        private NetworkStream _stream;
        private TcpClient _tcpClient;

        public IClientSettings ClientSettings { get; set; } = Telegram.Net.Core.Settings.ClientSettings.Instance;

        public async Task Connect()
        {
            await AsyncHelper.RedirectToThreadPool();

            _tcpClient = new TcpClient
            {
                LingerState = new LingerOption(true, 1)
            };

            _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            await _tcpClient.ConnectAsync(ClientSettings.Session.ServerAddress, ClientSettings.Session.Port);
            _stream = _tcpClient.GetStream();

            _stream.ReadTimeout = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
        }

        private bool IsTcpClientConnected()
        {
            if (_tcpClient == null || !_tcpClient.Connected ||
                _tcpClient.Client == null || !_tcpClient.Client.Connected)
            {
                return false;
            }

            var endpoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
            var session = ClientSettings.Session;

            if (endpoint.Address.ToString() != session.ServerAddress || endpoint.Port != session.Port)
            {
                return false;
            }

            return true;
        }

        public async Task<int> Read(byte[] buffer, int offset, int neededToRead, CancellationToken cancellationToken)
        {
            await AsyncHelper.RedirectToThreadPool();

            await CheckConnectionState();

            var bytesRead = 0;

            do
            {
                var availableBytes = await _stream.ReadAsync(buffer, bytesRead, neededToRead - bytesRead);
                if (availableBytes == 0)
                {
                    Debug.WriteLine("TcpTransport: read the connection termination 0 packet");
                }

                bytesRead += availableBytes;
            }
            while (bytesRead != neededToRead);

            return bytesRead;
        }

        public async Task Send(byte[] encodedMessage, CancellationToken cancellationToken)
        {
            await AsyncHelper.RedirectToThreadPool();

            await CheckConnectionState();

            await _stream.WriteAsync(encodedMessage, 0, encodedMessage.Length, cancellationToken);
        }

        private async Task CheckConnectionState()
        {
            await AsyncHelper.RedirectToThreadPool();

            if (!IsTcpClientConnected())
            {
                await _semaphore.WaitAsync();

                try
                {
                    if (!IsTcpClientConnected())
                    {
                        var previouslyConnected = _tcpClient != null;

                        Disconnect();

                        await Connect();

                        if (previouslyConnected)
                        {
                            throw new DisconnectedException();
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public void Disconnect()
        {
            if (_tcpClient != null)
            {
                _stream.Dispose();
                _tcpClient.Close();
                _tcpClient = null;
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
                _semaphore?.Dispose();
                Disconnect();
            }

            _disposed = true;
        }

        ~TcpService()
        {
            Dispose(false);
        }
        #endregion
    }
}
