using System;
using System.Net;
using System.Net.Sockets;

using System.Collections;
using System.Collections.Generic;

namespace VVVV.Nodes.ARDrone
{
	/// <summary>
	/// Description of Commander.
	/// </summary>
	public class Commander
	{
		public event EventHandler<IssueSendEventArgs> IssueCommand;
		protected virtual void OnIssueCommand(IssueSendEventArgs e)
		{
			EventHandler<IssueSendEventArgs> handler = IssueCommand;
			if (handler != null)
            	handler(this, e);
		}
		
		private const String ResetWatchdog = "AT*COMWDG\r";
		
		private const int FDefault = 290717696;
		private const int FTakeOff = 290718208;
		private const int FEmergency = 290717952;
		
		private List<string> anims = new List<string>{"phiM30Deg","phi30Deg",
    "thetaM30Deg","theta30Deg","theta20degYaw200deg","theta20degYawM200deg",
    "turnaround","turnaroundGodown",
    "yawShake","yawDance",
    "phiDance","thetaDance",
    "vzDance","wave",
    "phiThetaMixed","doublePhiThetaMixed",
      "flipAhead","flipBehind","flipLeft","flipRight"};
		
		private ARDrone drone;
		public DroneStatus DroneStatus { get { return drone.Status; } set { drone.Status = value; } }
		private int seqNo = 0;
		private System.Timers.Timer aliveTimer;
		
		private float _roll; 
		private float _pitch; 
		private float _gaz; 
		private float _yaw;
		
		public Commander(ARDrone Drone)
		{
			this.drone = Drone;
			this.drone.StatusChanged += new EventHandler<DroneStatusChangedEventArgs>(DroneStatusChanged);
			_roll = _pitch = _gaz = _yaw = 0;
		}

		private void DroneStatusChanged(object sender, DroneStatusChangedEventArgs e)
		{
			if((int)e.FormerStatus < (int)DroneStatus.Connected)
			{
				if ((int)drone.Status >= (int)DroneStatus.Connected)
					Initialize();
			}
			if((int)e.FormerStatus < (int)DroneStatus.Ready && (int)this.drone.Status == (int)DroneStatus.Ready)
				SendMessage("AT*CTRL=1,0,0\r");
		}
		
		private void Initialize()
		{
			SendMessage(CreateConfigCmd("general:navdata_demo","TRUE")); //request sending nav data
			drone.debug += "req nav data\r";

			aliveTimer = new System.Timers.Timer(200);
			aliveTimer.AutoReset = true;
			aliveTimer.Elapsed += delegate { SendMessage(ResetWatchdog); };
			aliveTimer.Enabled = true;
		}
		
		private void SendMessage(string cmd)
		{
			byte[] buffer = System.Text.Encoding.ASCII.GetBytes(cmd);
			OnIssueCommand(new IssueSendEventArgs(new IPEndPoint(this.drone.IPAddress, 0), buffer));
		}
		
		public void EmergencySend() 
		{ 
			seqNo = 0;	
			SendMessage(CreateRefCmd(FEmergency));
			drone.debug += "emergency\r";
		}
		
		public void FlatTrimSend()
		{
			if ((int)drone.Status == (int)DroneStatus.Ready)
			{
				SendMessage(CreateFlatTrimCmd());
				drone.debug += "flat trim\r";
			}
		}
		
		public void TakeOffSend()
		{
			SendMessage(CreateRefCmd(FTakeOff));
		}
		
		public void LandSend()
		{
			SendMessage(CreateRefCmd(FDefault));
			seqNo = 0;
		}
		
		public void MoveSend(float roll, float pitch, float gaz, float yaw)
		{
			if ((int)drone.Status == (int)DroneStatus.Flying)
			{
				if (roll != _roll || pitch != _pitch || gaz != _gaz || yaw != _yaw)
				{
					seqNo++;
					SendMessage(CreateMove(roll, pitch, gaz, yaw));
					_roll = roll;
					_pitch = pitch;
					_gaz = gaz;
					_yaw = yaw;
				}
			}
		}
		
		public void AnimSend(string animation, int Length)
		{
			int id = anims.IndexOf(animation);
			if (id >-1)
			{
				SendMessage(CreateAnimCmd(id, Length));
       		}
    	}
		
		private String CreateAnimCmd(int cmd, int length)
		{
			seqNo++;
			return String.Format("AT*ANIM={0},{1},{2}\r", seqNo, cmd, length);
		}
		
		private String CreateRefCmd(int cmd)
		{
			seqNo++;
			return String.Format("AT*REF={0},{1}\r", seqNo, cmd);
		}
		
		private String CreateConfigCmd(string Key, string Value)
		{
			seqNo++;
			return String.Format("AT*CONFIG={0},\"{1}\",\"{2}\"\r", seqNo, Key, Value);
			/* using ids requires enqueued communication, not yet implemented
			seqNo+=2;
			return String.Format("AT*CONFIG_IDS={0},\"76767676\",\"76767676\",\"76767676\"\rAT*CONFIG={1},\"{2}\",\"{3}\"\r", seqNo-1, seqNo, Key, Value); 
			 */
		}
		public bool ConfigCmdSend(string Key, string Value)
		{
			SendMessage(CreateConfigCmd(Key, Value));
			return true;
		}
		
		private String CreateFlatTrimCmd()
		{
			seqNo++;
			return String.Format("AT*FTRIM={0}\r", seqNo);
		}
		
		private String CreateMove(float roll, float pitch, float gaz, float yaw)
        {
			float hover = (roll == 0 && pitch == 0 && gaz == 0 && yaw == 0) ? 0 : 1;
            return String.Format("AT*PCMD={0},{1},{2},{3},{4},{5}\r", seqNo, hover, NormalizeValue(roll), NormalizeValue(pitch), NormalizeValue(gaz), NormalizeValue(yaw));
        }
		
		private int NormalizeValue(float value)
        {
            int resultingValue = 0;
            unsafe
            {
                value = (Math.Abs(value) > 1) ? 1 : value;
                resultingValue = *(int*)(&value);
            }
            return resultingValue;
        }		
	}
}
