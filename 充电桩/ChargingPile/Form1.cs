using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChargingPile
{
    public partial class Form1 : Form
    {
        //这里声明多个套接字是为了在连接，接收数据，发送数据的函数中不发生混乱，同时方便关闭
        public static Socket ClientSocket1;  //声明负责与服务器进行通信的socket
        public static Socket ClientSocket2;    //声明用于与插座进行通信的socket
        public static List<Socket> socketsList = new List<Socket>();    //创建一个全局的List用来存放不同的Client套接字

        public static int SFlag = 0;    //连接成功标志
        public static int CFlag = 0;    //客户端关闭的标志

        public static byte[] send = new byte[16];//发送报文
        public static byte[] recieve = new byte[16];//接收报文

        public static List<RadioButton> rbtnList = new List<RadioButton>(10);
        public static List<Button> btnONList = new List<Button>(10);
        public static List<Button> btnOFFList = new List<Button>(10);

        Thread th1;     //声明线程1,绑定Receive,用于接收来自服务器的消息
        Thread th2;     //声明线程2,绑定Recieve，用于接收来自插座的消息
        Thread th3;     //声明线程3,绑定Listen,用于监听连接插座

        //Thread th4;     //声明线程4,用于建立连接时发送信息

        public static int port = 8002;//设置默认端口号

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //comboBox_Clients.SelectedIndex = 0;
            Control.CheckForIllegalCrossThreadCalls = false;    //执行新线程时跨线程资源访问检查会提示报错，所以这里关闭检测

            string ip = "";
            ///获取本地的IP地址
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    ip = _IPAddress.MapToIPv4().ToString();
                    textBox_thisIP.Text = string.Format(ip);//默认获取本机IP
                }
            }
            textBox_thisPort.Text = port.ToString();//设置默认端口号
            textBox_IP.Text = "192.168.43.215";//设置默认服务器IP
            textBox_Port.Text = "8001";//设置默认服务器端口号

            radioButton.Checked = false;
            groupBox_ChargingPile.Enabled = false;
            //button_Close.Enabled = false;
            //button_Connect .Enabled = false;
            button1.Enabled = false;

            //初始化数组
            for (int i = 0; i < 10; i++)
            {
                RadioButton rbt = new RadioButton();
                Button bt = new Button();
                rbtnList.Add(rbt);
                btnONList.Add(bt);
                btnOFFList.Add(bt);
            }

            //创建按钮数组
            foreach (Control control in groupBox_ChargingPile.Controls)
            {
                if (control is RadioButton)
                {
                    rbtnList[int.Parse(control.Name[12].ToString())-1] = (RadioButton)control;
                    control.Enabled = false;
                }

                if ((control is Button) && control.Text == "开")
                {
                    btnONList[int.Parse(control.Name[7].ToString())-1] = (Button)control;
                    //control.Enabled = false;
                }
                if ((control is Button) && control.Text == "关")
                {
                    btnOFFList[int.Parse(control.Name[7].ToString())-1] = (Button)control;
                    control.Enabled = false;
                }
            }
            
        }

        private void label5_Click(object sender, EventArgs e)
        {

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
        private void button_Connect_Click(object sender, EventArgs e)
        {
            ClientSocket1 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);     //声明负责通信的套接字

            //richTextBox_Recieve.Text += "正在连接...\n";

            IPAddress IP = IPAddress.Parse(textBox_IP.Text);      //获取设置的IP地址
            int Port = int.Parse(textBox_Port.Text);       //获取设置的端口号
            IPEndPoint iPEndPoint = new IPEndPoint(IP, Port);    //指定的端口号和服务器的ip建立一个IPEndPoint对象

            try
            {
                ClientSocket1.Connect(iPEndPoint);       //用socket对象的Connect()方法以上面建立的IPEndPoint对象做为参数，向服务器发出连接请求
                SFlag = 1;  //若连接成功将标志设置为1

                //richTextBox_Recieve.Text += DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ") + textBox_Addr.Text + "连接成功" + "\n";
                button_Connect.Enabled = false;     //禁止操作连接按钮

                

                //开启一个线程接收服务器数据
                th1 = new Thread(Receive);
                th1.Name = "receiveS";
                th1.IsBackground = true;
                th1.Start(ClientSocket1);


            }
            catch
            {
                MessageBox.Show("服务器未打开");
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        void connect()
        {
            //Thread.Sleep(1000);
            
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 与接口配对
        private void button1_Click(object sender, EventArgs e)
        {
            ClientSocket2 = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);     //创建用于通信的套接字
            button1.Enabled = false;
            //1.绑定IP和Port
            IPAddress IP = IPAddress.Parse(textBox_thisIP.Text);
            int Port = int.Parse(textBox_thisPort.Text);

            //IPAddress ip = IPAddress.Any;
            IPEndPoint iPEndPoint = new IPEndPoint(IP, Port);

            try
            {
                //2.使用Bind()进行绑定
                ClientSocket2.Bind(iPEndPoint);
                //3.开启监听
                //Listen(int backlog); backlog:监听数量 
                ClientSocket2.Listen(10);

                /*
                 * tip：
                 * Accept会阻碍主线程的运行，一直在等待客户端的请求，
                 * 客户端如果不接入，它就会一直在这里等着，主线程卡死
                 * 所以开启一个新线程接收客户单请求
                 */

                //开启线程Accept进行通信的客户端socket
                th3 = new Thread(Listen);   //线程绑定Listen函数
                th3.Name = "listen";
                th3.IsBackground = true;    //运行线程在后台执行
                th3.Start(ClientSocket2);    //Start里面的参数是Listen函数所需要的参数，这里传送的是用于通信的Socket对象
                Console.WriteLine("1");
            }
            catch
            {
                MessageBox.Show("服务器出问题了");
            }
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 监听接口
        void Listen(Object sk)
        {
            try
            {
                while (true)
                {
                    //GNFlag如果为1就进行中断连接，使用在标志为是为了保证能够顺利关闭服务器端
                    /*
                     * 当客户端关闭一次后，如果没有这个标志位会使得服务器端一直卡在中断的位置
                     * 如果服务器端一直卡在中断的位置就不能顺利关闭服务器端
                     */

                    //4.阻塞到有client连接
                    Socket Client = ClientSocket2.Accept(); //声明用于与某一个客户端通信的套接字
                    socketsList.Add(Client);               //将连接的客户端存进List

                    //获取客户端信息将不同客户端并存进comBox
                    //string client = Client.RemoteEndPoint.ToString();
                    //comboBox_Clients.Items.Add(client);
                    //comboBox_Clients.SelectedIndex = 0;

                    CFlag = 0;  //连接成功，将客户端关闭标志设置为0
                    SFlag = 1;  //当连接成功，将连接成功标志设置为1

                    //richTextBox_CMD.Text += DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ") + client + "连接成功";
                    //richTextBox_CMD.Text += "\r\n";

                    //连接成功后将checkedlistbox打开


                    //开启第二个线程接收插座端数据
                    th2 = new Thread(Receive);  //线程绑定Receive函数
                    th2.Name = "receiveI";
                    th2.IsBackground = true;    //运行线程在后台执行
                    th2.Start(Client);    //Start里面的参数是Listen函数所需要的参数，这里传送的是用于通信的Socket对象
                }
            }
            catch
            {
                MessageBox.Show("没有连接上客户端");
            }
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 接收消息
        void Receive(Object sk)
        {
            Socket socket = sk as Socket;  //创建用于通信的套接字(这里是线程传过来的client套接字)

            

            while (true)
            {
                while (true)
                {
                    try
                    {
                        if (CFlag == 0 && SFlag == 1)
                        {

                            int len = socket.Receive(recieve);
                            string s = Analyse(recieve);
                            //if(s == "connectedP")
                            //{
                            //    label1.Text = "充电桩"+ Encoding.UTF8.GetString(recieve, 9, 1);//给充电桩设置编号显示
                            //    break;
                            //}
                            //if(s == "connectedI")
                            //{
                            //    //变UI
                            //    int j = socketsList.Count - 1;
                            //    foreach (Control control in groupBox_ChargingPile.Controls)
                            //    {
                            //        if (control is RadioButton)
                            //        {
                            //            RadioButton r = (RadioButton)control;
                            //            if (r.Name == rbtnList[j - 1].Name)
                            //            {
                            //                control.Enabled = true;//收到连接成功的响应后使其可用
                            //            }
                            //        }
                            //        if (control is Button)
                            //        {
                            //            Button b = (Button)control;
                            //            if (b.Name == btnONList[j - 1].Name)//连接成功，但仍处于关闭状态
                            //                control.Enabled = true;//收到开启成功的响应后使其可用
                            //            if (b.Name == btnOFFList[j - 1].Name)
                            //                control.Enabled = false;//收到开启成功的响应后使其可用
                            //        }
                            //    }
                            //    send = Encoding.ASCII.GetBytes("P" + label1.Text[-1].ToString() + "0" + "S00" + "co" + "1" + label1.Text[-1].ToString() + j.ToString());//现在分配好了接口号，告知插座
                            //    ClientSocket2.Send(send);
                            //    break;
                            //}
                            if (s == "openI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));
                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "I" + i.ToString() + j.ToString() + "op" + "0" + i.ToString() + j.ToString() + "1");
                                socketsList[j - 1].Send(send);
                                break;
                            }
                            if (s == "openedI")
                            {
                                //变UI
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//第几个插口
                                foreach (Control control in groupBox_ChargingPile.Controls)
                                {
                                    if (control is RadioButton)
                                    {
                                        RadioButton r = (RadioButton)control;
                                        if (r.Name == rbtnList[j - 1].Name)
                                        {
                                            control.Enabled = true;//收到开启成功的响应后使其可用}
                                        }
                                    }
                                    if (control is Button)
                                    {
                                        Button b = (Button)control;
                                        if (b.Name == btnONList[j - 1].Name)
                                            control.Enabled = false;//收到开启成功的响应后使其可用
                                        if (b.Name == btnOFFList[j - 1].Name)
                                            control.Enabled = true;//收到开启成功的响应后使其可用
                                    }
                                }

                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "S00" + "op" + "1" + i.ToString() + j.ToString() + "1");
                                ClientSocket1.Send(send);//通知服务器插座已开启
                                break;
                            }
                            if (s == "closeI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));
                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "I" + i.ToString() + j.ToString() + "cl" + "0" + i.ToString() + j.ToString() + "1");
                                socketsList[j - 1].Send(send);
                                break;
                            }
                            if (s == "closedI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//第几个插口
                                foreach (Control control in groupBox_ChargingPile.Controls)
                                {
                                    if (control is RadioButton)
                                    {
                                        RadioButton r = (RadioButton)control;
                                        if (r.Name == rbtnList[j - 1].Name)
                                        {
                                            control.Enabled = false;//收到开启成功的响应后使其可用}
                                        }
                                    }
                                    if (control is Button)
                                    {
                                        Button b = (Button)control;
                                        if (b.Name == btnONList[j - 1].Name)
                                            control.Enabled = true;//收到开启成功的响应后使其可用
                                        if (b.Name == btnOFFList[j - 1].Name)
                                            control.Enabled = false;//收到开启成功的响应后使其可用
                                    }
                                }
                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "S00" + "cl" + "1" + i.ToString() + j.ToString() + "1");
                                ClientSocket1.Send(send);
                                break;
                            }
                            if (s == "openP")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));
                                radioButton.Checked = true;
                                groupBox_ChargingPile.Enabled = true;
                                //button_Close.Enabled = true;
                                button1.Enabled = true;
                                //button_Connect.Enabled = true;
                                button1.Enabled = true;
                                label1.Text = "充电桩" + i.ToString();
                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "S00" + "op" + "1" + i.ToString() + "0" + "0");
                                ClientSocket1.Send(send);
                                break;
                            }
                            if (s == "closeP")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));
                                for(int j = 0; j < socketsList.Count; j++)
                                {
                                    send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "I"+ i.ToString() + j.ToString() + "cl" + "0" + i.ToString() + (j + 1).ToString() + "1");
                                    socketsList[j].Send(send);
                                }
                                radioButton.Checked = false;
                                groupBox_ChargingPile.Enabled = false;
                                //button_Close.Enabled = false;
                                button1.Enabled = false;
                                //button_Connect.Enabled = true;
                                button1.Enabled = true;
                                send = Encoding.ASCII.GetBytes("P" + i.ToString() + "0" + "S00" + "cl" + "1" + i.ToString() + "0" + "0");
                                ClientSocket1.Send(send);
                                break;
                            }
                        }
                        else
                        {
                            break;  //跳出循环
                        }
                    }
                    catch
                    {
                        //MessageBox.Show("收不到信息");
                        break;
                    }
                }
            }

            
            
        }

        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 发送消息
        void Send(Object sk)//目标Sockets sk
        {

            Socket socket = sk as Socket;  //创建用于通信的套接字(这里是线程传过来的client套接字)
            //SFlag标志连接成功，同时当客户端是打开的时候才能发送数据
            if (SFlag == 1 && CFlag == 0)
            {
                //byte[] send = new byte[1024];
                //send = Encoding.ASCII.GetBytes(richTextBox_Send.Text);    //将字符串转成字节格式发送
                //send = Encoding.UTF8.GetBytes(richTextBox_Send.Text);
                /*
                 * 上面将每一个连接的client的套接字信息（ip和端口号）存放进了combox
                 * 我们可以在combox中选择需要通信的客户端
                 * 通过comboBox_Clients.SelectedIndex获取选择的index，此index对于List中的socket对象
                 * 从而实现对选择的客户端发送信息
                 */
                //int i = comboBox_Clients.SelectedIndex;
                //string client = sk.ToString();
                socket.Send(send); //调用Send()向客户端发送数据

                //打印发送时间和发送的数据
                //richTextBox_CMD.Text += "*" + DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ") + "向" + client + "发送：";
                //richTextBox_CMD.Text += send + "\n";
                //richTextBox_Send.Clear();   //清除发送框
            }
        }


        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 打开插座N-N
        void openChargingInterface(int i, int j)
        {

        }
        private void button_1_on_Click(object sender, EventArgs e)
        {

        }

        private void button_1_off_Click(object sender, EventArgs e)
        {

        }

        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 关闭充电桩
        private void button_Close_Click(object sender, EventArgs e)
        {
            //保证是在连接状态下退出
            if (SFlag == 1)
            {
                byte[] send = new byte[1024];
                send = Encoding.ASCII.GetBytes("*close*");  //关闭客户端时给服务器发送一个退出标志
                ClientSocket1.Send(send);

                th1.Abort();    //关闭线程
                ClientSocket1.Close();   //关闭套接字
                button_Connect.Enabled = true;  //允许操作按钮
                SFlag = 0;  //客户端退出后将连接成功标志程序设置为0
                //richTextBox_Recieve.Text += DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ");
                //richTextBox_Recieve.Text += "客户端已关闭" + "\n";
                MessageBox.Show("已关闭连接");
            }
        }
        #endregion
        /*****************************************************************/


        private void button_getid_Click(object sender, EventArgs e)
        {
            ////告知S和P已上线
            //send = Encoding.ASCII.GetBytes("P" + "-" + "0" + "S00" + "co" + "1" + "-" + "0");//告知服务器充电桩P已经连接，告知服务器分配一个充电桩号
            //ClientSocket1.Send(send);
            //Receive(ClientSocket3);
        }


    }
}
