using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics;
using SharpDX.XInput;
using WeScriptWrapper;
using WeScript.SDK.UI;
using WeScript.SDK.UI.Components;

namespace ApexLegends
{
    class Program
    {
        public static double dims = 0.01905f;
        public static float M_PI_F = (180.0f / Convert.ToSingle(System.Math.PI));
        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static Vector2 GameCenterPos = new Vector2(0, 0); //for crosshair and aim
        public static Vector2 AimTarg2D = new Vector2(0, 0); //for aimbot
        public static Vector3 AimTarg3D = new Vector3(0, 0, 0);
        public static IntPtr GameBase = IntPtr.Zero;
        public static IntPtr GameSize = IntPtr.Zero;
        public static IntPtr EntityListPtr = IntPtr.Zero;
        public static IntPtr LocalPlayerPtr = IntPtr.Zero;
        public static IntPtr ViewRenderPtr = IntPtr.Zero;
        public static IntPtr ViewMatrixOffs = IntPtr.Zero;

        public static int WM_KEYDOWN = 0x0100;
        public static int WM_KEYUP = 0x0101;
        public static int mySecondsBefore = 0;
        public static bool shouldpostmsg = false;

        public static uint Velocity = 0x140; //vec3
        public static uint Origin = 0x14C; //vec3
        public static uint Shield = 0x170; //int
        public static uint MaxShield = 0x174; //int
        public static uint Health = 0x3E0; //int
        public static uint Team = 0x3F0; //int
        public static uint BoundingBox = 0x474; //vec3
        public static uint MaxHealth = 0x510; //int
        public static uint BoneClass = 0xED8; //ptr

        public static uint m_latestPrimaryWeapons = 0x1934; //int
        public static uint BulletSpeed = 0x1D48; //float

