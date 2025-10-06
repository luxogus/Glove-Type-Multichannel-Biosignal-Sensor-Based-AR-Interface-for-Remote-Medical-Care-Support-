using MixedReality.Toolkit.UX.Experimental;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SubsystemsImplementation;
using UnityEngine.UI;
using UnityEngine.XR;
//using UnityEngine.UI.Extensions;
using UnityEngine.XR.Hands;

public class MRTK3HandVisualizer : MonoBehaviour
{
    [Header("Sphere Settings")]
    public GameObject spherePrefab;
    public float sphereScale = 0.01f;
    public float colorChangeInterval = 1.0f;

    [Header("TCP / RMS Settings")]
    public TCPClient TCPconnection;
    public int channelIndex = 0;
    public RHXWaveformClient client;
    public RmsBallVisualizer ballVIS;
    public float minRms = 30000f;
    public float maxRms = 32500f;
    public enum HandSide { Left, Right };
    public HandSide handToShow = HandSide.Left;
    public Gradient colorGradient;

    [Header("UI")]
    public Button leftHandButton;
    public Button rightHandButton;
    public TextMeshProUGUI IsConnected;
    public TextMeshProUGUI modeText;
    public TextMeshProUGUI IPaddress_value;
    public GameObject WristPlate;
    public Transform cam;
    private XRHandSubsystem handSubsystem;
    private Dictionary<UIRPCustomJointID, GameObject> jointSpheresLeft = new();
    private Dictionary<UIRPCustomJointID, GameObject> jointSpheresRight = new();

    #region Joint Definitions
    private enum UIRPCustomJointID
    {
        Wrist = XRHandJointID.Wrist,
        ThumbMetacarpal = XRHandJointID.ThumbMetacarpal,
        ThumbProximal = XRHandJointID.ThumbProximal,
        IndexMetacarpal = XRHandJointID.IndexMetacarpal,
        IndexProximal = XRHandJointID.IndexProximal,
        MiddleMetacarpal = XRHandJointID.MiddleMetacarpal,
        MiddleProximal = XRHandJointID.MiddleProximal,
        RingMetacarpal = XRHandJointID.RingMetacarpal,
        RingProximal = XRHandJointID.RingProximal,
        LittleMetacarpal = XRHandJointID.LittleMetacarpal,
        LittleProximal = XRHandJointID.LittleProximal,

        ThumbDistal = XRHandJointID.ThumbDistal,
        ThumbTip = XRHandJointID.ThumbTip,
        IndexIntermediate = XRHandJointID.IndexIntermediate,
        IndexDistal = XRHandJointID.IndexDistal,
        IndexTip = XRHandJointID.IndexTip,
        MiddleIntermediate = XRHandJointID.MiddleIntermediate,
        MiddleDistal = XRHandJointID.MiddleDistal,
        MiddleTip = XRHandJointID.MiddleTip,
        RingIntermediate = XRHandJointID.RingIntermediate,
        RingDistal = XRHandJointID.RingDistal,
        RingTip = XRHandJointID.RingTip,
        LittleIntermediate = XRHandJointID.LittleIntermediate,
        LittleDistal = XRHandJointID.LittleDistal,
        LittleTip = XRHandJointID.LittleTip,

        CustomJoint1 = 1000, // A16부터 차례대로
        CustomJoint2 = 1001, // A17
        CustomJoint3 = 1002, // A18
        CustomJoint4 = 1003, // A19
        CustomJoint5 = 1004, // A20
        CustomJoint6 = 1005, // A21
        CustomJoint7 = 1006, // A22
        CustomJoint8 = 1007, // A23
        CustomJoint9 = 1008, // A24
        CustomJoint10 = 1009, // A25
        CustomJoint11 = 1010, // A26
        CustomJoint12 = 1011, // A27
        CustomJoint13 = 1012, // A28
        CustomJoint14 = 1013, // A29
        CustomJoint15 = 1014, // A30
    }

