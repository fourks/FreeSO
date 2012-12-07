﻿/*The contents of this file are subject to the Mozilla Public License Version 1.1
(the "License"); you may not use this file except in compliance with the
License. You may obtain a copy of the License at http://www.mozilla.org/MPL/

Software distributed under the License is distributed on an "AS IS" basis,
WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
the specific language governing rights and limitations under the License.

The Original Code is the TSOClient.

The Initial Developer of the Original Code is
Mats 'Afr0' Vederhus. All Rights Reserved.

Contributor(s): ______________________________________.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Linq;
using Microsoft.Win32;
using System.IO;
using SimsLib.FAR3;

namespace Mr.Shipper
{
    class Program
    {
        private static int[] m_RandomNumbers = new int[200];
        private static int m_RandomCounter = 0;

        static void Main(string[] args)
        {
            Random Rnd = new Random();
            m_RandomNumbers = Enumerable.Range(10240, 12240).OrderBy(i => Rnd.Next()).ToArray();

            //Find the path to TSO on the user's system.
            RegistryKey softwareKey = Registry.LocalMachine.OpenSubKey("SOFTWARE");
            if (Array.Exists(softwareKey.GetSubKeyNames(), delegate(string s) { return s.CompareTo("Maxis") == 0; }))
            {
                RegistryKey maxisKey = softwareKey.OpenSubKey("Maxis");
                if (Array.Exists(maxisKey.GetSubKeyNames(), delegate(string s) { return s.CompareTo("The Sims Online") == 0; }))
                {
                    RegistryKey tsoKey = maxisKey.OpenSubKey("The Sims Online");
                    string installDir = (string)tsoKey.GetValue("InstallDir");
                    installDir += "\\TSOClient\\";
                    GlobalSettings.Default.StartupPath = installDir;
                }
                else
                {
                    Console.WriteLine("Error TSO was not found on your system.");
                    Console.ReadLine();
                    return;
                }
            }
            else
            {
                Console.WriteLine("Error: No Maxis products were found on your system.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Building database of existing entries...");
            Database.BuildEntryDatabase();
            Console.WriteLine("Done!");

            Console.WriteLine("Generating uigraphics database...");
            GenerateUIGraphicsDatabase();
            Console.WriteLine("Done!");

            m_RandomNumbers = Enumerable.Range(12240, 14240).OrderBy(i => Rnd.Next()).ToArray();

            Console.WriteLine("Generating collections database...");
            GenerateCollectionsDatabase();
            Console.WriteLine("Done!");

            Console.WriteLine("Generating purchasables database...");
            GeneratePurchasablesDatabase();
            Console.WriteLine("Done!");

            Console.WriteLine("Generating outfits database...");
            GenerateOutfitsDatabase();
            Console.WriteLine("Done!");

            Console.ReadLine();
        }

        /// <summary>
        /// Generates a database of the files in the uigraphics folder,
        /// as well as a *.cs file with an enumeration of the same files
        /// and their corresponding FileIDs.
        /// </summary>
        private static void GenerateUIGraphicsDatabase()
        {
            Dictionary<Far3Entry, string> UIEntries = new Dictionary<Far3Entry, string>();

            AddFilesFromDir(GlobalSettings.Default.StartupPath + "uigraphics\\", "", ref UIEntries);

            Directory.CreateDirectory("packingslips");
            StreamWriter Writer = new StreamWriter(File.Create("packingslips\\UIFileIDs.cs"));

            Writer.WriteLine("using System;");
            Writer.WriteLine("");
            Writer.WriteLine("namespace TSOClient");
            Writer.WriteLine("{");
            Writer.WriteLine("  //Generated by Mr. Shipper - filenames have been sanitized, and does not match");
            Writer.WriteLine("  //actual filenames character for character!");
            Writer.WriteLine("  partial class FileIDs");
            Writer.WriteLine("  {");
            Writer.WriteLine("      public enum UIFileIDs");
            Writer.WriteLine("      {");

            int StopCounter = 0;
            foreach (KeyValuePair<Far3Entry, string> KVP in UIEntries)
            {
                StopCounter++;

                if (StopCounter < UIEntries.Count)
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) + 
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", 
                        KVP.Key.TypeID)).Replace("0x", "") + ",");
                }
                else
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) + 
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", 
                        KVP.Key.TypeID)).Replace("0x", ""));
                }
            }

            Writer.WriteLine("      };");
            Writer.WriteLine("  }");
            Writer.WriteLine("}");
            Writer.Close();

            Writer = new StreamWriter(File.Create("packingslips\\uigraphics.xml"));
            Writer.WriteLine("<?xml version=\"1.0\"?>");
            Writer.WriteLine("<AssetList>");

            //For some really weird reason, "key" and "assetID" are written in reverse order...
            foreach (KeyValuePair<Far3Entry, string> KVP in UIEntries)
            {
                if (KVP.Value.Contains(".dat"))
                {
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + KVP.Value +
                        "\" assetID=\"" + HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
                else
                {
                    DirectoryInfo DirInfo = new DirectoryInfo(KVP.Value);
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + DirInfo.Parent + "\\" +
                        Path.GetFileName(KVP.Value) + "\" assetID=\"" +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) + 
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", 
                        KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
            }

            Writer.WriteLine("</AssetList>");
            Writer.Close();
        }

        private static void GenerateCollectionsDatabase()
        {
            Dictionary<Far3Entry, string> CollectionEntries = new Dictionary<Far3Entry, string>();

            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\bodies\\", "collections", ref CollectionEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\heads\\", "collections", ref CollectionEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\bodies\\", "collections", ref CollectionEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\heads\\", "collections", ref CollectionEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\bodies\\", "collections", ref CollectionEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\heads\\", "collections", ref CollectionEntries);

            StreamWriter Writer = new StreamWriter(File.Create("packingslips\\CollectionsFileIDs.cs"));

            Writer.WriteLine("using System;");
            Writer.WriteLine("");
            Writer.WriteLine("namespace TSOClient");
            Writer.WriteLine("{");
            Writer.WriteLine("  //Generated by Mr. Shipper - filenames have been sanitized, and does not match");
            Writer.WriteLine("  //actual filenames character for character!");
            Writer.WriteLine("  partial class FileIDs");
            Writer.WriteLine("  {");
            Writer.WriteLine("      public enum CollectionsFileIDs");
            Writer.WriteLine("      {");

            int StopCounter = 0;
            foreach (KeyValuePair<Far3Entry, string> KVP in CollectionEntries)
            {
                StopCounter++;

                if (StopCounter < CollectionEntries.Count)
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + ",");
                }
                else
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", ""));
                }
            }

            Writer.WriteLine("      };");
            Writer.WriteLine("  }");
            Writer.WriteLine("}");
            Writer.Close();

            Writer = new StreamWriter(File.Create("packingslips\\collections.xml"));
            Writer.WriteLine("<?xml version=\"1.0\"?>");
            Writer.WriteLine("<AssetList>");

            //For some really weird reason, "key" and "assetID" are written in reverse order...
            foreach (KeyValuePair<Far3Entry, string> KVP in CollectionEntries)
            {
                if (KVP.Value.Contains(".dat"))
                {
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + KVP.Value +
                        "\" assetID=\"" + HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
                else
                {
                    DirectoryInfo DirInfo = new DirectoryInfo(KVP.Value);
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + DirInfo.Parent + "\\" +
                        Path.GetFileName(KVP.Value) + "\" assetID=\"" +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
            }

            Writer.WriteLine("</AssetList>");
            Writer.Close();
        }

        private static void GeneratePurchasablesDatabase()
        {
            Dictionary<Far3Entry, string> PurchasablesEntries = new Dictionary<Far3Entry, string>();

            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\bodies\\", "purchasables", ref PurchasablesEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\heads\\", "purchasables", ref PurchasablesEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\bodies\\", "purchasables", ref PurchasablesEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\heads\\", "purchasables", ref PurchasablesEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\bodies\\", "purchasables", ref PurchasablesEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\heads\\", "purchasables", ref PurchasablesEntries);

            StreamWriter Writer = new StreamWriter(File.Create("packingslips\\PurchasablesFileIDs.cs"));

            Writer.WriteLine("using System;");
            Writer.WriteLine("");
            Writer.WriteLine("namespace TSOClient");
            Writer.WriteLine("{");
            Writer.WriteLine("  //Generated by Mr. Shipper - filenames have been sanitized, and does not match");
            Writer.WriteLine("  //actual filenames character for character!");
            Writer.WriteLine("  partial class FileIDs");
            Writer.WriteLine("  {");
            Writer.WriteLine("      public enum PurchasablesFileIDs");
            Writer.WriteLine("      {");

            int StopCounter = 0;
            foreach (KeyValuePair<Far3Entry, string> KVP in PurchasablesEntries)
            {
                StopCounter++;

                if (StopCounter < PurchasablesEntries.Count)
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + ",");
                }
                else
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", ""));
                }
            }

            Writer.WriteLine("      };");
            Writer.WriteLine("  }");
            Writer.WriteLine("}");
            Writer.Close();

            Writer = new StreamWriter(File.Create("packingslips\\purchasables.xml"));
            Writer.WriteLine("<?xml version=\"1.0\"?>");
            Writer.WriteLine("<AssetList>");

            //For some really weird reason, "key" and "assetID" are written in reverse order...
            foreach (KeyValuePair<Far3Entry, string> KVP in PurchasablesEntries)
            {
                if (KVP.Value.Contains(".dat"))
                {
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + KVP.Value +
                        "\" assetID=\"" + HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
                else
                {
                    DirectoryInfo DirInfo = new DirectoryInfo(KVP.Value);
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + DirInfo.Parent + "\\" +
                        Path.GetFileName(KVP.Value) + "\" assetID=\"" +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
            }

            Writer.WriteLine("</AssetList>");
            Writer.Close();
        }

        private static void GenerateOutfitsDatabase()
        {
            Dictionary<Far3Entry, string> OutfitsEntries = new Dictionary<Far3Entry, string>();

            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\bodies\\", "outfits", ref OutfitsEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata\\heads\\", "outfits", ref OutfitsEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\bodies\\", "outfits", ref OutfitsEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata2\\heads\\", "outfits", ref OutfitsEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\bodies\\", "outfits", ref OutfitsEntries);
            AddFilesFromDir(GlobalSettings.Default.StartupPath + "avatardata3\\heads\\", "outfits", ref OutfitsEntries);

            StreamWriter Writer = new StreamWriter(File.Create("packingslips\\OutfitsFileIDs.cs"));

            Writer.WriteLine("using System;");
            Writer.WriteLine("");
            Writer.WriteLine("namespace TSOClient");
            Writer.WriteLine("{");
            Writer.WriteLine("  //Generated by Mr. Shipper - filenames have been sanitized, and does not match");
            Writer.WriteLine("  //actual filenames character for character!");
            Writer.WriteLine("  partial class FileIDs");
            Writer.WriteLine("  {");
            Writer.WriteLine("      public enum OutfitsFileIDs");
            Writer.WriteLine("      {");

            int StopCounter = 0;
            foreach (KeyValuePair<Far3Entry, string> KVP in OutfitsEntries)
            {
                StopCounter++;

                if (StopCounter < OutfitsEntries.Count)
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + ",");
                }
                else
                {
                    Writer.WriteLine("          " + HelperFuncs.SanitizeFilename(Path.GetFileName(KVP.Key.Filename)) + " = " +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", ""));
                }
            }

            Writer.WriteLine("      };");
            Writer.WriteLine("  }");
            Writer.WriteLine("}");
            Writer.Close();

            Writer = new StreamWriter(File.Create("packingslips\\alloutfits.xml"));
            Writer.WriteLine("<?xml version=\"1.0\"?>");
            Writer.WriteLine("<AssetList>");

            //For some really weird reason, "key" and "assetID" are written in reverse order...
            foreach (KeyValuePair<Far3Entry, string> KVP in OutfitsEntries)
            {
                if (KVP.Value.Contains(".dat"))
                {
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + KVP.Value +
                        "\" assetID=\"" + HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
                else
                {
                    DirectoryInfo DirInfo = new DirectoryInfo(KVP.Value);
                    Writer.WriteLine("  " + "<DefineAssetString key=\"" + DirInfo.Parent + "\\" +
                        Path.GetFileName(KVP.Value) + "\" assetID=\"" +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.FileID)) +
                        HelperFuncs.ApplyPadding(string.Format("{0:X}", KVP.Key.TypeID)).Replace("0x", "") + "\"/>");
                }
            }

            Writer.WriteLine("</AssetList>");
            Writer.Close();
        }

        /// <summary>
        /// Adds files from a specified directory to a dictionary of entries.
        /// </summary>
        /// <param name="EntryDir">The directory to scan for entries.</param>
        /// <param name="Filetype">A fully qualified lowercase filetype to scan for (can be empty).</param>
        /// <param name="Entries">The Dictionary to add entries to.</param>
        private static void AddFilesFromDir(string EntryDir, string Filetype, ref Dictionary<Far3Entry, string> Entries)
        {
            string[] Dirs = Directory.GetDirectories(EntryDir);

            foreach(string Dir in Dirs)
            {
                if (Filetype != "")
                {
                    if (Dir.Contains(Filetype))
                    {
                        string[] Files = Directory.GetFiles(Dir);
                        string[] SubDirs = Directory.GetDirectories(Dir);
                        foreach (string Fle in Files)
                        {
                            if (Fle.Contains(".dat"))
                            {
                                FAR3Archive Archive = new FAR3Archive(Fle);

                                foreach (Far3Entry Entry in Archive.GetAllFAR3Entries())
                                    Entries.Add(Entry, Fle.Replace(GlobalSettings.Default.StartupPath, ""));
                            }
                            else
                            {
                                //This works for now, as there are always less than 100 unarchived files.
                                if (m_RandomCounter < 200)
                                    m_RandomCounter++;

                                Far3Entry Entry = new Far3Entry();
                                Entry.Filename = Fle.Replace(GlobalSettings.Default.StartupPath, "");
                                //Entry.FileID = (uint)m_RandomNumbers[m_RandomCounter];
                                Entry.FileID = HelperFuncs.GetFileID(Entry);
                                Entry.TypeID = HelperFuncs.GetTypeID(Path.GetExtension(Fle));

                                HelperFuncs.CheckCollision(Entry.FileID, Entries);

                                //Ignore fonts to minimize the risk of ID collisions.
                                if (!Entry.Filename.Contains(".ttf"))
                                {
                                    if (!Entry.Filename.Contains(".ffn"))
                                        Entries.Add(Entry, Entry.Filename);
                                }
                            }
                        }

                        foreach (string SubDir in SubDirs)
                        {
                            Files = Directory.GetFiles(SubDir);
                            foreach (string SubFle in Files)
                            {
                                if (SubFle.Contains(".dat"))
                                {
                                    FAR3Archive Archive = new FAR3Archive(SubFle);

                                    foreach (Far3Entry Entry in Archive.GetAllFAR3Entries())
                                        Entries.Add(Entry, SubFle.Replace(GlobalSettings.Default.StartupPath, ""));
                                }
                                else
                                {
                                    //This works for now, as there are always less than 100 unarchived files.
                                    if (m_RandomCounter < 200)
                                        m_RandomCounter++;

                                    Far3Entry Entry = new Far3Entry();
                                    Entry.Filename = SubFle.Replace(GlobalSettings.Default.StartupPath, "");
                                    //Entry.FileID = (uint)m_RandomNumbers[m_RandomCounter];
                                    Entry.FileID = HelperFuncs.GetFileID(Entry);
                                    Entry.TypeID = HelperFuncs.GetTypeID(Path.GetExtension(SubFle));

                                    HelperFuncs.CheckCollision(Entry.FileID, Entries);

                                    //Ignore fonts to minimize the risk of ID collisions.
                                    if (!Entry.Filename.Contains(".ttf"))
                                    {
                                        if (!Entry.Filename.Contains(".ffn"))
                                            Entries.Add(Entry, Entry.Filename);
                                    }
                                }
                            }
                        }
                    }
                }
                else //Filetype was empty, so just add all filetypes found...
                {
                    string[] Files = Directory.GetFiles(Dir);
                    string[] SubDirs = Directory.GetDirectories(Dir);
                    foreach (string Fle in Files)
                    {
                        if (Fle.Contains(".dat"))
                        {
                            FAR3Archive Archive = new FAR3Archive(Fle);

                            foreach (Far3Entry Entry in Archive.GetAllFAR3Entries())
                                Entries.Add(Entry, Fle.Replace(GlobalSettings.Default.StartupPath, ""));
                        }
                        else
                        {
                            //This works for now, as there are always less than 100 unarchived files.
                            if (m_RandomCounter < 200)
                                m_RandomCounter++;

                            Far3Entry Entry = new Far3Entry();
                            Entry.Filename = Fle.Replace(GlobalSettings.Default.StartupPath, "");
                            Entry.FileID = HelperFuncs.GetFileID(Entry);
                            Entry.TypeID = HelperFuncs.GetTypeID(Path.GetExtension(Fle));

                            HelperFuncs.CheckCollision((ulong)(((ulong)Entry.FileID) << 32 | ((ulong)(Entry.TypeID >> 32))), Entries);

                            //Ignore fonts to minimize the risk of ID collisions.
                            if (!Entry.Filename.Contains(".ttf"))
                            {
                                if(!Entry.Filename.Contains(".ffn"))
                                    Entries.Add(Entry, Entry.Filename);
                            }
                        }
                    }

                    foreach (string SubDir in SubDirs)
                    {
                        Files = Directory.GetFiles(SubDir);
                        foreach (string SubFle in Files)
                        {
                            if (SubFle.Contains(".dat"))
                            {
                                FAR3Archive Archive = new FAR3Archive(SubFle);

                                foreach (Far3Entry Entry in Archive.GetAllFAR3Entries())
                                    Entries.Add(Entry, SubFle.Replace(GlobalSettings.Default.StartupPath, ""));
                            }
                            else
                            {
                                //This works for now, as there are always less than 100 unarchived files.
                                if (m_RandomCounter < 200)
                                    m_RandomCounter++;

                                Far3Entry Entry = new Far3Entry();
                                Entry.Filename = SubFle.Replace(GlobalSettings.Default.StartupPath, "");
                                //Entry.FileID = (uint)m_RandomNumbers[m_RandomCounter];
                                Entry.FileID = HelperFuncs.GetFileID(Entry);
                                Entry.TypeID = HelperFuncs.GetTypeID(Path.GetExtension(SubFle));

                                HelperFuncs.CheckCollision((ulong)(((ulong)Entry.FileID) << 32 | ((ulong)(Entry.TypeID >> 32))), Entries);

                                //Ignore fonts to minimize the risk of ID collisions.
                                if (!Entry.Filename.Contains(".ttf"))
                                {
                                    if (!Entry.Filename.Contains(".ffn"))
                                        Entries.Add(Entry, Entry.Filename);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
