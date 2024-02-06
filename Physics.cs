using Godot;
using System;

public partial class Physics : Node
{
	// Make sure to update this in the GLSL compute shader as well - i'm too dumb and tired rn to figure out how to transfer an int
	// MUST BE EVEN!! (divisible by 2)
	// TODO: figure out how to transfer int into compute shader
	// see https://github.com/athillion/ProceduralPlanetGodot/blob/850ea37428deb1d7c00669f892caa847cd3a0f88/scripts/planet/shape/modules/EarthHeightModule.gd#L65
	public const int N_PARTICLES = 12000;

	[Export]
	public bool randomlyDistribute;
	[Export]
	// Wait a little while for the GPU to do its calculations before syncing. Increases framerate but decreases sim accuracy.
	public bool inaccuratePhysics;
	[Export]
	public int inaccuracy = 10;
	[Export]
	public bool startWithRotation;
	[Export]
	public float rotationSpeed = 0.1f;
	[Export]
	public bool disablePhysics;

	public Vector2Array positionArray = new Vector2Array(N_PARTICLES);
	Vector2Array velocityArray = new Vector2Array(N_PARTICLES);
	RenderingDevice renderingDevice;
	Rid computeShader;
	Rid velocityBufferRid;
	int numFramesSinceUpdate;

	void InitParticles() {
		// Create one particle for each game unit
		int sqrSideLength = Mathf.CeilToInt(Mathf.Sqrt(N_PARTICLES));
		GD.Print(sqrSideLength);

		int index = 0;
		for (int y = 0; y < sqrSideLength; y++) {
			for (int x = 0; x < sqrSideLength; x++) {
				if (index >= N_PARTICLES) { return; }
				if (randomlyDistribute) {
					positionArray[index] = new Vector2((float)GD.RandRange(-(float)sqrSideLength / 2, sqrSideLength / 2), (float)GD.RandRange(-(float)sqrSideLength / 2, sqrSideLength / 2));
				}
				else {
					positionArray[index] = new Vector2(-sqrSideLength / 2 + x, sqrSideLength / 2 - y);
				}
				index++;
			}
		}
	}

	void InitParticleVelocity() {
		for (int i = 0; i < N_PARTICLES; i++) {
			velocityArray[i] = positionArray[i].ToVector3().Cross(Vector3.Back).ToVector2().Normalized() * rotationSpeed;
		}
	}

	public override void _Ready() {
		InitParticles();
		if (startWithRotation) { InitParticleVelocity(); }
		renderingDevice = RenderingServer.CreateLocalRenderingDevice();

		// Load GLSL shader
		RDShaderFile shaderFile = GD.Load<RDShaderFile>("res://compute.glsl");
		RDShaderSpirV shaderBytecode = shaderFile.GetSpirV();
		computeShader = renderingDevice.ShaderCreateFromSpirV(shaderBytecode);

		//velocityArray[50] = new Vector2(100,100);
		velocityBufferRid = ExecuteComputePipeline();
	}

	// https://docs.godotengine.org/en/stable/tutorials/shaders/compute_shaders.html
	Rid ExecuteComputePipeline() {
		byte[] positionBytes = new byte[positionArray.InternalArraySize * sizeof(float)];
		byte[] velocityBytes = new byte[velocityArray.InternalArraySize * sizeof(float)];

		Buffer.BlockCopy(positionArray._internalFloatArray, 0, positionBytes, 0, positionBytes.Length);
		Buffer.BlockCopy(velocityArray._internalFloatArray, 0, velocityBytes, 0, velocityBytes.Length);

		// Create storage buffers that can hold our float values.
		Rid positionBuffer = renderingDevice.StorageBufferCreate((uint)positionBytes.Length, positionBytes);
		Rid velocityBuffer = renderingDevice.StorageBufferCreate((uint)velocityBytes.Length, velocityBytes);

		// Create a uniform to assign the position buffer to the rendering device
		RDUniform positionArrayUniform = new RDUniform {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 0
		};
		positionArrayUniform.AddId(positionBuffer);

		// Create a uniform to assign the velocity buffer to the rendering device
		RDUniform velocityArrayUniform = new RDUniform {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 1
		};
		velocityArrayUniform.AddId(velocityBuffer);

		Rid uniformSet = renderingDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform> { positionArrayUniform, velocityArrayUniform }, computeShader, 0);

