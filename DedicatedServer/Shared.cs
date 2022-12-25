﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameServer;
using System.Text.RegularExpressions;
#if (!DEDICATED)
using UnityEngine;
using MelonLoader;
#else
using System.Numerics;
#endif

namespace SkyCoop
{
    public class Shared
    {
        public static int SecondsBeforeUnload = 0;
        public static Dictionary<string, DataStr.AnimalKilled> AnimalsKilled = new Dictionary<string, DataStr.AnimalKilled>();
        public static Dictionary<string, int> StunnedRabbits = new Dictionary<string, int>();
        public static int ExperienceForDS = 2;
        public static int StartingRegionDS = 0;

        public static void OnUpdate()
        {

        }

        public static void EverySecond()
        {
            if (MyMod.iAmHost)
            {
                SecondsBeforeUnload = SecondsBeforeUnload + 1;
                if (SecondsBeforeUnload > MPSaveManager.SaveRecentTimer)
                {
                    SecondsBeforeUnload = 0;
                    MPSaveManager.SaveRecentStuff();
                }
                MPSaveManager.UpdateKnockDoorRequests();
                ServerSend.KEEPITALIVE(0, true);

                UpdateStunnedRabbits();

                List<DataStr.MultiPlayerClientStatus> PlayersListDat = SleepTrackerAndTimeOutAndAnimalControllers();
#if (!DEDICATED)
                MyMod.UpdatePlayerStatusMenu(PlayersListDat);
#endif
            }
        }

        public static void EveryInGameMinute()
        {
            MyMod.OverridedMinutes++;

            if (MyMod.OverridedMinutes > 59)
            {
                MyMod.OverridedMinutes = 0;
                MyMod.OverridedHourse = MyMod.OverridedHourse + 1;
                MyMod.PlayedHoursInOnline = MyMod.PlayedHoursInOnline + 1;
            }
            if (MyMod.OverridedHourse > 23)
            {
                MyMod.OverridedHourse = 0;
            }

            MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
            if (MyMod.iAmHost)
            {
                MyMod.MinutesFromStartServer++;
                ServerSend.GAMETIME(MyMod.OveridedTime);
                UpdateTicksOnScenes();
                // TODO: WEATHER SYNC
            }
        }




        public static void Log(string TXT)
        {
#if (!DEDICATED)
            MelonLogger.Msg(TXT);
#else
            Logger.Log(TXT);
#endif
        }

        public static bool HasNonASCIIChars(string str)
        {
            return (Encoding.UTF8.GetByteCount(str) != str.Length);
        }

        public static int GetVectorHash(Vector3 v3)
        {
#if (!DEDICATED)
            float NewFloat = v3.x + v3.y + v3.z;
#else
            float NewFloat = v3.X + v3.Y + v3.Z;
#endif
            return NewFloat.GetHashCode();
        }
        public static int GetQuaternionHash(Quaternion Quat)
        {
#if (!DEDICATED)
            float NewFloat = Quat.x + Quat.y + Quat.z + Quat.w;
#else
            float NewFloat = Quat.X + Quat.Y + Quat.Z;
#endif
            return NewFloat.GetHashCode();
        }


        public static bool IsZero(float a, float epsilon = 0.0001f) => (double)Math.Abs(a) <= (double)epsilon;
        public static bool IsZero(double a, double epsilon = 0.0001f) => Math.Abs(a) <= epsilon;
        public static bool Approximately(double a, double b, double epsilon = 0.0001) => IsZero(a - b, epsilon);
        public static bool RollChance(float percent)
        {
            return !IsZero(percent, 0.0001f) && (Approximately(percent, 100f, 0.0001f) || (double)NextFloat(0.0f, 100f) < (double)percent);
        }

        public static void ModifyDynamicGears(string Scene)
        {
            Dictionary<int, DataStr.DroppedGearItemDataPacket> LoadedVisual = MPSaveManager.LoadDropVisual(Scene);
            Dictionary<int, DataStr.SlicedJsonDroppedGear> LoadedData = MPSaveManager.LoadDropData(Scene);

            if (LoadedVisual == null || LoadedData == null)
            {
                return;
            }

            // Here goes kinda crappy code!!!!!
            List<DataStr.DictionaryElementToReNew> Buff = new List<DataStr.DictionaryElementToReNew>();
            List<Vector3> RabbitsBuff = new List<Vector3>();

            foreach (var cur in LoadedData) //Checking if we have any dynamic gears we need to modify!
            {
                int curKey = cur.Key;
                DataStr.SlicedJsonDroppedGear dat = cur.Value;
                if (dat.m_GearName.Contains("gear_snare") == true && (dat.m_Extra.m_Variant == 1 || dat.m_Extra.m_Variant == 4)) // If is placed snare
                {
                    int minutesPlaced = MyMod.MinutesFromStartServer - dat.m_Extra.m_DroppedTime;
                    int NeedToBePlaced = 720;
                    int minutesLeft = NeedToBePlaced - minutesPlaced + 1;
                    float ChanceToCatch = 50;
                    float ChanceToBreak = 15;
                    if (minutesLeft <= 0) // If snare ready to roll random
                    {
#if (!DEDICATED)
                        if (dat.m_Extra.m_Variant == 4 || Utils.RollChance(ChanceToCatch))
                        {
                            dat.m_Extra.m_Variant = (int)SnareState.WithRabbit; // New visual state!
                            dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                            GearItemSaveDataProxy DummyGear = Utils.DeserializeObject<GearItemSaveDataProxy>(dat.m_Json);
                            SnareItem DummySnare = Utils.DeserializeObject<SnareItem>(DummyGear.m_SnareItemSerialized);
                            DummySnare.m_State = (SnareState)dat.m_Extra.m_Variant;
                            DummyGear.m_SnareItemSerialized = DummySnare.Serialize();
                            dat.m_Json = Utils.SerializeObject(DummyGear);

                            RabbitsBuff.Add(DummyGear.m_Position); // Add request on rabbit on snare position
                        } else
                        {
                            if (Utils.RollChance(ChanceToBreak))
                            {
                                dat.m_Extra.m_Variant = (int)SnareState.Broken; // New visual state!
                                dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                                GearItemSaveDataProxy DummyGear = Utils.DeserializeObject<GearItemSaveDataProxy>(dat.m_Json);
                                SnareItem DummySnare = Utils.DeserializeObject<SnareItem>(DummyGear.m_SnareItemSerialized);
                                DummySnare.m_State = (SnareState)dat.m_Extra.m_Variant;
                                DummyGear.m_SnareItemSerialized = DummySnare.Serialize();
                                dat.m_Json = Utils.SerializeObject(DummyGear);

                            } else
                            {
                                // No new visual state, but reseting time player should to wait for.
                                dat.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
                                dat.m_Extra.m_GoalTime = MyMod.MinutesFromStartServer + NeedToBePlaced;
                            }
                        }
#else
                        if (dat.m_Extra.m_Variant == 4 || RollChance(ChanceToCatch))
                        {
                            dat.m_Extra.m_Variant = 3; // With Rabbit
                            dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                            // TODO: GET Possition AND Rotation From Old Object via dat.m_Json


                            //string Pattern = "\r\n^.*[^\"]*.*$\r\n";
                            //Regex regex = new Regex(Pattern);
                            //MatchCollection matches = regex.Matches(dat.m_Json);

                            dat.m_Json = ResourceIndependent.GetSnare(new Vector3(0,0,0),new Quaternion(0,0,0,0), dat.m_Extra.m_Variant);

                            RabbitsBuff.Add(new Vector3(0, 0, 0)); // Add request on rabbit on snare position
                        } else
                        {
                            if (RollChance(ChanceToBreak))
                            {
                                dat.m_Extra.m_Variant = 2; // Broken
                                dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                                // TODO: GET Possition AND Rotation From Old Object via dat.m_Json

                                dat.m_Json = ResourceIndependent.GetSnare(new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), dat.m_Extra.m_Variant);
                            } else
                            {
                                // No new visual state, but reseting time player should to wait for.
                                dat.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
                                dat.m_Extra.m_GoalTime = MyMod.MinutesFromStartServer + NeedToBePlaced;
                            }
                        }
#endif
                        DataStr.DictionaryElementToReNew newGear = new DataStr.DictionaryElementToReNew();
                        newGear.m_Key = curKey;
                        newGear.m_Val = dat;

                        DataStr.DroppedGearItemDataPacket Visual;
                        if (LoadedVisual.TryGetValue(curKey, out Visual))
                        {
                            Visual.m_Extra = dat.m_Extra;
                            newGear.m_Val2 = Visual;
                        }
                        Buff.Add(newGear); //Adding to buffer snare to re-add them.
                    }
                }
            }

