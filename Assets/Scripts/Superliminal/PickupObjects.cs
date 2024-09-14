using System.Collections.Generic;
using UnityEngine;

public class PickupObjects : MonoBehaviour
{
    [SerializeField]
    private Transform _cameraTransform;
    [Tooltip("Has to be a single layer")]
    [SerializeField]
    private LayerMask _pickupableLayer;
    [Tooltip("Has to be a single layer")]
    [SerializeField]
    private LayerMask _heldObjectLayer;
    [SerializeField]
    private LayerMask _wallLayers;
    private Transform _heldObject;
    private Rigidbody _heldObjectsRb;
    //Camera's (_cameraTransform's) local space, 4 furthest points of the held object
    private Vector3 _left;
    private Vector3 _right;
    private Vector3 _top;
    private Vector3 _bottom;
    private float _orgDistanceToScaleRatio;
    private Vector3 _orgViewportPos;

    private List<Vector3> _shapedGrid = new List<Vector3>();

    [SerializeField]
    private int NUMBER_OF_GRID_ROWS = 10;
    [SerializeField]
    private int NUMBER_OF_GRID_COLUMNS = 10;
    private const float SCALE_MARGIN = .001f;
    private void FixedUpdate()
    {
        if (_heldObject == null) return;

        MoveInFrontOfObstacles();

        UpdateScale();
    }
    private void MoveInFrontOfObstacles() 
    {
        if (_shapedGrid.Count == 0) throw new System.Exception("Shaped grid calculation error");

        float closestZ = 1000;
        for (int i = 0; i < _shapedGrid.Count; i++)
        {
            RaycastHit hit = CastTowardsGridPoint(_shapedGrid[i], _wallLayers + _pickupableLayer);   
            if (hit.collider == null) continue;

            Vector3 wallPoint = _cameraTransform.InverseTransformPoint(hit.point);
            if (i == 0 || wallPoint.z < closestZ)
            {
                //Find the closest point of the obstacle(s) to the camera
                closestZ = wallPoint.z;
            }
        }

        //Move the held object in front of the closestZ
        float boundsMagnitude = _heldObject.GetComponent<Renderer>().localBounds.extents.magnitude * _heldObject.localScale.x;
        Vector3 newLocalPos = _heldObject.localPosition;
        newLocalPos.z = closestZ - boundsMagnitude;
        _heldObject.localPosition = newLocalPos;
    }
    private void UpdateScale() 
    {
        float newScale = (_cameraTransform.position - _heldObject.position).magnitude / _orgDistanceToScaleRatio;
        if (Mathf.Abs(newScale - _heldObject.localScale.x) < SCALE_MARGIN) return;

        _heldObject.localScale = new Vector3(newScale, newScale, newScale);
        //By scaling we're actually changing the viewportPosition of heldObject and we don't want that
        Vector3 newPos = Camera.main.ViewportToWorldPoint(new Vector3(_orgViewportPos.x, _orgViewportPos.y,
            (_heldObject.position - _cameraTransform.position).magnitude));
        _heldObject.position = newPos;
    }
    private void OnDrawGizmos()
    {
        if (_heldObject == null) return;

        //Hits
        Gizmos.matrix = _cameraTransform.localToWorldMatrix;
        Gizmos.color = Color.green;
        foreach (Vector3 point in _shapedGrid)
        {
            Gizmos.DrawSphere(point, .01f);
        }
    }
    public void OnPickup()
    {
        if (_heldObject != null)
        {
            _heldObject.parent = null;
            _heldObjectsRb.useGravity = true;
            _heldObjectsRb.constraints = RigidbodyConstraints.None;
            _heldObject.gameObject.layer = (int)Mathf.Log(_pickupableLayer.value, 2);
            _heldObject = null;
            return;
        }

        RaycastHit hit;
        Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out hit, 100, _pickupableLayer);

        if (hit.collider == null) return;

        _heldObject = hit.collider.gameObject.transform;
        _heldObjectsRb = _heldObject.GetComponent<Rigidbody>();

        float scale = _heldObject.localScale.x;
        if (Mathf.Abs(scale - _heldObject.localScale.y) > SCALE_MARGIN
            || Mathf.Abs(scale - _heldObject.localScale.z) > SCALE_MARGIN)
            throw new System.Exception("Wrong Pickupable object's scale!"); //不让拿非等比例的物体
        _orgDistanceToScaleRatio = (_cameraTransform.position - _heldObject.position).magnitude / scale;

        _heldObject.gameObject.layer = (int)Mathf.Log(_heldObjectLayer.value, 2);
        _heldObjectsRb.useGravity = false;
        _heldObject.parent = _cameraTransform;
        _orgViewportPos = Camera.main.WorldToViewportPoint(_heldObject.position);
        _heldObjectsRb.constraints = RigidbodyConstraints.FreezeAll;

