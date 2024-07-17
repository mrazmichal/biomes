/// <summary>
/// Random source for MathNet.Numerics.Random.RandomSource using Unity's Random class.
/// Thanks to this class we don't have to set the seed of MathNet.Numerics.Random.RandomSource separately,
/// it's enough to set the seed of the Unity.Random.
/// This connection is useful for achieving repeatable procedural generation.
/// </summary>
public class UnityRandomSource : MathNet.Numerics.Random.RandomSource
{
    protected override double DoSample()
    {
        return UnityEngine.Random.value;       
    }
}

