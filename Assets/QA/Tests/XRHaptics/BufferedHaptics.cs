using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Input.Plugins.XR;
using UnityEngine.Experimental.Input.Plugins.XR.Haptics;

public class BufferedHaptics : MonoBehaviour
{
    public float fillRate = 1.5f;
    public float interval = 2.0f;

    public bool enableLeft;
    public bool enableRight;

	// Update is called once per frame
	void Update ()
    {
		if(enableLeft)
        {
            XRControllerWithRumble controller = XRController.leftHand as XRControllerWithRumble;
            if (controller != null)
            {
                BufferedRumble rumble = controller.m_BufferedRumble;
                uint numSamples = (uint)(rumble.capabilities.frequencyHz * (Time.deltaTime * fillRate));
                numSamples = (numSamples > rumble.capabilities.maxBufferSize) ? rumble.capabilities.maxBufferSize : numSamples;

                float sampleInterval = (1.0f / (float)rumble.capabilities.frequencyHz) * interval;

                float currentSampleTime = Time.realtimeSinceStartup * interval;
                byte[] samples = new byte[numSamples];
                for(uint i = 0; i < numSamples; i++)
                {
                    samples[i] = (byte)(Mathf.Sin(currentSampleTime) * 255);
                    currentSampleTime += sampleInterval;
                }

                rumble.EnqueueRumble(0, samples);
            }
        }

        if(enableRight)
        {
            XRControllerWithRumble controller = XRController.rightHand as XRControllerWithRumble;
            if (controller != null)
            {
                BufferedRumble rumble = controller.m_BufferedRumble;
                uint numSamples = (uint)(rumble.capabilities.frequencyHz * (Time.deltaTime * fillRate));
                numSamples = (numSamples > rumble.capabilities.maxBufferSize) ? rumble.capabilities.maxBufferSize : numSamples;

                float sampleInterval = (1.0f / (float)rumble.capabilities.frequencyHz) * interval;

                float currentSampleTime = Time.realtimeSinceStartup * interval;
                byte[] samples = new byte[numSamples];
                for (uint i = 0; i < numSamples; i++)
                {
                    samples[i] = (byte)(Mathf.Sin(currentSampleTime) * 255);
                    currentSampleTime += sampleInterval;
                }

                rumble.EnqueueRumble(0, samples);
            }
        }
	}
}
