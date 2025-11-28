using System.Threading.Tasks.Dataflow;
using Tpl.Dataflow.Builder.Abstractions;

/// <summary>
/// Interface for multiplier blocks, demonstrating keyed service registration.
/// </summary>
internal interface IMultiplierBlock : IPropagatorBlock<int, int>;

/// <summary>
/// A block that doubles the input value.
/// </summary>
internal sealed class DoublerBlock : PropagatorBlock<int, int>, IMultiplierBlock
{
    protected override int Transform(int input) => input * 2;
}

/// <summary>
/// A block that triples the input value.
/// </summary>
internal sealed class TriplerBlock : PropagatorBlock<int, int>, IMultiplierBlock
{
    protected override int Transform(int input) => input * 3;
}
