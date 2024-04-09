using System.Threading;

using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Misc;
using NCalc.Domain;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using L = System.Linq.Expressions;

namespace NCalc
{
    public class Expression
    {
        public EvaluateOptions Options { get; set; }

        /// <summary>
        /// Textual representation of the expression to evaluate.
        /// </summary>
        protected string OriginalExpression;

        /// <summary>
        /// Get or set the culture info.
        /// </summary>
        protected CultureInfo CultureInfo { get; set; }


        public Expression(string expression) : this(expression, EvaluateOptions.None, CultureInfo.CurrentCulture)
        {
        }

        public Expression(string expression, CultureInfo cultureInfo) : this(expression, EvaluateOptions.None, cultureInfo)
        {
        }

        public Expression(string expression, EvaluateOptions options) : this(expression, options, CultureInfo.CurrentCulture)
        {
        }

        public Expression(string expression, EvaluateOptions options, CultureInfo cultureInfo)
        {
            if (String.IsNullOrEmpty(expression))
                throw new ArgumentException("Expression can't be empty", nameof(expression));

            OriginalExpression = expression;
            Options = options;
            CultureInfo = cultureInfo;
        }

        public Expression(LogicalExpression expression, EvaluateOptions options) : this(expression, options, CultureInfo.CurrentCulture)
        {
        }

        public Expression(LogicalExpression expression, EvaluateOptions options, CultureInfo cultureInfo)
        {
            if (expression == null)
                throw new ArgumentException("Expression can't be null", nameof(expression));

            ParsedExpression = expression;
            Options = options;
            CultureInfo = cultureInfo;
        }

        #region Cache management
        private static bool _cacheEnabled = true;
        private static readonly ConcurrentDictionary<string, WeakReference<LogicalExpression>> _compiledExpressions =
            new ConcurrentDictionary<string, WeakReference<LogicalExpression>>();
        internal static int CurrentCachedCompilations => _compiledExpressions.Count;

        private static int _totalCachedCompilations = 0;
        internal static int TotalCachedCompilations => _totalCachedCompilations;
        public static int CacheCleanInterval { get; set; } = 1000;

        public static bool CacheEnabled
        {
            get { return _cacheEnabled; }
            set
            {
                _cacheEnabled = value;

                if (!CacheEnabled)
                {
                    // Clears cache
                    _compiledExpressions.Clear();
                }
            }
        }


