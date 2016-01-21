# NCalc

[![Build status](https://ci.appveyor.com/api/projects/status/ugw4wg1iws3far9m?svg=true)](https://ci.appveyor.com/project/sklose/ncalc2)

A clone of NCalc from http://ncalc.codeplex.com/ with the following changes:
- added support for CoreCLR / DNX
- embedded portable version of Antlr (no extra library/dependency required)
- added compilation of expressions to actual CLR lambdas

# Installation

Simply install the package via NuGet

```
PM> Install-Package CoreCLR-NCalc
```

# Creating Lambdas

## Simple Expressions

```
var expr = new Expression("1 + 2");
Func<int> f = expr.ToLambda<int>();
Console.WriteLine(f()); // will print 3
```

## Expressions with Functions and Parameters

```
class ExpressionContext
{
  public int Param1 { get; set; }
  public string Param2 { get; set; }
  
  public int Foo(int a, int b)
  {
    return a + b;
  }
}

var expr = new Expression("Foo([Param1], 2) = 4 && [Param2] = 'test'");
Func<ExpressionContext, bool> f = expr.ToLambda<ExpressionContext, bool>();

var context = new ExpressionContext { Param1 = 2, Param2 = "test" };
Console.WriteLine(f(context)); // will print True
```
