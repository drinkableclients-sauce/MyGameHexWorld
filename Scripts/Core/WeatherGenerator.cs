using Godot;

public static class WeatherGenerator
{
	public static void UpdateTerrainMesh(Planet p)
	{
		if (p.TerrainNode == null) return;

		var sphereMesh = new SphereMesh { Radius = p.Radius, Height = p.Radius * 2.0f, RadialSegments = p.Detail * 2, Rings = p.Detail };
		var arrays = sphereMesh.GetMeshArrays();
		Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
		arrays[(int)Mesh.ArrayType.Color] = new Color[verts.Length]; 
		var tempArrayMesh = new ArrayMesh();
		tempArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		var mdt = new MeshDataTool();
		mdt.CreateFromSurface(tempArrayMesh, 0);

		for (int i = 0; i < mdt.GetVertexCount(); i++)
		{
			Vector3 v = mdt.GetVertex(i);
			Vector3 vNorm = v.Normalized();
			float latitude = Mathf.Abs(vNorm.Y); 
			
			float rawNoise = (p.Noise.GetNoise3D(vNorm.X * 100, vNorm.Y * 100, vNorm.Z * 100) + 1.0f) * 0.5f;
			float curvedNoise = Mathf.Pow(rawNoise, p.TerrainExponent);
			float clampedNoise = Mathf.Clamp(curvedNoise, p.MinHeightLimit, p.MaxHeightLimit);

			float moisture = (p.MoistureNoise.GetNoise3D(vNorm.X * 150, vNorm.Y * 150, vNorm.Z * 150) + 1.0f) * 0.5f;
			Color vertexColor = (moisture < 0.45f) ? new Color(0.85f, 0.76f, 0.53f) : new Color(0.22f, 0.58f, 0.28f); 

			if (clampedNoise >= p.WaterLevel && clampedNoise <= p.WaterLevel + 0.04f) vertexColor = new Color(0.9f, 0.82f, 0.62f);

			if (latitude > 0.75f)
			{
				float snowBlend = Mathf.SmoothStep(0.0f, 1.0f, (latitude - 0.75f) / 0.15f);
				vertexColor = vertexColor.Lerp(Colors.White, snowBlend);
				
				float iceHeight = p.WaterLevel + 0.05f;
				if (clampedNoise < iceHeight) clampedNoise = Mathf.Lerp(clampedNoise, iceHeight, snowBlend);
			}

			mdt.SetVertex(i, v + (vNorm * clampedNoise * p.Height));
			mdt.SetVertexColor(i, vertexColor);
		}

		var finalMesh = new ArrayMesh();
		mdt.CommitToSurface(finalMesh);
		p.TerrainNode.Mesh = finalMesh;

		var mat = new StandardMaterial3D();
		mat.VertexColorUseAsAlbedo = true; 
		mat.AlbedoColor = Colors.White; 
		mat.Roughness = 0.8f;
		p.TerrainNode.MaterialOverride = mat;
	}

