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


namespace Server
{
    public partial class Form1 : Form
    {
        //这里声明多个套接字是为了在连接，接收数据，发送数据的函数中不发生混乱，同时方便关闭
        public Socket ServerSocket;    //声明用于监听的套接字
        public static List<Socket> socketsList = new List<Socket>();    //创建一个全局的List用来存放不同的Client套接字

        public static int SFlag = 0;    //连接成功标志
        public static int CFlag = 0;    //客户端关闭的标志

        public static byte[] send = new byte[16];//发送报文
        public static byte[] recieve = new byte[16];//接收报文


        public static List<RadioButton> rbtnList1 = new List<RadioButton>(10);
        public static List<RadioButton> rbtnList2 = new List<RadioButton>(10);
        public static List<Button> btnONList1 = new List<Button>(10);
        public static List<Button> btnOFFList1 = new List<Button>(10);
        public static List<Button> btnONList2 = new List<Button>(10);
        public static List<Button> btnOFFList2 = new List<Button>(10);

        Thread th1;     //声明线程1,绑定Listen
        Thread th2;     //声明线程2,绑定Receive

        public static int port = 8001;//设置默认端口号


        public Form1()
        {
            InitializeComponent();
        }



        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;    //执行新线程时跨线程资源访问检查会提示报错，所以这里关闭检测

