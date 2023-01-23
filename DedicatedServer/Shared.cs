﻿using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameServer;
using System.Text.RegularExpressions;
using static SkyCoop.DataStr;
using System.Net.NetworkInformation;
#if (!DEDICATED)
using UnityEngine;
using MelonLoader;
using MelonLoader.TinyJSON;
#else
using System.Numerics;
using TinyJSON;
#endif

namespace SkyCoop
{
    public class Shared
    {
        public static int SecondsBeforeUnload = 0;
        public static int ExperienceForDS = 2;
        public static int StartingRegionDS = 0;
        public static Dictionary<string, DataStr.AnimalKilled> AnimalsKilled = new Dictionary<string, DataStr.AnimalKilled>();
        public static Dictionary<string, int> StunnedRabbits = new Dictionary<string, int>();
        public static List<RegionWeatherControler> RegionWeathers = new List<RegionWeatherControler>();
        public static List<float> HoursOffsetTable = new List<float> { 5, 6, 7, 12, 16.5f, 18, 19.5f };
        public static TimeOfDayStatus CurrentTimeOfDayStatus = TimeOfDayStatus.NightEndToDawn;
        public static bool DSQuit = false;
        public static float LocalChatMaxDistance = 70f;


        public enum LoggerColor
        {
            Red,
            Green,
            Blue,
            Yellow,
            Magenta,
            White,
        }


        // WeatherSet
        // Is Pack of WeatherStages. But often, WeatherSet has only one Stage.
        // WeatherSet utilize WeatherStage's type for categorize.
        // For example Blizzard is Pack of Stages: LightSnow, HeavySnow, Blizzard, HeavySnow, LightSnow.
        // But Blizzard *set* still use Blizzard *stage* for categorize. That means, to start blizzard set
        // You need to start set with index 7, that math *stage* blizzard index, damn that so dumb and confusing.
        // Thanksfully we have debug_weather command that handly disply how that all working in runtime.

        //WeatherStage
        //Is setting for weather behavior. Almost always it used as WeatherSet itself,
        //because most of WeatherSets contains only one WeatherStage, but every scene
        //has different amout of WeatherSets.

        public class RegionWeatherControler
        {
            public int m_Region = 0;
            public int m_WeatherType = 0;
            public int m_WeatherSetIndex = 0;
            public float m_Time = 0;
            public float m_Duration = 0;
            public float m_Progress = 0;
            public bool m_WaitsForUpdate = false;
            public bool m_SearchingVolunteer = false;
            public List<float> m_StageDuration = new List<float>();
            public List<float> m_TransitionDuration = new List<float>();
            public int m_WeatherSeed = Guid.NewGuid().GetHashCode();
            public int m_WeatherTimeOfDayCount = 0;
            public TimeOfDayStatus m_PreviousTimeOfDayStatus = TimeOfDayStatus.NightEndToDawn;
            public int m_CoolingHours = 0;
            public int m_WarmingHours = 0;
            public float m_TempHighMin = 0;
            public float m_TempHighMax = 0;
            public float m_TempLowMin = 0;
            public float m_TempLowhMax = 0;
            public float m_HighTemp = 0;
            public float m_LowTemp = 0;
            public bool m_CanUpdateLowTemp = false;
            public bool m_CanUpdateHighTemp = false;
            public int m_PreviousStage = 10;

            public void RecalculateTemperture()
            {
                m_HighTemp = NextFloat(m_TempHighMin, m_TempHighMax);
                m_LowTemp = NextFloat(m_TempLowMin, m_TempLowhMax);
            }
            public void MayRecalculate()
            {
                int hour = MyMod.OverridedHourse;
                if (hour >= m_WarmingHours && hour < m_CoolingHours)
                {
                    if (m_CanUpdateHighTemp)
                    {
                        m_HighTemp = NextFloat(m_TempHighMin, m_TempHighMax);
                        Log("High Temperture Updated For Region "+m_Region, LoggerColor.Blue);
                        m_CanUpdateHighTemp = false;
                    }
                    m_CanUpdateLowTemp = true;
                } else
                {
                    if (m_CanUpdateLowTemp)
                    {
                        m_LowTemp = NextFloat(m_TempLowMin, m_TempLowhMax);
                        Log("Low Temperture Updated For Region " + m_Region, LoggerColor.Blue);
                        m_CanUpdateLowTemp = false;
                    }
                    m_CanUpdateHighTemp = true;
                }
            }

            public RegionWeatherControler(WeatherVolunteerData Data)
            {
                m_Region = Data.CurrentRegion;

                m_WeatherSetIndex = Data.SetIndex;
                m_Duration = Data.WeatherDuration;
                m_WeatherType = Data.WeatherType;
                m_StageDuration = Data.StageDuration;
                m_TransitionDuration = Data.StageTransition;
                m_TempHighMin = Data.HighMin;
                m_TempHighMax = Data.HighMax;
                m_TempLowMin = Data.LowMin;
                m_TempLowhMax = Data.LowMax;
                m_CoolingHours = Data.CoolingHours;
                m_WarmingHours = Data.WarmingHours;
                m_PreviousStage = Data.PreviousStage;
                RecalculateTemperture();
            }

