using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Multipoint.Sdk;

namespace TouchBaseWPF
{
    public class TouchBaseDevice : TouchDevice, IDisposable
    {
        #region Delegates
        public delegate void TouchEvent(object sender, TouchBaseEventArgs args);
        #endregion

        #region Events
        /// <summary>
        /// Fired when the user places their finger down on the display
        /// </summary>
        public event TouchEvent TouchDown;

        /// <summary>
        /// Fired when the user lifts their finger off the display
        /// </summary>
        public event TouchEvent TouchUp;

        /// <summary>
        /// Fired when the user moves their finger across the display
        /// </summary>
        public event TouchEvent TouchMove;
        #endregion

        private readonly TouchBaseAPI _api;

        /// <summary>
        /// Attach a Touch-Base driven touch input device to a WPF Window, directly invoking touch events to C#, even if it is an off-primary monitor.
        /// </summary>
        /// <param name="hookedWindow">WPF Window target</param>
        /// <param name="relativeId">Relative device ID (0 for only 1 device)</param>
        /// <param name="nativeDeviceId">Native device ID for MultiPointSdk binding</param>
        public TouchBaseDevice(Window hookedWindow, int relativeId, int nativeDeviceId = 0) : base(nativeDeviceId)
        {
            MultipointSdk.Instance.Register(hookedWindow);
            var _devices = MultipointSdk.Instance.MouseDeviceList.Where(devInfo => devInfo.DeviceName.Contains("VID_1AC7")).ToList();

            _api = new TouchBaseAPI(_devices[relativeId].Id);
            Hook(hookedWindow);

            Activate();
            SetActiveSource(PresentationSource.FromVisual(hookedWindow));
            _api.TouchBaseDown += API_TouchBaseDown;
            _api.TouchBaseUp += API_TouchBaseUp;
            _api.TouchBaseMove += API_TouchBaseMove;
        }

        /// <summary>
        /// Position of most recent touch interaction
        /// </summary>
        public Point Position { get; set; }

        /// <summary>
        /// Hook a new window
        /// </summary>
        /// <param name="targetWindow"></param>
        public void Hook(Window targetWindow)
        {
            MultipointSdk.Instance.Register(targetWindow);
            _api.HookWindow(targetWindow);

            /* TODO: MultipointSdk seems to have a nasty side effect of trapping the system cursor on registration, fix it.
             *       Presumably because it's trying to trap the cursor to replicate it for multiple mice. Perhaps I can
             *       trap the cursor position pre-registration and force the cursor position on the system to go back
             *       there after registration is complete. (Alt-Tab out of the app fixes this, what?)
             */
        }

        /// <summary>
        /// Bubble out courtesy events from the class (Up/Down/Move) with the ability for the user to handle them
        /// and suppress the TouchUp(),TouchDown(),TouchMove() native input stack events.
        /// </summary>
        /// <param name="outgoingEvent">Outgoing event type (0=move, 1=up, 2=down)</param>
        /// <param name="position">Occurring position</param>
        /// <returns>True if the user handled the event, Else if we need to fire Touch{type}()</returns>
        private bool BubblePossibleHandledEvent(byte outgoingEvent, Point position)
        {
            var args = new TouchBaseEventArgs() {Handled = false, Point = position};
            switch (outgoingEvent)
            {
                case 0: //move
                    if (TouchMove != null) 
                        TouchMove(this, args);
                    break;
                case 1: //up
                    if (TouchUp != null)
                        TouchUp(this, args);
                    break;
                case 2: //down
                    if (TouchDown != null)
                        TouchDown(this, args);
                    break;
            }
            return args.Handled;
        }

        #region API Event Callbacks

        private void API_TouchBaseMove(object sender, Point args)
        {
            if (!BubblePossibleHandledEvent(0, args))
            {
                Position = args;
                ReportMove();
            }
        }

        private void API_TouchBaseUp(object sender, Point args)
        {
            if (!BubblePossibleHandledEvent(1, args))
            {
                Position = args;
                ReportUp();
            }
        }

        private void API_TouchBaseDown(object sender, Point args)
        {
            if (!BubblePossibleHandledEvent(2, args))
            {
                Position = args;
                ReportDown();
            }
        }

        #endregion

        #region TouchDevice Members

        public override TouchPointCollection GetIntermediateTouchPoints(IInputElement relativeTo)
        {
            return new TouchPointCollection();
        }

        public override TouchPoint GetTouchPoint(IInputElement relativeTo)
        {
            Point pt = Position;
            if (relativeTo != null)
            {
                pt = ActiveSource.RootVisual.TransformToDescendant((Visual) relativeTo).Transform(Position);
            }

            var rect = new Rect(pt, new Size(1.0, 1.0));

            return new TouchPoint(this, pt, rect, TouchAction.Move);
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Disposing function deactivates the device from the input stack
        /// </summary>
        public void Dispose()
        {
            Deactivate();
        }

        #endregion
    }

    /// <summary>
    /// Touch-Base Touch Event Arguments
    /// </summary>
    public class TouchBaseEventArgs
    {
        /// <summary>
        /// If the event is user-handled, or if the system needs to complete the event processing
        /// </summary>
        public bool Handled;

        /// <summary>
        /// Position, relative to the upper-left corner of the hooked window, of the touch event.
        /// </summary>
        public Point Point;
    }
}