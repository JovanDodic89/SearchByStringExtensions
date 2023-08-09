namespace Search.By.String.Extensions.Entites
{
    public class SeekOrderBy
    {
        public string PropertyName { get; set; }
        public string LastValue { get; set; }
        public string FirstValue { get; set; }
        public string SortOrder { get; set; } = "asc";
        public bool IsUnique { get; set; }
    }
}
