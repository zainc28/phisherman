using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    private Vector3 targetPosition;
    private bool isMoving = false;

    // If a MapNavAgent is on this object, it owns all movement — this script does nothing.
    private MapNavAgent _nav;

    void Start()
    {
        targetPosition = transform.position;
        _nav = GetComponent<MapNavAgent>();
    }

    void Update()
    {
        // Yield to MapNavAgent when present (WorldMap scenes)
        if (_nav != null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = -1f; // keep Phisherman's Z position
            targetPosition = mousePos;
            isMoving = true;
        }

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.05f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }
}