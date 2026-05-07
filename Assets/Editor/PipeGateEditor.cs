using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Environment;

/// <summary>
/// Custom Scene editor for PipeGate.
/// Draws draggable handles for each path point directly in the Scene view.
/// Use the Inspector buttons to Add / Remove / Insert points, then
/// just drag them with the standard Move tool in the Scene.
/// </summary>
[CustomEditor(typeof(PipeGate))]
public class PipeGateEditor : Editor
{
    // Colours
    private static readonly Color CURVE_COLOR   = new Color(0.2f, 0.9f, 1f, 1f);    // cyan
    private static readonly Color POINT_COLOR   = new Color(1f,   0.85f, 0.1f, 1f); // yellow
    private static readonly Color TANGENT_COLOR = new Color(1f,   0.85f, 0.1f, 0.4f);
    private static readonly Color START_COLOR   = new Color(0.2f, 1f,   0.2f, 1f);  // green
    private static readonly Color END_COLOR     = new Color(1f,   0.2f, 0.2f, 1f);  // red

    private PipeGate _pipe;
    private int _selectedIndex = -1;

    private void OnEnable()
    {
        _pipe = (PipeGate)target;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INSPECTOR GUI
    // ─────────────────────────────────────────────────────────────────────────
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Path Editor", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("➕  Add Point at End"))
        {
            AddPointAtEnd();
        }
        GUI.enabled = _selectedIndex >= 0 && _selectedIndex < _pipe.pathPoints.Count;
        if (GUILayout.Button("Insert After Selected"))
        {
            InsertAfterSelected();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = _selectedIndex >= 0 && _selectedIndex < _pipe.pathPoints.Count
                      && _pipe.pathPoints.Count > 2;
        if (GUILayout.Button("🗑  Remove Selected"))
        {
            RemoveSelected();
        }
        GUI.enabled = true;
        if (GUILayout.Button("Clear All Points"))
        {
            if (EditorUtility.DisplayDialog("Clear Path", "Remove all path points?", "Yes", "Cancel"))
                ClearAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Show each point as a labelled row
        if (_pipe.pathPoints != null && _pipe.pathPoints.Count > 0)
        {
            EditorGUILayout.LabelField("Points", EditorStyles.miniBoldLabel);
            for (int i = 0; i < _pipe.pathPoints.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // Highlight selected
                GUI.backgroundColor = (i == _selectedIndex)
                    ? new Color(0.4f, 0.8f, 1f)
                    : Color.white;

                string label = i == 0 ? "  [START]" : (i == _pipe.pathPoints.Count - 1) ? "  [END]" : $"  [{i}]";
                if (GUILayout.Button(label, GUILayout.Width(80)))
                {
                    _selectedIndex = i;
                    // Focus the scene view on this point
                    if (_pipe.pathPoints[i] != null)
                        SceneView.lastActiveSceneView?.Frame(new Bounds(_pipe.pathPoints[i].position, Vector3.one * 2f), false);
                }

                GUI.backgroundColor = Color.white;
                EditorGUI.BeginChangeCheck();
                Transform pt = (Transform)EditorGUILayout.ObjectField(_pipe.pathPoints[i], typeof(Transform), true);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_pipe, "Change Path Point");
                    _pipe.pathPoints[i] = pt;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        if (GUI.changed) EditorUtility.SetDirty(_pipe);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SCENE GUI — Handles
    // ─────────────────────────────────────────────────────────────────────────
    private void OnSceneGUI()
    {
        if (_pipe == null || _pipe.pathPoints == null) return;

        DrawCurve();
        DrawHandles();
    }

    private void DrawCurve()
    {
        List<Transform> pts = _pipe.pathPoints;
        if (pts.Count < 2) return;

        Handles.color = CURVE_COLOR;
        int segments = Mathf.Max(pts.Count * 15, 30);
        Vector3 prev = EvaluateCatmullRom(pts, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 curr = EvaluateCatmullRom(pts, t);
            Handles.DrawLine(prev, curr);
            prev = curr;
        }

        // Draw faint skeleton lines between points
        Handles.color = TANGENT_COLOR;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (pts[i] != null && pts[i + 1] != null)
                Handles.DrawDottedLine(pts[i].position, pts[i + 1].position, 4f);
        }
    }

    private void DrawHandles()
    {
        List<Transform> pts = _pipe.pathPoints;

        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i] == null) continue;

            // Choose colour: green for start, red for end, yellow otherwise
            if (i == 0)             Handles.color = START_COLOR;
            else if (i == pts.Count - 1) Handles.color = END_COLOR;
            else                    Handles.color = POINT_COLOR;

            Vector3 pos = pts[i].position;
            float size = HandleUtility.GetHandleSize(pos) * 0.15f;

            // Clickable sphere to select the point
            if (Handles.Button(pos, Quaternion.identity, size, size * 1.5f, Handles.SphereHandleCap))
            {
                _selectedIndex = i;
                Repaint();
            }

            // Label
            Handles.Label(pos + Vector3.up * (size * 2f),
                i == 0 ? "START" : (i == pts.Count - 1 ? "END" : $"P{i}"),
                new GUIStyle { normal = { textColor = (i == _selectedIndex) ? Color.white : Color.yellow },
                               fontStyle = FontStyle.Bold, fontSize = 11 });

            // Draggable position handle on selected point
            if (i == _selectedIndex)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(pos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pts[i], "Move Pipe Path Point");
                    pts[i].position = newPos;
                    EditorUtility.SetDirty(pts[i]);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Point management helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void AddPointAtEnd()
    {
        Undo.RecordObject(_pipe, "Add Pipe Point");
        Vector3 newPos = _pipe.pathPoints.Count > 0 && _pipe.pathPoints[_pipe.pathPoints.Count - 1] != null
            ? _pipe.pathPoints[_pipe.pathPoints.Count - 1].position + new Vector3(0, 0, 1)
            : _pipe.transform.position;

        Transform t = CreatePointTransform($"PathPoint_{_pipe.pathPoints.Count}", newPos);
        _pipe.pathPoints.Add(t);
        _selectedIndex = _pipe.pathPoints.Count - 1;
        EditorUtility.SetDirty(_pipe);
    }

    private void InsertAfterSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _pipe.pathPoints.Count) return;
        Undo.RecordObject(_pipe, "Insert Pipe Point");

        Transform cur = _pipe.pathPoints[_selectedIndex];
        Transform next = (_selectedIndex + 1 < _pipe.pathPoints.Count) ? _pipe.pathPoints[_selectedIndex + 1] : null;

        Vector3 midPos = cur != null && next != null
            ? (cur.position + next.position) * 0.5f
            : (cur != null ? cur.position + new Vector3(0, 0, 1) : _pipe.transform.position);

        Transform t = CreatePointTransform($"PathPoint_{_selectedIndex + 1}", midPos);
        _pipe.pathPoints.Insert(_selectedIndex + 1, t);
        _selectedIndex = _selectedIndex + 1;
        RenameAllPoints();
        EditorUtility.SetDirty(_pipe);
    }

