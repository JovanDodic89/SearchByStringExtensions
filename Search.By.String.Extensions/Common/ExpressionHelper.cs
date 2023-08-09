using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace Search.By.String.Extensions.Common
{
    internal class ExpressionHelper
    {
        public static MemberExpression GetPropertyPathAccessor(Expression parameter, string path)
        {
            Expression current = parameter;

            foreach (var propertyName in path.Split('.'))
            {
                current = Expression.Property(current, propertyName);
            }

            return (MemberExpression)current;
        }

        public static Expression<Func<T, bool>> AndAlso<T>(Expression<Func<T, bool>> x, Expression<Func<T, bool>> y)
        {
            var newY = new ParameterVisitor(y.Parameters, x.Parameters)
                      .VisitAndConvert(y.Body, "AndAlso");

            return Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(x.Body, newY),
                x.Parameters);
        }

        public static Expression<Func<T, bool>> OrElse<T>(Expression<Func<T, bool>> x, Expression<Func<T, bool>> y)
        {
            var newY = new ParameterVisitor(y.Parameters, x.Parameters)
                      .VisitAndConvert(y.Body, "OrElse");
            return Expression.Lambda<Func<T, bool>>(
                Expression.OrElse(x.Body, newY),
                x.Parameters);
        }

        private class ParameterVisitor : ExpressionVisitor
        {
            private readonly ReadOnlyCollection<ParameterExpression> from, to;

            public ParameterVisitor(ReadOnlyCollection<ParameterExpression> from,
                ReadOnlyCollection<ParameterExpression> to)
            {
                this.from = from;
                this.to = to;
            }
            protected override Expression VisitParameter(ParameterExpression node)
            {
                for (int i = 0; i < from.Count; i++)
                {
                    if (node == from[i]) return to[i];
                }
                return node;
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Object == null && node.Method.IsGenericMethod)
                {
                    var arguments = Visit(node.Arguments);
                    var genericArguments = node.Method.GetGenericArguments().ToArray();
                    var method = node.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArguments);
                    return Expression.Call(method, arguments);
                }

                return base.VisitMethodCall(node);
            }
        }
    }
}
