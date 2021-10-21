using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebugAdapter
{
    public partial class AttachForm : Form
    {
        class TargetItem
        {
            public IPEndPoint addr;
            public Dictionary<string, object> data;

            public override string ToString()
            {
                string typ = "server";
                object c;
                if (data.TryGetValue("client", out c) && (bool)c)
                    typ = "client";
                object sys;
                if (!data.TryGetValue("device", out sys))
                    sys = data["sys"];
                return string.Format("{0} - {1}<{2}.{3}> {4} - {5}", addr,
                    data["name"], data["game"], data["engineVersion"], typ, sys);
            }

            public string Info
            {
                get
                {
                    StringBuilder sb = new StringBuilder("[" + addr.ToString() + "]");
                    foreach (var pair in data)
                        sb.AppendFormat("\r\n{0}: {1}", pair.Key, pair.Value);
                    return sb.ToString();
                }
            }
        }

        UdpClient udp = new UdpClient(0);
        Dictionary<IPEndPoint, TargetItem> list = new Dictionary<IPEndPoint, TargetItem>();
        Properties.Settings setting = Properties.Settings.Default;

        public IPEndPoint attachAddr = null;
        public bool LocalSource
        {
            get { return checkBoxLocalSource.Checked; }
            set { checkBoxLocalSource.Checked = checkBoxLocalSource.Enabled = value; }
        }

        public AttachForm()
        {
            InitializeComponent();
        }

        private void SendUdp(string typ, IPEndPoint addr)
        {
            byte[] buf = Encoding.UTF8.GetBytes("{\"typ\":\"" + typ + "\"}");
            udp.Send(buf, buf.Length, addr);
        }

        private Dictionary<string, object> RecvUdp(ref IPEndPoint addr)
        {
            byte[] buf = udp.Receive(ref addr);
            if (buf.Length < 2)
                return null;
            return CJsonHelper.Load(Encoding.UTF8.GetString(buf)) as Dictionary<string, object>;
        }

        void updateList()
        {
            listBoxAddr.Items.Clear();
            string filter = textBoxFilter.Text.ToLower();
            foreach (TargetItem item in list.Values)
            {
                if (filter == "" || item.Info.ToLower().IndexOf(filter) >= 0)
                    listBoxAddr.Items.Add(item);
            }
        }

        private void AttachForm_Load(object sender, EventArgs e)
        {
            udp.EnableBroadcast = true;
            buttonRefresh_Click(sender, e);
            textBoxFilter.Text = setting.filter;
        }

        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            timerRefresh.Stop();
            list.Clear();
            while (udp.Available > 0)
            {
                IPEndPoint addr = new IPEndPoint(0, 0);
                var data = RecvUdp(ref addr);
                if (data == null)
                    continue;
                list[addr] = new TargetItem()
                {
                    addr = addr,
                    data = data,
                };
            }
            updateList();
        }

        private void listBoxAddr_SelectedIndexChanged(object sender, EventArgs e)
        {
            TargetItem item = listBoxAddr.SelectedItem as TargetItem;
            if (item == null)
            {
                textBoxInfo.Text = "";
                buttonOk.Enabled = false;
            }
            else
            {
                textBoxInfo.Text = item.Info;
                buttonOk.Enabled = true;
            }
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            setting.filter = textBoxFilter.Text;
            setting.Save();
            TargetItem item = listBoxAddr.SelectedItem as TargetItem;
            if (item == null)
                return;
            SendUdp("debug", item.addr);
            if (!udp.Client.Poll(500 * 1000, SelectMode.SelectRead))
            {
                MessageBox.Show("connect failed 1.");
                return;
            }
            IPEndPoint addr = new IPEndPoint(0, 0);
            Dictionary<string, object> data;
            object _typ;
            do
            {
                data = RecvUdp(ref addr);
                if (data == null)
                {
                    MessageBox.Show("connect failed 2.");
                    return;
                }
            } while (data.TryGetValue("typ", out _typ) && _typ as string != "debug");
            addr.Port = (int)data["port"];
            attachAddr = addr;
            DialogResult = DialogResult.OK;
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            byte[] buf = Encoding.UTF8.GetBytes("{\"typ\":\"info\"}");
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (adapter.Supports(NetworkInterfaceComponent.IPv4) == false)
                    continue;
                foreach (var ua in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.Bind(new IPEndPoint(ua.Address, ((IPEndPoint)udp.Client.LocalEndPoint).Port));
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                    IPEndPoint addr = new IPEndPoint(IPAddress.Broadcast, 0);
                    for (int i = 0; i < 10; i++)
                    {
                        addr.Port = 6600 + i;
                        socket.SendTo(buf, addr);
                        addr.Port = 6661 + i;
                        socket.SendTo(buf, addr);
                    }
                    socket.Close();
                }
            }
            timerRefresh.Start();
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            textBoxFilter.Text = "";
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            buttonClear.Enabled = textBoxFilter.Text != "";
            updateList();
        }
    }
}
