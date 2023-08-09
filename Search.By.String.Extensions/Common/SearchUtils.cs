using Search.By.String.Extensions.Exceptions;
using System.Data;
using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;

namespace Search.By.String.Extensions.Common
{
    internal class SearchUtils
    {
        internal static List<string> ComparisonOperators = new()
        {
            "!=",
            ">=",
            "<=",
             "=",
             "<",
             ">",
            "contains",
            "startswith",
            "endswith",
            "empty"
        };

        public static Expression<Func<T, bool>> BuildPredicate<T>(string searchString)
        {
            if (string.IsNullOrWhiteSpace(searchString))
            {
                return exp => true;
            }

            List<string> separatedValues = SeparatePropertiesAndOperators(searchString, out List<string> logOperators);

            var parameter = Expression.Parameter(typeof(T), "x");

            List<PropertyInfo> propertyInfos = GetPropertyInfos<T>();

            string value = default;

            List<string> properties = new();

            foreach (var item in separatedValues)
            {
                foreach (var op in ComparisonOperators)
                {
                    if (item.IndexOf(op) != -1)
                    {
                        properties.Add(item.Substring(0, item.IndexOf(op)).Replace("(", "").Replace(")", ""));
                        break;
                    }
                }
            }

            if (properties.Count == 0)
            {
                throw new Exception(separatedValues[0]);
            }

            List<Expression<Func<T, bool>>> propertyExpressions = new();

            for (int i = 0; i < properties.Count; i++)
            {
                string criteria = separatedValues[i];
                string comparison = null;

                if ((comparison = ComparisonOperators.Find(co => criteria.ToLower().Contains(co.ToLower()))) != null)
                {
                    value = criteria[(criteria.IndexOf(comparison) + comparison.Length)..];

                    if (value.EndsWith(')'))
                    {
                        while (value.EndsWith(')'))
                        {
                            value = value.Substring(0, value.Length - 1);
                        }
                    }

                    propertyExpressions.Add(BuildExpression<T>(propertyInfos, properties[i], value, comparison, parameter));
                }
            }

            if (separatedValues.Count == 1)
            {
                return propertyExpressions[0];
            }

            return BuildAndParseLambaExpression(separatedValues, logOperators, propertyExpressions, parameter);
        }

        private static Expression<Func<T, bool>> BuildAndParseLambaExpression<T>(List<string> separatedValues,
               List<string> logOperators,
               List<Expression<Func<T, bool>>> propertyExpressions,
               ParameterExpression parametarExpression)
        {
            string expression = default;

            for (int i = 0; i < separatedValues.Count; i++)
            {
                string openBrackets = default;

                string clossedBracket = default;

                string currentValue = separatedValues[i];

                while (currentValue.StartsWith("("))
                {
                    openBrackets += "(";
                    currentValue = currentValue.Substring(1);
                }

                while (currentValue.EndsWith(")"))
                {
                    clossedBracket += ")";
                    currentValue = currentValue.Substring(0, currentValue.Length - 1);
                }

                var expressionString = propertyExpressions[i].Body.ToString();

                BinaryExpression be = propertyExpressions[i].Body as BinaryExpression;

                if (be != null && be.Left.Type == typeof(DateTime))
                {
                    expressionString = expressionString.Replace("ToDateTime(\"", "Convert.ToDateTime(\"");
                }
                else if (be != null && be.Right.Type.IsEnum)
                {
                    expressionString = expressionString.Replace(be.Right.ToString() + ")", be.Right.Type.FullName + "." + be.Right + ")");
                }
                else if (be != null && be.Left is UnaryExpression unary && unary.Operand.Type.IsEnum)
                {
                    var enumFull = unary.Operand.Type.FullName + "."
                        + Enum.Parse(unary.Operand.Type, ((ConstantExpression)be.Right).Value.ToString()).ToString();

                    expressionString = unary.Operand.ToString() + GetNodeTypeString(be.NodeType) + enumFull;
                }
                else if (be != null && be.Left is MethodCallExpression methodCallExpression && methodCallExpression.Method.Name == "Compare"
                        && methodCallExpression.Arguments.All(exp => exp.Type == typeof(string)))
                {
                    expressionString = string.Concat("(string.", expressionString.AsSpan(1));
                }

                if (i == 0)
                {
                    expression = openBrackets + RemoveBrackets(expressionString) + clossedBracket;
                }
                else
                {
                    expression += (logOperators[i - 1].ToLower() == "and" ? "&&" : "||") + openBrackets + RemoveBrackets(expressionString) + clossedBracket;
                }
            }

            expression = parametarExpression.Name + "=>" + expression;

            return DynamicExpressionParser.ParseLambda<T, bool>(typeof(Func<T, bool>), ParsingConfig.Default, false, expression);
        }

