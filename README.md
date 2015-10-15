Jefferson <img src="logo.png" width="48" height="48"></img> 
===========================================================
[<img src="http://img.shields.io/nuget/v/Jefferson.svg"></img>](https://www.nuget.org/packages/Jefferson/)
[<img src="http://foobar.aquabrowser.com/app/rest/builds/buildType:(id:Jefferson_Build)/statusIcon"></img>](http://foobar.aquabrowser.com/viewType.html?buildTypeId=Jefferson_Build&guest=1)

Jefferson is a tiny little "variable replacer" or more often called a "template engine".

Jefferson is mainly intended to process configuration files, particular in scenarios where various components must be configured independently to enable a single feature it is useful to be able to switch this on/off with a single config change.

```
$$#define ENABLE_FEATURE_FOO=true /$$
...
<component name="foo" active="$$ENABLE_FEATURE_FOO$$" />

$$#if ENABLE_FEATURE_FOO$$
<component name="foo_support" ... />
...
$$/if$$
```

It can also be useful to *parameterize* some configuration to avoid repeating it.

```
$$#define location(url)$$
   <location url="$$url$$" ... />
$$/define

$$location('www.example.com')$$
$$location('www.google.com')$$
```

Jefferson can also be used for more general template processing, e.g.

```
$$#if Today = Monday$$
   Hello $$#each Users['active']$$, how is $$Name.Capitalize()$$ on this fine Monday$$/each$$?
$$/if$$
```

Strings in between `$$` signs are compiled to Linq expression trees, and the entire template itself is compiled as an expression tree, preserving type safety.
Expressions are compiled relative to a context value, often called a model. Names are resolved by first looking for properties and fields of this context and otherwise seeing if these are dynamically declared (e.g. using a dictionary). Internally this uses an `IVariableBinder` which can be used to customize variable binding.

Further, as in other template engines, there is support for *directives* and the ability to define new directives.
Provided directives are e.g.

`$$#if …$$`, `$$#each …$$`, `$$#define .. /$$` and `$$#let …$$`.

We use this in configuration files for things like

* `$$if(IS_PROD) IncludeFile('foobar')$$`
* `<plugin active='$$IS_DEV and Blah()$$' />`
* etc.

As output is abstracted through an `IOutputWriter` (and one that implements this using a `TextWriter` is provided) it is very easy to use this to output, say, directly to an HTTP output stream.

## Project Overview
* `Jefferson.Core` implements the core expression and replacement logic
* `Jefferson.Build` provides some services for using Jefferson during build (needs documentation)
* `Jefferson.Tests` hosts unit tests for all projects, currently
* `Jefferson.FileProcessing` provides some out-of the box support for working with files and per-file scopes (needs documentation and tests)

## Design philosophy:
* directives are limited and kept simple to avoid e.g. config files becoming code (separation of concerns) - use e.g. Razor otherwise
* expressions are simple (but powerful), so we stop at assignment, in fact `=` is an alias for `==`
* expression syntax follows C# but extends this, e.g. it adds a Perl like regex match `=~`, "overloads" `&&` with `and` (as this is much nicer to use from xml) and more
* if anything more complex is required, add a method to the context class (aka the model)
* expressions are type safe
* errors are descriptive, helpful and provide accurate source location information
* the focus is runtime performance not compile performance

## Documentation

See the <a href="https://github.com/mjvh80/jefferson/wiki">wiki</a>.

FAQ
===

#### Why did you create this?
I'm not a believer in creationism, this evolved from more humble beginnings and then just ... grew.

#### Is it fast?
It's fast as in things are compiled using Linq expression trees. Whether it's *actually* fast I don't know as I have not done performance tests just yet. Also, the focus is on runtime performance, not parsing peformance. With regards to parsing the focus has been to keep the parser relatively simple vs as performant as possible.

#### Is the API stable?
No. This is just an initial version and the API may change. In particular the area of how names are resolved is likely to be updated. So I don't guarantee any backwards compatibility at the moment.

#### Is it type safe?
Yes in the sense that values are typed and ultimately converted to string before emission to output.

#### What's with all the `$$`
The `$$` was initially chosen as it conflicts least with existing file formats, in our use case. I don't think other syntaxes like `{{...}}` are necessarily any better.

### Could this be used to run other syntaxes?
Not as it stands currently, but the actual template syntax parser *could* be extracted in theory, however, I have no interest in doing so at the moment.

#### What's the license?
Apache v2, see [license.txt](license.txt). Copyright Marcus van Houdt © 2015
