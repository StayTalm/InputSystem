using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Input.LowLevel;
using UnityEngine.Experimental.Input.Processors;
using UnityEngine.Experimental.Input.Utilities;

namespace UnityEngine.Experimental.Input
{
    /// <summary>
    /// A filter for individual devices to check if events or device states contain significant, relevant changes.
    /// Irrelevant changes are any updates controls that are tagged as 'noisy', and any state change that turns into a no-operation change once processors are applied (e.g. in deadzone joystick movements).
    /// </summary>
    public class NoiseFilter
    {
        /// <summary>
        /// Simple cached value identifying how to filter this element (Bitmask, or individual type filtered).
        /// </summary>
        public enum ElementType
        {
            TypeUnknown = 0,
            EntireControl,
            FloatBelowEpsilon,
            Vector2MagnitudeBelowEpsilon
        }

        /// <summary>
        /// A filter for a single InputControl that gets it's value filtered.
        /// </summary>
        public struct FilterElement
        {
            /// <summary>
            /// The index in InputDevice.allControls for the filtered control.
            /// </summary>
            public int controlIndex;

            /// <summary>
            /// The type of filtering to perform.
            /// </summary>
            public ElementType type;

            /// <summary>
            /// Called when the NoiseFilter gets applied to a device, marks out any controls that can be wholly bitmasked.
            /// </summary>
            /// <param name="noiseFilterBuffer">The Noisefilter buffer for doing whole control filtering.</param>
            /// <param name="device">The device you want to apply filtering to.</param>
            public void Apply(IntPtr noiseFilterBuffer, InputDevice device)
            {
                if (controlIndex >= device.allControls.Count)
                    throw new IndexOutOfRangeException("NoiseFilter has array index beyond total size of device's controls");

                InputControl control = device.allControls[controlIndex];
                MemoryHelpers.SetBitsInBuffer(noiseFilterBuffer, control, false);
            }

            /// <summary>
            /// Checking an individual input event for significant InputControl changes.
            /// </summary>
            /// <param name="inputEvent">The input event being checked for changes</param>
            /// <param name="device">The input device being checked against </param>
            /// <returns>True if any changes exist in the event once the device has been filtered through for noise and non-significant changes.  False otherwise.</returns>
            public bool HasValidData(InputEventPtr inputEvent, InputDevice device)
            {
                if (type == ElementType.EntireControl)
                    return false;

                InputControl control = device.allControls[controlIndex];
                if (control == null)
                    return false;

                return control.HasSignificantChange(inputEvent);
            }
        }

        public static unsafe NoiseFilter CreateDefaultNoiseFilter(InputDevice device)
        {
            if (device == null)
                throw new ArgumentException("No device supplied to create default noise filter for", "device");

            var filter = new NoiseFilter();
            var elementsToAdd = stackalloc FilterElement[device.allControls.Count];
            var elementCount = 0;
            var controls = device.allControls;
            for (int i = 0; i < controls.Count; i++)
            {
                InputControl control = controls[i];
                if (control.noisy)
                {
                    FilterElement newElement;
                    newElement.controlIndex = i;
                    newElement.type = ElementType.EntireControl;
                    InputStateBlock stateblock = control.stateBlock;
                    elementsToAdd[elementCount++] = newElement;
                }
                else
                {
                    InputControl<float> controlAsFloat = control as InputControl<float>;
                    if (controlAsFloat != null && controlAsFloat.processors != null)
                    {
                        if (controlAsFloat.processors != null)
                        {
                            FilterElement newElement;
                            newElement.controlIndex = i;
                            newElement.type = ElementType.FloatBelowEpsilon;
                            InputStateBlock stateblock = control.stateBlock;
                            elementsToAdd[elementCount++] = newElement;
                        }
                    }
                    else
                    {
                        InputControl<Vector2> controlAsVec2 = control as InputControl<Vector2>;
                        if (controlAsVec2 != null && controlAsVec2.processors != null)
                        {
                            FilterElement newElement;
                            newElement.controlIndex = i;
                            newElement.type = ElementType.Vector2MagnitudeBelowEpsilon;
                            InputStateBlock stateblock = control.stateBlock;
                            elementsToAdd[elementCount++] = newElement;
                        }
                    }
                }
            }

            filter.elements = new FilterElement[elementCount];
            for (int j = 0; j < elementCount; j++)
            {
                filter.elements[j] = elementsToAdd[j];
            }

            return filter;
        }