    private UIRPCustomJointID[] jointsToTrack = new UIRPCustomJointID[]
    {
        UIRPCustomJointID.Wrist, UIRPCustomJointID.ThumbMetacarpal, UIRPCustomJointID.ThumbProximal,
        UIRPCustomJointID.ThumbDistal, UIRPCustomJointID.ThumbTip, UIRPCustomJointID.IndexMetacarpal,
        UIRPCustomJointID.IndexProximal, UIRPCustomJointID.IndexIntermediate, UIRPCustomJointID.IndexDistal,
        UIRPCustomJointID.IndexTip, UIRPCustomJointID.MiddleMetacarpal, UIRPCustomJointID.MiddleProximal,
        UIRPCustomJointID.MiddleIntermediate, UIRPCustomJointID.MiddleDistal, UIRPCustomJointID.MiddleTip,
        UIRPCustomJointID.RingMetacarpal, UIRPCustomJointID.RingProximal, UIRPCustomJointID.RingIntermediate,
        UIRPCustomJointID.RingDistal, UIRPCustomJointID.RingTip, UIRPCustomJointID.LittleMetacarpal,
        UIRPCustomJointID.LittleProximal, UIRPCustomJointID.LittleIntermediate, UIRPCustomJointID.LittleDistal,
        UIRPCustomJointID.LittleTip, UIRPCustomJointID.CustomJoint1, UIRPCustomJointID.CustomJoint2,
        UIRPCustomJointID.CustomJoint3, UIRPCustomJointID.CustomJoint4, UIRPCustomJointID.CustomJoint5,
        UIRPCustomJointID.CustomJoint6, UIRPCustomJointID.CustomJoint7, UIRPCustomJointID.CustomJoint8,
        UIRPCustomJointID.CustomJoint9, UIRPCustomJointID.CustomJoint10, UIRPCustomJointID.CustomJoint11,
        UIRPCustomJointID.CustomJoint12, UIRPCustomJointID.CustomJoint13, UIRPCustomJointID.CustomJoint14,
        UIRPCustomJointID.CustomJoint15
    };

    private Dictionary<UIRPCustomJointID, string> jointNames = new Dictionary<UIRPCustomJointID, string>()
    {
        {UIRPCustomJointID.ThumbTip, "64"}, {UIRPCustomJointID.Wrist, "Wrist"},
        {UIRPCustomJointID.ThumbMetacarpal, "ThumbMetacarpal"},{UIRPCustomJointID.MiddleMetacarpal, "MiddleMetacarpal"},
        {UIRPCustomJointID.ThumbProximal, "ThumbProximal"}, {UIRPCustomJointID.IndexMetacarpal, "IndexMetacarpal"},
        {UIRPCustomJointID.RingMetacarpal, "RingMetacarpal"},{UIRPCustomJointID.LittleMetacarpal, "LittleMetacarpal"},
        {UIRPCustomJointID.LittleTip, "63"}, {UIRPCustomJointID.LittleDistal, "61"}, {UIRPCustomJointID.LittleIntermediate, "59"},
        {UIRPCustomJointID.RingTip, "57"}, {UIRPCustomJointID.RingDistal, "55"}, {UIRPCustomJointID.RingIntermediate, "53"},
        {UIRPCustomJointID.MiddleTip, "51"}, {UIRPCustomJointID.MiddleDistal, "49"}, {UIRPCustomJointID.MiddleIntermediate, "48"},
        {UIRPCustomJointID.IndexTip, "50"}, {UIRPCustomJointID.IndexDistal, "52"}, {UIRPCustomJointID.IndexIntermediate, "54"},
        {UIRPCustomJointID.LittleProximal, "56"}, {UIRPCustomJointID.RingProximal, "58"}, {UIRPCustomJointID.MiddleProximal, "60"},
        {UIRPCustomJointID.IndexProximal, "62"}, {UIRPCustomJointID.CustomJoint1, "0"}, {UIRPCustomJointID.CustomJoint2, "2"},  
        {UIRPCustomJointID.CustomJoint3, "4"}, {UIRPCustomJointID.CustomJoint4, "6"}, {UIRPCustomJointID.CustomJoint5, "8"}, 
        {UIRPCustomJointID.CustomJoint6, "10"}, {UIRPCustomJointID.CustomJoint7, "12"}, {UIRPCustomJointID.CustomJoint8, "14"}, 
        {UIRPCustomJointID.CustomJoint9, "15"}, {UIRPCustomJointID.CustomJoint10, "13"}, {UIRPCustomJointID.CustomJoint11, "11"}, 
        {UIRPCustomJointID.CustomJoint12, "9"}, {UIRPCustomJointID.CustomJoint13, "7"}, {UIRPCustomJointID.CustomJoint14, "5"}, 
        {UIRPCustomJointID.CustomJoint15, "3"}, {UIRPCustomJointID.ThumbDistal, "1" },
    };

