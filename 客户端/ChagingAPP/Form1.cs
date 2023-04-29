using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChagingAPP
{
    public partial class Form1 : Form
    {
        public static Socket ClientSocket;  //声明负责通信的socket
        //public static Socket connectSocket;  //声明负责
        public static int SFlag = 0;    //连接服务器成功标志

        public static byte[] send = new byte[16];//发送报文
        public static byte[] recieve = new byte[16];//接收报文

        public static List<RadioButton> rbtnList = new List<RadioButton>(10);

        Thread th1;     //声明一个线程 用于接收消息

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;    //执行新线程时跨线程资源访问检查会提示报错，所以这里关闭检测
            textBox_IP.Text = "192.168.43.215";//设置默认服务器IP
            textBox_Port.Text = "8002";//设置默认服务器端口号

            //初始化数组
            for (int i = 0; i < 10; i++)
            {
                RadioButton rbt = new RadioButton();
                rbtnList.Add(rbt);
            }

            //创建按钮数组
            foreach (Control control in groupBox_ChargingPile.Controls)
            {
                if (control is RadioButton)
                {
                    //rbtnList[int.Parse(control.Name[11].ToString())] = (RadioButton)control;
                    control.Enabled = false;
                    RadioButton rbt = (RadioButton)control;
                    rbtnList[int.Parse(rbt.Text[3].ToString())] = rbt;
                }
            }

        }

        /*****************************************************************/
        #region 解析报文
        String Analyse(byte[] text)
        {
            string s = "false";//解析结果
            switch (Encoding.UTF8.GetString(text, 6, 2))
            {
                case "op":
                    if (text[8] == '0') s = "open";
                    if (text[8] == '1') s = "opened";
                    break;
                case "cl":
                    if (text[8] == '0') s = "close";
                    if (text[8] == '1') s = "closed";
                    break;
                case "co":
                    if (text[8] == '0') s = "connect";
                    if (text[8] == '1') s = "connected";
                    break;
                case "gt":
                    if (text[8] == '0') s = "get";
                    if (text[8] == '1') s = "geted";
                    break;
            }
            if (Encoding.UTF8.GetString(text, 11, 1) == "0")
            {
                s += "P";
            }
            else
            {
                s += "I";
            }
            return s;
        }
        #endregion
        /*****************************************************************/



        /*****************************************************************/
        #region 连接服务器端
        private void button_connect_Click(object sender, EventArgs e)
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);     //声明负责通信的套接字

            IPAddress IP = IPAddress.Parse(textBox_IP.Text);      //获取设置的IP地址
            int Port = int.Parse(textBox_Port.Text);       //获取设置的端口号
            IPEndPoint iPEndPoint = new IPEndPoint(IP, Port);    //指定的端口号和服务器的ip建立一个IPEndPoint对象

            try
            {
                ClientSocket.Connect(iPEndPoint);       //用socket对象的Connect()方法以上面建立的IPEndPoint对象做为参数，向服务器发出连接请求
                SFlag = 1;  //若连接成功将标志设置为1

                button_connect.Enabled = false;     //禁止操作连接按钮
                //radioButton.Checked = true;
                MessageBox.Show("已连接到服务器" + iPEndPoint.ToString());



                //开启一个线程接收数据
                th1 = new Thread(Receive);
                th1.Name = "receive";
                th1.IsBackground = true;
                th1.Start(ClientSocket);


            }
            catch
            {
                MessageBox.Show("服务器未打开");
            }
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 接收服务器端数据

        void Receive(Object sk)
        {
            Socket socketRec = sk as Socket;

            while (true)
            {
                while (true)
                {
                    //5.接收数据
                    ClientSocket.Receive(recieve);  //调用Receive()接收字节数据

                    //6.打印接收数据
                    if (recieve.Length > 0)
                    {
                        string s = Analyse(recieve);
                        List<bool> F = new List<bool>(10);
                        if (s == "getedP")//获取到充电口状态信息
                        {
                            int f = int.Parse(Encoding.UTF8.GetString(recieve, 12, 4));
                            
                            string str = Convert.ToString(f, 2);
							string str1 = str;
							for (int i = 0; i < (10 - str.Length); i++)
							{
								str1 = '0' + str1;
							}

							for (int i = 0; i < 10; i++)
                            {
                                bool bo = true;
                                if (str1[i] == '1')
                                {
                                    bo = true;
                                }
                                else
                                {
                                    bo = false;
                                }
                                F.Add(bo);
                            }

                            //int cunt = 0;
                            foreach (Control control in groupBox_ChargingPile.Controls)
                            {
                                if(control is RadioButton)
                                {
                                    RadioButton rbt = (RadioButton)control;
                                    for(int x = 0; x < rbtnList.Count; x++)
                                    {
                                        if(rbt.Name == rbtnList[x].Name)
                                        {
                                            if (F[x]) control.Enabled = false;
                                            else control.Enabled = true;
                                        }
                                    }
                                }
                                
                            }
                            break;
                        }
                        if (s == "openedI")
                        {
                            foreach (Control control in groupBox_ChargingPile.Controls)
                            {
                                if (control is RadioButton)
                                {
                                    RadioButton rbt = (RadioButton)control;
                                    for (int x = 0; x < rbtnList.Count; x++)
                                    {
                                        if (rbt.Name == rbtnList[x].Name)
                                        {
                                            if (F[x]) control.Enabled = false;
                                            else control.Enabled = true;
                                        }
                                    }
                                }

                            }
                            break;
                        }
                    }
                }
            }


        }


        #endregion
        /*****************************************************************/


        /*****************************************************************/
        #region 获取充电口使用信息
        private void button_getInterfaces_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("A00" + "S00" + "gt" + "0" + "100");
            ClientSocket.Send(send);
        }
        private void button_getInterfaces2_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("A00" + "S00" + "gt" + "0" + "200");
            ClientSocket.Send(send);
        }



        #endregion
        /*****************************************************************/


        /*****************************************************************/
        #region 提交支付
        private void button_pay_Click(object sender, EventArgs e)
        {
            int j;
            bool flag = false;//标记是否选中项
            foreach(Control control in groupBox_ChargingPile.Controls)
            {
                if(control is RadioButton)
                {
                    RadioButton rbt = (RadioButton)control;
                    if (rbt.Checked == true)
                    {
                        flag = true;
                        j = int.Parse(rbt.Text[3].ToString());
                        send= Encoding.ASCII.GetBytes("A00" + "S00" + "op" + "0" + "1" + j.ToString() + "1");
                        ClientSocket.Send(send);
                    }
                }
                
            }
            if (flag == false) MessageBox.Show("请选择充电口！");
        }
        #endregion
        /*****************************************************************/
    }
}
