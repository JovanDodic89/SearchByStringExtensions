using Search.By.String.Extensions.Common;
using Search.By.String.Extensions.Constants;
using Search.By.String.Extensions.Entites;
using Search.By.String.Extensions.Exceptions;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Search.By.String.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<TSource> Where<TSource>(this IQueryable<TSource> source,
            string searchString)
                => source.Where(SearchUtils.BuildPredicate<TSource>(searchString));


        public static IQueryable<TSource> WhereSeekOrderByTake<TSource>(this IQueryable<TSource> source,
            string searchString,
            List<SeekOrderBy> seekOrdersBy,
            int pageSize,
            bool firstPage = false,
            bool lastPage = false,
            IQueryable<TSource> source1 = null)
        {
            if (seekOrdersBy == null || seekOrdersBy.Count == 0)
            {
                return Where(source, searchString);
            }

            string whereSeekExp = "(";
            string previousExp = string.Empty;
            string orderby = string.Empty;
            string sortOrder = string.Empty;
            string sortOrderTwo = string.Empty;

            for (int i = 0; i < seekOrdersBy.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(seekOrdersBy[i].PropertyName))
                {
                    continue;
                }

                orderby += $"{seekOrdersBy[i].PropertyName},";

                if (lastPage)
                {
                    GetOrdersBy(seekOrdersBy[i], ref sortOrder, ref sortOrderTwo, firstPage, lastPage);

                    continue;
                }
                else if (!string.IsNullOrWhiteSpace(seekOrdersBy[i].FirstValue))
                {
                    GetOrdersBy(seekOrdersBy[i], ref sortOrder, ref sortOrderTwo, firstPage, lastPage);
                }
                else
                {
                    sortOrder += $"{seekOrdersBy[i].SortOrder},";
                }

                if (firstPage || (string.IsNullOrWhiteSpace(seekOrdersBy[i].LastValue) && string.IsNullOrWhiteSpace(seekOrdersBy[i].FirstValue)))
                {
                    continue;
                }

                string logOperator = string.Empty;

                if (!string.IsNullOrWhiteSpace(seekOrdersBy[i].FirstValue))
                {
                    logOperator = seekOrdersBy[i].SortOrder.ToLower() == "asc" ? "<" : ">";
                }
                else
                {
                    logOperator = seekOrdersBy[i].SortOrder.ToLower() == "asc" ? ">" : "<";
                }

                string value = string.IsNullOrWhiteSpace(seekOrdersBy[i].FirstValue) ? seekOrdersBy[i].LastValue : seekOrdersBy[i].FirstValue;

                if (i != 0)
                {
                    string valueRet = string.IsNullOrWhiteSpace(seekOrdersBy[i - 1].FirstValue) ? seekOrdersBy[i - 1].LastValue : seekOrdersBy[i - 1].FirstValue;

                    if (!seekOrdersBy[i - 1].IsUnique && !string.IsNullOrWhiteSpace(valueRet))
                    {
                        previousExp += $"{((i - 1 >= 1) ? "and" : string.Empty)}{seekOrdersBy[i - 1].PropertyName}={valueRet}";
                    }

                    whereSeekExp += GetNextCondition(previousExp, seekOrdersBy[i].PropertyName, logOperator, value);
                }
                else
                {
                    whereSeekExp += $"({seekOrdersBy[i].PropertyName}{logOperator}{value})";
                }

                if (i + 1 == seekOrdersBy.Count && !seekOrdersBy[i].IsUnique)
                {
                    whereSeekExp += GetNextCondition(previousExp, seekOrdersBy[i].PropertyName, "=", value);
                }
            }

            orderby = orderby[..^1];
            sortOrder = sortOrder[..^1];

            whereSeekExp += ")";

            if (whereSeekExp.Length == 2)
            {
                whereSeekExp = null;
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                whereSeekExp = $"({searchString}){(whereSeekExp == null ? string.Empty : "and" + whereSeekExp)}";
            }

            if (!string.IsNullOrWhiteSpace(sortOrderTwo))
            {
                sortOrderTwo = sortOrderTwo[..^1];

                var query = source1 ?? source;

                query = query.OrderBy(@orderby, sortOrderTwo);

                var ret = Where(source, whereSeekExp).OrderBy(@orderby, sortOrder).Take(pageSize);

                var result = query.Join(ret, q => q, q => q, (t, t1) => t);

                return result;
            }

            return Where(source, whereSeekExp).OrderBy(orderby, sortOrder).Take(pageSize);
        }

        private static void GetOrdersBy(SeekOrderBy seekOrderBy, ref string sortOrder, ref string sortOrderTwo, bool firstPage, bool lastPage)
        {
            if (seekOrderBy.SortOrder.ToLower() == "asc")
            {
                if (!firstPage && (!string.IsNullOrEmpty(seekOrderBy.FirstValue) || lastPage))
                {
                    sortOrderTwo += $"asc,";
                    sortOrder += $"desc,";
                }
                else
                {
                    sortOrder += $"asc,";
                }
            }
            else
            {
                if (!firstPage && (!string.IsNullOrEmpty(seekOrderBy.FirstValue) || lastPage))
                {
                    sortOrderTwo += $"desc,";
                    sortOrder += $"asc,";
                }
                else
                {
                    sortOrder += $"desc,";
                }
            }
        }

        private static string GetNextCondition(string previousExp, string propertyName, string logOperator, string lastValue)
        {
            if (string.IsNullOrWhiteSpace(previousExp))
            {
                return $"OR({propertyName}{logOperator}{lastValue})";
            }

            return $"OR({previousExp}and{propertyName}{logOperator}{lastValue})";
        }

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string ordering, string sortOrder)
        {
            var type = typeof(T);

            var parameter = Expression.Parameter(type, "p");

            PropertyInfo property;

            var expression = source.Expression;
            int count = 0;
            string[] propertyNames = ordering.Split(",");
            string[] sorting = sortOrder.Split(",");

            foreach (var propertyName in propertyNames)
            {
                MemberExpression selector = null;

                if (propertyName.Contains('.'))
                {
                    string[] childProperties = propertyName.Split('.', StringSplitOptions.RemoveEmptyEntries);

                    bool isDateOnly = false;

                    if (propertyName.ToLower().EndsWith(InternalConstants.ORDER_DATE_ONLY))
                    {
                        isDateOnly = true;
                    }

                    property = type.GetProperties().FirstOrDefault(pr => pr.Name.ToLower() == childProperties[0].ToLower());

                    if (property == null)
                    {
                        throw new BadPropertyException(childProperties[0]);
                    }

                    selector = Expression.PropertyOrField(parameter, property.Name);

                    for (int i = 1; i < childProperties.Length; i++)
                    {
                        string path = "";

                        if (i == childProperties.Length - 1 && isDateOnly)
                        {
                            property = typeof(T).GetRuntimeProperties().FirstOrDefault(pr => pr.Name.ToLower() == childProperties[i].ToLower().Substring(0, childProperties[i].Length - 9));

                            path = GetPathForDateOnlyProperty(property);
                        }
                        else
                        {
                            property = property.PropertyType.GetProperties().FirstOrDefault(pr => pr.Name.ToLower() == childProperties[i].ToLower());
                        }

                        if (property == null)
                        {
                            throw new BadPropertyException(childProperties[i]);
                        }

                        selector = ExpressionHelper.GetPropertyPathAccessor(parameter, (isDateOnly ? propertyName.Substring(0, propertyName.ToLower().LastIndexOf(InternalConstants.ORDER_DATE_ONLY)) : propertyName) + path);
                    }
                }
                else
                {
                    string path = "";

                    if (propertyName.ToLower().EndsWith("-dateonly"))
                    {
                        property = typeof(T).GetRuntimeProperties().FirstOrDefault(pr => pr.Name.ToLower() == propertyName.ToLower().Substring(0, propertyName.Length - 9));

                        path = GetPathForDateOnlyProperty(property);
                    }
                    else
                    {
                        property = typeof(T).GetRuntimeProperties().FirstOrDefault(pr => pr.Name.ToLower() == propertyName.ToLower());
                    }

                    if (property == null)
                    {
                        throw new BadPropertyException(ordering);
                    }

                    selector = ExpressionHelper.GetPropertyPathAccessor(parameter, property.Name + path);
                }

                var method = !AscDesc(count, sorting) ?
                        (count++ == 0 ? "OrderByDescending" : "ThenByDescending") :
                        (count++ == 0 ? "OrderBy" : "ThenBy");

                expression = Expression.Call(typeof(Queryable), method,
                    new Type[] { source.ElementType, selector.Type },
                    expression, Expression.Quote(Expression.Lambda(selector, parameter)));
            }

            return source.Provider.CreateQuery<T>(expression);
        }

        private static string GetPathForDateOnlyProperty(PropertyInfo property)
        {
            if (property == null)
            {
                return null;
            }

            if (Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                return ".Value.Date";
            }

            return ".Date";
        }

        private static bool AscDesc(int index, string[] sorting)
        {
            if (sorting.Length - 1 >= index)
            {
                return sorting[index].ToLower() == "asc";
            }

            return sorting[^1].ToLower() == "asc";
        }
    }
}