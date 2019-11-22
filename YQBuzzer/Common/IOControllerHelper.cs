using MyLogLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace YQBuzzer
{
    public class IOControllerHelper
    {
        private Socket socket;
        private string _ip;
        private int _port;
        public IOControllerHelper(string ip, int port)
        {
            this._ip = ip;
            this._port = port;
        }
        public Action<string> OnShowMsg { get; set; }

        private void ShowMsg(string msg)
        {
            try
            {
                OnShowMsg?.Invoke(msg);
            }
            catch (Exception ex)
            {
                MyLog.WriteLog(ex);
            }
        }

        public bool Connect(int timeout = 1000)
        {
            DisConnect();
            try
            {
                socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                socket.SendTimeout = timeout;
                socket.ReceiveTimeout = timeout;
                socket.Connect(new IPEndPoint(IPAddress.Parse(_ip), _port));
                return true;
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("连接Socket失败！", ex);
                ShowMsg($"{_ip}:{_port}连接失败！");
            }
            return false;
        }

        public void DisConnect()
        {
            try
            {
                socket?.Disconnect(false);
                socket.Dispose();
            }
            catch (Exception)
            {
            }
        }

        public string Communicate(string strData)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(strData);
                socket.Send(data);
                byte[] buffer = new byte[1024];
                int len = socket.Receive(buffer);
                if (len > 0)
                {
                    byte[] rcvData = buffer.Take(len).ToArray();
                    return Encoding.ASCII.GetString(rcvData);
                }
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("通信失败！", ex);
                ShowMsg("通信失败！");
            }
            return "";
        }
    }
}
