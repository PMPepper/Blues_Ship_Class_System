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
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	public class Manager : MySessionComponentBase
	{
		public static long GetOwner(MyCubeGrid grid)
        {

            var gridOwnerList = grid.BigOwners;
            var ownerCnt = gridOwnerList.Count;
            var gridOwner = 0L;

            if (ownerCnt > 0 && gridOwnerList[0] != 0)
                return gridOwnerList[0];
            else if (ownerCnt > 1)
                return gridOwnerList[1];

            return gridOwner;
        }
		public static List<IMyCubeGrid> GridList = new List<IMyCubeGrid>();
		public static bool IsClient => !(IsServer && IsDedicated);
		public static bool IsDedicated => MyAPIGateway.Utilities.IsDedicated;
		public static bool IsServer => MyAPIGateway.Multiplayer.IsServer;
		public static bool IsActive => MyAPIGateway.Multiplayer.MultiplayerActive;
		public BlueSync<ShipMatrixConfig> MySettingsSynced;
		public static ShipMatrixConfig MySettings;
		private int ticks=0;
		public override void Init(MyObjectBuilder_SessionComponent SessionComponent)
		{
			MySettings = ShipMatrixConfig.Load();
			MySettingsSynced= new BlueSync<ShipMatrixConfig>();
			if(IsServer)
			{
				MySettings = ShipMatrixConfig.Load();
				ShipMatrixConfig.Save(MySettings);
				MyLog.Default.WriteLine("Blues_Ship_Matrix: Server Loaded and Saved Config");
				MySettingsSynced.ValidateAndSet(MySettings);
				//Get All Grids
				var entityHashSet = new HashSet<IMyEntity>();
				MyAPIGateway.Entities.GetEntities(entityHashSet, entity => entity is IMyCubeGrid);
				foreach (var Entity in entityHashSet)
				{
					if (Entity is IMyCubeGrid)
					{
						GridList.Add(Entity as IMyCubeGrid);
					}
				}
				
				MyLog.Default.WriteLine("Blues_Ship_Matrix: Grabbed All Grids, Bc I'm Clingy Like That");
				//Damage Handler
				
				MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(89,Modify.GridClassDamageHandler);
				//SetUpEntityStuff
				MyAPIGateway.Entities.OnEntityAdd+=EntityCreatedHandler;
				MyAPIGateway.Entities.OnEntityRemove+=EntityRemovalHandler;
				MyLog.Default.WriteLine("Blues_Ship_Matrix: Added Handlers For Grids");
			}
		}
		
		public override void UpdateAfterSimulation()
		{
			base.UpdateBeforeSimulation();
			if(MySettingsSynced.Value!=null && !IsDedicated){MySettings=MySettingsSynced.Value;}
			ticks+=1;
			MyAPIGateway.Parallel.Start(delegate{
				if(IsServer)
				{
					//Only Send Config If you are the server
					if(ticks > 240) {ticks=0;MySettingsSynced.ValidateAndSet(MySettings);}

					foreach(IMyCubeGrid CoreGrid in GridList.ToList())
					{
						if((CoreGrid as MyCubeGrid).BlocksCount<4){continue;}
						IEnumerable<IMyBeacon> Beacons = CoreGrid.GetFatBlocks<IMyBeacon>();
						List<IMyBeacon> BeaconsList=Beacons.ToList();
						long GridOwner=GetOwner(CoreGrid as MyCubeGrid);
						var OwnerFaction=MyAPIGateway.Session.Factions.TryGetPlayerFaction(GridOwner);
						if(OwnerFaction!=null)
						{
							if(OwnerFaction.Tag!=null || OwnerFaction.Tag!="")
							{
								if(MySettings.IgnoredFactions.Contains(OwnerFaction.Tag)){continue;}
							}
						}
						if(BeaconsList.Count<1){
							IEnumerable<IMyFunctionalBlock> BlocksOnGrid = CoreGrid.GetFatBlocks<IMyFunctionalBlock>();
							foreach (IMyFunctionalBlock Block in BlocksOnGrid.ToList())
							{
								if(Block==null){continue;}
								if(Block.Enabled){Block.Enabled = false;}	
							}
							continue;
						}
						else{
							//MySync<string, SyncDirection.BothWays> SyncedWarning;
							string Warning="";
							bool Penalize=false;
							IMyBeacon CoreBeacon=BeaconsList.First();
							if(CoreBeacon==null){continue;}
							var MyShipCore = CoreBeacon?.GameLogic?.GetAs<ShipCore>();
							MyGridLimit CoreGridClass=MyShipCore.CoreGridClass;
							if(MyShipCore.CoreGridClass==null)
							{
								CoreGridClass = Globals.GetClass(CoreGrid);
								MyShipCore.CoreGridClass=CoreGridClass;
							}
							/*if(CoreGridClass==null||(!CoreGrid.CustomName.ToLower().Contains(CoreGridClass.Name.ToLower()))||CoreGrid.CustomName!=CoreBeacon.CustomData)
							{
								CoreGridClass = Globals.GetClass(CoreGrid));
								SyncWarning.ValidateAndSet(SyncWarning.Value+=ClassLimit.Warning);
								CoreBeacon.CustomData=CoreGridClass.Name;
							}*/
							Warning+="\n<<< Class Limits Info: >>>\nClass Name:"+CoreGridClass.Name;
							string PerStatWarning="";
							string PerBlockWarning="";
							if(ticks % 3 == 0)
							{
								try{
								PerBlockWarning+=Globals.CheckAndPenalizeBlockLimits(CoreBeacon,CoreGridClass);
								}catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @BlockLimits - {e.Message}");}
							}
							if(ticks % 6 == 0)
							{
								try{
								DoubleReturn GridStatLimit = Globals.CheckGridStatLimits(CoreBeacon,CoreGridClass);
								if(GridStatLimit.Penalty){Penalize=GridStatLimit.Penalty;}
								PerStatWarning+=GridStatLimit.Warning;
								}catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @GridStats - {e.Message}");}
							}
							if(ticks % 12 == 0)
							{
								//MySettingsSynced.ValidateAndSet(MySettings);
								try{
								DoubleReturn ClassLimit = Globals.CheckClassLimits(CoreBeacon,CoreGridClass);
								if(ClassLimit.Penalty){Penalize=ClassLimit.Penalty;}
								Warning+=ClassLimit.Warning+PerStatWarning+PerBlockWarning+MyShipCore.Info;
								}catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @ClassLimits - {e.Message}");}
								//MyShipCore.CoreBeacon.CustomData=Warning;
								//MyShipCore.Warning=Warning;
								foreach(IMyBeacon Beacon in BeaconsList.ToList())
								{
									if(Beacon==null){continue;}
									if (Beacon?.GameLogic?.GetAs<ShipCore>() != null){
										var AuxShipCore = Beacon?.GameLogic?.GetAs<ShipCore>();
										if(CoreBeacon!=Beacon){AuxShipCore.Warning="Warning: Not Primary Beacon\n"+Warning;}else{AuxShipCore.Warning=Warning;}
										if(GridOwner!=Beacon.OwnerId){AuxShipCore.Warning="Warning: Beacon Owner Does NOT Own Grid\n"+Warning;}
									}
									
								}
								//if(MyShipCore!=null){MyShipCore.CustomData=Warning;}
								//CoreBeacon.RefreshCustomInfo();
								//SyncedWarning.ValidateAndSet(Warning);

							}
							//CoreBeacon.CustomData=Warning;
							/*Dictionary<long, string> messages = new Dictionary<long, string>();
							if(SyncMessages.Value.MSG!=null)
							{
									messages = SyncMessages.Value.MSG;
							}
							messages[CoreBeacon.EntityId] = Warning;
							SyncMessages.ValidateAndSet(new MessageStorage{MSG=messages});
							*/
							//Penalize offenders
							try{
							if(Penalize){Globals.Penalize(CoreBeacon);}
							}catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @Penalize - {e.Message}");}
							/*if(IsDedicated)
							{
								try{
									if(ticks % 1000 == 0)
									{
										//Globals.Messages[CoreBeacon.EntityId]=CoreBeacon.CustomData;
										CoreBeacon.RefreshCustomInfo();
									}
								catch (Exception e){MyLog.Default.WriteLine($"BlueShipMatrix: Error @Client - {e.Message}");}
							}*/
						}
					}
				}
				/*if(IsDedicated)
				{
					foreach(IMyCubeGrid CoreGrid in GridList.ToList())
					{
						IEnumerable<IMyBeacon> Beacons = CoreGrid.GetFatBlocks<IMyBeacon>();
						List<IMyBeacon> BeaconsList=Beacons.ToList();
						IMyBeacon CoreBeacon=BeaconsList.First();
						CoreBeacon.RefreshCustomInfo();
					}
				}*/
			});

		}			

	    protected override void UnloadData()
		{
		  MyAPIGateway.Entities.OnEntityAdd-=EntityCreatedHandler;
		  MyAPIGateway.Entities.OnEntityRemove-=EntityRemovalHandler;
		  //MyAPIGateway.Utilities.MessageEntered-= Client_Chat_Manager;
		  //MyAPIGateway.Multiplayer.UnregisterMessageHandler(MySettingsSynced.modID, MySettingsSynced.MessageHandler);
		  
		}
		public void EntityCreatedHandler(IMyEntity Entity)
		{
			if (Entity is IMyCubeGrid && !GridList.Contains(Entity as IMyCubeGrid))
			{
				GridList.Add(Entity as IMyCubeGrid);
			}
			//MyAPIGateway.Utilities.ShowMessage("Blues_Ship_Matrix", Convert.ToString(GridList.Count));
		}
		public void EntityRemovalHandler(IMyEntity Entity)
		{
			if (Entity is IMyCubeGrid)
			{
				GridList.Remove(Entity as IMyCubeGrid);
			//MyAPIGateway.Utilities.ShowMessage("Blues_Ship_Matrix", Convert.ToString(GridList.Count));
			}
		}

	}
	
}