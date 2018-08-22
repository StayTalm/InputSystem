using System;
using UnityEngine.Experimental.Input.LowLevel;
using UnityEngine.Experimental.Input.Utilities;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Experimental.Input.Plugins.XR;

////REVIEW: can we construct the control tree of devices on demand so that the user never has to pay for
////        the heap objects of devices he doesn't use?

// per device functions:
//  - update/poll
//  - IOCTL
//  - text input
//  - configuration change
//  - make current
//  - on remove (also resets current)
//
// Ideally, these would *not* be virtual methods on InputDevice but use a different process (which?)
// for associating responses with devices

namespace UnityEngine.Experimental.Input
{
    /// <summary>
    /// The root of a control hierarchy.
    /// </summary>
    /// <remarks>
    /// Input devices act as the container for control hierarchies. Every hierarchy has to have
    /// a device at the root. Devices cannot occur inside of hierarchies.
    ///
    /// Unlike other controls, usages of InputDevices are allowed to be changed on the fly
    /// without requiring a change to the device layout (<see cref="InputSystem.SetUsage"/>).
    /// </remarks>
    public class InputDevice : InputControl
    {
        public const int kInvalidDeviceId = 0;
        internal const int kInvalidDeviceIndex = -1;

        /// <summary>
        /// Metadata describing the device (product name etc.).
        /// </summary>
        /// <remarks>
        /// The description of a device is unchanging over its lifetime and does not
        /// comprise data about a device's configuration (which is considered mutable).
        /// </remarks>
        public InputDeviceDescription description
        {
            get { return m_Description; }
        }

        ////REVIEW: turn this into an object of some kind?
        ////REVIEW: on Xbox, a device can have multiple player IDs assigned to it
        /// <summary>
        /// The user currently associated with the input device or null if no user is.
        /// </summary>
        public string userId
        {
            get
            {
                RefreshConfigurationIfNeeded();
                return m_UserId;
            }
            protected set { m_UserId = value; }
        }

        ////REVIEW: this might be useful even at the control level
        public bool enabled
        {
            get
            {
                // Fetch state from runtime, if necessary.
                if ((m_DeviceFlags & DeviceFlags.DisabledStateHasBeenQueried) != DeviceFlags.DisabledStateHasBeenQueried)
                {
                    var command = QueryEnabledStateCommand.Create();
                    if (ExecuteCommand(ref command) >= 0)
                    {
                        if (command.isEnabled)
                            m_DeviceFlags &= ~DeviceFlags.Disabled;
                        else
                            m_DeviceFlags |= DeviceFlags.Disabled;
                    }
                    else
                    {
                        // We got no response on the enable/disable state. Assume device is enabled.
                        m_DeviceFlags &= ~DeviceFlags.Disabled;
                    }

                    // Only fetch enable/disable state again if we get a configuration change event.
                    m_DeviceFlags |= DeviceFlags.DisabledStateHasBeenQueried;
                }

                return (m_DeviceFlags & DeviceFlags.Disabled) != DeviceFlags.Disabled;
            }
        }

        /// <summary>
        /// Whether the device has been added to the system.
        /// </summary>
        public bool added
        {
            get { return (m_DeviceIndex != kInvalidDeviceIndex); }
        }

        /// <summary>
        /// Whether the device is mirrored from a remote input system and not actually present
        /// as a "real" device in the local system.
        /// </summary>
        public bool remote
        {
            get { return (m_DeviceFlags & DeviceFlags.Remote) == DeviceFlags.Remote; }
        }

        /// <summary>
        /// Whether the device comes from the native Unity runtime.
        /// </summary>
        public bool native
        {
            get { return (m_DeviceFlags & DeviceFlags.Native) == DeviceFlags.Native; }
        }

        public bool updateBeforeRender
        {
            get { return (m_DeviceFlags & DeviceFlags.UpdateBeforeRender) == DeviceFlags.UpdateBeforeRender; }
        }

        /// <summary>
        /// Unique numeric ID for the device.
        /// </summary>
        /// <remarks>
        /// This is only assigned once a device has been added to the system. No two devices will receive the same
        /// ID and no device will receive an ID that another device used before even if the device was removed.
        ///
        /// IDs are assigned by the input runtime.
        /// </remarks>
        /// <seealso cref="IInputRuntime.AllocateDeviceId"/>
        public int id
        {
            get { return m_Id; }
        }

        /// <summary>
        /// Timestamp of last state event used to update the device.
        /// </summary>
        /// <remarks>
        /// Events other than <see cref="LowLevel.StateEvent"/> and <see cref="LowLevel.DeltaStateEvent"/> will
        /// not cause lastUpdateTime to be changed.
        /// </remarks>
        public double lastUpdateTime
        {
            get { return m_LastUpdateTime; }
        }