    Vector3 GetCustomJointPosition(XRHand hand, UIRPCustomJointID id)
    {
        if (!hand.isTracked) return Vector3.zero;

        var wrist = hand.GetJoint(XRHandJointID.Wrist);
        if (!wrist.TryGetPose(out var wristPose)) return Vector3.zero;

        var index = hand.GetJoint(XRHandJointID.IndexProximal);
        if (!index.TryGetPose(out var indexPose)) return Vector3.zero;

        var middle = hand.GetJoint(XRHandJointID.MiddleProximal);
        if (!middle.TryGetPose(out var middlePose)) return Vector3.zero;

        var ring = hand.GetJoint(XRHandJointID.RingProximal);
        if (!ring.TryGetPose(out var ringPose)) return Vector3.zero;

        var little = hand.GetJoint(XRHandJointID.LittleProximal);
        if (!little.TryGetPose(out var littlePose)) return Vector3.zero;

        var index_meta = hand.GetJoint(XRHandJointID.IndexMetacarpal);
        if (!index_meta.TryGetPose(out var index_metaPose)) return Vector3.zero;

        var middle_meta = hand.GetJoint(XRHandJointID.MiddleMetacarpal);
        if (!middle_meta.TryGetPose(out var middle_metaPose)) return Vector3.zero;

        var ring_meta = hand.GetJoint(XRHandJointID.RingMetacarpal);
        if (!ring_meta.TryGetPose(out var ring_metaPose)) return Vector3.zero;

        var little_meta = hand.GetJoint(XRHandJointID.LittleMetacarpal);
        if (!little_meta.TryGetPose(out var little_metaPose)) return Vector3.zero;

        switch (id)
        {
            case UIRPCustomJointID.CustomJoint1: // A0
                return (2 * (indexPose.position + ((indexPose.position + index_metaPose.position) / 2)) / 2 - (middlePose.position + ((middlePose.position + middle_metaPose.position) / 2)) / 2);
            case UIRPCustomJointID.CustomJoint2: // A2
                return (littlePose.position + ((littlePose.position + little_metaPose.position) / 2)) / 2;
            case UIRPCustomJointID.CustomJoint3: // A4
                return (ringPose.position + ((ringPose.position + ring_metaPose.position) / 2)) / 2;
            case UIRPCustomJointID.CustomJoint4: // A6
                return (middlePose.position + ((middlePose.position + middle_metaPose.position) / 2)) / 2;
            case UIRPCustomJointID.CustomJoint5: // A8
                return (indexPose.position + ((indexPose.position + index_metaPose.position) / 2)) / 2;
            case UIRPCustomJointID.CustomJoint6: // A10
                return (2 * (indexPose.position + index_metaPose.position) / 2) - (middlePose.position + middle_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint7: // A12
                return (littlePose.position + little_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint8: // A14
                return (ringPose.position + ring_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint9: // A15
                return (middlePose.position + middle_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint10: // A13
                return (indexPose.position + index_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint11: // A11
                return (2* (((indexPose.position + index_metaPose.position) / 2) + index_metaPose.position) / 2) - (((middlePose.position + middle_metaPose.position) / 2) + middle_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint12: // A09
                return (((littlePose.position + little_metaPose.position) / 2) + little_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint13: // A07
                return (((ringPose.position + ring_metaPose.position) / 2) + ring_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint14: // A05
                return (((middlePose.position + middle_metaPose.position) / 2) + middle_metaPose.position) / 2;
            case UIRPCustomJointID.CustomJoint15: // A03
                return (((indexPose.position + index_metaPose.position) / 2) + index_metaPose.position) / 2;
            default:
                return Vector3.zero;
        }
    }

    #endregion

    void Start()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            handSubsystem.Start();
        }
        else
        {
            Debug.LogWarning("No XRHandSubsystem found! Using fake hand simulation.");
        }

        foreach (var jointId in jointsToTrack)
        {
            int channelIndexLeft = GetChannelIndexForJoint(jointId);
            int channelIndexRight = GetChannelIndexForJoint(jointId);
            if (channelIndexLeft == 0 && !jointNames[jointId].Equals("0"))
            {
                continue;
            }
            if (channelIndexLeft == 0 && !jointNames[jointId].Equals("0"))
            {
                continue;
            }
            if (Camera.main != null)
            {
                cam = Camera.main.transform;
            }

            var sphereL = Instantiate(spherePrefab, transform, false);
            sphereL.name = "A" + jointNames[jointId];
            sphereL.transform.localScale = Vector3.one * sphereScale;

            // Color 매칭하는 부분
            var rmsVisL = sphereL.GetComponent<RmsBallVisualizer>();
            if (rmsVisL == null) rmsVisL = sphereL.AddComponent<RmsBallVisualizer>();
            rmsVisL.rhx = TCPconnection;  // TCPClient 할당
            rmsVisL.channelIndex = GetChannelIndexForJoint(jointId); // 조인트별 채널 매핑
            rmsVisL.minRms = minRms;
            rmsVisL.maxRms = maxRms;
            rmsVisL.colorGradient = colorGradient;
            jointSpheresLeft[jointId] = sphereL; // sphere들을 제목을 붙여서 배치하는 코드.

            // sphere 생성 후 라벨 붙이기
            var textObjL = new GameObject("Label");
            textObjL.transform.SetParent(sphereL.transform, false);
            textObjL.transform.localPosition = Vector3.back * 2f; // 구체 위쪽에 위치
            

            var tmpL = textObjL.AddComponent<TextMeshPro>();
            tmpL.fontSize = 5.0f;
            tmpL.alignment = TextAlignmentOptions.Center;
            tmpL.text = jointNames[jointId];    
            tmpL.transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
            rmsVisL.label = tmpL;  // RmsBallVisualizer에 연결


            var sphereR = Instantiate(spherePrefab, transform, false);
            sphereR.name = "A" + jointNames[jointId];
            sphereR.transform.localScale = Vector3.one * sphereScale;

            // Color 매칭하는 부분
            var rmsVisR = sphereR.GetComponent<RmsBallVisualizer>();
            if (rmsVisR == null) rmsVisR = sphereR.AddComponent<RmsBallVisualizer>();
            rmsVisR.rhx = TCPconnection;
            rmsVisR.channelIndex = GetChannelIndexForJoint(jointId); // 같은 방식으로 매핑
            rmsVisR.minRms = minRms;
            rmsVisR.maxRms = maxRms;
            rmsVisR.colorGradient = colorGradient;
            jointSpheresRight[jointId] = sphereR;

            var textObjR = new GameObject("Label");
            textObjR.transform.SetParent(sphereR.transform, false);
            textObjR.transform.localPosition = Vector3.back * 2f; // 구체 위쪽에 위치
            var tmpR = textObjR.AddComponent<TextMeshPro>();
            tmpR.fontSize = 5.0f;
            tmpR.alignment = TextAlignmentOptions.Center;
            tmpR.text = jointNames[jointId];
            tmpR.transform.rotation = Quaternion.LookRotation(cam.forward, cam.up);
            rmsVisR.label = tmpR;


        }
    }

    void UpdateButtonColors()
    {
        if (leftHandButton != null && rightHandButton != null)
        {
            leftHandButton.GetComponent<Image>().color = (handToShow == HandSide.Left) ? Color.green : Color.gray;
            rightHandButton.GetComponent<Image>().color = (handToShow == HandSide.Right) ? Color.green : Color.gray;
        }
    }

    void Update()
    {
        IPaddress_value.text = $"{TCPconnection.host}";
        if (TCPconnection.IsConnected)
        {
            IsConnected.text = "Connected";
            IsConnected.color = Color.green;

        }
        else
        {
            IsConnected.text = "Not Connected";
            IsConnected.color = Color.red;
        }
        if (handSubsystem != null && handSubsystem.running)
        {
            if (handToShow == HandSide.Left)
            {
                if (handSubsystem.leftHand.isTracked)
                    UpdateHandJoints(handSubsystem.leftHand, jointSpheresLeft);
            }

            if (handToShow == HandSide.Right)
            {
                if (handSubsystem.rightHand.isTracked)
                    UpdateHandJoints(handSubsystem.rightHand, jointSpheresRight);
            }
        }
        //WristWaveformSlate(handToShow);
    }

    void UpdateHandJoints(XRHand hand, Dictionary<UIRPCustomJointID, GameObject> sphereDict)
    {
        if (!hand.isTracked)
            return;

        foreach (var jointId in jointsToTrack)
        {
            // XRHandJointID 범위 내 joint만 처리 (0~25)
            int jointIndex = (int)jointId;
            if (jointIndex < 0 || jointIndex > 26)  // 26은 XRHandJointID 개수
            {
                // 커스텀 조인트는 여기에 따로 처리하거나 무시
                Vector3 customPos = GetCustomJointPosition(hand, jointId);
                if (sphereDict.TryGetValue(jointId, out var sphere))
                {
                    if (customPos == Vector3.zero)
                    {
                        sphere.SetActive(false);
                    }
                    else
                    {
                        sphere.SetActive(true);
                        sphere.transform.localPosition = customPos;
                        sphere.transform.localRotation = Quaternion.identity; // 회전은 임의로 설정
                    }
                }
                continue;
            }

            XRHandJoint joint;
            try
            {
                joint = hand.GetJoint((XRHandJointID)jointId);
            }
            catch (System.ObjectDisposedException)
            {
                return;
            }

            
            if (joint.TryGetPose(out var pose))
            {
                if (sphereDict.TryGetValue(jointId, out var sphere))
                {
                    sphere.SetActive(true);
                    sphere.transform.localPosition = pose.position;
                    sphere.transform.rotation = Quaternion.identity;
                }
            }
            else
            {
                if (sphereDict.TryGetValue(jointId, out var sphere))
                {
                    sphere.SetActive(false);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (handSubsystem != null && handSubsystem.running)
        {
            handSubsystem.Stop();
        }
    }

    public void StartMeasure()
    {
        modeText.text = "ACTIVATED";
        modeText.color = Color.green;
        foreach (var sphere in jointSpheresLeft.Values)
        {
            sphere.SetActive(true);
            var rmsVis = sphere.GetComponent<RmsBallVisualizer>();
            rmsVis?.OnEnable();
        }
        foreach (var sphere in jointSpheresRight.Values)
        {
            sphere.SetActive(true);
            var rmsVis = sphere.GetComponent<RmsBallVisualizer>();
            rmsVis?.OnEnable();
        }
    }

    public void StopMeasure()
    {
        modeText.text = "DEACTIVATED";
        modeText.color = Color.yellow;

        // 모든 구체 색을 완전 투명으로
        foreach (var sphere in jointSpheresLeft.Values)
        {
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0, 0, 0, 0); // 투명
            }
            var rmsVis = sphere.GetComponent<RmsBallVisualizer>();
            rmsVis?.OnDisable();
        }
        foreach (var sphere in jointSpheresRight.Values)
        {
            var renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0, 0, 0, 0); // 투명
            }
            var rmsVis = sphere.GetComponent<RmsBallVisualizer>();
            rmsVis?.OnDisable();
        }
    }

    private int GetChannelIndexForJoint(UIRPCustomJointID jointId)
    {
        if (jointNames.TryGetValue(jointId, out var name))
        {
            if (int.TryParse(name, out int channel))
            {
                return channel; // "0","1","2" → 채널 번호
            }
        }
        return 0; // 못 찾으면 기본 0번 채널
    }

    public void SelectHand(HandSide hand)
    {
        // 이전 손 비활성화
        var prevSpheres = (handToShow == HandSide.Left) ? jointSpheresLeft : jointSpheresRight;
        foreach (var sphere in prevSpheres.Values)
        {
            sphere.SetActive(false);
        }

        handToShow = hand;

        // 선택한 손 sphere 활성화
        var newSpheres = (handToShow == HandSide.Left) ? jointSpheresLeft : jointSpheresRight;
        foreach (var sphere in newSpheres.Values)
        {
            sphere.SetActive(true);
        }

        UpdateButtonColors();
    }

    public void OnHandButtonPressed(string hand)
    {
        if (hand.Equals("Left", System.StringComparison.OrdinalIgnoreCase))
        {
            SelectHand(HandSide.Left);
        }
        else if (hand.Equals("Right", System.StringComparison.OrdinalIgnoreCase))
        {
            SelectHand(HandSide.Right);
        }
    }

    public void WristWaveformSlate(HandSide handToShow)
    {
        Transform mainCam = Camera.main.transform;

        if (handToShow == HandSide.Left)
        {
            if (handSubsystem.leftHand.isTracked)
            {
                var wrist = handSubsystem.leftHand.GetJoint(XRHandJointID.Wrist);
                var middle_meta = handSubsystem.leftHand.GetJoint(XRHandJointID.MiddleMetacarpal);
                if (wrist.TryGetPose(out var wristPose))
                {
                    middle_meta.TryGetPose(out var middle_metaPose);
                    WristPlate.transform.localPosition = 2 * wristPose.position - middle_metaPose.position; // 손목 위로 약간 올림
                    Vector3 dirToCam = (mainCam.position - WristPlate.transform.position).normalized;
                    Quaternion lookRot = Quaternion.LookRotation(dirToCam, wristPose.up);
                    Vector3 euler = lookRot.eulerAngles;
                    euler.z = 0; // Z축 회전 고정
                    WristPlate.transform.localRotation = Quaternion.Euler(euler);
                }
            }
        }
        else if (handToShow == HandSide.Right)
        {
            if (handSubsystem.rightHand.isTracked)
            {
                var wrist = handSubsystem.rightHand.GetJoint(XRHandJointID.Wrist);
                var middle_meta = handSubsystem.rightHand.GetJoint(XRHandJointID.MiddleMetacarpal);
                if (wrist.TryGetPose(out var wristPose))
                {
                    middle_meta.TryGetPose(out var middle_metaPose);
                    WristPlate.transform.localPosition = 2 * wristPose.position - middle_metaPose.position; // 손목 위로 약간 올림
                    Vector3 dirToCam = (mainCam.position - WristPlate.transform.position).normalized;
                    Quaternion lookRot = Quaternion.LookRotation(dirToCam, wristPose.up);
                    Vector3 euler = lookRot.eulerAngles;
                    euler.z = 0; // Z축 회전 고정
                    WristPlate.transform.localRotation = Quaternion.Euler(euler);
                }
            }
        }
    }

}
