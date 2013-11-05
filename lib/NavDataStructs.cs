using System;
using System.Runtime.InteropServices;
using VVVV.Nodes.ARDrone;
using SlimDX;

namespace VVVV.Nodes.ARDrone
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NavDataHeader
    {
        public uint Header;
        public uint Status;
        public uint SequenceNumber;
        public uint Vision;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NavDataDemo
    {
        public uint ControlStatus;

        public uint BatteryLevel;

        public Vector3 Attitude;

        public int Altitude;

        public Vector3 Velocity;
        
        public uint NumFrames;
        
        // Camera parameters compute by detection, deprecated
        private Matrix3x3 CamRot;        
        private Vector3 CamTrans;
  		private uint detection_tag_index;
  		private uint detection_camera_type; 

  		// Camera parameters compute by drone
  		public Matrix3x3 DroneCamRot;
  		public Vector3 DroneCamTrans;
    }
    
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Matrix3x3
    {
    	public Vector3 row1;
    	public Vector3 row2;
    	public Vector3 row3;
    }
}