        public bool wasUpdatedThisFrame
        {
            get
            {
                var updateType = InputUpdate.lastUpdateType;
                if (updateType == InputUpdateType.Dynamic || updateType == InputUpdateType.BeforeRender)
                    return m_CurrentDynamicUpdateCount == InputUpdate.dynamicUpdateCount;
                if (updateType == InputUpdateType.Fixed)
                    return m_CurrentFixedUpdateCount == InputUpdate.fixedUpdateCount;

                ////REVIEW: how should this behave in the editor
                return false;
            }
        }

        /// <summary>
        /// A flattened list of controls that make up the device.
        /// </summary>
        /// <remarks>
        /// Does not allocate.
        /// </remarks>
        public ReadOnlyArray<InputControl> allControls
        {
            get
            {
                // Since m_ChildrenForEachControl contains the device's children as well as the children
                // of each control in the hierarchy, and since each control can only have a single parent,
                // this list will actually deliver a flattened list of all controls in the hierarchy (and without
                // the device itself being listed).
                return new ReadOnlyArray<InputControl>(m_ChildrenForEachControl);
            }
        }

        public override Type valueType
        {
            get { return typeof(byte[]); }
        }

        public NoiseFilter noiseFilter
        {
            get
            {
                return m_NoiseFilter;
            }
            set
            {
                if(value != m_NoiseFilter)
                {
                    if (value == null)
                        m_NoiseFilter.Reset();
                    else
                        value.Apply(this);

                    m_NoiseFilter = value;
                }               
            }
        }

        /// <summary>
        /// Return the current state of the device as byte array.
        /// </summary>
        public override unsafe object ReadValueAsObject()
        {
            if (m_DeviceIndex == kInvalidDeviceIndex)
                return null;

            var numBytes = stateBlock.alignedSizeInBytes;
            var array = new byte[numBytes];
            fixed(byte* arrayPtr = array)
            {
                UnsafeUtility.MemCpy(arrayPtr, currentStatePtr.ToPointer(), numBytes);
            }

            return array;
        }

        public override object ReadDefaultValueAsObject()
        {
            throw new NotImplementedException();
        }

        public override void WriteValueFromObjectInto(IntPtr buffer, long bufferSize, object value)
        {
            throw new NotImplementedException();
        }

        // This has to be public for Activator.CreateInstance() to be happy.
        public InputDevice()
        {
            m_Id = kInvalidDeviceId;
            m_DeviceIndex = kInvalidDeviceIndex;
        }

        /// <summary>
        /// Make this the current device of its type.
        /// </summary>
        /// <remarks>
        /// Use this to set static properties that give fast access to the latest device used of a given
        /// type (<see cref="Gamepad.current"/> or <see cref="XRController.leftHand"/> and <see cref="XRController.rightHand"/>).
        ///
        /// This functionality is somewhat like a 'pwd' for the semantic paths but one where there can
        /// be multiple current working directories, one for each type.
        ///
        /// A device will be made current by the system initially when it is created and subsequently whenever
        /// it receives an event.
        /// </remarks>
        public virtual void MakeCurrent()
        {
        }

        ////REVIEW: should this receive a timestamp, too?
        /// <summary>
        /// Invoked when the device receive a <see cref="LowLevel.TextEvent">text input event</see>.
        /// </summary>
        /// <param name="character"></param>
        public virtual void OnTextInput(char character)
        {
        }

        /// <summary>
        /// Called by the system when the configuration of the device has changed.
        /// </summary>
        /// <seealso cref="DeviceConfigurationEvent"/>
        internal void OnConfigurationChanged()
        {
            // Mark all controls in the hierarchy as having their config out of date.
            // We don't want to update configuration right away but rather wait until
            // someone actually depends on it.
            isConfigUpToDate = false;
            for (var i = 0; i < m_ChildrenForEachControl.Length; ++i)
                m_ChildrenForEachControl[i].isConfigUpToDate = false;

            // Make sure we fetch the enabled/disabled state again.
            m_DeviceFlags &= ~DeviceFlags.DisabledStateHasBeenQueried;
        }

        protected virtual void OnAdded()
        {
        }

        protected virtual void OnRemoved()
        {
        }

