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

namespace ChargingInterface
{
    public partial class Form1 : Form
    {

        public static Socket ClientSocket;  //声明负责通信的socket
        public static Socket connectSocket;  //声明负责
        public static int SFlag = 0;    //连接服务器成功标志

        public static byte[] send = new byte[16];//发送报文
        public static byte[] recieve = new byte[16];//接收报文

        Thread th1;     //声明一个线程 用于接收消息
        //Thread th2;     //声明一个线程 用于连接时获取编号
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;    //执行新线程时跨线程资源访问检查会提示报错，所以这里关闭检测
            textBox_Addr.Text = "192.168.43.215";//设置默认服务器IP
            textBox_Port.Text = "8002";//设置默认服务器端口号

            radioButton.Checked = false;
            radioButton.Enabled = false;
            pictureBox1.Enabled = false;
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

        private void button_connect_Click_1(object sender, EventArgs e)
        {
            ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);     //声明负责通信的套接字

            IPAddress IP = IPAddress.Parse(textBox_Addr.Text);      //获取设置的IP地址
            int Port = int.Parse(textBox_Port.Text);       //获取设置的端口号
            IPEndPoint iPEndPoint = new IPEndPoint(IP, Port);    //指定的端口号和服务器的ip建立一个IPEndPoint对象

            try
            {
                ClientSocket.Connect(iPEndPoint);       //用socket对象的Connect()方法以上面建立的IPEndPoint对象做为参数，向服务器发出连接请求
                SFlag = 1;  //若连接成功将标志设置为1

                button_Connect.Enabled = false;     //禁止操作连接按钮
                radioButton.Checked = false;
                //radioButton.Checked = true;
                MessageBox.Show("已连接到服务器"+iPEndPoint.ToString());

                

                //开启一个线程接收数据
                th1 = new Thread(Receive);
                th1.Name = "receive";
                th1.IsBackground = true;
                th1.Start(ClientSocket);

                ////开启一个线程接收数据
                //th2 = new Thread(connect);
                //th2.Name = "connect";
                //th2.IsBackground = true;
                //th2.Start(connectSocket);

            }
            catch
            {
                MessageBox.Show("服务器未打开");
            }
        }

        void connect()
        {
            Thread.Sleep(1000);
            //告知P和S插座以上线
            send = Encoding.ASCII.GetBytes("I" + "-" + "-" + "P" + "-" + "0" + "co" + "1" + "-" + "-");//告知P已连接，通知服务器给分配一个插口号,"-"是因为编号还没有分配
            ClientSocket.Send(send);
            Receive(connectSocket);
        }
        #endregion
        /*****************************************************************/



        /*****************************************************************/
        #region 接收服务器端数据
        void Receive(Object sk)
        {
            Socket socketRec = sk as Socket;

            while(true)
            {
                while (true)
                {
                    //5.接收数据
                    ClientSocket.Receive(recieve);  //调用Receive()接收字节数据

                    //6.打印接收数据
                    if (recieve.Length > 0)
                    {
                        string s = Analyse(recieve);
                        if(s == "concectedI")
                        {
                            label1.Text = "充电口"+ Encoding.UTF8.GetString(recieve, 10, 1);
                            break;
                        }
                        if (s == "openI")
                        {
                            radioButton.Enabled = true;
                            radioButton.Checked = true;
                            pictureBox1.Enabled = true;
                            int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//表示第几个充电桩
                            int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//表示第几个插口
                            label1.Text = "充电口" + i.ToString() + "-" + j.ToString();
                            send = Encoding.ASCII.GetBytes("I" + i.ToString() + j.ToString() + "P" + i.ToString() + "0" + "op" + "1" + i.ToString() + j.ToString() + "1");
                            ClientSocket.Send(send);
                            break;
                        }
                        if (s == "closeI")
                        {
                            radioButton.Checked = false;
                            radioButton.Enabled = false;
                            pictureBox1.Enabled = false;
                            int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//表示第几个充电桩
                            int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//表示第几个插口
                            send = Encoding.ASCII.GetBytes("I" + i.ToString() + j.ToString() + "P" + i.ToString() + "0" + "cl" + "1" + i.ToString() + j.ToString() + "1");
                            ClientSocket.Send(send);
                            break;
                        }
                    }
                }
            }

            
        }


        #endregion
        /*****************************************************************/
        
    }
}
