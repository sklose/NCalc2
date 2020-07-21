using System;
using System.Collections;
using System.Collections.Generic;
using NCalc.Domain;
using Antlr.Runtime;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using FastExpressionCompiler;
using FastExpressionCompiler.LightExpression;

namespace NCalc
{
    public class Expression
    {
        public EvaluateOptions Options { get; set; }

        /// <summary>
        /// Textual representation of the expression to evaluate.
        /// </summary>
        protected string OriginalExpression;

        public Expression(string expression) : this(expression, EvaluateOptions.None)
        {
        }

        public Expression(string expression, EvaluateOptions options)
        {
            if (String.IsNullOrEmpty(expression))
                throw new
                    ArgumentException("Expression can't be empty", "expression");

            OriginalExpression = expression;
            Options = options;
        }

        public Expression(LogicalExpression expression) : this(expression, EvaluateOptions.None)
        {
        }

        public Expression(LogicalExpression expression, EvaluateOptions options)
        {
            if (expression == null)
                throw new
                    ArgumentException("Expression can't be null", "expression");

            ParsedExpression = expression;
            Options = options;
        }

        #region Cache management
        private static bool _cacheEnabled = true;
        private static ConcurrentDictionary<string, WeakReference> _compiledExpressions = new ConcurrentDictionary<string, WeakReference>();

        public static bool CacheEnabled
        {
            get { return _cacheEnabled; }
            set
            {
                _cacheEnabled = value;

                if (!CacheEnabled)
                {
                    // Clears cache
                    _compiledExpressions = new ConcurrentDictionary<string, WeakReference>();
                }
            }
        }

        /// <summary>
        /// Removed unused entries from cached compiled expression
        /// </summary>
        private static void CleanCache()
        {
            try
            {
                var keysToRemove = new List<string>();
                foreach (var de in _compiledExpressions)
                {
                    if (!de.Value.IsAlive)
                    {
                        keysToRemove.Add(de.Key);
                    }
                }

                foreach (string key in keysToRemove)
                {
                    _compiledExpressions.TryRemove(key, out _);
                    //Debug.WriteLine("Cache entry released: " + key);
                }
            }
            finally
            {
            }
        }

        #endregion

        public static LogicalExpression Compile(string expression, bool nocache)
        {
            LogicalExpression logicalExpression = null;

            if (_cacheEnabled && !nocache)
            {
                if (_compiledExpressions.ContainsKey(expression))
                {
                    //Debug.WriteLine("Expression retrieved from cache: " + expression);
                    if (_compiledExpressions.TryGetValue(expression, out var wr))
                    {
                        logicalExpression = wr.Target as LogicalExpression;

                        if (wr.IsAlive && logicalExpression != null)
                        {
                            return logicalExpression;
                        }
                    }
                }
            }

            if (logicalExpression == null)
            {
                var lexer = new NCalcLexer(new ANTLRStringStream(expression));
                var parser = new NCalcParser(new CommonTokenStream(lexer));

                logicalExpression = parser.ncalcExpression().value;

                if (parser.Errors != null && parser.Errors.Count > 0)
                {
                    throw new EvaluationException(String.Join(Environment.NewLine, parser.Errors.ToArray()));
                }

                if (_cacheEnabled && !nocache)
                {
                    try
                    {
                        if (_compiledExpressions.TryGetValue(expression, out var wr))
                        {
                            _compiledExpressions.TryUpdate(expression, new WeakReference(logicalExpression), wr);
                        }
                        else
                        {
                            _compiledExpressions.TryAdd(expression, new WeakReference(logicalExpression));
                        }
                    }
                    finally
                    {
                        CleanCache();
                    }
                    //Debug.WriteLine("Expression added to cache: " + expression);
                }
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

        protected Dictionary<string, IEnumerator> ParameterEnumerators;
        protected Dictionary<string, object> ParametersBackup;

        public Func<TResult> ToLambda<TResult>()
        {
            if (HasErrors())
            {
                throw new EvaluationException(Error, ErrorException);
            }

            if (ParsedExpression == null)
            {
                ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
            }

            var visitor = new LambdaExpressionVistor(Parameters, Options);
            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof(TResult))
            {
                body = FastExpressionCompiler.LightExpression.Expression.Convert(body, typeof(TResult));
            }

            var lambda = FastExpressionCompiler.LightExpression.Expression.Lambda<Func<TResult>>(body);
            return lambda.CompileFast();
        }

        public Func<TContext, TResult> ToLambda<TContext, TResult>()
        {
            if (HasErrors())
            {
                throw new EvaluationException(Error, ErrorException);
            }

            if (ParsedExpression == null)
            {
                ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
            }

            var parameter = FastExpressionCompiler.LightExpression.Expression.Parameter(typeof(TContext), "ctx");
            var visitor = new LambdaExpressionVistor(parameter, Options);
            visitor.EvaluateFunction += EvaluateFunction;
            visitor.EvaluateParameter += EvaluateParameter;
            visitor.Parameters = Parameters;

            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof(TResult))
            {
                body = FastExpressionCompiler.LightExpression.Expression.Convert(body, typeof(TResult));
            }

            var lambda =
                FastExpressionCompiler.LightExpression.Expression
                    .Lambda<Func<TContext, TResult>>(body, parameter);
            return lambda.CompileFast();
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


            var visitor = new EvaluationVisitor(Options);
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
                ParametersBackup = new Dictionary<string, object>();
                foreach (string key in Parameters.Keys)
                {
                    ParametersBackup.Add(key, Parameters[key]);
                }

                ParameterEnumerators = new Dictionary<string, IEnumerator>();

                foreach (object parameter in Parameters.Values)
                {
                    if (parameter is IEnumerable)
                    {
                        int localsize = 0;
                        foreach (object o in (IEnumerable)parameter)
                        {
                            localsize++;
                        }

                        if (size == -1)
                        {
                            size = localsize;
                        }
                        else if (localsize != size)
                        {
                            throw new EvaluationException("When IterateParameters option is used, IEnumerable parameters must have the same number of items");
                        }
                    }
                }

                foreach (string key in Parameters.Keys)
                {
                    var parameter = Parameters[key] as IEnumerable;
                    if (parameter != null)
                    {
                        ParameterEnumerators.Add(key, parameter.GetEnumerator());
                    }
                }

                var results = new List<object>();
                for (int i = 0; i < size; i++)
                {
                    foreach (string key in ParameterEnumerators.Keys)
                    {
                        IEnumerator enumerator = ParameterEnumerators[key];
                        enumerator.MoveNext();
                        Parameters[key] = enumerator.Current;
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