        /// <summary>
        /// The list of elements to be checked for.  Each element represents a single InputControl.
        /// </summary>
        public FilterElement[] elements;

        /// <summary>
        /// Called when the NoiseFilter gets applied to a device, calls down to any individual FilteredElements that need to do any work.
        /// </summary>
        /// <param name="device">The device you want to apply filtering to.</param>
        internal void Apply(InputDevice device)
        {
            if (device == null)
                throw new ArgumentException("No device supplied to apply NoiseFilter to.", "device");

            var noiseFilterPtr = InputStateBuffers.s_NoiseFilterBuffer;
            if (noiseFilterPtr == IntPtr.Zero)
                return;

            MemoryHelpers.SetBitsInBuffer(noiseFilterPtr, device, true);

            for (int i = 0; i < elements.Length; i++)
            {
                elements[i].Apply(noiseFilterPtr, device);
            }
        }

        /// <summary>
        /// Resets a device to unfiltered
        /// </summary>
        /// <param name="device">The device you want reset</param>
        internal void Reset(InputDevice device)
        {
            var noiseFilterPtr = InputStateBuffers.s_NoiseFilterBuffer;
            if (noiseFilterPtr == IntPtr.Zero)
                return;

            MemoryHelpers.SetBitsInBuffer(noiseFilterPtr, device, true);
        }

        /// <summary>
        /// Checks an Input Event for any significant changes that would be considered user activity.
        /// </summary>
        /// <param name="inputEvent">The input event being checked for changes</param>
        /// <param name="device">The input device being checked against </param>
        /// <param name="offset">The offset into the device that the event is placed</param>
        /// <param name="sizeInBytes">The size of the event in bytes</param>
        /// <returns>True if any changes exist in the event once the device has been filtered through for noise and non-significant changes.  False otherwise.</returns>
        public unsafe bool HasValidData(InputDevice device, InputEventPtr inputEvent, uint offset, uint sizeInbytes)
        {
            if (!inputEvent.valid)
                throw new ArgumentException("Invalid or unset event being checked.", "inputEvent");

            if (device == null)
                throw new ArgumentException("No device passed in to check if inputEvent has valid data", "device");

            if (elements.Length == 0)
                return true;

            if ((offset + sizeInbytes) * 8 > device.stateBlock.sizeInBits)
                return false;

            bool result = false;

            var noiseFilterPtr = InputStateBuffers.s_NoiseFilterBuffer;
            if (noiseFilterPtr == IntPtr.Zero)
                throw new Exception("Noise Filter Buffer is uninitialized while trying to check state events for data.");

            var ptrToEventState = IntPtr.Zero;
            if (inputEvent.IsA<StateEvent>())
            {
                var stateEvent = StateEvent.From(inputEvent);
                ptrToEventState = stateEvent->state;
            }
            else if (inputEvent.IsA<DeltaStateEvent>())
            {
                var stateEvent = DeltaStateEvent.From(inputEvent);
                ptrToEventState = stateEvent->deltaState;
            }
            else
            {
                throw new ArgumentException("Invalid event type, we can only check for valid data on StateEvents and DeltaStateEvents.", "inputEvent");
            }

            if (MemoryHelpers.HasAnyNonZeroBitsAfterMaskingWithBuffer(ptrToEventState, noiseFilterPtr, offset, sizeInbytes * 8))
                return true;

            for (int i = 0; i < elements.Length && !result; i++)
            {
                result = elements[i].HasValidData(inputEvent, device);
            }

            return result;
        }
    }
}