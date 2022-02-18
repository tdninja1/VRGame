using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[System.Serializable]
struct DeviceData{
    public string[] deviceNames;
    public Vector3 position;
    public Vector3 rotation;

    public DeviceData(string name, Vector3 pos, Vector3 rot)
    {
        deviceNames = new string[] { name };
        position = pos;
        rotation = rot;
    }
    public DeviceData(string[] names, Vector3 pos, Vector3 rot)
    {
        deviceNames = names;
        position = pos;
        rotation = rot;
    }
};


public class XRHandOffset : MonoBehaviour
{
    [Tooltip("This is the device that you are using to setup the innital proper orientation of the hand, all offsets are relative to this device")]
    public string defaultDevice = "Oculus";
    [SerializeField] 
    private Transform[] rightOffsets, leftOffsets;

    [SerializeField]
    DeviceData[] devices = new DeviceData[] {
        new DeviceData("Oculus", new Vector3(0.005f, -0.016f, 0.014f), new Vector3(48, 0, 15)),
        new DeviceData("Windows MR", new Vector3(0.003f, -0.005f, -0.078f), new Vector3(36, -12, 2)),
        new DeviceData(new string[]{"Vive", "HTC", "Index", "Cosmos", "Elite" }, new Vector3(0.015f, 0, 0.0412f), new Vector3(30, -17, 0))
    };

    void OnEnable(){
        InputDevices.deviceConnected += DeviceConnected;
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        foreach (var device in devices)
            DeviceConnected(device);
    }

    void OnDisable(){
        InputDevices.deviceConnected -= DeviceConnected;
    }

    void DeviceConnected(InputDevice inputDevice){
        bool done = false;
        // The Left Hand
        if (inputDevice.characteristics != 0){
            foreach (var device in devices){
                if (done)
                    break;

                for (int i = 0; i < device.deviceNames.Length; i++){
                    if (inputDevice.name.Contains(device.deviceNames[i])){
                        var offsetPos = GetPositionOffset(defaultDevice, device.deviceNames[i]);
                        var offsetRot = GetRotationOffset(defaultDevice, device.deviceNames[i]);

                        foreach (var leftOffset in leftOffsets){
                            leftOffset.localPosition += new Vector3(-offsetPos.x, offsetPos.y, offsetPos.z);
                            leftOffset.localEulerAngles += new Vector3(offsetRot.x, -offsetRot.y, -offsetRot.z);
                        }

                        foreach (var rightOffset in rightOffsets){
                            rightOffset.localPosition += offsetPos;
                            rightOffset.localEulerAngles += offsetRot;
                        }

                        OnDisable();

                        //print(device.deviceNames[i]);
                        done = true;
                        break;
                    }
                }
            }
        }
    }


    Vector3 GetPositionOffset(string from, string to){
        if (from == to)
            return Vector3.zero;

        Vector3 fromPos, toPos = fromPos = Vector3.zero;
        foreach (var device in devices){
            foreach (var deviceName in device.deviceNames){
                if (deviceName == from)
                    fromPos = device.position;
                if (deviceName == to)
                    toPos = device.position;
            }
        }

        return (toPos - fromPos);
    }


    Vector3 GetRotationOffset(string from, string to){
        if (from == to)
            return Vector3.zero;

        Vector3 fromPos, toPos = fromPos = Vector3.zero;
        foreach (var device in devices){
            foreach (var deviceName in device.deviceNames){
                if (deviceName == from)
                    fromPos = device.rotation;
                if (deviceName == to)
                    toPos = device.rotation;
            }
        }

        return (toPos - fromPos);
    }
}
