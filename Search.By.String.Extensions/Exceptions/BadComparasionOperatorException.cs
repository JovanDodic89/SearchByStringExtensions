namespace Search.By.String.Extensions.Exceptions
{
    public class BadComparasionOperatorException : Exception
    {
        public BadComparasionOperatorException(string comparasionOperator, string propertyName, params string[] allowedOperators)
            : base($"Operator '{comparasionOperator}' not allowed for property '{propertyName}'. Allowed operators: '{string.Join(",", allowedOperators)}'")
        {
            
        }
    }
}
