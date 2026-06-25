using Godot;
using Godot.Collections;

public partial class MapLoader : Node
{
    public Dictionary LoadMapStub()
    {
        return new Dictionary
        {
            { "width", 15 },
            { "height", 10 },
            { "blocked", new Array() }
        };
    }
}
