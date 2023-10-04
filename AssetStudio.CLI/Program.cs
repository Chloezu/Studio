﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static AssetStudio.CLI.Studio;

namespace AssetStudio.CLI 
{
    public class Program
    {
        public static void Main(string[] args) => CommandLine.Init(args);

        public static void Run(Options o)
        {
            try
            {
                if (o.ExportOptions)
                {
                    var exportOpt = new ExportOptions() { StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen };
                    exportOpt.ShowDialog();
                }

                var game = GameManager.GetGame(o.GameName);

                if (game == null)
                {
                    Console.WriteLine("Invalid Game !!");
                    Console.WriteLine(GameManager.SupportedGames());
                    return;
                }

                if (game.Type.IsUnityCN())
                {
                    if (o.KeyIndex == -1)
                    {
                        var unityCNForm = new UnityCNForm(ref game) { StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen };
                        if (unityCNForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            o.KeyIndex = Properties.Settings.Default.selectedUnityCNKey;
                        }
                    }
                    if (!UnityCNManager.TryGetEntry(o.KeyIndex, out var unityCN))
                    {
                        Console.WriteLine("Invalid key index !!");
                        Console.WriteLine($"Available Options: \n{UnityCNManager.ToString()}");
                        return;
                    }

                    UnityCN.SetKey(unityCN);
                    Logger.Info($"[UnityCN] Selected Key is {unityCN}");
                }

                Studio.Game = game;
                Logger.LogVerbose = o.Verbose;
                Logger.Default = new ConsoleLogger();
                Logger.FileLogging = Properties.Settings.Default.enableFileLogging;
                AssetsHelper.Minimal = Properties.Settings.Default.minimalAssetMap;
                AssetsHelper.SetUnityVersion(o.UnityVersion);
                MiHoYoBinData.Encrypted = Properties.Settings.Default.encrypted;
                MiHoYoBinData.Key = Properties.Settings.Default.key;

                assetsManager.Silent = o.Silent;
                assetsManager.Game = game;
                assetsManager.SpecifyUnityVersion = o.UnityVersion;
                if (o.Model)
                {
                    AssetsManager.TypesInfo[ClassIDType.GameObject] = (true, true);
                    AssetsManager.TypesInfo[ClassIDType.Texture2D] = (true, false);
                    AssetsManager.TypesInfo[ClassIDType.Animator] = (true, false);
                }
                o.Output.Create();

                if (o.AIFile != null && game.Type.IsGISubGroup())
                {
                    ResourceIndex.FromFile(o.AIFile.FullName);
                }

                if (o.DummyDllFolder != null)
                {
                    assemblyLoader.Load(o.DummyDllFolder.FullName);
                }

                if (o.AssetBrowser)
                {
                    var thread = new Thread(() => {
                        var assetBrowser = new AssetBrowser(LoadFiles) { StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen };
                        assetBrowser.ShowDialog();
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                    thread.Join();
                    return;
                }

                Logger.Info("Scanning for files...");
                var files = o.Input.Attributes.HasFlag(FileAttributes.Directory) ? Directory.GetFiles(o.Input.FullName, "*.*", SearchOption.AllDirectories).OrderBy(x => x.Length).ToArray() : new string[] { o.Input.FullName };
                Logger.Info($"Found {files.Length} files");

                if (o.MapOp.HasFlag(MapOpType.CABMap))
                {
                    AssetsHelper.BuildCABMap(files, o.MapName, o.Input.FullName, game);
                }
                if (o.MapOp.HasFlag(MapOpType.Load))
                {
                    AssetsHelper.LoadCABMap(o.MapName);
                    assetsManager.ResolveDependencies = true;
                }
                if (o.MapOp.HasFlag(MapOpType.AssetMap))
                {
                    if (files.Length == 1)
                    {
                        throw new Exception("Unable to build AssetMap with input_path as a file !!");
                    }
                    var resetEvent = new ManualResetEvent(false);
                    AssetsHelper.BuildAssetMap(files, o.MapName, game, o.Output.FullName, o.MapType, resetEvent, o.Filter);
                    resetEvent.WaitOne();
                }
                if (o.MapOp.HasFlag(MapOpType.Both))
                {
                    var resetEvent = new ManualResetEvent(false);
                    AssetsHelper.BuildBoth(files, o.MapName, o.Input.FullName, game, o.Output.FullName, o.MapType, resetEvent, o.Filter);
                    resetEvent.WaitOne();
                }
                if (o.MapOp.Equals(MapOpType.None) || o.MapOp.HasFlag(MapOpType.Load))
                {
                    var path = Path.GetDirectoryName(Path.GetFullPath(files[0]));
                    ImportHelper.MergeSplitAssets(path);
                    var toReadFile = ImportHelper.ProcessingSplitFiles(files.ToList());
                    LoadFiles(toReadFile);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            void LoadFiles(string[] files)
            {
                var i = 0;
                foreach (var file in files)
                {
                    assetsManager.LoadFiles(file);
                    if (assetsManager.assetsFileList.Count > 0)
                    {
                        BuildAssetData(o.Filter, ref i);
                        ExportAssets(o.Output.FullName, exportableAssets, o.GroupAssetsType);
                    }
                    exportableAssets.Clear();
                    assetsManager.Clear();
                }
            }
        }

        
    }
}