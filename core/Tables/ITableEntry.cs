namespace Core.Tables
{
    // one line of a GM table: drawn by weight, named by an id, and maybe with a narrator's line
    public interface ITableEntry : IWeighted
    {
        string Id { get; }

        // whether the narrator has a line for it
        bool Speaks { get; }
    }
}
