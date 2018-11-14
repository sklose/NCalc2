using System;
using System.Collections;
using System.Collections.Generic;
using NCalc.Domain;
using Antlr.Runtime;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace NCalc
{
    public class Expression
    {
        // Use "self managed" dictionary because the generic dictionary is too slow when accessed from generated lambda.
        private string[] _parameterNames = new string[10];
        private object[] _parameterValues = new object[10];
        private uint _currentParameterIndex;

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
        private static Dictionary<string, WeakReference> _compiledExpressions = new Dictionary<string, WeakReference>();
        private static readonly ReaderWriterLockSlim Rwl = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public static bool CacheEnabled
        {
            get { return _cacheEnabled; }
            set
            {
                _cacheEnabled = value;

                if (!CacheEnabled)
                {
                    // Clears cache
                    _compiledExpressions = new Dictionary<string, WeakReference>();
                }
            }
        }

        /// <summary>
        /// Removed unused entries from cached compiled expression
        /// </summary>
        private static void CleanCache()
        {
            var keysToRemove = new List<string>();

            try
            {
                Rwl.EnterReadLock();
                foreach (var de in _compiledExpressions)
                {
                    if (!de.Value.IsAlive)
                    {
                        keysToRemove.Add(de.Key);
                    }
                }


                foreach (string key in keysToRemove)
                {
                    _compiledExpressions.Remove(key);
                    Debug.WriteLine("Cache entry released: " + key);
                }
            }
            finally
            {
                Rwl.ExitReadLock();
            }
        }

        #endregion

        public static LogicalExpression Compile(string expression, bool nocache)
        {
            LogicalExpression logicalExpression = null;

            if (_cacheEnabled && !nocache)
            {
                try
                {
                    Rwl.EnterReadLock();

                    if (_compiledExpressions.ContainsKey(expression))
                    {
                        Debug.WriteLine("Expression retrieved from cache: " + expression);
                        var wr = _compiledExpressions[expression];
                        logicalExpression = wr.Target as LogicalExpression;

                        if (wr.IsAlive && logicalExpression != null)
                        {
                            return logicalExpression;
                        }
                    }
                }
                finally
                {
                    Rwl.ExitReadLock();
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
                        Rwl.EnterWriteLock();
                        _compiledExpressions[expression] = new WeakReference(logicalExpression);
                    }
                    finally
                    {
                        Rwl.ExitWriteLock();
                    }

                    CleanCache();

                    Debug.WriteLine("Expression added to cache: " + expression);
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
            catch(Exception e)
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

            var visitor = new LambdaExpressionVisitor(_parameterNames, _parameterValues, typeof(TResult), Options);
            visitor.EvaluateFunction += EvaluateFunctionExpression;
            visitor.EvaluateParameter += EvaluateParameterExpression;

            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof(TResult))
            {
                body = System.Linq.Expressions.Expression.Convert(body, typeof(TResult));
            }

            if (!Parameters.Any())
            {
                // no parameter used
                // formula is static and can be used directly
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<TResult>>(body);
                return lambda.Compile();
            }

            // create lambda with parameters context
            var innerLambda = System.Linq.Expressions.Expression.Lambda<Func<object[], TResult>>(
                body,
                visitor.Context);
            var invokeInner = System.Linq.Expressions.Expression.Invoke(
                innerLambda,
                System.Linq.Expressions.Expression.Constant(_parameterValues));

            // create parameter free lambda to be called directly
            var outerLambda = System.Linq.Expressions.Expression.Lambda<Func<TResult>>(invokeInner);
            return outerLambda.Compile();
        }

        public Func<TContext, TResult> ToLambda<TContext, TResult>() where TContext : class
        {
            if (HasErrors())
            {
                throw new EvaluationException(Error, ErrorException);
            }

            if (ParsedExpression == null)
            {
                ParsedExpression = Compile(OriginalExpression, (Options & EvaluateOptions.NoCache) == EvaluateOptions.NoCache);
            }

            var visitor = new LambdaExpressionVisitor(typeof(TContext), Options);
            visitor.EvaluateFunction += EvaluateFunctionExpression;
            visitor.EvaluateParameter += EvaluateParameterExpression;

            ParsedExpression.Accept(visitor);

            var body = visitor.Result;
            if (body.Type != typeof (TResult))
            {
                body = System.Linq.Expressions.Expression.Convert(body, typeof (TResult));
            }

            var lambda = System.Linq.Expressions.Expression.Lambda<Func<TContext, TResult>>(body, visitor.Context);
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


            var visitor = new EvaluationVisitor(Options);
            visitor.EvaluateFunction += EvaluateFunction;
            visitor.EvaluateParameter += EvaluateParameter;
            visitor.Parameters = _parameterNames
                .TakeWhile(x => x != null)
                .Zip(_parameterValues, (name, value) => new { name, value })
                .ToDictionary(val => val.name, val => val.value);

            // if array evaluation, execute the same expression multiple times
            if ((Options & EvaluateOptions.IterateParameters) == EvaluateOptions.IterateParameters)
            {
                int size = -1;
                ParametersBackup = Parameters.ToDictionary(x => x.Key, x => x.Value);

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
                        var index = Array.IndexOf(_parameterNames, key);
                        _parameterValues[index] = enumerator.Current;
                    }

                    visitor.Parameters = _parameterNames
                        .TakeWhile(x => x != null)
                        .Zip(_parameterValues, (name, value) => new { name, value })
                        .ToDictionary(val => val.name, val => val.value);

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
        public event EventHandler<FunctionExpressionEventArgs> EvaluateFunctionExpression;
        public event EventHandler<ParameterExpressionEventArgs> EvaluateParameterExpression;

        public IReadOnlyDictionary<string, object> Parameters
        {
            get
            {
                return _parameterNames
                    .TakeWhile(x => x != null)
                    .Zip(_parameterValues, (name, value) => new {name, value})
                    .ToDictionary(val => val.name, val => val.value);
            }
        }

        public void SetParameters(IReadOnlyDictionary<string, object> parameters)
        {
            _parameterNames = parameters.Keys.ToArray();
            _parameterValues = parameters.Values.ToArray();
        }

        public void SetParameter(string key, object value)
        {
            var index = Array.IndexOf(_parameterNames, key);
            if (index < 0)
            {
                AddParameter(key, value);
                return;
            }

            _parameterValues[index] = value;
        }

        public void SetParameter(uint index, object value)
        {
            _parameterValues[index] = value;
        }

        public uint AddParameter(string key, object value)
        {
            if (_currentParameterIndex >= _parameterNames.Length)
            {
                Resize();
            }

            _parameterNames[_currentParameterIndex] = key;
            _parameterValues[_currentParameterIndex] = value;

            return _currentParameterIndex++;
        }

        private void Resize()
        {
            var names = _parameterNames;
            _parameterNames = new string[names.Length + 10];
            Array.Copy(names, _parameterNames, names.Length);
            var values = _parameterValues;
            _parameterValues = new object[values.Length + 10];
            Array.Copy(values, _parameterValues, values.Length);
        }
    }
}
