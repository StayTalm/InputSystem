#if UNITY_EDITOR || UNITY_PS4
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.PS4;
using UnityEngine.InputSystem.PS4.LowLevel;
using UnityEngine.InputSystem.Processors;

public class PS4Tests : InputTestFixture
{
    [Test]
    [Category("Devices")]
    public void Devices_SupportsDualShockOnPS4()
    {
        var device = InputSystem.AddDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad", ////REVIEW: this should be the product name instead
            interfaceName = "PS4"
        });

        Assert.That(device, Is.AssignableTo<DualShockGamepadPS4>());
        var gamepad = (DualShockGamepadPS4)device;

        InputSystem.QueueStateEvent(gamepad,
            new DualShockGamepadStatePS4
            {
                buttons = 0xffffffff,
                leftStick = new Vector2(0.123f, 0.456f),
                rightStick = new Vector2(0.789f, 0.234f),
                leftTrigger = 0.567f,
                rightTrigger = 0.891f,
                acceleration = new Vector3(0.987f, 0.654f, 0.321f),
                orientation = new Quaternion(0.111f, 0.222f, 0.333f, 0.444f),
                angularVelocity = new Vector3(0.444f, 0.555f, 0.666f),
                touch0 = new PS4Touch
                {
                    touchId = 123,
                    position = new Vector2(0.231f, 0.342f)
                },
                touch1 = new PS4Touch
                {
                    touchId = 456,
                    position = new Vector2(0.453f, 0.564f)
                },
            });
        InputSystem.Update();

        Assert.That(gamepad.squareButton.isPressed);
        Assert.That(gamepad.triangleButton.isPressed);
        Assert.That(gamepad.circleButton.isPressed);
        Assert.That(gamepad.crossButton.isPressed);
        Assert.That(gamepad.buttonSouth.isPressed);
        Assert.That(gamepad.buttonNorth.isPressed);
        Assert.That(gamepad.buttonEast.isPressed);
        Assert.That(gamepad.buttonWest.isPressed);
        Assert.That(gamepad.leftStickButton.isPressed);
        Assert.That(gamepad.rightStickButton.isPressed);
        Assert.That(gamepad.L3.isPressed);
        Assert.That(gamepad.R3.isPressed);

        var leftStickDeadzone = gamepad.leftStick.TryGetProcessor<StickDeadzoneProcessor>();
        var rightStickDeadzone = gamepad.leftStick.TryGetProcessor<StickDeadzoneProcessor>();

        Assert.That(gamepad.leftStick.ReadValue(), Is.EqualTo(leftStickDeadzone.Process(new Vector2(0.123f, 0.456f))));
        Assert.That(gamepad.rightStick.ReadValue(), Is.EqualTo(rightStickDeadzone.Process(new Vector2(0.789f, 0.234f))));
        Assert.That(gamepad.leftTrigger.ReadValue(), Is.EqualTo(0.567).Within(0.00001));
        Assert.That(gamepad.rightTrigger.ReadValue(), Is.EqualTo(0.891).Within(0.00001));

        Assert.That(gamepad.acceleration.x.ReadValue(), Is.EqualTo(0.987).Within(0.00001));
        Assert.That(gamepad.acceleration.y.ReadValue(), Is.EqualTo(0.654).Within(0.00001));
        Assert.That(gamepad.acceleration.z.ReadValue(), Is.EqualTo(0.321).Within(0.00001));

        var orientation = gamepad.orientation.ReadValue();

        Assert.That(orientation.x, Is.EqualTo(0.111).Within(0.00001));
        Assert.That(orientation.y, Is.EqualTo(0.222).Within(0.00001));
        Assert.That(orientation.z, Is.EqualTo(0.333).Within(0.00001));
        Assert.That(orientation.w, Is.EqualTo(0.444).Within(0.00001));

        Assert.That(gamepad.angularVelocity.x.ReadValue(), Is.EqualTo(0.444).Within(0.00001));
        Assert.That(gamepad.angularVelocity.y.ReadValue(), Is.EqualTo(0.555).Within(0.00001));
        Assert.That(gamepad.angularVelocity.z.ReadValue(), Is.EqualTo(0.666).Within(0.00001));

        // Sensors should be marked as noisy.
        Assert.That(gamepad.acceleration.noisy, Is.True);
        Assert.That(gamepad.orientation.noisy, Is.True);
        Assert.That(gamepad.angularVelocity.noisy, Is.True);

        ////TODO: touch
    }

    [Test]
    [Category("Devices")]
    public void Devices_CanSetLightBarColorAndMotorSpeedsOnDualShockPS4()
    {
        var device = InputSystem.AddDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4"
        });

        Assert.That(device, Is.AssignableTo<DualShockGamepadPS4>());
        var gamepad = (DualShockGamepadPS4)device;

        DualShockPS4OuputCommand? receivedCommand = null;
        unsafe
        {
            runtime.SetDeviceCommandCallback(gamepad.id,
                (id, commandPtr) =>
                {
                    if (commandPtr->type == DualShockPS4OuputCommand.Type)
                    {
                        Assert.That(receivedCommand.HasValue, Is.False);
                        receivedCommand = *((DualShockPS4OuputCommand*)commandPtr);
                        return 1;
                    }

                    Assert.Fail("Received wrong type of command");
                    return InputDeviceCommand.GenericFailure;
                });
        }
        gamepad.SetMotorSpeeds(0.1234f, 0.5678f);

        Assert.That(receivedCommand.HasValue, Is.True);
        Assert.That(receivedCommand.Value.largeMotorSpeed, Is.EqualTo((byte)(0.1234 * 255)));
        Assert.That(receivedCommand.Value.smallMotorSpeed, Is.EqualTo((byte)(0.56787 * 255)));

        receivedCommand = null;
        gamepad.SetLightBarColor(new Color(0.123f, 0.456f, 0.789f));

        Assert.That(receivedCommand.HasValue, Is.True);
        Assert.That(receivedCommand.Value.redColor, Is.EqualTo((byte)(0.123f * 255)));
        Assert.That(receivedCommand.Value.greenColor, Is.EqualTo((byte)(0.456f * 255)));
        Assert.That(receivedCommand.Value.blueColor, Is.EqualTo((byte)(0.789f * 255)));
    }

    [Test]
    [Category("Devices")]
    public void Devices_CanReadSlotIndexAndGetDualShockPS4BySlotIndex()
    {
        runtime.ReportNewInputDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4",
            capabilities = new PS4InputDeviceDescriptor { slotId = 0, isAimController = false,  defaultColorId = 0, userId = 1234  }.ToJson()
        }.ToJson(), 1);

        runtime.ReportNewInputDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4",
            capabilities = new PS4InputDeviceDescriptor { slotId = 1, isAimController = false, defaultColorId = 0, userId = 1234 }.ToJson()
        }.ToJson(), 2);

        runtime.ReportNewInputDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4",
            capabilities = new PS4InputDeviceDescriptor { slotId = 2, isAimController = false, defaultColorId = 0, userId = 1234 }.ToJson()
        }.ToJson(), 3);

        runtime.ReportNewInputDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4",
            capabilities = new PS4InputDeviceDescriptor { slotId = 3, isAimController = false, defaultColorId = 0, userId = 1234 }.ToJson()
        }.ToJson(), 4);

        InputSystem.Update();

        var gamepad1 = (DualShockGamepadPS4)InputSystem.devices[0];
        var gamepad2 = (DualShockGamepadPS4)InputSystem.devices[1];
        var gamepad3 = (DualShockGamepadPS4)InputSystem.devices[2];
        var gamepad4 = (DualShockGamepadPS4)InputSystem.devices[3];

        Assert.That(gamepad1.slotIndex, Is.EqualTo(0));
        Assert.That(gamepad2.slotIndex, Is.EqualTo(1));
        Assert.That(gamepad3.slotIndex, Is.EqualTo(2));
        Assert.That(gamepad4.slotIndex, Is.EqualTo(3));

        Assert.That(DualShockGamepadPS4.GetBySlotIndex(0), Is.SameAs(gamepad1));
        Assert.That(DualShockGamepadPS4.GetBySlotIndex(1), Is.SameAs(gamepad2));
        Assert.That(DualShockGamepadPS4.GetBySlotIndex(2), Is.SameAs(gamepad3));
        Assert.That(DualShockGamepadPS4.GetBySlotIndex(3), Is.SameAs(gamepad4));
    }

    [Test]
    [Category("Devices")]
    public void Devices_CanQueryPS4UserIdFromDualShockPS4()
    {
        runtime.ReportNewInputDevice(new InputDeviceDescription
        {
            deviceClass = "PS4DualShockGamepad",
            interfaceName = "PS4",
            capabilities = new PS4InputDeviceDescriptor
            {
                slotId = 0,
                isAimController = false,
                defaultColorId = 0,
                userId = 1234
            }.ToJson()
        }.ToJson(), 1);
        InputSystem.Update();

        var device = InputSystem.devices[0];

        Assert.That(device, Is.AssignableTo<DualShockGamepadPS4>());

        var gamepad = (DualShockGamepadPS4)device;

        Assert.That(gamepad.ps4UserId, Is.EqualTo(1234));
        Assert.That(gamepad.slotIndex, Is.EqualTo(0));
    }

    [Test]
    [Category("Devices")]
    public void Devices_SupportsMoveOnPS4()
    {
        var device = InputSystem.AddDevice(new InputDeviceDescription
        {
            deviceClass = "PS4MoveController",
            interfaceName = "PS4"
        });

        Assert.That(device, Is.AssignableTo<MoveControllerPS4>());
        var move = (MoveControllerPS4)device;

        InputSystem.QueueStateEvent(move,
            new MoveControllerStatePS4
            {
                buttons = 0xffffffff,
                trigger = 0.567f,
                accelerometer = new Vector3(0.987f, 0.654f, 0.321f),
                gyro = new Vector3(0.444f, 0.555f, 0.666f),
            });

        InputSystem.Update();

        Assert.That(move.squareButton.isPressed);
        Assert.That(move.triangleButton.isPressed);
        Assert.That(move.circleButton.isPressed);
        Assert.That(move.crossButton.isPressed);
        Assert.That(move.selectButton.isPressed);
        Assert.That(move.triggerButton.isPressed);
        Assert.That(move.moveButton.isPressed);
        Assert.That(move.startButton.isPressed);

        Assert.That(move.trigger.ReadValue(), Is.EqualTo(0.567).Within(0.00001));

        Assert.That(move.accelerometer.x.ReadValue(), Is.EqualTo(0.987).Within(0.00001));
        Assert.That(move.accelerometer.y.ReadValue(), Is.EqualTo(0.654).Within(0.00001));
        Assert.That(move.accelerometer.z.ReadValue(), Is.EqualTo(0.321).Within(0.00001));

        Assert.That(move.gyro.x.ReadValue(), Is.EqualTo(0.444).Within(0.00001));
        Assert.That(move.gyro.y.ReadValue(), Is.EqualTo(0.555).Within(0.00001));
        Assert.That(move.gyro.z.ReadValue(), Is.EqualTo(0.666).Within(0.00001));
    }

    [Test]
    [Category("Devices")]
    public void Devices_CanSetLightColorAndMotorSpeedsOnMoveController()
    {
        var device = InputSystem.AddDevice(new InputDeviceDescription
        {
            deviceClass = "PS4MoveController",
            interfaceName = "PS4"
        });

        Assert.That(device, Is.AssignableTo<MoveControllerPS4>());
        var move = (MoveControllerPS4)device;

        MoveControllerPS4OuputCommand? receivedCommand = null;
        unsafe
        {
            runtime.SetDeviceCommandCallback(move.id,
                (id, commandPtr) =>
                {
                    if (commandPtr->type == MoveControllerPS4OuputCommand.Type)
                    {
                        Assert.That(receivedCommand.HasValue, Is.False);
                        receivedCommand = *((MoveControllerPS4OuputCommand*)commandPtr);
                        return 1;
                    }

                    Assert.Fail("Received wrong type of command");
                    return InputDeviceCommand.GenericFailure;
                });
        }
        move.SetMotorSpeed(0.1234f);

        Assert.That(receivedCommand.HasValue, Is.True);
        Assert.That(receivedCommand.Value.motorSpeed, Is.EqualTo((byte)(0.1234 * 255)));

        receivedCommand = null;
        move.SetLightSphereColor(new Color(0.123f, 0.456f, 0.789f));

        Assert.That(receivedCommand.HasValue, Is.True);
        Assert.That(receivedCommand.Value.redColor, Is.EqualTo((byte)(0.123f * 255)));
        Assert.That(receivedCommand.Value.greenColor, Is.EqualTo((byte)(0.456f * 255)));
        Assert.That(receivedCommand.Value.blueColor, Is.EqualTo((byte)(0.789f * 255)));
    }
}
#endif // UNITY_EDITOR || UNITY_PS4