            if (Buff.Count > 0) //If buffer contains anything, we need to remove old gears and update them with modified ones
            {
                for (int i = 0; i < Buff.Count; i++)
                {
                    int Key = Buff[i].m_Key;
                    LoadedData.Remove(Key);
                    LoadedData.Add(Key, Buff[i].m_Val);

                    if (Buff[i].m_Val2 != null)
                    {
                        LoadedVisual.Remove(Key);
                        LoadedVisual.Add(Key, Buff[i].m_Val2);
                    }
                }
            }
            if (RabbitsBuff.Count > 0) //If buffer contains anything, we need to spawn rabbits
            {
                for (int i = 0; i < RabbitsBuff.Count; i++)
                {
                    DataStr.SlicedJsonDroppedGear Rabbit = new DataStr.SlicedJsonDroppedGear();
                    Rabbit.m_GearName = "gear_rabbitcarcass";
                    Rabbit.m_Extra.m_DroppedTime = MyMod.MinutesFromStartServer;
                    Rabbit.m_Extra.m_Dropper = "Snare";
                    string RabbitJson = "";
                    int SearchKey = 0;
                    Vector3 v3 = RabbitsBuff[i];
                    Quaternion rot = new Quaternion(0, 0, 0, 0);
#if (!DEDICATED)
                    GameObject reference = MyMod.GetGearItemObject("gear_rabbitcarcass");
                    

                    if (reference != null)
                    {

                        GameObject obj = UnityEngine.Object.Instantiate<GameObject>(reference, v3, rot);
                        GearItem gi = obj.GetComponent<GearItem>();
                        gi.SkipSpawnChanceRollInitialDecayAndAutoEvolve();
                        BodyHarvest bh = obj.GetComponent<BodyHarvest>();
                        if (bh == null)
                        {
                            bh = obj.AddComponent<BodyHarvest>();
                        }
                        obj.name = "gear_rabbitcarcass";
                        gi.m_CurrentHP = bh.GetCondition() / 100f * gi.m_MaxHP;

                        RabbitJson = obj.GetComponent<GearItem>().Serialize();
                        int hashV3 = Shared.GetVectorHash(v3);
                        int hashRot = Shared.GetQuaternionHash(rot);
                        int hashLevelKey = Scene.GetHashCode();
                        SearchKey = hashV3 + hashRot + hashLevelKey;
                        UnityEngine.Object.Destroy(obj);
                    }
#else
                    int hashV3 = Shared.GetVectorHash(v3);
                    int hashRot = Shared.GetQuaternionHash(rot);
                    int hashLevelKey = Scene.GetHashCode();
                    SearchKey = hashV3 + hashRot + hashLevelKey;
                    RabbitJson = ResourceIndependent.GetRabbit(v3, rot);
#endif

                    DataStr.DroppedGearItemDataPacket RabbitVisual = new DataStr.DroppedGearItemDataPacket();
                    RabbitVisual.m_Extra = Rabbit.m_Extra;
                    RabbitVisual.m_GearID = MyMod.GetGearIDByName("gear_rabbitcarcass");
                    RabbitVisual.m_Hash = SearchKey;
                    RabbitVisual.m_LevelGUID = Scene;
                    RabbitVisual.m_Position = RabbitsBuff[i];
                    RabbitVisual.m_Rotation = new Quaternion(0, 0, 0, 0);
                    Rabbit.m_Json = RabbitJson;
                    LoadedData.Add(SearchKey, Rabbit);
                    LoadedVisual.Add(SearchKey, RabbitVisual);
                }
            }
            if (MPSaveManager.RecentData.ContainsKey(Scene))
            {
                MPSaveManager.RecentData.Remove(Scene);
            }
            MPSaveManager.RecentData.Add(Scene, LoadedData);
            if (MPSaveManager.RecentVisual.ContainsKey(Scene))
            {
                MPSaveManager.RecentVisual.Remove(Scene);
            }
            MPSaveManager.RecentVisual.Add(Scene, LoadedVisual);
        }

        public static void UpdateTicksOnScenes()
        {
            for (int i = 0; i < MyMod.MaxPlayers; i++)
            {
                if (i == 0)
                {
                    MyMod.MyTicksOnScene = MyMod.MyTicksOnScene + 1;
                } else
                {
                    if (MyMod.playersData[i] != null)
                    {
                        if (Server.clients[i].IsBusy() == true && MyMod.playersData[i].m_AnimState != "Knock")
                        {
                            if (MyMod.playersData[i].m_Levelid != MyMod.playersData[i].m_PreviousLevelId)
                            {
                                MyMod.playersData[i].m_PreviousLevelId = MyMod.playersData[i].m_Levelid;
                                MyMod.playersData[i].m_TicksOnScene = 0;
                            } else
                            {
                                MyMod.playersData[i].m_TicksOnScene = MyMod.playersData[i].m_TicksOnScene + 1;
                            }
                        } else
                        {
                            MyMod.playersData[i].m_TicksOnScene = 0;
                        }
                    }
                }
            }
        }

        public static void SkipRTTime(int h)
        {
            int totaltime = MyMod.OverridedHourse + h;
            MyMod.PlayedHoursInOnline = MyMod.PlayedHoursInOnline + h;
            int leftovers;
            if (totaltime > 24)
            {
                leftovers = totaltime - 24;
                MyMod.OverridedHourse = 0 + leftovers;
            } else
            {
                MyMod.OverridedHourse = MyMod.OverridedHourse + h;
            }
            int PrevMinutesFromStartServer = MyMod.MinutesFromStartServer;
            MyMod.MinutesFromStartServer = MyMod.MinutesFromStartServer + h * 60;
            Log("Skipping " + h + " hour(s) now should be " + MyMod.OverridedHourse);
            Log("MinutesFromStartServer " + PrevMinutesFromStartServer + " now it " + MyMod.MinutesFromStartServer + " because " + h * 60 + " been added");

#if (!DEDICATED)
           EveryInGameMinute();
#else
           EveryInGameMinute();
#endif



        }

        public static void ProcessSleep(int Sleepers, int SleepersNeed, int Deads, int FinalHours)
        {
            bool EveryOneIsSleeping = false;
            if (Sleepers >= SleepersNeed && Deads < SleepersNeed)
            {
                EveryOneIsSleeping = true;
            }

#if (!DEDICATED)

            string SleepersText = "Players sleep " + Sleepers + "/" + SleepersNeed;
            if (MyMod.WaitForSleepLable != null)
            {
                MyMod.WaitForSleepLable.GetComponent<UILabel>().text = "WAITING FOR OTHER PLAYERS TO SLEEP\n" + SleepersText;
            }

            if (MyMod.WaitForSleepLable != null && MyMod.WaitForSleepLable.activeSelf == true && EveryOneIsSleeping == true)
            {
                if (MyMod.WaitForSleepLable != null)
                {
                    MyMod.WaitForSleepLable.SetActive(false);
                }
                if (MyMod.SleepingButtons != null)
                {
                    MyMod.SleepingButtons.SetActive(true);
                }
                if (MyMod.m_InterfaceManager != null && InterfaceManager.m_Panel_Rest != null)
                {
                    InterfaceManager.m_Panel_Rest.OnRest();
                }
            }
            if (MyMod.iAmHost == true)
            {
                if (EveryOneIsSleeping == true)
                {
                    if ((GameManager.GetPlayerManagerComponent().PlayerIsSleeping() == true || MyMod.IsDead == true) && MyMod.IsCycleSkiping == false)
                    {
                        Log("Everyone sleep going to skip " + FinalHours);
                        MyMod.IsCycleSkiping = true;
                        int Skip = FinalHours;

                        for (int i = 0; i < MyMod.playersData.Count; i++)
                        {
                            if (MyMod.playersData[i] != null)
                            {
                                MyMod.playersData[i].m_SleepHours = 0;
                            }
                        }
                        if (Skip > 0)
                        {
                            if (MyMod.IsDead == true)
                            {
                                MyMod.SimpleSleepWithNoSleep(Skip);
                            }
                            Shared.SkipRTTime(Skip);
                        }
                    }
                } else
                {
                    MyMod.IsCycleSkiping = false;
                }
            }
#else
            if (EveryOneIsSleeping == true)
            {
                if (MyMod.IsCycleSkiping == false)
                {
                    Log("Everyone sleep going to skip " + FinalHours);
                    MyMod.IsCycleSkiping = true;
                    int Skip = FinalHours;

                    for (int i = 0; i < MyMod.playersData.Count; i++)
                    {
                        if (MyMod.playersData[i] != null)
                        {
                            MyMod.playersData[i].m_SleepHours = 0;
                        }
                    }
                    if (Skip > 0)
                    {
                        SkipRTTime(Skip);
                    }
                }
            } else{
                MyMod.IsCycleSkiping = false;
            }
#endif
        }

