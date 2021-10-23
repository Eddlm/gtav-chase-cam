using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.Native;
namespace RaceCam
{
    public class RaceCam : Script
    {


        public List<Vector3> AccelerationVector = new List<Vector3>();
        public List<Vector3> LocalAccelerationVector = new List<Vector3>();
        public List<Vector3> PositionsAvg = new List<Vector3>();
        Vector3 lSpeed = Vector3.Zero;
        Vector3 lLocalSpeed = Vector3.Zero;


        Camera nfsCam = null;
        float spdCorrection = 0.1f;
        float baseFarBack = 8;
        float baseFarUp = 5;

        float GsFactor = 0.1f;
        float GsAverage = 50;
        float positionTightness = 1;
        float aimTightness = 1;
        int mode = 0;
        Vector3 properPosition = Vector3.Zero;
        float AimModelVsDir = 0f;
        float PosModelVsDir = 0f;


        bool AlreadyShilled = false;
        Vector3 GsClamp = new Vector3(1, 1, 1f);
        Vector3 directionClamp = new Vector3(1, 1, 1f);
        public RaceCam()
        {

            Tick += OnTick;
            Aborted += OnAbort;

            Interval = 0;
        }
        void OnTick(object sender, EventArgs e)
        {
            HandleCam();
        }
        void OnAbort(object sender, EventArgs e)
        {
            DeleteCam();
        }

        void GetChaseCamModeData(int mode, Vehicle Car)
        {

            switch (mode)
            {
                case 0:
                    {

                        baseFarBack = 1.5f;
                        baseFarUp = (Car.Model.GetDimensions().Z / 2) + 1;

                        GsFactor = 1.5f;
                        GsAverage = 10;



                        positionTightness = 0.1f;

                        aimTightness = 0.1f;

                        AimModelVsDir = 1f;
                        PosModelVsDir = 0f;

                        GsClamp = new Vector3(5, 5, 5);
                        directionClamp = new Vector3(100, 100, 100);
                        break;
                    }
                case 1:
                    {
                        baseFarBack = 3;
                        baseFarUp = (Car.Model.GetDimensions().Z / 2) + 1;
                        break;
                    }

            }
        }
        enum CamMode
        {
            Normal, Far//, LockAimAndModel,Balanced, BalancedNoVertical, BalancedNoHorizontal,BalancedNoGs
        }
        void DeleteCam()
        {

            World.RenderingCamera = null;
            if (nfsCam != null) { nfsCam.Destroy(); nfsCam = null; }

        }
        int ChangeCamPresed = 0;
        int GameTimeRef = 0;
        int CamTimeRef = 0;
        bool ChaseCamOn = false;
        void HandleCam()
        {
            if (GameTimeRef < Game.GameTime)
            {
                GameTimeRef = Game.GameTime + 200;
                if (Game.IsControlPressed(2, Control.NextCamera)) ChangeCamPresed++; else ChangeCamPresed = 0;
                if (ChangeCamPresed > 5) { ChangeCamPresed = 0; ChaseCamOn = !ChaseCamOn; }


            }
            Vehicle Car = Game.Player.Character.CurrentVehicle;
            if (Car == null && ChaseCamOn) { UI.Notify("ChaseCam ato disabled: not in a car"); ChaseCamOn = false; }

            if (ChaseCamOn)
            {
                if (nfsCam == null)
                {
                    nfsCam = World.CreateCamera(GameplayCamera.Position, Vector3.Zero, GameplayCamera.FieldOfView);
                    World.RenderingCamera = nfsCam;


                    if (!AlreadyShilled) { AlreadyShilled = true; UI.Notify("~g~[Eddlm]~w~ ChaseCam proof of concept~n~Unfinished AF"); }
                    GetChaseCamModeData(mode, Car);
                    UI.Notify("Chase cam ~g~on");
                }
            }
            else
            {
                if (nfsCam != null)
                {
                    DeleteCam();
                    UI.Notify("Chase cam ~b~off");

                }
            }
            if (ChaseCamOn && nfsCam != null)
            {


                if (Game.IsControlJustPressed(2, GTA.Control.NextCamera))
                {

                    mode++;
                    if (mode >= Enum.GetValues(typeof(CamMode)).Length) mode = 0;
                    GetChaseCamModeData(mode, Car);
                    UI.ShowSubtitle("Current mode: ~b~" + ((CamMode)mode).ToString(), 800);
                }

                PositionsAvg.Add(Car.Position);
                if (PositionsAvg.Count > 2) PositionsAvg.RemoveAt(0);

                properPosition = PositionsAvg.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)PositionsAvg.Count;

                Vector3 cSpeed = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, Car, false);
                Vector3 spdLocal = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, Car, true);

