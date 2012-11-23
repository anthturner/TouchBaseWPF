Touch-Base WPF Touch Helper Library
Anthony Turner <mzxgiant at gmail dot com>
This library was written for the MIMO UM-720S, please forgive the allusions to a touchscreen... while I haven't tested this on any other Touch-Base monitors, it only uses the Touch-Base API and MultiPoint SDK for all of its operations. If your application can deal with those requirements, give this a shot.


This library is to fix the gap in Touch-Base UPDD drivers and Windows 7 that forces only the primary monitor to report touch events. It is also designed to allow the user to have "multiple" mouse cursors, such that the developer can isolate the touch input events from the rest of the OS. (Note how some applications receive touch events but allow them to be promoted to click events, which can pull focus from the core monitor if you're using the Touch-Base monitor for a dock/control application)


Requirements:
	- Windows 7 (32/64-bit)
	- .NET Framework 4


This library utilizes Microsoft's MultiPointSdk, which allows us to manually insert an input device into the input stack, and force its events to match the positional coordinates from the touch controller, coming directly off the API. The integration of the API here is very shallow; it's only sufficient to pull in touch coordinates and button states.


This package includes libraries from Microsoft and Touch-Base. These files are not mine; I hold no liability for them nor can I support them. These files are:
	Touch-Base ( http://www.touch-base.com/ ):
		+ ACE_UPDD_5.6.2.DLL
		+ TBApi.dll
	Microsoft Windows MultiPoint SDK ( http://www.microsoft.com/multipoint/mouse-sdk/ ):
		+ Microsoft.Multipoint.Sdk.dll
		+ Microsoft.Multipoint.Sdk.Controls.dll


Usage:
	[In a C# codebehind from a Window]
	var boundTouchDevice = new TouchBaseDevice(this, 0);
	// This will hook a Touch-Base input panel to the current window


Events:
	+ TouchDown(object sender, TouchBaseEventArgs args)
		Fired when the user puts their finger on the Touch-Base display
	+ TouchUp(object sender, TouchBaseEventArgs args)
		Fired when the user takes their finger off the Touch-Base display
	+ TouchMove(object sender, TouchBaseEventArgs args)
		Fired when the user moves their finger across the Touch-Base display


Methods:
	+ Hook(Window targetWindow)
		Run this method to hook the MultiPointSdk engine and the scaling
		handle to the target window