    private void RemoveSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _pipe.pathPoints.Count) return;
        Undo.RecordObject(_pipe, "Remove Pipe Point");

        Transform t = _pipe.pathPoints[_selectedIndex];
        _pipe.pathPoints.RemoveAt(_selectedIndex);
        if (t != null) Undo.DestroyObjectImmediate(t.gameObject);

        _selectedIndex = Mathf.Clamp(_selectedIndex - 1, 0, _pipe.pathPoints.Count - 1);
        RenameAllPoints();
        EditorUtility.SetDirty(_pipe);
    }

    private void ClearAll()
    {
        Undo.RecordObject(_pipe, "Clear Pipe Path");
        foreach (var pt in _pipe.pathPoints)
        {
            if (pt != null) Undo.DestroyObjectImmediate(pt.gameObject);
        }
        _pipe.pathPoints.Clear();
        _selectedIndex = -1;
        EditorUtility.SetDirty(_pipe);
    }

    private Transform CreatePointTransform(string name, Vector3 worldPos)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Path Point");
        go.transform.SetParent(_pipe.transform);
        go.transform.position = worldPos;
        return go.transform;
    }

    private void RenameAllPoints()
    {
        for (int i = 0; i < _pipe.pathPoints.Count; i++)
        {
            if (_pipe.pathPoints[i] != null)
                _pipe.pathPoints[i].gameObject.name = $"PathPoint_{i}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Catmull-Rom evaluation (mirrors PipeGate's runtime version)
    // ─────────────────────────────────────────────────────────────────────────
    private static Vector3 EvaluateCatmullRom(List<Transform> pts, float t)
    {
        if (pts.Count == 0) return Vector3.zero;
        if (pts.Count == 1) return pts[0] != null ? pts[0].position : Vector3.zero;
        if (pts.Count == 2)
        {
            Vector3 a = pts[0] != null ? pts[0].position : Vector3.zero;
            Vector3 b = pts[1] != null ? pts[1].position : Vector3.zero;
            return Vector3.Lerp(a, b, t);
        }

        t = Mathf.Clamp01(t);
        if (t >= 1f) return pts[pts.Count - 1] != null ? pts[pts.Count - 1].position : Vector3.zero;

        int numSections = pts.Count - 1;
        int currPt = Mathf.FloorToInt(t * numSections);
        float u = (t * numSections) - currPt;

        Vector3 p0 = pts[Mathf.Max(currPt - 1, 0)].position;
        Vector3 p1 = pts[currPt].position;
        Vector3 p2 = pts[Mathf.Min(currPt + 1, pts.Count - 1)].position;
        Vector3 p3 = pts[Mathf.Min(currPt + 2, pts.Count - 1)].position;

        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * u +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) +
            (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u)
        );
    }
}
