namespace Search.By.String.Extensions.Exceptions
{
    public class OperatorNotValidException : Exception
    {
        public OperatorNotValidException(string comparison, string propertyName, params string[] allowedOperatos) 
            :base($"Operator '{comparison}' not allowed for property '{propertyName}'. Allowed operator: {string.Join(",", allowedOperatos)}")
        { 
        }
    }
}
