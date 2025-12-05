using System.Collections.Generic;
using Tpl.Dataflow.Builder.Abstractions;

internal sealed class SplitterBlock : PropagatorManyBlock<string, char>
{
    public override IEnumerable<char> Transform(string input) => input.ToCharArray();
}

internal sealed class AsyncSplitterBlock : AsyncPropagatorManyBlock<string, char>
{
    public override async Task<IEnumerable<char>> TransformAsync(string input)
    {
        await Task.Delay(1);
        return input.ToCharArray();
    }
}
