using Godot;
using System;
using System.Linq;

// To enter very small values in the inspector, reduce the "Default float step" setting under Editor -> Editor settings -> Interface -> Inspector -> Default Float Step
// For small enough values, that doesn't work for some reason, so edit them here directly instead
public partial class Physics : Node
{
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
	// Distance between particles in units.
	public float density = 1;

	[Export]
	// Gravitational constant. Higher = stronger gravity.
	float gravityConstant;
	[Export]
	// Maximum impulse, maximum amount of force a particle can apply to another in one frame. Lower values usually create larger structures.
    float maxImpulse = 0.00001f;
	[Export]
	// How much each particle is pushed away from the center of the screen.
    float expansionFactor;
	[Export]
    public int particleCount = 18000;
	[Export]
	public bool disablePhysics;

	public Vector2Array positionArray;
	Vector2Array velocityArray;
	RenderingDevice renderingDevice;
	Rid computeShader;
	Rid velocityBufferRid;
	int numFramesSinceUpdate;
	byte[] paramBytes;

	void InitParticles() {
		// Create one particle for each game unit
		int sqrSideLength = Mathf.CeilToInt(Mathf.Sqrt(particleCount));
		GD.Print(sqrSideLength);

		int index = 0;
		for (int y = 0; y < sqrSideLength; y++) {
			for (int x = 0; x < sqrSideLength; x++) {
				if (index >= particleCount) { return; }
				if (randomlyDistribute) {
					positionArray[index] = new Vector2((float)GD.RandRange(-(float)sqrSideLength / 2 * density, sqrSideLength / 2 * density), (float)GD.RandRange(-(float)sqrSideLength / 2 * density, sqrSideLength / 2 * density));
				}
				else {
					positionArray[index] = new Vector2((-sqrSideLength / 2 + x) * density, (sqrSideLength / 2 - y) * density);
				}
				index++;
			}
		}
	}

	void InitParticleVelocity() {
		for (int i = 0; i < particleCount; i++) {
			velocityArray[i] = positionArray[i].ToVector3().Cross(Vector3.Back).ToVector2().Normalized() * rotationSpeed;
		}
	}

	public override void _Ready() {
		// ensure particleCount is even
		if (particleCount % 2 != 0) { particleCount -= 1; }
		GD.Print("Simulation values:");
		GD.Print($"Particle count: {particleCount}");
		GD.Print($"Gravitational constant: {gravityConstant}");
		GD.Print($"Max impulse: {maxImpulse}");
		GD.Print($"Expansion factor: {expansionFactor}");

		positionArray = new Vector2Array(particleCount);
		velocityArray = new Vector2Array(particleCount);

		paramBytes = new [] {
			BitConverter.GetBytes(gravityConstant),
			BitConverter.GetBytes(maxImpulse),
			BitConverter.GetBytes(expansionFactor),
			BitConverter.GetBytes(particleCount),
		}.SelectMany(s => s).ToArray();

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
		Rid paramBuffer = renderingDevice.StorageBufferCreate((uint)paramBytes.Length, paramBytes);

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

		// Create a uniform to assign the parameter buffer to the rendering device
		RDUniform paramArrayUniform = new RDUniform {
			UniformType = RenderingDevice.UniformType.StorageBuffer,
			Binding = 2
		};
		paramArrayUniform.AddId(paramBuffer);

		Rid uniformSet = renderingDevice.UniformSetCreate(new Godot.Collections.Array<RDUniform> { positionArrayUniform, velocityArrayUniform, paramArrayUniform }, computeShader, 0);

		// Create a compute pipeline
		Rid pipeline = renderingDevice.ComputePipelineCreate(computeShader);
		long computeList = renderingDevice.ComputeListBegin();
		renderingDevice.ComputeListBindComputePipeline(computeList, pipeline);
		renderingDevice.ComputeListBindUniformSet(computeList, uniformSet, 0);
		renderingDevice.ComputeListDispatch(computeList, xGroups: (uint)particleCount / 2, yGroups: 1, zGroups: 1);
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
