#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VMath;
using SlimDX;

using VVVV.Nodes.ARDrone;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{	
	#region PluginInfo
	[PluginInfo(Name = "ARDrone", 
				Category = "Device", 
				Version = "Parrot", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneDeviceNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("Drone IP", StringType = StringType.IP, DefaultString ="192.168.1.1")]
		IDiffSpread<string> FDroneIP;

		
		[Output("ARDrone")]
		ISpread<ARDrone.ARDrone> FOutput;

		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		
		private ARDroneHost droneHost;
		private bool disposed = false;
		#pragma warning restore 649, 169
		#endregion fields & pins

		public void OnImportsSatisfied()
		{
			droneHost = new ARDroneHost();
		}
		
		#region dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!this.disposed)
            {
                if(disposing)
                {
                	droneHost.Dispose();
                }
                disposed = true;
            }
        }

        ~ARDroneDeviceNode()
        {
            Dispose(false);
        }
        #endregion
		
		//called when data for any output pin is requested
		public void Evaluate(int spreadMax)
		{
			if (droneHost.Count>FDroneIP.SliceCount)
				for (int r=droneHost.Count-1; r>=FDroneIP.SliceCount; r--)
					droneHost.RemoveAt(r);
			
			if (droneHost.Count<FDroneIP.SliceCount)
				for (int a=droneHost.Count; a<FDroneIP.SliceCount; a++)
					droneHost.InsertAt(-1, new ARDrone.ARDrone(FDroneIP[a]));
			
			spreadMax = FDroneIP.SliceCount;
			FStatus.SliceCount = spreadMax;
			FOutput.SliceCount = spreadMax;
			for (int i=0; i<spreadMax; i++)
			{
				if (droneHost[i].IP != FDroneIP[i])
				{
					droneHost.RemoveAt(i);
					droneHost.InsertAt(i, new ARDrone.ARDrone(FDroneIP[i]));
				}
				if (droneHost[i].debug != "")
				{
					FLogger.Log(LogType.Debug, FDroneIP[i]+": "+droneHost[i].debug);
					
					droneHost[i].debug = "";
				}
				FOutput[i] = droneHost[i];
				FStatus[i] = droneHost[i].Status.ToString();
			}
			if (droneHost.debug != string.Empty)
			{
				FLogger.Log(LogType.Debug, droneHost.debug);
				droneHost.debug = "";
				
			}
			
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Configuration", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneConfigurationNode : IPluginEvaluate
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("ARDrone")]
		ISpread<ARDrone.ARDrone> FDrones;
		
		[Input("Min Y", DefaultValue = 0.005)]
		ISpread<double> FMinAlt;
		
		[Input("Max Y", DefaultValue = 3)]
		ISpread<double> FMaxAlt;
		
		[Input("Max Velocity Y", DefaultValue = 0.7, MinValue = 0.2, MaxValue = 2.0)]
		ISpread<double> FMaxVelAlt;
		
		[Input("Max Angle", DefaultValue = 0.25, MinValue = 0, MaxValue = 0.52)]
		ISpread<double> FMaxAngle;
		
		[Input("Max Yaw", DefaultValue = 3.00, MinValue = 0.7, MaxValue = 6.11)]
		ISpread<double> FMaxYaw;
		
		[Input("Config Key", Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FConfigKey;
		
		[Input("Config Value", Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FConfigValue;
		
		[Input("Do Send Config", IsBang = true, Visibility = PinVisibility.OnlyInspector)]
		ISpread<bool> FSendConfig;
		
		[Input("Telnet", Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FTelnetText;
		
		[Input("Do Send Telnet", IsBang = true, Visibility = PinVisibility.OnlyInspector)]
		ISpread<bool> FSendTelnet;

		
		[Output("Config Keys")]
		ISpread<ISpread<string>> FKeys;
		
		[Output("Config Values")]
		ISpread<ISpread<string>> FValues;
		
		[Output("Raw Config", Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FRawConfig;
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		
		private System.Globalization.NumberFormatInfo nfi = new System.Globalization.NumberFormatInfo() { NumberGroupSeparator = "." };
		private System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.CreateSpecificCulture("en-US");
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FKeys.SliceCount = FDrones.SliceCount;
			FValues.SliceCount = FDrones.SliceCount;
			FRawConfig.SliceCount = FDrones.SliceCount;
			FStatus.SliceCount = FDrones.SliceCount;
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				if (FDrones[i] == null)
				{
					FKeys[i].SliceCount=0;
					FValues[i].SliceCount=0;
					FRawConfig[i] = "";
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].Status > (int)DroneStatus.NotConnected && FDrones[i].Configurator.HasConfig)
					{
						int minAlt = (int)(FMinAlt[i]*1000);
						if (minAlt != int.Parse(FDrones[i].Configurator["control:altitude_min"]))
							FDrones[i].Configurator.SetConfig("control:altitude_min", minAlt.ToString());
						
						int maxAlt = (int)(FMaxAlt[i]*1000);
						if (maxAlt != int.Parse(FDrones[i].Configurator["control:altitude_max"]))
							FDrones[i].Configurator.SetConfig("control:altitude_max", maxAlt.ToString());
						
						double maxVelAlt = FMaxVelAlt[i]*1000;
						if (maxVelAlt != double.Parse(FDrones[i].Configurator["control:control_vz_max"],nfi))
							FDrones[i].Configurator.SetConfig("control:control_vz_max", maxVelAlt.ToString("e",ci));
						
						if (FMaxAngle[i] != double.Parse(FDrones[i].Configurator["control:euler_angle_max"],nfi))
							FDrones[i].Configurator.SetConfig("control:euler_angle_max", FMaxAngle[i].ToString("e",ci));
						
						if (FMaxYaw[i] != double.Parse(FDrones[i].Configurator["control:control_yaw"],nfi))
						{
							FDrones[i].Configurator.SetConfig("control:control_yaw", FMaxYaw[i].ToString("e",ci));
							FLogger.Log(LogType.Debug,"yawing");
						}
						
						//send config command
						if (FSendConfig[i])
							FDrones[i].Configurator.SetConfig(FConfigKey[i], FConfigValue[i]);
						
						//telnet direct command
						if (FSendTelnet[i])
							FDrones[i].Configurator.IssueTelnet(FTelnetText[i]);
						
					}
					FKeys[i].AssignFrom(FDrones[i].Configurator.Keys);
					FValues[i].AssignFrom(FDrones[i].Configurator.Values);
					FStatus[i] = FDrones[i].Status.ToString();
					FRawConfig[i] = FDrones[i].Configurator.TelnetText;
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Command", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneCommanderNode : IPluginEvaluate
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("ARDrone")]
		ISpread<ARDrone.ARDrone> FDrones;
		
		[Input("Emergency", IsBang = true)]
		ISpread<bool> FEmergency;
		
		[Input("Flat Trim", IsBang = true)]
		ISpread<bool> FFlatTrim;
		
		[Input("Take Off", IsBang = true)]
		ISpread<bool> FTakeOff;
		
		[Input("Land", IsBang = true)]
		ISpread<bool> FLand;
		
		[Input("Receive Debug", Visibility = PinVisibility.OnlyInspector)]
		IDiffSpread<bool> FDemo;

		
		[Output("Command")]
		ISpread<ARDrone.FlyCommand> FCmd;
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FCmd.SliceCount = FDrones.SliceCount;
			FStatus.SliceCount = FDrones.SliceCount;
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				if (FDrones[i] == null)
				{
					FCmd[i] = null;
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].Status > (int)DroneStatus.Connected)
					{
						if (FFlatTrim[i])
							FDrones[i].Commander.FlatTrimSend();
						
						if (FTakeOff[i])
							FDrones[i].Commander.TakeOffSend();
						
						if (FLand[i])
							FDrones[i].Commander.LandSend();
						
						if (FEmergency[i])
							FDrones[i].Commander.EmergencySend();
						
						if (FDemo.IsChanged)
							FDrones[i].Commander.ConfigCmdSend("general:navdata_demo",FDemo[i]?"FALSE":"TRUE");
					}
					FStatus[i] = FDrones[i].Status.ToString();
					FCmd[i] = FDrones[i].FlyCommands;
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "FlyCommand", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneFlyCommandNode : IPluginEvaluate
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("Command")]
		ISpread<ARDrone.FlyCommand> FDrones;
		
		[Input("Roll", MinValue = -1.0f, MaxValue = 1.0f)]
		ISpread<float> FRoll;
		
		[Input("Pitch", MinValue = -1.0f, MaxValue = 1.0f)]
		ISpread<float> FPitch;
		
		[Input("Gaz", MinValue = -1.0f, MaxValue = 1.0f)]
		ISpread<float> FGaz;
		
		[Input("Yaw", MinValue = -1.0f, MaxValue = 1.0f)]
		ISpread<float> FYaw;
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FStatus.SliceCount = FDrones.SliceCount;
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				if (FDrones[i] == null)
				{
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].DroneStatus >= (int)DroneStatus.Flying)
					{
						FDrones[i].Commander.MoveSend(FRoll[i], -FPitch[i], FGaz[i], FYaw[i]);
						
						
					}
					FStatus[i] = FDrones[i].DroneStatus.ToString();
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "FlipCommand", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneFlipCommandNode : IPluginEvaluate
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("Command")]
		ISpread<ARDrone.FlyCommand> FDrones;
		
		[Input("Left", IsBang = true)]
		ISpread<bool> FLeft;
		
		[Input("Right", IsBang = true)]
		ISpread<bool> FRight;
		
		[Input("Ahead", IsBang = true)]
		ISpread<bool> FAhead;
		
		[Input("Behind",IsBang = true)]
		ISpread<bool> FBehind;
		
		[Input("Animation Length", DefaultValue = 5)]
		ISpread<int> FAnimLength;
		
		[Input("Animation", Visibility = PinVisibility.OnlyInspector)]
		ISpread<string> FAnimation;
		
		[Input("Do Animation", Visibility = PinVisibility.OnlyInspector)]
		ISpread<bool> FDoAnim;
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FStatus.SliceCount = FDrones.SliceCount;
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				if (FDrones[i] == null)
				{
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].DroneStatus >= (int)DroneStatus.Flying)
					{
						if (FAhead[i])
							FDrones[i].Commander.AnimSend("flipAhead", FAnimLength[i]);
						if (FBehind[i])
							FDrones[i].Commander.AnimSend("flipBehind", FAnimLength[i]);
						if (FLeft[i])
							FDrones[i].Commander.AnimSend("flipLeft", FAnimLength[i]);
						if (FRight[i])
							FDrones[i].Commander.AnimSend("flipRight", FAnimLength[i]);
						
						if (FDoAnim[i])
							FDrones[i].Commander.AnimSend(FAnimation[i], FAnimLength[i]);
					}
					FStatus[i] = FDrones[i].DroneStatus.ToString();
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Position", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDronePositionNode : IPluginEvaluate, IPartImportsSatisfiedNotification
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("ARDrone")]
		ISpread<ARDrone.FlyCommand> FDrones;
		
		[Input("Initial Position")]
		ISpread<Vector3> FInitPos;
		
		[Input("Initial Yaw")]
		ISpread<float> FYaw;
		
		[Input("Set Initial", IsBang = true)]
		ISpread<bool> FSetInit;
		

		[Output("XYZ")]
		ISpread<Vector3> FXYZ;
		
		[Output("Inertial", Visibility = PinVisibility.OnlyInspector)]
		ISpread<Vector3> FInertial;
		
		[Output("Rotation")]
		ISpread<Matrix> FRot;
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		
		private Matrix toVVVVRot;
		private Matrix toVVVVPos;
		
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		public void OnImportsSatisfied()
		{
			toVVVVRot = new Matrix();
			Matrix.RotationX((float)(0.5*Math.PI),out toVVVVRot);
			Matrix.Scaling(0.001f, 0.001f,-0.001f, out toVVVVPos);
			toVVVVPos = toVVVVRot*toVVVVPos;
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			FXYZ.SliceCount = FDrones.SliceCount;
			FInertial.SliceCount = FDrones.SliceCount;
			FRot.SliceCount = FDrones.SliceCount;
			FStatus.SliceCount = FDrones.SliceCount;			
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				if (FDrones[i] == null)
				{
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].DroneStatus > (int)DroneStatus.BatteryLow)
					{
						if (FSetInit[i])
						{
							FDrones[i].Positioner.SetPositionBySensor( Vector3.TransformCoordinate(FInitPos[i], Matrix.Invert(toVVVVPos)),new Vector3(0,(float)(FYaw[i]*Math.PI*2),0));
							FDrones[i].Positioner.SetInertialPosition(Vector3.TransformCoordinate(FInitPos[i], Matrix.Invert(toVVVVPos)));
						}
					}
					
					FRot[i] = (FDrones[i].Positioner.Rotation*toVVVVRot);
					FXYZ[i] = Vector3.TransformCoordinate(FDrones[i].Positioner.PositionBySensor, toVVVVPos);
					FInertial[i] = Vector3.TransformCoordinate(FDrones[i].Positioner.PositionBySensor, toVVVVPos);
					FStatus[i] = FDrones[i].DroneStatus.ToString();
				}
			}
		}
	}
	
	#region PluginInfo
	[PluginInfo(Name = "Target", 
				Category = "ARDrone", 
				Help = "Basic template with one value in/out", 
				Tags = "", 
				Author = "woei",
				AutoEvaluate = true)]
	#endregion PluginInfo
	public class ARDroneTargetNode : IPluginEvaluate, IPartImportsSatisfiedNotification
	{
		#region fields & pins
		#pragma warning disable 649, 169
		[Input("ARDrone")]
		ISpread<ARDrone.FlyCommand> FDrones;
		
		[Input("Use Inertial Position", Visibility = PinVisibility.OnlyInspector)]
		ISpread<bool> FUseInertial;
		
		[Input("Target Position")]
		ISpread<Vector3> FPos;
		
		[Input("Target Yaw")]
		ISpread<float> FYaw;
		
		[Input("Target Speed")]
		ISpread<float> FSpeed;
		
		[Input("Follow Target")]
		ISpread<bool> FTarget;
		
		
		[Output("Status")]
		ISpread<string> FStatus;
		
		[Import()]
		ILogger FLogger;
		
		private Matrix toVVVVRot;
		private Matrix toVVVVPos;
		
		#pragma warning restore 649, 169
		#endregion fields & pins
		
		public void OnImportsSatisfied()
		{
			toVVVVRot = new Matrix();
			Matrix.RotationX((float)(0.5*Math.PI),out toVVVVRot);
			Matrix.Scaling(0.001f, 0.001f,-0.001f, out toVVVVPos);
			toVVVVPos = toVVVVRot*toVVVVPos;
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
//			FXYZ.SliceCount = FDrones.SliceCount;
//			FRot.SliceCount = FDrones.SliceCount;
			FStatus.SliceCount = FDrones.SliceCount;			
			for (int i=0; i<FDrones.SliceCount; i++)
			{
				//FTest[i] = Vector3.TransformCoordinate(new Vector3(0,0.5f,0),Matrix.Invert(toVVVVPos));
				if (FDrones[i] == null)
				{
					FStatus[i] = DroneStatus.Invalid.ToString();
				}
				else
				{
					if ((int)FDrones[i].DroneStatus > (int)DroneStatus.BatteryLow)
					{
						
						if (FTarget[i])
						{
							FDrones[i].Targeter.SetTarget(Vector3.TransformCoordinate(FPos[i],Matrix.Invert(toVVVVPos)),new Vector3(0,(float)(FYaw[i]*Math.PI*2),0),FSpeed[i]);
							FDrones[i].Targeter.UseInertialPosition = FUseInertial[i];
						}
						else
							FDrones[i].Targeter.ReleaseTarget();
					}
					
//					FRot[i] = (FDrones[i].Positioner.Rotation*toVVVVRot);
//					Vector3 outPos = FDrones[i].Positioner.PositionBySensor;
//					if (FUseInertial[i])
//						outPos = FDrones[i].Positioner.InertialPosition;
//					FXYZ[i] = Vector3.TransformCoordinate(outPos, toVVVVPos);
					FStatus[i] = FDrones[i].DroneStatus.ToString();
				}
			}
		}
	}
}
