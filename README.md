# NCalc

[![Build status](https://ci.appveyor.com/api/projects/status/ugw4wg1iws3far9m?svg=true)](https://ci.appveyor.com/project/sklose/ncalc2) [![NuGet](https://img.shields.io/nuget/v/CoreCLR-NCalc.svg)](https://www.nuget.org/packages/CoreCLR-NCalc) [![NuGet](https://img.shields.io/nuget/dt/CoreCLR-NCalc.svg)](https://www.nuget.org/packages/CoreCLR-NCalc)

| :warning: This repository is currently only passively maintained. If there are fully fledged PRs, I will occasional merge those in and publish new versions. If you would like to become a maintainer and work on some of the open issues, please reply here https://github.com/sklose/NCalc2/issues/61
| --- |

A clone of NCalc from http://ncalc.codeplex.com/ with the following changes:
- added support for .NET Standard 2.0+
- added compilation of expressions to actual CLR lambdas

# Installation

Simply install the package via NuGet

```powershell
PM> Install-Package CoreCLR-NCalc
```

# Creating Lambdas

## Simple Expressions

```csharp
var expr = new Expression("1 + 2");
Func<int> f = expr.ToLambda<int>();
Console.WriteLine(f()); // will print 3
```

## Expressions with Functions and Parameters

```csharp
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

## Performance Comparison

The measurements were done during CI runs on AppVeyor and fluctuate a lot in between runs, but the speedup is consistently in the thousands of percent range. The speedup measured on actual hardware was even higher (between 10,000% and 35,000%).

| Formula  | Description | Expression Evaluations / sec | Lambda Evaluations / sec | Speedup |
| ------------- | ------------- | ------------- | ------------- | ------------- |
| (4 * 12 / 7) + ((9 * 2) % 8)  | Simple Arithmetics | 474,247.87 | 32,691,490.41 | 6,793.33% |
| 5 * 2 = 2 * 5 && (1 / 3.0) * 3 = 1  | Simple Arithmetics | 276,226.31 | 93,222,709.05 | 33,648.67% |
| [Param1] * 7 + [Param2]  | Constant Values| 707,493.27 | 21,766,101.47 | 2,976.51% |
| [Param1] * 7 + [Param2]  | Dynamic Values | 582,832.10 | 21,400,445.13 | 3,571.80% |
| Foo([Param1] * 7, [Param2])  | Dynamic Values and Function Call | 594,259.69 | 17,209,334.34 | 2,795.93% |