            public void SetNewSet(WeatherVolunteerData Data)
            {
                m_WaitsForUpdate = false;
                m_Time = 0;
                m_Progress = 0;
                m_WeatherTimeOfDayCount = 0;

                m_WeatherSetIndex = Data.SetIndex;
                m_Duration = Data.WeatherDuration;
                m_WeatherType = Data.WeatherType;
                m_StageDuration = Data.StageDuration;
                m_TransitionDuration = Data.StageTransition;
                m_TempHighMin = Data.HighMin;
                m_TempHighMax = Data.HighMax;
                m_TempLowMin = Data.LowMin;
                m_TempLowhMax = Data.LowMax;
                m_CoolingHours = Data.CoolingHours;
                m_WarmingHours = Data.WarmingHours;
                m_PreviousStage = Data.PreviousStage;
                RecalculateTemperture();
            }
            public void AddTime(float Val)
            {
                if (!m_WaitsForUpdate)
                {
                    if (m_Progress >= 1)
                    {
                        m_WaitsForUpdate = true;
                        m_SearchingVolunteer = true;
                        m_Progress = 1;
                        m_Time = m_Duration;
                        

#if (!DEDICATED)
                        if (MyMod.iAmHost)
                        {
                            if (m_Region == MyMod.GetCurrentRegionIfPossible())
                            {
                                Log("WeatherSet is over, I am on same region, so going to provide weatherset for Region " + m_Region, LoggerColor.Blue);
                                RegisterWeatherSetForRegion(0, Pathes.GetWeatherVolunteerData(true));
                            } else
                            {
                                Log("WeatherSet is over, searching weather volunteer for Region " + m_Region, LoggerColor.Blue);
                                ServerSend.WEATHERVOLUNTEER(m_Region);
                            }
                        }
#else
                        Log("WeatherSet is over, searching weather volunteer for Region "+m_Region);
                        ServerSend.WEATHERVOLUNTEER(m_Region);
#endif

                        return;
                    }
                    m_Time += Val;
                    m_Progress = m_Time / m_Duration;
                }
            }
            public void AddTOD(int Ticks, TimeOfDayStatus TODStatus)
            {
                if(m_PreviousTimeOfDayStatus != TODStatus)
                {
                    m_PreviousTimeOfDayStatus = TODStatus;
                    m_WeatherTimeOfDayCount = 0;
                }
                m_WeatherTimeOfDayCount += Ticks;
            }
        }

        public static void NewDayBegins()
        {
            //TODO
        }
        public static void NewTimeOfDayState()
        {
            Log("New Time Of Day Status: " + CurrentTimeOfDayStatus.ToString(), LoggerColor.Blue);
        }

        public static void UpdateTimeOfDayState()
        {
            float Master = 1;
            float HourOffset = MyMod.OverridedHourse - Master;

            if ((double)HourOffset < (double)HoursOffsetTable[0])
            {
                if(CurrentTimeOfDayStatus != TimeOfDayStatus.NightStartToNightEnd)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.NightStartToNightEnd;
                    NewTimeOfDayState();
                }
                return;
            }
            else if ((double)HourOffset < (double)HoursOffsetTable[1])
            {
                if(CurrentTimeOfDayStatus != TimeOfDayStatus.NightEndToDawn)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.NightEndToDawn;
                    NewTimeOfDayState();
                }

