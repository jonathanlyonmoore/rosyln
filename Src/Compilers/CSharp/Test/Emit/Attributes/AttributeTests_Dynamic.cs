﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.CSharp.UnitTests.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    public class AttributeTests_Dynamic : WellKnownAttributesTestBase
    {
        private static string DynamicTestSource = @"
public class Base0 { }
public class Base1<T> { }
public class Base2<T, U> { }

public class Outer<T> : Base1<dynamic>
{
    public class Inner<U, V> : Base2<dynamic, V>
    {
        public class InnerInner<W> : Base1<dynamic> { }
    }
}

public class Outer2<T> : Base1<dynamic>
{
    public class Inner2<U, V> : Base0
    {
        public class InnerInner2<W> : Base0 { }
    }
}

public class Outer3
{
    public class Inner3<U>
    {
        public static Outer3.Inner3<dynamic> field1 = null;
    }
}

public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
where T : Derived<T>
{
    public static dynamic field1;
    public static dynamic[] field2;
    public static dynamic[][] field3;

    public const dynamic field4 = null;
    public const dynamic[] field5 = null;
    public const dynamic[][] field6 = null;
    public const dynamic[][] field7 = null;

    public Outer<T>.Inner<int, T>.InnerInner<Outer<dynamic>> field8 = null;
    public Outer<dynamic>.Inner<T, T>.InnerInner<T> field9 = null;
    public Outer<Outer<dynamic>.Inner<T, dynamic>>.Inner<dynamic, T>.InnerInner<T> field10 = null;
    public Outer<T>.Inner<dynamic, dynamic>.InnerInner<T> field11 = null;
    public Outer<T>.Inner<T, T>.InnerInner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>> field12 = null;
    public Outer<dynamic>.Inner<Outer<T>, T>.InnerInner<dynamic> field13 = null;
    public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;

    public Outer<dynamic>.Inner<Outer<dynamic>, T>.InnerInner<dynamic>[] field15 = null;
    public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
    public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;

    public static dynamic F1(dynamic x) { return x; }
    public static dynamic F2(ref dynamic x) { return x; }
    public static dynamic[] F3(dynamic[] x) { return x; }
    public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }

    public static dynamic Prop1 { get { return field1; } }
    public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }

    public dynamic this[dynamic param]
    {
        get { return null; }
        set {}
    }
}

public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }

public struct Struct
{
    public static Outer<dynamic>.Inner<dynamic, Struct?> nullableField;
}

