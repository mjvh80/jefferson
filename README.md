Jefferson <img src="logo.png" width="48" height="48"></img> 
===========================================================
[<img src="http://img.shields.io/nuget/v/Jefferson.svg"></img>](https://www.nuget.org/packages/Jefferson/)

Jefferson is a tiny little "variable replacer" or more often called a "template engine".

```
$$#if Today = Monday$$
   Hello $$#each Users['active']$$, how is $$Name.Capitalize()$$ on this fine Monday$$/each$$?
$$/if$$
```

Strings in between `$$` signs are compiled to Linq expression trees, and the entire template itself is compiled as an expression tree, all while preserving type safety.
Expressions are compiled relative to a context value, often called a model. Names are resolved by first looking for properties and fields of this context and otherwise seeing if these are dynamically declared (e.g. using a dictionary).

Further, as in other template engines, there is support for *directives* and the ability to define new directives.
Provided directives are

`$$#if ...$$` and `$$#each ...$$`

We use this in configuration files for things like

* `$$if(IS_PROD) IncludeFile('foobar')$$`
* `<plugin active='$$IS_DEV and Blah()$$' />`
* etc.

but as output is abstracted through an `IOutputWriter` (and one that implements this using a `TextWriter` is provided) it is very easy to use this to output, say, directly to an HTTP output stream.

## Design philosophy:
* expressions are simple (but powerful), so we stop at assignment, in fact `=` is an alias for `==`
* expression syntax follows C# but extends this, e.g. it adds a Perl like regex match `=~`, "overloads" `&&` with `and` (as this is much nicer to use from xml) and more
* if anything more complex is required, add a method to the context class (aka the model)

## Supported expression syntax
Some of the following is supported (list not exhaustive):
* arithmetic
* boolean operators
* + as string concatenation
* `?` and `??` and `if else`
* regex support `=~` and `!~` and `/regex/`
* method calls (no fancy overload resolution, however, at the moment)
* indexers (currently restricted to single argument indexers)

FAQ
===

#### Why did you create this?
I'm not a believer in creationism, this evolved from more humble beginnings.

#### Is it fast?
Probably, it's compiled using Linq expression trees, but I have done no formal testing as yet.

#### Is it type safe?
Yes.

#### What's the license?
Apache v2, see [license.txt](license.txt).