                AccelerationVector.Add((cSpeed - lSpeed));
                lSpeed = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, Car, false);

                LocalAccelerationVector.Add(spdLocal - lLocalSpeed);
                lLocalSpeed = Function.Call<Vector3>(Hash.GET_ENTITY_SPEED_VECTOR, Car, true);


                if (AccelerationVector.Count > GsAverage) AccelerationVector.RemoveAt(0);
                if (LocalAccelerationVector.Count > GsAverage) LocalAccelerationVector.RemoveAt(0);
                Vector3 avgGs = AccelerationVector.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)AccelerationVector.Count;

                Vector3 avgLocalGs = LocalAccelerationVector.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)AccelerationVector.Count;


                Vector3 Gs = Vector3.Clamp(avgGs, -GsClamp, GsClamp);
                Vector3 dirGs = Vector3.Clamp(avgGs, -new Vector3(100, 100, 0), new Vector3(100, 100, 0));
                Vector3 vertGst = Vector3.Clamp(avgGs, -new Vector3(100, 100, 100), new Vector3(100, 100, 100));


                //Direction                                            
                Vector3 aimTo = Vector3.Zero;

                float antiSlowWiggle = map(Car.Velocity.Length(), 20f, 5f, 0f, 1, true);
                float antiCantSeeSlide = 0;// map(Math.Abs(Vector3.SignedAngle(Car.Velocity.Normalized, Car.ForwardVector, Car.UpVector)), 45f, 90f, 0f, 1f, true);

                Vector3 aimOffset = new Vector3(0, 0, -0.1f + (avgLocalGs.Y / 2));
                aimOffset.Z = Clamp(aimOffset.Z, -0.2f, 0.2f);

                Vector3 baseAim = Vector3.Lerp((Car.Velocity).Normalized, Car.ForwardVector, Math.Max(antiSlowWiggle, antiCantSeeSlide)) + (dirGs / 5); //AimModelVsDir
                Vector3 negativeBaseAim = Vector3.Lerp((Car.Velocity).Normalized, Car.ForwardVector, Math.Max(antiSlowWiggle, antiCantSeeSlide)) + (dirGs / 5); //AimModelVsDir


                //aimTo = Vector3.Lerp(lastPos + (Car.ForwardVector * 10), aimTo, antiSlowWiggle);
                if (Game.IsControlPressed(2, Control.LookBehind)) { baseAim = -baseAim; negativeBaseAim = -negativeBaseAim; };

                if (Game.IsControlJustPressed(2, Control.LookBehind) || Game.IsControlJustReleased(2, Control.LookBehind)) nfsCam.Direction = baseAim;
                else nfsCam.Direction = Vector3.Lerp(nfsCam.Direction, baseAim + aimOffset, Game.LastFrameTime * 10);


                float backOff = antiCantSeeSlide * 2 * antiSlowWiggle;


                //Position
                Vector3 newPos = properPosition;
                Vector3 basePos = Vector3.Lerp(-Car.Velocity.Normalized, -Car.ForwardVector, antiSlowWiggle); //PosModelVsDir


                newPos = Car.Position + (Car.Velocity * spdCorrection) - (negativeBaseAim * (Car.Model.GetDimensions().Y + baseFarBack + backOff)); //; + (basePos * (Car.Model.GetDimensions().Y + baseFarBack + backOff));
                newPos += (new Vector3(0, 0, baseFarUp));


                if (newPos.DistanceTo(Car.Position) < baseFarBack) newPos = Car.Position + ((newPos- Car.Position ).Normalized * baseFarBack);

                nfsCam.Position = Vector3.Lerp(nfsCam.Position, newPos, Game.LastFrameTime * 10);

                nfsCam.FieldOfView = GameplayCamera.FieldOfView;
                //World.DrawMarker(MarkerType.DebugSphere, aimTo, Vector3.Zero, Vector3.Zero, new Vector3(0.01f, 0.01f, 0.01f), Color.Blue);
                //World.DrawMarker(MarkerType.DebugSphere, newPos, Vector3.Zero, Vector3.Zero, new Vector3(0.01f, 0.01f, 0.01f), Color.Blue);

            }


            bool WasCheatStringJustEntered(string cheat)
            {
                return Function.Call<bool>(Hash._0x557E43C447E700A8, Game.GenerateHash(cheat));
            }
            float map(float x, float in_min, float in_max, float out_min, float out_max, bool clamp = false)
            {
                float r = (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
                if (clamp) r = Clamp(r, out_min, out_max);
                return r;
            }

            float Clamp(float val, float min, float max)
            {
                if (val.CompareTo(min) < 0) return min;
                else if (val.CompareTo(max) > 0) return max;
                else return val;
            }
        }
    }

}
