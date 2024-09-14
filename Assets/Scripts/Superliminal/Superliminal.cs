using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class Superliminal : MonoBehaviour
{
	[Header("Components")]
	private Transform target;
	private Rigidbody targetRb;

	[Header("Parameters")]
	public LayerMask targetMask;
	public LayerMask heldTargetMask;
	public LayerMask ignoreTargetMask;

	[Header("Layers")]
	private int heldLayer;
	private int originalLayer;

	float _orgDistanceToScaleRatio;
	Vector3 _orgViewportPos;
	
	
	[SerializeField]
	private int NUMBER_OF_GRID_ROWS = 10;
	[SerializeField]
	private int NUMBER_OF_GRID_COLUMNS = 10;
	private const float SCALE_MARGIN = .001f;
	
	//将物体分割成一个个矩形小格子用于检测碰撞
	private List<Vector3> _shapedGrid = new List<Vector3>();

	void Start()
	{
		Cursor.visible = false;
		Cursor.lockState = CursorLockMode.Locked;
		
		originalLayer = (int)Mathf.Log(targetMask.value, 2);
		heldLayer = (int)Mathf.Log(heldTargetMask.value, 2);
	}

	void Update()
	{
		HandleInput();
		ResizeTarget();
	}

	void HandleInput()
	{
		if (Input.GetMouseButtonDown(0))
		{
			if (target != null)
			{
				targetRb.isKinematic = false;
				targetRb.constraints = RigidbodyConstraints.None;

				// 切换回原来的图层
				target.gameObject.layer = originalLayer;
				target.parent = null;
				target = null;
				targetRb = null;
				
				return;
			}
			
			RaycastHit hit;
			Physics.Raycast(transform.position, transform.forward, out hit, 100f, targetMask);
			if(hit.collider == null) return;
			
			target = hit.transform;
			targetRb = target.GetComponent<Rigidbody>();
			targetRb.isKinematic = true;
			targetRb.constraints = RigidbodyConstraints.FreezeAll;
			target.parent = transform;
			
			// 切换到 heldLayer 图层
			originalLayer = target.gameObject.layer;
			target.gameObject.layer = heldLayer;

			_orgDistanceToScaleRatio = Vector3.Distance(transform.position, target.position) / target.localScale.x;
			_orgViewportPos = Camera.main.WorldToViewportPoint(target.position);
			
			Vector3[] bbPoints = GetBoundingBoxPoints();
			SetupShapedGrid(bbPoints);
		}
	}

	void ResizeTarget()
	{
		if (target == null)
		{
			return;
		}
		
		if (_shapedGrid.Count == 0) throw new System.Exception("Shaped grid calculation error");

		MoveInFrontOfObstacles();
		UpdateScale();
	}
	
	private void MoveInFrontOfObstacles() 
	{

		float closestZ = 1000;
		for (int i = 0; i < _shapedGrid.Count; i++)
		{
			RaycastHit hit = CastTowardsGridPoint(_shapedGrid[i], ignoreTargetMask);
			if (hit.collider == null) continue;

			Vector3 obstaclePoint = transform.InverseTransformPoint(hit.point);	//Convert the point to camera space
			if (i == 0 || obstaclePoint.z < closestZ)
			{
				//Find the closest point of the obstacle(s) to the camera
				closestZ = obstaclePoint.z;
			}
		}

		//Move the held object in front of the closestZ
		float boundsMagnitude = target.GetComponent<Renderer>().localBounds.extents.magnitude * target.localScale.x;
		Vector3 newLocalPos = target.localPosition;
		newLocalPos.z = closestZ - boundsMagnitude;
		target.localPosition = newLocalPos;
	}
	
	
	private void UpdateScale() 
	{
		float newScale = (transform.position - target.position).magnitude / _orgDistanceToScaleRatio;
		if (Mathf.Abs(newScale - target.localScale.x) < SCALE_MARGIN) return;

		target.localScale = new Vector3(newScale, newScale, newScale);
		//By scaling we're actually changing the viewportPosition of heldObject and we don't want that
		Vector3 newPos = Camera.main.ViewportToWorldPoint(new Vector3(_orgViewportPos.x, _orgViewportPos.y,
			(target.position - transform.position).magnitude));
		target.position = newPos;
	}


	#region Calculating grid
	private Vector3[] GetBoundingBoxPoints() 
	{
		Vector3 size = target.GetComponent<Renderer>().localBounds.size;
		Vector3 x = new Vector3(size.x, 0, 0);
		Vector3 y = new Vector3(0, size.y, 0);
		Vector3 z = new Vector3(0, 0, size.z);
		Vector3 min = target.GetComponent<Renderer>().localBounds.min;
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
	
	[Header("相机空间下的物体边界")]
	private Vector3 _left;
	private Vector3 _right;
	private Vector3 _top;
	private Vector3 _bottom;
	
	private void SetupShapedGrid(Vector3[] bbPoints) 
	{
		_left = _right = _top = _bottom = Vector2.zero;
		GetRectConfines(bbPoints);
        
		Vector3[,] grid = SetupGrid();
		GetShapedGrid(grid);
	}

	private void GetRectConfines(Vector3[] bbPoints)
	{
		Vector3 bbPoint;
		Vector3 cameraPoint;
		Vector2 viewportPoint;
		Vector3 closestPoint = target.GetComponent<Renderer>().localBounds.ClosestPoint(transform.position);
		float closestZ = transform.InverseTransformPoint(target.TransformPoint(closestPoint)).z;
		// Vector3 closestPoint = target.GetComponent<Renderer>().bounds.ClosestPoint(transform.position);
		// float closestZ = transform.InverseTransformPoint(closestPoint).z;
        
		if (closestZ <= 0) throw new System.Exception("HeldObject's inside the player!");

		for (int i = 0; i < bbPoints.Length; i++)
		{
			bbPoint = target.TransformPoint(bbPoints[i]);
			viewportPoint = gameObject.GetComponent<Camera>().WorldToViewportPoint(bbPoint);
			cameraPoint = transform.InverseTransformPoint(bbPoint);
			cameraPoint.z = closestZ;

			if (viewportPoint.x < 0 || viewportPoint.x > 1
			                        || viewportPoint.y < 0 || viewportPoint.y > 1) continue;

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
			RaycastHit hit = CastTowardsGridPoint(point, heldTargetMask);
			if (hit.collider != null) _shapedGrid.Add(point);
		}
	}
	#endregion
	
	private RaycastHit CastTowardsGridPoint(Vector3 gridPoint, LayerMask layers) 
	{
		Vector3 worldPoint = transform.TransformPoint(gridPoint);
		Vector3 origin = Camera.main.WorldToViewportPoint(worldPoint);
		origin.z = 0;
		origin = Camera.main.ViewportToWorldPoint(origin);
		Vector3 direction = worldPoint - origin;
		Physics.Raycast(origin, direction, out var hit, 1000, layers);
		return hit;
	}


	private void OnDrawGizmos()
	{
		if(!Application.isPlaying) return;
		
		Gizmos.color = Color.red;
		// draw grid
		if (_shapedGrid.Count > 0)
		{
			foreach (var point in _shapedGrid)
			{
				Gizmos.DrawSphere(transform.TransformPoint(point), 0.1f);
			}
		}
		
		if(target != null)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(target.position, target.GetComponent<Renderer>().bounds.size);
		}
	}
}
