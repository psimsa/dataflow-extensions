using Tpl.Dataflow.Builder.Abstractions;

internal class CustomBlock : PropagatorBlock<int, int>
{
    protected override int Transform(int input) => input + 10;
}