	public static void UpdateWaterMesh(Planet p)
	{
		if (p.WaterNode == null) return;
		if (p.WaterLevel <= 0.0f) { p.WaterNode.Visible = false; return; }

		p.WaterNode.Visible = true;
		float waterRadius = Mathf.Lerp(p.Radius, p.Radius + p.Height, p.WaterLevel);
		p.WaterNode.Mesh = new SphereMesh { Radius = waterRadius, Height = waterRadius * 2.0f, RadialSegments = p.Detail * 2, Rings = p.Detail };

		p.WaterNode.MaterialOverride = new StandardMaterial3D {
			AlbedoColor = new Color(0.1f, 0.4f, 0.85f, 0.85f),
			Roughness = 0.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
	}

	public static void UpdateCloudsMesh(Planet p)
	{
		if (p.CloudsNode == null) return;
		if (!p.EnableClouds) { p.CloudsNode.Visible = false; return; }

		p.CloudsNode.Visible = true;
		float cloudsRadius = p.Radius + p.Height + 0.35f;
		
		var sphereMesh = new SphereMesh { Radius = cloudsRadius, Height = cloudsRadius * 2.0f, RadialSegments = p.Detail * 2, Rings = p.Detail };
		var arrays = sphereMesh.GetMeshArrays();
		Vector3[] verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
		arrays[(int)Mesh.ArrayType.Color] = new Color[verts.Length]; 
		var tempArrayMesh = new ArrayMesh();
		tempArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		var mdt = new MeshDataTool();
		mdt.CreateFromSurface(tempArrayMesh, 0);

		for (int i = 0; i < mdt.GetVertexCount(); i++)
		{
			Vector3 v = mdt.GetVertex(i);
			Vector3 vNorm = v.Normalized();
			float latitude = Mathf.Abs(vNorm.Y);

			float rawNoise = (p.CloudNoise.GetNoise3D(vNorm.X * 150, vNorm.Y * 150, vNorm.Z * 150) + 1.0f) * 0.5f;
			float cloudDensity = Mathf.Pow(rawNoise, 1.8f); 

			float latMask = 0.0f;
			if (latitude < 0.1f) latMask = 0.0f; 
			else if (latitude < 0.35f) latMask = Mathf.SmoothStep(0.0f, 1.0f, (latitude - 0.1f) / 0.25f); 
			else if (latitude < 0.6f) latMask = Mathf.SmoothStep(1.0f, 0.3f, (latitude - 0.35f) / 0.25f); 
			else latMask = Mathf.SmoothStep(0.3f, 0.85f, (latitude - 0.6f) / 0.4f); 

			float finalAlpha = cloudDensity * latMask;
			finalAlpha = Mathf.SmoothStep(0.1f, 0.45f, finalAlpha);

			mdt.SetVertexColor(i, new Color(1.0f, 1.0f, 1.0f, finalAlpha));
		}

		var finalMesh = new ArrayMesh();
		mdt.CommitToSurface(finalMesh);
		p.CloudsNode.Mesh = finalMesh;

		p.CloudsNode.MaterialOverride = new StandardMaterial3D {
			VertexColorUseAsAlbedo = true,
			AlbedoColor = Colors.White,
			Roughness = 0.9f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
	}

	public static void UpdateAtmosphereMesh(Planet p)
	{
		if (p.AtmosphereNode == null) return;
		if (!p.EnableAtmosphere) { p.AtmosphereNode.Visible = false; return; }

		p.AtmosphereNode.Visible = true;
		float atmosRadius = p.Radius + p.Height + 0.8f;
		p.AtmosphereNode.Mesh = new SphereMesh { Radius = atmosRadius, Height = atmosRadius * 2.0f, RadialSegments = p.Detail * 2, Rings = p.Detail };

		p.AtmosphereNode.MaterialOverride = new StandardMaterial3D {
			AlbedoColor = new Color(0.2f, 0.5f, 0.95f, 0.05f),
			Roughness = 0.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
	}

	public static void UpdateStarsMesh(Planet p)
	{
		if (p.StarsNode == null) return;
		p.StarsNode.Visible = true;
		p.StarsNode.Mesh = new SphereMesh { Radius = 100.0f, Height = 200.0f, RadialSegments = 32, Rings = 16 };

		var starNoise = new FastNoiseLite { Seed = 9999, NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.15f };
		var starGradient = new Gradient();
		starGradient.AddPoint(0.0f, Colors.Black);
		starGradient.AddPoint(0.95f, Colors.Black); 
		starGradient.AddPoint(1.0f, Colors.White);  

		p.StarsNode.MaterialOverride = new StandardMaterial3D {
			AlbedoColor = Colors.White,
			AlbedoTexture = new NoiseTexture2D { Noise = starNoise, ColorRamp = starGradient, Width = 1024, Height = 1024, Seamless = true },
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Front 
		};
	}
}