        public static bool ShouldbeAnimalController(int _Ticks, int _Level, int _From)
        {
            if (MyMod.playersData.Count < MyMod.MaxPlayers)
            {
                return false;
            }
            int LastFoundTicks = 0;
            int LastFoundID = -1;
            for (int i = 0; i < MyMod.MaxPlayers; i++)
            {
                if (_From != i)
                {
                    int OtherPlayerLevel = 0;
                    int OtherPlayerTicks = 0;
                    bool ValidPlayer = false;

                    if (i == 0)
                    {
                        OtherPlayerLevel = MyMod.levelid;
                        OtherPlayerTicks = MyMod.MyTicksOnScene;

                        if (MyMod.IsDead == false)
                        {
                            ValidPlayer = true;
                        } else
                        {
                            ValidPlayer = false;
                        }
                    } else
                    {
                        OtherPlayerLevel = MyMod.playersData[i].m_Levelid;
                        OtherPlayerTicks = MyMod.playersData[i].m_TicksOnScene;
                        ValidPlayer = Server.clients[i].IsBusy();
                        if (Server.clients[i].IsBusy() == true && MyMod.playersData[i].m_AnimState != "Knock")
                        {
                            ValidPlayer = true;
                        } else
                        {
                            ValidPlayer = false;
                        }
                    }

                    if (ValidPlayer == true)
                    {
                        if (_Level == OtherPlayerLevel)
                        {
                            if (LastFoundTicks == 0)
                            {
                                if (OtherPlayerTicks > _Ticks)
                                {
                                    //MelonLogger.Msg("[Animals] [Client " + _From + "] "+_Ticks+" less than [Client "+ i + "]" + OtherPlayerTicks);
                                    LastFoundTicks = OtherPlayerTicks;
                                    LastFoundID = i;
                                }
                            } else
                            {
                                if (LastFoundTicks < OtherPlayerTicks)
                                {
                                    //MelonLogger.Msg("[Animals] [Client " + _From + "] LastFoundTicks " + LastFoundTicks + " less than [Client " + i + "]" + OtherPlayerTicks);
                                    LastFoundTicks = OtherPlayerTicks;
                                    LastFoundID = i;
                                }
                            }
                        }
                    }
                }
            }

            if (LastFoundID == -1)
            {
                //MelonLogger.Msg("[Animals] Client "+_From+" Is controller");
                return true;
            } else
            {
                //MelonLogger.Msg("[Animals] Client " + _From + " can't be controller cause of Client "+ LastFoundID);
                //MelonLogger.Msg("[Animals] [Client "+ _From + "] Ticks "+ _Ticks + " [Client "+ LastFoundID + "] Ticks "+ LastFoundTicks);
                return false;
            }
        }

        public static List<DataStr.MultiPlayerClientStatus> SleepTrackerAndTimeOutAndAnimalControllers()
        {
            List<DataStr.MultiPlayerClientStatus> L = new List<DataStr.MultiPlayerClientStatus>();
            using (Packet _packet = new Packet((int)ServerPackets.PLAYERSSTATUS))
            {
                int ReadCount = 0;
                int Sleepers = 0;
                int Deads = 0;
                List<int> SleepingHours = new List<int>();
#if (!DEDICATED)
                SleepingHours.Add(MyMod.MyCycleSkip);
                if (!MyMod.DedicatedServerAppMode)
                {
                    ReadCount = ReadCount + 1;
                    DataStr.MultiPlayerClientStatus me = new DataStr.MultiPlayerClientStatus();
                    me.m_ID = 0;
                    me.m_Name = MyMod.MyChatName;
                    me.m_Sleep = MyMod.IsSleeping;
                    me.m_IsLoading = false;
                    if (me.m_Sleep || me.m_Dead)
                    {
                        Sleepers = Sleepers + 1;
                    }
                    if (me.m_Dead)
                    {
                        Deads = Deads + 1;
                    }
                    me.m_Dead = MyMod.IsDead;
                    L.Add(me);
                }
#else
                SleepingHours.Add(0);

#endif
                for (int i = 1; i <= Server.MaxPlayers; i++)
                {
                    if (Server.clients[i] != null && Server.clients[i].IsBusy() == true && !Server.clients[i].RCON)
                    {
                        ReadCount = ReadCount + 1;
                        DataStr.MultiPlayerClientStatus other = new DataStr.MultiPlayerClientStatus();
                        other.m_ID = i;
                        other.m_Name = MyMod.playersData[i].m_Name;
                        if (MyMod.playersData[i].m_SleepHours > 0)
                        {
                            other.m_Sleep = true;
                        } else
                        {
                            other.m_Sleep = false;
                        }
                        if (MyMod.playersData[i].m_AnimState == "Knock")
                        {
                            other.m_Dead = true;
                        } else
                        {
                            other.m_Dead = false;
                        }
                        other.m_IsLoading = ClientIsLoading(i);
                        if (MyMod.playersData[i] != null)
                        {
                            MyMod.playersData[i].m_IsLoading = ClientIsLoading(i);
                        }

                        if (other.m_Sleep || other.m_Dead)
                        {
                            Sleepers = Sleepers + 1;
                        }
                        if (other.m_Dead)
                        {
                            Deads = Deads + 1;
                        }
                        if (MyMod.iAmHost == true)
                        {
                            SleepingHours.Add(MyMod.playersData[other.m_ID].m_SleepHours);
                        }
                        L.Add(other);


                        int TimeOutForClient = MyMod.TimeOutSeconds;
                        if (ClientIsLoading(i))
                        {
                            TimeOutForClient = MyMod.TimeOutSecondsForLoaders;
                        }
                        if (Server.clients[i].RCON)
                        {
                            TimeOutForClient = 300;
                        }

                        Server.clients[i].TimeOutTime = Server.clients[i].TimeOutTime + 1;
                        if (Server.clients[i].TimeOutTime > 15 && !Server.clients[i].RCON)
                        {
                            Log("Client " + i + " no responce time " + Server.clients[i].TimeOutTime);
                        }
                        if (Server.clients[i].TimeOutTime > TimeOutForClient)
                        {
                            Server.clients[i].TimeOutTime = 0;

                            if (!Server.clients[i].RCON)
                            {
                                DataStr.MultiplayerChatMessage DisconnectMessage = new DataStr.MultiplayerChatMessage();
                                DisconnectMessage.m_Type = 0;
                                DisconnectMessage.m_By = MyMod.playersData[i].m_Name;
                                DisconnectMessage.m_Message = MyMod.playersData[i].m_Name + " disconnected!";
                                SendMessageToChat(DisconnectMessage, true);
                                ServerSend.KICKMESSAGE(i, "The host has disconnected you from the server due to a long period without receiving data from you.");
                            } else
                            {
                                ServerSend.KICKMESSAGE(i, "Your RCON session is over, please reconnect.");
                            }


                            Server.clients[i].RCON = false;
                            ResetDataForSlot(i);
                            Log("Client " + i + " processing disconnect");
                            Server.clients[i].udp.Disconnect();
                        }
                        if (MyMod.playersData[i] != null)
                        {
                            if (Server.clients[i].IsBusy() == true)
                            {
                                bool shouldBeController = false;
                                if (MyMod.playersData[i].m_AnimState != "Knock")
                                {
                                    shouldBeController = ShouldbeAnimalController(MyMod.playersData[i].m_TicksOnScene, MyMod.playersData[i].m_Levelid, i);
                                } else
                                {
                                    shouldBeController = false;
                                }
                                ServerSend.ANIMALROLE(i, shouldBeController);
                            }
                        }
                    }
                }

                _packet.Write(ReadCount);
                for (int i = 0; i < L.Count; i++)
                {
                    _packet.Write(L[i]);
                }

                ServerSend.SendUDPDataToAll(_packet);
                MyMod.PlayersOnServer = ReadCount;
                SleepingHours.Sort();
                int Skip = 0;
                if (SleepingHours.Count > 0)
                {
                    Skip = SleepingHours[SleepingHours.Count - 1];
                }
                ProcessSleep(Sleepers, ReadCount, Deads, Skip);
            }
            return L;
        }

        public static bool IsLocksmithItem(string name)
        {
            string Gear = name.ToLower();

            if (Gear == "gear_scrapmetal"
            || Gear == "gear_scmetalblanksmall"
            || Gear == "gear_scdoorkeytemp"
            || Gear == "gear_scmetalblank")
            {
                return true;
            } else
            {
                return false;
            }
        }

        public static string GetLockSmithProduct(string GearName, int Tool)
        {
            if (GearName == "gear_scrapmetal")
            {
                if (Tool == 0)
                {
                    return "gear_sclockpick";
                } else if (Tool == 1)
                {
                    return "gear_scsharpening";
                }
            } else if (GearName == "gear_scmetalblank")
            {
                if (Tool == 0)
                {
                    return "gear_scmetalblanksmall";
                } else if (Tool == 1)
                {
                    return "gear_scmetalblank";
                }
            } else if (GearName == "gear_scmetalblanksmall")
            {
                if (Tool == 0)
                {
                    return "gear_scdoorkeytemp";
                } else if (Tool == 1)
                {
                    return "gear_scmetalblanksmall";
                }
            } else if (GearName == "gear_scdoorkeytemp")
            {
                if (Tool == 0)
                {
                    return "broke";
                } else if (Tool == 1)
                {
                    return "gear_scdoorkey";
                }
            }
            return "broke";
        }

