using Godot;
using System;

public partial class LocalTileSelector : Node3D
{
	public event Action<LocalTriangleData> OnTriangleHovered;
	public event Action<LocalTriangleData> OnTriangleClicked;
	public event Action<LocalTriangleData> OnTrianglePaintDragged;

	public LocalTriangleData HoveredTriangle { get; private set; }

	private LocalMapGenerator _mapGenerator;
	private MeshInstance3D _highlightMeshInstance;
	private ImmediateMesh _immediateMesh;
	private StandardMaterial3D _highlightMaterial;
	private bool _isLeftMouseDown = false;

	public override void _Ready()
	{
		_mapGenerator = GetParent() as LocalMapGenerator ?? GetNodeOrNull<LocalMapGenerator>("../LocalMap");

		_immediateMesh = new ImmediateMesh();
		_highlightMaterial = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			NoDepthTest = false
		};

		_highlightMeshInstance = new MeshInstance3D
		{
			Name = "TriangleHoverHighlight",
			Mesh = _immediateMesh,
			MaterialOverride = _highlightMaterial,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_highlightMeshInstance);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_mapGenerator == null) return;

		Camera3D cam = GetViewport().GetCamera3D();
		if (cam == null) return;

		if (GetViewport().GuiGetHoveredControl() != null)
		{
			ClearHighlight();
			return;
		}

		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector3 rayOrigin = cam.ProjectRayOrigin(mousePos);
		Vector3 rayEnd = rayOrigin + cam.ProjectRayNormal(mousePos) * 5000.0f;

		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
		var result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			Vector3 hitPoint = (Vector3)result["position"];
			var tri = _mapGenerator.GetTriangleAtPosition(hitPoint);

			if (tri != HoveredTriangle)
			{
				HoveredTriangle = tri;
				UpdateHighlightVisual(tri);
				OnTriangleHovered?.Invoke(tri);

				// Si el jugador mantiene clic izquierdo y mueve el cursor, pintar continuamente
				if (_isLeftMouseDown && tri != null)
				{
					OnTrianglePaintDragged?.Invoke(tri);
				}
			}
		}
		else if (HoveredTriangle != null)
		{
			ClearHighlight();
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed && GetViewport().GuiGetHoveredControl() == null)
			{
				_isLeftMouseDown = true;
				if (HoveredTriangle != null)
				{
					OnTriangleClicked?.Invoke(HoveredTriangle);
					OnTrianglePaintDragged?.Invoke(HoveredTriangle);
				}
			}
			else if (!mb.Pressed)
			{
				_isLeftMouseDown = false;
			}
		}
	}

	private void ClearHighlight()
	{
		HoveredTriangle = null;
		_immediateMesh.ClearSurfaces();
		OnTriangleHovered?.Invoke(null);
	}

	private void UpdateHighlightVisual(LocalTriangleData tri)
	{
		_immediateMesh.ClearSurfaces();
		if (tri == null) return;

		Color fillColor = tri.IsBuildable
			? new Color(0.25f, 0.95f, 0.45f, 0.42f)
			: new Color(0.95f, 0.32f, 0.22f, 0.42f);

		Color borderColor = tri.IsBuildable
			? new Color(0.75f, 1.0f, 0.80f, 0.95f)
			: new Color(1.0f, 0.65f, 0.55f, 0.95f);

		Vector3 lift = tri.Normal * 0.45f + Vector3.Up * 0.25f;
		Vector3 a = tri.V1 + lift;
		Vector3 b = tri.V2 + lift;
		Vector3 c = tri.V3 + lift;

		_immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
		_immediateMesh.SurfaceSetColor(fillColor);
		_immediateMesh.SurfaceAddVertex(a);
		_immediateMesh.SurfaceAddVertex(b);
		_immediateMesh.SurfaceAddVertex(c);
		_immediateMesh.SurfaceEnd();

		Vector3 lineLift = Vector3.Up * 0.15f;
		_immediateMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
		_immediateMesh.SurfaceSetColor(borderColor);
		_immediateMesh.SurfaceAddVertex(a + lineLift); _immediateMesh.SurfaceAddVertex(b + lineLift);
		_immediateMesh.SurfaceAddVertex(b + lineLift); _immediateMesh.SurfaceAddVertex(c + lineLift);
		_immediateMesh.SurfaceAddVertex(c + lineLift); _immediateMesh.SurfaceAddVertex(a + lineLift);
		_immediateMesh.SurfaceEnd();
	}
}
