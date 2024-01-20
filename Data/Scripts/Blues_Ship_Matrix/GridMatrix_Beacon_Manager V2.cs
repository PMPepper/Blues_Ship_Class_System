using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
//Sandboxs
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using Sandbox.Definitions;
//Vrage
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRage.Network;
using VRage.Sync;
using Blues_Ship_Matrix;

namespace Blues_Ship_Matrix
{
    [MyEntityComponentDescriptor(typeof(Sandbox.Common.ObjectBuilders.MyObjectBuilder_Beacon), false, new string[] { "SmallBlockBeacon", "LargeBlockBeacon", "SmallBlockBeaconReskin", "LargeBlockBeaconReskin" })]
    public class ShipCore : MyGameLogicComponent
    {
        public static ShipCore Instance;
		//public MyObjectBuilder_EntityBase builder;
		public IMyBeacon CoreBeacon;
        
        public IMyCubeBlock CoreBlock;
        public IMyCubeGrid CoreGrid;
        public MyGridLimit CoreGridClass;
        //public BlueSync<MyGridLimit> SyncGridClass;
        //public BlueSync<string> GUIText;
        public string Info="\n  Server Failed to Update GUI";
        public string Warning="Server Aquired Block:";
		public static bool IsClient => !(IsServer && IsDedicated);
		public static bool IsDedicated => MyAPIGateway.Utilities.IsDedicated;
		public static bool IsServer => MyAPIGateway.Multiplayer.IsServer;
		public static bool IsActive => MyAPIGateway.Multiplayer.MultiplayerActive;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            
            Instance=this;
            CoreBeacon = Entity as Sandbox.ModAPI.IMyBeacon;
            CoreBlock = Entity as IMyCubeBlock;
			//builder = objectBuilder;
			CoreBeacon.CustomData="Initilized";
			CoreGrid = CoreBlock.CubeGrid;
            //GUIText= new BlueSync<string>(6060);
            MyLog.Default.WriteLine("BlueSync: Try Entity Id Update");
            //GUIText.EntityId=CoreBeacon.EntityId;
            //GUIText.ValidateAndSet("Welcome To Blues Grid Matrix");
            Info = "Unable to retrive class info from server";
            //SyncGridClass= new BlueSync<MyGridLimit>(8080);
            //SyncGridClass.EntityId=CoreBeacon.EntityId;
            //SyncGridClass.ValidateAndSet(Manager.MySettings.LargeShip_Basic);
            CoreGridClass=Manager.MySettings.Station_Basic;
            Globals.BeaconList.Add(CoreBeacon);
            Action<IMyTerminalBlock, StringBuilder> GUI_ACTON = (termBlock, builder) =>
                {
                    if(IsServer)
                    {
                        CoreBeacon.CustomData=Warning;
                    }
                    if(!IsDedicated){
                        builder.Clear();
                        string Message=LoadCustomData();
                        builder.Append(Message);//CoreBeacon.CustomData
                    }
                };
            CoreBeacon.AppendingCustomInfo += GUI_ACTON;
			
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
            //MyLog.Default.WriteLine("BlueSync: Updated ID");
            if(IsServer){CoreBeacon.CustomData="Aquired By Server";}
        }

        public bool Client_GUI_Initialized=false;
        public override void UpdateAfterSimulation10(){
                if(IsServer)
                {
                    MyGridLimit NewGridClass=Globals.GetClass(CoreGrid);
                    
                    if(NewGridClass!=null)
                    {
                        DoubleReturn ClassLimit = Globals.CheckClassLimits(CoreBeacon,NewGridClass);
                        
                    if( NewGridClass!=CoreGridClass)
                        if(!ClassLimit.Penalty || NewGridClass==Manager.MySettings.Station_Basic|| NewGridClass==Manager.MySettings.LargeShip_Basic|| NewGridClass==Manager.MySettings.SmallShip_Basic)
                        {
                            if(CoreGrid.CustomName.Contains(CoreGridClass.Name)){CoreGrid.CustomName=CoreGrid.CustomName.Replace(CoreGridClass.Name,NewGridClass.Name);}
                            if(CoreBeacon.HudText.Contains(CoreGridClass.Name)){CoreBeacon.HudText=CoreBeacon.HudText.Replace(CoreGridClass.Name,NewGridClass.Name);}
                            CoreGridClass=NewGridClass;
                        }
                        else
                        {
                            if(CoreGrid.CustomName.Contains(NewGridClass.Name)){CoreGrid.CustomName=CoreGrid.CustomName.Replace(NewGridClass.Name,CoreGridClass.Name);}
                            if(CoreBeacon.HudText.Contains(NewGridClass.Name)){CoreBeacon.HudText=CoreBeacon.HudText.Replace(NewGridClass.Name,CoreGridClass.Name);}
                        }
                       //SyncGridClass.ValidateAndSet(CoreGridClass);
                    }
                }

                if(!IsDedicated)
                {
                    CustomControls.AddControls(ModContext);
                    /*foreach(MyGridLimit Class in Manager.MySettings.GridLimits)
                    {
                        if(Class!=CoreGridClass && CoreGrid.CustomData.Contains(Class.Name)){CustomName.Replace(Class.Name,"")}
                    }*/
                }
                //Both Server and Client need to Rephresh this value
                /*
                if (CoreGridClass.ForceBroadCast)
                {
                    CoreBeacon.Radius=CoreGridClass.ForceBroadCastRange;
                    if(!CoreBeacon.HudText.Contains(CoreGridClass.Name)){CoreBeacon.HudText+=": "+CoreGridClass.Name;}
                    if(!CoreBeacon.Enabled){CoreBeacon.Enabled=true;}
                }*/
                //CoreBeacon.RefreshCustomInfo();
                try
                {
                    Info="\nActive Modifiers:"+Modify.Thrusters(CoreGrid,CoreGridClass)+Modify.Gyros(CoreGrid,CoreGridClass)+Modify.Assemblers(CoreGrid,CoreGridClass)+Modify.Refineries(CoreGrid,CoreGridClass)+Modify.Reactors(CoreGrid,CoreGridClass)+Modify.Drills(CoreGrid,CoreGridClass);
                    //CoreBeacon.CustomData+="\n<Synced>";
                    CoreBeacon.RefreshCustomInfo();
                   
                }
                catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @Modify - {e.Message}");}
                
                

        }
        public string LoadCustomData()
        {
            return(CoreBeacon.CustomData);
        }
		public override void Close()
        {
            //MyAPIGateway.Multiplayer.UnregisterMessageHandler(SyncGridClass.modID, SyncGridClass.MessageHandler);
           // MyAPIGateway.Multiplayer.UnregisterMessageHandler(GUIText.modID, GUIText.MessageHandler);
            if(Globals.BeaconList.Contains(CoreBeacon))
            {Globals.BeaconList.Remove(CoreBeacon);}
            if (Entity == null)
            {
                return;
            }
        }
		
	
    }
}