                return;
            }
            else if((double)HourOffset < (double)HoursOffsetTable[2])
            {
                if(CurrentTimeOfDayStatus != TimeOfDayStatus.DawnToMorning)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.DawnToMorning;
                    NewTimeOfDayState();
                }
                return;
            }
            else if((double)HourOffset < (double)HoursOffsetTable[3])
            {
                if(CurrentTimeOfDayStatus != TimeOfDayStatus.MorningToMidday)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.MorningToMidday;
                    NewTimeOfDayState();
                }
                return;
            }  
            else if((double)HourOffset < (double)HoursOffsetTable[4])
            {
                if(CurrentTimeOfDayStatus != TimeOfDayStatus.MiddayToAfternoon)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.MiddayToAfternoon;
                    NewTimeOfDayState();
                }
                return;
            }
            else if((double)HourOffset < (double)HoursOffsetTable[5])
            {
                if (CurrentTimeOfDayStatus != TimeOfDayStatus.AfternoonToDusk)
                {
                    CurrentTimeOfDayStatus = TimeOfDayStatus.AfternoonToDusk;
                    NewTimeOfDayState();
                }
                return;
            }

            TimeOfDayStatus GoingToBe = (double)HourOffset < (double)HoursOffsetTable[6] ? TimeOfDayStatus.DuskToNightStart : TimeOfDayStatus.NightStartToNightEnd;
            if(GoingToBe != CurrentTimeOfDayStatus)
            {
                CurrentTimeOfDayStatus = GoingToBe;
                NewTimeOfDayState();
            }
        }
        public static void ForceNextWeatherSet()
        {
            foreach (RegionWeatherControler RegionController in RegionWeathers)
            {
                if (!RegionController.m_WaitsForUpdate)
                {
                    RegionController.m_WaitsForUpdate = true;
                    RegionController.m_SearchingVolunteer = true;
                }
            }
        }
        public static void ForceNextWeather()
        {
            foreach (RegionWeatherControler RegionController in RegionWeathers)
            {
                if (!RegionController.m_WaitsForUpdate)
                {
                    if (RegionController.m_StageDuration.Count == 1)
                    {
                        RegionController.m_WaitsForUpdate = true;
                        RegionController.m_SearchingVolunteer = true;
                    } else{

                        float HoursPased = 0;
                        
                        for (int i = 0; i < RegionController.m_StageDuration.Count; i++)
                        {
                            HoursPased +=RegionController.m_StageDuration[i];

                            if(HoursPased > RegionController.m_Time)
                            {
                                RegionController.m_Time = RegionController.m_StageDuration[i];
                                break;
                            }
                        }
                    }
                }
            }
        }

        public static void RegisterWeatherSetForRegion(int ClientID, WeatherVolunteerData Data)
        {
            foreach (RegionWeatherControler RegionController in RegionWeathers)
            {
                if(RegionController.m_Region == Data.CurrentRegion)
                {
                    if (RegionController.m_WaitsForUpdate)
                    {
                        RegionController.m_SearchingVolunteer = false;
                        RegionController.SetNewSet(Data);
                        Log("New WeatherSet for Region "+ Data.CurrentRegion + " provided by Client "+ ClientID, LoggerColor.Blue);
                    }
                    return;
                }
            }
            Log("New WeatherSet for Region "+ Data.CurrentRegion + " provided by Client " + ClientID, LoggerColor.Blue);
            RegionWeathers.Add(new RegionWeatherControler(Data));
        }
        public static void WeatherUpdate(int Minutes = 1)
        {
            float OneMinuteVal = 0.016f;
            foreach (RegionWeatherControler RegionController in RegionWeathers)
            {
                if (!RegionController.m_WaitsForUpdate)
                {
                    RegionController.AddTime(OneMinuteVal * Minutes);
                    RegionController.MayRecalculate();
                }
            }
        }
        public static void WeatherUpdateSecond()
        {
            float OneMinuteVal = 0.016f;
            foreach (RegionWeatherControler RegionController in RegionWeathers)
            {
                if (!RegionController.m_WaitsForUpdate)
                {
                    RegionController.AddTime(OneMinuteVal / 60);
                    RegionController.AddTOD(90, CurrentTimeOfDayStatus);

#if (!DEDICATED)
                    if(RegionController.m_Region == MyMod.GetCurrentRegionIfPossible())
                    {
                        ClientHandle.DoWeatherSync(RegionController.m_Progress, 
                            RegionController.m_WeatherSeed, 
                            RegionController.m_Duration, 
                            (WeatherStage)RegionController.m_WeatherType, 
                            RegionController.m_WeatherSetIndex, 
                            RegionController.m_StageDuration, 
                            RegionController.m_TransitionDuration, 
                            RegionController.m_WeatherTimeOfDayCount, 
                            RegionController.m_HighTemp, 
                            RegionController.m_LowTemp,
                            RegionController.m_PreviousStage);
                    }
#endif

                    ServerSend.DEDICATEDWEATHER(RegionController.m_Region, 
                        RegionController.m_WeatherType, 
                        RegionController.m_WeatherSetIndex, 
                        RegionController.m_Progress, 
                        RegionController.m_WeatherSeed, 
                        RegionController.m_Duration, 
                        RegionController.m_StageDuration, 
                        RegionController.m_TransitionDuration,
                        RegionController.m_WeatherTimeOfDayCount,
                        RegionController.m_HighTemp,
                        RegionController.m_LowTemp,
                        RegionController.m_PreviousStage);
                }
            }
        }

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
                WeatherUpdateSecond();
                SafeZoneManager.UpdatePlayersSafeZoneStatus();
                for (int n = 0; n < MyMod.RecentlyPickedGears.Count; n++)
                {
                    DataStr.PickedGearSync currGear = MyMod.RecentlyPickedGears[n];
                    if (currGear != null)
                    {
                        if (currGear.m_Recently > 0)
                        {
                            currGear.m_Recently = currGear.m_Recently - 1;
                        }
                        if (currGear.m_Recently <= 0)
                        {
                            MyMod.RecentlyPickedGears.RemoveAt(n);
                        }
                    }
                }
            }
        }

        public static void EveryInGameMinute(int MinutesSkipped = 1)
        {
            MyMod.OverridedMinutes++;

            if (MyMod.OverridedMinutes > 59)
            {
                MyMod.OverridedMinutes = 0;
                MyMod.OverridedHourse = MyMod.OverridedHourse + 1;
                MyMod.PlayedHoursInOnline = MyMod.PlayedHoursInOnline + 1;
                UpdateTimeOfDayState();
            }
            if (MyMod.OverridedHourse > 23)
            {
                MyMod.OverridedHourse = 0;
                UpdateTimeOfDayState();
            }

            MyMod.OveridedTime = MyMod.OverridedHourse + ":" + MyMod.OverridedMinutes;
            if (MyMod.iAmHost)
            {
                MyMod.MinutesFromStartServer++;
                ServerSend.GAMETIME(MyMod.OveridedTime);
                UpdateTicksOnScenes();
                WeatherUpdate(MinutesSkipped);
            }
        }




        public static void Log(string TXT, LoggerColor Color = LoggerColor.White)
        {
#if (!DEDICATED)
            MelonLogger.Msg(MyMod.ConvertLoggerColor(Color), TXT);
#else
            Logger.Log(TXT, Color);
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

        public static long GetVectorHashV2(Vector3 v3)
        {
#if (!DEDICATED)
            
            string Base = v3.x.ToString() + v3.y.ToString() + v3.z.ToString();
#else
            string Base = v3.X.ToString() + v3.Y.ToString() + v3.Z.ToString();
#endif
            return GetDeterministicId(Base);
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
                if (dat.m_GearName.ToLower().Contains("gear_snare") == true && (dat.m_Extra.m_Variant == 1 || dat.m_Extra.m_Variant == 4)) // If is placed snare
                {
                    int minutesPlaced = MyMod.MinutesFromStartServer - dat.m_Extra.m_DroppedTime;
                    int NeedToBePlaced = 720;
                    int minutesLeft = NeedToBePlaced - minutesPlaced + 1;
                    float ChanceToCatch = 50;
                    float ChanceToBreak = 15;
                    if (minutesLeft <= 0) // If snare ready to roll random
                    {
                        DataStr.DroppedGearItemDataPacket Visual;
                        LoadedVisual.TryGetValue(curKey, out Visual);
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
                        Vector3 pos = new Vector3(0, 0, 0);
                        Quaternion rot = new Quaternion(0, 0, 0, 0);
                        if (Visual != null)
                        {
                            pos = Visual.m_Position;
                            rot = Visual.m_Rotation;
                        }

                        if (dat.m_Extra.m_Variant == 4 || RollChance(ChanceToCatch))
                        {
                            dat.m_Extra.m_Variant = 3; // With Rabbit
                            dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                            dat.m_Json = ResourceIndependent.GetSnare(pos, rot, dat.m_Extra.m_Variant);

                            RabbitsBuff.Add(pos); // Add request on rabbit on snare position
                        } else
                        {
                            if (RollChance(ChanceToBreak))
                            {
                                dat.m_Extra.m_Variant = 2; // Broken
                                dat.m_Extra.m_GoalTime = -1; // So it won't reload itself.

                                dat.m_Json = ResourceIndependent.GetSnare(pos, rot, dat.m_Extra.m_Variant);
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

                        
                        if (Visual != null)
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
                    Rabbit.m_Extra.m_GearName = "gear_rabbitcarcass";
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
                        int hashV3 = GetVectorHash(v3);
                        int hashRot = GetQuaternionHash(rot);
                        int hashLevelKey = Scene.GetHashCode();
                        SearchKey = hashV3 + hashRot + hashLevelKey;
                        UnityEngine.Object.Destroy(obj);
                    }
#else
                    int hashV3 = GetVectorHash(v3);
                    int hashRot = GetQuaternionHash(rot);
                    int hashLevelKey = Scene.GetHashCode();
                    SearchKey = hashV3 + hashRot + hashLevelKey;
                    RabbitJson = ResourceIndependent.GetRabbit(v3, rot);
#endif

                    DataStr.DroppedGearItemDataPacket RabbitVisual = new DataStr.DroppedGearItemDataPacket();
                    RabbitVisual.m_Extra = Rabbit.m_Extra;
                    RabbitVisual.m_GearID = -1;
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
            EveryInGameMinute(h * 60);
#else
           EveryInGameMinute(h * 60);
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
            || Gear == "gear_scmetalblank"
            || Gear == "gear_scdoorkeyleadtemp")
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
                    return "broken";
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
            } else if (GearName == "gear_scdoorkeyleadtemp")
            {
                if (Tool == 0)
                {
                    return "broke";
                } else if (Tool == 1)
                {
                    return "gear_scdoorkeylead";
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
        public static string ContainerDecompressedDataBackup = "";

        public static void SendContainerData(string DataProxy, string LevelKey, string GUID, string DecompressedBackup, int SendTo = 0)
        {
            byte[] bytesToSlice = Encoding.UTF8.GetBytes(DataProxy);
            string HashDummy = GUID + DataProxy;
            int Hash = HashDummy.GetHashCode();
            long CheckHash = GetDeterministicId(HashDummy);
            Log("Going to sent " + bytesToSlice.Length + "bytes");
#if (!DEDICATED)
            if (!string.IsNullOrEmpty(DecompressedBackup) && MyMod.sendMyPosition)
            {
                ContainerDecompressedDataBackup = DecompressedBackup;
            }
#endif

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
                    SlicedPacket.m_CheckHash = CheckHash;
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
                    SlicedPacket.m_CheckHash = CheckHash;
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
                SlicedPacket.m_CheckHash = CheckHash;
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
        public static bool CloseContainerOnCancle = false;
        public static void AddSlicedJsonDataForContainer(DataStr.SlicedJsonData jData, int From = -1)
        {
            bool Error = false;
            bool Finished = false;
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
                Finished = true;
                if (MyMod.SlicedJsonDataBuffer.TryGetValue(jData.m_Hash, out finalJsonData) == true)
                {
                    MyMod.SlicedJsonDataBuffer.Remove(jData.m_Hash);

                    string OriginalData = jData.m_GearName;
                    string Scene = OriginalData.Split(Convert.ToChar("|"))[0];
                    string GUID = OriginalData.Split(Convert.ToChar("|"))[1];
                    long CheckHash = GetDeterministicId(GUID + finalJsonData);

                    bool IsBase64 = IsBase64String(finalJsonData);
                    Log("Finished loading container data for " + jData.m_Hash);

                    if (IsBase64)
                    {
                        //Log("This is base64!");
                        
                        if(jData.m_CheckHash == CheckHash)
                        {
                            //Log("Checkhash is valid!");
                        } else{
                            Log("Checkhash is NOT valid. Got "+ CheckHash+" expected "+ jData.m_Hash, LoggerColor.Red);
                            Error = true;
                        }
                    } else
                    {
                        Log("This is NOT base64!", LoggerColor.Red);
                        Error = true;
                    }

#if (!DEDICATED)
                    if (MyMod.iAmHost == true)
                    {
                        if (!Error)
                        {
                            MPSaveManager.SaveContainer(Scene, GUID, finalJsonData);
                        }
                    }
                    if (MyMod.sendMyPosition == true)
                    {
                        if (!Error)
                        {
                            MyMod.DiscardRepeatPacket();
                            MyMod.FinishOpeningFakeContainer(finalJsonData);
                        } else
                        {
                            MyMod.DiscardRepeatPacket();
                            MyMod.RemovePleaseWait();
                            
                            GameManager.GetPlayerManagerComponent().SetControlMode(PlayerControlMode.Normal);
                            string Title = "INVALID CONTAINER DATA";
                            string Text = "Server sent invalid data, this can be network delay problem, please press Confirm to try load data again. If problem stays, message us about this problem.\n\n\n\n\n\n\nGUID: "+Scene +"_"+ GUID+ "\nCheckhash:"+CheckHash+"\nExpected:  "+jData.m_CheckHash+"\nIs base64 "+ IsBase64;
                            CloseContainerOnCancle = true;
                            InterfaceManager.m_Panel_Confirmation.AddConfirmation(Panel_Confirmation.ConfirmationType.Confirm, Title, "\n" + Text, Panel_Confirmation.ButtonLayout.Button_2, Panel_Confirmation.Background.Transperent, null, null);
                        }
                    }
#else
                    if (!Error)
                    {
                        MPSaveManager.SaveContainer(Scene, GUID, finalJsonData);
                    }
#endif
                }
            }

            if (From != -1)
            {
                ServerSend.READYSENDNEXTSLICE(From, true);
                if (Finished)
                {
                    ServerSend.FINISHEDSENDINGCONTAINER(From, Error);
                }
            }
        }

        public static void PieTrigger(ExtraDataForDroppedGear Extra, int Picker)
        {
            if(Extra.m_GearName.ToLower() == "gear_pumpkinpie" && (Extra.m_Dropper.ToLower() == "filigrani" || Extra.m_Dropper.ToLower() == "redcat" || Extra.m_Dropper.ToLower() == "snwball"))
            {
                string PickerName = "unknown";

                if(Picker != 0)
                {
                    if (MyMod.playersData[Picker] != null)
                    {
                        PickerName = MyMod.playersData[Picker].m_Name;
                        
                    }
                }

                if(PickerName.ToLower() != "filigrani" && PickerName.ToLower() != "redcat" && PickerName.ToLower() != "snwball")
                {
                    Log(PickerName+" found pie! Steam/EGSID "+ MyMod.playersData[Picker].m_SteamOrEGSID, LoggerColor.Green);
                    ExecuteCommand("say "+ PickerName + " found pie!");
                }
            }
        }

        public static void SendDroppedItemToPicker(string DataProxy, int GiveItemTo, int SearchKey, int GearID, bool place, DataStr.ExtraDataForDroppedGear Extra)
        {
            byte[] bytesToSlice = Encoding.UTF8.GetBytes(DataProxy);
            Log("Going to send gear to client " + GiveItemTo + " bytes: " + bytesToSlice.Length);
            PieTrigger(Extra, GiveItemTo);

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
                SendDroppedItemToPicker(DataProxy.m_Json, sendTo, Hash, -1, place, DataProxy.m_Extra);
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
                SendDroppedItemToPicker(DataProxy.m_Json, sendTo, Hash, -1, place, DataProxy.m_Extra);
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
            element.m_GearName = extra.m_GearName;
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

        public static void AddSlicedJsonDataForDrop(DataStr.SlicedJsonData jData, int ClientID)
        {
            Log("Got Dropped Item Slice for hash:" + jData.m_Hash + " Is Last " + jData.m_Last);
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
                    Log("Finished adding data for:" + jData.m_Hash + " total " + Encoding.UTF8.GetBytes(finalJsonData).Length + "bytes");
                }
            }
            ServerSend.READYSENDNEXTSLICEGEAR(ClientID, true);
        }

        public static void AddLootedContainer(DataStr.ContainerOpenSync box, bool needSync, int Looter = 0, int State = 0)
        {
            if(MyMod.iAmHost)
            {
                MPSaveManager.AddLootedContainer(box);
            }
#if (!DEDICATED)

            if (needSync)
            {
                if (MyMod.iAmHost == true)
                {
                    if (Looter == 0)
                    {
                        ServerSend.LOOTEDCONTAINER(0, box, State, true);
                    } else
                    {
                        ServerSend.LOOTEDCONTAINER(Looter, box, State, false);
                    }
                }else if (MyMod.sendMyPosition)
                {
                    using (Packet _packet = new Packet((int)ClientPackets.CHANGECONTAINERSTATE))
                    {
                        _packet.Write(box.m_Guid);
                        _packet.Write(MyMod.level_guid);
                        _packet.Write(State);
                        MyMod.SendUDPData(_packet);
                    }
                }
            } else
            {
                if (box.m_LevelGUID == MyMod.level_guid)
                {
                    MyMod.RemoveLootFromContainer(box.m_Guid, State);
                }
            }
#else
            if(needSync)
            {
                ServerSend.LOOTEDCONTAINER(Looter, box, State, false);
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
            if (Supporters.MyID == "76561198152259224" || Supporters.MyID == "76561198867520214")
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
                    string GlobalOrArea = "[Global] ";
                    if (!message.m_Global)
                    {
                        GlobalOrArea = "[Area] ";
                    }
                    
                    Comp.text = GlobalOrArea + message.m_By + ": " + message.m_Message;
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
                    ServerSend.CHAT(0, message, GameManager.GetPlayerTransform().position, MyMod.level_guid);
                }
            }
#else
            if (needSync)
            {
                ServerSend.CHAT(0, message);
            }
            string LogText = "";
            LoggerColor TextColor = LoggerColor.Yellow;
            if (message.m_Type == 1)
            {
                string GlobalOrArea = "[Chat][Global] ";
                if (!message.m_Global)
                {
                    GlobalOrArea = "[Chat][Area] ";
                    TextColor = LoggerColor.Blue;
                }

                LogText = GlobalOrArea + message.m_By + ": " + message.m_Message;
            } else
            {
                LogText = " " + message.m_Message;
                TextColor = LoggerColor.Green;
            }
            Log(LogText, TextColor);
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
        public static void AddPickedGear(Vector3 spawn, int lvl, string lvlguid, int pickerId, int isntID, string gearName, bool needSync)
        {
            DataStr.PickedGearSync picked = new DataStr.PickedGearSync();
            picked.m_Spawn = spawn;
            picked.m_LevelID = lvl;
            picked.m_LevelGUID = lvlguid;
            picked.m_PickerID = pickerId;
            picked.m_MyInstanceID = isntID;
            picked.m_GearName = gearName;

            long Key = MPSaveManager.GetPickedGearKey(picked);

#if (!DEDICATED)
            if (MyMod.iAmHost == true)
            {
                MPSaveManager.AddPickedGear(picked);
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
                    Log("Sent pickup "+ Key);
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
                if (lvlguid == MyMod.level_guid)
                {
                    MyMod.RemovePickedGear(Key);
                }
            }
#else
            MPSaveManager.AddPickedGear(picked);
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
        public static float NextFloat(System.Random random, float min, float max)
        {
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
                MPSaveManager.AnimalsKilledChanged = true;
            }
        }

        public static void OnAnimalQuarted(string GUID)
        {
            DataStr.AnimalKilled Animal;
            if (AnimalsKilled.TryGetValue(GUID, out Animal))
            {
                AnimalsKilled.Remove(GUID);
                MPSaveManager.AnimalsKilledChanged = true;
            }
        }

        public static int PickUpRabbit(string GUID)
        {
            MPSaveManager.AnimalsKilledChanged = true;
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
            MPSaveManager.AnimalsKilledChanged = true;
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
                MPSaveManager.AnimalsKilledChanged = true;
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
#if (!DEDICATED)
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
#if (!DEDICATED)
            if (ExperienceModeManager.s_CurrentModeType == ExperienceModeType.Custom)
            {
                SaveData.m_CustomExperienceStr = GameManager.GetExperienceModeManagerComponent().GetCurrentCustomModeString();
            } else
            {
                SaveData.m_CustomExperienceStr = "";
            }
#endif

            ServerSend.SAVEDATA(_forClient, SaveData);
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
                MyMod.KillConsole();
            }
#else
            MPSaveManager.LoadNonUnloadables();
            Server.Start(MyMod.MaxPlayers, port);
            InitAllPlayers(); // Prepare players objects based on amount of max players
            Log("Server has been runned with InGame time: " + MyMod.OveridedTime + " seed " + MPSaveManager.Seed);     
            UpdateTimeOfDayState();
#endif
        }


        public static DataStr.DedicatedServerData LoadDedicatedServerConfig()
        {
            string Path = "Mods\\server.json";

#if (DEDICATED)
            Path = "server.json";
#endif
            if (System.IO.File.Exists(Path))
            {
                Log("Reading server.json...", LoggerColor.Blue);
                string readText = System.IO.File.ReadAllText(Path);
                DataStr.DedicatedServerData ServerData = JSON.Load(readText).Make<DataStr.DedicatedServerData>();
                Log("Server settings: ", LoggerColor.Blue);
                Log("SaveSlot: " + ServerData.SaveSlot, LoggerColor.Blue);
                Log("ItemDupes: " + ServerData.ItemDupes, LoggerColor.Blue);
                Log("ContainersDupes: " + ServerData.ContainersDupes, LoggerColor.Blue);
                Log("SpawnStyle: " + ServerData.SpawnStyle, LoggerColor.Blue);
                Log("MaxPlayers: " + ServerData.MaxPlayers, LoggerColor.Blue);
                Log("UsingSteam: " + ServerData.UsingSteam, LoggerColor.Blue);
                Log("Ports: " + ServerData.Ports, LoggerColor.Blue);
                Log("Cheats: " + ServerData.Cheats, LoggerColor.Blue);
                Log("SteamServerAccessibility: " + ServerData.SteamServerAccessibility, LoggerColor.Blue);
                Log("RCON: (SECURED)", LoggerColor.Blue);
                Log("DropUnloadPeriod: " + ServerData.DropUnloadPeriod, LoggerColor.Blue);
                Log("SaveScamProtection: " + ServerData.SaveScamProtection, LoggerColor.Blue);
                Log("ModValidationCheck: " + ServerData.ModValidationCheck, LoggerColor.Blue);
                Log("ExperienceMode: " + ServerData.ExperienceMode, LoggerColor.Blue);
                Log("StartRegion: " + ServerData.StartRegion, LoggerColor.Blue);
                ExperienceForDS = ServerData.ExperienceMode;
                StartingRegionDS = ServerData.StartRegion;
                if (ServerData.Seed == 0)
                {
                    Log("Seed: (Random)", LoggerColor.Blue);
                    ServerData.Seed = Guid.NewGuid().GetHashCode();

                } else
                {
                    Log("Seed: " + ServerData.Seed, LoggerColor.Blue);
                }
                Log("PVP: " + ServerData.PVP, LoggerColor.Blue);
                Log("SavingPeriod: " + ServerData.SavingPeriod, LoggerColor.Blue);
                Log("RestartPerioud: " + ServerData.RestartPerioud, LoggerColor.Blue);
                Log("No problems with server.json found!", LoggerColor.Green);
                if (ServerData.RCON != null)
                {
                    MyMod.RCON = ServerData.RCON;
                }
                if (ServerData.DropUnloadPeriod > 5)
                {
                    MPSaveManager.SaveRecentTimer = ServerData.DropUnloadPeriod;
                }
                MyMod.ServerConfig.m_CheckModsValidation = ServerData.ModValidationCheck;
                MyMod.ServerConfig.m_SaveScamProtection = ServerData.SaveScamProtection;
                MyMod.ServerConfig.m_DuppedContainers = ServerData.ContainersDupes;
                MyMod.ServerConfig.m_DuppedSpawns = ServerData.ItemDupes;
                MyMod.ServerConfig.m_PlayersSpawnType = ServerData.SpawnStyle;
                MyMod.ServerConfig.m_CheatsMode = ServerData.Cheats;
                MyMod.ServerConfig.m_PVP = ServerData.PVP;

                MyMod.DsSavePerioud = ServerData.SavingPeriod;
                MyMod.MaxPlayers = ServerData.MaxPlayers;
                return ServerData;
            } else
            {
                DataStr.DedicatedServerData ServerData = new DedicatedServerData();
                ServerData.Seed = Guid.NewGuid().GetHashCode();

                return ServerData;
            }
        }
        public static void FakeDropItem(DataStr.DroppedGearItemDataPacket GearData, bool JustLoad = false)
        {
#if (!DEDICATED)
            if (MyMod.iAmHost && !JustLoad)
            {
                MPSaveManager.AddGearVisual(GearData.m_LevelGUID, GearData);
            }
            if (GearData.m_LevelGUID == MyMod.level_guid)
            {
                MyMod.FakeDropItem(GearData.m_GearID, GearData.m_Position, GearData.m_Rotation, GearData.m_Hash, GearData.m_Extra);
            }
#else
            MPSaveManager.AddGearVisual(GearData.m_LevelGUID, GearData);
#endif
        }

        public static string ExecuteCommand(string CMD, int _fromClient = -1)
        {
            string Low = CMD.ToLower();
            if(_fromClient != -1)
            {
                Log("[RCON] Operator Execute: " + CMD, LoggerColor.Magenta);
            }

            if (Low == "disconnect" || Low == "exit" || Low == "quit")
            {
                if (_fromClient != -1)
                {
                    Server.clients[_fromClient].RCON = false;
                    ResetDataForSlot(_fromClient);
                    Log("RCON process disconnect");
                    Server.clients[_fromClient].udp.Disconnect();
                }
                return "";
            } else if (Low == "trafficdebug" || Low == "traffic" || Low == "trafictrace" || Low == "trafficcheck")
            {
                MyMod.DebugTrafficCheck = !MyMod.DebugTrafficCheck;
                return "DebugTrafficCheck = " + MyMod.DebugTrafficCheck;
            } else if (Low == "savediag" || Low == "unloaddiag")
            {
                MPSaveManager.Diagnostic = !MPSaveManager.Diagnostic;
                return "MPSaveManager.Diagnostic = " + MPSaveManager.Diagnostic;
            } else if (Low == "reset" || Low == "restart")
            {
                MyMod.RestartPerioud = 0;
                MyMod.SecondsWithoutSaving = MyMod.DsSavePerioud;
                return "Server going to restart in 30 seconds!";
            } else if (Low == "shutdown")
            {
#if (DEDICATED)
                DSQuit = true;
#else
                Application.Quit();
#endif
                return "Server will shutdown";
            } else if (Low == "players" || Low == "playerslist" || Low == "clients")
            {
                string List = "Players:";
                foreach (var c in Server.clients)
                {
                    if (c.Value.IsBusy())
                    {
                        if (!c.Value.RCON)
                        {
                            List = List + " " + c.Key + ". " + MyMod.playersData[c.Key].m_Name;
                        } else
                        {
                            List = List + " " + c.Key + ". RCON";
                        }
                    }
                }
                return List;
            } else if (Low.StartsWith("say "))
            {
                string Message = CMD.Remove(0, 4);
                DataStr.MultiplayerChatMessage MSG = new DataStr.MultiplayerChatMessage();
                MSG.m_Type = 0;
                MSG.m_By = "";
                MSG.m_Message = "[SERVER] " + Message;
                SendMessageToChat(MSG, true);
                return "Message sent to chat";
            } else if (Low.StartsWith("skip "))
            {
                int Skip = int.Parse(CMD.Split(' ')[1]);
                SkipRTTime(Skip);
                return "Skipped " + Skip + " hour(s)";
            } else if (Low == "skip")
            {
                SkipRTTime(1);
                return "Skipped 1 hour";
            } else if (Low.StartsWith("rpc "))
            {
                string[] Things = CMD.Split(' ');

                if (Things.Length < 2)
                {
                    return "Invalid syntax!";
                }


                int Client = int.Parse(Things[1]);

                if (Client < 0)
                {
                    return "Client ID can't be negative";
                }

                string RPCDATA = "";
                for (int i = 2; i < Things.Length; i++)
                {
                    if (RPCDATA == "")
                    {
                        RPCDATA = Things[i];
                    } else
                    {
                        RPCDATA = RPCDATA + " " + Things[i];
                    }
                }

                if (Client < MyMod.playersData.Count)
                {
                    if (MyMod.playersData[Client] != null)
                    {
                        if (Server.clients[Client].IsBusy())
                        {
                            ServerSend.RPC(Client, RPCDATA);
                            return "Sent " + RPCDATA + " to client " + Client;
                        }
                    }
                }
                return "There no Client " + Client;
            } else if (Low == "save")
            {
                MPSaveManager.SaveGlobalData();
                return "Manual saving done!";
            }
#if (DEDICATED)
            else if (Low == "next_weather" || Low == "next weather")
            {
                ForceNextWeather();
            } else if (Low == "next_weatherset" || Low == "next weatherset" || Low == "next weather set")
            {
                ForceNextWeatherSet();
            }
#endif

#if (!DEDICATED)
            uConsole.RunCommand(CMD);
            string Responce = uConsoleLog.m_Log[uConsoleLog.m_Log.Count - 1];
            return Responce;
#else
            return "Unknown command";
#endif
        }
        public static string CompressString(string text)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            var memoryStream = new MemoryStream();
            using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                gZipStream.Write(buffer, 0, buffer.Length);
            }

            memoryStream.Position = 0;

            var compressedData = new byte[memoryStream.Length];
            memoryStream.Read(compressedData, 0, compressedData.Length);

            var gZipBuffer = new byte[compressedData.Length + 4];
            Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
            return Convert.ToBase64String(gZipBuffer);
        }

        public static string DecompressString(string compressedText)
        {
            byte[] gZipBuffer = Convert.FromBase64String(compressedText);
            using (var memoryStream = new MemoryStream())
            {
                int dataLength = BitConverter.ToInt32(gZipBuffer, 0);
                memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

                var buffer = new byte[dataLength];

                memoryStream.Position = 0;
                using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                {
                    gZipStream.Read(buffer, 0, buffer.Length);
                }

                return Encoding.UTF8.GetString(buffer);
            }
        }
        public static bool IsBase64String(string s)
        {
            s = s.Trim();
            return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
        }
        public static long GetDeterministicId(string m)
        {
            return (long)m.ToCharArray().Select((c, i) => Math.Pow(i, c % 5) * Math.Max(Math.Sqrt(c), i)).Sum();
        }
        public static string GetMacAddress()
        {
            string macAddr =
                (
                    from nic in NetworkInterface.GetAllNetworkInterfaces()
                    where nic.OperationalStatus == OperationalStatus.Up
                    select nic.GetPhysicalAddress().ToString()
                ).FirstOrDefault();
            return macAddr;
        }
    }
}
