﻿/*
This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
If a copy of the MPL was not distributed with this file, You can obtain one at
http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using FSO.Client.UI.Framework;
using FSO.Client.UI.Panels;
using FSO.Client.UI.Model;
using FSO.Client.Rendering.City;
using Microsoft.Xna.Framework;
using FSO.Client.Utils;
using FSO.Common.Rendering.Framework.Model;
using FSO.Common.Rendering.Framework.IO;
using FSO.Common.Rendering.Framework;
using FSO.Client.Network;
using FSO.LotView;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Utils;
using FSO.Debug;
using FSO.SimAntics.Primitives;
using FSO.HIT;
using FSO.SimAntics.NetPlay.Drivers;
using FSO.SimAntics.NetPlay.Model.Commands;
using System.IO;
using FSO.SimAntics.NetPlay;
using FSO.Client.UI.Controls;
using FSO.Client.Controllers;
using FSO.Client.Controllers.Panels;
using FSO.Client.Debug;
using FSO.Client.UI.Panels.WorldUI;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.Common;
using FSO.Common.Utils;

namespace FSO.Client.UI.Screens
{
    public class CoreGameScreen : FSO.Client.UI.Framework.GameScreen, IGameScreen
    {
        public UIUCP ucp;
        public UIGizmo gizmo;
        public UIInbox Inbox;
        public UIGameTitle Title;

        public UIContainer WindowContainer;
        public UIPersonPage PersonPage;
        public UILotPage LotPage;
        public UIBookmarks Bookmarks;
        public UIRelationshipDialog Relationships;

        private Queue<SimConnectStateChange> StateChanges;

        public Terrain CityRenderer; //city view
        public UICustomTooltip CityTooltip;
        public UICustomTooltipContainer CityTooltipHitArea;
        public UIMessageTray MessageTray;
        public UIJoinLotProgress JoinLotProgress;
        private UIAlert SwitchLotDialog;

        public UILotControl LotControl { get; set; } //world, lotcontrol and vm will be null if we aren't in a lot.
        private LotView.World World;
        public FSO.SimAntics.VM vm { get; set; }
        public VMClientDriver Driver;
        public uint VisualBudget { get; set; }

        private UIMouseEventRef MouseHitAreaEventRef = null;

        public bool InLot
        {
            get
            {
                return (vm != null);
            }
        }

        private int m_ZoomLevel;
        public int ZoomLevel
        {
            get
            {
                return m_ZoomLevel;
            }
            set
            {
                value = Math.Max(1, Math.Min(5, value));

                if(value == 5)
                {
                    var controller = FindController<CoreGameScreenController>();

                    if (controller != null)
                    {
                        controller.Terrain.ZoomOut();
                    }
                }

                if (value < 4)
                {
                    if (vm == null) ZoomLevel = 4; //call this again but set minimum cityrenderer view
                    else
                    {
                        Title.SetTitle(LotControl.GetLotTitle());
                        var targ = (WorldZoom)(4 - value); //near is 3 for some reason... will probably revise
                        if (m_ZoomLevel > 3)
                        {
                            HITVM.Get().PlaySoundEvent(UIMusic.None);
                            if (CityRenderer != null) CityRenderer.m_Zoomed = TerrainZoomMode.Lot;
                            gizmo.Visible = false;
                            LotControl.Visible = true;
                            World.Visible = true;
                            ucp.SetMode(UIUCP.UCPMode.LotMode);
                        } else
                        {
                            if (m_ZoomLevel != value) vm.Context.World.InitiateSmoothZoom(targ);
                        }
                        vm.Context.World.State.Zoom = targ;
                        m_ZoomLevel = value;
                    }
                }
                else //cityrenderer! we'll need to recreate this if it doesn't exist...
                {
                    CityTooltipHitArea.HideTooltip();
                    if (CityRenderer == null) m_ZoomLevel = value; //set to far zoom... again, we should eventually create this.
                    else
                    {
                        if (m_ZoomLevel < 4)
                        { //coming from lot view... snap zoom % to 0 or 1
                            Title.SetTitle(GameFacade.CurrentCityName);
                            CityRenderer.m_ZoomProgress = 1;
                            HITVM.Get().PlaySoundEvent(UIMusic.Map); //play the city music as well
                            CityRenderer.Visible = true;
                            gizmo.Visible = true;
                            if (World != null)
                            {
                                LotControl.Visible = false;
                            }
                            ucp.SetMode(UIUCP.UCPMode.CityMode);
                        }
                        m_ZoomLevel = value;
                        CityRenderer.m_Zoomed = (value == 4)?TerrainZoomMode.Near:TerrainZoomMode.Far;
                    }
                }
                ucp.UpdateZoomButton();
            }
        }

        private int _Rotation = 0;
        public int Rotation
        {
            get
            {
                return _Rotation;
            }
            set
            {
                _Rotation = value;
                if (World != null)
                {
                    switch (_Rotation)
                    {
                        case 0:
                            World.State.Rotation = WorldRotation.TopLeft; break;
                        case 1:
                            World.State.Rotation = WorldRotation.TopRight; break;
                        case 2:
                            World.State.Rotation = WorldRotation.BottomRight; break;
                        case 3:
                            World.State.Rotation = WorldRotation.BottomLeft; break;
                    }
                }
            }
        }

        public sbyte Level
        {
            get
            {
                if (World == null) return 1;
                else return World.State.Level;
            }
            set
            {
                if (World != null)
                {
                    World.State.Level = value;
                }
            }
        }

        public sbyte Stories
        {
            get
            {
                if (World == null) return 2;
                return World.Stories;
            }
        }

        public CoreGameScreen() : base()
        {
            StateChanges = new Queue<SimConnectStateChange>();
            /**
            * Music
            */
            HITVM.Get().PlaySoundEvent(UIMusic.Map);

            /*VMDebug = new UIButton()
            {
                Caption = "Simantics",
                Y = 45,
                Width = 100,
                X = GlobalSettings.Default.GraphicsWidth - 110
            };
            VMDebug.OnButtonClick += new ButtonClickDelegate(VMDebug_OnButtonClick);
            this.Add(VMDebug);*/

            /*SaveHouseButton = new UIButton()
            {
                Caption = "Save House",
                Y = 10,
                Width = 100,
                X = GlobalSettings.Default.GraphicsWidth - 110
            };
            SaveHouseButton.OnButtonClick += new ButtonClickDelegate(SaveHouseButton_OnButtonClick);
            this.Add(SaveHouseButton);*/

            ucp = new UIUCP(this);
            ucp.Y = ScreenHeight - 210;
            ucp.SetInLot(false);
            ucp.UpdateZoomButton();
            ucp.MoneyText.Caption = "0";// PlayerAccount.Money.ToString();
            this.Add(ucp);

            gizmo = new UIGizmo();
            gizmo.BindController<GizmoController>();
            gizmo.X = ScreenWidth - 430;
            gizmo.Y = ScreenHeight - 230;
            this.Add(gizmo);

            Title = new UIGameTitle();
            Title.SetTitle("");
            this.Add(Title);
            
            this.Add(FSOFacade.MessageController);

            MessageTray = new UIMessageTray();
            MessageTray.X = ScreenWidth - 70;
            MessageTray.Y = 12;
            this.Add(MessageTray);

            WindowContainer = new UIContainer();
            Add(WindowContainer);

            PersonPage = new UIPersonPage();
            PersonPage.Visible = false;
            PersonPage.BindController<PersonPageController>();
            WindowContainer.Add(PersonPage);

            LotPage = new UILotPage();
            LotPage.Visible = false;
            LotPage.BindController<LotPageController>();
            WindowContainer.Add(LotPage);

            Bookmarks = new UIBookmarks();
            Bookmarks.Visible = false;
            Bookmarks.BindController<BookmarksController>();
            WindowContainer.Add(Bookmarks);

            Relationships = new UIRelationshipDialog();
            Relationships.Visible = false;
            Relationships.BindController<RelationshipDialogController>();
            WindowContainer.Add(Relationships);
        }

        public override void GameResized()
        {
            base.GameResized();
            Title.SetTitle(Title.Label.Caption);
            ucp.Y = ScreenHeight - 210;
            gizmo.X = ScreenWidth - 430;
            gizmo.Y = ScreenHeight - 230;
            MessageTray.X = ScreenWidth - 70;
            World?.GameResized();
            var oldPanel = ucp.CurrentPanel;
            ucp.SetPanel(-1);
            ucp.SetPanel(oldPanel);
            if (MouseHitAreaEventRef != null)
            {
                MouseHitAreaEventRef.Region = new Rectangle(0, 0, ScreenWidth, ScreenHeight);
            }
        }

        public void Initialize(string cityName, int cityMap, TerrainController terrainController)
        {
            Title.SetTitle(cityName);
            GameFacade.CurrentCityName = cityName;
            InitializeMap(cityMap);
            InitializeMouse();
            ZoomLevel = 5; //screen always starts at far zoom, city visible.
            CityRenderer.m_ZoomProgress = 0;

            JoinLotProgress = new UIJoinLotProgress();
            JoinLotProgress.BindController<JoinLotProgressController>();

            terrainController.Init(CityRenderer);
            CityRenderer.SetController(terrainController);
        }

        private void InitializeMap(int cityMap)
        {
            CityRenderer = new Terrain(GameFacade.GraphicsDevice); //The Terrain class implements the ThreeDAbstract interface so that it can be treated as a scene but manage its own drawing and updates.
            CityRenderer.m_GraphicsDevice = GameFacade.GraphicsDevice;
            CityRenderer.Initialize(cityMap);
            CityRenderer.LoadContent(GameFacade.GraphicsDevice);
            CityRenderer.RegenData = true;
            CityRenderer.SetTimeOfDay(0.5);
            GameFacade.Scenes.Add(CityRenderer);

            CityTooltip = new UICustomTooltip();
            Add(CityTooltip);
            CityTooltipHitArea = new UICustomTooltipContainer(CityTooltip);
            CityTooltipHitArea.SetSize(ScreenWidth, ScreenHeight);
            AddAt(0, CityTooltipHitArea);
        }

        private void InitializeMouse(){
            /** City Scene **/
            UIContainer mouseHitArea = new UIContainer();
            MouseHitAreaEventRef = mouseHitArea.ListenForMouse(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new UIMouseEvent(MouseHandler));
            AddAt(0, mouseHitArea);
        }

        public override void Update(FSO.Common.Rendering.Framework.Model.UpdateState state)
        {
            GameFacade.Game.IsFixedTimeStep = (vm == null || vm.Ready);

            base.Update(state);

            if (CityRenderer != null)
            {
                if (ZoomLevel > 3 && (CityRenderer.m_Zoomed == TerrainZoomMode.Near) != (ZoomLevel == 4)) ZoomLevel = (CityRenderer.m_Zoomed == TerrainZoomMode.Near) ? 4 : 5;

                if (World != null) {
                    if (CityRenderer.m_Zoomed == TerrainZoomMode.Lot)
                    {
                        if (World.FrameCounter < 3)
                        {
                            //wait until the draw stage has stabalized a bit. tends to be like this
                            // 1. heavy singular draw
                            // 2. update * 30
                            // 3. normal draws
                            CityRenderer.m_LotZoomProgress = 0;
                            World.Visible = true;
                            World.Opacity = 0;
                        }
                        else if (World.FrameCounter == 5 && GlobalSettings.Default.CompatState < GlobalSettings.TARGET_COMPAT_STATE)
                        {
                            GlobalSettings.Default.CompatState = GlobalSettings.TARGET_COMPAT_STATE;
                            GlobalSettings.Default.Save();
                        }
                        else
                            CityRenderer.InheritPosition(World, FindController<CoreGameScreenController>());
                    }
                    if (CityRenderer.m_LotZoomProgress > 0f && CityRenderer.m_LotZoomProgress < 1f)
                    {
                        if (CityRenderer.m_Zoomed == TerrainZoomMode.Lot)
                        {
                            if (CityRenderer.m_LotZoomProgress > 0.9995f)
                            {
                                CityRenderer.m_LotZoomProgress = 1f;
                                CityRenderer.Visible = false;
                            }
                        } else
                        {
                            if (CityRenderer.m_LotZoomProgress < 0.001f)
                            {
                                CityRenderer.m_LotZoomProgress = 0f;
                                World.Visible = false;
                            }
                        }
                        World.Opacity = Math.Max(0, (CityRenderer.m_LotZoomProgress - 0.5f) * 2);

                        var scale =
                            1/((CityRenderer.m_LotZoomProgress * (1/CityRenderer.m_LotZoomSize) + (1 - CityRenderer.m_LotZoomProgress) * (1/(Terrain.NEAR_ZOOM_SIZE*CityRenderer.m_WheelZoom))))
                            / CityRenderer.m_LotZoomSize;

                        World.State.PreciseZoom = scale;
                    }
                }

                if (InLot) //if we're in a lot, use the VM's more accurate time!
                    CityRenderer.SetTimeOfDay((vm.Context.Clock.Hours / 24.0) + (vm.Context.Clock.Minutes / 1440.0) + (vm.Context.Clock.Seconds / 86400.0));
                else
                {
                    var time = DateTime.UtcNow;
                    var tsoTime = TSOTime.FromUTC(time);
                    CityRenderer.SetTimeOfDay((tsoTime.Item1 / 24.0) + (tsoTime.Item2 / 1440.0) + (tsoTime.Item3 / 86400.0));
                }
            }

            lock (StateChanges)
            {
                while (StateChanges.Count > 0)
                {
                    var e = StateChanges.Dequeue();
                    ClientStateChangeProcess(e.State, e.Progress);
                }
            }

            if (vm != null) vm.Update();
        }

        public override void PreDraw(UISpriteBatch batch)
        {
            base.PreDraw(batch);
            vm?.PreDraw();
        }

        public void CleanupLastWorld()
        {
            if (vm == null) return;

            //clear our cache too, if the setting lets us do that
            TimedReferenceController.Clear();
            TimedReferenceController.Clear();
            VM.ClearAssembled();

            if (ZoomLevel < 4) ZoomLevel = 5;
            vm.Context.Ambience.Kill();
            foreach (var ent in vm.Entities) { //stop object sounds
                var threads = ent.SoundThreads;
                for (int i = 0; i < threads.Count; i++)
                {
                    threads[i].Sound.RemoveOwner(ent.ObjectID);
                }
                threads.Clear();
            }
            vm.CloseNet(VMCloseNetReason.LeaveLot);
            Driver.OnClientCommand -= VMSendCommand;
            GameFacade.Scenes.Remove(World);
            World.Dispose();
            LotControl.Dispose();
            this.Remove(LotControl);
            ucp.SetPanel(-1);
            ucp.SetInLot(false);
            vm.SuppressBHAVChanges();
            vm = null;
            World = null;
            Driver = null;
            LotControl = null;
        }

        public void InitiateLotSwitch()
        {
            vm?.SendCommand(new VMNetSimLeaveCmd());
        }

        public void ShowReconnectDialog(uint id)
        {
            var controller = FindController<CoreGameScreenController>();
            if (controller != null && SwitchLotDialog == null)
            {
                SwitchLotDialog = new UIAlert(new UIAlertOptions()
                {
                    Title = GameFacade.Strings.GetString("215", "1"),
                    Message = GameFacade.Strings.GetString("215", "2"),
                    Buttons = new UIAlertButton[]
                    {
                    new UIAlertButton(UIAlertButtonType.Yes, (btn) => {
                        controller.ReconnectLotID = id;
                        vm?.SendCommand(new VMNetSimLeaveCmd());
                        RemoveDialog(SwitchLotDialog); SwitchLotDialog = null; }),
                    new UIAlertButton(UIAlertButtonType.No, (btn) => { RemoveDialog(SwitchLotDialog); SwitchLotDialog = null; })
                    },
                });
                ShowDialog(SwitchLotDialog, true);
            }
        }

        private void VMSendCommand(byte[] data)
        {
            var controller = FindController<CoreGameScreenController>();

            if (controller != null)
            {
                controller.SendVMMessage(data);
            }
            //TODO: alternate controller for sandbox/standalone mode?
        }

        private void VMShutdown(VMCloseNetReason reason)
        {
            var controller = FindController<CoreGameScreenController>();

            if (controller != null)
            {
                controller.HandleVMShutdown(reason);
            }
        }

        public void ClientStateChange(int state, float progress)
        {
            lock (StateChanges) StateChanges.Enqueue(new SimConnectStateChange(state, progress));
        }

        public void ClientStateChangeProcess(int state, float progress)
        {     
            switch (state)
            {
                case 2:
                    JoinLotProgress.ProgressCaption = GameFacade.Strings.GetString("211", "27");
                    JoinLotProgress.Progress = 100f*(0.5f+progress*0.5f);
                    break;
                case 3:
                    GameFacade.Cursor.SetCursor(CursorType.Normal);
                    UIScreen.RemoveDialog(JoinLotProgress);
                    ZoomLevel = 1;
                    ucp.SetInLot(true);
                    break;
            }
        }

        public void InitializeLot()
        {
            CleanupLastWorld();

            World = new LotView.World(GameFacade.GraphicsDevice);
            World.Opacity = 0;
            GameFacade.Scenes.Add(World);
            Driver = new VMClientDriver(ClientStateChange);
            Driver.OnClientCommand += VMSendCommand;
            Driver.OnShutdown += VMShutdown;

            vm = new VM(new VMContext(World), Driver, new UIHeadlineRendererProvider());
            vm.ListenBHAVChanges();
            vm.Init();

            LotControl = new UILotControl(vm, World);
            this.AddAt(1, LotControl);

            var time = DateTime.UtcNow;
            var tsoTime = TSOTime.FromUTC(time);

            vm.Context.Clock.Hours = tsoTime.Item1;
            vm.Context.Clock.Minutes = tsoTime.Item2;
            if (m_ZoomLevel > 3)
            {
                World.Visible = false;
                LotControl.Visible = false;
            }

            ZoomLevel = Math.Max(ZoomLevel, 4);

            if (IDEHook.IDE != null) IDEHook.IDE.StartIDE(vm);

            vm.OnFullRefresh += VMRefreshed;
            vm.OnChatEvent += Vm_OnChatEvent;
            vm.OnEODMessage += LotControl.EODs.OnEODMessage;
            vm.OnRequestLotSwitch += VMLotSwitch;
            vm.OnGenericVMEvent += Vm_OnGenericVMEvent;
        }

        private void Vm_OnGenericVMEvent(VMEventType type, object data)
        {
            switch (type)
            {
                case VMEventType.TSOUnignore:
                    PersonPage.ToggleBookmark(Common.DataService.Model.BookmarkType.IGNORE_AVATAR, null, (uint)data);
                    break;
                case VMEventType.TSOTimeout:
                    var dialog = new UITimeOutDialog(vm, (int)data);
                    UIScreen.GlobalShowDialog(dialog, true);
                    var rnd = new Random();
                    dialog.Position = new Vector2(rnd.Next(ScreenWidth - 500), rnd.Next(ScreenHeight - 500));
                    break;
            }
        }

        private void VMLotSwitch(uint lotId)
        {
            FindController<CoreGameScreenController>()?.SwitchLot(lotId);
        }

        private void Vm_OnChatEvent(SimAntics.NetPlay.Model.VMChatEvent evt)
        {
            if (ZoomLevel < 4)
            {
                Title.SetTitle(LotControl.GetLotTitle());
            }
        }

        private void VMRefreshed()
        {
            if (vm == null) return;
            LotControl.ActiveEntity = null;
            LotControl.RefreshCut();
        }

        private void VMDebug_OnButtonClick(UIElement button)
        {
            /*
            if (vm == null) return;

            var debugTools = new Simantics(vm);

            var window = GameFacade.Game.Window;
            debugTools.Show();
            debugTools.Location = new System.Drawing.Point(window.ClientBounds.X + window.ClientBounds.Width, window.ClientBounds.Y);
            debugTools.UpdateAQLocation();
            */

        }

        private void SaveHouseButton_OnButtonClick(UIElement button)
        {
            if (vm == null) return;
            
            var exporter = new VMWorldExporter();
            exporter.SaveHouse(vm, GameFacade.GameFilePath("housedata/blueprints/house_00.xml"));
            var marshal = vm.Save();
            Directory.CreateDirectory(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/"));
            using (var output = new FileStream(Path.Combine(FSOEnvironment.UserDir, "LocalHouse/house_00.fsov"), FileMode.Create))
            {
                marshal.SerializeInto(new BinaryWriter(output));
            }
            if (vm.GlobalLink != null) ((VMTSOGlobalLinkStub)vm.GlobalLink).Database.Save();
        }

        public void CloseInbox()
        {
            this.Remove(Inbox);
            Inbox = null;
        }

        public void OpenInbox()
        {
            if (Inbox == null)
            {
                Inbox = new UIInbox();
                this.Add(Inbox);
                Inbox.X = GlobalSettings.Default.GraphicsWidth / 2 - 332;
                Inbox.Y = GlobalSettings.Default.GraphicsHeight / 2 - 184;
            }
            //todo, on already visible move to front
        }

        private void MouseHandler(UIMouseEventType type, UpdateState state)
        {
            if (CityRenderer != null) CityRenderer.UIMouseEvent(type.ToString()); //all the city renderer needs are events telling it if the mouse is over it or not.
            //if the mouse is over it, the city renderer will handle the rest.
        }
    }

    public class SimConnectStateChange
    {
        public int State;
        public float Progress;
        public SimConnectStateChange(int state, float progress)
        {
            State = state; Progress = progress;
        }
    }
}
