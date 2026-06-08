using UnityEngine;

/// <summary>
/// Lets you drag the Player GameObject around with the mouse in Play mode.
/// Attach to the Player. Works in 2D — converts mouse screen position to world XY.
/// No allocation — just reading Input and setting position.
/// </summary>
public class PlayerDrag : MonoBehaviour
{
    private bool    _dragging;
    private Vector3 _offset;
    private Camera  _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void OnMouseDown()
    {
        _dragging = true;
        _offset   = transform.position - GetMouseWorldPos();
    }

    private void OnMouseUp()
    {
        _dragging = false;
    }

    private void Update()
    {
        if (!_dragging) return;
        Vector3 pos = GetMouseWorldPos() + _offset;
        pos.z = 0f; // keep on 2D plane
        transform.position = pos;
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mp = Input.mousePosition;
        mp.z = Mathf.Abs(_cam.transform.position.z);
        return _cam.ScreenToWorldPoint(mp);
    }
}
