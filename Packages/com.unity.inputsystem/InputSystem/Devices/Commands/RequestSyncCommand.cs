using System;
using System.Runtime.InteropServices;
using UnityEngine.Experimental.Input.Utilities;

namespace UnityEngine.Experimental.Input.LowLevel
{
    /// <summary>
    /// A command to tell the runtime to sync the device to it's last known state.
    /// </summary>
    /// <remarks>
    /// This triggers an event from the underlying device that represents the whole, current state.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = InputDeviceCommand.kBaseCommandSize)]
    public unsafe struct RequestSyncCommand : IInputDeviceCommandInfo
    {
        public static FourCC Type { get { return new FourCC('S', 'Y', 'N', 'C'); } }

        public const int kSize = InputDeviceCommand.kBaseCommandSize;

        [FieldOffset(0)]
        public InputDeviceCommand baseCommand;

        public FourCC GetTypeStatic()
        {
            return Type;
        }

        public static RequestSyncCommand Create()
        {
            return new RequestSyncCommand
            {
                baseCommand = new InputDeviceCommand(Type, kSize)
            };
        }
    }
}