		// Create a compute pipeline
		Rid pipeline = renderingDevice.ComputePipelineCreate(computeShader);
		long computeList = renderingDevice.ComputeListBegin();
		renderingDevice.ComputeListBindComputePipeline(computeList, pipeline);
		renderingDevice.ComputeListBindUniformSet(computeList, uniformSet, 0);
		renderingDevice.ComputeListDispatch(computeList, xGroups: N_PARTICLES / 2, yGroups: 1, zGroups: 1);
		renderingDevice.ComputeListEnd();
		
		renderingDevice.Submit();

		// The purpose of the compute shader is to recalculate velocities - we only want the velocities back
		return velocityBuffer;
	}

	void CopyBytesToVelocityArray(byte[] bytes) {
		Buffer.BlockCopy(bytes, 0, velocityArray._internalFloatArray, 0, bytes.Length);
	}

	public override void _Process(double delta) {
		if (disablePhysics) { return; }
		if (!inaccuratePhysics || numFramesSinceUpdate >= inaccuracy) {
			numFramesSinceUpdate = 0;
			renderingDevice.Sync();

			byte[] outputBytes = renderingDevice.BufferGetData(velocityBufferRid);
			CopyBytesToVelocityArray(outputBytes);

			velocityBufferRid = ExecuteComputePipeline();
		}

		//GD.Print(positionArray[0]);

		if (float.IsNaN(positionArray[0].X) || float.IsNaN(positionArray[0].Y)) {
			GD.Print("Ruh roh, there's NaNs everywhere!!");
		}

		positionArray.AddArray(velocityArray);
		numFramesSinceUpdate++;
	}

	/*
	// Reimplementation of the compute shader on the cpu for testing
	Vector2 GetVelocityVector(uint index) {
    	return new Vector2(velocityArray._internalFloatArray[index], velocityArray._internalFloatArray[index + N_PARTICLES]);
	}
	void AddVelocityVector(Vector2 value, uint index) {
		velocityArray._internalFloatArray[index] += value.X;
		velocityArray._internalFloatArray[index + N_PARTICLES] += value.Y;
	}

	Vector2 GetPositionVector(uint index) {
		return new Vector2(positionArray._internalFloatArray[index], positionArray._internalFloatArray[index + N_PARTICLES]);
	}

	void GravityPhysicsTest() {
		for (uint id = 0; id < N_PARTICLES; id++) {
			Vector2 velocityVector = GetVelocityVector(id);
			Vector2 position = GetPositionVector(id);
			
			for (uint i = 0; i < N_PARTICLES; i++)
			{
				if (i == id) { continue; }
				Vector2 otherPosition = GetPositionVector(i);
				Vector2 displacement = otherPosition - position;
				AddVelocityVector(0.001f / Mathf.Pow(displacement.Length(), 3) * displacement, id);
			}

			
		}
	}

	void GravityPhysicsTestAlt() {
		for (uint id = 0; id < N_PARTICLES; id++) {
			Vector2 velocityVector = GetVelocityVector(id);
			Vector2 position = GetPositionVector(id);
			
			for (uint i = 0; i < N_PARTICLES; i++)
			{
				if (i == id) { continue; }
				Vector2 otherPosition = GetPositionVector(i);

				// https://en.wikipedia.org/wiki/Newton's_law_of_universal_gravitation
				Vector2 directionVec = otherPosition - position;
				float distSqr = directionVec.Dot(directionVec);
				Vector2 directionUnitVec = directionVec.Normalized();

				float impulse = Mathf.Min(100, 10 / distSqr);
				velocityVector += impulse * directionUnitVec;
			}

			AddVelocityVector(velocityVector, id);
		}
	}
	*/
}
