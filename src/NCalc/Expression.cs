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
                throw new
                    ArgumentException("Expression can't be empty", nameof(expression));

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
                throw new
                    ArgumentException("Expression can't be null", nameof(expression));

            ParsedExpression = expression;
            Options = options;
            CultureInfo = cultureInfo;
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

                    var wr = _compiledExpressions[expression];
                    logicalExpression = wr.Target as LogicalExpression;

                    if (wr.IsAlive && logicalExpression != null)
                    {
                        return logicalExpression;
                    }
                }
            }

            if (logicalExpression == null)
            {
                var lexer = new NCalcLexer(CharStreams.fromString(expression));
                var lexerErrorListener = new ErrorListener<int>();
                lexer.AddErrorListener(lexerErrorListener);

                var parser = new NCalcParser(new CommonTokenStream(lexer));
                parser.Interpreter.PredictionMode = PredictionMode.SLL;
                var parserErrorListener = new ErrorListener<IToken>();

                parser.RemoveErrorListeners();
                parser.ErrorHandler = new BailErrorStrategy();

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
                body = System.Linq.Expressions.Expression.Convert(body, typeof(TResult));
            }

            var lambda = System.Linq.Expressions.Expression.Lambda<Func<TResult>>(body);
            return lambda.Compile();
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

            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TContext), "ctx");
            var visitor = new LambdaExpressionVistor(parameter, Options);
            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof(TResult))
            {
                body = System.Linq.Expressions.Expression.Convert(body, typeof(TResult));
            }

            var lambda = System.Linq.Expressions.Expression.Lambda<Func<TContext, TResult>>(body, parameter);
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
