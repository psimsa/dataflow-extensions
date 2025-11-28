using Tpl.Dataflow.Builder.Abstractions;

internal class CustomBlock : PropagatorBlock<int, int>
{
    public override int Transform(int input) => input + 10;
}