        public static uint CameraPosition = 0x1DA4;
        public static uint CameraAngles = 0x1DB0;
        //public static uint AimPunch = 0x2300;
        public static uint AnglesStatic = 0x23B8;
        public static uint ViewAngles = 0x23C8;
        //public static uint BleedOutState = 0x2590; //0 = alive; 2 = downed



        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }
        public static Menu AimbotMenu { get; private set; }
        //public static Menu MiscMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuSlider ESPRendDist = new MenuSlider("menurenddist", "ESP Render Distance", 240, 20, 500);
                public static readonly MenuColor EnemiesColor = new MenuColor("enemycolor", "Enemies ESP Color", new SharpDX.Color(255, 0, 0));
                public static readonly MenuBool DrawBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuBool DrawBoxHP = new MenuBool("drawboxhp", "Draw Health", true);
                public static readonly MenuBool DrawBoxAR = new MenuBool("drawboxar", "Draw Armor", true);
                public static readonly MenuSliderBool DrawTextSize = new MenuSliderBool("drawtextsize", "Text Size", false, 14, 4, 72);
                public static readonly MenuBool DrawTextDist = new MenuBool("drawtextdist", "Draw Distance", true);
                public static readonly MenuBool DrawTimeLeft = new MenuBool("drawtimeleft", "Draw Time Left before EAC Kicks", true);
            }
            public static class AimbotComponent
            {
                public static readonly MenuBool AimGlobalBool = new MenuBool("enableaim", "Enable Aimbot Features", true);
                public static readonly MenuKeyBind AimKey = new MenuKeyBind("aimkey", "Aimbot HotKey (HOLD)", VirtualKeyCode.CapsLock, KeybindType.Hold, false);
                public static readonly MenuList AimType = new MenuList("aimtype", "Aimbot Type", new List<string>() { "Direct Engine ViewAngles", "Real Mouse Movement" }, 0);
                public static readonly MenuList AimSpot = new MenuList("aimspot", "Aimbot Spot", new List<string>() { "Aim at their Head", "Aim at their Body" }, 0);
                public static readonly MenuSlider AimSpeed = new MenuSlider("aimspeed", "Aimbot Speed %", 12, 1, 100);
                public static readonly MenuBool DrawAimSpot = new MenuBool("drawaimspot", "Draw Aimbot Spot", true);
                public static readonly MenuBool DrawAimTarget = new MenuBool("drawaimtarget", "Draw Aimbot Current Target", true);
                public static readonly MenuColor AimTargetColor = new MenuColor("aimtargetcolor", "Target Color", new SharpDX.Color(0x1F, 0xBE, 0xD6, 255));
                public static readonly MenuBool DrawAimFov = new MenuBool("drawaimfov", "Draw Aimbot FOV Circle", true);
                public static readonly MenuColor AimFovColor = new MenuColor("aimfovcolor", "FOV Color", new SharpDX.Color(255, 255, 255, 60));
                public static readonly MenuSlider AimFov = new MenuSlider("aimfov", "Aimbot FOV", 100, 4, 1000);
            }
            //public static class MiscComponent
            //{
            //    public static readonly MenuBool SupportInChat = new MenuBool("supportinchat", "Support WeScript.app by promoting it in chat to your teammates?", true);
            //}
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.EnemiesColor,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawBoxHP,
                Components.VisualsComponent.DrawTextSize,
                Components.VisualsComponent.DrawTextDist,
                Components.VisualsComponent.DrawTimeLeft,
            };

            AimbotMenu = new Menu("aimbotmenu", "Aimbot Menu")
            {
                Components.AimbotComponent.AimGlobalBool,
                Components.AimbotComponent.AimKey,
                Components.AimbotComponent.AimType,
                Components.AimbotComponent.AimSpot,
                Components.AimbotComponent.AimSpeed,
                Components.AimbotComponent.DrawAimSpot,
                Components.AimbotComponent.DrawAimTarget,
                Components.AimbotComponent.DrawAimFov,
                Components.AimbotComponent.AimFovColor,
                Components.AimbotComponent.AimFov,
            };

            //MiscMenu = new Menu("miscmenu", "Misc Menu")
            //{
            //    Components.MiscComponent.SupportInChat,
            //};

            RootMenu = new Menu("apexlegendsexample", "WeScript.app Apex Legends Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
                AimbotMenu,
                //MiscMenu,
            };
            RootMenu.Attach();
        }


        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app ApexLegends Example Assembly 2.0 Loaded (With Reconnect for free)!");
            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }



        private static IntPtr GetEntityByIndex(IntPtr processHandle, uint index)
        {
            if ((index < 0) || (index > 0x10000))
            {
                return IntPtr.Zero;
            }
            //var entity_list = Memory.ReadPointer(processHandle, EntityListPtr, isWow64Process);
            var entity_list = EntityListPtr;
            if (entity_list != IntPtr.Zero)
            {
                var base_entity = Memory.ReadDWORD64(processHandle, entity_list);
                if (base_entity == 0)
                {
                    return IntPtr.Zero;
                }
                var entity_itself = Memory.ReadPointer(processHandle, (IntPtr)(entity_list.ToInt64() + (index << 5)), isWow64Process);
                return entity_itself;
            }
            return IntPtr.Zero;
        }


        private static double GetDistance3D(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 vector = new Vector3(myPos.X - enemyPos.X, myPos.Y - enemyPos.Y, myPos.Z - enemyPos.Z);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z) * dims;
        }

        private static double GetDistance2D(Vector2 pos1, Vector2 pos2)
        {
            Vector2 vector = new Vector2(pos1.X - pos2.X, pos1.Y - pos2.Y);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }

        public static float ProjectileDrop(float ProjectileSpeed, float m_gravity, float Dist)
        {
            if (Dist < 0.001)
                return 0;
            float m_time = Dist / ProjectileSpeed;
            return 0.5f * Math.Abs(m_gravity) * m_time * m_time;
        }

        public static Vector3 GetPrediction(Vector3 MyPos, Vector3 TargetPos, Vector3 TargetVelocity, float ProjectileSpeed, float m_gravity)
        {
            float Distance;
            Distance = (float)GetDistance3D(MyPos, TargetPos);
            float flTime = (Distance / ProjectileSpeed);
            Vector3 PredictedPos = new Vector3(0, 0, 0);
            PredictedPos.X = TargetPos.X + (TargetVelocity.X * flTime);
            PredictedPos.Y = TargetPos.Y + (TargetVelocity.Y * flTime);
            if (TargetVelocity.Z != 0)
            {
                PredictedPos.Z = TargetPos.Z + 0.5f * m_gravity * (float)Math.Pow(flTime, 2) + (TargetVelocity.Z * flTime);
            }
            else
            {
                PredictedPos.Z = TargetPos.Z;
            }
            PredictedPos.Z += ProjectileDrop(ProjectileSpeed, m_gravity, Distance);
            return PredictedPos;
        }


        private static Vector3 ReadBonePos(IntPtr playerPtr, int boneIDX)
        {
            Vector3 targetVec = new Vector3(0, 0, 0);
            var BoneMatrixPtr = Memory.ReadPointer(processHandle, (IntPtr)(playerPtr.ToInt64() + BoneClass), isWow64Process);
            if (BoneMatrixPtr != IntPtr.Zero)
            {
                targetVec.X = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x0C));
                targetVec.Y = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x1C));
                targetVec.Z = Memory.ReadFloat(processHandle, (IntPtr)(BoneMatrixPtr.ToInt64() + 0x30 * boneIDX + 0x2C));
            }
            return targetVec;
        }


        public static Vector3 ClampAngle(Vector3 angle)
        {
            while (angle.Y > 180) angle.Y -= 360;
            while (angle.Y < -180) angle.Y += 360;

            if (angle.X > 89.0f) angle.X = 89.0f;
            if (angle.X < -89.0f) angle.X = -89.0f;

            angle.Z = 0f;

            return angle;
        }

        public static Vector3 NormalizeAngle(Vector3 angle)
        {
            while (angle.X < -180.0f) angle.X += 360.0f;
            while (angle.X > 180.0f) angle.X -= 360.0f;

            while (angle.Y < -180.0f) angle.Y += 360.0f;
            while (angle.Y > 180.0f) angle.Y -= 360.0f;

            while (angle.Z < -180.0f) angle.Z += 360.0f;
            while (angle.Z > 180.0f) angle.Z -= 360.0f;

            return angle;
        }

        public static Vector3 CalcAngle(Vector3 camPosition, Vector3 enemyPosition, Vector3 aimPunch, float yawRecoilReductionFactory, float pitchRecoilReductionFactor)
        {
            Vector3 delta = new Vector3(camPosition.X - enemyPosition.X, camPosition.Y - enemyPosition.Y, camPosition.Z - enemyPosition.Z);

            Vector3 tmp = Vector3.Zero;
            tmp.X = Convert.ToSingle(System.Math.Atan(delta.Z / System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y))) * 57.295779513082f - aimPunch.X * yawRecoilReductionFactory;
            tmp.Y = Convert.ToSingle(System.Math.Atan(delta.Y / delta.X)) * M_PI_F - aimPunch.Y * pitchRecoilReductionFactor;
            tmp.Z = 0;

            if (delta.X >= 0.0) tmp.Y += 180f;

            tmp = NormalizeAngle(tmp);
            tmp = ClampAngle(tmp);

            return tmp;
        }


        //public static void SendChatMessageToTeam(IntPtr gameWindow)
        //{
        //    //ignore this ghetto implementation, it's TERRIBLE
        //    Input.SendMessageWS(gameWindow, WM_KEYDOWN, (int)VirtualKeyCode.Enter, (IntPtr)(Input.MapVirtualKeyWS((uint)VirtualKeyCode.Enter, 0) << 16)); //keydown U
        //    Input.SendMessageWS(gameWindow, WM_KEYUP, (int)VirtualKeyCode.Enter, (IntPtr)(Input.MapVirtualKeyWS((uint)VirtualKeyCode.Enter, 0) << 16)); //keyup U
        //    Input.SleepWS(100);
        //    Input.KeyPress(VirtualKeyCode.S);
        //    Input.KeyPress(VirtualKeyCode.E);
        //    Input.KeyPress(VirtualKeyCode.A);
        //    Input.KeyPress(VirtualKeyCode.R);
        //    Input.KeyPress(VirtualKeyCode.C);
        //    Input.KeyPress(VirtualKeyCode.H);
        //    Input.KeyPress(VirtualKeyCode.Space);
        //    Input.KeyPress(VirtualKeyCode.F);
        //    Input.KeyPress(VirtualKeyCode.O);
        //    Input.KeyPress(VirtualKeyCode.R);
        //    Input.KeyPress(VirtualKeyCode.Space);
        //    Input.KeyPress(VirtualKeyCode.W);
        //    Input.KeyPress(VirtualKeyCode.E);
        //    Input.KeyPress(VirtualKeyCode.S);
        //    Input.KeyPress(VirtualKeyCode.C);
        //    Input.KeyPress(VirtualKeyCode.R);
        //    Input.KeyPress(VirtualKeyCode.I);
        //    Input.KeyPress(VirtualKeyCode.P);
        //    Input.KeyPress(VirtualKeyCode.T);
        //    Input.KeyPress(VirtualKeyCode.Space);
        //    Input.KeyPress(VirtualKeyCode.A);
        //    Input.KeyPress(VirtualKeyCode.P);
        //    Input.KeyPress(VirtualKeyCode.P);
        //    Input.SleepWS(33);
        //    Input.KeyPress(VirtualKeyCode.Enter);
        //}

        public static void SendMessageToOrigin()
        {
            var originPID = Memory.GetPIDForProcess("Origin.exe");
            if (originPID > 0)
            {
                IntPtr originWindow = Memory.FindMainWindow(originPID);
                //Console.WriteLine($"OriginWindow: {originWindow.ToString()}");
                Input.SetFocusWS(originWindow);
                Input.SetForegroundWindowWS(originWindow);
                Input.KeyDown(VirtualKeyCode.Alt);
                Input.KeyPress(VirtualKeyCode.O);
                Input.KeyPress(VirtualKeyCode.G);
                Input.KeyUp(VirtualKeyCode.Alt);
                Input.SleepWS(500);
                Input.SetFocusWS(originWindow);
                Input.SetForegroundWindowWS(originWindow);
                Input.KeyDown(VirtualKeyCode.Alt);
                Input.KeyPress(VirtualKeyCode.O);
                Input.KeyPress(VirtualKeyCode.G);
                Input.KeyUp(VirtualKeyCode.Alt);
                Input.KeyPress(VirtualKeyCode.Alt); //to unstuck the key ... if possible
                Input.SleepWS(500);
                IntPtr gameWindowz = Memory.FindWindowClassName("Respawn001");
                Input.SetFocusWS(gameWindowz);
                Input.SetForegroundWindowWS(gameWindowz);

            }

        }


        private static void OnTick(int counter, EventArgs args)
        {


            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowClassName("Respawn001"); //classname
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    //Console.WriteLine("weheree");
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.OpenProcess(PROCESS_ALL_ACCESS, calcPid); //the driver will get a stripped handle, but doesn't matter, it's still OK
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle);
                        }
                        else
                        {
                            //Console.WriteLine("failed to get handle");
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowClassName("Respawn001"); //classname
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();
                    GameCenterPos = new Vector2(wndSize.X / 2 + wndMargins.X, wndSize.Y / 2 + wndMargins.Y); //even if the game is windowed, calculate perfectly it's "center" for aim or crosshair

                    if (GameBase == IntPtr.Zero) //do we have access to Gamebase address?
                    {
                        GameBase = Memory.GetModule(processHandle, null, isWow64Process); //if not, find it
                        //Console.WriteLine($"GameBase: {GameBase.ToString("X")}");
                    }
                    else
                    {
                        if (GameSize == IntPtr.Zero)
                        {
                            GameSize = Memory.GetModuleSize(processHandle, null, isWow64Process);
                        }
                        else
                        {
                            //Console.WriteLine($"GameBase: {GameBase.ToString("X")}"); //easy way to check if we got reading rights
                            //Console.WriteLine($"GameSize: {GameSize.ToString("X")}"); //easy way to check if we got reading rights
                            if (EntityListPtr == IntPtr.Zero)
                            {
                                EntityListPtr = EntityListPtr = Memory.FindSignature(processHandle, GameBase, GameSize, "0F B7 C8 48 8D 05 ? ? ? ? 48 C1 E1 05 48 03 C8", 0x6); //(IntPtr)(GameBase.ToInt64() + 0x175EC28);//
                            }
                            if (LocalPlayerPtr == IntPtr.Zero)
                            {
                                LocalPlayerPtr = LocalPlayerPtr = Memory.FindSignature(processHandle, GameBase, GameSize, "48 8B 05 ? ? ? ? 48 0F 44 C7 48 89 05", 0x3); //(IntPtr)(GameBase.ToInt64() + 0x1B0D448);//
                            }
                            if (ViewRenderPtr == IntPtr.Zero)
                            {
                                ViewRenderPtr = ViewRenderPtr = Memory.FindSignature(processHandle, GameBase, GameSize, "48 8B 0D ? ? ? ? 44 0F 28 C2", 0x3); //(IntPtr)(GameBase.ToInt64() + 0x3F5C2C0);//
                            }
                            if (ViewMatrixOffs == IntPtr.Zero)
                            {
                                ViewMatrixOffs = Memory.FindSignature(processHandle, GameBase, GameSize, "48 89 AB ? ? ? ? 4C 89 9B", 0x3, true); //(IntPtr)0x1B3BD0;
                            }

                            //Console.WriteLine($"EntityListPtr: {EntityListPtr.ToString("X")}");
                            //Console.WriteLine($"LocalPlayerIDPtr: {LocalPlayerIDPtr.ToString("X")}");
                            //Console.WriteLine($"ViewRenderPtr: {ViewRenderPtr.ToString("X")}");

                            //if (Components.MiscComponent.SupportInChat.Enabled)
                            //{
                                if (isGameOnTop)
                                {
                                    if (shouldpostmsg)
                                    {
                                        shouldpostmsg = false;
                                        SendMessageToOrigin();
                                    }
                                }
                            //}

                        }
                    }
                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                    //clear your offsets, modules
                    GameBase = IntPtr.Zero;
                    GameSize = IntPtr.Zero;
                    EntityListPtr = IntPtr.Zero;
                    LocalPlayerPtr = IntPtr.Zero;
                    ViewRenderPtr = IntPtr.Zero;
                    ViewMatrixOffs = IntPtr.Zero;
                }
            }
        }

        public static ulong timeWithLP = 0;
        public static ulong timeWithoutLP = 0;
        public static ulong timeToPlayWithoutDC = 87000; //about minute and half, with 3 seconds to reconnect

        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off


            if ((timeWithLP > 0) && (timeWithLP < timeToPlayWithoutDC))
            {
                var secondsLeft = (timeToPlayWithoutDC - timeWithLP) / 1000;

                //if (Components.MiscComponent.SupportInChat.Enabled)
                {
                    if (secondsLeft == 5)
                    {
                        if (mySecondsBefore == 0)
                        {
                            mySecondsBefore = 5;
                            shouldpostmsg = true;
                        }
                    }
                    else
                    {
                        mySecondsBefore = 0;
                    }
                }


                if (Components.VisualsComponent.DrawTimeLeft.Enabled)
                {
                    if (secondsLeft < 15)
                    {
                        Renderer.DrawText($"!! {secondsLeft.ToString()} !!", GameCenterPos.X, GameCenterPos.Y + (GameCenterPos.Y / 2), new Color(255, 0, 0), 72, TextAlignment.centered);
                    }
                    else
                    {
                        Renderer.DrawText(secondsLeft.ToString(), GameCenterPos.X, GameCenterPos.Y + (GameCenterPos.Y / 2), new Color(255, 255, 255), 40, TextAlignment.centered);
                    }
                }
            }

            double fClosestPos = 999999;
            AimTarg2D = new Vector2(0, 0);
            AimTarg3D = new Vector3(0, 0, 0);

            if ((ViewRenderPtr != IntPtr.Zero) && (ViewMatrixOffs != IntPtr.Zero))
            {
                var matPtr0 = Memory.ReadPointer(processHandle, (IntPtr)(ViewRenderPtr.ToInt64()), isWow64Process);
                if (matPtr0 != IntPtr.Zero)
                {
                    var matptr1 = Memory.ReadPointer(processHandle, (IntPtr)(matPtr0.ToInt64() + ViewMatrixOffs.ToInt64()), isWow64Process);
                    if (matptr1 != IntPtr.Zero)
                    {
                        //Console.WriteLine($"{matptr1.ToString("X")}");
                        var matrix = Memory.ReadMatrix(processHandle, matptr1);
                        var localPlayer = Memory.ReadPointer(processHandle, LocalPlayerPtr, isWow64Process);
                        //Console.WriteLine($"{localPlayer.ToString("X")}");
                        if (localPlayer != IntPtr.Zero)
                        {
                            
                            timeWithLP = Memory.TickCount - timeWithoutLP;

                            var myCameraPos = Memory.ReadVector3(processHandle, (IntPtr)(localPlayer.ToInt64() + CameraPosition));
                            var StaticAngles = Memory.ReadVector3(processHandle, (IntPtr)(localPlayer.ToInt64() + AnglesStatic));
                            var WritableAngles = Memory.ReadVector3(processHandle, (IntPtr)(localPlayer.ToInt64() + ViewAngles));
                            var myPos = Memory.ReadVector3(processHandle, (IntPtr)(localPlayer.ToInt64() + Origin));
                            var myTeam = Memory.ReadInt32(processHandle, (IntPtr)(localPlayer.ToInt64() + Team));
                            //var myHP = Memory.ReadInt32(processHandle, (IntPtr)(localPlayer.ToInt64() + Health));
                            var wepHnd = Memory.ReadUInt32(processHandle, (IntPtr)(localPlayer.ToInt64() + m_latestPrimaryWeapons));
                            var weaponIndex = wepHnd & 0xFFFF;
                            var weaponPtr = GetEntityByIndex(processHandle, weaponIndex);
                            float bulletSpeed = 999999999.0f;
                            if (weaponPtr != IntPtr.Zero)
                            {
                                bulletSpeed = Memory.ReadFloat(processHandle, (IntPtr)(weaponPtr.ToInt64() + BulletSpeed));
                            }
                            for (uint i = 0; i <= 60; i++)
                            {
                                var entity = GetEntityByIndex(processHandle, i);
                                //Console.WriteLine($"{entity.ToString("X")}");
                                if ((entity != IntPtr.Zero) && (localPlayer != entity))
                                {
                                    //Console.WriteLine($"{entity.ToString("X")}");
                                    var entTeam = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + Team));
                                    if (entTeam == myTeam) continue;
                                    var entHP = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + Health));
                                    var entHPMAX = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + MaxHealth));
                                    if ((entHP > 0) && (entHPMAX > 0))
                                    {
                                        var entPos = Memory.ReadVector3(processHandle, (IntPtr)(entity.ToInt64() + Origin));
                                        var dist = GetDistance3D(myPos, entPos);
                                        if (dist > Components.VisualsComponent.ESPRendDist.Value) continue;

                                        Vector2 vScreen_feet = new Vector2(0, 0);
                                        Vector2 vScreen_head = new Vector2(0, 0);
                                        if (Renderer.WorldToScreen(entPos, out vScreen_feet, matrix, wndMargins, wndSize, W2SType.TypeD3D9))
                                        {
                                            var entShield = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + Shield));
                                            var entShieldMax = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + MaxShield));
                                            //var bleedOutState = Memory.ReadInt32(processHandle, (IntPtr)(entity.ToInt64() + BleedOutState));
                                            var boundingBox = Memory.ReadVector3(processHandle, (IntPtr)(entity.ToInt64() + BoundingBox));
                                            var entVelocity = Memory.ReadVector3(processHandle, (IntPtr)(entity.ToInt64() + Velocity));
                                            var ent_bone = ReadBonePos(entity, 12);//bone for head
                                            var ent_HeadPosBOX = new Vector3(entPos.X + ent_bone.X, entPos.Y + ent_bone.Y, entPos.Z + ent_bone.Z + 2.0f);
                                            Renderer.WorldToScreen(ent_HeadPosBOX, out vScreen_head, matrix, wndMargins, wndSize, W2SType.TypeD3D9);

                                            string dist_str = "";
                                            if (Components.VisualsComponent.DrawTextDist.Enabled)
                                            {
                                                dist_str = $"[{dist.ToString("0.0")}]"; //only 1 demical number after the dot
                                            }
                                            if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                            {
                                                Renderer.DrawFPSBox(vScreen_head, vScreen_feet, Components.VisualsComponent.EnemiesColor.Color, (boundingBox.Z < 55.0f ? BoxStance.crouching : BoxStance.standing), Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, entHP, Components.VisualsComponent.DrawBoxHP.Enabled ? entHPMAX : 0, entShield, Components.VisualsComponent.DrawBoxAR.Enabled ? entShieldMax : 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, string.Empty, string.Empty, string.Empty, string.Empty);
                                            }

                                            if (Components.AimbotComponent.AimGlobalBool.Enabled)
                                            {
                                                Vector3 targetVec = new Vector3(0, 0, 0);
                                                switch (Components.AimbotComponent.AimSpot.Value)
                                                {
                                                    case 0: //head
                                                        {
                                                            var tmp = ReadBonePos(entity, 12);
                                                            targetVec = new Vector3(entPos.X + tmp.X, entPos.Y + tmp.Y, entPos.Z + tmp.Z);
                                                        }
                                                        break;
                                                    case 1: //body
                                                        {
                                                            var tmp = ReadBonePos(entity, 5);
                                                            targetVec = new Vector3(entPos.X + tmp.X, entPos.Y + tmp.Y, entPos.Z + tmp.Z);
                                                        }
                                                        break;
                                                    default: //ignore default case, should never occur
                                                        break;
                                                }
                                                Vector2 vScreen_aim = new Vector2(0, 0);
                                                if (Renderer.WorldToScreen(targetVec, out vScreen_aim, matrix, wndMargins, wndSize, W2SType.TypeD3D9)) //our aimpoint is on screen
                                                {
                                                    if (Components.AimbotComponent.DrawAimSpot.Enabled)
                                                    {
                                                        Renderer.DrawFilledRect(vScreen_aim.X - 1, vScreen_aim.Y - 1, 2, 2, new Color(255, 255, 255)); //lazy to implement aimspotcolor
                                                    }

                                                    var PredictedPos = GetPrediction(myPos, targetVec, entVelocity, bulletSpeed * 0.01905f, 750.0f);
                                                    var PredPosScreen = new Vector2(0, 0);
                                                    if (Renderer.WorldToScreen(PredictedPos, out PredPosScreen, matrix, wndMargins, wndSize, W2SType.TypeD3D9))
                                                    {
                                                        var AimDist2D = GetDistance2D(PredPosScreen, GameCenterPos);
                                                        if (Components.AimbotComponent.AimFov.Value < AimDist2D) continue; //ignore anything outside our fov
                                                        if (AimDist2D < fClosestPos)
                                                        {
                                                            fClosestPos = AimDist2D;
                                                            AimTarg2D = PredPosScreen;
                                                            AimTarg3D = PredictedPos;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (Components.AimbotComponent.AimGlobalBool.Enabled)
                            {
                                if (Components.AimbotComponent.DrawAimFov.Enabled) //draw fov circle
                                {
                                    Renderer.DrawCircle(GameCenterPos, Components.AimbotComponent.AimFov.Value, Components.AimbotComponent.AimFovColor.Color);
                                }

                                if ((AimTarg2D.X != 0) && (AimTarg2D.Y != 0))//check just in case if we have aimtarg
                                {
                                    if (Components.AimbotComponent.DrawAimTarget.Enabled) //draw aim target
                                    {
                                        Renderer.DrawRect(AimTarg2D.X - 3, AimTarg2D.Y - 3, 6, 6, Components.AimbotComponent.AimTargetColor.Color);
                                    }
                                    if (Components.AimbotComponent.AimKey.Enabled)
                                    {
                                        switch (Components.AimbotComponent.AimType.Value)
                                        {
                                            case 0: //engine viewangles
                                                {
                                                    var Breath = new Vector3(0, 0, 0);
                                                    Breath = StaticAngles - WritableAngles;
                                                    var newAng = CalcAngle(myCameraPos, AimTarg3D, Breath, 1.0f, 1.0f);
                                                    if (Components.AimbotComponent.AimSpeed.Value < 100) //smoothing only below 100%
                                                    {
                                                        float aimsmooth_ = Components.AimbotComponent.AimSpeed.Value * 0.01f;
                                                        var diff = newAng - WritableAngles;
                                                        diff = NormalizeAngle(diff);
                                                        diff = ClampAngle(diff);
                                                        if (diff.X > aimsmooth_)
                                                        {
                                                            newAng.X = WritableAngles.X + aimsmooth_;
                                                        }
                                                        if (diff.X < -aimsmooth_)
                                                        {
                                                            newAng.X = WritableAngles.X - aimsmooth_;
                                                        }
                                                        if (diff.Y > aimsmooth_)
                                                        {
                                                            newAng.Y = WritableAngles.Y + aimsmooth_;
                                                        }
                                                        if (diff.Y < -aimsmooth_)
                                                        {
                                                            newAng.Y = WritableAngles.Y - aimsmooth_;
                                                        }
                                                        newAng = ClampAngle(newAng); //just in case?
                                                    }
                                                    Memory.WriteVector3(processHandle, (IntPtr)(localPlayer.ToInt64() + ViewAngles), newAng); //rip ring0 version
                                                }
                                                break;
                                            case 1: //mouse event
                                                {

                                                    double DistX = 0;
                                                    double DistY = 0;
                                                    DistX = (AimTarg2D.X) - GameCenterPos.X;
                                                    DistY = (AimTarg2D.Y) - GameCenterPos.Y;
                                                    double slowDistX = DistX / (1.0f + (Math.Abs(DistX) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    double slowDistY = DistY / (1.0f + (Math.Abs(DistY) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    Input.mouse_eventWS(MouseEventFlags.MOVE, (int)slowDistX, (int)slowDistY, MouseEventDataXButtons.NONE, IntPtr.Zero);
                                                }
                                                break;
                                            default: //ignore default case, should never occur
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            timeWithoutLP = Memory.TickCount;
                        }
                    }
                }
            }
        }
    }
}
