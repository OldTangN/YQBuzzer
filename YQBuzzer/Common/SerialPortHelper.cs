using MyLogLib;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace YQBuzzer
{
    public class SerialPortHelper
    {
        private string _ComName;
        private SerialPort serial;
        private int _RcvTimeout;
        public SerialPortHelper(string comName)
        {
            this._ComName = comName;
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

        public bool Connect(int rcvTimeout = 1000)
        {
            this._RcvTimeout = rcvTimeout;
            DisConnect();
            try
            {
                serial = new SerialPort(this._ComName, 9600, Parity.None, 8, StopBits.One);
                serial.Open();
                return true;
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("打开串口失败！", ex);
                ShowMsg($"{_ComName}打开失败！");
            }
            return false;
        }

        public void DisConnect()
        {
            try
            {
                serial?.Close();
                serial.Dispose();
            }
            catch (Exception)
            {
            }
        }

        public string Communicate(string strData)
        {
            try
            {
                serial.DiscardInBuffer();//清空接收缓冲区
                byte[] data = Encoding.ASCII.GetBytes(strData);
                serial.Write(data, 0, data.Length);
                byte[] buffer = new byte[1024];
                Thread.Sleep(_RcvTimeout);
                if (serial.BytesToRead > 0)
                {
                    int len = serial.Read(buffer, 0, serial.BytesToRead);
                    if (len > 0)
                    {
                        byte[] rcvData = buffer.Take(len).ToArray();
                        return Encoding.ASCII.GetString(rcvData);
                    }
                }
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("通信失败！", ex);
                ShowMsg("通信失败！");
            }
            return null;
        }
    }
}
