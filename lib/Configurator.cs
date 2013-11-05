using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;


namespace VVVV.Nodes.ARDrone
{	
	/// <summary>
	/// Description of Configurator.
	/// </summary>
	/// 
	public class Configurator
	{
		enum Verbs
	    {
	        WILL = 251,
	        WONT = 252,
	        DO = 253,
	        DONT = 254,
	        IAC = 255
	    }
	
	    enum Options { SGA = 3 }
		
	    private const char prompt = '#';
		private const int port = 23;
		
		private ARDrone drone;
		private TcpClient client;
		
		private Thread worker;
		private volatile bool connected;
		private volatile bool doRead;
		
		public event EventHandler TelnetReceiveCompleted;
		protected virtual void OnTelnetReceiveCompleted(EventArgs e)
		{
			EventHandler handler = TelnetReceiveCompleted;
			if (handler != null)
            	handler(this, e);
		}
		
		private Queue<string> telnetCmds;
		private string telnetText;
		public string TelnetText { get { return telnetText; } }
		
		private bool hasConfig;
		public bool HasConfig { get { return hasConfig; } }
		
		private Dictionary<string, string> configs;
		public string this[string key] 
		{ 
			get 
			{ 
				string val = "";
				configs.TryGetValue(key, out val);
				return val;
			}
		}
		public string[] Keys 
		{ 
			get 
			{ 
				string[] ret = new string[configs.Count];
				configs.Keys.CopyTo(ret, 0);
				return ret;
			}
		}
		public string[] Values
		{
			get 
			{
				string[] ret = new string[configs.Count];
				configs.Values.CopyTo(ret, 0);
				return ret;
			}
		}
		
		public Configurator(ARDrone Drone)
		{
			this.drone = Drone;
			this.drone.StatusChanged += new EventHandler<DroneStatusChangedEventArgs>(DroneStatusChanged);
			
			
			connected = false;
			doRead = false;
			
			telnetCmds = new Queue<string>();
			telnetText = "";
			hasConfig = false;
			configs = new Dictionary<string, string>();
			TelnetReceiveCompleted += new EventHandler(TelnetReceived);
		}

		private void DroneStatusChanged(object sender, DroneStatusChangedEventArgs e)
		{
			if((int)e.FormerStatus < (int)DroneStatus.Available)
			{
				if ((int)drone.Status >= (int)DroneStatus.Available)
					Initialize();
			}
		}
		
		private void Initialize()
		{
			this.Connect();
			RequestConfig();
		}
		
		public bool SetConfig(string Key, string Value)
		{
			bool available = false;
			if (!(string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Value)))
			{
				available = configs.ContainsKey(Key);
				if (available)
					available = this.drone.Commander.ConfigCmdSend(Key, Value);
				
				RequestConfig();
			}
			return available;
		}
		
		private void RequestConfig()
		{
			hasConfig = false;
			IssueTelnet("cat /data/config.ini");
		}
		
		public void IssueTelnet(string cmd)
		{
			if (this.doRead)
				telnetCmds.Enqueue(cmd);
			else
				WriteTelnet(cmd);
		}
		
		private void WriteTelnet(string cmd)
		{
			cmd += "\n";
			byte[] buf = System.Text.ASCIIEncoding.ASCII.GetBytes(cmd.Replace("\0xFF", "\0xFF\0xFF"));
            client.GetStream().Write(buf, 0, buf.Length);
            doRead = true;
		}
		
		private void TelnetReceived(object sender, EventArgs e)
		{
			ParseConfig();
			
			if (telnetCmds.Count>0)
				WriteTelnet(telnetCmds.Dequeue());
		}
		
		private void ParseConfig()
		{
			int sectionCount = 0;
			string section = "";
			foreach (string line in telnetText.Split('\n'))
			{
				if (line.StartsWith("["))
				{
				    sectionCount++;
				    section = line.Substring(1,line.IndexOf(']')-1);
				}
				if (line.Contains(" = ") && sectionCount > 0)
				{
					string[] pair = line.Split('=');
					string key = section+":"+pair[0].Trim();
					string val = pair[1].Trim();
					if (configs.ContainsKey(key))
						configs[key]=val;
					else
						configs.Add(key,val);
				}
			}
			if (sectionCount > 0 )
				hasConfig = true;
		}
		
		// function from ARDrone-Control-.NET
		private void ParseTelnet(StringBuilder sb)
        {
            while (doRead)
            {
                int input = client.GetStream().ReadByte();
                switch (input)
                {
                    case -1:
                        break;
                    case (int)Verbs.IAC:
                        // interpret as command
                        int inputverb = client.GetStream().ReadByte();
                        if (inputverb == -1) break;
                        switch (inputverb)
                        {
                            case (int)Verbs.IAC:
                                //literal IAC = 255 escaped, so append char 255 to string
                                sb.Append(inputverb);
                                break;
                            case (int)Verbs.DO:
                            case (int)Verbs.DONT:
                            case (int)Verbs.WILL:
                            case (int)Verbs.WONT:
                                // reply to all commands with "WONT", unless it is SGA (suppres go ahead)
                                int inputoption = client.GetStream().ReadByte();
                                if (inputoption == -1) break;
                                client.GetStream().WriteByte((byte)Verbs.IAC);
                                if (inputoption == (int)Options.SGA)
                                    client.GetStream().WriteByte(inputverb == (int)Verbs.DO ? (byte)Verbs.WILL : (byte)Verbs.DO);
                                else
                                    client.GetStream().WriteByte(inputverb == (int)Verbs.DO ? (byte)Verbs.WONT : (byte)Verbs.DONT);
                                client.GetStream().WriteByte((byte)inputoption);
                                break;
                            default:
                                break;
                        }
                        break;
                   case (int)prompt:
                        doRead = false; //# seems to be busybox telnet prompt
                        sb.Append((char)input);
                        break;
                    default:
                        sb.Append((char)input);
                        break;
                }
            }
        }
		
		public void Connect()
		{
			if (!connected)
			{
				connected = true;
				client = new TcpClient();
				client.ExclusiveAddressUse = false;
				client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				client.Connect(drone.IPAddress, port);
				
				worker = new Thread(ReceiveWorker);
				worker.Start();
				doRead = true;
			}
		}
		
		private void ReceiveWorker()
		{
			while (this.connected)
			{
				if (doRead)
				{
					StringBuilder sb = new StringBuilder();
					ParseTelnet(sb);
	            	telnetText = sb.ToString();
	            	TelnetReceiveCompleted.BeginInvoke(this, new EventArgs(), ar => { var del = (EventHandler)ar.AsyncState; del.EndInvoke(ar);}, TelnetReceiveCompleted);
				}
				Thread.Sleep(10);
			}
		}
		
		public void Disconnect()
		{
			if (connected)
			{
				doRead = false;
				connected = false;
				
				worker.Join(10);
		        worker.Abort();
		        worker = null;
		        
				client.Client.Close();
			}
		}
	}
}