        Vector3[] bbPoints = GetBoundingBoxPoints();
        SetupShapedGrid(bbPoints);
    }

    #region Calculating grid
    private Vector3[] GetBoundingBoxPoints() 
    {
        Vector3 size = _heldObject.GetComponent<Renderer>().localBounds.size;
        Vector3 x = new Vector3(size.x, 0, 0);
        Vector3 y = new Vector3(0, size.y, 0);
        Vector3 z = new Vector3(0, 0, size.z);
        Vector3 min = _heldObject.GetComponent<Renderer>().localBounds.min;
        Vector3[] bbPoints =
            {
            min,
            min + x,
            min + y,
            min + x + y,
            min + z,
            min + z + x,
            min + z + y,
            min + z + x + y
            };
        return bbPoints;
    }

    private void SetupShapedGrid(Vector3[] bbPoints) 
    {
        _left = _right = _top = _bottom = Vector2.zero;
        GetRectConfines(bbPoints);
        
        Vector3[,] grid = SetupGrid();
        GetShapedGrid(grid);
    }

    private void GetRectConfines(Vector3[] bbPoints)
    {
        Vector3 worldBBPoint;
        Vector3 cameraPoint;
        Vector2 viewportPoint;
        // Vector3 closestPoint = _heldObject.GetComponent<Renderer>().localBounds.ClosestPoint(_cameraTransform.position);
        // float closestZ = _cameraTransform.InverseTransformPoint(_heldObject.TransformPoint(closestPoint)).z;
        Vector3 closestPoint = _heldObject.GetComponent<Renderer>().bounds.ClosestPoint(_cameraTransform.position);
        float closestZ = _cameraTransform.InverseTransformPoint(closestPoint).z;
        
        if (closestZ <= 0) throw new System.Exception("HeldObject's inside the player!");

        for (int i = 0; i < bbPoints.Length; i++)
        {
            worldBBPoint = _heldObject.TransformPoint(bbPoints[i]);
            viewportPoint = Camera.main.WorldToViewportPoint(worldBBPoint);
            cameraPoint = _cameraTransform.InverseTransformPoint(worldBBPoint);
            cameraPoint.z = closestZ;

            if (viewportPoint.x < 0 || viewportPoint.x > 1
                || viewportPoint.y < 0 || viewportPoint.y > 1) continue;    //位于相机视野之外的点不考虑

            if (i == 0) _left = _right = _top = _bottom = cameraPoint;

            if (cameraPoint.x < _left.x) _left = cameraPoint;
            if (cameraPoint.x > _right.x) _right = cameraPoint;
            if (cameraPoint.y > _top.y) _top = cameraPoint;
            if (cameraPoint.y < _bottom.y) _bottom = cameraPoint;
        }
    }

    private Vector3[,] SetupGrid() 
    {
        float rectHrLength = _right.x - _left.x;
        float rectVertLength = _top.y - _bottom.y;
        Vector3 hrStep = new Vector2(rectHrLength / (NUMBER_OF_GRID_COLUMNS - 1), 0);
        Vector3 vertStep = new Vector2(0, rectVertLength / (NUMBER_OF_GRID_ROWS - 1));

        Vector3[,] grid = new Vector3[NUMBER_OF_GRID_ROWS, NUMBER_OF_GRID_COLUMNS];
        grid[0, 0] = new Vector3(_left.x, _bottom.y, _left.z);

        for (int i = 0; i < grid.GetLength(0); i++)
        {
            for (int w = 0; w < grid.GetLength(1); w++)
            {
                if (i == 0 & w == 0) continue;
                else if (w == 0)
                {
                    grid[i, w] = grid[i - 1, 0] + vertStep;
                }
                else grid[i, w] = grid[i, w - 1] + hrStep;
            }
        }
        return grid;
    }
    private void GetShapedGrid(Vector3[,] grid)
    {
        _shapedGrid.Clear();
        foreach (Vector3 point in grid)
        {
            RaycastHit hit = CastTowardsGridPoint(point, _heldObjectLayer);
            if (hit.collider != null) _shapedGrid.Add(point);
        }
    }
    #endregion

    private RaycastHit CastTowardsGridPoint(Vector3 gridPoint, LayerMask layers) 
    {
        Vector3 worldPoint = _cameraTransform.TransformPoint(gridPoint);
        Vector3 origin = Camera.main.WorldToViewportPoint(worldPoint);
        origin.z = 0;
        origin = Camera.main.ViewportToWorldPoint(origin);
        Vector3 direction = worldPoint - origin;
        RaycastHit hit;
        Physics.Raycast(origin, direction, out hit, 1000, layers);
        return hit;
    }
}