public delegate dynamic[] MyDelegate(dynamic[] x);
";
        [Fact]
        public void TestCompileDynamicAttributes()
        {
            CompileAndVerify(DynamicTestSource, options: TestOptions.UnsafeDll, additionalRefs: new[] { SystemCoreRef });
        }

        [Fact]
        public void TestDynamicAttributesAreSynthesized()
        {
            var comp = CreateCompilationWithMscorlibAndSystemCore(DynamicTestSource, compOptions: TestOptions.UnsafeDll);
            DynamicAttributeValidator.ValidateDynamicAttributes(comp);
        }

        private struct DynamicAttributeValidator
        {
            private readonly SourceAssemblySymbol srcAssembly;
            private readonly CSharpCompilation comp;
            private readonly MethodSymbol dynamicAttributeCtorNoArgs, dynamicAttributeCtorTransformFlags;
            private readonly NamedTypeSymbol base0Class, base1Class, base2Class, derivedClass;
            private readonly NamedTypeSymbol outerClass, innerClass, innerInnerClass;
            private readonly NamedTypeSymbol outer2Class, inner2Class, innerInner2Class;
            private readonly NamedTypeSymbol outer3Class, inner3Class;
            private readonly NamedTypeSymbol unsafeClass;
            private readonly NamedTypeSymbol structType;
            private readonly NamedTypeSymbol synthesizedMyDelegateType;
            private bool[] expectedTransformFlags;

            private DynamicAttributeValidator(CSharpCompilation compilation)
            {
                this.comp = compilation;
                this.srcAssembly = compilation.SourceAssembly;
                NamespaceSymbol globalNamespace = srcAssembly.Modules[0].GlobalNamespace;

                this.base0Class = globalNamespace.GetMember<NamedTypeSymbol>("Base0");
                this.base1Class = globalNamespace.GetMember<NamedTypeSymbol>("Base1");
                this.base2Class = globalNamespace.GetMember<NamedTypeSymbol>("Base2");
                this.derivedClass = globalNamespace.GetMember<NamedTypeSymbol>("Derived");
                this.outerClass = globalNamespace.GetMember<NamedTypeSymbol>("Outer");
                this.innerClass = this.outerClass.GetTypeMember("Inner");
                this.innerInnerClass = this.innerClass.GetTypeMember("InnerInner");
                this.outer2Class = globalNamespace.GetMember<NamedTypeSymbol>("Outer2");
                this.inner2Class = this.outer2Class.GetTypeMember("Inner2");
                this.innerInner2Class = this.inner2Class.GetTypeMember("InnerInner2");
                this.outer3Class = globalNamespace.GetMember<NamedTypeSymbol>("Outer3");
                this.inner3Class = this.outer3Class.GetTypeMember("Inner3");
                this.unsafeClass = globalNamespace.GetMember<NamedTypeSymbol>("UnsafeClass");
                this.structType = globalNamespace.GetMember<NamedTypeSymbol>("Struct");
                this.synthesizedMyDelegateType = globalNamespace.GetMember<NamedTypeSymbol>("MyDelegate");
                
                this.dynamicAttributeCtorNoArgs = (MethodSymbol)compilation.GetWellKnownTypeMember(WellKnownMember.System_Runtime_CompilerServices_DynamicAttribute__ctor);
                this.dynamicAttributeCtorTransformFlags = (MethodSymbol)compilation.GetWellKnownTypeMember(WellKnownMember.System_Runtime_CompilerServices_DynamicAttribute__ctorTransformFlags);

                this.expectedTransformFlags = null;
            }

            internal static void ValidateDynamicAttributes(CSharpCompilation comp)
            {
                var validator = new DynamicAttributeValidator(comp);
                
                validator.ValidateAttributesOnNamedTypes();
                validator.ValidateAttributesOnFields();
                validator.ValidateAttributesOnMethodReturnValueAndParameters();
                validator.ValidateAttributesOnProperty();
                validator.ValidateAttributesOnIndexer();
                validator.ValidateAttributesForPointerType();
                validator.ValidateAttributesForNullableType();
                validator.ValidateAttributesForSynthesizedDelegateMembers();
            }

            private void ValidateDynamicAttribute(Symbol symbol, bool expectedDynamicAttribute, bool[] expectedTransformFlags = null, bool forReturnType = false)
            {
                ValidateDynamicAttribute(symbol, this.comp, this.dynamicAttributeCtorNoArgs,
                     this.dynamicAttributeCtorTransformFlags, expectedDynamicAttribute, expectedTransformFlags, forReturnType);
            }

            internal static void ValidateDynamicAttribute(Symbol symbol, CSharpCompilation comp, bool expectedDynamicAttribute, bool[] expectedTransformFlags = null, bool forReturnType = false)
            {
                var dynamicAttributeCtorNoArgs = (MethodSymbol)comp.GetWellKnownTypeMember(WellKnownMember.System_Runtime_CompilerServices_DynamicAttribute__ctor);
                var dynamicAttributeCtorTransformFlags = (MethodSymbol)comp.GetWellKnownTypeMember(WellKnownMember.System_Runtime_CompilerServices_DynamicAttribute__ctorTransformFlags);
                ValidateDynamicAttribute(symbol, comp, dynamicAttributeCtorNoArgs, dynamicAttributeCtorTransformFlags, expectedDynamicAttribute, expectedTransformFlags, forReturnType);
            }

            internal static void ValidateDynamicAttribute(Symbol symbol, CSharpCompilation comp, MethodSymbol dynamicAttributeCtorNoArgs,
                 MethodSymbol dynamicAttributeCtorTransformFlags, bool expectedDynamicAttribute, bool[] expectedTransformFlags = null, bool forReturnType = false)
            {
                Assert.True(!forReturnType || symbol.Kind == SymbolKind.Method, "Incorrect usage of ValidateDynamicAttribute");

                var synthesizedDynamicAttributes = symbol.GetSynthesizedAttributes(forReturnType).Where((attr) => string.Equals(attr.AttributeClass.Name, "DynamicAttribute", StringComparison.Ordinal));

                if (!expectedDynamicAttribute)
                {
                    Assert.Empty(synthesizedDynamicAttributes);
                }
                else
                {
                    Assert.Equal(1, synthesizedDynamicAttributes.Count());

                    var dynamicAttribute = synthesizedDynamicAttributes.First();
                    var expectedCtor = expectedTransformFlags == null ? dynamicAttributeCtorNoArgs : dynamicAttributeCtorTransformFlags;
                    Assert.NotNull(expectedCtor);
                    Assert.Equal(expectedCtor, dynamicAttribute.AttributeConstructor);

                    if (expectedTransformFlags == null)
                    {
                        // Dynamic()
                        Assert.Equal(0, dynamicAttribute.CommonConstructorArguments.Length);
                    }
                    else
                    {
                        // Dynamic(bool[] transformFlags)
                        Assert.Equal(1, dynamicAttribute.CommonConstructorArguments.Length);

                        TypedConstant argument = dynamicAttribute.CommonConstructorArguments[0];
                        Assert.Equal(TypedConstantKind.Array, argument.Kind);

                        ImmutableArray<TypedConstant> actualTransformFlags = argument.Values;
                        Assert.Equal(expectedTransformFlags.Length, actualTransformFlags.Length);
                        TypeSymbol booleanType = comp.GetSpecialType(SpecialType.System_Boolean);

                        for (int i = 0; i < actualTransformFlags.Length; i++)
                        {
                            TypedConstant actualTransformFlag = actualTransformFlags[i];

                            Assert.Equal(TypedConstantKind.Primitive, actualTransformFlag.Kind);
                            Assert.Equal(booleanType, actualTransformFlag.Type);
                            Assert.Equal(expectedTransformFlags[i], (bool)actualTransformFlag.Value);
                        }
                    }
                }
            }

            private void ValidateAttributesOnNamedTypes()
            {
                // public class Base0 { }
                ValidateDynamicAttribute(base0Class, expectedDynamicAttribute: false);

                // public class Base1<T> { }
                ValidateDynamicAttribute(base1Class, expectedDynamicAttribute: false);

                // public class Base2<T, U> { }
                ValidateDynamicAttribute(base2Class, expectedDynamicAttribute: false);

                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Assert.True(derivedClass.BaseType.ContainsDynamic());
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0B 00 00 00 * 00 01 00 00 01 00 00 01 00 01 01 * 00 00 )
                expectedTransformFlags = new bool[] { false, true, false, false, true, false, false, true, false, true, true };
                ValidateDynamicAttribute(derivedClass, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // public class Outer<T> : Base1<dynamic>
                Assert.True(outerClass.BaseType.ContainsDynamic());
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(outerClass, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // public class Inner<U, V> : Base2<dynamic, V>
                Assert.True(innerClass.BaseType.ContainsDynamic());
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 03 00 00 00 * 00 01 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true, false };
                ValidateDynamicAttribute(innerClass, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // public class InnerInner<W> : Base1<dynamic> { }
                Assert.True(innerInnerClass.BaseType.ContainsDynamic());
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(innerInnerClass, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // public class Outer2<T> : Base1<dynamic>
                Assert.True(outer2Class.BaseType.ContainsDynamic());
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(outer2Class, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // public class Inner2<U, V> : Base0
                Assert.False(inner2Class.BaseType.ContainsDynamic());
                ValidateDynamicAttribute(inner2Class, expectedDynamicAttribute: false);

                // public class InnerInner2<W> : Base0 { }
                Assert.False(innerInner2Class.BaseType.ContainsDynamic());
                ValidateDynamicAttribute(innerInner2Class, expectedDynamicAttribute: false);

                // public class Inner3<U>
                ValidateDynamicAttribute(inner3Class, expectedDynamicAttribute: false);
            }

            private void ValidateAttributesOnFields()
            {
                bool[] expectedTransformFlags;

                //public static dynamic field1;
                var field1 = derivedClass.GetMember<FieldSymbol>("field1");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(field1, expectedDynamicAttribute: true);

                //public static dynamic[] field2;
                var field2 = derivedClass.GetMember<FieldSymbol>("field2");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(field2, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public static dynamic[][] field3;
                var field3 = derivedClass.GetMember<FieldSymbol>("field3");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 03 00 00 00 * 00 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true };
                ValidateDynamicAttribute(field3, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public const dynamic field4 = null;
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                var field4 = derivedClass.GetMember<FieldSymbol>("field4");
                ValidateDynamicAttribute(field4, expectedDynamicAttribute: true);

                //public const dynamic[] field5 = null;
                var field5 = derivedClass.GetMember<FieldSymbol>("field5");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(field5, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public const dynamic[][] field6 = null;
                var field6 = derivedClass.GetMember<FieldSymbol>("field6");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 03 00 00 00 * 00 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true };
                ValidateDynamicAttribute(field6, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public const dynamic[][] field7 = null;
                var field7 = derivedClass.GetMember<FieldSymbol>("field7");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 03 00 00 00 * 00 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true };
                ValidateDynamicAttribute(field7, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<T>.Inner<int, T>.InnerInner<Outer<dynamic>> field8 = null;
                var field8 = derivedClass.GetMember<FieldSymbol>("field8");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 06 00 00 00 * 00 00 00 00 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, false, false, true };
                ValidateDynamicAttribute(field8, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<dynamic>.Inner<T, T>.InnerInner<T> field9 = null;
                var field9 = derivedClass.GetMember<FieldSymbol>("field9");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 05 00 00 00 * 00 01 00 00 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true, false, false, false };
                ValidateDynamicAttribute(field9, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<Outer<dynamic>.Inner<T, dynamic>>.Inner<dynamic, T>.InnerInner<T> field10 = null;
                var field10 = derivedClass.GetMember<FieldSymbol>("field10");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 08 00 00 00 * 00 00 01 00 01 01 00 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true, false, true, true, false, false };
                ValidateDynamicAttribute(field10, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<T>.Inner<dynamic, dynamic>.InnerInner<T> field11 = null;
                var field11 = derivedClass.GetMember<FieldSymbol>("field11");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 05 00 00 00 * 00 00 01 01 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true, true, false };
                ValidateDynamicAttribute(field11, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<T>.Inner<T, T>.InnerInner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>> field12 = null;
                var field12 = derivedClass.GetMember<FieldSymbol>("field12");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 09 00 00 00 * 00 00 00 00 00 01 00 01 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, false, false, true, false, true, false };
                ValidateDynamicAttribute(field12, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<dynamic>.Inner<Outer<T>, T>.InnerInner<dynamic> field13 = null;
                var field13 = derivedClass.GetMember<FieldSymbol>("field13");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 06 00 00 00 * 00 01 00 00 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true, false, false, false, true };
                ValidateDynamicAttribute(field13, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;
                var field14 = derivedClass.GetMember<FieldSymbol>("field14");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 05 00 00 00 * 00 01 01 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true, true, true, true };
                ValidateDynamicAttribute(field14, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<dynamic>.Inner<Outer<dynamic>, T>.InnerInner<dynamic>[] field15 = null;
                var field15 = derivedClass.GetMember<FieldSymbol>("field15");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 07 00 00 00 * 00 00 01 00 01 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, true, false, true, false, true };
                ValidateDynamicAttribute(field15, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                var field16 = derivedClass.GetMember<FieldSymbol>("field16");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0C 00 00 00 * 00 00 00 01 00 01 00 01 00 00 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, true, false, true, false, true, false, false, true, true };
                ValidateDynamicAttribute(field16, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                var field17 = derivedClass.GetMember<FieldSymbol>("field17");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, true, false, false, true, false, false, true, false, true, true };
                ValidateDynamicAttribute(field17, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public static Outer3.Inner3<dynamic> field1 = null;
                field1 = inner3Class.GetMember<FieldSymbol>("field1");
                //   .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(field1, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
            }

            private void ValidateAttributesOnMethodReturnValueAndParameters()
            {
                //public static dynamic F1(dynamic x) { return x; }
                var f1 = derivedClass.GetMember<MethodSymbol>("F1");
                ValidateDynamicAttribute(f1, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(f1, forReturnType: true, expectedDynamicAttribute: true);
                ValidateDynamicAttribute(f1.Parameters[0], expectedDynamicAttribute: true);

                //public static dynamic F2(ref dynamic x) { return x; }
                var f2 = derivedClass.GetMember<MethodSymbol>("F2");
                ValidateDynamicAttribute(f2, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 )
                ValidateDynamicAttribute(f2, forReturnType: true, expectedDynamicAttribute: true);
                expectedTransformFlags = new bool[] { false, true };
                ValidateDynamicAttribute(f2.Parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public static dynamic[] F3(dynamic[] x) { return x; }
                var f3 = derivedClass.GetMember<MethodSymbol>("F3");
                ValidateDynamicAttribute(f3, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                ValidateDynamicAttribute(f3, forReturnType: true, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                ValidateDynamicAttribute(f3.Parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                //public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                var f4 = derivedClass.GetMember<MethodSymbol>("F4");
                ValidateDynamicAttribute(f4, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, true, false, false, true, false, false, true, false, true, true };
                ValidateDynamicAttribute(f4, forReturnType: true, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                ValidateDynamicAttribute(f4.Parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
            }

            private void ValidateAttributesOnProperty()
            {
                //public static dynamic Prop1 { get { return field1; } }
                var prop1 = derivedClass.GetMember<PropertySymbol>("Prop1");
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(prop1, expectedDynamicAttribute: true);
                
                // GetMethod
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(prop1.GetMethod, forReturnType: true, expectedDynamicAttribute: true);

                //public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                var prop2 = derivedClass.GetMember<PropertySymbol>("Prop2");
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, true, false, false, true, false, false, true, false, true, true };
                ValidateDynamicAttribute(prop2, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                
                // GetMethod
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                ValidateDynamicAttribute(prop2.GetMethod, forReturnType: true, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                
                // SetMethod
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 0D 00 00 00 * 00 00 00 01 00 00 01 00 00 01 00 01 01 * 00 00 ) 
                ValidateDynamicAttribute(prop2.SetMethod.Parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
            }

            private void ValidateAttributesOnIndexer()
            {
                // public dynamic this[dynamic param]
                var indexer = derivedClass.GetIndexer<PropertySymbol>("Item");
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(indexer, expectedDynamicAttribute: true);
                
                // GetMethod
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                ValidateDynamicAttribute(indexer.GetMethod, forReturnType: true, expectedDynamicAttribute: true);
                ValidateDynamicAttribute(indexer.GetMethod.Parameters[0], expectedDynamicAttribute: true);
                
                // SetMethod
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 ) 
                //.param [2]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor() = ( 01 00 00 00 )
                ValidateDynamicAttribute(indexer.SetMethod, forReturnType: true, expectedDynamicAttribute: false);
                ValidateDynamicAttribute(indexer.SetMethod.Parameters[0], expectedDynamicAttribute: true);
                ValidateDynamicAttribute(indexer.SetMethod.Parameters[1], expectedDynamicAttribute: true);
            }

            private void ValidateAttributesForPointerType()
            {
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                // .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 14 00 00 00 * 00 00 00 00 00 00 00 01 00 00 01 00 00 01 00 00 00 00 01 01 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, false, false, false, false, false, false, true, false, false, true, false, false, true, false, false, false, false, true, true };
                Assert.False(unsafeClass.ContainsDynamic());
                Assert.True(unsafeClass.BaseType.ContainsDynamic()); 
                ValidateDynamicAttribute(unsafeClass, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
            }

            private void ValidateAttributesForNullableType()
            {
                // public static Outer<dynamic>.Inner<dynamic, Struct?> nullableField;
                var nullableField = structType.GetMember<FieldSymbol>("nullableField");
                // .custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 05 00 00 00 * 00 01 01 00 00 * 00 00 ) 
                expectedTransformFlags = new bool[] { false, true, true, false, false };
                ValidateDynamicAttribute(nullableField, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
            }

            private void ValidateAttributesForSynthesizedDelegateMembers()
            {
                // public delegate dynamic[] MyDelegate(dynamic[] x);

                // .class public auto ansi sealed MyDelegate
                //      extends [mscorlib]System.MulticastDelegate
                ValidateDynamicAttribute(synthesizedMyDelegateType, expectedDynamicAttribute: false);

                var expectedTransformFlags = new bool[] { false, true };

                // MyDelegate::.ctor
                //
                // .method public hidebysig specialname rtspecialname 
                //  instance void  .ctor(object 'object',
                //                    native int 'method') runtime managed
                var ctor = synthesizedMyDelegateType.InstanceConstructors[0];
                ValidateDynamicAttribute(ctor, expectedDynamicAttribute: false);
                ValidateDynamicAttribute(ctor, forReturnType: true, expectedDynamicAttribute: false);
                foreach (var param in ctor.Parameters)
                {
                    ValidateDynamicAttribute(param, expectedDynamicAttribute: false);
                }

                // Invoke method
                // 
                //  .method public hidebysig newslot virtual 
                //      instance object[]  Invoke(object[] x) runtime managed
                var invokeMethod = synthesizedMyDelegateType.GetMember<MethodSymbol>("Invoke");
                ValidateDynamicAttribute(invokeMethod, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                ValidateDynamicAttribute(invokeMethod, forReturnType: true, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                ValidateDynamicAttribute(invokeMethod.Parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);

                // BeginInvoke method
                //
                // .method public hidebysig newslot virtual 
                // instance class [mscorlib]System.IAsyncResult 
                //  BeginInvoke(object[] x,
                //      class [mscorlib]System.AsyncCallback callback,
                //      object 'object') runtime managed
                var beginInvokeMethod = synthesizedMyDelegateType.GetMember<MethodSymbol>("BeginInvoke");
                ValidateDynamicAttribute(beginInvokeMethod, expectedDynamicAttribute: false);
                ValidateDynamicAttribute(beginInvokeMethod, forReturnType: true, expectedDynamicAttribute: false);
                //.param [1]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                var parameters = beginInvokeMethod.Parameters;
                ValidateDynamicAttribute(parameters[0], expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                ValidateDynamicAttribute(parameters[1], expectedDynamicAttribute: false);
                ValidateDynamicAttribute(parameters[2], expectedDynamicAttribute: false);

                // EndInvoke method
                //
                // .method public hidebysig newslot virtual 
                // instance object[]  EndInvoke(class [mscorlib]System.IAsyncResult result) runtime managed
                var endInvokeMethod = synthesizedMyDelegateType.GetMember<MethodSymbol>("EndInvoke");
                ValidateDynamicAttribute(endInvokeMethod, expectedDynamicAttribute: false);
                //.param [0]
                //.custom instance void [System.Core]System.Runtime.CompilerServices.DynamicAttribute::.ctor(bool[]) = ( 01 00 02 00 00 00 * 00 01 * 00 00 ) 
                ValidateDynamicAttribute(endInvokeMethod, forReturnType: true, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformFlags);
                ValidateDynamicAttribute(endInvokeMethod.Parameters[0], expectedDynamicAttribute: false);
            }
        }

        [Fact]
        public void CS1980ERR_DynamicAttributeMissing()
        {
            var comp = CreateCompilationWithMscorlib(DynamicTestSource, compOptions: TestOptions.UnsafeDll);
            comp.VerifyDiagnostics(
                // (14,32): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Outer2<T> : Base1<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,33): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,54): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,74): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,102): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,122): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Derived<T> : Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (6,31): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Outer<T> : Base1<dynamic>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (8,38): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public class Inner<U, V> : Base2<dynamic, V>
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (10,44): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //         public class InnerInner<W> : Base1<dynamic> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (76,17): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public delegate dynamic[] MyDelegate(dynamic[] x);
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (76,38): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public delegate dynamic[] MyDelegate(dynamic[] x);
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (69,58): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (69,79): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (69,99): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (69,132): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (69,152): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public unsafe class UnsafeClass<T> : Base2<int*[], Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int*[][]>[], dynamic>.InnerInner<dynamic>[][]> { }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (54,30): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic F1(dynamic x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (54,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic F1(dynamic x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (55,34): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic F2(ref dynamic x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (55,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic F2(ref dynamic x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (56,32): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic[] F3(dynamic[] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (56,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic[] F3(dynamic[] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,136): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,157): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,177): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,205): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,225): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,25): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,46): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,66): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,94): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (57,114): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] F4(Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] x) { return x; }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (62,25): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public dynamic this[dynamic param]
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (26,37): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //         public static Outer3.Inner3<dynamic> field1 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (62,12): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public dynamic this[dynamic param]
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (34,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic[] field2;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (35,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic[][] field3;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (37,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public const dynamic field4 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (38,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public const dynamic[] field5 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (39,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public const dynamic[][] field6 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (40,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public const dynamic[][] field7 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (73,25): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<dynamic, Struct?> nullableField;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (73,40): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<dynamic, Struct?> nullableField;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (42,52): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<T>.Inner<int, T>.InnerInner<Outer<dynamic>> field8 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (43,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<T, T>.InnerInner<T> field9 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (44,24): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<Outer<dynamic>.Inner<T, dynamic>>.Inner<dynamic, T>.InnerInner<T> field10 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (44,42): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<Outer<dynamic>.Inner<T, dynamic>>.Inner<dynamic, T>.InnerInner<T> field10 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (44,58): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<Outer<dynamic>.Inner<T, dynamic>>.Inner<dynamic, T>.InnerInner<T> field10 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (45,27): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<T>.Inner<dynamic, dynamic>.InnerInner<T> field11 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (45,36): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<T>.Inner<dynamic, dynamic>.InnerInner<T> field11 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (46,50): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<T>.Inner<T, T>.InnerInner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>> field12 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (46,68): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<T>.Inner<T, T>.InnerInner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>> field12 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (47,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<T>, T>.InnerInner<dynamic> field13 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (47,57): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<T>, T>.InnerInner<dynamic> field13 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (48,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (48,33): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (48,42): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (48,62): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<dynamic, dynamic>.InnerInner<dynamic> field14 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (50,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>, T>.InnerInner<dynamic>[] field15 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (50,39): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>, T>.InnerInner<dynamic>[] field15 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (50,63): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>, T>.InnerInner<dynamic>[] field15 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (51,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (51,39): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (51,57): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (51,83): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (51,105): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public Outer<dynamic>.Inner<Outer<dynamic>.Inner<T, dynamic>.InnerInner<int>, dynamic[]>.InnerInner<dynamic>[][] field16 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (52,25): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (52,46): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (52,66): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (52,94): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (52,114): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] field17 = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (59,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic Prop1 { get { return field1; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (60,25): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (60,46): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (60,66): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (60,94): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (60,114): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static Outer<dynamic>.Inner<Outer<dynamic>.Inner<T[], dynamic>.InnerInner<int>[], dynamic>.InnerInner<dynamic>[][] Prop2 { get { return field17; } set { field17 = value; } }
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (33,19): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public static dynamic field1;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));
        }

        private static string NoCS1980String = @"
[Attr(typeof(%TYPENAME%))]            // No CS1980
public class Gen<T>
{
  public object f = typeof(%TYPENAME%);  // No CS1980
  public const object Const = null;

  private void M([Attr(Gen<dynamic>.Const)]object param = Gen<dynamic>.Const)     // No CS1980
  {
    %TYPENAME% x = null;             // No CS1980
    System.Console.WriteLine(x);
    object y = typeof(%TYPENAME%);   // No CS1980
  }
}

class Attr: System.Attribute
{
  public Attr(object x) {}
}
";
        private static string GetNoCS1980String(string typeName)
        {
            return NoCS1980String.Replace("%TYPENAME%", typeName);
        }

        [Fact]
        public void TestNoCS1980WhenNotInContextWhichNeedsDynamicAttribute()
        {
            // Regular mode
            TestNoCS1980WhenNotInContextWhichNeedsDynamicAttribute(parseOptions: TestOptions.Regular);

            // Script
            TestNoCS1980WhenNotInContextWhichNeedsDynamicAttribute(parseOptions: TestOptions.Script);

            // Interactive
            TestNoCS1980WhenNotInContextWhichNeedsDynamicAttribute(parseOptions: TestOptions.Interactive);
        }

        private void TestNoCS1980WhenNotInContextWhichNeedsDynamicAttribute(CSharpParseOptions parseOptions)
        {
            var source = GetNoCS1980String(typeName: @"dynamic");
            var comp = CreateCompilationWithMscorlib(source, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (4,7): error CS1962: The typeof operator cannot be used on the dynamic type
                // [Attr(typeof(dynamic))]            // No CS1980
                Diagnostic(ErrorCode.ERR_BadDynamicTypeof, "typeof(dynamic)"),
                // (7,21): error CS1962: The typeof operator cannot be used on the dynamic type
                //   public object f = typeof(dynamic);  // No CS1980
                Diagnostic(ErrorCode.ERR_BadDynamicTypeof, "typeof(dynamic)"),
                // (13,16): error CS1962: The typeof operator cannot be used on the dynamic type
                //     object y = typeof(dynamic);   // No CS1980
                Diagnostic(ErrorCode.ERR_BadDynamicTypeof, "typeof(dynamic)"));

            source = GetNoCS1980String(typeName: @"Gen<dynamic>");
            comp = CreateCompilationWithMscorlib(source, parseOptions: parseOptions);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp);
        }

        [Fact]
        public void TestDynamicAttributeInAliasContext()
        {
            // Regular mode
            TestDynamicAttributeInAliasContext(parseOptions: TestOptions.Regular);

            // Script
            TestDynamicAttributeInAliasContext(parseOptions: TestOptions.Script);

            // Interactive
            TestDynamicAttributeInAliasContext(parseOptions: TestOptions.Interactive);
        }

        private void TestDynamicAttributeInAliasContext(CSharpParseOptions parseOptions)
        {
            // Dynamic type in Alias target
            string aliasDecl = @"using X = Gen<dynamic>;     // No CS1980";
            
            // NO ERROR CASES
            string source = aliasDecl + GetNoCS1980String(typeName: "X");
            var comp = CreateCompilationWithMscorlib(source, parseOptions: parseOptions);
            comp.VerifyDiagnostics();
            CompileAndVerify(comp);

            // ERROR CASES
            string source2 = source + @"
public class Gen2<T> : X    // CS1980
{
  public X field = null;   // CS1980

  private X Method(X param) // CS1980, CS1980
  {
     return param;
  }

  private X Prop { get; set; } // CS1980

  private X this[X param]   // CS1980, CS1980
  {
    get { return null; }
    set {}
  }
}";
            comp = CreateCompilationWithMscorlib(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (21,24): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // public class Gen2<T> : X    // CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (25,20): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   private X Method(X param) // CS1980, CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (25,11): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   private X Method(X param) // CS1980, CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (32,18): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   private X this[X param]   // CS1980, CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (32,11): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   private X this[X param]   // CS1980, CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (30,11): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   private X Prop { get; set; } // CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"),
                // (23,10): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //   public X field = null;   // CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));
        }

        [Fact]
        public void TestDynamicAttributeForSubmissionField()
        {
            // Script
            TestDynamicAttributeForSubmissionField(parseOptions: TestOptions.Script);

            // Interactive
            TestDynamicAttributeForSubmissionField(parseOptions: TestOptions.Interactive);
        }

        private void TestDynamicAttributeForSubmissionField(CSharpParseOptions parseOptions)
        {
            string source = GetNoCS1980String(typeName: @"Gen<dynamic>");
            var comp = CreateCompilationWithMscorlib(source, parseOptions: parseOptions);
            comp.VerifyDiagnostics();

            // Dynamic type field
            string source2 = @"
dynamic x = 0;";
            comp = CreateCompilationWithMscorlibAndSystemCore(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics();
            var implicitField = comp.ScriptClass.GetMember<FieldSymbol>("x");
            DynamicAttributeValidator.ValidateDynamicAttribute(implicitField, comp, expectedDynamicAttribute: true);

            // No reference to System.Core, generates CS1980
            comp = CreateCompilationWithMscorlib(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (2,1): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // dynamic x = 0;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));


            // Field type is constructed generic type with dynamic type argument
            source2 = source + @"
Gen<dynamic> x = null;";
            comp = CreateCompilationWithMscorlibAndSystemCore(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics();
            implicitField = comp.ScriptClass.GetMember<FieldSymbol>("x");
            var expectedTransformsFlags = new bool[] { false, true };
            DynamicAttributeValidator.ValidateDynamicAttribute(implicitField, comp, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformsFlags);

            // No reference to System.Core, generates CS1980
            comp = CreateCompilationWithMscorlib(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (20,5): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // Gen<dynamic> x = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));


            // Dynamic type in Alias target
            string aliasDecl = @"using X = Gen<dynamic>;     // No CS1980";
            source2 = aliasDecl + source + @"
X x = null;";
            comp = CreateCompilationWithMscorlibAndSystemCore(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics();
            implicitField = comp.ScriptClass.GetMember<FieldSymbol>("x");
            DynamicAttributeValidator.ValidateDynamicAttribute(implicitField, comp, expectedDynamicAttribute: true, expectedTransformFlags: expectedTransformsFlags);

            // No reference to System.Core, generates CS1980
            comp = CreateCompilationWithMscorlib(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (20,1): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                // X x = null;
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "X").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));
        }

        [Fact]
        public void TestDynamicAttributeForSubmissionGlobalStatement()
        {
            // Script
            TestDynamicAttributeForSubmissionGlobalStatement(parseOptions: TestOptions.Script);

            // Interactive
            TestDynamicAttributeForSubmissionGlobalStatement(parseOptions: TestOptions.Interactive);
        }

        private void TestDynamicAttributeForSubmissionGlobalStatement(CSharpParseOptions parseOptions)
        {
            // Ensure no CS1980 for use of dynamic in global statement
            string aliasDecl = @"using X = Gen<dynamic>;     // No CS1980";
            string source = GetNoCS1980String(typeName: @"Gen<dynamic>");
            string source2 = aliasDecl + source + @"
System.Console.WriteLine(typeof(dynamic));
System.Console.WriteLine(typeof(Gen<dynamic>));
System.Console.WriteLine(typeof(X));";
            var comp = CreateCompilationWithMscorlib(source2, parseOptions: parseOptions);
            comp.VerifyDiagnostics(
                // (20,26): error CS1962: The typeof operator cannot be used on the dynamic type
                // System.Console.WriteLine(typeof(dynamic));
                Diagnostic(ErrorCode.ERR_BadDynamicTypeof, "typeof(dynamic)"));
        }

        [Fact, WorkItem(531108, "DevDiv")]
        public void DynamicAttributeCtorCS1980BreakingChange()
        {
            var customDynamicAttrSource = @"
namespace System
{
    namespace Runtime
    {
        namespace CompilerServices
        {
            public class DynamicAttribute : Attribute
            {
                public DynamicAttribute() {}
            }
        }
    }
}";
            var customRef = CreateCompilationWithMscorlib(customDynamicAttrSource).ToMetadataReference();

            var source = @"
public class C<T>
{
    public C<dynamic> field2;   // Uses missing ctor ""DynamicAttribute(bool[] transformFlags)"", generates CS1980
}";
            var comp = CreateCompilationWithMscorlib(source, references: new[] { customRef });
            comp.VerifyDiagnostics(
                // (4,14): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public C<dynamic> field2;   // Uses missing ctor "DynamicAttribute(bool[] transformFlags)", generates CS1980
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute"));

            source = @"
public class C<T>
{
    public dynamic field1;      // Uses available ctor ""DynamicAttribute()"", No CS1980 in native compiler.
}";
            // Bug 531108-Won't Fix
            comp = CreateCompilationWithMscorlib(source, references: new[] { customRef });
            comp.VerifyDiagnostics(
                // (4,12): error CS1980: Cannot define a class or member that utilizes 'dynamic' because the compiler required type 'System.Runtime.CompilerServices.DynamicAttribute' cannot be found. Are you missing a reference?
                //     public dynamic field1;      // Uses available ctor "DynamicAttribute()", No CS1980 in native compiler.
                Diagnostic(ErrorCode.ERR_DynamicAttributeMissing, "dynamic").WithArguments("System.Runtime.CompilerServices.DynamicAttribute")
             );
        }

        [Fact]
        public void ExplicitDynamicAttribute()
        {
            var text = @"
using System.Runtime.CompilerServices;

[Dynamic(new[] { true })]
public class C
{
    [Dynamic(new[] { true })]
    public object F = null;

    [Dynamic(new[] { true })]
    public object P { get; set; }
    
    [return: Dynamic(new[] { true })]
    public void M([Dynamic(new[] { true })]object a) 
    {
    }
}

[Dynamic(new bool[] { true })]
public struct S { }
";
            CreateCompilationWithMscorlibAndSystemCore(text).VerifyDiagnostics(
                // (4,2): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new[] { true })"),
                // (19,2): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new bool[] { true })"),
                // (10,6): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new[] { true })"),
                // (13,14): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new[] { true })"),
                // (14,20): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new[] { true })"),
                // (7,6): error CS1970: Do not use 'System.Runtime.CompilerServices.DynamicAttribute'. Use the 'dynamic' keyword instead.
                Diagnostic(ErrorCode.ERR_ExplicitDynamicAttr, "Dynamic(new[] { true })"));
        }

        [Fact]
        public void DynamicAttributeType()
        {
            var text = @"
[dynamic]
public class C
{
    [return: dynamic]
    [dynamic]
    public void dynamic([dynamic]dynamic dynamic) { }
}
";
            CreateCompilationWithMscorlibAndSystemCore(text).VerifyDiagnostics(
                // (2,2): error CS0246: The type or namespace name 'dynamic' could not be found (are you missing a using directive or an assembly reference?)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "dynamic").WithArguments("dynamic"),
                // (6,6): error CS0246: The type or namespace name 'dynamic' could not be found (are you missing a using directive or an assembly reference?)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "dynamic").WithArguments("dynamic"),
                // (5,14): error CS0246: The type or namespace name 'dynamic' could not be found (are you missing a using directive or an assembly reference?)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "dynamic").WithArguments("dynamic"),
                // (7,26): error CS0246: The type or namespace name 'dynamic' could not be found (are you missing a using directive or an assembly reference?)
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "dynamic").WithArguments("dynamic"));
        }

        [Fact]
        [WorkItem(552843, "DevDiv")]
        public void IteratorYieldingDynamic()
        {
            string source = @"
using System.Collections.Generic;
 
class C
{
    static IEnumerable<dynamic> Foo()
    {
        yield break;
    }
}
";
            CompileAndVerify(source, additionalRefs: new[] { CSharpRef, SystemCoreRef }, options: TestOptions.Dll.WithMetadataImportOptions(MetadataImportOptions.All), symbolValidator: module =>
            {
                var c = module.GlobalNamespace.GetMember<NamedTypeSymbol>("C");
                var iterator = c.GetMember<NamedTypeSymbol>("<Foo>d__0");
                var getEnumerator = iterator.GetMember<MethodSymbol>("System.Collections.Generic.IEnumerable<dynamic>.GetEnumerator");
                var attrs = getEnumerator.GetAttributes();

                foreach (var attr in attrs)
                {
                    switch (attr.AttributeClass.Name)
                    {
                        case "DebuggerHiddenAttribute":
                            break;

                        case "DynamicAttribute":
                            var values = attr.ConstructorArguments.Single().Values.ToArray();
                            Assert.Equal(2, values.Length);
                            Assert.Equal(false, values[0].Value);
                            Assert.Equal(true, values[1].Value);
                            break;

                        default:
                            throw TestExceptionUtilities.UnexpectedValue(attr.AttributeClass.Name);
                    }
                }
            });
        }
    }
}