        private static string GetNodeTypeString(ExpressionType expressionType)
        {
            switch (expressionType)
            {
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                default:
                    return "==";
            }
        }

        private static string RemoveBrackets(string expression)
        {
            if (expression.StartsWith("("))
            {
                return expression[1..^1];
            }

            return expression;
        }

        private static Expression<Func<T, bool>> BuildExpression<T>(List<PropertyInfo> propertyInfos, string field, string value, string comparison, ParameterExpression parameter)
        {
            try
            {
                PropertyInfo ret = null;

                if (field.Contains("."))
                {
                    string[] depthProp = field.Split('.');
                    Expression current = parameter;

                    for (int i = 0; i < depthProp.Length; i++)
                    {
                        if ((ret = propertyInfos.Find(p => p.Name.ToLower() == depthProp[i].ToLower())) != null)
                        {
                            current = Expression.Property(current, ret.Name);
                            if (i + 1 != depthProp.Length)
                            {
                                if (ret.PropertyType.GetInterfaces().Any(x => x.IsGenericType
                                                && (ret.PropertyType.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                                                    ret.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))))
                                {
                                    var parameter1 = Expression.Parameter(ret.PropertyType.GetGenericArguments().Single(), "y");

                                    var right = typeof(SearchUtils).GetMethod("BuildExpression", BindingFlags.Static | BindingFlags.NonPublic)
                                            .MakeGenericMethod(ret.PropertyType.GetGenericArguments().Single())
                                            .Invoke(null, new object[] { ret.PropertyType.GetGenericArguments().Single().GetProperties().ToList(),
                                            field.Substring(field.IndexOf('.', field.IndexOf(depthProp[i])) + 1), value, comparison, parameter1 }) as Expression;


                                    var method = Expression.Call(typeof(Enumerable), "Any", new Type[] { current.Type.GetGenericArguments().Single() }, current, right);

                                    return Expression.Lambda<Func<T, bool>>(method, parameter);
                                }
                            }
                            propertyInfos = ret.PropertyType.GetProperties().ToList();
                            continue;
                        }

                        throw new BadPropertyException(field);
                    }

                    return ComparasionExpression<T>(parameter, value, ret, comparison, GetPropertyPathAccessor(parameter, field));
                }

                if ((ret = propertyInfos.Find(p => p.Name.ToLower() == field.ToLower())) != null)
                {
                    return ComparasionExpression<T>(parameter, value, ret, comparison, GetPropertyPathAccessor(parameter, field));
                }

                throw new BadPropertyException(field);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    throw ex.InnerException;
                }

