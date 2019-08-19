using System;
using System.Collections.Generic;
#if UNITY_INPUT_SYSTEM_ENABLE_XR
using UnityEngine.XR;
#endif
using UnityEngine.InputSystem.Layouts;

namespace UnityEngine.InputSystem.XR
{
    public static class XRUtilities
    {
        /// <summary>
        /// A simple Regex pattern that allows InputDeviceMatchers to match to any version of the XRInput interface.
        /// </summary>
        public const string kXRInterfaceMatchAnyVersion = "^(XRInput)";

        /// <summary>
        /// The initial, now deprecated interface for XRInput.  This version handles button packing for Android differently from current.
        /// </summary>
        public const string kXRInterfaceV1 = "XRInput";

        /// <summary>
        /// The current interface code sent with devices to identify as XRInput devices.
        /// </summary>
        public const string kXRInterfaceCurrent = "XRInputV1";
    }

    // Sync to UnityXRInputFeatureType in IUnityXRInput.h
    enum FeatureType
    {
        Custom = 0,
        Binary,
        DiscreteStates,
        Axis1D,
        Axis2D,
        Axis3D,
        Rotation,
    }

    // These structures are not explicitly assigned, but they are filled in via JSON serialization coming from matching structs in native.
#pragma warning disable 0649
    [Serializable]
    struct UsageHint
    {
        public string content;
    }

    //Sync to XRInputFeatureDefinition in XRInputDeviceDefinition.h
    [Serializable]
    struct XRFeatureDescriptor
    {
        public string name;
        public List<UsageHint> usageHints;
        public FeatureType featureType;
        public uint customSize;
    }

    // Sync to UnityXRInputDeviceRole in IUnityXRInput.h
    /// <summary>
    /// The generalized role that the device plays.  This can help in grouping devices by type (HMD, vs. hardware tracker vs. handed controller).
    /// </summary>
    public enum DeviceRole
    {
        Unknown = 0,
        Generic,
        LeftHanded,
        RightHanded,
        GameController,
        TrackingReference,
        HardwareTracker,
    }

    //Sync to XRInputDeviceDefinition in XRInputDeviceDefinition.h
    [Serializable]
    class XRDeviceDescriptor
    {
        public string deviceName;
        public string manufacturer;
        public string serialNumber;
#if UNITY_INPUT_SYSTEM_ENABLE_XR
#if UNITY_2019_3_OR_NEWER
        public InputDeviceCharacteristics characteristics;
#else //UNITY_2019_3_OR_NEWER
        public InputDeviceRole deviceRole;
#endif //UNITY_2019_3_OR_NEWER
#endif //UNITY_INPUT_SYSTEM_ENABLE_XR
        public int deviceId;
        public List<XRFeatureDescriptor> inputFeatures;

        internal string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        internal static XRDeviceDescriptor FromJson(string json)
        {
            return JsonUtility.FromJson<XRDeviceDescriptor>(json);
        }
    }
#pragma warning restore 0649

    /// <summary>
    /// A small helper class to aid in initializing and registering XR devices and layout builders.
    /// </summary>
#if UNITY_DISABLE_DEFAULT_INPUT_PLUGIN_INITIALIZATION
    public
#else
    internal
#endif
    static class XRSupport
    {
        /// <summary>
        /// Registers all initial templates and the generalized layout builder with the InputSystem.
        /// </summary>
        public static void Initialize()
        {
            InputSystem.RegisterLayout<XRHMD>();
            InputSystem.RegisterLayout<XRController>();

            InputSystem.RegisterLayout<DaydreamHMD>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithProduct("Daydream HMD"));
            InputSystem.RegisterLayout<DaydreamController>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithProduct("^(Daydream Controller)"));

            InputSystem.RegisterLayout<ViveHMD>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("HTC")
                    .WithProduct(@"^((Vive DVT)|(Vive MV.)|(Vive Pro)|(Vive. MV))"));
            InputSystem.RegisterLayout<ViveWand>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("HTC")
                    .WithProduct(@"^(OpenVR Controller\(((Vive. Controller)|(VIVE. Controller)|(Vive Controller)))"));
            InputSystem.RegisterLayout<KnucklesController>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("Valve")
                    .WithProduct(@"^(OpenVR Controller\(Knuckles)"));
            InputSystem.RegisterLayout<ViveTracker>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("HTC")
                    .WithProduct(@"^(VIVE Tracker)"));
            InputSystem.RegisterLayout<HandedViveTracker>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("HTC")
                    .WithProduct(@"^(OpenVR Controller\(VIVE Tracker)"));
            InputSystem.RegisterLayout<ViveLighthouse>(
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.kXRInterfaceMatchAnyVersion)
                    .WithManufacturer("HTC")
                    .WithProduct(@"^(HTC V2-XD/XE)"));

            InputSystem.onFindLayoutForDevice += XRLayoutBuilder.OnFindLayoutForDevice;
        }
    }
}
