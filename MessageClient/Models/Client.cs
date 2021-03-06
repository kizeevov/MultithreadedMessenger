﻿using MessageCommonLib.Api;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MessageClient.Models
{
    public class Client : INotifyPropertyChanged
    {
        #region Private properties

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private string hostAddress = "localhost";
        private int port = 8080;
        private string clientName = "test 1";

        private bool connected = false;

        private TcpClient tcpClient = null;

        #endregion

        #region Constructor

        public Client()
        {
        }

        #endregion

        #region Events

        public delegate void UpdateClientListDelegate(List<string> list);
        public delegate void NewMessageDelegate(string clientName, string message);
        public delegate void ErrorOccurredDelegate(string errorMessage);

        public event UpdateClientListDelegate UpdateClientList;
        public event NewMessageDelegate NewMessage;
        public event ErrorOccurredDelegate ErrorOccurred;

        #endregion

        #region Properties

        public int Port
        {
            get => port;
            set
            {
                if (port == value)
                    return;

                port = value;
                OnPropertyChanged("Port");
            }
        }

        public string HostAddress
        {
            get => hostAddress;
            set
            {
                if (hostAddress == value)
                    return;

                hostAddress = value;
                OnPropertyChanged("HostAddress");
            }
        }

        public string ClientName
        {
            get => clientName;
            set
            {
                if (clientName == value)
                    return;

                clientName = value;
                OnPropertyChanged("ClientName");
            }
        }

        public bool Connected
        {
            get => connected;
            private set
            {
                if (connected == value)
                    return;

                connected = value;
                OnPropertyChanged("Connected");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Public methods

        public async void Connect()
        {
            IPAddress ipAddress = null;
            IPHostEntry ipHostInfo = Dns.GetHostEntry(hostAddress);

            for (int i = 0; i < ipHostInfo.AddressList.Length; ++i)
            {
                if (ipHostInfo.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipHostInfo.AddressList[i];
                    break;
                }
            }

            if (ipAddress == null)
            {
                throw new Exception("Unable to find an IPv4 address for server");
            }

            tcpClient = new TcpClient();

            try
            {
                await tcpClient.ConnectAsync(ipAddress, port);

                Connected = true;

                Task processTask = Process(tcpClient);
                await processTask;
            }
            catch (SocketException ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
                logger.Error(ex);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        public async void Disconnect()
        {
            if (tcpClient.Connected)
            {
                try
                {
                    NetworkStream networkStream = tcpClient.GetStream();
                    StreamWriter writer = new StreamWriter(networkStream);

                    writer.AutoFlush = true;

                    LogoutRequest logoutRequest = new LogoutRequest();
                    await writer.WriteLineAsync(logoutRequest.ToJson());
                }
                catch
                {

                }

                tcpClient.Close();
            }

            tcpClient = null;
            Connected = false;
        }

        public async void Login()
        {
            if (!tcpClient.Connected)
            {
                Connected = false;
                return;
            }

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                StreamWriter writer = new StreamWriter(networkStream);

                writer.AutoFlush = true;

                LoginRequest authorizationRequest = new LoginRequest(clientName);

                await writer.WriteLineAsync(authorizationRequest.ToJson());
            }
            catch
            {
                Connected = false;
            }
        }

        public async void SendMessage(string clentName, string message)
        {
            if (!tcpClient.Connected)
            {
                Connected = false;
                return;
            }

            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                StreamWriter writer = new StreamWriter(networkStream);

                writer.AutoFlush = true;

                SendMessageRequest messageRequest = new SendMessageRequest();
                messageRequest.ClientName = clentName;
                messageRequest.Message = message;
                messageRequest.SenderName = ClientName;

                await writer.WriteLineAsync(messageRequest.ToJson());
            }
            catch
            {
                Connected = false;
            }
        }

        #endregion

        #region Private methods

        private async Task Process(TcpClient tcpClient)
        {
            try
            {
                NetworkStream networkStream = tcpClient.GetStream();
                StreamReader reader = new StreamReader(networkStream);
                StreamWriter writer = new StreamWriter(networkStream);
                writer.AutoFlush = true;

                while (true)
                {
                    string request = await reader.ReadLineAsync();
                    if (request != null)
                    {
                        try
                        {
                            JsonResponse response = JsonResponse.FromJson(request);
                            if (response.Result == JsonResponse.ResponseResults.Error)
                            {
                                ErrorOccurred?.Invoke(response.Description);
                                break;
                            }
                        }
                        catch { }

                        try
                        {
                            object obj = JsonRequest.FromJson(request);
                            if (obj.GetType() == typeof(ClientListBroadcast))
                            {
                                ClientListBroadcast broadcast = (ClientListBroadcast)obj;

                                UpdateClientList?.Invoke(broadcast.ClientList);
                            }
                            else if (obj.GetType() == typeof(SendMessageRequest))
                            {
                                SendMessageRequest messageRequest = (SendMessageRequest)obj;

                                NewMessage?.Invoke(messageRequest.SenderName, messageRequest.Message);
                            }
                        }
                        catch {}
                    }
                    else
                    {
                        break;
                    }
                }

                tcpClient.Close();
                Connected = false;
            }
            catch (Exception ex)
            {
                logger.Error(ex);

                if (tcpClient.Connected)
                {
                    tcpClient.Close();
                }

                Connected = false;
            }
        }

        #endregion
    }
}