                throw;
            }
        }

        private static Expression<Func<T, bool>> ComparasionExpression<T>(ParameterExpression parameter, string value, PropertyInfo propertyInfo, string comparison, Expression left)
        {
            Expression<Func<T, bool>> retExp;

            if (propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
            {
                DateTime date = default;

                try
                {
                    date = DateTime.ParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new BadPropertyTypeException(propertyInfo.Name, value, nameof(DateTime));
                }

                var body = MakeComparison(left, comparison, value);

                retExp = Expression.Lambda<Func<T, bool>>(body, parameter);
            }
            else if (propertyInfo.PropertyType == typeof(string) && comparison == "=" && value.Contains("*"))
            {
                retExp = GenerateLikeExpression<T>(parameter, left, value);
            }
            else if (propertyInfo.PropertyType.IsEnum && propertyInfo.PropertyType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                if (comparison != "=" && comparison != "!=")
                {
                    throw new RequestNotValidException($"Operator '{comparison}' not allowed for property '{propertyInfo.Name}'. Allowed operator: =,!=.");
                }

                var method = typeof(Enum).GetMethods().Where(x => x.Name == "HasFlag").FirstOrDefault();
                Enum enumValue = (Enum)Enum.Parse(propertyInfo.PropertyType, value);
                var argument = Expression.Constant(enumValue, typeof(Enum));

                var methodCall = Expression.Call(left, method, argument);

                var body = Expression.MakeBinary(ExpressionType.Equal, methodCall, Expression.Constant(comparison == "=" ? true : false, typeof(bool)));

                retExp = Expression.Lambda<Func<T, bool>>(body, parameter);
            }
            else
            {
                var body = MakeComparison(left, comparison, value);

                retExp = Expression.Lambda<Func<T, bool>>(body, parameter);
            }

            return retExp;
        }

        private static MemberExpression GetPropertyPathAccessor(Expression parameter, string path)
        {
            Expression current = parameter;

            foreach (var propertyName in path.Split('.'))
            {
                current = Expression.Property(current, propertyName);
            }

            if (current.Type == typeof(DateTime))
            {
                current = Expression.Property(current, "Date");
            }
            else if (current.Type == typeof(DateTime?))
            {
                current = Expression.Property(current, "Value");
                current = Expression.Property(current, "Date");
            }

            return (MemberExpression)current;
        }

        private static Expression<Func<T, bool>> GenerateLikeExpression<T>(ParameterExpression parameter, Expression left, string value)
        {
            var parts = value.Split("*");

            Expression<Func<T, bool>> result = null;

            for (int j = 0; j < parts.Length; j++)
            {
                if (!string.IsNullOrWhiteSpace(parts[j]))
                {
                    Expression exp;
                    if (j == 0)
                    {
                        exp = Expression.Call(MakeString(left), "startswith", Type.EmptyTypes, Expression.Constant(parts[j], typeof(string)));

                    }
                    else if (j == parts.Length - 1)
                    {
                        exp = Expression.Call(MakeString(left), "endswith", Type.EmptyTypes, Expression.Constant(parts[j], typeof(string)));
                    }
                    else
                    {
                        exp = Expression.Call(MakeString(left), "contains", Type.EmptyTypes, Expression.Constant(parts[j], typeof(string)));
                    }

                    Expression<Func<T, bool>> expression = Expression.Lambda<Func<T, bool>>(exp, parameter);

                    if (result == null)
                    {
                        result = expression;
                    }
                    else
                    {
                        result = Combine(result, expression, "and");
                    }
                }
            }

            return result;
        }

        private static List<PropertyInfo> GetPropertyInfos<T>()
        {
            List<PropertyInfo> propertyInfos;
            if (typeof(T).IsAbstract)
            {
                propertyInfos =
                        Assembly.GetAssembly(typeof(T)).GetTypes()
                        .Where(t => t.IsSubclassOf(typeof(T)))
                        .SelectMany(t => t.GetRuntimeProperties()).ToList().GroupBy(gr => gr.Name).Select(g => g.First()).ToList();
            }
            else
            {
                propertyInfos = typeof(T).GetRuntimeProperties().ToList();
            }

            return propertyInfos;
        }

        private static Expression<Func<T, K>> Combine<T, K>(Expression<Func<T, K>> a, Expression<Func<T, K>> b, string logOperator)
        {
            if (logOperator == "and")
            {
                return Expression.Lambda<Func<T, K>>(Expression.AndAlso(a.Body, b.Body), a.Parameters[0]);
            }

            return Expression.Lambda<Func<T, K>>(Expression.OrElse(a.Body, b.Body), a.Parameters[0]);
        }

        private static Expression MakeComparison(Expression left, string comparison, string value)
        {
            switch (comparison.ToLower())
            {
                case "=":
                    return MakeBinary(ExpressionType.Equal, left, value);
                case "!=":
                    return MakeBinary(ExpressionType.NotEqual, left, value);
                case ">":
                    return MakeBinary(ExpressionType.GreaterThan, left, value);
                case ">=":
                    return MakeBinary(ExpressionType.GreaterThanOrEqual, left, value);
                case "<":
                    return MakeBinary(ExpressionType.LessThan, left, value);
                case "<=":
                    return MakeBinary(ExpressionType.LessThanOrEqual, left, value);
                case "contains":
                case "startswith":
                case "endswith":
                    return Expression.Call(MakeString(left), comparison, Type.EmptyTypes, Expression.Constant(value, typeof(string)));
                case "empty":
                    return MakeBinary(ExpressionType.Equal, left, null);
                default:
                    throw new NotSupportedException($"Invalid comparison operator '{comparison}'.");
            }
        }

        private static Expression MakeString(Expression source)
        {
            return source.Type == typeof(string) ? source : Expression.Call(source, "ToString", Type.EmptyTypes);
        }

        private static Expression MakeBinary(ExpressionType type, Expression left, string value)
        {
            try
            {
                object typedValue = value;
                Type valueType = null;

                if (left.Type != typeof(string))
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        typedValue = null;

                        if (Nullable.GetUnderlyingType(left.Type) == null)
                        {
                            left = Expression.Convert(left, typeof(Nullable<>).MakeGenericType(left.Type));
                        }
                    }
                    else
                    {
                        valueType = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
                        typedValue = valueType.IsEnum ? Enum.Parse(valueType, value) :
                            valueType == typeof(Guid) ? Guid.Parse(value) :
                            valueType == typeof(DateTime) ? DateTime.ParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture) :
                            Convert.ChangeType(value, valueType);
                    }
                }

                ConstantExpression right = null;

                if ((type == ExpressionType.GreaterThan || type == ExpressionType.GreaterThanOrEqual ||
                    type == ExpressionType.LessThan || type == ExpressionType.LessThanOrEqual) && valueType != null && valueType.IsEnum)
                {
                    left = Expression.Convert(left, typeof(int));
                    right = Expression.Constant((int)typedValue, left.Type);
                }
                else
                {
                    if (left.Type == typeof(DateTime))
                    {
                        Type methodClassType = typeof(Convert);

                        MethodInfo methodToCall = methodClassType.GetMethod("ToDateTime", BindingFlags.Static | BindingFlags.Public, new Type[] { typeof(string) });

                        var constant = Expression.Constant(typedValue.ToString(), typeof(string));

                        var call = Expression.Call(methodToCall, constant);

                        return Expression.MakeBinary(type, left, call);
                    }
                    else if (left.Type == typeof(string) && type != ExpressionType.Equal)
                    {
                        right = Expression.Constant(0, typeof(int));

                        MethodInfo methodToCall = left.Type.GetMethod("Compare", BindingFlags.Static | BindingFlags.Public, new Type[] { typeof(string), typeof(string) });

                        left = Expression.Call(methodToCall, left, Expression.Constant(value));
                    }
                    else if (left.Type == typeof(bool) && type != ExpressionType.Equal)
                    {
                        right = Expression.Constant(typedValue, left.Type);

                        return Expression.MakeBinary(ExpressionType.NotEqual, left, right);
                    }
                    else
                    {
                        right = Expression.Constant(typedValue, left.Type);
                    }                    
                }

                return Expression.MakeBinary(type, left, right);
            }
            catch
            {
                throw new BadPropertyTypeException(((MemberExpression)left).Member.Name, value, left.Type.Name);
            }
        }

        private int StringExpTypeConst(ExpressionType type)
        {
            switch (type)
            {
                case ExpressionType.LessThan:
                    return 0;
                case ExpressionType.LessThanOrEqual:
                    return 0;
                case ExpressionType.GreaterThan:
                    return 1;
                case ExpressionType.GreaterThanOrEqual:
                    return 0;
                default:
                    return 0;
            }
        }

        public static List<string> SeparatePropertiesAndOperators(string searchString, out List<string> logOperators)
        {
            List<int> indexes = new List<int>();

            logOperators = new List<string>();

            foreach (var compOperator in ComparisonOperators)
            {
                var ret = AllIndexesOf(searchString, compOperator);

                if (ret.Count > 0)
                {
                    indexes.ForEach(i =>
                    {
                        ret.RemoveAll(r => r == i || r - 1 == i);
                    });

                    indexes.AddRange(ret);
                }
            }

            List<string> result = new();

            if (indexes.Count == 1)
            {
                result.Add(searchString);
            }
            else
            {
                indexes.Sort();

                int position = 0;

                for (int i = 0; i < indexes.Count; i++)
                {
                    int start;

                    start = i == 0 ? 0 : indexes[i] + 1;

                    int end = i + 1 != indexes.Count ? indexes[i + 1] : searchString.Length;

                    string ret = searchString[(start - position)..end];

                    int indexOf = -1;

                    if (i + 1 != indexes.Count)
                    {
                        if ((indexOf = ret.ToLower().LastIndexOf("and")) != -1)
                        {
                            position = ret.Length - indexOf - 2;

                            result.Add(ret.Substring(0, indexOf));

                            logOperators.Add("and");
                        }
                        else if ((indexOf = ret.ToLower().LastIndexOf("or")) != -1)
                        {
                            position = ret.Length - indexOf - 1;

                            result.Add(ret.Substring(0, indexOf));

                            logOperators.Add("or");
                        }
                    }
                    else
                    {
                        result.Add(ret);
                    }
                }
            }

            return result;
        }

        public static bool CheckBracket(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return true;
            }

            Stack<char> brackets = new Stack<char>();

            foreach (var c in input)
            {
                if (c == '(')
                {
                    brackets.Push(c);
                }
                else if (c == ')')
                {
                    if (brackets.Count <= 0)
                    {
                        return false;
                    }

                    char open = brackets.Pop();

                    if (c == ')' && open != '(')
                    {
                        return false;
                    }
                }
            }

            if (brackets.Count > 0)
            {
                return false;
            }

            return true;
        }

        private static List<int> AllIndexesOf(string str, string value)
        {
            List<int> indexes = new();

            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);

                if (index == -1)
                {
                    return indexes;
                }

                indexes.Add(index);
            }
        }

        public static string ChangedSearchStringForQuery(string searchString, string prefix)
        {
            List<string> fields = new();

            List<string> logOperators;

            foreach (var sv in SeparatePropertiesAndOperators(searchString, out logOperators))
            {
                foreach (var item in ComparisonOperators)
                {
                    if (sv.IndexOf(item) != -1)
                    {
                        string fieldName = sv.Substring(0, sv.IndexOf(item));

                        fields.Add(prefix + "." + fieldName + sv.Substring(sv.IndexOf(fieldName) + fieldName.Length));

                        break;
                    }
                }
            }

            string fixedSearchString = string.Empty;

            for (int i = 0; i < fields.Count; i++)
            {
                fixedSearchString += fields[i] + (logOperators.Count == 0 ? "" : i + 1 == fields.Count ? "" : logOperators[i]);
            }

            return fixedSearchString;
        }

        private static string ConvertToFullQuilifedString(Expression e)
        {
            var lam = e as LambdaExpression;

            if (lam != null)
            {
                var pStr = lam.Parameters.Select(p => ConvertToFullQuilifedString(p));

                var paramStr = pStr.Any() ? string.Format("({0})", string.Join(", ", pStr)) : "()";

                var bodyExpr = ConvertToFullQuilifedString(lam.Body);

                return paramStr + " => " + bodyExpr;
            }

            var param = e as ParameterExpression;

            if (param != null)
            {
                return param.Name;
            }
            var methodExpression = e as MethodCallExpression;

            if (methodExpression != null)
            {
                string methodName;
                if (methodExpression.Method.IsStatic)
                {
                    methodName = methodExpression.Method.DeclaringType.Name + "." + methodExpression.Method.Name;
                }
                else
                {
                    methodName = methodExpression.Method.Name;
                }

                var pStr = methodExpression.Arguments.Select(p => ConvertToFullQuilifedString(p));

                var paramStr = pStr.Any() ? string.Format("({0})", string.Join(", ", pStr)) : "()";

                return methodName + paramStr;
            }

            return e.ToString();
        }
    }
}
