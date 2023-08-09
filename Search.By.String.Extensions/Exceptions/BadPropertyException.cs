namespace Search.By.String.Extensions.Exceptions
{
    public class BadPropertyException : Exception
    {
        public BadPropertyException(string propertyName) 
            : base($"Property '{propertyName}' - not found!")
        {
        }
    }
}
