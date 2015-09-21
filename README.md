Jefferson <img src="logo.png" width="48" height="48"></img> 
===========================================================
[<img src="http://img.shields.io/nuget/v/Jefferson.svg"></img>](https://www.nuget.org/packages/Jefferson/)

Jefferson is a tiny little "variable replacer" or more often called a "template engine".

Jefferson is mainly intended to process configuration files, particular in scenarios where various components must be configured independently to enable a single feature it is useful to be able to switch this on/off with a single config change.

```
$$#define ENABLE_FEATURE_FOO=true /$$
...
<component name="foo" active="$$ENABLE_FEATURE_FOO" />

$$#if ENABLE_FEATURE_FOO$$
<component name="foo_support" ... />
...
$$/if$$
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

but as output is abstracted through an `IOutputWriter` (and one that implements this using a `TextWriter` is provided) it is very easy to use this to output, say, directly to an HTTP output stream.

## Design philosophy:
* directives are limited and kept simple to avoid e.g. config files becoming code (separation of concerns) - use e.g. Razor otherwise
* expressions are simple (but powerful), so we stop at assignment, in fact `=` is an alias for `==`
* expression syntax follows C# but extends this, e.g. it adds a Perl like regex match `=~`, "overloads" `&&` with `and` (as this is much nicer to use from xml) and more
* if anything more complex is required, add a method to the context class (aka the model)
* expressions are type safe
* errors are descriptive, helpful and provide accurate source location information
* the focus is runtime performance not compile performance

## Supported expression and template syntax
Some of the following is supported (list not exhaustive):
* arithmetic
* boolean operators
* + as string concatenation
* `?` and `??` and `if else`
* regex support `=~` and `!~` and `/regex/`
* method calls (no fancy overload resolution, however, at the moment)
* indexers (currently restricted to single argument indexers)

In template expressions the *namespaces* `$x` where `x` is a digit can be used to move up the "scope chain". Here `$0 == this`, `$1` is the parent scope etc. Note that whenever any name `foo` is resolved the context/scope chain is always walked from current to top level until a binding is found. If that is not possible an error is thrown.

## Supported Directives

### `$$#if$$`
The if directive can be used for conditional output. If the given expression evaluates to true its contents are output. Otherwise if there is an elif statement or an else statement these are executed and otherwise nothing is output.

Syntax is

```
$$#if <expr>$$
   ...
$$#elif <expr>$$
   ...
$$#else$$
   ...
$$/if$$
```

Here `$$#else` is optional, and `$$#elif$$` can occur 0 or more times. `<expr>` denotes a usual expression evaluated against the current context/scope to a Boolean value.

The if directive does not introduce a new scope.

### `$$#each$$`
The each directive can be used to iterate over an enumerable collection after which each object of the collection becomes the current scope. It's syntax is

```
$$#each SomeCollection$$
   <exprA>
$$#else$$
   <exprB>
$$/each$$
```

Here, again, the `$$#else$$` is optional. The `$$#else$$` clause is only executed if the enumerable is empty. Note that `each` introduces a new scope, namely the current object of the enumerator. This means that `<exprA>` is evaluated in a different context/scope than `<exprB>`.

### `$$#block$$`
The block directive does very little, it simply introduces a new scope. See the `$$#let$$` directive for an example of how this may be useful.
Syntax:
```
$$#block$$
   ...
$$/block$$
```

### `$$#let$$`
The let directive can be used to bind names to values.

Syntax is:

```
$$#let varA = exprA; varB = exprB; ... $$
   ...
$$/let$$
```

The expressions are evaluated within the current scope. Note that the let directive does **not** introduce a new scope itself. This means it is not possible to access a variable in the current scope that has been bound to a new value using a let expression. There is, however, a way to work around this by using a `$$#block$$` expression.

```
$$#block$$
   $$#let varA = $1.Foobar$$
      ...
   $$/let$$
$$/block$$
```
Note the use of the `$1` namespace to access the parent scope here.

The *let* directive has another syntax form which can be used to reuse "snippets" of a source file. It binds a variable to the output of the templater like so:

```
$$#let common_stuff$$
  $$foobar$$ is repeated
$$#out$$
   $$#if some_condition$$
      Some condition is true, output $$common_stuff$$.
   $$#else$$
      Else output $$common_stuff$$.
   $$/if$$
$$/let$$
```

Thus the let keyword is followed by a variable name and then the part of the body until `$$#out$$` is considered the definition of type `String`. The actual output of this directive is the body part from `$$#out$$` until `$$/let$$`.

Note that the `#define` and `#undef` directives won't function within a let directive if they match the name that has been bound.

### `$$#define$$` and `$$#undef$$`
These directives can be used to *set* variables in the current scope. This is only supported if there is a variable binder that supports the setting and unsetting of variables.

```
$$#define foo = 'bar'$$

Foo is the new $$foo$$!
```

`#define` can be usefull for snippet re-use.

`#define` can also take parameters, e.g.

```
$$#define hello(x)$$
  Hello $$x$$!
$$/define$$
```

This can then be called using, e.g. `$$hello('Marcus')$$`.
Note that currently parameter values must be strings (or will be converted to string).

FAQ
===

#### Why did you create this?
I'm not a believer in creationism, this evolved from more humble beginnings.

#### Is it fast?
It's fast as in things are compiled using Linq expression trees. Whether it's *actually* fast I don't know as I have not done performance tests just yet. Also, the focus is on runtime performance, not parsing peformance. With regards to parsing the focus has been to keep the parser relatively simple vs as performant as compturely possible.

#### Is the API stable?
No. This is just an initial version and the API may change. In particular the area of how names are resolved is likely to be updated. So I don't guarantee any backwards compatibility at the moment.

#### Is it type safe?
Yes in the sense that values are typed and ultimately converted to string before emission to output.

#### What's with all the `$$`
The `$$` was initially chosen as it conflicts least with existing file formats, in our use case. I don't think other syntaxes like `{{...}}` are necessarily any better.

### Could this be used to run other syntaxes?
Not as it stands currently, but the actual template syntax parser *could* be extracted in theory, however, I have no interest in doing so at the moment.

#### What's the license?
Apache v2, see [license.txt](license.txt).