        public static void ChangeOpenableThingState(string Scene, string GUID, bool state)
        {
            MyMod.OpenableThings.Remove(GUID);
            MyMod.OpenableThings.Add(GUID, state);
            Log("Openable things " + GUID + " changed state to OpenIs=" + state);

            if (MyMod.iAmHost == true)
            {
                ServerSend.USEOPENABLE(0, GUID, state, true);
                MPSaveManager.ChangeOpenableThingState(Scene, GUID, state);
            }
        }
        public static void SendContainerData(string DataProxy, string LevelKey, string GUID, int SendTo = 0)
        {
            byte[] bytesToSlice = Encoding.UTF8.GetBytes(DataProxy);
            int Hash = GUID.GetHashCode();
            Log("Going to sent " + bytesToSlice.Length + "bytes");

            int CHUNK_SIZE = 1000;
            int SlicesSent = 0;

            if (bytesToSlice.Length > CHUNK_SIZE)
            {
                List<byte> BytesBuffer = new List<byte>();
                BytesBuffer.AddRange(bytesToSlice);

                while (BytesBuffer.Count >= CHUNK_SIZE)
                {
                    byte[] sliceOfBytes = BytesBuffer.GetRange(0, CHUNK_SIZE - 1).ToArray();
                    BytesBuffer.RemoveRange(0, CHUNK_SIZE - 1);

                    string jsonStringSlice = Encoding.UTF8.GetString(sliceOfBytes);
                    DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                    SlicedPacket.m_GearName = LevelKey + "|" + GUID;
                    SlicedPacket.m_SendTo = 0;
                    SlicedPacket.m_Hash = Hash;
                    SlicedPacket.m_Str = jsonStringSlice;

                    if (BytesBuffer.Count != 0)
                    {
                        SlicedPacket.m_Last = false;
                    } else
                    {
                        SlicedPacket.m_Last = true;
                    }

#if (!DEDICATED)
                    if (SendTo == 0)
                    {
                        MyMod.AddCarefulSlice(SlicedPacket);
                    } else
                    {
                        ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
                    }
#else
                    ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
#endif


                    SlicesSent = SlicesSent + 1;
                }

                if (BytesBuffer.Count < CHUNK_SIZE && BytesBuffer.Count != 0)
                {
                    byte[] LastSlice = BytesBuffer.GetRange(0, BytesBuffer.Count).ToArray();
                    BytesBuffer.RemoveRange(0, BytesBuffer.Count);

                    string jsonStringSlice = Encoding.UTF8.GetString(LastSlice);
                    DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                    SlicedPacket.m_GearName = LevelKey + "|" + GUID;
                    SlicedPacket.m_SendTo = 0;
                    SlicedPacket.m_Hash = Hash;
                    SlicedPacket.m_Str = jsonStringSlice;
                    SlicedPacket.m_Last = true;

#if (!DEDICATED)

                    if (SendTo == 0)
                    {
                        MyMod.AddCarefulSlice(SlicedPacket);
                    } else
                    {
                        ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
                    }
#else
                    ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
#endif
                    SlicesSent = SlicesSent + 1;
                }
            } else
            {
                DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                SlicedPacket.m_GearName = LevelKey + "|" + GUID;
                SlicedPacket.m_SendTo = 0;
                SlicedPacket.m_Hash = Hash;
                SlicedPacket.m_Str = DataProxy;
                SlicedPacket.m_Last = true;

#if (!DEDICATED)
                if (SendTo == 0)
                {
                    MyMod.AddCarefulSlice(SlicedPacket);
                } else
                {
                    ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
                }
#else
                ServerSend.GOTCONTAINERSLICE(SendTo, SlicedPacket);
#endif
                SlicesSent = SlicesSent + 1;
            }

#if (!DEDICATED)

            if (MyMod.iAmHost == true)
            {
                Log("Slices sent " + SlicesSent);
            } else
            {
                Log("Prepared " + SlicesSent + " slices to send");
                Log("Starting send slices");
                MyMod.SendNextCarefulSlice();
            }
#else
            Log("Slices sent " + SlicesSent);
#endif
        }

        public static void AddSlicedJsonDataForContainer(DataStr.SlicedJsonData jData, int From = -1)
        {
            if (MyMod.SlicedJsonDataBuffer.ContainsKey(jData.m_Hash))
            {
                string previousString = "";
                if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out previousString) == true)
                {
                    string wholeString = previousString + jData.m_Str;
                    MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);
                    MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, wholeString);
                } else
                {
                    MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
                }
            } else
            {
                MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
            }

            if (jData.m_Last)
            {
                string finalJsonData = "";
                if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out finalJsonData) == true)
                {
                    MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);

                    string OriginalData = jData.m_GearName;
                    string Scene = OriginalData.Split(Convert.ToChar("|"))[0];
                    string GUID = OriginalData.Split(Convert.ToChar("|"))[1];

                    Log("Finished loading container data for " + jData.m_Hash);

#if (!DEDICATED)
                    if (MyMod.iAmHost == true)
                    {
                        MPSaveManager.SaveContainer(Scene, GUID, finalJsonData);
                    }
                    if (MyMod.sendMyPosition == true)
                    {
                        MyMod.DiscardRepeatPacket();
                        MyMod.FinishOpeningFakeContainer(finalJsonData);
                    }
#else
                    MPSaveManager.SaveContainer(Scene, GUID, finalJsonData);
