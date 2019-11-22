using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YQBuzzer
{
    public class SysCfg
    {
        /// <summary>
        /// 设备类型
        /// </summary>
        public static string DEVICE_TYPE => ConfigurationUtil.GetConfiguration(Convert.ToString, () => "E012");

        /// <summary>
        /// 设备类型
        /// </summary>
        public static string NO => ConfigurationUtil.GetConfiguration(Convert.ToString, () => "E01201");

        /// <summary>
        /// 电表通信串口
        /// </summary>
        public static string METER_COM => ConfigurationUtil.GetConfiguration(Convert.ToString, () => "");

        /// <summary>
        /// 心跳间隔
        /// <para>默认1000ms</para>
        /// </summary>
        public static int HEARTBEAT_TIMESPAN => ConfigurationUtil.GetConfiguration(int.Parse, () => 1000);

        /// <summary>
        /// IO控制器IP
        /// </summary>
        public static string IO_CONTROLLER_IP => ConfigurationUtil.GetConfiguration(Convert.ToString, () => "");

        /// <summary>
        /// IO控制器Port
        /// </summary>
        public static int IO_CONTROLLER_PORT => ConfigurationUtil.GetConfiguration(int.Parse, () => 0);

        /// <summary>
        /// IO超时
        /// </summary>
        public static int IO_TIMEOUT => ConfigurationUtil.GetConfiguration(int.Parse, () => 0);

        /// <summary>
        /// 上电端口
        /// </summary>
        public static int POWER_PORT => ConfigurationUtil.GetConfiguration(int.Parse, () => 0);

        /// <summary>
        /// PLC端口
        /// </summary>
        public static int PLC_PORT => ConfigurationUtil.GetConfiguration(int.Parse, () => 8501);

        /// <summary>
        /// PLC地址
        /// </summary>
        public static string PLC_IP => ConfigurationUtil.GetConfiguration(Convert.ToString, () => "10.50.57.40");

        /// <summary>
        /// 等待电表上电后初始化的时间
        /// </summary>
        public static int WAIT_METER_INIT => ConfigurationUtil.GetConfiguration(int.Parse, () => 5000);
        public static bool SetConfiguration(string key, object val)
        {
            return ConfigurationUtil.SetConfiguration(key, val.ToString());
        }
    }
}