        ////REVIEW: return just bool instead of long and require everything else to go in the command?
        /// <summary>
        /// Perform a device-specific command.
        /// </summary>
        /// <param name="command">Data for the command to be performed.</param>
        /// <returns>A transfer-specific return code. Negative values are considered failure codes.</returns>
        /// <remarks>
        /// Commands allow devices to set up custom protocols without having to extend
        /// the device API. This is most useful for devices implemented in the native Unity runtime
        /// which, through the command interface, may provide custom, device-specific functions.
        ///
        /// This is a low-level API. It works in a similar way to <a href="https://msdn.microsoft.com/en-us/library/windows/desktop/aa363216%28v=vs.85%29.aspx?f=255&MSPPError=-2147217396"
        /// target="_blank">DeviceIoControl</a> on Windows and <a href="https://developer.apple.com/legacy/library/documentation/Darwin/Reference/ManPages/man2/ioctl.2.html"
        /// target="_blank">ioctl</a> on UNIX-like systems.
        /// </remarks>
        public long ExecuteCommand<TCommand>(ref TCommand command)
            where TCommand : struct, IInputDeviceCommandInfo
        {
            return InputRuntime.s_Instance.DeviceCommand(id, ref command);
        }

        protected void RefreshUserId()
        {
            m_UserId = null;
            var command = QueryUserIdCommand.Create();
            if (ExecuteCommand(ref command) > 0)
                m_UserId = command.ReadId();
        }

        [Flags]
        internal enum DeviceFlags
        {
            UpdateBeforeRender = 1 << 0,
            HasStateCallbacks = 1 << 1,
            HasNoisyControls = 1 << 2,
            HasControlsWithDefaultState = 1 << 3,
            Remote = 1 << 4, // It's a local mirror of a device from a remote player connection.
            Native = 1 << 5, // It's a device created from data surfaced by NativeInputRuntime.
            Disabled = 1 << 6,
            DisabledStateHasBeenQueried = 1 << 7, // Whether we have fetched the current enable/disable state from the runtime.
        }

        internal DeviceFlags m_DeviceFlags;
        internal int m_Id;
        internal string m_UserId;
        internal int m_DeviceIndex; // Index in InputManager.m_Devices.
        internal InputDeviceDescription m_Description;

        // Time of last event we received.
        internal double m_LastUpdateTime;

        // The dynamic and fixed update count corresponding to the current
        // front buffers that are active on the device. We use this to know
        // when to flip buffers.
        internal uint m_CurrentDynamicUpdateCount;
        internal uint m_CurrentFixedUpdateCount;

        // List of aliases for all controls. Each control gets a slice of this array.
        // See 'InputControl.aliases'.
        // NOTE: The device's own aliases are part of this array as well.
        internal InternedString[] m_AliasesForEachControl;

        // List of usages for all controls. Each control gets a slice of this array.
        // See 'InputControl.usages'.
        // NOTE: The device's own usages are part of this array as well. They are always
        //       at the *end* of the array.
        internal InternedString[] m_UsagesForEachControl;
        internal InputControl[] m_UsageToControl;

        // List of children for all controls. Each control gets a slice of this array.
        // See 'InputControl.children'.
        // NOTE: The device's own children are part of this array as well.
        internal InputControl[] m_ChildrenForEachControl;

        internal NoiseFilter m_NoiseFilter;

        // NOTE: We don't store processors in a combined array the same way we do for
        //       usages and children as that would require lots of casting from 'object'.

        /// <summary>
        /// If true, the device has at least one control that has an explicit default state.
        /// </summary>
        internal bool hasControlsWithDefaultState
        {
            get { return (m_DeviceFlags & DeviceFlags.HasControlsWithDefaultState) == DeviceFlags.HasControlsWithDefaultState; }
            set
            {
                if (value)
                    m_DeviceFlags |= DeviceFlags.HasControlsWithDefaultState;
                else
                    m_DeviceFlags &= ~DeviceFlags.HasControlsWithDefaultState;
            }
        }

        internal void SetUsage(InternedString usage)
        {
            // Make last entry in m_UsagesForEachControl be our device usage string.
            var numControlUsages = m_UsageToControl != null ? m_UsageToControl.Length : 0;
            Array.Resize(ref m_UsagesForEachControl, numControlUsages + 1);
            m_UsagesForEachControl[numControlUsages] = usage;
            m_UsagesReadOnly = new ReadOnlyArray<InternedString>(m_UsagesForEachControl, numControlUsages, 1);

            // Update controls to all point to new usage array.
            UpdateUsageArraysOnControls();
        }

        internal void UpdateUsageArraysOnControls()
        {
            if (m_UsageToControl == null)
                return;

            for (var i = 0; i < m_UsageToControl.Length; ++i)
                m_UsageToControl[i].m_UsagesReadOnly.m_Array = m_UsagesForEachControl;
        }

        internal void NotifyAdded()
        {
            OnAdded();
        }

        internal void NotifyRemoved()
        {
            OnRemoved();
        }
    }
}
