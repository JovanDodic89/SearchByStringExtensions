using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Search.By.String.Extensions.Exceptions
{
    public class BadPropertyTypeException : Exception
    {
        public BadPropertyTypeException(string propertyName, string value, string type) 
            : base($"Property '{propertyName}' has wrong value '{value}', according by his type '{type}'.")
        {
        }
    }
}
