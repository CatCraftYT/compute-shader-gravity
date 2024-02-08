using Godot;
using System;

public partial class Rendering : Sprite2D
{
	[Export]
	// X and Y square resolution (RESOLUTIONxRESOLUTION)
	int resolution = 512;
	[Export]
	// Size of the viewport in world coordinates. Should be at least sqrt(N_PARTICLES) to fit everything
	float viewportSize = 316.2f;
	// World units per pixel.
	float pixelDensity;
	Color oneColor = new Color(0.1f, 0, 0, 0);

	Vector2 baseOffset;
	Physics physicsScript;
	ShaderMaterial shaderMaterial;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		baseOffset = new Vector2(viewportSize, viewportSize) / 2;
		pixelDensity = viewportSize / resolution;
		GD.Print($"Pixel density: {pixelDensity}");
		physicsScript = (Physics)GetNode("/root/RootNode");
		shaderMaterial = (ShaderMaterial)this.Material;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		// Given a point in world space, the point as a pixel would be: position / PIXEL_DENSITY ?
		Image positionData = Image.Create(resolution, resolution, false, Image.Format.Rf);

		for (int i = 0; i < physicsScript.particleCount; i++) {
			Vector2I position = (Vector2I)((physicsScript.positionArray[i] + baseOffset) / pixelDensity);
			if (0 > position.X || position.X >= resolution || 0 > position.Y || position.Y >= resolution) { continue; }

			positionData.SetPixelv(position, positionData.GetPixelv(position) + oneColor);
		}

		ImageTexture dataTexture = ImageTexture.CreateFromImage(positionData);
		shaderMaterial.SetShaderParameter("positionData", dataTexture);
	}
}
