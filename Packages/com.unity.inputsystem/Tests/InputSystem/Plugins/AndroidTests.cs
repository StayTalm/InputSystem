#if UNITY_EDITOR || UNITY_ANDROID
using System;
using UnityEngine.Experimental.Input;
using UnityEngine.Experimental.Input.Plugins.Android;
using UnityEngine.Experimental.Input.Plugins.Android.LowLevel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Input.Controls;
using UnityEngine.Experimental.Input.LowLevel;

class AndroidTests : InputTestFixture
{
    [Test]
    [Category("Devices")]
    public void Devices_CanDifferentiateAndroidGamepadFromJoystick()
    {
        var gamepad = InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            capabilities = new AndroidDeviceCapabilities
            {
                inputSources = AndroidInputSource.Gamepad | AndroidInputSource.Joystick,
            }.ToJson()
        });
        var joystick = InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            capabilities = new AndroidDeviceCapabilities
            {
                inputSources = AndroidInputSource.Joystick
            }.ToJson()
        });

        Assert.That(gamepad, Is.TypeOf<AndroidGamepad>());
        Assert.That(joystick, Is.TypeOf<AndroidJoystick>());
    }

    [Test]
    [Category("Devices")]
    public void Devices_SupportsAndroidGamepad()
    {
        var device = InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            capabilities = new AndroidDeviceCapabilities
            {
                inputSources = AndroidInputSource.Gamepad | AndroidInputSource.Joystick,
            }.ToJson()
        });

        Assert.That(device, Is.TypeOf<AndroidGamepad>());
        var controller = (AndroidGamepad)device;

        InputSystem.QueueStateEvent(controller,
            new AndroidGameControllerState()
            .WithAxis(AndroidAxis.Ltrigger, 0.123f)
            .WithAxis(AndroidAxis.Rtrigger, 0.456f)
            .WithAxis(AndroidAxis.X, 0.789f)
            .WithAxis(AndroidAxis.Y, 0.987f)
            .WithAxis(AndroidAxis.Z, 0.654f)
            .WithAxis(AndroidAxis.Rz, 0.321f));

        InputSystem.Update();

        Assert.That(controller.leftTrigger.ReadValue(), Is.EqualTo(0.123).Within(0.000001));
        Assert.That(controller.rightTrigger.ReadValue(), Is.EqualTo(0.456).Within(0.000001));
        Assert.That(controller.leftStick.x.ReadValue(), Is.EqualTo(0.789).Within(0.000001));
        Assert.That(controller.leftStick.y.ReadValue(), Is.EqualTo(-0.987).Within(0.000001)); // Y is upside down on Android.
        Assert.That(controller.rightStick.x.ReadValue(), Is.EqualTo(0.654).Within(0.000001));
        Assert.That(controller.rightStick.y.ReadValue(), Is.EqualTo(-0.321).Within(0.000001)); // Y is upside down on Android.

        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonA), controller.buttonSouth);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonX), controller.buttonWest);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonY), controller.buttonNorth);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonB), controller.buttonEast);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonThumbl), controller.leftStickButton);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonThumbr), controller.rightStickButton);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonL1), controller.leftShoulder);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonR1), controller.rightShoulder);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonStart), controller.startButton);
        AssertButtonPress(controller, new AndroidGameControllerState().WithButton(AndroidKeyCode.ButtonSelect), controller.selectButton);
    }

    [Test]
    [Category("Devices")]
    public void Devices_SupportsAndroidGamepad_WithAxisDpad()
    {
        var gamepad = (Gamepad)InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            capabilities = new AndroidDeviceCapabilities
            {
                inputSources = AndroidInputSource.Gamepad | AndroidInputSource.Joystick,
                motionAxes = new[]
                {
                    AndroidAxis.Generic1, // Noise
                    AndroidAxis.HatX,
                    AndroidAxis.Generic2, // Noise
                    AndroidAxis.HatY
                }
            }.ToJson()
        });

        // HatX is -1 (left) to 1 (right)
        // HatY is -1 (up) to 1 (down)

        InputSystem.QueueStateEvent(gamepad,
            new AndroidGameControllerState()
            .WithAxis(AndroidAxis.HatX, 1)
            .WithAxis(AndroidAxis.HatY, 1));
        InputSystem.Update();

        Assert.That(gamepad.dpad.left.isPressed, Is.False);
        Assert.That(gamepad.dpad.right.isPressed, Is.True);
        Assert.That(gamepad.dpad.up.isPressed, Is.False);
        Assert.That(gamepad.dpad.down.isPressed, Is.True);

        InputSystem.QueueStateEvent(gamepad,
            new AndroidGameControllerState()
            .WithAxis(AndroidAxis.HatX, -1)
            .WithAxis(AndroidAxis.HatY, -1));
        InputSystem.Update();

        Assert.That(gamepad.dpad.left.isPressed, Is.True);
        Assert.That(gamepad.dpad.right.isPressed, Is.False);
        Assert.That(gamepad.dpad.up.isPressed, Is.True);
        Assert.That(gamepad.dpad.down.isPressed, Is.False);

        InputSystem.QueueStateEvent(gamepad,
            new AndroidGameControllerState()
            .WithAxis(AndroidAxis.HatX, 0)
            .WithAxis(AndroidAxis.HatY, 0));
        InputSystem.Update();

        Assert.That(gamepad.dpad.left.isPressed, Is.False);
        Assert.That(gamepad.dpad.right.isPressed, Is.False);
        Assert.That(gamepad.dpad.up.isPressed, Is.False);
        Assert.That(gamepad.dpad.down.isPressed, Is.False);
    }

    [Test]
    [Category("Devices")]
    public void Devices_SupportsAndroidGamepad_WithButtonDpad()
    {
        var gamepad = (Gamepad)InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "Android",
            deviceClass = "AndroidGameController",
            capabilities = new AndroidDeviceCapabilities
            {
                inputSources = AndroidInputSource.Gamepad | AndroidInputSource.Joystick,
                motionAxes = new[] {
                    AndroidAxis.Generic1, // Noise
                    AndroidAxis.Generic2, // Noise
                }
            }.ToJson()
        });

        AssertButtonPress(gamepad, new AndroidGameControllerState().WithButton(AndroidKeyCode.DpadDown), gamepad.dpad.down);
        AssertButtonPress(gamepad, new AndroidGameControllerState().WithButton(AndroidKeyCode.DpadUp), gamepad.dpad.up);
        AssertButtonPress(gamepad, new AndroidGameControllerState().WithButton(AndroidKeyCode.DpadLeft), gamepad.dpad.left);
        AssertButtonPress(gamepad, new AndroidGameControllerState().WithButton(AndroidKeyCode.DpadRight), gamepad.dpad.right);
    }

    [Test]
    [Category("Devices")]
    [TestCase(typeof(AndroidAccelerometer))]
    [TestCase(typeof(AndroidMagneticField))]
    [TestCase(typeof(AndroidGyroscope))]
    [TestCase(typeof(AndroidLight))]
    [TestCase(typeof(AndroidPressure))]
    [TestCase(typeof(AndroidProximity))]
    [TestCase(typeof(AndroidGravity))]
    [TestCase(typeof(AndroidLinearAcceleration))]
    [TestCase(typeof(AndroidRotationVector))]
    [TestCase(typeof(AndroidRelativeHumidity))]
    [TestCase(typeof(AndroidAmbientTemperature))]
    [TestCase(typeof(AndroidStepCounter))]
    public void Devices_CanCreateAndroidSensors(Type type)
    {
        var device = InputSystem.AddDevice(type.Name);

        Assert.That(device, Is.AssignableTo<Sensor>());
        Assert.That(device, Is.TypeOf(type));
    }

    [Test]
    [Category("Devices")]
    [TestCase("AndroidAmbientTemperature", "ambientTemperature")]
    [TestCase("AndroidLight", "lightLevel")]
    [TestCase("AndroidPressure", "atmosphericPressure")]
    [TestCase("AndroidProximity", "distance")]
    [TestCase("AndroidRelativeHumidity", "relativeHumidity")]
    [TestCase("AndroidAmbientTemperature", "ambientTemperature")]
    public void Devices_SupportSensorsWithAxisControl(string layoutName, string controlName)
    {
        var device = InputSystem.AddDevice(layoutName);
        var control = (AxisControl)device[controlName];

        InputEventPtr stateEventPtr;
        using (StateEvent.From(device, out stateEventPtr))
        {
            control.WriteValueInto(stateEventPtr, 0.123f);

            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.Update();

            Assert.That(control.ReadValue(), Is.EqualTo(0.123f).Within(0.0000001));
        }
    }

    [Test]
    [Category("Devices")]
    [TestCase("AndroidStepCounter", "stepCounter")]
    public void Devices_SupportSensorsWithIntegerControl(string layoutName, string controlName)
    {
        var device = InputSystem.AddDevice(layoutName);
        var control = (IntegerControl)device[controlName];

        InputEventPtr stateEventPtr;
        using (StateEvent.From(device, out stateEventPtr))
        {
            control.WriteValueInto(stateEventPtr, 5);

            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.Update();

            Assert.That(control.ReadValue(), Is.EqualTo(5));
        }
    }

    [Test]
    [Category("Devices")]
    [TestCase("AndroidAccelerometer", "acceleration")]
    [TestCase("AndroidMagneticField", "magneticField")]
    public void Devices_SupportSensorsWithVector3Control(string layoutName, string controlName)
    {
        var device = InputSystem.AddDevice(layoutName);
        var control = (Vector3Control)device[controlName];

        InputEventPtr stateEventPtr;
        using (StateEvent.From(device, out stateEventPtr))
        {
            ////FIXME: Seems like written value doesn't through processor, for ex, AndroidCompensateDirectionProcessor
            control.WriteValueInto(stateEventPtr, new Vector3(0.1f, 0.2f, 0.3f));

            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.QueueEvent(stateEventPtr);
            InputSystem.Update();

            Assert.That(control.x.ReadValue(), Is.EqualTo(0.1f).Within(0.000001));
            Assert.That(control.y.ReadValue(), Is.EqualTo(0.2f).Within(0.000001));
            Assert.That(control.z.ReadValue(), Is.EqualTo(0.3f).Within(0.000001));
        }
    }
}
#endif // UNITY_EDITOR || UNITY_ANDROID
