using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace TouchBaseWPF
{
    public unsafe class TouchBaseAPI // todo: internal
    {
        private uint _lastX;
        private uint _lastY;
        private DateTime _lastRead;
        internal Point LastPoint;

        private static bool APIInitialized;
        private bool _mousePort = true;
        private ManualResetEventSlim _callbackReset = new ManualResetEventSlim(true);

        private long ScalingHandle = -1;

        #region Event Bubbling
        public delegate void TouchBaseEvent(object sender, Point args);
        /// <summary>
        /// Fired when display registers a L|R=1 event
        /// </summary>
        public event TouchBaseEvent TouchBaseDown;
        /// <summary>
        /// Fired when display registers a L&R=0 event
        /// </summary>
        public event TouchBaseEvent TouchBaseUp;
        /// <summary>
        /// Fired when display registers a positional change
        /// </summary>
        public event TouchBaseEvent TouchBaseMove;

        private Window HookedWnd;
        private DataRaisedCallback DataRaisedDelegate;
        private delegate void DataRaisedCallback(int context, data_block* p_mydata);
        #endregion

        /// <summary>
        /// Wrap the Touch-Base API for the purpose of raising off-primary-monitor touch events
        /// </summary>
        /// <param name="devNum"></param>
        internal TouchBaseAPI(int devNum)
        {
            if (!APIInitialized)
            {
                // Init/Open the API
                DLL_TBApiInit();
                int result = DLL_TBApiOpen();
                if (result == 0)
                    throw new Exception("Touch-Base API Initialization failed");
                APIInitialized = true;
            }

            // Open device #0
            DLL_TBApiGetRelativeDevice(devNum);

            // Reset error counts
            DLL_TBApiResetErrorCounts();

            // Disable MousePort hook (will keep Windows from pulling focus to the touch monitor)
            MousePort = false;

            // Link the call function back to the delegate pointer
            DataRaisedDelegate = this.DataCallback;

            // Register the data callback to retrieve pointer x/y and press information
            DLL_TBApiRegisterDataCallback(0, 0, 3, DataRaisedDelegate);
        }
        ~TouchBaseAPI()
        {
            MousePort = true;
            DLL_TBApiUnregisterDataCallback(DataRaisedDelegate);
            DLL_TBApiClose();
            DLL_TBApiTerminate();
        }

        /// <summary>
        /// Set scale dimensions for the controller coordinates based off the given window
        /// </summary>
        /// <param name="targetWindow">Hooked window</param>
        internal void HookWindow(Window targetWindow)
        {
            HookedWnd = targetWindow;
            //if (!DLL_TBApiSetScaleDimensions(ref ScalingHandle, (int)targetWindow.Left, (int)targetWindow.Top, (int)(targetWindow.Width + targetWindow.Left),
            //                            (int)(targetWindow.Height + targetWindow.Top)))
            if (!DLL_TBApiSetScaleDimensions(ref ScalingHandle, 0, 0, (int)(targetWindow.Width),
                                        (int)(targetWindow.Height)))
                throw new Exception("Failed to hook touch controller's scaling handle");
        }

        /// <summary>
        /// Turn on or off the mouse-port capability, which raises touch events as click events to the OS
        /// </summary>
        internal bool MousePort
        {
            get { return _mousePort; }
            set
            {
                _mousePort = value;
                DLL_TBApiMousePortInterfaceEnable(value ? 1 : 0);
            }
        }

        /// <summary>
        /// DataReceived callback from the API
        /// </summary>
        /// <param name="context">Context ID (always 0 for this)</param>
        /// <param name="data">Data block pointer</param>
        private void DataCallback(int context, data_block* data)
        {
            // In order to make this play nice, we have to invoke it on the dispatcher thread of the hooked window.
            HookedWnd.Dispatcher.Invoke(new ThreadStart(() => DataCallbackSTA(context, data)),
                                             DispatcherPriority.Render);
        }

        /// <summary>
        /// Dispatched thread of DataReceived callback
        /// </summary>
        /// <param name="context">Context ID (always 0 for this)</param>
        /// <param name="data">Data block pointer</param>
        private void DataCallbackSTA(int context, data_block* data)
        {
            try
            {
                // X/Y Coordinates
                if (data->type == 1)
                {
                    my_data_block.tick = data->tick;
                    my_data_block.rawx = data->rawx;
                    my_data_block.rawy = data->rawy;
                    my_data_block.calx = data->calx;
                    my_data_block.caly = data->caly;
                    my_data_block.hDevice = data->hDevice;
                    my_data_block.hStylus = data->hStylus;
                    my_data_block.type = data->type;

                    if (Math.Abs(my_data_block.rawx - _lastX) < 5 && Math.Abs(my_data_block.rawy - _lastY) < 5)
                    // todo: invert this
                    {
                    }
                    else
                    {
                        _lastX = my_data_block.rawx;
                        _lastY = my_data_block.rawy;

                        if (TouchBaseMove != null && ScalingHandle != -1)
                        {
                            int x = 0, y = 0;
                            // Use the hooked window's rectangle to scale the controller coordinates to the window
                            if (DLL_TBApiScaleCoordinates(ref ScalingHandle, ref my_data_block, ref x, ref y))
                                LastPoint = new Point(x, y);
                            else
                                LastPoint = new Point(my_data_block.calx, my_data_block.caly);

                            TouchBaseMove(this, LastPoint);
                        }
                    }
                }
                // Touch Up/Down state
                if (data->type == 2)
                {
                    event_block* p_event = (event_block*)data;
                    my_event_block.tick = p_event->tick;
                    my_event_block.left = p_event->left;
                    my_event_block.right = p_event->right;
                    my_event_block.timed = p_event->timed;
                    my_event_block.hDevice = p_event->hDevice;
                    my_event_block.hStylus = p_event->hStylus;
                    my_event_block.type = p_event->type;

                    // Fire "Down" if either Left or Right is pressed
                    if (my_event_block.left == 1 || my_event_block.right == 1)
                    {
                        if (TouchBaseDown != null && ScalingHandle != -1)
                            TouchBaseDown(this, LastPoint);
                    }
                    // Fire "Up" if both Left and Light are not pressed
                    else if (my_event_block.left == 0 && my_event_block.right == 0)
                    {
                        if (TouchBaseUp != null && ScalingHandle != -1)
                            TouchBaseUp(this, LastPoint);
                    }
                }

                if ((DateTime.Now - _lastRead).TotalMilliseconds < 100)
                    return;
                _lastRead = DateTime.Now;
            }
            catch (Exception ex) { }
            finally
            {
                _callbackReset.Set();
            }
        }

        #region Low-Level Structs
        public struct data_block
        {
            public uint tick;
            public uint rawx;
            public uint rawy;
            public uint calx;
            public uint caly;
            public fixed byte data_inside[76 - (4 * 6) - (2 * 1)]; //to make up correct size of 76 bytes.
            public byte hDevice;
            public byte hStylus;
            public uint type;
        };

        public data_block my_data_block;

        public struct event_block
        {
            public uint tick;
            public byte left;
            public byte right;
            public byte timed;
            public fixed byte data_inside[76 - (4 * 2) - (5 * 1)]; //to make up correct size of 76 bytes.
            public byte hDevice;
            public byte hStylus;
            public uint type;
        };

        public event_block my_event_block;
        #endregion

        #region Imports
        [DllImport("TBApi.dll")]
        private static extern bool DLL_TBApiScaleCoordinates(ref long handle, ref data_block data, ref int x, ref int y);

        [DllImport("TBApi.dll")]
        private static extern bool DLL_TBApiSetScaleDimensions(ref long handle, int left, int top, int right, int bottom);

        [DllImport("TBApi.dll")]
        private static extern void DLL_TBApiInit();

        [DllImport("TBApi.dll")]
        private static extern int DLL_TBApiOpen();

        [DllImport("TBApi.dll")]
        private static extern int DLL_TBApiGetRelativeDevice(int position);

        [DllImport("TBApi.dll")]
        private static extern void DLL_TBApiMousePortInterfaceEnable(int state);

        [DllImport("TBApi.dll")]
        private static extern bool DLL_TBApiResetErrorCounts();

        [DllImport("TBApi.dll")]
        private static extern int DLL_TBApiRegisterDataCallback(
            int aDeviceHandle,
            int aContext, int aTypes,
            DataRaisedCallback callback
            );
        
        [DllImport("TBApi.dll")]
        private static extern int DLL_TBApiUnregisterDataCallback(DataRaisedCallback callback);
        
        [DllImport("TBApi.dll")]
        private static extern void DLL_TBApiTerminate();

        [DllImport("TBApi.dll")]
        private static extern void DLL_TBApiClose();

        #endregion
    }
}