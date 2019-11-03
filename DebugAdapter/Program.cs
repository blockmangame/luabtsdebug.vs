using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugAdapter
{
    class Program
    {
        static TcpClient tcp = new TcpClient();
        static Stream stdin = Console.OpenStandardInput();
        static Stream stdout = Console.OpenStandardOutput();
        static MemoryStream recvBuf = new MemoryStream(0x1000);

        static void Main(string[] args)
        {
            recvBuf.SetLength(recvBuf.Capacity);
            //System.Windows.Forms.MessageBox.Show("wait attach");
            while (ProcessStdin()) ;

            if (!tcp.Connected)
                return;

            CancellationTokenSource source = new CancellationTokenSource();
            stdin.CopyToAsync(tcp.GetStream(), 0x1000, source.Token);
            tcp.GetStream().CopyTo(stdout);
            source.Cancel();
            tcp.Close();
            tcp.Dispose();
        }

        static bool ProcessStdin()
        {
            byte[] buf = recvBuf.GetBuffer();
            recvBuf.Position += stdin.Read(buf, (int)recvBuf.Position, buf.Length - (int)recvBuf.Position);
            int index;
            for (int i = 0; ; i++)
            {
                if (i + 4 > recvBuf.Position)
                    return true;
                if (buf[i] == '\r' && buf[i + 1] == '\n' & buf[i + 2] == '\r' & buf[i + 3] == '\n')
                {
                    index = i;
                    break;
                }
            }

            string line = Encoding.UTF8.GetString(buf, 0, index);
            string[] ary = line.Split(':');
            Trace.Assert(ary.Length == 2);
            Trace.Assert(ary[0] == "Content-Length");
            int len = int.Parse(ary[1]);
            index += 4;
            if (index + len > recvBuf.Position)
                return true;

            var data = CJsonHelper.Load(Encoding.UTF8.GetString(buf, index, len)) as Dictionary<string, object>;

            index += len;
            for (int i = 0; i + index < recvBuf.Position; i++)
                buf[i] = buf[i + index];
            recvBuf.Position -= index;

            string cmd = data["command"] as string;
            if (cmd == "initialize")
            {
                var res = new Dictionary<string, object>();
                res["type"] = "response";
                res["command"] = data["command"];
                res["success"] = true;
                res["request_seq"] = data["seq"];
                var body = new Dictionary<string, object>();
                res["body"] = body;
                res["seq"] = 1;
                body["supportsConfigurationDoneRequest"] = true;
                body["supportsEvaluateForHovers"] = true;
                body["supportsSetVariable"] = true;
                body["supportsExceptionInfoRequest"] = true;
                var excs = new Dictionary<string, object>[2];
                body["exceptionBreakpointFilters"] = excs;
                excs[0] = new Dictionary<string, object>();
                excs[0]["filter"] = "perror";
                excs[0]["label"] = "SCRIPT_EXCEPTION output";
                excs[0]["default"] = false;
                excs[1] = new Dictionary<string, object>();
                excs[1]["filter"] = "xpcall";
                excs[1]["label"] = "Exception in xpcall";
                excs[1]["default"] = true;
                buf = Encoding.UTF8.GetBytes(Msg2Txt(res));
                stdout.Write(buf, 0, buf.Length);
                stdout.Flush();
                return true;
            }
            else if (cmd == "attach" || cmd == "launch")
            {
                DoAttach(data);
                return false;
            }
            return true;
        }

        static string Msg2Txt(Dictionary<string, object> res)
        {
            string json = CJsonHelper.Save(res);
            return "Content-Length: " + Encoding.UTF8.GetByteCount(json).ToString() + "\r\n\r\n" + json;
        }

        static void DoAttach(Dictionary<string, object> req)
        {
            System.Net.IPEndPoint addr;
            var args = req["arguments"] as Dictionary<string, object>;
            using (AttachForm frm = new AttachForm())
            {
                frm.LocalSource = args.ContainsKey("resDir");
                if (frm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;
                addr = frm.attachAddr;
                if (!frm.LocalSource)
                {
                    args.Remove("resDir");
                    args.Remove("gameDir");
                }
            }
            try
            {
                tcp.Connect(addr);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("connect failed 3.");
                return;
            }
            byte[] buf = Encoding.UTF8.GetBytes(Msg2Txt(req));
            tcp.GetStream().Write(buf, 0, buf.Length);
        }
    }
}