            string ip = "";
            ///获取本地的IP地址
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    ip = _IPAddress.MapToIPv4().ToString();
                    textBox_IP.Text = string.Format(ip);//默认获取本机IP
                }
            }
            textBox_Port.Text = port.ToString();//设置默认端口

            groupBox_ChargingPile1.Enabled = false;
            radioButton1.Checked = false;
            groupBox_ChargingPile2.Enabled = false;
            radioButton2.Checked = false;
            

            for(int i = 0; i < 10; i++)
            {
                RadioButton rbt = new RadioButton();
                Button bt = new Button();
                rbtnList1.Add(rbt);
                rbtnList2.Add(rbt);
                btnONList1.Add(bt);
                btnONList2.Add(bt);
                btnOFFList1.Add(bt);
                btnOFFList2.Add(bt);
            }

            //创建按钮数组
            foreach(Control control in groupBox_ChargingPile1.Controls)
            {
                if (control is RadioButton)
                {
                    rbtnList1[int.Parse(control.Name[14].ToString())] = (RadioButton)control;
                    control.Enabled = false;
                }

                if ((control is Button) && control.Text == "开")
                {
                    btnONList1[int.Parse(control.Name[9].ToString())] = (Button)control;
                }
                if ((control is Button) && control.Text == "关")
                {
                    btnOFFList1[int.Parse(control.Name[9].ToString())] = (Button)control;
                    control.Enabled = false;
                }
            }
            foreach (Control control in groupBox_ChargingPile2.Controls)
            {
                if (control is RadioButton)
                {
                    rbtnList2[int.Parse(control.Name[14].ToString())] = (RadioButton)control;
                    control.Enabled = false;
                }

                if ((control is Button) && control.Text == "开")
                {
                    btnONList2[int.Parse(control.Name[9].ToString())] = (Button)control;
                }
                if ((control is Button) && control.Text == "关")
                {
                    btnOFFList2[int.Parse(control.Name[9].ToString())] = (Button)control;
                    control.Enabled = false;
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
            if(Encoding.UTF8.GetString(text, 11, 1) == "0")
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
        #region 与客户端进行配对
        private void button_Accept_Click(object sender, EventArgs e)
        {
            ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);     //创建用于通信的套接字
            button_Accept.Enabled = false;



            //1.绑定IP和Port
            IPAddress IP = IPAddress.Parse(textBox_IP.Text);
            int Port = int.Parse(textBox_Port.Text);

            //IPAddress ip = IPAddress.Any;
            IPEndPoint iPEndPoint = new IPEndPoint(IP, Port);

            try
            {
                //2.使用Bind()进行绑定
                ServerSocket.Bind(iPEndPoint);
                //3.开启监听
                //Listen(int backlog); backlog:监听数量 
                ServerSocket.Listen(10);

                /*
                 * tip：
                 * Accept会阻碍主线程的运行，一直在等待客户端的请求，
                 * 客户端如果不接入，它就会一直在这里等着，主线程卡死
                 * 所以开启一个新线程接收客户单请求
                 */

                //开启线程Accept进行通信的客户端socket
                th1 = new Thread(Listen);   //线程绑定Listen函数
                th1.Name = "listen";//
                th1.IsBackground = true;    //运行线程在后台执行
                th1.Start(ServerSocket);    //Start里面的参数是Listen函数所需要的参数，这里传送的是用于通信的Socket对象
                Console.WriteLine("1");
                richTextBox_CMD.Text += "正在配对......\r\n";
            }
            catch
            {
                MessageBox.Show("服务器出问题了");
            }
        }
        #endregion
        /*****************************************************************/


        /*****************************************************************/
        #region 建立与客户端的连接
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
                    Socket Client = ServerSocket.Accept(); //声明用于与某一个客户端通信的套接字
                    socketsList.Add(Client);               //将连接的客户端存进List
                    

                    //获取客户端信息将不同客户端并存进comBox
                    string client = Client.RemoteEndPoint.ToString();
                    comboBox_Clients.Items.Add(client);
                    comboBox_Clients.SelectedIndex = 0;

                    CFlag = 0;  //连接成功，将客户端关闭标志设置为0
                    SFlag = 1;  //当连接成功，将连接成功标志设置为1

                    richTextBox_CMD.Text += DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ") + client + "连接成功";
                    richTextBox_CMD.Text += "\r\n";

                    //连接成功后将checkedlistbox打开


                    //开启第二个线程接收客户端数据
                    th2 = new Thread(Receive)
                    {
                        IsBackground = true    //运行线程在后台执行
                    };  //线程绑定Receive函数
                    th2.Name = "receive";
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
        #region 接收充电桩消息
        void Receive(Object sk)
        {
            Socket socket = sk as Socket;  //创建用于通信的套接字(这里是线程传过来的client套接字)

            while(true)
            {
                while (true)
                {
                    try
                    {
                        if (CFlag == 0 && SFlag == 1)
                        {

                            socket.Receive(recieve);
                            
                            if(Analyse(recieve) == "connectedP")
                            {
                                int i = socketsList.Count - 1;
                                foreach (Control control in groupBox_ChargingPile1.Controls)
                                {
                                    if (control is RadioButton)
                                    {
                                        RadioButton r = (RadioButton)control;
                                        if (r.Name == rbtnList1[i - 1].Name)
                                        {
                                            control.Enabled = true;//收到连接成功的响应后使其可用
                                        }
                                    }
                                    if (control is Button)
                                    {
                                        Button b = (Button)control;
                                        if (b.Name == btnONList1[i - 1].Name)//连接成功，但仍处于关闭状态
                                            control.Enabled = true;//收到开启成功的响应后使其可用
                                        if (b.Name == btnOFFList1[i - 1].Name)
                                            control.Enabled = false;//收到开启成功的响应后使其可用
                                    }
                                }
                                foreach (Control control in groupBox_ChargingPile1.Controls)
                                {
                                    if (control is RadioButton)
                                    {
                                        RadioButton r = (RadioButton)control;
                                        if (r.Name == rbtnList2[i - 1].Name)
                                        {
                                            control.Enabled = true;//收到连接成功的响应后使其可用}
                                        }
                                    }
                                    if (control is Button)
                                    {
                                        Button b = (Button)control;
                                        if (b.Name == btnONList2[i - 1].Name)//连接成功，但仍处于关闭状态
                                            control.Enabled = true;//收到开启成功的响应后使其可用
                                        if (b.Name == btnOFFList2[i - 1].Name)
                                            control.Enabled = false;//收到开启成功的响应后使其可用
                                    }
                                }
                                send = Encoding.ASCII.GetBytes("S00" + "P"+ i.ToString() + "0" + "co" + "1" + i.ToString() + "0");//现在分配好了充电桩号，告知充电桩
                                ServerSocket.Send(send);
                                break;
                            }
                            if (Analyse(recieve) == "openedI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//第几个插口
                                if(i == 1)
                                {
                                    foreach (Control control in groupBox_ChargingPile1.Controls)
                                    {

                                        if (control is RadioButton)
                                        {
                                            RadioButton r = (RadioButton)control;
                                            if (r.Name == rbtnList1[j].Name)
                                            {
                                                control.Enabled = true;//收到开启成功的响应后使其可用}
                                            }
                                        }
                                        if (control is Button)
                                        {
                                            Button b = (Button)control;
                                            if (b.Name == btnONList1[j].Name)
                                                control.Enabled = false;//收到开启成功的响应后使其可用
                                            if (b.Name == btnOFFList1[j].Name)
                                                control.Enabled = true;//收到开启成功的响应后使其可用
                                        }


                                    }
                                }
                                else
                                {
                                    foreach (Control control in groupBox_ChargingPile2.Controls)
                                    {
                                        if (control is RadioButton)
                                        {
                                            RadioButton r = (RadioButton)control;
                                            if (r.Name == rbtnList2[j].Name)
                                            {
                                                control.Enabled = true;//收到开启成功的响应后使其可用}
                                            }
                                        }
                                        if (control is Button)
                                        {
                                            Button b = (Button)control;
                                            if (b.Name == btnONList2[j].Name)
                                                control.Enabled = false;//收到开启成功的响应后使其可用
                                            if (b.Name == btnOFFList2[j].Name)
                                                control.Enabled = true;//收到开启成功的响应后使其可用
                                        }
                                    }
                                }
                                
                                
                                richTextBox_CMD.Text += "接口" + i.ToString() + "-" + j.ToString() + "已开启\r\n";
                            }
                            if (Analyse(recieve) == "closedI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//第几个插口
                                if(i == 1)
                                {
                                    foreach (Control control in groupBox_ChargingPile1.Controls)
                                    {

                                        if (control is RadioButton)
                                        {
                                            RadioButton r = (RadioButton)control;
                                            if (r.Name == rbtnList1[j].Name)
                                            {
                                                control.Enabled = false;//收到开启成功的响应后使其可用
                                            }
                                        }
                                        if (control is Button)
                                        {
                                            Button b = (Button)control;
                                            if (b.Name == btnONList1[j].Name)
                                                control.Enabled = true;//收到开启成功的响应后使其可用
                                            if (b.Name == btnOFFList1[j].Name)
                                                control.Enabled = false;//收到开启成功的响应后使其可用
                                        }


                                    }
                                }
                                else
                                {
                                    foreach (Control control in groupBox_ChargingPile2.Controls)
                                    {
                                        if (control is RadioButton)
                                        {
                                            RadioButton r = (RadioButton)control;
                                            if (r.Name == rbtnList2[j].Name)
                                            {
                                                control.Enabled = false;//收到开启成功的响应后使其可用}
                                            }
                                        }
                                        if (control is Button)
                                        {
                                            Button b = (Button)control;
                                            if (b.Name == btnONList2[j].Name)
                                                control.Enabled = true;//收到开启成功的响应后使其可用
                                            if (b.Name == btnOFFList2[j].Name)
                                                control.Enabled = false;//收到开启成功的响应后使其可用
                                        }
                                    }
                                }
                                
                                
                                richTextBox_CMD.Text += "接口" + i.ToString() + "-" + j.ToString() + "已关闭\r\n";
                            }
                            if (Analyse(recieve) == "openedP")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                if(i==1)
                                {
                                    groupBox_ChargingPile1.Enabled = true;
                                    button_Pile1ON.Enabled = false;
                                    button_Pile1OFF.Enabled = true;
                                    radioButton1.Checked = true;
                                }
                                if(i==2)
                                {
                                    groupBox_ChargingPile2.Enabled = true;
                                    button_Pile2ON.Enabled = false;
                                    button_Pile2OFF.Enabled = true;
                                    radioButton2.Checked = true;
                                }
                                richTextBox_CMD.Text += "充电桩" + i.ToString() + "已打开\r\n";
                                break;
                            }
                            if (Analyse(recieve) == "closedP")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                if (i == 1)
                                {
                                    groupBox_ChargingPile1.Enabled = false;
                                    button_Pile1ON.Enabled = true;
                                    button_Pile1OFF.Enabled = false;
                                    radioButton1.Checked = false;
                                }
                                if (i == 2)
                                {
                                    groupBox_ChargingPile2.Enabled = false;
                                    button_Pile2ON.Enabled = true;
                                    button_Pile2OFF.Enabled = false;
                                    radioButton2.Checked = false;
                                }
                                richTextBox_CMD.Text += "充电桩" + i.ToString() + "已关闭\r\n";
                                break;
                            }
                            if (Analyse(recieve) == "getP")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                List<char> F = new List<char>(10);
                                for (int y = 0; y < 10; y++) F.Add('0');
                                if (i == 1)
                                {
                                    if (!button_1_1_on.Enabled) F[1] = '1';
                                    if (!button_1_2_on.Enabled) F[2] = '1';
                                    if (!button_1_3_on.Enabled) F[3] = '1';
                                    if (!button_1_4_on.Enabled) F[4] = '1';
                                    if (!button_1_5_on.Enabled) F[5] = '1';
                                    if (!button_1_6_on.Enabled) F[6] = '1';
                                    if (!button_1_7_on.Enabled) F[7] = '1';
                                    if (!button_1_8_on.Enabled) F[8] = '1';
                                    if (!button_1_9_on.Enabled) F[9] = '1';
                                    if (!button_1_0_on.Enabled) F[0] = '1';
                                }
                                if (i == 2)
                                {
                                    if (!button_2_1_on.Enabled) F[1] = '1';
                                    if (!button_2_2_on.Enabled) F[2] = '1';
                                    if (!button_2_3_on.Enabled) F[3] = '1';
                                    if (!button_2_4_on.Enabled) F[4] = '1';
                                    if (!button_2_5_on.Enabled) F[5] = '1';
                                    if (!button_2_6_on.Enabled) F[6] = '1';
                                    if (!button_2_7_on.Enabled) F[7] = '1';
                                    if (!button_2_8_on.Enabled) F[8] = '1';
                                    if (!button_2_9_on.Enabled) F[9] = '1';
                                    if (!button_2_0_on.Enabled) F[0] = '1';
                                }
                                //二进制字符串转换为十六进制
                                string f = "";
                                int t = 0;
                                for(int y = 0; y < 10; y++)
                                {
                                    f += F[y];
                                    if(F[y] == '1')
                                    {
                                        t += (int)Math.Pow(2,(9-y));
                                    }
                                }

                                //int t = 0;
                                
                                string x = t.ToString();
                                if (x.Length == 4)//用四位来存
                                {
                                    send = Encoding.ASCII.GetBytes("S00" + "A00" + "gt" + "1" + i.ToString() + "00" + x);
                                }
                                else
                                {
                                    if (x.Length == 3)//用三位来存
                                    {
                                        send = Encoding.ASCII.GetBytes("S00" + "A00" + "gt" + "1" + i.ToString() + "000" + x);
                                    }
                                    else
                                    {
                                        if (x.Length == 2)
                                        {
                                            send = Encoding.ASCII.GetBytes("S00" + "A00" + "gt" + "1" + i.ToString() + "0000" + x);
                                        }
                                        else 
                                        {
                                            if (x.Length == 1)
                                            {
                                                send = Encoding.ASCII.GetBytes("S00" + "A00" + "gt" + "1" + i.ToString() + "00000" + x);
                                            }
                                        }
                                    }
                                }
                                socketsList[socketsList.Count()-1].Send(send);//客户端是最后加进来的
                                break;


                            }
                            if(Analyse(recieve) == "openI")
                            {
                                int i = int.Parse(Encoding.UTF8.GetString(recieve, 9, 1));//第几个充电桩
                                int j = int.Parse(Encoding.UTF8.GetString(recieve, 10, 1));//第几个插口
                                switch(j)
                                {
                                    case 1:
                                        button_1_1_on.PerformClick();
                                        break;
                                    case 2:
                                        button_1_2_on.PerformClick();
                                        break;
                                    case 3:
                                        button_1_3_on.PerformClick();
                                        break;
                                    case 4:
                                        button_1_4_on.PerformClick();
                                        break;
                                    case 5:
                                        button_1_5_on.PerformClick();
                                        break;
                                    case 6:
                                        button_1_6_on.PerformClick();
                                        break;
                                    case 7:
                                        button_1_7_on.PerformClick();
                                        break;
                                    case 8:
                                        button_1_8_on.PerformClick();
                                        break;
                                    case 9:
                                        button_1_9_on.PerformClick();
                                        break;
                                    case 0:
                                        button_1_0_on.PerformClick();
                                        break;
                                }
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
        #region 向充电桩发送消息
        void Send(object sk)//目标Sockets sk、Socketslist索引i、发送报文send
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
                //string client = socketsList[i].ToString();
                socket.Send(send); //调用Send()向客户端发送数据

                //打印发送时间和发送的数据
                richTextBox_CMD.Text += "*" + DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ") + "向" + socket.ToString() + "发送：";
                richTextBox_CMD.Text += send + "\n";
                //richTextBox_Send.Clear();   //清除发送框
            } 
        }


        #endregion
        /*****************************************************************/


        /*****************************************************************/
        #region 关闭服务器
        private void button_Close_Click(object sender, EventArgs e)
        {
            //若连接上了客户端需要关闭线程2和socket，没连接上客户端直接关闭线程1和其他套接字
            if (CFlag == 1)
            {
                th2.Abort();        //关闭线程2
                foreach (Socket s in socketsList)
                    s.Close();     //关闭用于通信的套接字
            }

            ServerSocket.Close();   //关闭用于连接的套接字
            //socketAccept.Close();   //关闭与客户端绑定的套接字
            if (th1.ThreadState == ThreadState.Running) th1.Abort();    //关闭线程1

            CFlag = 0;  //将客户端标志重新设置为0,在进行连接时表示是打开的状态
            SFlag = 0;  //将连接成功标志程序设置为0，表示退出连接
            button_Accept.Enabled = true;
            richTextBox_CMD.Text += DateTime.Now.ToString("yy-MM-dd hh:mm:ss  ");
            richTextBox_CMD.Text += "服务器已关闭" + "\n";
            MessageBox.Show("服务器已关闭");
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 开关插座N-N
        //用选中checkedlistbox中的选项的事件来调用
        void openChragingInterface(int i , int j)
        {
            send = Encoding.ASCII.GetBytes("S00" + "P" + i.ToString() + "0" + "op" + "0" + i.ToString() + j.ToString() + "1");
            socketsList[i - 1].Send(send);
        }

        void closeChragingInterface(int i, int j)
        {
            send = Encoding.ASCII.GetBytes("S00" + "P" + i.ToString() + "0" + "cl" + "0" + i.ToString() + j.ToString() + "1");
            socketsList[i - 1].Send(send);
        }

        void open(object sender , EventArgs e)
        {
            Button btn = (Button)sender;
            int i = int.Parse(btn.Name[7].ToString());
            int j = int.Parse(btn.Name[9].ToString());//提取充电桩号与插口号
            openChragingInterface(i, j);
        }

        void close(object sender,EventArgs e)
        {
            Button btn = (Button)sender;
            int i = int.Parse(btn.Name[7].ToString());
            int j = int.Parse(btn.Name[9].ToString());//提取充电桩号与插口号
            closeChragingInterface(i, j);
        }


        /*****************************************************************/
        #region 开事件绑定
        private void button_1_1_on_Click(object sender, EventArgs e)
        {
            open(sender,e);
        }

        private void button_1_2_on_Click_1(object sender, EventArgs e) { open(sender,e); }

        private void button_1_4_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_3_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_5_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_6_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_7_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_8_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_9_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_1_10_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_1_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_2_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_3_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_4_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_5_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_6_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_7_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_8_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_9_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }

        private void button_2_10_on_Click(object sender, EventArgs e)
        {
            open(sender, e);
        }
        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 关事件绑定
        private void button_1_1_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button11_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button13_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button15_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button17_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_1_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_2_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_3_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_4_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_5_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_6_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_7_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_8_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_9_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }

        private void button_2_10_off_Click(object sender, EventArgs e)
        {
            close(sender, e);
        }




        #endregion
        /*****************************************************************/

        #endregion
        /*****************************************************************/

        /*****************************************************************/
        #region 开关充电桩

        //开充电桩1
        private void button_Pile1ON_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("S00P10op0100");
            socketsList[0].Send(send);
        }


        //关充电桩1
        private void button_Pile1OFF_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("S00P10cl0100");
            socketsList[0].Send(send);
        }


        //开充电桩2
        private void button_Pile2ON_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("S00P20op0200");
            socketsList[1].Send(send);
        }


        //关充电桩2
        private void button_Pile2OFF_Click(object sender, EventArgs e)
        {
            send = Encoding.ASCII.GetBytes("S00P20cl0200");
            socketsList[1].Send(send);
        }

        #endregion
        /*****************************************************************/
    }
}
