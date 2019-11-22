using Microsoft.Win32;
using MyLogLib;
using Newtonsoft.Json;
using RabbitMQ;
using RabbitMQ.YQMsg;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;


namespace YQBuzzer.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        public MainViewModel()
        {

        }

        #region properties
        public Action<string> OnShowMsg { get; set; }

        /// <summary>
        /// 当前心跳状态
        /// </summary>
        public HeartStatus CurrHeart { get => _CurrHeart; set => Set(ref _CurrHeart, value); }

        /// <summary>
        /// 当前制令等数据
        /// </summary>
        public TaskData TaskData { get => _TaskData; set => Set(ref _TaskData, value); }

        /// <summary>
        /// 当前结论数据
        /// </summary>
        public RltData ResultData { get => _ResultData; set => Set(ref _ResultData, value); }

        /// <summary>
        /// 当前液晶码（上板）
        /// </summary>
        public string CurrUpperCode { get => _CurrUpperCode; set => Set(ref _CurrUpperCode, value); }

        /// <summary>
        /// 当前电表结论
        /// </summary>
        public string CurrMeterRlt { get => _CurrMeterRlt; set => Set(ref _CurrMeterRlt, value); }

        public string CurrTestRlt { get => _CurrTestRlt; set => Set(ref _CurrTestRlt, value); }
        #endregion

        #region fields      
        private RltData _ResultData;
        private string _CurrMeterRlt;
        private string _CurrTestRlt;
        private string _CurrUpperCode;
        private ClientMQ mqClient;
        private HeartStatus _CurrHeart = HeartStatus.Initializing;
        private bool isBusy = false;
        private TaskData _TaskData;
        private CancellationTokenSource tokenSource;
        #endregion

        /// <summary>
        /// 连接MQ
        /// </summary>
        private void Init()
        {
            CurrHeart = HeartStatus.Initializing;
            InitMQ();
        }

        private bool InitMQ()
        {
            ShowMsg("初始化MQ...");
            mqClient?.Close();
            try
            {
                mqClient = new ClientMQ();
                mqClient.singleArrivalEvent += MqClient_singleArrivalEvent;
                mqClient.ReceiveMessage();
                ShowMsg("初始化MQ完毕！");
                //心跳开始
                HeartBeat();
                return true;
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("初始化MQ失败！", ex);
                ShowMsg("初始化MQ失败！");
            }
            return false;
        }

        private void MqClient_singleArrivalEvent(string data)
        {
            MyLog.WriteLog("收到信息 -- " + data, "MQ");
            ShowMsg(data);
            MsgBase msg = null;
            try
            {
                msg = JsonConvert.DeserializeObject<MsgBase>(data);
            }
            catch (Exception ex)
            {
                string errMsg = "协议格式错误！";
                MyLog.WriteLog(errMsg, ex, "MQ");
                ShowMsg(errMsg);
                return;
            }
            DateTime dtMsg;
            if (!DateTime.TryParse(msg.time_stamp, out dtMsg))
            {
                ShowMsg("时间戳错误，不处理此消息！");
                MyLog.WriteLog("时间戳错误，不处理此消息!");
                return;
            }
            if ((DateTime.Now - dtMsg).TotalSeconds > 120)
            {
                ShowMsg("2分钟前的消息不处理！");
                MyLog.WriteLog("2分钟前的消息不处理！");
                return;
            }
            try
            {
                //lock (objlock)
                {
                    if (msg.MESSAGE_TYPE == "task")
                    {
                        AnalyseTaskMsg(data);
                    }
                    else if (msg.MESSAGE_TYPE == "execute")
                    {
                        AnalyseExecuteMsg(data);
                    }
                    else { }
                }
            }
            catch (Exception ex)
            {
                MyLog.WriteLog(ex, "MQ");
                ShowMsg(ex.Message + "\r\n" + ex.StackTrace);
            }
        }

        #region 分析MQ
        private void AnalyseTaskMsg(string data)
        {
            var tskMsg = JsonConvert.DeserializeObject<TaskMsg>(data);
            this.TaskData = tskMsg.DATA;
            if (TaskData == null || string.IsNullOrEmpty(TaskData.WORKORDER_CODE))
            {
                CurrHeart = HeartStatus.Error;
                MyLog.WriteLog("主控下发的制令错误，空制令号!", "SYS");
                ShowMsg("主控下发的制令错误，空制令号!");
            }
            else
            {
                CurrHeart = HeartStatus.Init_Complete;
            }
        }

        private void AnalyseExecuteMsg(string data)
        {
            var execMsg = JsonConvert.DeserializeObject<ExecuteMsg>(data);
            this.ResultData = execMsg.DATA.FirstOrDefault();
            if (this.ResultData == null || string.IsNullOrEmpty(this.ResultData.BAR_CODE))
            {
                //上报故障，数据异常
                CurrHeart = HeartStatus.Error;
                MyLog.WriteLog("主控下发的启动命令错误，无厂内码!", "SYS");
                ShowMsg("主控下发的启动命令错误，无厂内码!");
                return;
            }
            //未完成，未初始化结束不执行
            if (CurrHeart != HeartStatus.Finished && CurrHeart != HeartStatus.Init_Complete)
            {
                MyLog.WriteLog("未完成，未初始化结束不执行!", "SYS");
                ShowMsg("未完成，未初始化结束不执行!");
                return;
            }
            CurrUpperCode = this.ResultData.BAR_CODE;
            //判断之前结论是否合格
            if (this.ResultData.result == "1")
            {
                CurrMeterRlt = "不合格";
                CurrTestRlt = "不检测";
                //直接上传结论
                DataMsg dataMsg = new DataMsg()
                {
                    NO = SysCfg.NO,
                    DEVICE_TYPE = SysCfg.DEVICE_TYPE,
                    DATA = new List<RltData>() { ResultData },
                };
                try
                {
                    mqClient.SentMessage(JsonConvert.SerializeObject(dataMsg));
                }
                catch (Exception ex)
                {
                    CurrHeart = HeartStatus.Error;
                    MyLog.WriteLog("上传结论到服务器失败！", ex);
                    ShowMsg("上传结论到服务器失败！");
                }
            }
            else
            {
                CurrMeterRlt = "合格";
                CurrTestRlt = "准备检测";
                CurrHeart = HeartStatus.Working;
                bool rlt = TestBuzzer();
                if (rlt)
                {
                    ResultData.result = "0";
                    CurrTestRlt = "合格";
                }
                else
                {
                    ResultData.result = "1";
                    CurrTestRlt = "不合格";
                }
                try
                {
                    //上传MQ
                    DataMsg dataMsg = new DataMsg()
                    {
                        NO = SysCfg.NO,
                        DEVICE_TYPE = SysCfg.DEVICE_TYPE,
                        DATA = new List<RltData>() { ResultData }
                    };
                    string strMsg = JsonConvert.SerializeObject(dataMsg);
                    mqClient.SentMessage(strMsg);
                    ShowMsg("上传:" + strMsg);
                    MyLog.WriteLog("上传:" + strMsg);
                }
                catch (Exception ex)
                {
                    ShowMsg("上传MQ失败!");
                    MyLog.WriteLog("上传MQ失败!", ex);
                }
                CurrHeart = HeartStatus.Finished;
            }
        }

        private IOControllerHelper io;
        private SerialPortHelper serial;
        private PLCHelper plc;
        public bool TestBuzzer()
        {
            try
            {
                io = new IOControllerHelper(SysCfg.IO_CONTROLLER_IP, SysCfg.IO_CONTROLLER_PORT);
                io.OnShowMsg = this.OnShowMsg;
                if (!io.Connect(SysCfg.IO_TIMEOUT))
                {
                    return false;
                }
                serial = new SerialPortHelper(SysCfg.METER_COM);
                serial.OnShowMsg = this.OnShowMsg;
                if (!serial.Connect(SysCfg.IO_TIMEOUT))
                {
                    return false;
                }
                plc = new PLCHelper(SysCfg.PLC_IP, SysCfg.PLC_PORT);
                plc.OnShowMsg += ShowMsg;
                if (!plc.Connect())
                {
                    return false;
                }

                #region 上电
                ShowMsg("上电开始！");
                string strPowerOn = $"write-relay:relay{SysCfg.POWER_PORT}=1;";
                ShowMsg("发送:" + strPowerOn);
                string rcvDataPowerOn = io.Communicate(strPowerOn);
                ShowMsg("接收:" + rcvDataPowerOn);
                if (rcvDataPowerOn == null || rcvDataPowerOn.Length == 0)
                {
                    ShowMsg("上电失败！");
                    MyLog.WriteLog("上电失败！", "SYS");
                    return false;
                }
                ShowMsg("等待:" + SysCfg.WAIT_METER_INIT);
                Thread.Sleep(SysCfg.WAIT_METER_INIT);//等待稳定
                #endregion

                #region 发蜂鸣开启命令
                string strOpenBuzzer = "C1020,1\r\n";
                ShowMsg("开启蜂鸣！");
                ShowMsg("发送:" + strOpenBuzzer);
                string rcvDataOpenBuzzer = serial.Communicate(strOpenBuzzer);
                ShowMsg("接收:" + rcvDataOpenBuzzer);
                if (rcvDataOpenBuzzer == null || rcvDataOpenBuzzer.Length == 0)
                {
                    ShowMsg("开启蜂鸣失败！");
                    MyLog.WriteLog("开启蜂鸣失败！", "SYS");
                    return false;
                }
                #endregion

                ShowMsg("等待:" + SysCfg.WAIT_METER_INIT);
                Thread.Sleep(SysCfg.WAIT_METER_INIT);
                #region 读取蜂鸣状态
                ShowMsg("读取蜂鸣状态！");
                PLCResponse resp = plc.ReadOnePoint("DM300");
                if (resp.HasError)
                {
                    ShowMsg("读取蜂鸣状态失败！" + resp.ErrorMsg);
                    MyLog.WriteLog("读取蜂鸣状态失败！", "SYS");
                    return false;
                }
                
                int status = -1;
                if (Int32.TryParse(resp.Text, out status))
                {
                    if (status == 1)
                    {
                        ShowMsg("蜂鸣合格！");
                        return true;
                    }
                }
                ShowMsg("蜂鸣不合格！");
                return false;
                #endregion
            }
            catch (Exception ex)
            {
                MyLog.WriteLog("蜂鸣检测异常", ex);
                ShowMsg("蜂鸣检测异常！");
            }
            finally
            {
                #region 下电
                try
                {
                    ShowMsg("下电！");
                    string strPowerOff = $"write-relay:relay{SysCfg.POWER_PORT}=0;";
                    ShowMsg("发送:" + strPowerOff);
                    io?.Communicate(strPowerOff);//插针断电，保证安全
                    io?.DisConnect();
                    serial?.DisConnect();
                    plc?.DisConnect();
                    io = null;
                    serial = null;
                    plc = null;
                }
                catch (Exception)
                {
                }
                #endregion
            }
            return false;
        }
        #endregion

        #region 心跳
        private void HeartBeat()
        {
            tokenSource?.Cancel();
            tokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!tokenSource.IsCancellationRequested)
                {
                    try
                    {
                        //lock (objlock)
                        {
                            HeartBeatMsg msg = new HeartBeatMsg()
                            {
                                DEVICE_TYPE = SysCfg.DEVICE_TYPE,
                                NO = SysCfg.NO,
                                STATUS = ((int)CurrHeart).ToString()
                            };
                            string strMsg = JsonConvert.SerializeObject(msg);
                            mqClient.SentMessage(strMsg);
                        }
                    }
                    catch (Exception ex)
                    {
                        MyLog.WriteLog(ex);
                        ShowMsg("心跳上报失败!" + ex.Message);
                    }
                    Thread.Sleep(SysCfg.HEARTBEAT_TIMESPAN);
                }
            }, tokenSource.Token);
        }
        #endregion

        #region ConnectCmd
        private RelayCommand _ConnectCmd;
        public RelayCommand ConnectCmd => _ConnectCmd ?? (_ConnectCmd = new RelayCommand(Connect, () => !isBusy));

        private async void Connect()
        {
            isBusy = true;
            await Task.Run(new Action(Init));
            isBusy = false;
        }
        #endregion

        private void ShowMsg(string msg)
        {
            try
            {
                OnShowMsg?.Invoke(DateTime.Now.ToString("HH:mm:ss.fff:") + " " + msg);
            }
            catch (Exception)
            {
                //MyLog.WriteLog("OnShowMsg委托调用异常！", ex);
            }
        }
    }
}
