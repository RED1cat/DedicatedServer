﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using TinyJSON;

namespace SkyCoop
{
    public class ModsValidation
    {
        public static ModValidationData LastRequested = null;
        public static List<string> ServerSideOnlyFiles = new List<string>();

        public static string SHA256CheckSum(string filePath)
        {
            using (SHA256 SHA256 = SHA256Managed.Create())
            {
                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    return Convert.ToBase64String(SHA256.ComputeHash(fileStream));
                }
            }
        }

        public class ModHashPair
        {
            public string m_Name = "";
            public string m_Hash = "";
            public ModHashPair(string n, string h)
            {
                m_Name = n;
                m_Hash = h;
            }
        }

        public class ModValidationData 
        {
            public int m_Hash = 0;
            public List<ModHashPair> m_Files = new List<ModHashPair>();
            public string m_FullString = "";
            public string m_FullStringBase64 = "";
        }

        public static bool ServerSideOnly(string Name)
        {
            return ServerSideOnlyFiles.Contains(Name);
        }

        public static ModValidationData GetModsHash(bool Force = false)
        {
            ModValidationData Valid = new ModValidationData();
            //if (!Force && LastRequested != null)
            //{
            //    return LastRequested;
            //}

            //if (MyMod.DedicatedServerAppMode)
            //{
            //    if (File.Exists(@"Mods\serversideonly.json"))
            //    {
            //        MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Found Server Side Files List!");
            //        string FilterJson = System.IO.File.ReadAllText("Mods\\serversideonly.json");
            //        ServerSideOnlyFiles = JSON.Load(FilterJson).Make<List<string>>();
            //    }
            //}

            //foreach (MelonMod Mod in MelonHandler.Mods)
            //{
            //    string Hash = MelonHandler.GetMelonHash(Mod);
            //    string FileName = Mod.Assembly.GetName().Name + ".dll";
            //    if (!ServerSideOnly(FileName))
            //    {
            //        Valid.m_Files.Add(new ModHashPair(@"Mods\" + FileName, Hash));
            //    }else{
            //        MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + FileName);
            //    }
            //}
            //DirectoryInfo d = new DirectoryInfo("Mods");
            //FileInfo[] Files = d.GetFiles("*.modcomponent");

            //foreach (FileInfo file in Files)
            //{
            //    string Hash = SHA256CheckSum("Mods\\" + file.Name);
            //    string FileName = file.Name;
            //    if (!ServerSideOnly(FileName))
            //    {
            //        Valid.m_Files.Add(new ModHashPair(@"Mods\" + FileName, Hash));
            //    }else{
            //        MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + FileName);
            //    }
            //}
            //foreach (MelonPlugin Plugin in MelonHandler.Plugins)
            //{
            //    string Hash = MelonHandler.GetMelonHash(Plugin);
            //    string FileName = Plugin.Assembly.GetName().Name + ".dll";
            //    if (!ServerSideOnly(FileName))
            //    {
            //        Valid.m_Files.Add(new ModHashPair(@"Plugins\" + FileName, Hash));
            //    }else{
            //        MelonLogger.Msg(ConsoleColor.Yellow, "[ModsValidation][Info] Ignore " + FileName);
            //    }
            //}
            //string MainHash = "";
            //string FullString = "";
            //foreach (ModHashPair Mod in Valid.m_Files)
            //{
            //    if (string.IsNullOrEmpty(MainHash))
            //    {
            //        MainHash = Mod.m_Hash;
            //        FullString = Mod.m_Name;
            //    }else{
            //        MainHash = MainHash + Mod.m_Hash;
            //        FullString = FullString + "\n" + Mod.m_Name;
            //    }

            //    //MelonLogger.Msg(ConsoleColor.Green,"[ModsValidation][Info] " +Mod.m_Name+" Hash: "+Mod.m_Hash);
            //}

            //Valid.m_Hash = MainHash.GetHashCode();
            //Valid.m_FullString = FullString;
            //Valid.m_FullStringBase64 = MyMod.CompressString(FullString);
            //MelonLogger.Msg(ConsoleColor.Blue,"[ModsValidation][Info] Main Hash: " + Valid.m_Hash);
            ////MelonLogger.Msg(ConsoleColor.Magenta, "[ModsValidation][Info] Stock: " + Encoding.UTF8.GetBytes(Valid.m_FullString).Length);
            ////MelonLogger.Msg(ConsoleColor.Magenta, "[ModsValidation][Info] Compressed: " + MyMod.CompressString(Valid.m_FullStringBase64).Length);
            //LastRequested = Valid;
            return Valid;
        }
    }
}
