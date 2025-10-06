//using UnityEngine;

//[RequireComponent(typeof(LineRenderer))]
//public class WristGraph : MonoBehaviour
//{
//    public int resolution = 100;   // 점 개수
//    public float xStart = -5f;
//    public float xEnd = 5f;
//    public float scale = 1f;       // 그래프 크기

//    private LineRenderer lineRenderer;

//    void Start()
//    {
//        lineRenderer = GetComponent<LineRenderer>();
//        lineRenderer.positionCount = resolution;

//        DrawGraph();
//    }

//    void DrawGraph()
//    {
//        float step = (xEnd - xStart) / (resolution - 1);

//        for (int i = 0; i < resolution; i++)
//        {
//            float x = xStart + step * i;
//            float y = Mathf.Sin(x);   // 여기서 원하는 수식을 넣으면 됨
//            Vector3 pos = new Vector3(x, y, 0) * scale;

//            // Plane 위에 그리려면, Plane이 (XZ 평면) 위에 있으니까 좌표를 바꿔줌
//            pos = new Vector3(x, 0.01f, y); // 살짝 띄워서 겹치지 않게
//            lineRenderer.SetPosition(i, pos);
//        }
//    }
//}
