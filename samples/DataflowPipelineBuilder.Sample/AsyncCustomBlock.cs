using Tpl.Dataflow.Builder.Abstractions;

internal class AsyncCustomBlock : AsyncPropagatorBlock<int, string>
{
    public override async Task<string> TransformAsync(int input)
    {
        await Task.Delay(100);
        return $"Value: {input}";
    }
}