        /// <summary>
        /// Removed unused entries from cached compiled expression
        /// </summary>
        private static void CleanCache()
        {
            foreach (var kvp in _compiledExpressions)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    _compiledExpressions.TryRemove(kvp.Key, out _);
                    //Debug.WriteLine("Cache entry released: " + key);
                }
            }
        }

        #endregion

        public static LogicalExpression Compile(string expression, bool nocache)
        {
            if (_cacheEnabled && !nocache)
            {
                if (_compiledExpressions.TryGetValue(expression, out var wr))
                {
                    //Debug.WriteLine("Expression retrieved from cache: " + expression);

                    if (wr.TryGetTarget(out var target))
                        return target;
                }
            }

            var lexer = new NCalcLexer(CharStreams.fromString(expression));
            var lexerErrorListener = new ErrorListener<int>();
            lexer.AddErrorListener(lexerErrorListener);

            var parser = new NCalcParser(new CommonTokenStream(lexer));
            parser.Interpreter.PredictionMode = PredictionMode.SLL;
            var parserErrorListener = new ErrorListener<IToken>();

            parser.RemoveErrorListeners();
            parser.ErrorHandler = new BailErrorStrategy();

            LogicalExpression logicalExpression = null;

            try
            {
                logicalExpression = parser.ncalcExpression().value;
            }
            catch (ParseCanceledException)
            {
                lexer.Reset();

                parser.Reset();
                parser.ErrorHandler = new DefaultErrorStrategy();
                parser.Interpreter.PredictionMode = PredictionMode.LL;
                parser.AddErrorListener(parserErrorListener);
                logicalExpression = parser.ncalcExpression().value;
            }

            if (parserErrorListener.Errors.Count > 0 || lexerErrorListener.Errors.Count > 0)
            {
                var errors = string.Join(Environment.NewLine,
                    lexerErrorListener.Errors.Select(e => e.ToString()).Union(
                    parserErrorListener.Errors.Select(e => e.ToString()))
                );

                throw new EvaluationException(errors);
            }

            if (_cacheEnabled && !nocache)
            {
                _compiledExpressions[expression] = new WeakReference<LogicalExpression>(logicalExpression);

                if (Interlocked.Increment(ref _totalCachedCompilations) % CacheCleanInterval == 0)
                {
                    CleanCache();
                }
                //Debug.WriteLine("Expression added to cache: " + expression);
            }

            return logicalExpression;
        }

        /// <summary>
        /// Pre-compiles the expression in order to check syntax errors.
        /// If errors are detected, the Error property contains the message.
        /// </summary>
        /// <returns>True if the expression syntax is correct, otherwiser False</returns>
        public bool HasErrors()
        {
            try
            {
                if (ParsedExpression == null)
                {
                    ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
                }

                // In case HasErrors() is called multiple times for the same expression
                return ParsedExpression != null && Error != null;
            }
            catch (Exception e)
            {
                Error = e.Message;
                ErrorException = e;
                return true;
            }
        }

        public string Error { get; private set; }

        public Exception ErrorException { get; private set; }

        public LogicalExpression ParsedExpression { get; private set; }

        private struct Void { };

        public struct ExpressionWithParameter
        {
            public L.Expression Expr;
            public L.ParameterExpression Param;
        }

        private ExpressionWithParameter ToLinqExpressionInternal<TContext, TResult>()
        {
            if (HasErrors())
            {
                throw new EvaluationException(Error, ErrorException);
            }

            if (ParsedExpression == null)
            {
                ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
            }

            LambdaExpressionVistor visitor;
            L.ParameterExpression parameter = null;
            if (typeof(TContext) != typeof(Void))
            {
                parameter = L.Expression.Parameter(typeof(TContext), "ctx");
                visitor = new LambdaExpressionVistor(parameter, Options);
            }
            else
            {
                visitor = new LambdaExpressionVistor(Parameters, Options);
            }
            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof(TResult))
            {
                body = L.Expression.Convert(body, typeof(TResult));
            }

            return new ExpressionWithParameter { Expr = body, Param = parameter };
        }

        protected virtual L.Expression ToLinqExpression<TResult>()
        {
            return ToLinqExpressionInternal<Void, TResult>().Expr;
        }

        protected virtual ExpressionWithParameter ToLinqExpression<TContext, TResult>()
        {
            return ToLinqExpressionInternal<TContext, TResult>();
        }

        public virtual Func<TResult> ToLambda<TResult>()
        {
            L.Expression body = ToLinqExpression<TResult>();
            var lambda = L.Expression.Lambda<Func<TResult>>(body);
            return lambda.Compile();
        }

        public virtual Func<TContext, TResult> ToLambda<TContext, TResult>()
        {
            ExpressionWithParameter exprAndParamTuple = ToLinqExpression<TContext, TResult>();
            var lambda = L.Expression.Lambda<Func<TContext, TResult>>(exprAndParamTuple.Expr, exprAndParamTuple.Param);
            return lambda.Compile();
        }

        public object Evaluate()
        {
            if (HasErrors())
            {
                throw new EvaluationException(Error, ErrorException);
            }

            if (ParsedExpression == null)
            {
                ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
            }


            var visitor = new EvaluationVisitor(Options, CultureInfo);
            visitor.EvaluateFunction += EvaluateFunction;
            visitor.EvaluateParameter += EvaluateParameter;
            visitor.Parameters = Parameters;

            // Add a "null" parameter which returns null if configured to do so
            // Configured as an option to ensure no breaking changes for historical use
            if ((Options & EvaluateOptions.AllowNullParameter) == EvaluateOptions.AllowNullParameter && !visitor.Parameters.ContainsKey("null"))
            {
                visitor.Parameters["null"] = null;
            }

            // if array evaluation, execute the same expression multiple times
            if ((Options & EvaluateOptions.IterateParameters) == EvaluateOptions.IterateParameters)
            {
                int size = -1;

                var parameterEnumerators = new Dictionary<string, IEnumerator>();

                foreach (var parameter in Parameters)
                {
                    if (parameter.Value is IEnumerable enumerable)
                    {
                        parameterEnumerators.Add(parameter.Key, enumerable.GetEnumerator());

                        int localSize = 0;
                        foreach (object o in enumerable)
                        {
                            localSize++;
                        }

                        if (size == -1)
                        {
                            size = localSize;
                        }
                        else if (localSize != size)
                        {
                            throw new EvaluationException("When IterateParameters option is used, IEnumerable parameters must have the same number of items");
                        }
                    }
                }

                var results = new List<object>();
                for (int i = 0; i < size; i++)
                {
                    foreach (var parameterEnumerator in parameterEnumerators)
                    {
                        IEnumerator enumerator = parameterEnumerator.Value;
                        enumerator.MoveNext();
                        Parameters[parameterEnumerator.Key] = enumerator.Current;
                    }

                    ParsedExpression.Accept(visitor);
                    results.Add(visitor.Result);
                }

                return results;
            }

            ParsedExpression.Accept(visitor);
            return visitor.Result;
        }

        public event EvaluateFunctionHandler EvaluateFunction;
        public event EvaluateParameterHandler EvaluateParameter;

        private Dictionary<string, object> _parameters;

        public Dictionary<string, object> Parameters
        {
            get { return _parameters ?? (_parameters = new Dictionary<string, object>()); }
            set { _parameters = value; }
        }
    }
}
