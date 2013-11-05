using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace VVVV.Nodes.ARDrone
{	
	/// <summary>
	/// Description of NavData.
	/// </summary>
	public class NavData
	{
		public event EventHandler NavDataReceived;
		protected virtual void OnNavDataReceived(EventArgs e)
		{
			EventHandler handler = NavDataReceived;
			if (handler != null)
            	handler(this, e);
		}
		
		public event EventHandler<IssueSendEventArgs> IssueInit;
		protected virtual void OnIssueInit(IssueSendEventArgs e)
		{
			EventHandler<IssueSendEventArgs> handler = IssueInit;
			if (handler != null)
            	handler(this, e);
		}
		
		private ARDrone drone;
		
		private NavDataHeader dataHeader;
		private bool[] statusData;
		public bool[] Status { get { return statusData; } }
		private bool NavDataDemoOnly { get { return statusData[10]; } }
		
		private NavDataDemo dataDemo;
		public NavDataDemo Body { get { return dataDemo; } }
		public SlimDX.Vector3 Position { get { return dataDemo.DroneCamTrans; } }
		public SlimDX.Matrix Rotation 
		{ 
			get 
			{ 
				SlimDX.Matrix m = new SlimDX.Matrix();
				m.set_Rows(0,new SlimDX.Vector4(dataDemo.DroneCamRot.row1,0));
				m.set_Rows(1,new SlimDX.Vector4(dataDemo.DroneCamRot.row2,0));
				m.set_Rows(2,new SlimDX.Vector4(dataDemo.DroneCamRot.row3,0));
				m.set_Rows(3,new SlimDX.Vector4(0,0,0,1));
				return m; 
			} 
		}
		
		private uint timeStamp;
		public uint TimeStamp { get { return timeStamp; } }
		private int dTime;
		public int DeltaTime { get { return NavDataDemoOnly?66:dTime/1000; } }
		
		private System.Timers.Timer initTimer;
		
		public NavData(ARDrone Drone)
		{
			this.drone = Drone;
			this.drone.StatusChanged += new EventHandler<DroneStatusChangedEventArgs>(DroneStatusChanged);
			statusData = new bool[32];
			dataHeader = new NavDataHeader();
			dataDemo = new NavDataDemo();
			
			timeStamp = 0;
			dTime = 66;
			
			initTimer = new System.Timers.Timer(200);
			initTimer.AutoReset = true;
			initTimer.Elapsed += delegate { SendInit(); };
		}

		private void DroneStatusChanged(object sender, DroneStatusChangedEventArgs e)
		{
			if((int)e.FormerStatus < (int)DroneStatus.Available)
			{
				if (((int)e.Status) >= ((int)DroneStatus.Available))
				{
					SendInit();
					initTimer.Enabled = true;
				}
			}
			if((int)e.FormerStatus == (int)DroneStatus.Available)
			{
				initTimer.Enabled = false;
			}
		}
		
		private void SendInit()
		{
			OnIssueInit(new IssueSendEventArgs(new IPEndPoint(this.drone.IPAddress, 0), BitConverter.GetBytes(1)));
		}
		
		public void DataReceived(object sender, DataReceivedEventArgs e)
		{
			if (e.Source.Address.Equals( this.drone.IPAddress))
			{
				MemoryStream ms = new MemoryStream(e.Data);
				byte[] rawHeader = new byte[16]; //navData header size
				ms.Read(rawHeader, 0 , rawHeader.Length);
				unsafe { fixed (byte* header = &rawHeader[0]) { dataHeader = *(NavDataHeader*)header; } }
				if (dataHeader.Header == 0x55667788)
				{
					SetStatus();
					while (ms.Position < ms.Length)
					{
						byte[] rawOptionHeader = new byte[4];
						ms.Read(rawOptionHeader, 0, rawOptionHeader.Length);
						ushort tag = BitConverter.ToUInt16(rawOptionHeader,0);
						ushort size = BitConverter.ToUInt16(rawOptionHeader,2);
						byte[] rawOptionBody = new byte[size-4];
						ms.Read(rawOptionBody, 0, rawOptionBody.Length);
						switch (tag)
						{
							case 0xFFFF:
								long chksum = BitConverter.ToUInt32(rawOptionBody,0);
								break;
							case 0: //navdata_demo
								unsafe { fixed (byte* optionBody = &rawOptionBody[0]) { dataDemo = *(NavDataDemo*)optionBody; } }
								OnNavDataReceived(EventArgs.Empty);
								break;
							case 1: //time tag
								uint newTime = BitConverter.ToUInt32(rawOptionBody, 0);
								int _dTime = (int)(newTime - timeStamp);
								if (_dTime > 0)
									dTime = _dTime;
								timeStamp = newTime;
								break;
							default: // TODO: approx 20 more navdata options
								break;
						}
					}
				}
			}
		}
		
		private void SetStatus()
		{
			if ((int)drone.Status < (int) DroneStatus.Connected)
				drone.Status = DroneStatus.Connected;
			
			for (int i=0; i<32; i++)
				statusData[i] = (dataHeader.Status & (1 << i)) != 0;
			
			if (statusData[0])
				drone.Status = DroneStatus.Flying;
			else
				drone.Status = DroneStatus.Ready;
			
			if (statusData[31])
				drone.Status = DroneStatus.Emergency;
			
			if (statusData[15])
				drone.Status = DroneStatus.BatteryLow;
		}
	}
}
