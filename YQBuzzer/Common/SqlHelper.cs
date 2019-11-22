using MyLogLib;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YQBuzzer
{
    public class SqlHelper
    {
        public static string strSQL = "";
        public static string tableName = "";
        public static string FieldName = "";
        public static SqlConnection conn;
        public string strcon { get; set; }
        public SqlHelper(string strcon)
        {
            conn = new SqlConnection(strcon);
            SqlHelper.strSQL = strcon;
        }
        //打开数据库
        public bool Open()
        {
            bool connected = false;
            while (!connected)
            {
                try
                {
                    conn.Open();
                    connected = true;
                }
                catch (Exception ex)
                {
                    MyLog.WriteLog("连接数据库失败！" + conn.ConnectionString, ex);
                }
            }
            return connected;
        }


        //关闭数据库
        public void Close()
        {
            try
            {
                conn?.Close();
                conn?.Dispose();
            }
            catch (Exception)
            {
            }
        }

        //数据查询
        public DataTable SelectData(string strSQL)
        {
            DataTable dt = new DataTable();
            if (Open())
            {
                try
                {
                    SqlCommand comm = new SqlCommand(strSQL, conn);
                    SqlDataReader r = comm.ExecuteReader();
                    dt.Load(r);
                    comm.Dispose();
                    r.Close();
                    Close();
                }
                catch (Exception)
                {
                    MyLog.WriteLog("查询数据出错：" + strSQL, "SYS");
                    //MessageBox.Show("查询数据出错：" + strSQL);
                    Close();
                }

            }
            else
            {
                MyLog.WriteLog("数据库打开错误！", "SYS");
            }

            return dt;
        }
        //数据删除
        public void DelData(string strSQL)
        {
            if (Open())
            {
                try
                {
                    SqlCommand comm = new SqlCommand(strSQL, conn);
                    comm.ExecuteNonQuery();
                    comm.Dispose();
                    Close();
                }
                catch (Exception)
                {
                    Close();
                }
            }


        }
        //数据修改
        public int UpdateData(string strSQL)
        {
            try
            {
                if (Open())
                {
                    SqlCommand comm = new SqlCommand(strSQL, conn);
                    int num = comm.ExecuteNonQuery();
                    comm.Dispose();
                    Close();
                    return num;
                }
                else
                {
                    return 0;
                }
            }
            catch (Exception)
            {
                MyLog.WriteLog("数据写入错误！" + strSQL, "SYS");
                Close();
                return 0;
            }
        }
    }
}