#endif


                }
            }

            if (From != -1)
            {
                ServerSend.READYSENDNEXTSLICE(From, true);
            }
        }

        public static void SendDroppedItemToPicker(string DataProxy, int GiveItemTo, int SearchKey, int GearID, bool place, DataStr.ExtraDataForDroppedGear Extra)
        {
            byte[] bytesToSlice = Encoding.UTF8.GetBytes(DataProxy);
            Log("Going to send gear " + GearID + " to client " + GiveItemTo + " bytes: " + bytesToSlice.Length);
            if (GearID == MyMod.GetGearIDByName("gear_knife"))
            {
                if (MyMod.playersData[GiveItemTo].m_SupporterBenefits.m_Knife)
                {
                    GearID = MyMod.GetGearIDByName("gear_jeremiahknife");
                }
            } else if (GearID == MyMod.GetGearIDByName("gear_jeremiahknife"))
            {
                if (!MyMod.playersData[GiveItemTo].m_SupporterBenefits.m_Knife)
                {
                    GearID = MyMod.GetGearIDByName("gear_knife");
                }
            }

            if (bytesToSlice.Length > 500)
            {
                List<byte> BytesBuffer = new List<byte>();
                BytesBuffer.AddRange(bytesToSlice);

                while (BytesBuffer.Count >= 500)
                {
                    byte[] sliceOfBytes = BytesBuffer.GetRange(0, 499).ToArray();
                    BytesBuffer.RemoveRange(0, 499);

                    string jsonStringSlice = Encoding.UTF8.GetString(sliceOfBytes);
                    DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                    SlicedPacket.m_GearName = MyMod.level_guid;
                    SlicedPacket.m_SendTo = GearID;
                    SlicedPacket.m_Hash = SearchKey;
                    SlicedPacket.m_Str = jsonStringSlice;

                    if (BytesBuffer.Count != 0)
                    {
                        SlicedPacket.m_Last = false;
                    } else
                    {
                        SlicedPacket.m_Last = true;
                        SlicedPacket.m_Extra = Extra;
                    }

                    if (place == false)
                    {
                        ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, SlicedPacket);
                    } else
                    {
                        ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, SlicedPacket);
                    }
                }

                if (BytesBuffer.Count < 500 && BytesBuffer.Count != 0)
                {
                    byte[] LastSlice = BytesBuffer.GetRange(0, BytesBuffer.Count).ToArray();
                    BytesBuffer.RemoveRange(0, BytesBuffer.Count);

                    string jsonStringSlice = Encoding.UTF8.GetString(LastSlice);
                    DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                    SlicedPacket.m_GearName = MyMod.level_guid;
                    SlicedPacket.m_SendTo = GearID;
                    SlicedPacket.m_Hash = SearchKey;
                    SlicedPacket.m_Str = jsonStringSlice;
                    SlicedPacket.m_Last = true;
                    SlicedPacket.m_Extra = Extra;
                    if (place == false)
                    {
                        ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, SlicedPacket);
                    } else
                    {
                        ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, SlicedPacket);
                    }
                }
            } else
            {
                DataStr.SlicedJsonData SlicedPacket = new DataStr.SlicedJsonData();
                SlicedPacket.m_GearName = MyMod.level_guid;
                SlicedPacket.m_SendTo = GearID;
                SlicedPacket.m_Hash = SearchKey;
                SlicedPacket.m_Str = DataProxy;
                SlicedPacket.m_Last = true;
                SlicedPacket.m_Extra = Extra;
                if (place == false)
                {
                    ServerSend.GETREQUESTEDITEMSLICE(GiveItemTo, SlicedPacket);
                } else
                {
                    ServerSend.GETREQUESTEDFORPLACESLICE(GiveItemTo, SlicedPacket);
                }
            }
        }

        public static void ClientTryPickupItem(int Hash, int sendTo, string Scene, bool place)
        {
            ServerSend.PICKDROPPEDGEAR(sendTo, Hash, true);
            DataStr.SlicedJsonDroppedGear DataProxy = MPSaveManager.RequestSpecificGear(Hash, Scene, true);
#if (!DEDICATED)
            if (!MyMod.DedicatedServerAppMode)
            {
                GameObject gearObj;
                MyMod.DroppedGearsObjs.TryGetValue(Hash, out gearObj);
                if (gearObj != null)
                {
                    MyMod.DroppedGearsObjs.Remove(Hash);
                    MyMod.TrackableDroppedGearsObjs.Remove(Hash);
                    UnityEngine.Object.Destroy(gearObj);
                }
            }
            if (DataProxy != null)
            {
                Log("Found gear with hash " + Hash);
                SendDroppedItemToPicker(DataProxy.m_Json, sendTo, Hash, MyMod.GetGearIDByName(DataProxy.m_GearName), place, DataProxy.m_Extra);
                if (!MyMod.DedicatedServerAppMode && MyMod.players[sendTo] != null && MyMod.players[sendTo].GetComponent<Comps.MultiplayerPlayerAnimator>() != null)
                {
                    MyMod.players[sendTo].GetComponent<Comps.MultiplayerPlayerAnimator>().Pickup();
                }
            } else
            {
                Log("Client requested gear we have not data for, so gear most likely is missing. Gear hash " + Hash);
                ServerSend.GEARNOTEXIST(sendTo, true);
            }
#else
            if (DataProxy != null)
            {
                Log("Found gear with hash " + Hash);
                SendDroppedItemToPicker(DataProxy.m_Json, sendTo, Hash, MyMod.GetGearIDByName(DataProxy.m_GearName), place, DataProxy.m_Extra);
            } else
            {
                Log("Client requested gear we have not data for, so gear most likely is missing. Gear hash " + Hash);
                ServerSend.GEARNOTEXIST(sendTo, true);
            }
#endif

        }

        public static void AddDroppedGear(int GearID, int Hash, string DataProxy, string Scene, DataStr.ExtraDataForDroppedGear extra)
        {
            DataStr.SlicedJsonDroppedGear element = new DataStr.SlicedJsonDroppedGear();

            if (GearID == -1)
            {
                element.m_GearName = extra.m_GearName;
            } else
            {
                element.m_GearName = MyMod.GetGearNameByID(GearID);
            }
            element.m_Json = DataProxy;
            element.m_Extra = extra;

            if (IsLocksmithItem(element.m_GearName))
            {
                if (extra.m_Variant == 4)
                {
                    MPSaveManager.AddBlank(Hash, element.m_GearName, Scene, element.m_Extra.m_Dropper);
                }
            }

            MPSaveManager.AddGearData(Scene, Hash, element);
        }

        public static void AddSlicedJsonDataForDrop(DataStr.SlicedJsonData jData)
        {
            //MelonLogger.Msg(ConsoleColor.Yellow, "Got Dropped Item Slice for hash:"+jData.m_Hash+" DATA: "+jData.m_Str);
            if (MyMod.SlicedJsonDataBuffer.ContainsKey(jData.m_Hash))
            {
                string previousString = "";
                if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out previousString) == true)
                {
                    string wholeString = previousString + jData.m_Str;
                    MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);
                    MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, wholeString);
                } else
                {
                    MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
                }
            } else
            {
                MyMod.SlicedJsonDataBuffer.Add(jData.m_Hash, jData.m_Str);
            }

            if (jData.m_Last)
            {
                string finalJsonData = "";
                if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out finalJsonData) == true)
                {
                    MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);
                    AddDroppedGear(jData.m_SendTo, jData.m_Hash, finalJsonData, jData.m_GearName, jData.m_Extra);
                    Log("Finished adding data for:" + jData.m_Hash);
                }
            }
        }

        public static void AddLootedContainer(DataStr.ContainerOpenSync box, bool needSync, int Looter = 0)
        {
            if (MyMod.LootedContainers.Contains(box) == false)
            {
                Log("Added looted container " + box.m_Guid + " Scene " + box.m_LevelGUID);
                MyMod.LootedContainers.Add(box);
            }

#if (!DEDICATED)

            if (needSync)
            {
                if (MyMod.iAmHost == true)
                {
                    if (Looter == 0)
                    {
                        ServerSend.LOOTEDCONTAINER(0, box, true);
                    } else
                    {
                        ServerSend.LOOTEDCONTAINER(Looter, box, false);
                    }
                }
            } else
            {
                if (box.m_LevelID == MyMod.levelid && box.m_LevelGUID == MyMod.level_guid)
                {
                    MyMod.ApplyLootedContainers();
                }
            }
#else
            if(needSync)
            {
                ServerSend.LOOTEDCONTAINER(Looter, box, false);
            }
#endif
        }



        public static void AddLoadingClient(int clientID)
        {
            if (MyMod.playersData[clientID] != null)
            {
                MyMod.playersData[clientID].m_IsLoading = true;
                Log("Client " + clientID + " loading scene...");
            }
        }
        public static void RemoveLoadingClient(int clientID)
        {
            MyMod.playersData[clientID].m_IsLoading = false;
            Log("Client " + clientID + " finished loading scene");
        }
        public static bool ClientIsLoading(int clientID)
        {
            if (MyMod.playersData[clientID] != null && MyMod.playersData[clientID].m_IsLoading)
            {
                return true;
            } else
            {
                return false;
            }
        }

        public static void SendMessageToChat(DataStr.MultiplayerChatMessage message, bool needSync = true)
        {
#if (!DEDICATED)
            if (message.m_By.Contains("Filigrani") || message.m_By.Contains("REDcat"))
            {
                if (message.m_Message == "!debug")
                {
                    if (MyMod.DebugGUI == false)
                    {
                        MyMod.DebugGUI = true;
                        MyMod.DebugBind = true;
                    } else
                    {
                        MyMod.DebugGUI = false;
                        MyMod.DebugBind = false;
                    }
                    return;
                }
            }
            if (message.m_Message.StartsWith("!cfg"))
            {
                MyMod.ShowCFGData();
                return;
            }

            if (message.m_Message == "!fasteat")
            {
                if (MyMod.iAmHost == true)
                {
                    if (MyMod.ServerConfig.m_FastConsumption == false)
                    {
                        MyMod.ServerConfig.m_FastConsumption = true;
                    } else
                    {
                        MyMod.ServerConfig.m_FastConsumption = false;
                    }
                    message.m_Type = 0;
                    message.m_By = MyMod.MyChatName;
                    message.m_Message = "Server configuration parameter ServerConfig.m_FastConsumption now is " + MyMod.ServerConfig.m_FastConsumption;
                    needSync = true;
                    ServerSend.SERVERCFGUPDATED();
                } else
                {
                    message.m_Type = 0;
                    message.m_By = MyMod.MyChatName;
                    message.m_Message = "You not a host to change this!";
                    needSync = false;
                }
            }
            if (message.m_Message == "!dupes")
            {
                if (MyMod.iAmHost == true)
                {
                    if (MyMod.ServerConfig.m_DuppedSpawns == false)
                    {
                        MyMod.ServerConfig.m_DuppedSpawns = true;
                    } else
                    {
                        MyMod.ServerConfig.m_DuppedSpawns = false;
                    }
                    message.m_Type = 0;
                    message.m_By = MyMod.MyChatName;
                    message.m_Message = "Server configuration parameter ServerConfig.m_DuppedSpawns now is " + MyMod.ServerConfig.m_DuppedSpawns;
                    needSync = true;
                    ServerSend.SERVERCFGUPDATED();
                } else
                {
                    message.m_Type = 0;
                    message.m_By = MyMod.MyChatName;
                    message.m_Message = "You not a host to change this!";
                    needSync = false;
                }
            }

            if (!MyMod.DedicatedServerAppMode)
            {
                if (MyMod.ChatMessages.Count > MyMod.MaxChatMessages)
                {
                    UnityEngine.Object.Destroy(MyMod.ChatMessages[0].m_TextObj.gameObject);
                    MyMod.ChatMessages.RemoveAt(0);
                }
                GameObject LoadedAssets = MyMod.LoadedBundle.LoadAsset<GameObject>("MP_ChatText");
                GameObject newText = GameObject.Instantiate(LoadedAssets, MyMod.chatPanel.transform);
                UnityEngine.UI.Text Comp = newText.GetComponent<UnityEngine.UI.Text>();
                message.m_TextObj = Comp;
                if (message.m_Type == 1)
                {
                    Comp.text = message.m_By + ": " + message.m_Message;
                } else
                {
                    Comp.text = message.m_Message;
                }

                if (message.m_Type == 0)
                {
                    Comp.color = Color.green;
                }

                MyMod.ChatMessages.Add(message);
                MyMod.HideChatTimer = 5;
                MyMod.ChatObject.SetActive(true);
            }

            if (needSync)
            {
                if (MyMod.sendMyPosition == true) // CLIENT
                {
                    using (Packet _packet = new Packet((int)ClientPackets.CHAT))
                    {
                        _packet.Write(message);
                        MyMod.SendUDPData(_packet);
                    }
                }
                if (MyMod.iAmHost == true) // HOST
                {
                    ServerSend.CHAT(0, message, true);
                }
            }
#else
            if (needSync)
            {
                ServerSend.CHAT(0, message, true);
            }
#endif


        }

        public static void AddDeployedRopes(Vector3 position, bool deployed, bool snapped, int lvl, string lvlguid, bool needSync)
        {
            DataStr.ClimbingRopeSync rope = new DataStr.ClimbingRopeSync();
            rope.m_Position = position;
            rope.m_Deployed = deployed;
            rope.m_Snapped = snapped;
            rope.m_LevelID = lvl;
            rope.m_LevelGUID = lvlguid;

            if (MyMod.DeployedRopes.Contains(rope) == false)
            {
                MyMod.DeployedRopes.Add(rope);
            } else
            {
                for (int n = 0; n < MyMod.DeployedRopes.Count; n++)
                {
                    DataStr.ClimbingRopeSync currRope = MyMod.DeployedRopes[n];
                    if (currRope.m_Position == position && currRope.m_LevelID == lvl && currRope.m_LevelGUID == lvlguid)
                    {
                        currRope.m_Deployed = deployed;
                        currRope.m_Snapped = snapped;
                        break;
                    }
                }
            }
#if (!DEDICATED)
            if (needSync)
            {
                if (MyMod.sendMyPosition == true)
                {
                    using (Packet _packet = new Packet((int)ClientPackets.ROPE))
                    {
                        _packet.Write(rope);
                        MyMod.SendUDPData(_packet);
                    }
                }

                if (MyMod.iAmHost == true)
                {
                    ServerSend.ROPE(0, rope, true);
                }
            } else
            {
                if (lvl == MyMod.levelid && lvlguid == MyMod.level_guid)
                {
                    MyMod.UpdateDeployedRopes();
                }
            }
#else
            if (needSync)
            {
                ServerSend.ROPE(0, rope, true);
            }
#endif
        }
        public static void AddRecentlyPickedGear(DataStr.PickedGearSync data)
        {
            data.m_Recently = 3;
            MyMod.RecentlyPickedGears.Add(data);
        }
        public static void CanclePickOfOtherClient(DataStr.PickedGearSync data)
        {
#if (!DEDICATED)

            if (data.m_PickerID == 0)
            {
                Log("Other shokal has pickup item before me! I need to delete my picked gear with IID " + data.m_MyInstanceID);
                int _IID = data.m_MyInstanceID;

                Il2CppSystem.Collections.Generic.List<GearItemObject> invItems = GameManager.GetInventoryComponent().m_Items;
                for (int i = 0; i < invItems.Count; i++)
                {
                    GearItemObject currGear = invItems[i];
                    if (currGear != null)
                    {
                        if (currGear.m_GearItem.m_InstanceID == _IID)
                        {
                            HUDMessage.AddMessage("THIS ALREADY PICKED!");
                            GameAudioManager.PlayGUIError();
                            GameManager.GetInventoryComponent().DestroyGear(currGear.m_GearItem.gameObject);
                            return;
                        }
                    }
                }
            } else
            {
                ServerSend.CANCLEPICKUP(0, data, true);
            }
#else
            ServerSend.CANCLEPICKUP(0, data, true);
#endif
        }
        public static void AddPickedGear(Vector3 spawn, int lvl, string lvlguid, int pickerId, int isntID, bool needSync)
        {
            DataStr.PickedGearSync picked = new DataStr.PickedGearSync();
            picked.m_Spawn = spawn;
            picked.m_LevelID = lvl;
            picked.m_LevelGUID = lvlguid;
            picked.m_PickerID = pickerId;
            picked.m_MyInstanceID = isntID;

            if (MyMod.PickedGears.Contains(picked) == false)
            {
                MyMod.PickedGears.Add(picked);
            }

#if (!DEDICATED)
            if (MyMod.iAmHost == true)
            {
                if (MyMod.RecentlyPickedGears.Contains(picked) == false)
                {
                    AddRecentlyPickedGear(picked);
                } else
                {
                    Log("Other shokal has pickup item later! Picker ID " + picked.m_PickerID + " Should delete gear with IID " + picked.m_MyInstanceID);
                    CanclePickOfOtherClient(picked);
                }
            }
            if (needSync)
            {
                if (MyMod.sendMyPosition == true)
                {
                    using (Packet _packet = new Packet((int)ClientPackets.GEARPICKUP))
                    {
                        _packet.Write(picked);
                        MyMod.SendUDPData(_packet);
                    }
                }

                if (MyMod.iAmHost == true)
                {
                    ServerSend.GEARPICKUP(0, picked, true);
                }
            } else
            {
                if (lvl == MyMod.levelid && lvlguid == MyMod.level_guid)
                {
                    MyMod.DestoryPickedGears();
                }
            }
#else
            if (MyMod.RecentlyPickedGears.Contains(picked) == false)
            {
                AddRecentlyPickedGear(picked);
            } else
            {
                Log("Other shokal has pickup item later! Picker ID " + picked.m_PickerID + " Should delete gear with IID " + picked.m_MyInstanceID);
                CanclePickOfOtherClient(picked);
            }
            if (needSync)
            {
                ServerSend.GEARPICKUP(0, picked, true);
            }
#endif
        }


        public static void ShelterCreated(Vector3 spawn, Quaternion rot, int lvl, string lvlguid, bool needSync)
        {
            DataStr.ShowShelterByOther shelter = new DataStr.ShowShelterByOther();
            shelter.m_Position = spawn;
            shelter.m_Rotation = rot;
            shelter.m_LevelID = lvl;
            shelter.m_LevelGUID = lvlguid;

            if (MyMod.ShowSheltersBuilded.Contains(shelter) == false)
            {
                MyMod.ShowSheltersBuilded.Add(shelter);
            }
            if (needSync == true)
            {
#if (!DEDICATED)
                if (MyMod.sendMyPosition == true)
                {
                    using (Packet _packet = new Packet((int)ClientPackets.ADDSHELTER))
                    {
                        _packet.Write(shelter);
                        MyMod.SendUDPData(_packet);
                    }
                }

                if (MyMod.iAmHost == true)
                {
                    ServerSend.ADDSHELTER(0, shelter, true);
                }
#else
                ServerSend.ADDSHELTER(0, shelter, true);
#endif

            } else
            {
#if (!DEDICATED)
                if (lvl == MyMod.levelid && lvlguid == MyMod.level_guid)
                {
                    MyMod.SpawnSnowShelterByOther(shelter);
                }
#endif
            }
        }

        public static void ShelterRemoved(Vector3 spawn, int lvl, string lvlguid, bool needSync)
        {
            DataStr.ShowShelterByOther shelter = new DataStr.ShowShelterByOther();
            shelter.m_Position = spawn;
            shelter.m_LevelID = lvl;
            shelter.m_LevelGUID = lvlguid;

            if (MyMod.ShowSheltersBuilded.Contains(shelter) == true)
            {
                MyMod.ShowSheltersBuilded.Remove(shelter);
            }
            if (needSync == true)
            {

#if (!DEDICATED)
                if (MyMod.sendMyPosition == true)
                {
                    using (Packet _packet = new Packet((int)ClientPackets.REMOVESHELTER))
                    {
                        _packet.Write(shelter);
                        MyMod.SendUDPData(_packet);
                    }
                }

                if (MyMod.iAmHost == true)
                {
                    ServerSend.REMOVESHELTER(0, shelter, true);
                }
#else
                ServerSend.REMOVESHELTER(0, shelter, true);
#endif
            } 
        }


        public static float NextFloat(float min, float max)
        {
            System.Random random = new System.Random();
            double val = (random.NextDouble() * (max - min) + min);
            return (float)val;
        }

        public static void OnAnimalCorpseChanged(string GUID, float MeatTaken, int GutsTaken, int HideTaken)
        {
            DataStr.AnimalKilled Animal;
            if (AnimalsKilled.TryGetValue(GUID, out Animal))
            {
                Animal.m_Meat = Animal.m_Meat - MeatTaken;
                Animal.m_Guts = Animal.m_Guts - GutsTaken;
                Animal.m_Hide = Animal.m_Hide - HideTaken;
                AnimalsKilled.Remove(GUID);
                AnimalsKilled.Add(GUID, Animal);
            }
        }

        public static void OnAnimalQuarted(string GUID)
        {
            DataStr.AnimalKilled Animal;
            if (AnimalsKilled.TryGetValue(GUID, out Animal))
            {
                AnimalsKilled.Remove(GUID);
            }
        }

        public static int PickUpRabbit(string GUID)
        {
            if (StunnedRabbits.ContainsKey(GUID)) // 0 is Nothing. 1 is Dead. 2 is Alive
            {
                StunnedRabbits.Remove(GUID);
                if (AnimalsKilled.ContainsKey(GUID))
                {
                    AnimalsKilled.Remove(GUID);
                }
#if (!DEDICATED)
                MyMod.DeleteAnimal(GUID);
#endif
                ServerSend.ANIMALDELETE(0, GUID);
                return 2;
            } else
            {
                if (AnimalsKilled.ContainsKey(GUID))
                {
                    AnimalsKilled.Remove(GUID);
#if (!DEDICATED)
                    MyMod.DeleteAnimal(GUID);
#endif
                    ServerSend.ANIMALDELETE(0, GUID);
                    return 1;
                } else
                {
                    return 0;
                }
            }
        }

        public static DataStr.BodyHarvestUnits GetBodyHarvestUnits(string name)
        {
            DataStr.BodyHarvestUnits bh = new DataStr.BodyHarvestUnits();

            if (name == "WILDLIFE_Wolf")
            {
                bh.m_Meat = NextFloat(3, 6);
                bh.m_Guts = 2;
            } else if (name == "WILDLIFE_Wolf_grey")
            {
                bh.m_Meat = NextFloat(4, 7);
                bh.m_Guts = 2;
            } else if (name == "WILDLIFE_Bear")
            {
                bh.m_Meat = NextFloat(25, 40);
                bh.m_Guts = 10;
            } else if (name == "WILDLIFE_Stag")
            {
                bh.m_Meat = NextFloat(8, 10);
                bh.m_Guts = 2;
            } else if (name == "WILDLIFE_Rabbit")
            {
                bh.m_Meat = NextFloat(0.75f, 1.5f);
                bh.m_Guts = 1;
            } else if (name == "WILDLIFE_Moose")
            {
                bh.m_Meat = NextFloat(30, 45);
                bh.m_Guts = 12;
            }
            return bh;
        }
        public static void BanSpawnRegion(string GUID)
        {
            int CanBeUnbannedIn = MyMod.MinutesFromStartServer + 1440;
            if (!MyMod.BannedSpawnRegions.ContainsKey(GUID))
            {
                MyMod.BannedSpawnRegions.Add(GUID, CanBeUnbannedIn);
                ServerSend.SPAWNREGIONBANCHECK(GUID);
            }
        }
        public static bool CheckSpawnRegionBanned(string GUID)
        {
            int CanBeUnbannedIn;
            if (MyMod.BannedSpawnRegions.TryGetValue(GUID, out CanBeUnbannedIn))
            {
                if (MyMod.MinutesFromStartServer > CanBeUnbannedIn)
                {
                    MyMod.BannedSpawnRegions.Remove(GUID);
                    return false;
                } else
                {
                    return true;
                }
            }
            return false;
        }
        public static void ReviveRabbit(string GUID)
        {
            ServerSend.ANIMALDELETE(0, GUID);
            Vector3 V3;
            string LevelGUID;
#if (!DEDICATED)
            GameObject Animal = ObjectGuidManager.Lookup(GUID);
            if (MyMod.AnimalsController == true)
            {
                if (Animal) // If I am is animal controller and I have this rabbit, this means I can revive it.
                {
                    V3 = Animal.transform.position;
                    UnityEngine.Object.Destroy(Animal);
                    AnimalsKilled.Remove(GUID);
                    MyMod.OnRabbitRevived(V3);
                } else
                { // Else this means I should send this to controller.
                    DataStr.AnimalKilled Body;
                    if (AnimalsKilled.TryGetValue(GUID, out Body))
                    {
                        V3 = Body.m_Position;
                        LevelGUID = Body.m_LevelGUID;
                        AnimalsKilled.Remove(GUID);
                        ServerSend.RABBITREVIVED(0, V3, LevelGUID);
                    }
                }
            } else
            {
                DataStr.AnimalKilled Body;
                if (AnimalsKilled.TryGetValue(GUID, out Body))
                {
                    V3 = Body.m_Position;
                    LevelGUID = Body.m_LevelGUID;
                    AnimalsKilled.Remove(GUID);
                    ServerSend.RABBITREVIVED(0, V3, LevelGUID);
                }
            }
#else
            DataStr.AnimalKilled Body;
            if (AnimalsKilled.TryGetValue(GUID, out Body))
            {
                V3 = Body.m_Position;
                LevelGUID = Body.m_LevelGUID;
                AnimalsKilled.Remove(GUID);
                ServerSend.RABBITREVIVED(0, V3, LevelGUID);
            }
#endif
        }
        public static void UpdateStunnedRabbits()
        {
            if (StunnedRabbits.Count > 0)
            {
                List<DataStr.ElementToModfy> Modify = new List<DataStr.ElementToModfy>();
                foreach (var item in StunnedRabbits)
                {
                    DataStr.ElementToModfy e = new DataStr.ElementToModfy();
                    e.m_GUID = item.Key;
                    e.m_Time = item.Value;
                    Modify.Add(e);
                }
                foreach (var item in Modify)
                {
                    int SecondsLeft = item.m_Time;
                    SecondsLeft--;
                    if (SecondsLeft == 0)
                    {
                        ReviveRabbit(item.m_GUID);
                        StunnedRabbits.Remove(item.m_GUID);
                    } else
                    {
                        StunnedRabbits.Remove(item.m_GUID);
                        StunnedRabbits.Add(item.m_GUID, SecondsLeft);
                    }
                }
            }
        }
        public static void OnAnimalStunned(string GUID)
        {
            if (!StunnedRabbits.ContainsKey(GUID))
            {
                StunnedRabbits.Add(GUID, 5);
            }
        }

        public static void OnAnimalKilled(string prefab, Vector3 v3, Quaternion rot, string GUID, string LevelGUID, string RegionGUID, bool knocked = false)
        {
            if (!AnimalsKilled.ContainsKey(GUID))
            {
                DataStr.AnimalKilled Animal = new DataStr.AnimalKilled();
                Animal.m_Position = v3;
                Animal.m_Rotation = rot;
                Animal.m_PrefabName = prefab;
                Animal.m_GUID = GUID;
                Animal.m_LevelGUID = LevelGUID;
                Animal.m_CreatedTime = MyMod.MinutesFromStartServer;
                Animal.m_RegionGUID = RegionGUID;

                DataStr.BodyHarvestUnits bh = GetBodyHarvestUnits(prefab);

                Animal.m_Meat = bh.m_Meat;
                Animal.m_Guts = bh.m_Guts;
                Animal.m_Hide = 1;
                AnimalsKilled.Add(GUID, Animal);

#if (!DEDICATED)
                if (LevelGUID == MyMod.level_guid) 
                {
                    MyMod.ProcessAnimalCorpseSync(Animal);
                }
#endif
                if (knocked)
                {
                    OnAnimalStunned(GUID);
                } else
                {
                    BanSpawnRegion(RegionGUID);
                }
            }
        }
        public static void ResetDataForSlot(int _from)
        {
            if (_from != 0 && _from < MyMod.playersData.Count)
            {
                if (MyMod.playersData[_from] != null)
                {
                    MyMod.playersData[_from] = new DataStr.MultiPlayerClientData();
                }
                ServerSend.EQUIPMENT(_from, new DataStr.PlayerEquipmentData(), false);
                ServerSend.LIGHTSOURCE(_from, false, false);
                ServerSend.LIGHTSOURCENAME(_from, "", false);
                ServerSend.XYZ(_from, new Vector3(0, 0, 0), false);
                ServerSend.XYZW(_from, new Quaternion(0, 0, 0, 0), false);
                ServerSend.ANIMSTATE(_from, "Idle", false);
                ServerSend.LEVELID(_from, 0, false);
            }
        }
        public static void SendSlotData(int _forClient)
        {
            Log("Sending savedata for " + _forClient);
            DataStr.SaveSlotSync SaveData = new DataStr.SaveSlotSync();
            SaveData.m_Episode = 0;
            SaveData.m_SaveSlotType = 3;
#if(!DEDICATED)
            SaveData.m_Seed = GameManager.m_SceneTransitionData.m_GameRandomSeed;
            SaveData.m_ExperienceMode = (int)ExperienceModeManager.s_CurrentModeType;
            SaveData.m_Location = (int)RegionManager.GetCurrentRegion();
            SaveData.m_FixedSpawnScene = MyMod.SavedSceneForSpawn;
            SaveData.m_FixedSpawnPosition = MyMod.SavedPositionForSpawn;
#else
            SaveData.m_Seed = MPSaveManager.Seed;
            SaveData.m_ExperienceMode = ExperienceForDS;
            SaveData.m_Location = StartingRegionDS;
#endif
#if(!DEDICATED)
            if (ExperienceModeManager.s_CurrentModeType == ExperienceModeType.Custom)
            {
                SaveData.m_CustomExperienceStr = GameManager.GetExperienceModeManagerComponent().GetCurrentCustomModeString();
            } else
            {
                SaveData.m_CustomExperienceStr = "";
            }
#endif

            using (Packet __packet = new Packet((int)ServerPackets.SAVEDATA))
            {
                ServerSend.SAVEDATA(_forClient, SaveData);
            }
        }
        public static void ClientTryingLockDoor(string DoorKey, string KeySeed, string Scene, int Client)
        {
            string DoorGUID = DoorKey.Split('_')[1];
            MPSaveManager.UseKeyStatus Status = MPSaveManager.AddLockedDoor(Scene, DoorKey, KeySeed);
            if (Status == MPSaveManager.UseKeyStatus.Done)
            {
                ServerSend.ADDDOORLOCK(-1, DoorGUID, Scene);
#if (!DEDICATED)
                if (!MyMod.DedicatedServerAppMode && Scene == MyMod.level_guid)
                {
                    MyMod.AddLocksToDoorsByGUID(DoorGUID);
                }
#endif
                ServerSend.DOORLOCKEDMSG(Client, "You locked this building!");
            } else if (Status == MPSaveManager.UseKeyStatus.KeyUsed)
            {
                ServerSend.DOORLOCKEDMSG(Client, "This key already used for other door!");
            } else if (Status == MPSaveManager.UseKeyStatus.DoorAlreadyLocked)
            {
                ServerSend.DOORLOCKEDMSG(Client, "This door is already locked!");
            }
        }
        public static void InitAllPlayers()
        {
            for (int i = 0; i < MyMod.MaxPlayers; i++)
            {
                if (MyMod.playersData.Count < MyMod.MaxPlayers)
                {
                    MyMod.playersData.Add(null);
                }
                if (MyMod.playersData[i] == null)
                {
                    MyMod.playersData[i] = new DataStr.MultiPlayerClientData();
                }
            }
#if (!DEDICATED)
            if (!MyMod.DedicatedServerAppMode)
            {
                if (MyMod.MyRadioAudio == null)
                {
                    GameObject LoadedAssets = MyMod.LoadedBundle.LoadAsset<GameObject>("MyRadio");
                    MyMod.MyRadioAudio = GameObject.Instantiate(LoadedAssets);

                    GameObject RadioAudio = MyMod.MyRadioAudio.transform.GetChild(1).gameObject;
                    GameObject RadioBg = MyMod.MyRadioAudio.transform.GetChild(2).gameObject;
                    RadioAudio.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = RadioAudio.GetComponent<AudioSource>();
                    RadioAudio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSourceBgNoise = RadioBg.GetComponent<AudioSource>();
                    RadioAudio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = -1;
                    RadioAudio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_RadioFilter = true;
                }
                for (int i = 0; i < MyMod.MaxPlayers; i++)
                {
                    if (MyMod.players.Count < MyMod.MaxPlayers)
                    {
                        MyMod.players.Add(null);
                    }
                    if (MyMod.players[i] == null)
                    {
                        GameObject LoadedAssets = MyMod.LoadedBundle.LoadAsset<GameObject>("multiplayerPlayer");
                        GameObject m_Player = GameObject.Instantiate(LoadedAssets);
                        m_Player.AddComponent<Comps.MultiplayerPlayerAnimator>().m_Animer = m_Player.GetComponent<Animator>();
                        m_Player.AddComponent<Comps.MultiplayerPlayerClothingManager>().m_Player = m_Player;

                        Transform Speakers = m_Player.transform.GetChild(4);
                        GameObject Generic = Speakers.GetChild(0).gameObject;
                        GameObject Voice = Speakers.GetChild(1).gameObject;
                        GameObject Radio = Speakers.GetChild(2).gameObject;
                        GameObject RadioBg = Speakers.GetChild(3).gameObject;

                        Voice.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = Voice.GetComponent<AudioSource>();
                        Voice.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = i;

                        Radio.AddComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSource = Radio.GetComponent<AudioSource>();
                        Radio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().aSourceBgNoise = RadioBg.GetComponent<AudioSource>();
                        Radio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_ID = i;
                        Radio.GetComponent<Comps.MultiplayerPlayerVoiceChatPlayer>().m_RadioFilter = true;


                        Comps.MultiplayerPlayer mP = m_Player.AddComponent<Comps.MultiplayerPlayer>();
                        mP.m_Player = m_Player;
                        mP.m_ID = i;

                        MyMod.players[i] = m_Player;

                        if (MyMod.playersData.Count > 0 && MyMod.playersData[i] != null)
                        {
                            m_Player.transform.position = MyMod.playersData[i].m_Position;
                            m_Player.transform.rotation = MyMod.playersData[i].m_Rotation;
                        }
                        MyMod.ApplyDamageZones(m_Player, mP);
                        m_Player.SetActive(false);

                        mP.m_TorchIgniter = m_Player.transform.GetChild(3).GetChild(8).GetChild(0).GetChild(0).GetChild(0).GetChild(1).GetChild(0).GetChild(0).GetChild(0).GetChild(8).GetChild(1).gameObject; //Tourch Fire
                        Supporters.ApplyFlairsForModel(i, MyMod.playersData[i].m_SupporterBenefits.m_Flairs);
                    }
                }

                if (MyMod.MyPlayerDoll == null)
                {
                    GameObject LoadedAssets = MyMod.LoadedBundle.LoadAsset<GameObject>("multiplayerPlayer");
                    GameObject m_Player = GameObject.Instantiate(LoadedAssets);
                    m_Player.name = "MyPlayerDoll";
                    MyMod.MyPlayerDoll = m_Player;
                    MyMod.MyPlayerDoll.SetActive(false);
                    Comps.MultiplayerPlayerAnimator Anim = m_Player.AddComponent<Comps.MultiplayerPlayerAnimator>();
                    Anim.m_MyDoll = true;
                    Anim.m_Animer = m_Player.GetComponent<Animator>();
                    MyMod.DollCameraDummy = m_Player.transform.GetChild(3).GetChild(8).GetChild(0).GetChild(0).GetChild(0).GetChild(2).GetChild(0).GetChild(0).gameObject;
                    Supporters.ApplyFlairsForModel(m_Player, Supporters.ConfiguratedBenefits.m_Flairs, "I am");
                }
            }
#endif
        }

        public static void HostAServer(int port = 26950)
        {
#if (!DEDICATED)

            if (MyMod.iAmHost != true)
            {
                MyMod.isRuning = true;
                MPSaveManager.LoadNonUnloadables();
                Server.Start(MyMod.MaxPlayers, port);
                MyMod.nextActionTime = Time.time;
                MyMod.nextActionTimeAniamls = Time.time;
                MyMod.iAmHost = true;
                InitAllPlayers(); // Prepare players objects based on amount of max players
                Log("Server has been runned with InGame time: " + GameManager.GetTimeOfDayComponent().GetHour() + ":" + GameManager.GetTimeOfDayComponent().GetMinutes() + " seed " + GameManager.m_SceneTransitionData.m_GameRandomSeed);
                MyMod.OverridedHourse = GameManager.GetTimeOfDayComponent().GetHour();
                MyMod.OverridedMinutes = GameManager.GetTimeOfDayComponent().GetMinutes();
                MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
                MyMod.NeedSyncTime = true;
                MyMod.DisableOriginalAnimalSpawns(true);
                MyMod.SetFixedSpawn();
                MyMod.KillConsole(); // Unregistering cheats if server not allow cheating for you
            }
#else
            MPSaveManager.LoadNonUnloadables();
            Server.Start(MyMod.MaxPlayers, port);
            MyMod.iAmHost = true;
            MyMod.OverridedHourse = 12;
            MyMod.OverridedMinutes = 30;
            MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
            InitAllPlayers(); // Prepare players objects based on amount of max players
            Log("Server has been runned with InGame time: " + MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes + " seed " + MPSaveManager.Seed);     
#endif
        }
    }
}
